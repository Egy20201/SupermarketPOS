using System.Collections.Generic;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// A workflow rule that participates in a deterministic rule chain.
    /// Lower <see cref="Priority"/> values execute first.
    /// </summary>
    public interface IWorkflowRule
    {
        /// <summary>Stable identifier; used for idempotency and logging.</summary>
        string Name { get; }

        /// <summary>Lower runs first. Default convention: 100.</summary>
        int Priority { get; }

        /// <summary>
        /// Optional set of field names this rule cares about. When non-empty,
        /// the rule will only be evaluated if the change context indicates one
        /// of these fields actually changed. Returning an empty set means the
        /// rule is always evaluated.
        /// </summary>
        IReadOnlyCollection<string> WatchFields { get; }

        /// <summary>Evaluate the rule against the provided context.</summary>
        RuleResult Evaluate(WorkflowContext context);
    }
}
