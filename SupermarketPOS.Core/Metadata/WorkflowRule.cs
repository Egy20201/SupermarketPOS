using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Metadata
{
    [Table("WorkflowRules")]
    public class WorkflowRule
    {
        public int Id { get; set; }

        public int WorkflowId { get; set; }

        [MaxLength(1000)]
        public string ConditionExpression { get; set; }

        public int Priority { get; set; }

        public WorkflowDefinition Workflow { get; set; }
        public ICollection<WorkflowAction> Actions { get; set; } = new List<WorkflowAction>();
    }
}
