namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Describes a relationship between two entities (e.g., Sale -> Customer = many2one).
    /// Maps to the [Relations] table.
    /// </summary>
    public class RelationDefinition
    {
        public int Id { get; set; }
        public int SourceEntityId { get; set; }
        public int TargetEntityId { get; set; }
        public string RelationType { get; set; }
        public string FieldName { get; set; }

        public EntityDefinition SourceEntity { get; set; }
        public EntityDefinition TargetEntity { get; set; }
    }
}
