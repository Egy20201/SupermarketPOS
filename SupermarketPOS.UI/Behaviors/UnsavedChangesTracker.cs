using System;
using System.Windows;
using System.Windows.Input;

namespace SupermarketPOS.UI.Behaviors
{
    public static class UnsavedChangesTracker
    {
        public static readonly DependencyProperty IsDirtyProperty =
            DependencyProperty.RegisterAttached("IsDirty", typeof(bool), typeof(UnsavedChangesTracker),
                new PropertyMetadata(false, OnIsDirtyChanged));

        public static readonly DependencyProperty TrackChangesProperty =
            DependencyProperty.RegisterAttached("TrackChanges", typeof(bool), typeof(UnsavedChangesTracker),
                new PropertyMetadata(false, OnTrackChangesChanged));

        public static bool GetIsDirty(DependencyObject obj) => (bool)obj.GetValue(IsDirtyProperty);
        public static void SetIsDirty(DependencyObject obj, bool value) => obj.SetValue(IsDirtyProperty, value);

        public static bool GetTrackChanges(DependencyObject obj) => (bool)obj.GetValue(TrackChangesProperty);
        public static void SetTrackChanges(DependencyObject obj, bool value) => obj.SetValue(TrackChangesProperty, value);

        private static void OnTrackChangesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && (bool)e.NewValue)
            {
                element.PreviewKeyDown += OnInput;
                element.PreviewTextInput += OnInput;
                element.LostFocus += OnLostFocus;
            }
        }

        private static void OnInput(object sender, EventArgs e)
        {
            if (sender is DependencyObject obj)
                SetIsDirty(obj, true);
        }

        private static void OnLostFocus(object sender, RoutedEventArgs e)
        {
            // Optional: Mark as clean on save handled by ViewModel
        }

        private static void OnIsDirtyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = Window.GetWindow(d);
            if (window != null)
            {
                window.CommandBindings.Clear();
                window.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, (s, args) => { }, (s, args) => args.CanExecute = !(bool)e.NewValue));
                window.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, args) => { }, (s, args) => args.CanExecute = !(bool)e.NewValue));
            }
        }
    }
}