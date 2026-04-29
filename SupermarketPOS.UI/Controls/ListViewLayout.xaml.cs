using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SupermarketPOS.UI.Controls
{
    public partial class ListViewLayout : UserControl
    {
        public DataGrid Grid => DataGrid;
        public TextBox Search => SearchBox;
        public Button Filter => FilterButton;
        public Button Create => CreateButton;
        public Button FirstPage => FirstPageBtn;
        public Button PrevPage => PrevPageBtn;
        public Button NextPage => NextPageBtn;
        public Button LastPage => LastPageBtn;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ListViewLayout));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public ListViewLayout()
        {
            InitializeComponent();
            DataGrid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding { Source = this, Path = new PropertyPath(ItemsSourceProperty) });
        }

        public void SetColumns(params DataGridColumn[] columns)
        {
            DataGrid.Columns.Clear();
            foreach (var col in columns)
                DataGrid.Columns.Add(col);
        }

        public void SetPaginationInfo(int currentPage, int totalPages, int totalItems)
        {
            PageInfoText.Text = $"{currentPage} / {totalPages}";
            ItemsCountText.Text = $"📊 إجمالي: {totalItems} عنصر";

            FirstPageBtn.IsEnabled = currentPage > 1;
            PrevPageBtn.IsEnabled = currentPage > 1;
            NextPageBtn.IsEnabled = currentPage < totalPages;
            LastPageBtn.IsEnabled = currentPage < totalPages;
        }

        public object GetSelectedItem() => DataGrid.SelectedItem;
        public void SetItemsSource(IEnumerable itemsSource) => ItemsSource = itemsSource;
        public void ClearSelection() => DataGrid.SelectedItem = null;
    }
}
