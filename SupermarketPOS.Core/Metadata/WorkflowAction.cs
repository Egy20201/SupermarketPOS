using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Metadata
{
    [Table("WorkflowActions")]
    public class WorkflowAction
    {
        public int Id { get; set; }

        public int RuleId { get; set; }

        [Required, MaxLength(50)]
        public string ActionType { get; set; }

        public string ParametersJson { get; set; }

        public WorkflowRule Rule { get; set; }
    }
}
