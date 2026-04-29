using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Opportunity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public int? LeadId { get; set; }
        public virtual Lead Lead { get; set; }
        public int? CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
        public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
        public decimal Amount { get; set; }
        public int Probability { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedToUserId { get; set; }
        public virtual User AssignedTo { get; set; }
        public string Notes { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public virtual ICollection<Activity> Activities { get; set; }
    }
}
