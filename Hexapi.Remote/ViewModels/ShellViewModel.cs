using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Caliburn.Micro;
using Hardware.Xbox;
using Hexapi.Shared;
using Hexapi.Shared.Ik.Enums;
using Hexapi.Shared.Imu;
using Newtonsoft.Json;
using NLog;
using RxMqtt.Client;
using RxMqtt.Shared;

namespace Hexapi.Remote.ViewModels
{
    public class ShellViewModel : Screen
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private List<IDisposable> _disposables = new List<IDisposable>();

        private IDisposable _loggingDisposable;

        private MqttClient _mqttClient;

        private double _pitch;

        private double _roll;

        private double _sonar;

        private XboxIkController _xboxController;

        private double _yaw;
        public IObservableCollection<string> Log { get; set; } = new BindableCollection<string>();

        private readonly ILogger _logger = NLog.LogManager.GetCurrentClassLogger();

        public MediaElement MediaElement { get; } = new MediaElement();

        public string TextForSpeach { get; set; } = "Test";

        public List<string> GaitType { get; set; } = new List<string> {"Tripod8", "TripleTripod12", "TripleTripod16", "Wave24", "Ripple12"};

        public string GaitTypeSelectedValue { get; set; } = "TripleTripod16";

        public string BrokerIp { get; set; } = "127.0.0.1";

        public string SubTopic { get; set; }

        public string PubTopic { get; set; }

        public string PubMessage { get; set; }

        public int GaitSpeed { get; set; } = 30;
        public int BodyHeight { get; set; } = 60;
        public int LegLiftHeight { get; set; } = 60;
        public int UpdateInterval { get; set; } = 500;

        public bool StreamChanges { get; set; }

        public WriteableBitmap HexImage { get; set; }

        public double Yaw
        {
            get => _yaw;
            set
            {
                _yaw = value;
                NotifyOfPropertyChange(nameof(Yaw));
            }
        }

        public double Pitch
        {
            get => _pitch;
            set
            {
                _pitch = value;
                NotifyOfPropertyChange(nameof(Pitch));
            }
        }

        public double Roll
        {
            get => _roll;
            set
            {
                _roll = value;
                NotifyOfPropertyChange(nameof(Roll));
            }
        }

        public double Inches
        {
            get => _sonar;
            set
            {
                _sonar = value;
                NotifyOfPropertyChange(nameof(Inches));
            }
        }

        public async Task SetUpdateInterval()
        {
            if (!StreamChanges)
            {
                foreach (var disposable in _disposables) disposable.Dispose();

                _disposables = new List<IDisposable>();

                return;
            }

            _xboxController = new XboxIkController();

            await _xboxController.InitializeAsync(_cancellationTokenSource.Token);

            UpdateSubscriptions();
        }

        private async Task Connect()
        {
            _mqttClient = new MqttClient($"HexRemote-{DateTime.Now.Millisecond}", BrokerIp, 1883, 60000, _cancellationTokenSource.Token);

            var result = await _mqttClient.InitializeAsync();

            if (NLog.Targets.Rx.RxTarget.LogObservable != null)
            {
                AddToLog($"Subscribed to NLog RX target");

                _loggingDisposable = NLog.Targets.Rx.RxTarget
                    .LogObservable
                    .ObserveOnDispatcher()
                    .Subscribe(AddToLog);
            }

            AddToLog($"MQTT Connection => '{result}'");

            if (result != Status.Initialized)
                return;

            UpdateSubscriptions();
        }

        private void UpdateSubscriptions()
        {
            try
            {
                foreach (var disposable in _disposables)
                    disposable.Dispose();
            }
            catch (Exception)
            {
                //
            }

            _disposables = new List<IDisposable>();

            AddToLog($"Subscribing to 'hex-eye', 'hex-imu', 'hex-sonar'");

            if (_xboxController != null && _xboxController.IsConnected)
            {
                _disposables.Add(_xboxController.IkParamSubject
                    .Sample(TimeSpan.FromMilliseconds(Convert.ToInt64(UpdateInterval)))
                    .SubscribeOn(Scheduler.Default)
                    .Subscribe(ik =>
                    {
                        Enum.TryParse(typeof(GaitType), GaitTypeSelectedValue, true, out var gaitType);

                        ik.GaitType = (GaitType) gaitType;
                        ik.LegLiftHeight = LegLiftHeight;
                        ik.GaitSpeedMs = GaitSpeed;
                        ik.BodyPositionY = BodyHeight;

                        _mqttClient.PublishAsync(JsonConvert.SerializeObject(ik), "hex-ik")
                            .ToObservable()
                            .Subscribe();
                    }));

                AddToLog($"Publishing Xbox events every {UpdateInterval}ms");
            }
            else
            {
                AddToLog($"xBox controller not connected");
            }

            _disposables.Add(_mqttClient.GetPublishByteObservable("hex-eye").ObserveOnDispatcher().Subscribe(
                async buffer =>
                {
                    await ImageHandler(buffer);
                }));

            _disposables.Add(_mqttClient.GetPublishStringObservable("hex-imu").ObserveOnDispatcher().Subscribe(Imu));
            _disposables.Add(_mqttClient.GetPublishStringObservable("hex-sonar").ObserveOnDispatcher().Subscribe(Sonar));
        }

