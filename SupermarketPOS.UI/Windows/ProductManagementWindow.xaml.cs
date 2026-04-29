using SupermarketPOS.UI.Views;
using System.Windows;

namespace SupermarketPOS.UI.Windows
{
    public partial class ProductManagementWindow : BaseWindow
    {
        public ProductManagementWindow()
        {
            InitializeComponent();
            ShowProducts();
        }

        private void ShowProducts()
        {
            ContentHost.Content = new ProductsView();
            SetSelected(ProductsNavButton);
        }

        private void ShowCategories()
        {
            ContentHost.Content = new CategoriesView();
            SetSelected(CategoriesNavButton);
        }

        private void ShowUnits()
        {
            ContentHost.Content = new UnitsView();
            SetSelected(UnitsNavButton);
        }

        private void ShowBulk()
        {
            ContentHost.Content = new BulkEntryView();
            SetSelected(BulkNavButton);
        }

        private void SetSelected(System.Windows.Controls.Button selected)
        {
            ProductsNavButton.Style = (Style)FindResource("SecondaryButtonStyle");
            CategoriesNavButton.Style = (Style)FindResource("SecondaryButtonStyle");
            UnitsNavButton.Style = (Style)FindResource("SecondaryButtonStyle");
            BulkNavButton.Style = (Style)FindResource("SecondaryButtonStyle");
            selected.Style = (Style)FindResource("PrimaryButtonStyle");
        }

        private void ProductsNavButton_Click(object sender, RoutedEventArgs e) => ShowProducts();
        private void CategoriesNavButton_Click(object sender, RoutedEventArgs e) => ShowCategories();
        private void UnitsNavButton_Click(object sender, RoutedEventArgs e) => ShowUnits();
        private void BulkNavButton_Click(object sender, RoutedEventArgs e) => ShowBulk();
    }
}