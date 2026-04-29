using System;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Outcome of a workflow rule evaluation.
    /// Block aborts the rule chain immediately. Continue allows the chain
    /// to keep evaluating subsequent rules.
    /// </summary>
    public enum RuleOutcome
    {
        Continue = 0,
        Block = 1
    }

    /// <summary>
    /// Result of evaluating a single workflow rule.
    /// </summary>
    public sealed class RuleResult
    {
        public RuleOutcome Outcome { get; }
        public string RuleName { get; }
        public string Message { get; }

        public RuleResult(RuleOutcome outcome, string ruleName, string message)
        {
            if (string.IsNullOrWhiteSpace(ruleName)) throw new ArgumentException("ruleName required", nameof(ruleName));
            Outcome = outcome;
            RuleName = ruleName;
            Message = message ?? string.Empty;
        }

        public bool IsBlocking => Outcome == RuleOutcome.Block;

        public static RuleResult Continue(string ruleName, string message = null)
            => new RuleResult(RuleOutcome.Continue, ruleName, message);

        public static RuleResult Block(string ruleName, string message)
            => new RuleResult(RuleOutcome.Block, ruleName, message);
    }
}
