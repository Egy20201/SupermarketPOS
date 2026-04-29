using SupermarketPOS.Core.Metadata;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4.5, Step 9: Stub implementation of IRuleAdvisor.
    /// Returns empty suggestions. Replace with AI/ML implementation when ready.
    /// </summary>
    public sealed class StubRuleAdvisor : IRuleAdvisor
    {
        public Task<List<RuleSuggestion>> SuggestRulesAsync(
            string entityName,
            Dictionary<string, object> data)
        {
            return Task.FromResult(new List<RuleSuggestion>());
        }
    }
}
