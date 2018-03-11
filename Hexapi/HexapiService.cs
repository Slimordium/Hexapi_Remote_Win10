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

        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly IkFilter _ikFilter = new IkFilter();

        private readonly XboxIkController _xboxController = new XboxIkController();

        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly List<Task> _startTasks = new List<Task>();
        
        private readonly RazorImu _razorImu = new RazorImu();

        private readonly MaxbotixSonar _maxbotixSonar = new MaxbotixSonar();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private MqttClient _mqttClient;

        private readonly UsbCamera _usbCamera = new UsbCamera();

        private readonly List<IDisposable> _disposables = new List<IDisposable>();


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _mqttClient = new MqttClient("Hexapi", "172.16.0.245", 1883, 60000, _cancellationTokenSource.Token);

                await _mqttClient.InitializeAsync();

                _mqttClient?.Subscribe(IkParamsSubscriptionEventHandler, "hex-ik");
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, e.Message);
            }

            _initializeTasks.Add(_xboxController.InitializeAsync(_cancellationTokenSource.Token));
            _initializeTasks.Add(_ikFilter.InitializeAsync());
            _initializeTasks.Add(_maxbotixSonar.InitializeAsync());
            _initializeTasks.Add(_razorImu.InitializeAsync());

            _startTasks.Add(_ikFilter.StartAsync(_cancellationTokenSource.Token));
            _startTasks.Add(_usbCamera.StartAsync(_cancellationTokenSource.Token));
            _startTasks.Add(_maxbotixSonar.StartAsync(_cancellationTokenSource.Token));
            _startTasks.Add(_razorImu.StartAsync(_cancellationTokenSource.Token));

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
                .Subscribe(bytes =>
                    {
                        _mqttClient.PublishAsync(bytes, "hex-eye", TimeSpan.FromSeconds(1)).ToObservable().Subscribe();
                    }));

            _disposables.Add(_razorImu.ImuDataSubject
                .Where(imuData => imuData != null)
                .Sample(TimeSpan.FromMilliseconds(50))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(imuData =>
                {
                    _mqttClient.PublishAsync(JsonConvert.SerializeObject(imuData), "hex-imu", TimeSpan.FromSeconds(1)).ToObservable().Subscribe();
                }));

            _disposables.Add(_maxbotixSonar.SonarSubject
                .SubscribeOn(Scheduler.Default)
                .Subscribe(sonar =>
                {
                    _mqttClient.PublishAsync(sonar.ToString(), "hex-sonar", TimeSpan.FromSeconds(1)).ToObservable().Subscribe();
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

    }
}
