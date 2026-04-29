using SupermarketPOS.Business;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI
{
    public partial class SupplierPaymentWindow : BaseWindow
    {
        private readonly PaymentService paymentService;
        private readonly ObservableCollection<SupplierAllocationRow> invoiceRows = new ObservableCollection<SupplierAllocationRow>();
        private readonly ObservableCollection<string> recentTransactions = new ObservableCollection<string>();
        private PaymentSupplierDto selectedSupplier;

        public SupplierPaymentWindow()
        {
            InitializeComponent();
            paymentService = DependencyInjection.GetRequiredService<PaymentService>();
            RegisterFormShortcuts(() => SaveButton_Click(null, null), () => Close());
            InvoiceGrid.ItemsSource = invoiceRows;
            RecentTransactionsList.ItemsSource = recentTransactions;
            PaymentNumberBox.Text = BuildPaymentNumber();
            LoadSuppliers();
            UpdateSummary();
        }

        private void LoadSuppliers()
        {
            SupplierCombo.ItemsSource = paymentService.GetSuppliers();
        }

        private void SupplierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedSupplier = SupplierCombo.SelectedItem as PaymentSupplierDto;
            invoiceRows.Clear();
            recentTransactions.Clear();
            ValidationText.Text = string.Empty;

            if (selectedSupplier == null)
            {
                SupplierNameText.Text = "الاسم: -";
                SupplierBalanceText.Text = "💰 الرصيد الحالي: -";
                UpdateSummary();
                return;
            }

            SupplierNameText.Text = "الاسم: " + selectedSupplier.Name;
            SupplierBalanceText.Text = $"💰 الرصيد الحالي: {selectedSupplier.Balance:N2} ج.م";

            foreach (var invoice in paymentService.GetSupplierOutstandingInvoices(selectedSupplier.Id).Where(i => i.Id > 0))
            {
                var row = new SupplierAllocationRow
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    RemainingAmount = invoice.RemainingAmount
                };
                row.PropertyChanged += AllocationRow_PropertyChanged;
                invoiceRows.Add(row);
            }

            LoadRecentTransactions(selectedSupplier.Id);
            UpdateSummary();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            if (selectedSupplier == null || !decimal.TryParse(AmountBox.Text, out var totalAmount) || totalAmount <= 0)
            {
                ValidationText.Text = "أدخل بيانات صحيحة";
                return;
            }

            var method = (MethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "نقدي";
            var allocations = invoiceRows.Where(r => r.AllocationAmount > 0m).ToList();

            if (allocations.Any(r => r.AllocationAmount > r.RemainingAmount))
            {
                ValidationText.Text = "لا يمكن تخصيص مبلغ أكبر من المتبقي.";
                return;
            }

            var allocated = allocations.Sum(r => r.AllocationAmount);
            if (allocated > totalAmount)
            {
                ValidationText.Text = "إجمالي التخصيص أكبر من المبلغ المدخل.";
                return;
            }

            foreach (var row in allocations)
            {
                var invoiceResult = paymentService.CreateSupplierPayment(new SupplierPaymentRequest
                {
                    SupplierId = selectedSupplier.Id,
                    PurchaseInvoiceId = row.InvoiceId,
                    Amount = row.AllocationAmount,
                    Date = PaymentDatePicker.SelectedDate ?? DateTime.Today,
                    Notes = NotesBox.Text,
                    Method = method
                });

                if (!invoiceResult.Success)
                {
                    ValidationText.Text = invoiceResult.ErrorMessage;
                    return;
                }
            }

            var remaining = totalAmount - allocated;
            if (remaining > 0m)
            {
                var generalResult = paymentService.CreateSupplierPayment(new SupplierPaymentRequest
                {
                    SupplierId = selectedSupplier.Id,
                    PurchaseInvoiceId = null,
                    Amount = remaining,
                    Date = PaymentDatePicker.SelectedDate ?? DateTime.Today,
                    Notes = NotesBox.Text,
                    Method = method
                });

                if (!generalResult.Success)
                {
                    ValidationText.Text = generalResult.ErrorMessage;
                    return;
                }
            }

            DashboardEvents.RequestRefresh();
            MarkAsClean();
            MessageBox.Show("تم السداد");
            Close();
        }

        private void AutoAllocate_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            if (!decimal.TryParse(AmountBox.Text, out var totalAmount) || totalAmount <= 0)
            {
                ValidationText.Text = "أدخل مبلغ صحيح قبل التوزيع التلقائي.";
                return;
            }

            var remaining = totalAmount;
            foreach (var row in invoiceRows)
            {
                if (remaining <= 0)
                {
                    row.IsSelected = false;
                    row.AllocationAmount = 0m;
                    continue;
                }

                row.IsSelected = true;
                var allocation = Math.Min(row.RemainingAmount, remaining);
                row.AllocationAmount = allocation;
                remaining -= allocation;
            }

            UpdateSummary();
        }

        private void ClearAllocation_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in invoiceRows)
            {
                row.IsSelected = false;
                row.AllocationAmount = 0m;
            }

            ValidationText.Text = string.Empty;
            UpdateSummary();
        }

        private void AmountBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSummary();
        private void AllocationRow_PropertyChanged(object sender, PropertyChangedEventArgs e) => UpdateSummary();

        private void UpdateSummary()
        {
            decimal.TryParse(AmountBox.Text, out var amount);
            var allocated = invoiceRows.Sum(r => r.AllocationAmount > 0m ? r.AllocationAmount : 0m);
            var unallocated = amount - allocated;
            SummaryText.Text = $"الإجمالي: {amount:N2} ج.م | المخصص: {allocated:N2} ج.م | غير المخصص: {unallocated:N2} ج.م";
        }

        private void LoadRecentTransactions(int supplierId)
        {
            var transactions = paymentService.GetRecentSupplierPayments(supplierId);
            foreach (var tx in transactions)
            {
                recentTransactions.Add($"{tx.Date:dd/MM/yyyy} | {tx.Reference} | {tx.Amount:N2} ج.م");
            }
        }

        private static string BuildPaymentNumber() => "SPM-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class SupplierAllocationRow : INotifyPropertyChanged
    {
        private bool isSelected;
        private decimal allocationAmount;

        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal RemainingAmount { get; set; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                OnPropertyChanged();
                if (!isSelected) AllocationAmount = 0m;
            }
        }

        public decimal AllocationAmount
        {
            get => allocationAmount;
            set
            {
                var safe = value < 0m ? 0m : value;
                if (allocationAmount == safe) return;
                allocationAmount = safe;
                if (allocationAmount > 0m && !IsSelected)
                {
                    isSelected = true;
                    OnPropertyChanged(nameof(IsSelected));
                }
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
