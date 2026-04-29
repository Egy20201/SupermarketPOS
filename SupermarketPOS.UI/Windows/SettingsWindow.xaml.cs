using SupermarketPOS.Business;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;

        public SettingsWindow()
        {
            InitializeComponent();

            var userId = App.CurrentUser?.Id ?? 0;
            try
            {
                var authz = DependencyInjection.GetRequiredService<AuthorizationService>();
                authz.DemandPermission(userId, "settings.system");
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Warning($"Unauthorized access to Settings by user {userId}");
                MessageBox.Show("غير مصرح لك بالوصول إلى هذه الشاشة", "صلاحيات", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
                return;
            }

            _settingsService = DependencyInjection.GetRequiredService<SettingsService>();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var grouped = _settingsService.GetAllGrouped();
            SettingsPanel.Items.Clear();

            foreach (var group in grouped)
            {
                var groupBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 0, 0, 16)
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                foreach (var setting in group.Value)
                {
                    var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    row.Children.Add(new TextBlock
                    {
                        Text = setting.Key,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B0B0")),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 13
                    });

                    var textBox = new TextBox
                    {
                        Text = setting.Value,
                        Tag = setting.Key,
                        Foreground = Brushes.White,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A")),
                        Padding = new Thickness(10, 6, 10, 6),
                        VerticalContentAlignment = VerticalAlignment.Center
                    };

                    Grid.SetColumn(textBox, 1);
                    row.Children.Add(textBox);
                    stack.Children.Add(row);
                }

                var saveBtn = new Button
                {
                    Content = "💾 حفظ",
                    Style = (Style)FindResource("SuccessButtonStyle"),
                    Width = 100,
                    Height = 36,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 12, 0, 0)
                };

                saveBtn.Tag = stack;
                saveBtn.Click += SaveGroup_Click;
                stack.Children.Add(saveBtn);

                groupBorder.Child = stack;
                SettingsPanel.Items.Add(groupBorder);
            }
        }

        private void SaveGroup_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var stack = button.Tag as StackPanel;
            var errors = new System.Text.StringBuilder();

            foreach (var child in stack.Children)
            {
                if (child is Grid row && row.Children.Count >= 2 &&
                    row.Children[1] is TextBox tb && tb.Tag is string key)
                {
                    var value = tb.Text?.Trim();

                    if (key.Contains("tax") || key.Contains("rate") || key.Contains("overtime"))
                    {
                        if (!decimal.TryParse(value, out _))
                            errors.AppendLine($"'{key}' يجب أن يكون رقماً");
                    }
                    else if (key.Contains("time") && !string.IsNullOrWhiteSpace(value))
                    {
                        if (!TimeSpan.TryParse(value, out _))
                            errors.AppendLine($"'{key}' يجب أن يكون وقتاً صحيحاً (HH:mm:ss)");
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                        _settingsService.Set(key, value);
                }
            }

            if (errors.Length > 0)
                MessageBox.Show(errors.ToString(), "أخطاء", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show("تم حفظ الإعدادات", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}