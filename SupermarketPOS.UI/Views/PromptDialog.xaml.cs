using System.Windows;

namespace SupermarketPOS.UI.Views
{
    public partial class PromptDialog : Window
    {
        public string Result { get; private set; }

        public PromptDialog(string prompt, string title)
        {
            InitializeComponent();
            TitleText.Text = prompt;
            Title = title;
            InputBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Result = InputBox.Text;
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