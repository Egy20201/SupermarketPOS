using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Metadata
{
    [Table("Workflows")]
    public class WorkflowDefinition
    {
        public int Id { get; set; }

        public int EntityId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }

        [Required, MaxLength(50)]
        public string Trigger { get; set; }

        public bool IsActive { get; set; } = true;

        public EntityDefinition Entity { get; set; }
        public ICollection<WorkflowRule> Rules { get; set; } = new List<WorkflowRule>();
    }
}
