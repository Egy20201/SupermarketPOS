using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Context passed to every workflow rule and conditional action.
    /// Carries the subject entity, the set of changed fields (if any),
    /// the entity-type identifier and a per-execution idempotency key.
    /// </summary>
    public sealed class WorkflowContext
    {
        /// <summary>Entity-type identifier (e.g. "SalesOrder", "Quotation").</summary>
        public string EntityType { get; }

        /// <summary>Stable identifier of the subject entity (or null for new entities).</summary>
        public string EntityId { get; }

        /// <summary>The current entity instance under evaluation.</summary>
        public object Entity { get; }

        /// <summary>Optional snapshot of the entity prior to the change.</summary>
        public object PreviousEntity { get; }

        /// <summary>Set of fields that changed during this update.</summary>
        public FieldChangeSet Changes { get; }

        /// <summary>
        /// Idempotency key for this execution. The same key + same rule must
        /// only execute exactly once. When null, idempotency checks are skipped.
        /// </summary>
        public string IdempotencyKey { get; }

        /// <summary>Free-form bag for extra inputs (feature flags, business type, etc.).</summary>
        public IDictionary<string, object> Items { get; }

        public WorkflowContext(
            string entityType,
            string entityId,
            object entity,
            object previousEntity = null,
            FieldChangeSet changes = null,
            string idempotencyKey = null,
            IDictionary<string, object> items = null)
        {
            if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("entityType required", nameof(entityType));
            EntityType = entityType;
            EntityId = entityId;
            Entity = entity;
            PreviousEntity = previousEntity;
            Changes = changes ?? FieldChangeSet.Empty;
            IdempotencyKey = idempotencyKey;
            Items = items ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
