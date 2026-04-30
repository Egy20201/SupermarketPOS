using Microsoft.Extensions.DependencyInjection;
using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Controls.Dynamic;
using SupermarketPOS.UI.Services;
using SupermarketPOS.UI.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SupermarketPOS.UI
{
    public partial class App : Application
    {
        public static User CurrentUser
        {
            get
            {
                var app = Current as App;
                return app?.Services?.GetService<ICurrentUserService>()?.CurrentUser;
            }
        }

        public IServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Services = ConfigureServices();
            RegisterExceptionHandlers();

            AsyncDataService.Initialize(SynchronizationContext.Current);

            var startupService = Services.GetRequiredService<ApplicationStartupService>();
            startupService.EnsureSeedData();

            if (startupService.RequiresBusinessTypeSetup())
            {
                Services.GetRequiredService<SetupWizardWindow>().Show();
                return;
            }

            Services.GetRequiredService<LoginWindow>().Show();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddBusinessServices();

            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<NavigationService>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<SetupWizardWindow>();
            services.AddTransient<SalesReturnWindow>();
            services.AddTransient<PurchaseReturnWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var processor = Services?.GetService<OutboxBackgroundProcessor>();
                processor?.Stop();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error stopping OutboxBackgroundProcessor during shutdown");
            }

            base.OnExit(e);
        }

        private void RegisterExceptionHandlers()
        {
            // Wire UIExceptionHandler global pipeline (no MessageBox)
            UIExceptionHandler.WireGlobalHandlers();

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Logger.Fatal(ex, "UNHANDLED DOMAIN EXCEPTION");
                UIExceptionHandler.Handle(ex, "UnhandledDomain");
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.Error(args.Exception, "UNOBSERVED TASK EXCEPTION");
                args.SetObserved();
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                Logger.Error(args.Exception, "UI THREAD UNHANDLED EXCEPTION");
                args.Handled = true;
                UIExceptionHandler.Handle(args.Exception, "UIThread");
            };
        }
    }
}
