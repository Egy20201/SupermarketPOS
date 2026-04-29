using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace SupermarketPOS.UI.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceProvider ServiceProvider
        {
            get
            {
                var app = Application.Current as App;
                if (app?.Services == null)
                    throw new InvalidOperationException("Application services have not been configured.");

                return app.Services;
            }
        }

        public static T GetRequiredService<T>() where T : class
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        public static T GetService<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }
    }
}
