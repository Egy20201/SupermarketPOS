using System;

namespace SupermarketPOS.Core.Entities
{
    public class FollowUp
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public DateTime ReminderDate { get; set; }
        public bool IsCompleted { get; set; }
        public int? LeadId { get; set; }
        public virtual Lead Lead { get; set; }
        public int? OpportunityId { get; set; }
        public virtual Opportunity Opportunity { get; set; }
        public int? CustomerId { get; set; }
        public virtual Customer Customer { get; set; }
        public int CreatedByUserId { get; set; }
        public virtual User CreatedBy { get; set; }
        public int? BranchId { get; set; }
    }
}
