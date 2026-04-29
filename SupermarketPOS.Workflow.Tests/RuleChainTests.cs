using System.Collections.Generic;
using System.Linq;
using SupermarketPOS.Business.Workflow;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class RuleChainTests
    {
        private sealed class TrackingRule : IWorkflowRule
        {
            private readonly RuleResult _result;
            private readonly List<string> _trace;

            public TrackingRule(string name, int priority, RuleOutcome outcome, List<string> trace, IReadOnlyCollection<string> watchFields = null)
            {
                Name = name;
                Priority = priority;
                _trace = trace;
                WatchFields = watchFields ?? new List<string>().AsReadOnly();
                _result = outcome == RuleOutcome.Block
                    ? RuleResult.Block(name, name + ":blocked")
                    : RuleResult.Continue(name, name + ":continue");
            }

            public string Name { get; }
            public int Priority { get; }
            public IReadOnlyCollection<string> WatchFields { get; }

            public RuleResult Evaluate(WorkflowContext context)
            {
                _trace.Add(Name);
                return _result;
            }
        }

        private static WorkflowContext NewContext(FieldChangeSet changes = null, string idempotencyKey = null)
            => new WorkflowContext(
                entityType: "Test",
                entityId: "1",
                entity: new object(),
                changes: changes,
                idempotencyKey: idempotencyKey);

        [Fact]
        public void RulesExecuteInPriorityOrder()
        {
            var trace = new List<string>();
            var rules = new IWorkflowRule[]
            {
                new TrackingRule("C", priority: 30, RuleOutcome.Continue, trace),
                new TrackingRule("A", priority: 10, RuleOutcome.Continue, trace),
                new TrackingRule("B", priority: 20, RuleOutcome.Continue, trace)
            };

            var engine = new WorkflowRuleEngine();
            var result = engine.Run(rules, NewContext());

            Assert.False(result.Blocked);
            Assert.Equal(new[] { "A", "B", "C" }, trace);
            Assert.Equal(3, result.Results.Count);
        }

        [Fact]
        public void BlockingRuleStopsTheChain()
        {
            var trace = new List<string>();
            var rules = new IWorkflowRule[]
            {
                new TrackingRule("first", priority: 10, RuleOutcome.Continue, trace),
                new TrackingRule("blocker", priority: 20, RuleOutcome.Block, trace),
                new TrackingRule("never", priority: 30, RuleOutcome.Continue, trace)
            };

            var engine = new WorkflowRuleEngine();
            var result = engine.Run(rules, NewContext());

            Assert.True(result.Blocked);
            Assert.Equal("blocker", result.BlockingRule);
            Assert.Equal("blocker:blocked", result.BlockingMessage);
            Assert.Equal(new[] { "first", "blocker" }, trace);
            Assert.DoesNotContain("never", trace);
        }

        [Fact]
        public void ContinueResultsAreAccumulated()
        {
            var trace = new List<string>();
            var rules = new IWorkflowRule[]
            {
                new TrackingRule("A", 10, RuleOutcome.Continue, trace),
                new TrackingRule("B", 20, RuleOutcome.Continue, trace)
            };

            var result = new WorkflowRuleEngine().Run(rules, NewContext());

            Assert.False(result.Blocked);
            Assert.Equal(2, result.Results.Count);
            Assert.All(result.Results, r => Assert.Equal(RuleOutcome.Continue, r.Outcome));
            Assert.Equal(new[] { "A:continue", "B:continue" }, result.Results.Select(r => r.Message).ToArray());
        }

        [Fact]
        public void FieldScopedRuleIsSkippedWhenWatchedFieldDidNotChange()
        {
            var trace = new List<string>();
            var rules = new IWorkflowRule[]
            {
                new TrackingRule("price", 10, RuleOutcome.Continue, trace, new List<string> { "Price" }.AsReadOnly()),
                new TrackingRule("note", 20, RuleOutcome.Continue, trace, new List<string> { "Notes" }.AsReadOnly()),
                new TrackingRule("any", 30, RuleOutcome.Continue, trace)
            };

            var changes = new FieldChangeSet(new[] { new FieldChange("Notes", "old", "new") });
            var result = new WorkflowRuleEngine().Run(rules, NewContext(changes));

            Assert.False(result.Blocked);
            Assert.Equal(new[] { "note", "any" }, trace);
            Assert.DoesNotContain("price", trace);
        }

        [Fact]
        public void FieldScopedRulesAreNotRunWhenNoChangesDeclared()
        {
            var trace = new List<string>();
            var rules = new IWorkflowRule[]
            {
                new TrackingRule("price", 10, RuleOutcome.Continue, trace, new List<string> { "Price" }.AsReadOnly()),
                new TrackingRule("any", 20, RuleOutcome.Continue, trace)
            };

            var result = new WorkflowRuleEngine().Run(rules, NewContext());

            Assert.False(result.Blocked);
            Assert.Equal(new[] { "any" }, trace);
        }

        [Fact]
        public void IdempotencyKeySuppressesDuplicateRuleExecution()
        {
            var trace = new List<string>();
            var rules = new IWorkflowRule[]
            {
                new TrackingRule("A", 10, RuleOutcome.Continue, trace),
                new TrackingRule("B", 20, RuleOutcome.Continue, trace)
            };

            var store = new InMemoryIdempotencyStore();
            var engine = new WorkflowRuleEngine(store);

            var ctx = NewContext(idempotencyKey: "evt-123");
            var first = engine.Run(rules, ctx);
            var second = engine.Run(rules, ctx);

            Assert.False(first.Blocked);
            Assert.False(second.Blocked);
            Assert.Equal(new[] { "A", "B" }, trace);
            Assert.Equal(2, first.Results.Count);
            Assert.Empty(second.Results);
        }
    }
}
