using System;
using System.Windows;
using System.Windows.Input;

namespace SupermarketPOS.UI.Behaviors
{
    public static class KeyboardShortcuts
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(KeyboardShortcuts),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && (bool)e.NewValue)
            {
                element.PreviewKeyDown += OnPreviewKeyDown;
                if (element is FrameworkElement frameworkElement)
                    frameworkElement.Unloaded += (s, _) => element.PreviewKeyDown -= OnPreviewKeyDown;
            }
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var target = sender as FrameworkElement;

            switch (e.Key)
            {
                case Key.F5:
                    ExecuteCommand(target, "RefreshCommand");
                    e.Handled = true;
                    break;
                case Key.F when e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control:
                    ExecuteCommand(target, "FocusSearchCommand");
                    e.Handled = true;
                    break;
                case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                    ExecuteCommand(target, "NewCommand");
                    e.Handled = true;
                    break;
                case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                    ExecuteCommand(target, "SaveCommand");
                    e.Handled = true;
                    break;
                case Key.Escape:
                    ExecuteCommand(target, "CancelCommand");
                    e.Handled = true;
                    break;
                case Key.Delete:
                    ExecuteCommand(target, "DeleteCommand");
                    e.Handled = true;
                    break;
            }
        }

        private static void ExecuteCommand(FrameworkElement target, string commandName)
        {
            var vm = target?.DataContext;
            var command = vm?.GetType().GetProperty(commandName)?.GetValue(vm) as ICommand;
            if (command?.CanExecute(null) == true)
                command.Execute(null);
        }
    }
}
