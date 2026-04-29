using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Captures the set of fields that changed during an update,
    /// along with their old and new values. Field name comparison is
    /// case-insensitive.
    /// </summary>
    public sealed class FieldChangeSet
    {
        private readonly Dictionary<string, FieldChange> _changes;

        public FieldChangeSet()
        {
            _changes = new Dictionary<string, FieldChange>(StringComparer.OrdinalIgnoreCase);
        }

        public FieldChangeSet(IEnumerable<FieldChange> changes) : this()
        {
            if (changes == null) return;
            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrWhiteSpace(change.FieldName)) continue;
                _changes[change.FieldName] = change;
            }
        }

        public IReadOnlyCollection<string> ChangedFields => _changes.Keys.ToList();

        public bool HasChanged(string fieldName)
            => !string.IsNullOrWhiteSpace(fieldName) && _changes.ContainsKey(fieldName);

        public bool AnyChanged(IEnumerable<string> fieldNames)
        {
            if (fieldNames == null) return false;
            foreach (var name in fieldNames)
            {
                if (HasChanged(name)) return true;
            }
            return false;
        }

        public FieldChange Get(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return null;
            FieldChange change;
            return _changes.TryGetValue(fieldName, out change) ? change : null;
        }

        public bool IsEmpty => _changes.Count == 0;

        public static FieldChangeSet Empty { get; } = new FieldChangeSet();
    }

    /// <summary>
    /// Represents a single field's transition from one value to another.
    /// </summary>
    public sealed class FieldChange
    {
        public string FieldName { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public FieldChange(string fieldName, object oldValue, object newValue)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("fieldName required", nameof(fieldName));
            FieldName = fieldName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
