using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls
{
    public class BasePage : UserControl
    {
        public string PageTitle { get; set; } = "الصفحة";
        public Dictionary<string, object> Parameters { get; set; }

        public BasePage()
        {
            Parameters = new Dictionary<string, object>();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public virtual void OnNavigatedTo() { }
        public virtual void OnNavigatedFrom() { }
        public virtual void Refresh() => OnNavigatedTo();
        public virtual void Cleanup() { }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e) => OnNavigatedTo();
        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e) => OnNavigatedFrom();
    }
}