using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Caliburn.Micro;
using Hardware.Xbox;
using Hexapi.Shared;
using Hexapi.Shared.Ik;
using Hexapi.Shared.Ik.Enums;
using Hexapi.Shared.Imu;
using Newtonsoft.Json;
using RxMqtt.Client;

namespace Hexapi.Remote.ViewModels{
    public class ShellViewModel : Conductor<object>
    {
        public string Log { get; set; }

        public List<string> GaitType { get; set; } = new List<string> { "Tripod8", "TripleTripod12", "TripleTripod16", "Wave24", "Ripple12" };

        public string GaitTypeSelectedValue { get; set; } = "TripleTripod16";

        public string BrokerIp { get; set; } = "127.0.0.1";

        public string SubTopic { get; set; }

        public string PubTopic { get; set; }

        public string PubMessage { get; set; }

        private MqttClient _mqttClient;

        public int GaitSpeed { get; set; } = 30;
        public int BodyHeight { get; set; } = 60;
        public int LegLiftHeight { get; set; } = 60;
        public int UpdateInterval { get; set; } = 500;

        public bool StreamChanges { get; set; }


        private XboxIkController _xboxController;

        public WriteableBitmap HexImage { get; set; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private List<IDisposable> _disposables = new List<IDisposable>();

        private double _ax;
        public double Ax
        {
            get => _ax;
            set
            {
                _ax = value;
                NotifyOfPropertyChange(nameof(Ax));
            }
        }

        private double _ay;
        public double Ay
        {
            get => _ay;
            set
            {
                _ay = value;
                NotifyOfPropertyChange(nameof(Ay));
            }
        }

        private double _az;
        public double Az
        {
            get => _az;
            set
            {
                _az = value;
                NotifyOfPropertyChange(nameof(Az));
            }
        }

        private double _yaw;
        public double Yaw
        {
            get => _yaw;
            set
            {
                _yaw = value;
                NotifyOfPropertyChange(nameof(Yaw));
            }
        }


        private double _pitch;
        public double Pitch
        {
            get => _pitch;
            set
            {
                _pitch = value;
                NotifyOfPropertyChange(nameof(Pitch));
            }
        }

        private double _roll;
        public double Roll
        {
            get => _roll;
            set
            {
                _roll = value;
                NotifyOfPropertyChange(nameof(Roll));
            }
        }

        private double _inches;
        public double Inches
        {
            get => _inches;
            set
            {
                _inches = value;
                NotifyOfPropertyChange(nameof(Inches));
            }
        }

        public ShellViewModel()
        {
        }

        public async Task SetUpdateInterval()
        {
            if (!StreamChanges)
            {
                foreach (var disposable in _disposables)
                {
                    disposable.Dispose();
                }

                _disposables = new List<IDisposable>();

                _cancellationTokenSource.Cancel();

                _cancellationTokenSource = new CancellationTokenSource();

                return;
            }

            _xboxController = new XboxIkController();

            await _xboxController.InitializeAsync(_cancellationTokenSource.Token);

            if (_xboxController.IsConnected)
            {

                _disposables.Add(_xboxController.IkParamSubject
                                    .Distinct()
                                    .AsObservable()
                                    .Sample(TimeSpan.FromMilliseconds(Convert.ToInt64(UpdateInterval)))
                                    .SubscribeOn(Scheduler.Default)
                                    .Subscribe(ik => _mqttClient.PublishAsync(JsonConvert.SerializeObject(ik), "hex-ik").ToObservable().Subscribe()));

                await AddToLog($"Publishing Xbox events every {UpdateInterval}ms");
            }
            else
            {
                await AddToLog($"xBox controller not connected");
            }
        }

        private async Task AddToLog(string logEntry)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Log = logEntry + Environment.NewLine + Log;

                    NotifyOfPropertyChange(nameof(Log));
                });
        }

        public async Task BrokerConnect()
        {
            try
            {
                if (_mqttClient == null)
                {
                    _mqttClient = new MqttClient($"HexRemote-{DateTime.Now.Millisecond}", BrokerIp, 1883, 60000, _cancellationTokenSource.Token);

                    var result = await _mqttClient.InitializeAsync();

                    _mqttClient.Subscribe(ImageHandler, "hex-eye");

                    _mqttClient.Subscribe(Imu, "hex-imu");

                    _mqttClient.Subscribe(Sonar, "hex-sonar");

                    await AddToLog($"Connection {result}");
                }
                else
                    await AddToLog("Already connected");

            }
            catch (Exception e)
            {
                await AddToLog(e.Message);
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
            catch (Exception e)
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
            catch (Exception e)
            {
                //Dont care
            }
        }

        private async void ImageHandler(byte[] bytes)
        {
            //Run on UI thread
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    try
                    {
                        using (var imageBuffer = bytes.AsBuffer().AsStream().AsRandomAccessStream())
                        {
                            using (var b = await Reencode(imageBuffer, PhotoOrientation.Rotate180))
                            {
                                var decoder = await BitmapDecoder.CreateAsync(b);
                                b.Seek(0);

                                HexImage = new WriteableBitmap((int)decoder.PixelHeight, (int)decoder.PixelWidth);
                                await HexImage.SetSourceAsync(b);

                                NotifyOfPropertyChange(nameof(HexImage));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        await AddToLog(e.Message);
                    }
            });
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
                        new BitmapTransform { ScaledHeight = decoder.PixelHeight, ScaledWidth = decoder.PixelWidth },
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied, decoder.PixelWidth, decoder.PixelHeight, 96, 96, pixelData.DetachPixelData());

                    var properties = new BitmapPropertySet
                    {
                        { "System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16) },
                    };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                    await encoder.FlushAsync();

                    randomAccessStream = outputStream.CloneStream();
                }
            }

            return randomAccessStream;
        }

        public async Task PublishUpdate()
        {
            Enum.TryParse(typeof(GaitType), GaitTypeSelectedValue, true, out var gaitType);

            var ikParams = new IkParams(true)
            {
                GaitType = (GaitType) gaitType,
                LegLiftHeight = LegLiftHeight,
                GaitSpeedMs = GaitSpeed,
                BodyPositionY = BodyHeight,
            };

            var ack = await _mqttClient.PublishAsync(JsonConvert.SerializeObject(ikParams), "hex/ik").ConfigureAwait(false);
        }

        public async Task PublishMessage()
        {
            if (string.IsNullOrEmpty(PubMessage) || string.IsNullOrEmpty(PubTopic))
            {
                await AddToLog("Please enter message and topic first");
                return;
            }

            var ack = await _mqttClient.PublishAsync(PubMessage, PubTopic);

            await AddToLog("PublishAck");
        }

        public async Task Subscribe()
        {
            if (string.IsNullOrEmpty(SubTopic))
            {
                await AddToLog("Need a topic first");
                return;
            }

            _mqttClient.Subscribe(IncomingPublish, SubTopic);

            await AddToLog($"Subscribed to {SubTopic}");
        }

        private void IncomingPublish(string s)
        {
            AddToLog(s).ToObservable().Subscribe();
        }
    }
}