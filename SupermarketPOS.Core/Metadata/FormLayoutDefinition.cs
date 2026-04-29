namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Stores the form layout JSON for an entity.
    /// Maps to the [FormLayouts] table.
    /// </summary>
    public class FormLayoutDefinition
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string LayoutJson { get; set; }

        public EntityDefinition Entity { get; set; }
    }
}
