using SupermarketPOS.Business;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class RoleManagementWindow : Window
    {
        private readonly AuthorizationService _authzService;
        private readonly AdminService _adminService;
        private List<Role> _roles;
        private Dictionary<int, CheckBox> _permissionChecks = new Dictionary<int, CheckBox>();

        public RoleManagementWindow()
        {
            InitializeComponent();

            var userId = App.CurrentUser?.Id ?? 0;
            try
            {
                _authzService = DependencyInjection.GetRequiredService<AuthorizationService>();
                _authzService.DemandPermission(userId, "settings.roles");
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Warning($"Unauthorized access to RoleManagement by user {userId}");
                MessageBox.Show("غير مصرح لك بالوصول إلى هذه الشاشة", "صلاحيات", MessageBoxButton.OK, MessageBoxImage.Stop);
                Close();
                return;
            }

            _adminService = DependencyInjection.GetRequiredService<AdminService>();
            LoadRoles();
            LoadPermissions();
        }

        private void LoadRoles()
        {
            _roles = _adminService.GetActiveRoles();
            RolesList.ItemsSource = _roles;
        }

        private void LoadPermissions()
        {
            var grouped = _authzService.GetAllPermissionsGrouped();
            PermissionsPanel.Items.Clear();

            foreach (var group in grouped)
            {
                var groupBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                foreach (var perm in group.Value)
                {
                    var cb = new CheckBox
                    {
                        Content = perm.Name,
                        Tag = perm.Id,
                        Foreground = System.Windows.Media.Brushes.White,
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    _permissionChecks[perm.Id] = cb;
                    stack.Children.Add(cb);
                }

                groupBorder.Child = stack;
                PermissionsPanel.Items.Add(groupBorder);
            }
        }

        private void RolesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RolesList.SelectedItem is Role selectedRole)
            {
                SelectedRoleText.Text = selectedRole.Name;

                var rolePermIds = _adminService.GetRolePermissionIds(selectedRole.Id);
                foreach (var kvp in _permissionChecks)
                    kvp.Value.IsChecked = rolePermIds.Contains(kvp.Key);
            }
        }

        private void SavePermissions_Click(object sender, RoutedEventArgs e)
        {
            if (RolesList.SelectedItem is Role selectedRole)
            {
                foreach (var kvp in _permissionChecks)
                {
                    if (kvp.Value.IsChecked == true)
                        _authzService.GrantPermissionToRole(selectedRole.Id, kvp.Key);
                    else
                        _authzService.RevokePermissionFromRole(selectedRole.Id, kvp.Key);
                }

                MessageBox.Show("تم حفظ الصلاحيات", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddRole_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PromptDialog("اسم الدور الجديد", "إضافة دور");
            if (dialog.ShowDialog() == true)
            {
                _adminService.AddRole(dialog.Result);
                LoadRoles();
            }
        }
    }
}
