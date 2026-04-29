using SupermarketPOS.UI.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls
{
    public partial class PaginationControl : UserControl
    {
        public static readonly DependencyProperty TotalItemsProperty =
            DependencyProperty.Register(nameof(TotalItems), typeof(int), typeof(PaginationControl), new PropertyMetadata(0));
        public static readonly DependencyProperty PageSizeProperty =
            DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(PaginationControl), new PropertyMetadata(50));
        public static readonly DependencyProperty CurrentPageProperty =
            DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(PaginationControl), new PropertyMetadata(1));

        public event EventHandler<PageChangedEventArgs> PageChanged;

        public int TotalItems { get => (int)GetValue(TotalItemsProperty); set => SetValue(TotalItemsProperty, value); }
        public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }
        public int CurrentPage { get => (int)GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

        public PaginationControl()
        {
            InitializeComponent();
            PageSizeCombo.SelectionChanged += (s, e) =>
            {
                if (PageSizeCombo.SelectedItem is ComboBoxItem i && int.TryParse(i.Content.ToString(), out int sz))
                    PageSize = sz;
            };
            FirstPageButton.Click += (s, e) => { CurrentPage = 1; PageChanged?.Invoke(this, new PageChangedEventArgs { Page = 1, PageSize = PageSize }); };
            PreviousPageButton.Click += (s, e) => { if (CurrentPage > 1) { CurrentPage--; PageChanged?.Invoke(this, new PageChangedEventArgs { Page = CurrentPage, PageSize = PageSize }); } };
            NextPageButton.Click += (s, e) => { if (CurrentPage < TotalPages) { CurrentPage++; PageChanged?.Invoke(this, new PageChangedEventArgs { Page = CurrentPage, PageSize = PageSize }); } };
            LastPageButton.Click += (s, e) => { CurrentPage = TotalPages; PageChanged?.Invoke(this, new PageChangedEventArgs { Page = CurrentPage, PageSize = PageSize }); };
        }

        public void Reset() => CurrentPage = 1;
    }

    public class PageChangedEventArgs : EventArgs { public int Page { get; set; } public int PageSize { get; set; } }
}