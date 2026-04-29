namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Describes a single field on an entity (e.g., Product.Name, Product.Price).
    /// Maps to the [Fields] table.
    /// </summary>
    public class FieldDefinition
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string Name { get; set; }
        public string DataType { get; set; }
        public bool IsRequired { get; set; }
        public bool IsEditable { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public string DisplayName { get; set; }
        public int? OrderIndex { get; set; }

        // Phase 3: Validation & lookup support
        public int? MaxLength { get; set; }
        public string DefaultValue { get; set; }
        public int? LookupEntityId { get; set; }
        public string LookupEntity { get; set; }
        public string LookupDisplayField { get; set; }

        // Phase 2.5: Computed fields
        public bool IsComputed { get; set; }
        public string ComputedExpression { get; set; }

        public EntityDefinition Entity { get; set; }
        public EntityDefinition LookupEntityRef { get; set; }
    }
}
