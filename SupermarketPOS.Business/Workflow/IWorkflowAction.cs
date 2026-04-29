using System;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// A side-effect that runs after the rule chain succeeds.
    /// Actions may carry an <see cref="ICondition"/>; if present and
    /// not satisfied, <see cref="Execute"/> is skipped.
    /// </summary>
    public interface IWorkflowAction
    {
        string Name { get; }
        ICondition Condition { get; }
        void Execute(WorkflowContext context);
    }

    /// <summary>Convenience adapter for delegate-based actions.</summary>
    public sealed class DelegateWorkflowAction : IWorkflowAction
    {
        private readonly Action<WorkflowContext> _execute;

        public DelegateWorkflowAction(string name, Action<WorkflowContext> execute, ICondition condition = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name required", nameof(name));
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            Name = name;
            _execute = execute;
            Condition = condition ?? Conditions.Always;
        }

        public string Name { get; }
        public ICondition Condition { get; }
        public void Execute(WorkflowContext context) => _execute(context);
    }
}
