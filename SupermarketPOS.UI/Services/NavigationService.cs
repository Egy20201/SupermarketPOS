using System;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Services
{
    public interface INavigationHost
    {
        void OpenTab(string key, string title, Func<UserControl> contentFactory);
        void CloseTab(string key);
    }

    public sealed class NavigationService
    {
        private static readonly NavigationService _instance = new NavigationService();
        public static NavigationService Instance => _instance;
        private INavigationHost _host;
        private NavigationService() { }

        public void Initialize(INavigationHost host) => _host = host;
        public void OpenTab(string key, string title, Func<UserControl> contentFactory) => _host?.OpenTab(key, title, contentFactory);
        public void CloseTab(string key) => _host?.CloseTab(key);
    }
}