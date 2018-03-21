using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Media.SpeechSynthesis;
using Windows.UI.WebUI;
using Windows.UI.Xaml.Controls;
using Caliburn.Micro;
using Hexapi.Host.Views;
using Hexapi.Shared;
using NLog;
using NLog.Fluent;
using RxMqtt.Client;
using RxMqtt.Shared;

namespace Hexapi.Host.ViewModels{
    public class ShellViewModel : Conductor<object>
    {
        private Service.HexapiService _hexapodService;

        private RxMqtt.Client.MqttClient _rxMqttClient;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public string BrokerIp { get; set; } = "172.16.0.245";

        private IDisposable _logDisposable;

        private IDisposable _speechDisposable;

        public IObservableCollection<string> Log { get; set; } = new BindableCollection<string>();

        private ILogger _logger = NLog.LogManager.GetCurrentClassLogger();

        public MediaElement MediaElement { get; } = new MediaElement();

        public ShellViewModel()
        {
            if (NLog.Targets.Rx.RxTarget.LogObservable != null)
            {
                _logDisposable = NLog.Targets.Rx.RxTarget
                    .LogObservable
                    .ObserveOnDispatcher()
                    .Subscribe(s =>
                    {
                        Log.Insert(0, s);

                        if (Log.Count > 1000)
                            Log.RemoveAt(1000);
                    });
            }
        }

        public async Task TextToSpeech(string text)
        {
            using (var speech = new SpeechSynthesizer())
            {
                speech.Voice = SpeechSynthesizer.AllVoices.First(gender => gender.Gender == VoiceGender.Female);

                var stream = await speech.SynthesizeTextToStreamAsync(text);
                MediaElement.SetSource(stream, stream.ContentType);
                MediaElement.Play();
            }
        }

        public async Task Start()
        {
            if (string.IsNullOrEmpty(BrokerIp))
                return;

            _rxMqttClient = new MqttClient("hex-speak", BrokerIp, 1883, 123, CancellationToken.None);

            var status = await _rxMqttClient.InitializeAsync();

            if (status == Status.Initialized)
            {
                _speechDisposable = _rxMqttClient.GetPublishStringObservable("hex-speech")
                    .ObserveOnDispatcher()
                    .Subscribe(async s =>
                    {
                        await TextToSpeech(s);
                    });
            }

            _hexapodService = new Service.HexapiService(BrokerIp);

            await _hexapodService.StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
        }

    }
}