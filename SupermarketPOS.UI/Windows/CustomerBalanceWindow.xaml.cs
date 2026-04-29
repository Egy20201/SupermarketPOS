using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SupermarketPOS.UI
{
    public partial class CustomerBalanceWindow : BaseWindow
    {
        private ObservableCollection<PartyBalanceDto> items;
        private System.Collections.Generic.List<PartyBalanceDto> allItems;
        private readonly PartyAccountService _partyAccountService;

        public CustomerBalanceWindow()
        {
            InitializeComponent();
            _partyAccountService = DependencyInjection.GetRequiredService<PartyAccountService>();
            items = new ObservableCollection<PartyBalanceDto>();
            BalanceGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            LoadData();
        }

        private void LoadData()
        {
            allItems = _partyAccountService.GetCustomerBalances();
            items.Clear();
            foreach (var i in allItems) items.Add(i);
            TotalCustomersText.Text = $"👥 عدد العملاء: {allItems.Count}";
            TotalBalanceText.Text = $"💰 إجمالي الأرصدة: {allItems.Sum(c => c.Balance):N2} ج.م";
        }

        private void FilterGrid()
        {
            var search = SearchBox.Text?.Trim().ToLower() ?? "";
            var view = CollectionViewSource.GetDefaultView(BalanceGrid.ItemsSource);
            view.Filter = o => o is PartyBalanceDto c && (string.IsNullOrEmpty(search) || c.Name.ToLower().Contains(search) || c.Phone?.Contains(search) == true);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterGrid();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(BalanceGrid, "أرصدة العملاء");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
