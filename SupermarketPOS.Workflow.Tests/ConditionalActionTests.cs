using System.Collections.Generic;
using SupermarketPOS.Business.Workflow;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class ConditionalActionTests
    {
        private static WorkflowContext Ctx(FieldChangeSet changes = null, string idempotencyKey = null)
            => new WorkflowContext("Test", "1", new object(), changes: changes, idempotencyKey: idempotencyKey);

        [Fact]
        public void ActionWithUnsatisfiedConditionIsSkipped()
        {
            var ran = new List<string>();
            var actions = new IWorkflowAction[]
            {
                new DelegateWorkflowAction("always", _ => ran.Add("always")),
                new DelegateWorkflowAction("never", _ => ran.Add("never"), Conditions.Never),
                new DelegateWorkflowAction("on-status",
                    _ => ran.Add("on-status"),
                    Conditions.FieldChanged("Status"))
            };

            var changes = new FieldChangeSet(new[] { new FieldChange("Notes", "a", "b") });

            new ActionExecutor().Run(actions, Ctx(changes));

            Assert.Equal(new[] { "always" }, ran);
        }

        [Fact]
        public void ActionFiresWhenWatchedFieldChanges()
        {
            var ran = new List<string>();
            var actions = new IWorkflowAction[]
            {
                new DelegateWorkflowAction("on-status",
                    _ => ran.Add("on-status"),
                    Conditions.FieldChanged("Status"))
            };

            var changes = new FieldChangeSet(new[] { new FieldChange("Status", "Draft", "Pending") });
            new ActionExecutor().Run(actions, Ctx(changes));

            Assert.Equal(new[] { "on-status" }, ran);
        }

        [Fact]
        public void IdempotencyKeyPreventsDuplicateActionExecution()
        {
            var ran = new List<string>();
            var actions = new IWorkflowAction[]
            {
                new DelegateWorkflowAction("notify", _ => ran.Add("notify"))
            };

            var executor = new ActionExecutor(new InMemoryIdempotencyStore());
            var ctx = Ctx(idempotencyKey: "evt-42");

            executor.Run(actions, ctx);
            executor.Run(actions, ctx);
            executor.Run(actions, ctx);

            Assert.Single(ran);
        }

        [Fact]
        public void ActionFailureRollsBackIdempotencyMark()
        {
            var attempts = 0;
            var actions = new IWorkflowAction[]
            {
                new DelegateWorkflowAction("flaky", _ =>
                {
                    attempts++;
                    if (attempts == 1) throw new System.InvalidOperationException("boom");
                })
            };

            var executor = new ActionExecutor(new InMemoryIdempotencyStore());
            var ctx = Ctx(idempotencyKey: "evt-7");

            Assert.Throws<System.InvalidOperationException>(() => executor.Run(actions, ctx));
            // Retry should re-execute since the failed mark was rolled back.
            executor.Run(actions, ctx);

            Assert.Equal(2, attempts);
        }

        [Fact]
        public void CompositeConditionsCombineAsExpected()
        {
            var changes = new FieldChangeSet(new[]
            {
                new FieldChange("Price", 10m, 20m),
                new FieldChange("Quantity", 1, 2)
            });
            var ctx = Ctx(changes);

            var both = Conditions.All(Conditions.FieldChanged("Price"), Conditions.FieldChanged("Quantity"));
            var either = Conditions.Any(Conditions.FieldChanged("Notes"), Conditions.FieldChanged("Price"));
            var neither = Conditions.Any(Conditions.FieldChanged("Notes"), Conditions.FieldChanged("Comment"));

            Assert.True(both.IsSatisfied(ctx));
            Assert.True(either.IsSatisfied(ctx));
            Assert.False(neither.IsSatisfied(ctx));
        }
    }
}
