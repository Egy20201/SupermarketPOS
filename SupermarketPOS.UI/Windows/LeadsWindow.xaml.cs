using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class LeadsWindow : Window
    {
        private readonly CrmService _crmService;
        private ObservableCollection<Lead> _leads;

        public LeadsWindow()
        {
            InitializeComponent();
            try
            {
                _crmService = DependencyInjection.GetRequiredService<CrmService>();
                _leads = new ObservableCollection<Lead>();
                LeadsGrid.ItemsSource = _leads;
                LoadLeads();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "LeadsWindow initialization failed");
            }
        }

        private void LoadLeads()
        {
            var leads = _crmService.GetLeads(App.CurrentUser?.BranchId);
            _leads.Clear();
            foreach (var l in leads) _leads.Add(l);

            TotalLeadsText.Text = $"إجمالي: {leads.Count}";
            NewLeadsText.Text = $"جديد: {leads.FindAll(l => l.Status == LeadStatus.New).Count}";
            QualifiedText.Text = $"مؤهل: {leads.FindAll(l => l.Status == LeadStatus.Qualified).Count}";
        }

        private void AddLead_Click(object sender, RoutedEventArgs e)
        {
            var window = new LeadEditWindow(_crmService);
            if (window.ShowDialog() == true) LoadLeads();
        }

        private void ConvertLead_Click(object sender, RoutedEventArgs e)
        {
            if (LeadsGrid.SelectedItem is Lead lead)
            {
                if (MessageBox.Show("تحويل هذا العميل المحتمل إلى عميل فعلي؟", "تأكيد",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var customer = _crmService.ConvertToCustomer(lead.Id, App.CurrentUser?.Id ?? 1);
                    MessageBox.Show($"تم إنشاء العميل: {customer.Name}", "نجاح");
                    LoadLeads();
                }
            }
        }

        private void MarkContacted_Click(object sender, RoutedEventArgs e)
        {
            if (LeadsGrid.SelectedItem is Lead lead)
            {
                _crmService.UpdateLeadStatus(lead.Id, LeadStatus.Contacted, App.CurrentUser?.Id ?? 1);
                LoadLeads();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}