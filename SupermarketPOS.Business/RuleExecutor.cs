using System.Collections.Generic;

namespace SupermarketPOS.Business
{
    public class RuleExecutor
    {
        private readonly ConfigurationService configurationService = new ConfigurationService();
        private readonly IEnumerable<IRule> rules;

        public RuleExecutor(IEnumerable<IRule> rules)
        {
            this.rules = rules;
        }

        public string Execute(SaleRequest request, SaleRuleContext context)
        {
            configurationService.RefreshConfiguration();

            foreach (var rule in rules)
            {
                var error = rule.Validate(request, context);
                if (!string.IsNullOrWhiteSpace(error))
                    return error;
            }

            return null;
        }
    }
}
