/*
    3DOF Hexapod - Hexapi startup 
*/

using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
using RxMqtt.Shared;

namespace Hexapi.Service
{
    public class HexapiService
    {
        internal static readonly SerialDeviceHelper SerialDeviceHelper = new SerialDeviceHelper();

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        private readonly List<Task> _startTasks = new List<Task>();
        private readonly List<Task> _initializeTasks = new List<Task>();
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly MaxbotixSonar _maxbotixSonar = new MaxbotixSonar();
        private readonly UsbCamera _usbCamera = new UsbCamera();
        private readonly XboxIkController _xboxController = new XboxIkController();
        private readonly RazorImu _razorImu = new RazorImu();

        private readonly TextToSpeech _textToSpeech = new TextToSpeech();

        private readonly IkFilter _ikFilter = new IkFilter();

        private MqttClient _mqttClient;

        private readonly string _brokerIp;

        private bool _disposed;

        private readonly object _disposedLock = new object();

        public ISubject<string> SpeechSubject { get; } = new Subject<string>();

        public HexapiService(string brokerIp)
        {
            _brokerIp = brokerIp;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var status = Status.Error;

            try
            {
                _mqttClient = new MqttClient("Hexapi", _brokerIp, 1883);

                status = await _mqttClient.InitializeAsync();

                _logger.Log(LogLevel.Info, $"MQTT Client Started => {status}");
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, e.Message);
            }

            cancellationToken.Register(DisposeSubscriptions);

            _initializeTasks.Add(_xboxController.InitializeAsync(cancellationToken));
            _initializeTasks.Add(_ikFilter.InitializeAsync());
            _initializeTasks.Add(_maxbotixSonar.InitializeAsync());
            _initializeTasks.Add(_razorImu.InitializeAsync());

            _startTasks.Add(_ikFilter.StartAsync(cancellationToken));
            _startTasks.Add(_usbCamera.StartAsync(cancellationToken));
            _startTasks.Add(_maxbotixSonar.StartAsync(cancellationToken));
            _startTasks.Add(_razorImu.StartAsync(cancellationToken));

            await Task.WhenAll(_initializeTasks.ToArray());

            if (status == Status.Initialized)
            {
                Subscribe();
            }
            else
            {
                _logger.Log(LogLevel.Error, "Not subscribing, MQTT connection failed");
            }

            if (_xboxController.IsConnected)
            {
                _ikFilter.IkObservable = _xboxController.IkParamSubject
                                        .Distinct()
                                        .Sample(TimeSpan.FromMilliseconds(100))
                                        .SubscribeOn(Scheduler.Default)
                                        .AsObservable();
            }

            await Task.WhenAll(_startTasks.ToArray());
        }

        private void Subscribe()
        {
            _disposables.Add(_mqttClient.GetPublishStringObservable("hex-ik")
                .SubscribeOn(Scheduler.Default)
                .Subscribe(IkParamsSubscriptionEventHandler));

            _disposables.Add(_usbCamera.ImageCaptureSubject
                .Where(image => image != null)
                .SubscribeOn(Scheduler.Default)
                .Subscribe(async bytes =>
                {
                    try
                    {
                        await _mqttClient.PublishAsync(bytes, "hex-eye", TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        DisposeSubscriptions();
                    }
                }));

            _disposables.Add(_razorImu.ImuDataSubject
                .Where(imuData => imuData != null)
                .Sample(TimeSpan.FromMilliseconds(75))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(async imuData =>
                {
                    try
                    {
                        await _mqttClient.PublishAsync(JsonConvert.SerializeObject(imuData), "hex-imu", TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        DisposeSubscriptions();
                    }

                }));

            _disposables.Add(_maxbotixSonar.SonarSubject
                .SubscribeOn(Scheduler.Default)
                .Subscribe(async sonar =>
                {
                    try
                    {
                        await _mqttClient.PublishAsync(sonar.ToString(), "hex-sonar", TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        DisposeSubscriptions();
                    }

                }));

            _disposables.Add(_mqttClient.GetPublishStringObservable("hex-speech")
                .SubscribeOn(Scheduler.Default)
                .Subscribe(_textToSpeech.TextSubject));
        }

        private void DisposeSubscriptions()
        {
            lock (_disposedLock)
            {
                if (_disposed)
                    return;

                _logger.Log(LogLevel.Error, "Timeout, disposing subscriptions");

                _disposed = true;

                foreach (var disposable in _disposables)
                {
                    disposable.Dispose();
                }
            }
        }

        private void IkParamsSubscriptionEventHandler(string s)
        {
            try
            {
                var ikParams = JsonConvert.DeserializeObject<IkParams>(s);

                _ikFilter.IkRemoteSubject.OnNext(ikParams);
            }
            catch (Exception)
            {
                //
            }
        }
    }
}