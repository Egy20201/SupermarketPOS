using System.Collections.Generic;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Validates entity data against metadata field definitions.
    /// Enforces Required, MaxLength, and DataType rules from the database.
    /// </summary>
    public interface IMetadataValidationService
    {
        /// <summary>
        /// Validates a set of field values against the metadata rules for an entity.
        /// Returns a list of validation errors. Empty list means all valid.
        /// </summary>
        /// <param name="entityName">The metadata entity name (e.g., "Product").</param>
        /// <param name="data">Field name/value pairs to validate.</param>
        /// <param name="isUpdate">If true, required-field checks only apply to provided fields.</param>
        /// <returns>List of human-readable validation error messages.</returns>
        List<string> Validate(string entityName, Dictionary<string, object> data, bool isUpdate = false);
    }
}
