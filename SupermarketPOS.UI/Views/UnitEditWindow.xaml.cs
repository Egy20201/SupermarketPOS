using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System.Windows;

namespace SupermarketPOS.UI.Views
{
    public partial class UnitEditWindow : Window
    {
        private readonly UnitService _unitService;
        private readonly UnitDto _editing;

        public UnitEditWindow(UnitDto unit = null)
        {
            InitializeComponent();
            _unitService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<UnitService>();
            _editing = unit;
            TypeCombo.SelectedIndex = 0;
            if (_editing != null)
            {
                NameBox.Text = _editing.Name;
                TypeCombo.Text = "";
                FactorBox.Text = "1";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            decimal.TryParse(FactorBox.Text, out var factor);

            var result = _unitService.Save(new UnitSaveRequest
            {
                UnitId = _editing?.Id,
                Name = NameBox.Text,
                ConversionFactor = factor
            });

            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
