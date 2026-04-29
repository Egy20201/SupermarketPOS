using System.Collections.Generic;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Describes a business entity (e.g., Product, Customer, SaleInvoice).
    /// Maps to the [Entities] table.
    /// </summary>
    public class EntityDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string TableName { get; set; }
        public int ModuleId { get; set; }
        public bool IsActive { get; set; } = true;

        // Phase 2.5: Performance guard — max rows per query for this entity
        public int? MaxRows { get; set; }

        public ModuleDefinition Module { get; set; }
        public ICollection<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();
        public ICollection<RelationDefinition> OutgoingRelations { get; set; } = new List<RelationDefinition>();
        public ICollection<RelationDefinition> IncomingRelations { get; set; } = new List<RelationDefinition>();
        public ICollection<FormLayoutDefinition> FormLayouts { get; set; } = new List<FormLayoutDefinition>();
        public ICollection<GridLayoutDefinition> GridLayouts { get; set; } = new List<GridLayoutDefinition>();
        public ICollection<ActionDefinition> Actions { get; set; } = new List<ActionDefinition>();
        public ICollection<FieldPermission> FieldPermissions { get; set; } = new List<FieldPermission>();
    }
}
