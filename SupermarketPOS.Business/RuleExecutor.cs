using SupermarketPOS.Business.Workflow;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    /// <summary>
    /// Sale-rule executor. Wraps the legacy <see cref="IRule"/> contract on top
    /// of the new <see cref="WorkflowRuleEngine"/> so existing callers keep
    /// returning a single error message while still benefiting from priority
    /// ordering and Block/Continue semantics.
    /// </summary>
    public class RuleExecutor
    {
        private readonly ConfigurationService configurationService = new ConfigurationService();
        private readonly IList<IWorkflowRule> _rules;
        private readonly WorkflowRuleEngine _engine;

        public RuleExecutor(IEnumerable<IRule> rules, IIdempotencyStore idempotencyStore = null)
        {
            _rules = (rules ?? Enumerable.Empty<IRule>())
                .Where(r => r != null)
                .Select<IRule, IWorkflowRule>((r, idx) => new LegacyRuleAdapter(r, 100 + idx))
                .ToList();
            _engine = new WorkflowRuleEngine(idempotencyStore);
        }

        public string Execute(SaleRequest request, SaleRuleContext context)
        {
            configurationService.RefreshConfiguration();

            var items = new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase)
            {
                { LegacyRuleAdapter.RequestKey, request },
                { LegacyRuleAdapter.ContextKey, context }
            };

            var workflowContext = new WorkflowContext(
                entityType: "SaleRequest",
                entityId: request?.InvoiceNumber,
                entity: request,
                items: items);

            var result = _engine.Run(_rules, workflowContext);
            return result.Blocked ? result.BlockingMessage : null;
        }

        private sealed class LegacyRuleAdapter : IWorkflowRule
        {
            public const string RequestKey = "__sale_request";
            public const string ContextKey = "__sale_rule_context";

            private static readonly IReadOnlyCollection<string> _empty = new List<string>().AsReadOnly();

            private readonly IRule _inner;

            public LegacyRuleAdapter(IRule inner, int priority)
            {
                _inner = inner;
                Priority = priority;
                Name = inner.GetType().Name;
            }

            public string Name { get; }
            public int Priority { get; }
            public IReadOnlyCollection<string> WatchFields => _empty;

            public RuleResult Evaluate(WorkflowContext context)
            {
                var request = context.Items.TryGetValue(RequestKey, out var r) ? r as SaleRequest : null;
                var ruleContext = context.Items.TryGetValue(ContextKey, out var c) ? c as SaleRuleContext : null;
                var error = _inner.Validate(request, ruleContext);
                if (string.IsNullOrWhiteSpace(error)) return RuleResult.Continue(Name);
                return RuleResult.Block(Name, error);
            }
        }
    }
}
