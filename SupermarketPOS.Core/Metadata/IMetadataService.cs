using System.Collections.Generic;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Contract for retrieving metadata definitions from the database-driven registry.
    /// The system describes itself through this service — no hardcoded schemas.
    /// </summary>
    public interface IMetadataService
    {
        /// <summary>
        /// Returns the entity definition by its unique name (case-insensitive).
        /// Includes DisplayName, TableName, IsActive.
        /// Returns null if not found.
        /// </summary>
        EntityDefinition GetEntity(string name);

        /// <summary>
        /// Returns all field definitions for an entity, ordered by OrderIndex.
        /// Includes DataType, IsRequired, MaxLength, DefaultValue, LookupEntityId, IsVisible.
        /// </summary>
        IReadOnlyList<FieldDefinition> GetFields(int entityId);

        /// <summary>
        /// Returns the form layout for an entity (JSON describing field arrangement on forms).
        /// Returns null if no layout is configured.
        /// </summary>
        FormLayoutDefinition GetFormLayout(int entityId);

        /// <summary>
        /// Returns the grid layout for an entity (JSON describing column arrangement on lists).
        /// Returns null if no layout is configured.
        /// </summary>
        GridLayoutDefinition GetGridLayout(int entityId);

        /// <summary>
        /// Returns all actions defined for an entity (Create, Save, Delete, Approve).
        /// </summary>
        IReadOnlyList<ActionDefinition> GetActions(int entityId);
    }
}
