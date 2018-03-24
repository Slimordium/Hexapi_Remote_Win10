using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Caliburn.Micro;
using NLog;

namespace Hexapi.Host.ViewModels
{
    public class ShellViewModel : Conductor<object>
    {
        private Service.HexapiService _hexapodService;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public string BrokerIp { get; set; } = "172.16.0.245";

        private IDisposable _logDisposable;

        public IObservableCollection<string> Log { get; set; } = new BindableCollection<string>();

        private ILogger _logger = NLog.LogManager.GetCurrentClassLogger();

        public MediaElement MediaElement { get; } = new MediaElement();

        private readonly IDisposable _startDisposable;

        private bool _started;

        public ShellViewModel()
        {
            //15 seconds for alternate IP, otherwise use default
            _startDisposable = Observable.Timer(TimeSpan.FromSeconds(15)).ObserveOnDispatcher().Subscribe(l => { Start().ToObservable().Subscribe(); });

            if (NLog.Targets.Rx.RxTarget.LogObservable != null)
            {
                _logDisposable = NLog.Targets.Rx.RxTarget
                    .LogObservable
                    .ObserveOnDispatcher()
                    .Subscribe(s =>
                    {
                        Log.Insert(0, s);

                        if (Log.Count > 500)
                            Log.RemoveAt(500);
                    });
            }
        }

        public async Task Start()
        {
            if (string.IsNullOrEmpty(BrokerIp) || _started)
                return;

            _started = true;

            _startDisposable?.Dispose();

            _hexapodService = new Service.HexapiService(BrokerIp);

           await _hexapodService.StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
        }
    }
}