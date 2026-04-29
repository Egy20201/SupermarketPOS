using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SupermarketPOS.UI.Behaviors
{
    public static class EscapeBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(EscapeBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                window.PreviewKeyDown += OnWindowKeyDown;
                window.Unloaded += (s, _) => window.PreviewKeyDown -= OnWindowKeyDown;
            }
        }

        private static void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ((Window)sender)?.Close();
                e.Handled = true;
            }
        }
    }
}