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
using Hardware.Xbox;
using Hexapi.Service.Hardware;
using Hexapi.Service.IK;
using Hexapi.Shared.Ik;
using Newtonsoft.Json;
using NLog;
using RxMqtt.Client;

namespace Hexapi.Service
{
    public class HexapiService
    {
        internal static readonly SerialDeviceHelper SerialDeviceHelper = new SerialDeviceHelper();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        private readonly List<Task> _startTasks = new List<Task>();
        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly MaxbotixSonar _maxbotixSonar = new MaxbotixSonar();
        private readonly UsbCamera _usbCamera = new UsbCamera();
        private readonly XboxIkController _xboxController = new XboxIkController();
        private readonly RazorImu _razorImu = new RazorImu();

        private readonly IkFilter _ikFilter = new IkFilter();

        private MqttClient _mqttClient;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _mqttClient = new MqttClient("Hexapi", "172.16.0.245", 1883, 60000, _cancellationTokenSource.Token);

                var status = await _mqttClient.InitializeAsync();

                _logger.Log(LogLevel.Info, $"MQTT Client Started => {status}");

                _disposables.Add(_mqttClient.GetPublishStringObservable("hex-ik").SubscribeOn(Scheduler.Default).Subscribe(IkParamsSubscriptionEventHandler));
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
                _ikFilter.IkObservable = _xboxController.IkParamSubject
                    .Distinct()
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .SubscribeOn(Scheduler.Default)
                    .AsObservable();

            _disposables.Add(_usbCamera.ImageCaptureSubject
                .Where(image => image != null)
                .SubscribeOn(Scheduler.Default)
                .Subscribe(bytes =>
                {
                    _mqttClient.PublishAsync(bytes, "hex-eye", TimeSpan.FromSeconds(2)).ToObservable().Subscribe();
                }));

            _disposables.Add(_razorImu.ImuDataSubject
                .Where(imuData => imuData != null)
                .Sample(TimeSpan.FromMilliseconds(75))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(imuData =>
                {
                    _mqttClient.PublishAsync(JsonConvert.SerializeObject(imuData), "hex-imu", TimeSpan.FromSeconds(2)).ToObservable().Subscribe();
                }));

            _disposables.Add(_maxbotixSonar.SonarSubject
                .SubscribeOn(Scheduler.Default)
                .Subscribe(sonar =>
                {
                    _mqttClient.PublishAsync(sonar.ToString(), "hex-sonar", TimeSpan.FromSeconds(2)).ToObservable().Subscribe();
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