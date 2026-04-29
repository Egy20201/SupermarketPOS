using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI.Windows
{
    public partial class PipelineWindow : Window
    {
        private readonly CrmService _crmService;

        public PipelineWindow()
        {
            InitializeComponent();
            try
            {
                _crmService = DependencyInjection.GetRequiredService<CrmService>();
                LoadPipeline();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PipelineWindow initialization failed");
            }
        }

        private void LoadPipeline()
        {
            var opportunities = _crmService.GetPipeline(App.CurrentUser?.BranchId);
            var stages = new[] { "تأهيل", "عرض سعر", "تفاوض", "مكسب" };
            var stageEnum = new[] { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation, OpportunityStage.ClosedWon };

            for (int i = 0; i < stages.Length; i++)
            {
                var stageOpps = opportunities.Where(o => o.Stage == stageEnum[i]).ToList();
                var card = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")),
                    CornerRadius = new CornerRadius(12),
                    Width = 280,
                    Margin = new Thickness(0, 0, 16, 0),
                    Padding = new Thickness(16)
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = stages[i], FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });
                stack.Children.Add(new TextBlock { Text = $"{stageOpps.Count} فرصة — {stageOpps.Sum(o => o.Amount):N2} ج.م", FontSize = 13, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B0B0")), Margin = new Thickness(0, 0, 0, 12) });

                foreach (var opp in stageOpps)
                {
                    var item = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12),
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    var itemStack = new StackPanel();
                    itemStack.Children.Add(new TextBlock { Text = opp.Title, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
                    itemStack.Children.Add(new TextBlock { Text = $"{opp.Amount:N2} ج.م — {opp.Probability}%", FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) });
                    item.Child = itemStack;
                    stack.Children.Add(item);
                }

                card.Child = stack;
                PipelinePanel.Children.Add(card);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}