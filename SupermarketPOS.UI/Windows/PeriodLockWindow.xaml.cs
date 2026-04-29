using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI
{
    public partial class PeriodLockWindow : BaseWindow
    {
        private ObservableCollection<FiscalPeriod> periods;
        private readonly AccountingService _accountingService;

        public PeriodLockWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            periods = new ObservableCollection<FiscalPeriod>();
            PeriodsGrid.ItemsSource = periods;
            RegisterListShortcuts(null, null, null, null, () => LoadPeriods(), null);

            for (int y = DateTime.Now.Year - 2; y <= DateTime.Now.Year + 1; y++)
                YearCombo.Items.Add(y);
            YearCombo.SelectedItem = DateTime.Now.Year;

            MonthCombo.Items.Add("السنة كاملة");
            for (int m = 1; m <= 12; m++)
                MonthCombo.Items.Add($"{m:D2}");
            MonthCombo.SelectedIndex = 0;

            LoadPeriods();
        }

        private void LoadPeriods()
        {
            var list = _accountingService.GetLockedPeriods();
            periods.Clear();
            foreach (var p in list) periods.Add(p);
        }

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            if (YearCombo.SelectedItem == null) return;

            int year = (int)YearCombo.SelectedItem;
            int month = MonthCombo.SelectedIndex - 1;
            string periodName = month == -1 ? $"السنة كاملة {year}" : $"{month + 1:D2}/{year}";

            if (MessageBox.Show($"هل أنت متأكد من قفل {periodName}؟\nلن يتمكن أي مستخدم من إجراء عمليات في هذه الفترة.", "تأكيد القفل", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _accountingService.LockPeriod(year, month, App.CurrentUser?.Id, ReasonBox.Text);

            ReasonBox.Text = "";
            LoadPeriods();
            MessageBox.Show($"تم قفل {periodName} بنجاح");
        }

        private void UnlockButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FiscalPeriod p)
            {
                string periodName = p.Month == 0 ? $"السنة كاملة {p.Year}" : $"{p.Month:D2}/{p.Year}";
                if (MessageBox.Show($"هل أنت متأكد من فتح {periodName}؟", "تأكيد الفتح", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                _accountingService.UnlockPeriod(p.Id);
                LoadPeriods();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
