using SupermarketPOS.Core.Metadata;
using System;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3: Maps metadata DataType to WPF control types.
    /// The single source of truth for field → control mapping.
    /// No reflection. No hardcoding per entity.
    /// </summary>
    public enum DynamicControlType
    {
        TextBox,
        NumericBox,
        DecimalBox,
        CheckBox,
        DatePicker,
        ComboBox,
        MultiLineText
    }

    public static class FieldTypeMapper
    {
        /// <summary>
        /// Resolves the WPF control type for a given field definition.
        /// Priority: LookupEntity → DataType mapping.
        /// </summary>
        public static DynamicControlType Resolve(FieldDefinition field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));

            // Lookup fields always render as ComboBox
            if (!string.IsNullOrEmpty(field.LookupEntity))
                return DynamicControlType.ComboBox;

            switch (field.DataType?.ToLowerInvariant())
            {
                case "int":
                    return DynamicControlType.NumericBox;

                case "decimal":
                    return DynamicControlType.DecimalBox;

                case "bool":
                case "boolean":
                    return DynamicControlType.CheckBox;

                case "date":
                case "datetime":
                    return DynamicControlType.DatePicker;

                case "text":
                case "multiline":
                    return DynamicControlType.MultiLineText;

                case "string":
                default:
                    // Large MaxLength → multiline
                    if (field.MaxLength.HasValue && field.MaxLength.Value > 500)
                        return DynamicControlType.MultiLineText;
                    return DynamicControlType.TextBox;
            }
        }

        /// <summary>
        /// Returns the default column width for grid display based on data type.
        /// </summary>
        public static double GetDefaultColumnWidth(FieldDefinition field)
        {
            switch (Resolve(field))
            {
                case DynamicControlType.CheckBox: return 80;
                case DynamicControlType.DatePicker: return 130;
                case DynamicControlType.NumericBox:
                case DynamicControlType.DecimalBox: return 100;
                case DynamicControlType.ComboBox: return 150;
                default: return 160;
            }
        }
    }
}
