using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.UI.WebUI;
using Windows.UI.Xaml.Controls;
using Caliburn.Micro;
using Hexapi.Host.Views;
using Hexapi.Shared;
using NLog;
using NLog.Fluent;

namespace Hexapi.Host.ViewModels{
    public class ShellViewModel : Conductor<object>
    {
        private Service.HexapiService _hexapodService;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public string BrokerIp { get; set; } = "172.16.0.245";

        private IDisposable _logDisposable;

        public IObservableCollection<string> Log { get; set; } = new BindableCollection<string>();

        private ILogger _logger = NLog.LogManager.GetCurrentClassLogger();

        public ShellViewModel()
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

        public async Task Start()
        {
           

            _hexapodService = new Service.HexapiService(BrokerIp);

            await _hexapodService.StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
        }

    }
}