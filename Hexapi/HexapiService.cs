/*
    3DOF Hexapod - Hexapi startup 
*/

using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Hardware.Xbox;
using Hexapi.Service.Hardware;
using Hexapi.Service.IK;
using Hexapi.Service.Navigation;
using Hexapi.Shared.Ik;
using Newtonsoft.Json;
using NLog;
using RxMqtt.Client;

namespace Hexapi.Service
{
    public class HexapiService
    {
        internal static readonly SerialDeviceHelper SerialDeviceHelper = new SerialDeviceHelper();

        private ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly IkFilter _ikFilter = new IkFilter();

        private readonly XboxIkController _xboxController = new XboxIkController();

        private readonly Gps _gps = new Gps();
        private readonly GpsNavigator _gpsNavigator = new GpsNavigator();

        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly List<Task> _startTasks = new List<Task>();
        
        private GpioController _gpioController;
        private GpioPin _startButton;
        private static GpioPin _resetGps1;
        private static GpioPin _resetGps2;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private static bool _navRunning;

        private bool _ignoreDisconnect;

        private MqttClient _mqttClient;

        private readonly UsbCamera _usbCamera = new UsbCamera();

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        private readonly CurieReader _curieReader = new CurieReader();


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _mqttClient = new MqttClient("Hexapi", "172.16.0.244", 1883, 60000, _cancellationTokenSource.Token);

                await _mqttClient.InitializeAsync();

                _mqttClient?.Subscribe(IkParamsSubscriptionEventHandler, "hex-ik");
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, e.Message);
            }

            _initializeTasks.Add(_xboxController.InitializeAsync(_cancellationTokenSource.Token));
            _initializeTasks.Add(_ikFilter.InitializeAsync());

            _startTasks.Add(_ikFilter.StartAsync(_cancellationTokenSource.Token));
            _startTasks.Add(_usbCamera.StartAsync(_cancellationTokenSource.Token));
            _startTasks.Add(_curieReader.StartAsync(_cancellationTokenSource.Token));

            await Task.WhenAll(_initializeTasks.ToArray());

            

            if (_xboxController.IsConnected)
            {
                _ikFilter.IkObservable = _xboxController.IkParamSubject
                    .Distinct()
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .AsObservable(); 
                    //.Merge(_gpsNavigator.IkParamSubject);
            }

            _disposables.Add(_usbCamera.ImageCaptureSubject
                .Where(image => image != null)
                .SubscribeOn(Scheduler.Default)
                .Subscribe(
                    async image =>
                    {
                        try
                        {
                            var r = await _mqttClient.PublishAsync(image, "hex-eye", TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        }
                        catch (Exception e)
                         {

                        }
                        
                    }));

            _disposables.Add(_curieReader.HexapiTelemetrySubject
                .Where(hexapiTelemetry => hexapiTelemetry != null)
                //.Distinct()
                .Sample(TimeSpan.FromMilliseconds(150))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(
                    async hexapiTelemetry =>
                    {
                        try
                        {
                            var serializedData = JsonConvert.SerializeObject(hexapiTelemetry);

                            var r = await _mqttClient.PublishAsync(serializedData, "hex-telemetry", TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            //
                        }
                    }));

            await Task.WhenAll(_startTasks.ToArray());
        }

        private void IkParamsSubscriptionEventHandler(string s)
        {
          
            try
            {
                var ikParams = JsonConvert.DeserializeObject<IkParams>(s);

                _ikFilter.IkRemoteSubject.OnNext(ikParams);
            }
            catch (Exception e)
            {
                //
            }
        }

        private async Task InitGpioAsync()
        {
            try
            {
                _gpioController = GpioController.GetDefault();

                if (_gpioController == null)
                {
                    //await _display.WriteAsync("GPIO ?");
                    return;
                }
            }
            catch
            {
                //await _display.WriteAsync("GPIO Exception");
                return;
            }

            _resetGps1 = _gpioController.OpenPin(24);
            _resetGps2 = _gpioController.OpenPin(25);

            _resetGps1.SetDriveMode(GpioPinDriveMode.Output);
            _resetGps2.SetDriveMode(GpioPinDriveMode.Output);

            _startButton = _gpioController.OpenPin(5);
            _startButton.SetDriveMode(GpioPinDriveMode.Input);
            _startButton.DebounceTimeout = TimeSpan.FromMilliseconds(500);
            await Task.Delay(500);
            _startButton.ValueChanged += StartButton_ValueChanged;
        }

        internal static async Task ResetGps()
        {
            _resetGps1.Write(GpioPinValue.Low);
            _resetGps2.Write(GpioPinValue.Low);

            await Task.Delay(1000);

            _resetGps1.Write(GpioPinValue.High);
            _resetGps2.Write(GpioPinValue.High);
        }

        private async void StartButton_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (sender.PinNumber != 5 || args.Edge != GpioPinEdge.FallingEdge)
                return;

            //XboxController.Disconnected -= XboxControllerDisconnected;

            //await _display.WriteAsync("Start pushed").ConfigureAwait(false);

            if (_navRunning)
            {
                //await _display.WriteAsync("Busy", 2);
                return;
            }

            _navRunning = true;

            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource = new CancellationTokenSource();

            _ignoreDisconnect = true;
            await _gpsNavigator.StartAsync(_cancellationTokenSource.Token, () =>
            {
                _navRunning = false;
                _cancellationTokenSource.Cancel();
                //_display.WriteAsync("Completed", 2).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
