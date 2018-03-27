using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
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

        public App()
        {
            Initialize();
            InitializeComponent();
        }

        protected override async void Configure()
        {
            _container = new WinRTContainer();

            _container.RegisterWinRTServices();

            _container.Singleton<ShellViewModel>();
        }

        protected override void PrepareViewFirst(Frame rootFrame)
        {
            _container.RegisterNavigationService(rootFrame);
        }

        protected override async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var extendedExecutionSession = new ExtendedExecutionSession { Reason = ExtendedExecutionReason.Unspecified };
            var extendedExecutionResult = await extendedExecutionSession.RequestExtensionAsync();
            if (extendedExecutionResult != ExtendedExecutionResult.Allowed)
            {
                extendedExecutionSession.Dispose();
                extendedExecutionSession = null;
            }
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
