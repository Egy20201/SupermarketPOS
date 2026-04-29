using System.Windows;

namespace SupermarketPOS.UI.Behaviors
{
    public static class GlobalKeyboardBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(GlobalKeyboardBehavior),
                new PropertyMetadata(false));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);
    }
}
