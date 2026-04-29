using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls
{
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay() => InitializeComponent();
        public LoadingOverlay(string message) { InitializeComponent(); LoadingText.Text = message; }
        public void SetMessage(string message) => LoadingText.Text = message;
    }
}