        public async Task TextToSpeech(string text)
        {
            var t = TextForSpeach;

            if (!string.IsNullOrEmpty(text))
                t = text;

            using (var speech = new SpeechSynthesizer())
            {
                speech.Voice = SpeechSynthesizer.AllVoices.First(gender => gender.Gender == VoiceGender.Female);

                var stream = await speech.SynthesizeTextToStreamAsync(t);
                MediaElement.SetSource(stream, stream.ContentType);
                MediaElement.Play();
            }
        }

        public async Task BrokerConnect()
        {
            try
            {
                if (_mqttClient == null)
                {
                    await Connect();
                }
                else
                    AddToLog("Disconnecting/reconnecting...");
            }
            catch (Exception e)
            {
                AddToLog(e.Message);
            }
        }

        private void Imu(string serializedData)
        {
            try
            {
                var imuData = JsonConvert.DeserializeObject<ImuData>(serializedData);

                Yaw = imuData.Yaw;
                Pitch = imuData.Pitch;
                Roll = imuData.Roll;
            }
            catch (Exception)
            {
                //Dont care
            }
        }

        private void Sonar(string inches)
        {
            try
            {
                Inches = Convert.ToInt32(inches);
            }
            catch (Exception)
            {
                //Dont care
            }
        }

        private async Task ImageHandler(byte[] buffer)
        {
            try
            {
                using (var imageBuffer = buffer.AsBuffer().AsStream().AsRandomAccessStream())
                {
                    using (var bitmapStream = await Reencode(imageBuffer, PhotoOrientation.Rotate180))
                    {
                        var decoder = await BitmapDecoder.CreateAsync(bitmapStream);
                        bitmapStream.Seek(0);

                        HexImage = new WriteableBitmap((int) decoder.PixelHeight, (int) decoder.PixelWidth);
                        await HexImage.SetSourceAsync(bitmapStream);

                        NotifyOfPropertyChange(nameof(HexImage));
                    }
                }
            }
            catch (Exception e)
            {
                AddToLog(e.Message);
            }
        }

        private static async Task<IRandomAccessStream> Reencode(IRandomAccessStream stream, PhotoOrientation photoOrientation)
        {
            IRandomAccessStream randomAccessStream;

            using (var inputStream = stream)
            {
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    var pixelData = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Rgba8,
                        BitmapAlphaMode.Straight,
                        new BitmapTransform {ScaledHeight = decoder.PixelHeight, ScaledWidth = decoder.PixelWidth},
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied, decoder.PixelWidth, decoder.PixelHeight, 96, 96, pixelData.DetachPixelData());

                    var properties = new BitmapPropertySet
                    {
                        {"System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16)}
                    };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                    await encoder.FlushAsync();

                    randomAccessStream = outputStream.CloneStream();
                }
            }

            return randomAccessStream;
        }

        public async Task PublishMessage()
        {
            if (string.IsNullOrEmpty(PubMessage) || string.IsNullOrEmpty(PubTopic))
            {
                AddToLog("Please enter message and topic first");
                return;
            }

            await _mqttClient.PublishAsync(PubMessage, PubTopic);

            AddToLog("PublishAck");
        }

        private void AddToLog(string message)
        {
            Log.Insert(0, message);

            if (Log.Count > 1000)
                Log.RemoveAt(1000);
        }

        public void Subscribe()
        {
            if (string.IsNullOrEmpty(SubTopic))
            {
                Log.Insert(0, "Need a topic first");
                return;
            }

            _disposables.Add(_mqttClient.GetPublishStringObservable(SubTopic)
                .ObserveOnDispatcher()
                .Subscribe(
                    async message =>
                    {
                        AddToLog(message);

                        await TextToSpeech(message);
                    }));

            _logger.Log(LogLevel.Info, $"Subscribed to {SubTopic}");
        }
    }
}