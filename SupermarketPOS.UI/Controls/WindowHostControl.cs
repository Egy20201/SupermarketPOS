using System;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls
{
    public class WindowHostControl : UserControl
    {
        public WindowHostControl(Window window)
        {
            if (window == null)
            {
                Content = new TextBlock { Text = "لا يمكن تحميل الشاشة.", Margin = new Thickness(12) };
                return;
            }

            var content = window.Content as UIElement;
            if (content == null)
            {
                Content = new TextBlock { Text = "لا يمكن تحميل الشاشة.", Margin = new Thickness(12) };
                return;
            }

            window.Content = null;
            Content = content;
        }
    }
}