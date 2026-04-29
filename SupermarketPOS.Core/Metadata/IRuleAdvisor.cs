using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Phase 4.5, Step 9: AI-ready rule advisor interface.
    /// Analyzes entity data and suggests workflow rules.
    /// Stub implementation for now — ready for AI/ML integration.
    /// </summary>
    public interface IRuleAdvisor
    {
        Task<List<RuleSuggestion>> SuggestRulesAsync(
            string entityName,
            Dictionary<string, object> data);
    }

    public class RuleSuggestion
    {
        public string RuleName { get; set; }
        public string ConditionExpression { get; set; }
        public string SuggestedActionType { get; set; }
        public string Reason { get; set; }
        public double Confidence { get; set; }
    }
}
