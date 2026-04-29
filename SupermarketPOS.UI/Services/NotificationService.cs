using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SupermarketPOS.UI.Services
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationAction
    {
        public string Text { get; set; }
        public Action Action { get; set; }
    }

    public interface INotificationService
    {
        void Show(string message, NotificationType type = NotificationType.Info, int durationSeconds = 3);
        void ShowWithUndo(string message, Action undoAction, object undoData, int durationSeconds = 10);
        void ShowWithActions(string message, List<NotificationAction> actions, NotificationType type = NotificationType.Info, int durationSeconds = 5);
        void SetContainer(Panel container);
    }

    public class NotificationService : INotificationService
    {
        private readonly ConcurrentDictionary<string, DateTime> _lastShown = new ConcurrentDictionary<string, DateTime>();
        private readonly TimeSpan _throttlePeriod = TimeSpan.FromSeconds(10);
        private Panel _container;

        public void SetContainer(Panel container)
        {
            _container = container;
        }

        public void Show(string message, NotificationType type = NotificationType.Info, int durationSeconds = 3)
        {
            if (_container == null) return;

            var key = message.GetHashCode().ToString();
            DateTime last;
            if (_lastShown.TryGetValue(key, out last) && DateTime.UtcNow - last < _throttlePeriod)
                return;

            _lastShown[key] = DateTime.UtcNow;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var border = CreateNotificationBorder(message, type);
                _container.Children.Add(border);
                AnimateInAndScheduleRemoval(border, durationSeconds);
            }));
        }

        public void ShowWithUndo(string message, Action undoAction, object undoData, int durationSeconds = 10)
        {
            ShowWithActions(message, new List<NotificationAction>
            {
                new NotificationAction { Text = "تراجع", Action = undoAction }
            }, NotificationType.Success, durationSeconds);
        }

        public void ShowWithActions(string message, List<NotificationAction> actions, NotificationType type = NotificationType.Info, int durationSeconds = 5)
        {
            if (_container == null) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var border = CreateNotificationBorderWithActions(message, type, actions);
                _container.Children.Add(border);
                AnimateInAndScheduleRemoval(border, durationSeconds);
            }));
        }

        private void AnimateInAndScheduleRemoval(Border border, int durationSeconds)
        {
            border.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));

            if (durationSeconds <= 0) return;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, e) => _container.Children.Remove(border);
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        private Border CreateNotificationBorder(string message, NotificationType type)
        {
            return new Border
            {
                Background = GetBrush(type),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 0, 8),
                MaxWidth = 400,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0,
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private Border CreateNotificationBorderWithActions(string message, NotificationType type, List<NotificationAction> actions)
        {
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (actions != null && actions.Count > 0)
            {
                var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
                foreach (var action in actions)
                {
                    var button = new Button
                    {
                        Content = action.Text,
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1),
                        Foreground = Brushes.White,
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 8, 0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = action.Action
                    };
                    button.Click += (sender, args) => ((Action)((Button)sender).Tag)?.Invoke();
                    actionPanel.Children.Add(button);
                }
                stackPanel.Children.Add(actionPanel);
            }

            return new Border
            {
                Background = GetBrush(type),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 0, 8),
                MaxWidth = 400,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0,
                Child = stackPanel
            };
        }

        private static Brush GetBrush(NotificationType type)
        {
            if (type == NotificationType.Success) return new SolidColorBrush(Color.FromRgb(16, 185, 129));
            if (type == NotificationType.Warning) return new SolidColorBrush(Color.FromRgb(245, 158, 11));
            if (type == NotificationType.Error) return new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return new SolidColorBrush(Color.FromRgb(59, 130, 246));
        }
    }
}
