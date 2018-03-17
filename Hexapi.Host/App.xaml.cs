using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.UI.Xaml.Controls;
using Caliburn.Micro;
using Hexapi.Host.ViewModels;
using Hexapi.Host.Views;
using Hexapi.Service;

namespace Hexapi.Host
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App
    {
        private WinRTContainer _container;
        //private HexapodService _hexapodService;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public App()
        {
            Initialize();
            InitializeComponent();
        }

        protected override async void Configure()
        {
            var extendedExecutionSession = new ExtendedExecutionSession {Reason = ExtendedExecutionReason.Unspecified};
            var extendedExecutionResult = await extendedExecutionSession.RequestExtensionAsync();
            if (extendedExecutionResult != ExtendedExecutionResult.Allowed)
            {
                //extended execution session revoked
                extendedExecutionSession.Dispose();
                extendedExecutionSession = null;
            }

            _container = new WinRTContainer();

            _container.RegisterWinRTServices();

            _container.Singleton<ShellViewModel>();

            await Task.Factory.StartNew(async () =>
            {
                var _hexapodService = new Hexapi.Service.HexapiService();

                await _hexapodService.StartAsync(_cancellationTokenSource.Token);
            }
            , TaskCreationOptions.LongRunning);
        }

        protected override void PrepareViewFirst(Frame rootFrame)
        {
            _container.RegisterNavigationService(rootFrame);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            DisplayRootView<ShellView>();
        }

        protected override object GetInstance(Type service, string key)
        {
            return _container.GetInstance(service, key);
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return _container.GetAllInstances(service);
        }

        protected override void BuildUp(object instance)
        {
            _container.BuildUp(instance);
        }
    }
}
