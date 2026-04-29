using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Aggregated outcome of running a rule chain.
    /// </summary>
    public sealed class RuleChainResult
    {
        public bool Blocked { get; }
        public string BlockingMessage { get; }
        public string BlockingRule { get; }
        public IReadOnlyList<RuleResult> Results { get; }

        public RuleChainResult(IList<RuleResult> results, RuleResult blocking)
        {
            Results = (results ?? new List<RuleResult>()).ToList().AsReadOnly();
            if (blocking != null)
            {
                Blocked = true;
                BlockingMessage = blocking.Message;
                BlockingRule = blocking.RuleName;
            }
        }

        public static RuleChainResult Ok(IList<RuleResult> results) => new RuleChainResult(results, null);
        public static RuleChainResult Block(IList<RuleResult> results, RuleResult blocking) => new RuleChainResult(results, blocking);
    }

    /// <summary>
    /// Runs a chain of <see cref="IWorkflowRule"/> instances in priority order.
    /// Stops on the first <see cref="RuleOutcome.Block"/> result.
    /// Skips any rule whose <see cref="IWorkflowRule.WatchFields"/> didn't change.
    /// Honors <see cref="WorkflowContext.IdempotencyKey"/> to suppress duplicate executions.
    /// </summary>
    public sealed class WorkflowRuleEngine
    {
        private readonly IIdempotencyStore _idempotencyStore;

        public WorkflowRuleEngine(IIdempotencyStore idempotencyStore = null)
        {
            _idempotencyStore = idempotencyStore ?? new InMemoryIdempotencyStore();
        }

        public IIdempotencyStore IdempotencyStore => _idempotencyStore;

        public RuleChainResult Run(IEnumerable<IWorkflowRule> rules, WorkflowContext context)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var ordered = rules
                .Where(r => r != null)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            var results = new List<RuleResult>(ordered.Count);

            foreach (var rule in ordered)
            {
                if (!ShouldEvaluate(rule, context))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(context.IdempotencyKey))
                {
                    var scope = BuildScope(rule, context);
                    if (!_idempotencyStore.TryMarkExecuted(scope, context.IdempotencyKey))
                    {
                        // Already executed — skip without affecting block status.
                        continue;
                    }
                }

                RuleResult result;
                try
                {
                    result = rule.Evaluate(context);
                }
                catch (Exception ex)
                {
                    // Exceptions are treated as blocking failures. Roll back
                    // the idempotency mark so a retry can re-attempt.
                    if (!string.IsNullOrWhiteSpace(context.IdempotencyKey))
                    {
                        _idempotencyStore.Forget(BuildScope(rule, context), context.IdempotencyKey);
                    }
                    result = RuleResult.Block(rule.Name, ex.Message);
                }

                if (result == null)
                {
                    result = RuleResult.Continue(rule.Name);
                }

                results.Add(result);

                if (result.IsBlocking)
                {
                    return RuleChainResult.Block(results, result);
                }
            }

            return RuleChainResult.Ok(results);
        }

        private static bool ShouldEvaluate(IWorkflowRule rule, WorkflowContext context)
        {
            if (rule.WatchFields == null || rule.WatchFields.Count == 0) return true;
            if (context.Changes == null || context.Changes.IsEmpty)
            {
                // Field-scoped rules don't run when no changes were declared.
                return false;
            }
            return context.Changes.AnyChanged(rule.WatchFields);
        }

        private static string BuildScope(IWorkflowRule rule, WorkflowContext context)
        {
            return string.Concat(context.EntityType, "/", context.EntityId ?? "(new)", "/", rule.Name);
        }
    }
}
