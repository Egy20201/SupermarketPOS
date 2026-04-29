namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Stores the grid/list column layout JSON for an entity.
    /// Maps to the [GridLayouts] table.
    /// </summary>
    public class GridLayoutDefinition
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string ColumnsJson { get; set; }

        public EntityDefinition Entity { get; set; }
    }
}
