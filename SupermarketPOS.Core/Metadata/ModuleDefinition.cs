using System.Collections.Generic;

namespace SupermarketPOS.Core.Metadata
{
    /// <summary>
    /// Represents a logical module in the ERP system (e.g., Sales, Inventory, HR).
    /// Maps to the [Modules] table.
    /// </summary>
    public class ModuleDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<EntityDefinition> Entities { get; set; } = new List<EntityDefinition>();
    }
}
