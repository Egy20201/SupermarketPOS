namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Defines an action that can be performed on an entity (Create, Save, Delete, Approve).
    /// Maps to the [Actions] table.
    /// </summary>
    public class ActionDefinition
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public EntityDefinition Entity { get; set; }
    }
}
