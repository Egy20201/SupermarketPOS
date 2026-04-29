using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SupermarketPOS.UI.Behaviors
{
    public static class DataGridKeyboardBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(DataGridKeyboardBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static readonly DependencyProperty RemoveItemCommandProperty =
            DependencyProperty.RegisterAttached("RemoveItemCommand", typeof(ICommand), typeof(DataGridKeyboardBehavior));

        public static readonly DependencyProperty AddNewRowCommandProperty =
            DependencyProperty.RegisterAttached("AddNewRowCommand", typeof(ICommand), typeof(DataGridKeyboardBehavior));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static ICommand GetRemoveItemCommand(DependencyObject obj) => (ICommand)obj.GetValue(RemoveItemCommandProperty);
        public static void SetRemoveItemCommand(DependencyObject obj, ICommand value) => obj.SetValue(RemoveItemCommandProperty, value);

        public static ICommand GetAddNewRowCommand(DependencyObject obj) => (ICommand)obj.GetValue(AddNewRowCommandProperty);
        public static void SetAddNewRowCommand(DependencyObject obj, ICommand value) => obj.SetValue(AddNewRowCommandProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as DataGrid;
            if (grid == null) return;

            if ((bool)e.NewValue)
                grid.PreviewKeyDown += OnPreviewKeyDown;
            else
                grid.PreviewKeyDown -= OnPreviewKeyDown;
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null) return;

            if (e.Key == Key.Delete)
            {
                var command = GetRemoveItemCommand(grid);
                if (command != null && command.CanExecute(grid.SelectedItem))
                {
                    command.Execute(grid.SelectedItem);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Insert)
            {
                var command = GetAddNewRowCommand(grid);
                if (command != null && command.CanExecute(null))
                {
                    command.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
