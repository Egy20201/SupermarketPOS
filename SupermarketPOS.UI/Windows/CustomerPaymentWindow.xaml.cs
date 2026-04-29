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
    public partial class CustomerPaymentWindow : BaseWindow
    {
        private readonly PaymentService paymentService;
        private readonly ObservableCollection<CustomerAllocationRow> invoiceRows = new ObservableCollection<CustomerAllocationRow>();
        private readonly ObservableCollection<string> recentTransactions = new ObservableCollection<string>();
        private PaymentCustomerDto selectedCustomer;

        public CustomerPaymentWindow()
        {
            InitializeComponent();
            paymentService = DependencyInjection.GetRequiredService<PaymentService>();
            RegisterFormShortcuts(() => SaveButton_Click(null, null), () => Close());
            InvoiceGrid.ItemsSource = invoiceRows;
            RecentTransactionsList.ItemsSource = recentTransactions;
            PaymentNumberBox.Text = BuildPaymentNumber();
            LoadCustomers();
            UpdateSummary();
        }

        private void LoadCustomers()
        {
            CustomerCombo.ItemsSource = paymentService.GetActiveCustomers();
        }

        private void CustomerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedCustomer = CustomerCombo.SelectedItem as PaymentCustomerDto;
            invoiceRows.Clear();
            recentTransactions.Clear();
            ValidationText.Text = string.Empty;

            if (selectedCustomer == null)
            {
                CustomerNameText.Text = "الاسم: -";
                CustomerBalanceText.Text = "💰 الرصيد الحالي: -";
                UpdateSummary();
                return;
            }

            CustomerNameText.Text = "الاسم: " + selectedCustomer.Name;
            CustomerBalanceText.Text = $"💰 الرصيد الحالي: {selectedCustomer.Balance:N2} ج.م";

            foreach (var invoice in paymentService.GetCustomerOutstandingInvoices(selectedCustomer.Id).Where(i => i.Id > 0))
            {
                var row = new CustomerAllocationRow
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    RemainingAmount = invoice.RemainingAmount
                };
                row.PropertyChanged += AllocationRow_PropertyChanged;
                invoiceRows.Add(row);
            }

            LoadRecentTransactions(selectedCustomer.Id);
            UpdateSummary();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            if (selectedCustomer == null || !decimal.TryParse(AmountBox.Text, out var totalAmount) || totalAmount <= 0)
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
                var invoiceResult = paymentService.CreateCustomerPayment(new CustomerPaymentRequest
                {
                    CustomerId = selectedCustomer.Id,
                    SaleInvoiceId = row.InvoiceId,
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
                var generalResult = paymentService.CreateCustomerPayment(new CustomerPaymentRequest
                {
                    CustomerId = selectedCustomer.Id,
                    SaleInvoiceId = null,
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

        private void LoadRecentTransactions(int customerId)
        {
            var transactions = paymentService.GetRecentCustomerPayments(customerId);
            foreach (var tx in transactions)
            {
                recentTransactions.Add($"{tx.Date:dd/MM/yyyy} | {tx.Reference} | {tx.Amount:N2} ج.م");
            }
        }

        private static string BuildPaymentNumber() => "CPM-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class CustomerAllocationRow : INotifyPropertyChanged
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
