using SupermarketPOS.Core.Metadata;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Validates entity data against metadata field definitions.
    /// Rules enforced: Required, MaxLength, DataType (string, number, date, bool, lookup).
    /// All rules come from the database — zero hardcoded schemas.
    /// </summary>
    public sealed class MetadataValidationService : IMetadataValidationService
    {
        private readonly MetadataRegistryService _registry;

        public MetadataValidationService(MetadataRegistryService registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <inheritdoc />
        public List<string> Validate(string entityName, Dictionary<string, object> data, bool isUpdate = false)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(entityName))
            {
                errors.Add("Entity name is required.");
                return errors;
            }

            if (data == null || data.Count == 0)
            {
                errors.Add("No data provided for validation.");
                return errors;
            }

            var entity = _registry.GetEntity(entityName);
            if (entity == null)
            {
                errors.Add($"Entity '{entityName}' not found in metadata registry.");
                return errors;
            }

            var fields = (IReadOnlyList<FieldDefinition>)entity.Fields;

            // 1. Validate that all provided keys exist in metadata
            foreach (var key in data.Keys)
            {
                if (key.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                if (!fields.Any(f => f.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    errors.Add($"Field '{key}' does not exist on entity '{entity.Name}'.");
            }

            // 2. For each metadata field, validate rules
            foreach (var field in fields)
            {
                var entry = data.FirstOrDefault(d =>
                    d.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                bool provided = !string.IsNullOrEmpty(entry.Key);
                var value = provided ? entry.Value : null;

                // ── Required check ──
                if (field.IsRequired)
                {
                    if (!isUpdate && (!provided || IsNullOrEmpty(value)))
                    {
                        errors.Add($"'{field.DisplayName ?? field.Name}' is required.");
                        continue;
                    }
                    if (isUpdate && provided && IsNullOrEmpty(value))
                    {
                        errors.Add($"'{field.DisplayName ?? field.Name}' cannot be empty.");
                        continue;
                    }
                }

                if (!provided || value == null) continue;

                var strValue = value.ToString();

                // ── MaxLength check ──
                if (field.MaxLength.HasValue && field.MaxLength.Value > 0)
                {
                    if (strValue.Length > field.MaxLength.Value)
                        errors.Add($"'{field.DisplayName ?? field.Name}' exceeds maximum length of {field.MaxLength.Value} characters.");
                }

                // ── DataType check ──
                ValidateDataType(field, value, errors);
            }

            return errors;
        }

        /// <summary>
        /// Validates that a value matches the expected DataType from metadata.
        /// Supported types: string, number, date, bool, lookup.
        /// </summary>
        private static void ValidateDataType(FieldDefinition field, object value, List<string> errors)
        {
            if (value == null) return;
            var strValue = value.ToString();
            if (string.IsNullOrEmpty(strValue)) return;

            switch (field.DataType?.ToLowerInvariant())
            {
                case "string":
                    // Any value is valid as string
                    break;

                case "number":
                    if (!decimal.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        errors.Add($"'{field.DisplayName ?? field.Name}' must be a valid number.");
                    break;

                case "date":
                    if (!(value is DateTime) &&
                        !DateTime.TryParse(strValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        errors.Add($"'{field.DisplayName ?? field.Name}' must be a valid date.");
                    break;

                case "bool":
                    if (!(value is bool) &&
                        !bool.TryParse(strValue, out _) &&
                        strValue != "0" && strValue != "1")
                        errors.Add($"'{field.DisplayName ?? field.Name}' must be a valid boolean (true/false).");
                    break;

                case "lookup":
                    // Lookup values must be a valid integer (foreign key Id)
                    if (!int.TryParse(strValue, out var lookupId) || lookupId <= 0)
                        errors.Add($"'{field.DisplayName ?? field.Name}' must be a valid reference Id (positive integer).");
                    break;
            }
        }

        private static bool IsNullOrEmpty(object value)
        {
            if (value == null) return true;
            if (value is string s) return string.IsNullOrWhiteSpace(s);
            return false;
        }
    }
}
