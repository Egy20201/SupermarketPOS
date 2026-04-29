using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls
{
    public partial class FormLayout : UserControl
    {
        public Button Save => SaveButton;
        public Button Cancel => CancelButton;

        public FormLayout()
        {
            InitializeComponent();
        }

        public void SetTitle(string title) => FormTitle.Text = title;

        public void AddSection(string title, UIElement content)
        {
            var section = new FormSection(title, content);
            SectionsContainer.Items.Add(section);
        }

        public void ClearSections() => SectionsContainer.Items.Clear();
    }

    public class FormSection : Border
    {
        public FormSection(string title, UIElement content)
        {
            Style = Application.Current.FindResource("CardBorderStyle") as Style;
            Padding = new Thickness(20);
            Margin = new Thickness(0, 0, 0, 16);

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextPrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 16)
            });
            stackPanel.Children.Add(content);
            Child = stackPanel;
        }
    }
}