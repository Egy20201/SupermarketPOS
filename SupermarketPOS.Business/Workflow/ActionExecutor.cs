using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Executes a list of <see cref="IWorkflowAction"/> against a context.
    /// Skips actions whose <see cref="IWorkflowAction.Condition"/> isn't
    /// satisfied. Honors <see cref="WorkflowContext.IdempotencyKey"/> so
    /// retries don't double-execute side effects.
    /// </summary>
    public sealed class ActionExecutor
    {
        private readonly IIdempotencyStore _idempotencyStore;

        public ActionExecutor(IIdempotencyStore idempotencyStore = null)
        {
            _idempotencyStore = idempotencyStore ?? new InMemoryIdempotencyStore();
        }

        public IIdempotencyStore IdempotencyStore => _idempotencyStore;

        public void Run(IEnumerable<IWorkflowAction> actions, WorkflowContext context)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (context == null) throw new ArgumentNullException(nameof(context));

            foreach (var action in actions)
            {
                if (action == null) continue;

                var condition = action.Condition ?? Conditions.Always;
                if (!condition.IsSatisfied(context)) continue;

                if (!string.IsNullOrWhiteSpace(context.IdempotencyKey))
                {
                    var scope = BuildScope(action, context);
                    if (!_idempotencyStore.TryMarkExecuted(scope, context.IdempotencyKey))
                    {
                        // Already executed for this idempotency key.
                        continue;
                    }
                }

                try
                {
                    action.Execute(context);
                }
                catch
                {
                    // Roll back the idempotency mark so retry can re-attempt.
                    if (!string.IsNullOrWhiteSpace(context.IdempotencyKey))
                    {
                        _idempotencyStore.Forget(BuildScope(action, context), context.IdempotencyKey);
                    }
                    throw;
                }
            }
        }

        private static string BuildScope(IWorkflowAction action, WorkflowContext context)
        {
            return string.Concat("action/", context.EntityType, "/", context.EntityId ?? "(new)", "/", action.Name);
        }
    }
}
