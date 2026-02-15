using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using PingTool.Services;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;

namespace PingTool
{
    public sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService;
        public static BackgroundTaskDeferral AppServiceDeferral = null;
        public static AppServiceConnection Connection = null;
        public static event EventHandler AppServiceDisconnected;
        public static event EventHandler<AppServiceTriggerDetails> AppServiceConnected;
        private ActivationService ActivationService
        {
            get { return _activationService.Value; }
        }
        public App()
        {
            InitializeComponent();
            UnhandledException += OnAppUnhandledException;
            _activationService = new Lazy<ActivationService>(CreateActivationService);
            this.Suspending += OnSuspending;
            AppCenter.Start("6d7768e2-cf4e-41fb-a2b8-30c20c7ef36b",
                        typeof(Analytics), typeof(Crashes));
        }
        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            AppServiceDeferral?.Complete();
            AppServiceDeferral = null;
            Connection = null;
            AppServiceDisconnected?.Invoke(this, null);
        }
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (!args.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(args);
            }
        }
        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await ActivationService.ActivateAsync(args);
        }
        private void OnAppUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Analytics.TrackEvent("Unhandled Exception", new Dictionary<string, string> { { "Type", e.GetType().ToString() }, { "Message", e.Message }, { "Stack Trace", e.ToString() } });
        }
        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(Views.MainPage), new Lazy<UIElement>(CreateShell));
        }
        private UIElement CreateShell()
        {
            return new Views.ShellPage();
        }
        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details && details.CallerPackageFamilyName == Package.Current.Id.FamilyName)
            {
                AppServiceDeferral = args.TaskInstance.GetDeferral();
                args.TaskInstance.Canceled += OnTaskCanceled;
                Connection = details.AppServiceConnection;
                AppServiceConnected?.Invoke(this, args.TaskInstance.TriggerDetails as AppServiceTriggerDetails);
            }
        }
    }
}
