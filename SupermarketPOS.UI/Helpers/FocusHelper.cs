using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI.Helpers
{
    public static class FocusHelper
    {
        public static void FocusFirstInput(FrameworkElement container)
        {
            var first = FindFirstInput(container);
            first?.Focus();
            if (first is TextBox tb) tb.SelectAll();
        }

        private static FrameworkElement FindFirstInput(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBox || child is PasswordBox || child is ComboBox)
                    return child as FrameworkElement;
                var found = FindFirstInput(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}