using SupermarketPOS.Core.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Applies default values from metadata field definitions before insert.
    /// Supports metadata defaults (DefaultValue column) and system defaults ({DateNow}, {UserId}).
    /// </summary>
    internal static class DefaultValueResolver
    {
        /// <summary>
        /// Apply default values from metadata for any field not already supplied in data.
        /// Modifies the data dictionary in-place.
        /// </summary>
        /// <param name="fields">Metadata field definitions for the entity.</param>
        /// <param name="data">The data dictionary being prepared for insert.</param>
        /// <param name="systemContext">Optional system context (e.g., current user Id).</param>
        public static void ApplyDefaults(
            IReadOnlyList<FieldDefinition> fields,
            Dictionary<string, object> data,
            SystemContext systemContext = null)
        {
            foreach (var field in fields)
            {
                // Skip if caller already provided a value
                bool alreadyProvided = data.Any(d =>
                    d.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase)
                    && d.Value != null);

                if (alreadyProvided) continue;

                // Skip if no default is configured
                if (string.IsNullOrWhiteSpace(field.DefaultValue)) continue;

                var resolved = ResolveDefault(field.DefaultValue, field.DataType, systemContext);
                if (resolved != null)
                    data[field.Name] = resolved;
            }
        }

        /// <summary>
        /// Resolves a default value string to a CLR object.
        /// Supports:
        ///   - Literal values: "0", "true", "Pending"
        ///   - System tokens: {DateNow}, {UserId}
        /// </summary>
        private static object ResolveDefault(string defaultValue, string dataType, SystemContext ctx)
        {
            // System tokens
            if (defaultValue.Equals("{DateNow}", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now;

            if (defaultValue.Equals("{DateUtcNow}", StringComparison.OrdinalIgnoreCase))
                return DateTime.UtcNow;

            if (defaultValue.Equals("{UserId}", StringComparison.OrdinalIgnoreCase))
                return ctx?.UserId;

            if (defaultValue.Equals("{UserName}", StringComparison.OrdinalIgnoreCase))
                return ctx?.UserName;

            // Literal conversion based on DataType
            return ConvertLiteral(defaultValue, dataType);
        }

        private static object ConvertLiteral(string value, string dataType)
        {
            switch (dataType?.ToLowerInvariant())
            {
                case "number":
                case "decimal":
                    if (decimal.TryParse(value, out var dec)) return dec;
                    return null;

                case "int":
                case "lookup":
                    if (int.TryParse(value, out var i)) return i;
                    return null;

                case "bool":
                case "boolean":
                    if (bool.TryParse(value, out var b)) return b;
                    if (value == "1") return true;
                    if (value == "0") return false;
                    return null;

                case "date":
                case "datetime":
                    if (DateTime.TryParse(value, out var dt)) return dt;
                    return null;

                case "string":
                default:
                    return value;
            }
        }
    }

    /// <summary>
    /// System-level context passed to default value resolution.
    /// </summary>
    public class SystemContext
    {
        public int? UserId { get; set; }
        public string UserName { get; set; }
    }
}
