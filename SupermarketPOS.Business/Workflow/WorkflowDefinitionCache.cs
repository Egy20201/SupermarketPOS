using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// A workflow definition for a single entity type: the set of rules,
    /// the set of actions and the state machine used for status transitions.
    /// </summary>
    public sealed class WorkflowDefinition
    {
        public string EntityType { get; }
        public IReadOnlyList<IWorkflowRule> Rules { get; }
        public IReadOnlyList<IWorkflowAction> Actions { get; }
        public StateMachine<string> StateMachine { get; }

        public WorkflowDefinition(
            string entityType,
            IEnumerable<IWorkflowRule> rules,
            IEnumerable<IWorkflowAction> actions = null,
            StateMachine<string> stateMachine = null)
        {
            if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("entityType required", nameof(entityType));
            EntityType = entityType;
            Rules = (rules ?? Enumerable.Empty<IWorkflowRule>())
                .Where(r => r != null)
                .OrderBy(r => r.Priority)
                .ToList()
                .AsReadOnly();
            Actions = (actions ?? Enumerable.Empty<IWorkflowAction>())
                .Where(a => a != null)
                .ToList()
                .AsReadOnly();
            StateMachine = stateMachine;
        }
    }

    /// <summary>
    /// Caches <see cref="WorkflowDefinition"/> instances keyed by entity type
    /// to avoid hitting the database (or rebuilding rule lists) on every
    /// rule evaluation. Definitions can be invalidated explicitly when their
    /// underlying configuration changes.
    /// </summary>
    public sealed class WorkflowDefinitionCache
    {
        private readonly ConcurrentDictionary<string, WorkflowDefinition> _cache
            = new ConcurrentDictionary<string, WorkflowDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, WorkflowDefinition> _loader;

        public WorkflowDefinitionCache(Func<string, WorkflowDefinition> loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public WorkflowDefinition Get(string entityType)
        {
            if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("entityType required", nameof(entityType));
            return _cache.GetOrAdd(entityType, _loader);
        }

        public void Invalidate(string entityType)
        {
            if (string.IsNullOrWhiteSpace(entityType)) return;
            WorkflowDefinition _;
            _cache.TryRemove(entityType, out _);
        }

        public void InvalidateAll() => _cache.Clear();

        public int Count => _cache.Count;
    }
}
