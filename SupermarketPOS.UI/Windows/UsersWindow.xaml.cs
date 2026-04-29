using SupermarketPOS.Core.Entities;
using SupermarketPOS.Core.Security;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SupermarketPOS.UI
{
    public partial class UsersWindow : BaseWindow
    {
        private ObservableCollection<UserAdminDto> userItems;
        private UserAdminDto selectedUser;
        private bool isEditMode = false;
        private readonly AdminService _adminService;

        public UsersWindow()
        {
            if (App.CurrentUser?.Role != "Admin")
            {
                MessageBox.Show("غير مصرح لك بالوصول إلى هذه الشاشة");
                Close();
                return;
            }

            InitializeComponent();
            _adminService = DependencyInjection.GetRequiredService<AdminService>();
            userItems = new ObservableCollection<UserAdminDto>();
            UsersGrid.ItemsSource = userItems;
            RegisterListShortcuts(SetAddMode, SetEditMode, DeleteUser, () => SearchBox.Focus(), LoadUsers, null);
            LoadFilters();
            LoadUsers();
            SetAddMode();
        }

        private void LoadFilters()
        {
            RoleFilterCombo.ItemsSource = new[] { "الكل", "Admin", "Manager", "Cashier" };
            RoleFilterCombo.SelectedIndex = 0;
        }

        private void LoadUsers()
        {
            var users = _adminService.GetUsers();
            userItems.Clear();
            foreach (var u in users) userItems.Add(u);
        }

        private void FilterUsers()
        {
            var search = SearchBox.Text?.Trim().ToLower() ?? "";
            var role = RoleFilterCombo.SelectedItem?.ToString();
            var view = CollectionViewSource.GetDefaultView(UsersGrid.ItemsSource);
            view.Filter = o => o is UserAdminDto u && (string.IsNullOrEmpty(search) || u.Username.ToLower().Contains(search) || u.FullName.ToLower().Contains(search)) && (role == "الكل" || u.Role == role);
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!isEditMode && UsersGrid.SelectedItem is UserAdminDto u) { selectedUser = u; LoadUserToForm(); } }
        private void UsersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (selectedUser != null) SetEditMode(); }

        private void LoadUserToForm() { if (selectedUser == null) return; CodeBox.Text = selectedUser.Id.ToString(); UsernameBox.Text = selectedUser.Username; PasswordBox.Password = ""; FullNameBox.Text = selectedUser.FullName; RoleCombo.Text = selectedUser.Role; IsActiveCheck.IsChecked = selectedUser.IsActive; }
        private void SetAddMode() { isEditMode = false; selectedUser = null; CodeBox.Text = "(جديد)"; UsernameBox.Text = FullNameBox.Text = ""; PasswordBox.Password = ""; RoleCombo.SelectedIndex = 2; IsActiveCheck.IsChecked = true; UsernameBox.Focus(); }
        private void SetEditMode() { if (selectedUser == null) { MessageBox.Show("اختر مستخدم"); return; } isEditMode = true; UsernameBox.Focus(); }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(FullNameBox.Text))
            { MessageBox.Show("أدخل البيانات المطلوبة"); return; }

            _adminService.SaveUser(new UserAdminSaveRequest
            {
                Id = selectedUser?.Id,
                Username = UsernameBox.Text,
                Password = PasswordBox.Password,
                FullName = FullNameBox.Text,
                Role = ((ComboBoxItem)RoleCombo.SelectedItem).Content.ToString(),
                IsActive = IsActiveCheck.IsChecked ?? true
            });
            LoadUsers(); SetAddMode(); MarkAsClean(); MessageBox.Show("تم الحفظ");
        }

        private void DeleteUser() { if (selectedUser == null || selectedUser.Username == "admin") { MessageBox.Show("لا يمكن حذف هذا المستخدم"); return; } if (!ConfirmDelete(selectedUser.Username)) return; _adminService.DeleteUser(selectedUser.Id); LoadUsers(); SetAddMode(); }

        private void AddButton_Click(object sender, RoutedEventArgs e) => SetAddMode();
        private void EditButton_Click(object sender, RoutedEventArgs e) => SetEditMode();
        private void DeleteButton_Click(object sender, RoutedEventArgs e) => DeleteUser();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadUsers();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => SetAddMode();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterUsers();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
