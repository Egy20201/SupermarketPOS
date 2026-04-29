using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SupermarketPOS.UI.Behaviors
{
    public static class AutoCompleteBehavior
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.RegisterAttached("ItemsSource", typeof(IEnumerable), typeof(AutoCompleteBehavior),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.RegisterAttached("SelectedItem", typeof(object), typeof(AutoCompleteBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.RegisterAttached("DisplayMemberPath", typeof(string), typeof(AutoCompleteBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ValueMemberPathProperty =
            DependencyProperty.RegisterAttached("ValueMemberPath", typeof(string), typeof(AutoCompleteBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.RegisterAttached("Filter", typeof(Func<object, string, bool>), typeof(AutoCompleteBehavior));

        public static readonly DependencyProperty ItemSelectedCommandProperty =
            DependencyProperty.RegisterAttached("ItemSelectedCommand", typeof(ICommand), typeof(AutoCompleteBehavior));

        public static IEnumerable GetItemsSource(DependencyObject obj) => (IEnumerable)obj.GetValue(ItemsSourceProperty);
        public static void SetItemsSource(DependencyObject obj, IEnumerable value) => obj.SetValue(ItemsSourceProperty, value);

        public static object GetSelectedItem(DependencyObject obj) => obj.GetValue(SelectedItemProperty);
        public static void SetSelectedItem(DependencyObject obj, object value) => obj.SetValue(SelectedItemProperty, value);

        public static string GetDisplayMemberPath(DependencyObject obj) => (string)obj.GetValue(DisplayMemberPathProperty);
        public static void SetDisplayMemberPath(DependencyObject obj, string value) => obj.SetValue(DisplayMemberPathProperty, value);

        public static string GetValueMemberPath(DependencyObject obj) => (string)obj.GetValue(ValueMemberPathProperty);
        public static void SetValueMemberPath(DependencyObject obj, string value) => obj.SetValue(ValueMemberPathProperty, value);

        public static Func<object, string, bool> GetFilter(DependencyObject obj) => (Func<object, string, bool>)obj.GetValue(FilterProperty);
        public static void SetFilter(DependencyObject obj, Func<object, string, bool> value) => obj.SetValue(FilterProperty, value);

        public static ICommand GetItemSelectedCommand(DependencyObject obj) => (ICommand)obj.GetValue(ItemSelectedCommandProperty);
        public static void SetItemSelectedCommand(DependencyObject obj, ICommand value) => obj.SetValue(ItemSelectedCommandProperty, value);

        private static Popup _currentPopup;
        private static ListBox _currentListBox;
        private static TextBox _currentTextBox;

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.TextChanged += OnTextChanged;
                textBox.PreviewKeyDown += OnPreviewKeyDown;
                textBox.LostFocus += OnLostFocus;
                textBox.GotFocus += OnGotFocus;
            }
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (_currentPopup != null && _currentTextBox == textBox)
            {
                _currentPopup.IsOpen = true;
                UpdateFilteredItems(textBox, textBox.Text);
            }
        }

        private static void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            UpdateFilteredItems(textBox, textBox.Text);
        }

        private static void UpdateFilteredItems(TextBox textBox, string searchText)
        {
            var itemsSource = GetItemsSource(textBox);
            if (itemsSource == null) return;

            var filter = GetFilter(textBox) ?? ((item, text) => item.ToString().Contains(text));
            var filtered = string.IsNullOrEmpty(searchText)
                ? itemsSource
                : itemsSource.Cast<object>().Where(item => filter(item, searchText));

            if (_currentPopup == null)
                CreatePopup(textBox);

            _currentListBox.ItemsSource = filtered;
            _currentPopup.IsOpen = filtered.Cast<object>().Any() && textBox.IsFocused;
        }

        private static void CreatePopup(TextBox textBox)
        {
            _currentTextBox = textBox;
            _currentListBox = new ListBox
            {
                MaxHeight = 200,
                Background = (System.Windows.Media.Brush)Application.Current.FindResource("SecondaryBrush"),
                BorderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 2, 0, 0)
            };

            _currentListBox.SelectionChanged += (s, e) =>
            {
                if (_currentListBox.SelectedItem != null)
                {
                    SetSelectedItem(textBox, _currentListBox.SelectedItem);
                    _currentPopup.IsOpen = false;

                    var command = GetItemSelectedCommand(textBox);
                    command?.Execute(_currentListBox.SelectedItem);

                    textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            };

            _currentPopup = new Popup
            {
                PlacementTarget = textBox,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                Child = _currentListBox,
                Width = textBox.ActualWidth
            };
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_currentPopup == null || !_currentPopup.IsOpen) return;

            switch (e.Key)
            {
                case Key.Down:
                    if (_currentListBox.SelectedIndex < _currentListBox.Items.Count - 1)
                        _currentListBox.SelectedIndex++;
                    e.Handled = true;
                    break;
                case Key.Up:
                    if (_currentListBox.SelectedIndex > 0)
                        _currentListBox.SelectedIndex--;
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (_currentListBox.SelectedItem != null)
                    {
                        SetSelectedItem((TextBox)sender, _currentListBox.SelectedItem);
                        _currentPopup.IsOpen = false;

                        var command = GetItemSelectedCommand((TextBox)sender);
                        command?.Execute(_currentListBox.SelectedItem);
                    }
                    e.Handled = true;
                    break;
                case Key.Escape:
                    _currentPopup.IsOpen = false;
                    e.Handled = true;
                    break;
            }
        }

        private static void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (_currentPopup != null)
                _currentPopup.IsOpen = false;
        }
    }
}
