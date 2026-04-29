using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>Predicate over a <see cref="WorkflowContext"/>.</summary>
    public interface ICondition
    {
        bool IsSatisfied(WorkflowContext context);
    }

    /// <summary>Common condition combinators and built-ins.</summary>
    public static class Conditions
    {
        public static ICondition Always { get; } = new AlwaysCondition(true);
        public static ICondition Never { get; } = new AlwaysCondition(false);

        public static ICondition When(Func<WorkflowContext, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return new DelegateCondition(predicate);
        }

        public static ICondition FieldChanged(params string[] fieldNames)
        {
            if (fieldNames == null || fieldNames.Length == 0) return Always;
            var copy = fieldNames.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray();
            return new FieldChangedCondition(copy);
        }

        public static ICondition All(params ICondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return Always;
            return new CompositeCondition(conditions, requireAll: true);
        }

        public static ICondition Any(params ICondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return Never;
            return new CompositeCondition(conditions, requireAll: false);
        }

        private sealed class AlwaysCondition : ICondition
        {
            private readonly bool _value;
            public AlwaysCondition(bool value) { _value = value; }
            public bool IsSatisfied(WorkflowContext context) => _value;
        }

        private sealed class DelegateCondition : ICondition
        {
            private readonly Func<WorkflowContext, bool> _predicate;
            public DelegateCondition(Func<WorkflowContext, bool> predicate) { _predicate = predicate; }
            public bool IsSatisfied(WorkflowContext context) => _predicate(context);
        }

        private sealed class FieldChangedCondition : ICondition
        {
            private readonly IReadOnlyCollection<string> _fields;
            public FieldChangedCondition(string[] fields) { _fields = fields; }
            public bool IsSatisfied(WorkflowContext context)
                => context != null && context.Changes != null && context.Changes.AnyChanged(_fields);
        }

        private sealed class CompositeCondition : ICondition
        {
            private readonly ICondition[] _conditions;
            private readonly bool _requireAll;

            public CompositeCondition(ICondition[] conditions, bool requireAll)
            {
                _conditions = conditions;
                _requireAll = requireAll;
            }

            public bool IsSatisfied(WorkflowContext context)
            {
                foreach (var c in _conditions)
                {
                    var ok = c != null && c.IsSatisfied(context);
                    if (_requireAll && !ok) return false;
                    if (!_requireAll && ok) return true;
                }
                return _requireAll;
            }
        }
    }
}
