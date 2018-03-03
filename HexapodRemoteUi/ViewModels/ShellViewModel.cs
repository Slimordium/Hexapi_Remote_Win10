using System;
using System.Collections.Generic;
using System.IO;
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Caliburn.Micro;
using Hexapod;
using Hexapod.Enums;
using Hexapod.IK;
using Newtonsoft.Json;
using RxMqtt.Client;
using RxMqtt.Shared;

namespace HexapodRemoteUi.ViewModels{
    public class ShellViewModel : Conductor<object>
    {
        public string Log { get; set; }

        public List<string> GaitType { get; set; } = new List<string> { "Tripod8", "TripleTripod12", "TripleTripod16", "Wave24", "Ripple12" };

        public string GaitTypeSelectedValue { get; set; } = "Tripod8";

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

        private IObservable<long> _updateInterval;

        private IDisposable _disposable;

        private XboxController _xboxController;

        public WriteableBitmap HexImage { get; set; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private IDisposable _ikDisposable;

        public ShellViewModel()
        {
        }

        public async Task SetUpdateInterval()
        {
            if (!StreamChanges)
            {
                _disposable?.Dispose();

                _cancellationTokenSource.Cancel();

                _cancellationTokenSource = new CancellationTokenSource();

                return;
            }

            _xboxController = new XboxController();

            await _xboxController.InitializeAsync(_cancellationTokenSource.Token);

            if (_xboxController.IsConnected)
            {
                _ikDisposable?.Dispose();

                _ikDisposable = _xboxController.IkParamSubject.Distinct()
                                    .AsObservable()
                                    .Sample(TimeSpan.FromMilliseconds(Convert.ToInt64(UpdateInterval)))
                                    .Subscribe(ik => _mqttClient.PublishAsync(JsonConvert.SerializeObject(ik), "hex-ik").ToObservable().Subscribe());

                    AddToLog($"Publishing Xbox events every {UpdateInterval}ms");
            }
            else
            {
                AddToLog($"xBox controller not connected");
                return;
            }

            _disposable?.Dispose();
        }

        private async Task OnNextXboxEvent(IkParams ikParams)
        {
            await _mqttClient.PublishAsync(JsonConvert.SerializeObject(ikParams), "hex-ik");
        }

        private void AddToLog(string logEntry)
        {
            Log = logEntry + Environment.NewLine + Log;

            NotifyOfPropertyChange(nameof(Log));
        }

        public async Task BrokerConnect()
        {
            try
            {
                if (_mqttClient == null)
                {
                    _mqttClient = new MqttClient($"HexRemote-{DateTime.Now.Millisecond}", BrokerIp, 1883, 64000, _cancellationTokenSource.Token);

                    var result = await _mqttClient.InitializeAsync();

                    _mqttClient.Subscribe(ImageHandler, "hex-eye");

                    AddToLog($"Connection {result}");
                }
                else
                    AddToLog("Already connected");

            }
            catch (Exception e)
            {
                AddToLog(e.Message);
            }
        }

        private static async Task<IRandomAccessStream> ReencodeAndSavePhotoAsync1(IRandomAccessStream stream, PhotoOrientation photoOrientation)
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

        private async void ImageHandler(byte[] bytes)
        {
            //Run on UI thread
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                async () =>
                {
                    try
                    {
                        var imageBuffer = bytes.AsBuffer().AsStream().AsRandomAccessStream();
                      
                        //using (var image = await ReencodeAndSavePhotoAsync1(imageBuffer.AsStream().AsRandomAccessStream(), PhotoOrientation.Normal))
                        //{
                            var decoder = await BitmapDecoder.CreateAsync(imageBuffer);
                            imageBuffer.Seek(0);

                            var output = new WriteableBitmap((int)decoder.PixelHeight, (int)decoder.PixelWidth);
                            await output.SetSourceAsync(imageBuffer);

                            HexImage = output;

                            NotifyOfPropertyChange(nameof(HexImage));
                        //}
                    }
                    catch (Exception e)
                    {
                        AddToLog(e.Message);
                    }
                });
        }

        public async Task PublishUpdate()
        {
            Enum.TryParse(typeof(GaitType), GaitTypeSelectedValue, true, out var gaitType);

            var ikParams = new IkParams(true)
            {
                GaitType = (GaitType) gaitType,
                LegLiftHeight = LegLiftHeight,
                GaitSpeedMs = GaitSpeed,
                BodyPositionY = BodyHeight
            };

            await _mqttClient.PublishAsync(JsonConvert.SerializeObject(ikParams), "hex/ik").ConfigureAwait(false);
        }

        public async Task PublishMessage()
        {
            if (string.IsNullOrEmpty(PubMessage) || string.IsNullOrEmpty(PubTopic))
            {
                AddToLog("Please enter message and topic first");
                return;
            }

            await _mqttClient.PublishAsync(PubMessage, PubTopic);
        }

        public void Subscribe()
        {
            if (string.IsNullOrEmpty(SubTopic))
            {
                AddToLog("Need a topic first");
                return;
            }

            _mqttClient.Subscribe(IncomingPublish, SubTopic);

            AddToLog($"Subscribed to {SubTopic}");
        }

        private void IncomingPublish(string s)
        {
         
            AddToLog(s);
        }
    }
}