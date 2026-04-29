using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Lead
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string FullName { get; set; }
        public string CompanyName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Source { get; set; }
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public string Notes { get; set; }
        public decimal PotentialValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedToUserId { get; set; }
        public virtual User AssignedTo { get; set; }
        public int? ConvertedToCustomerId { get; set; }
        public virtual Customer ConvertedToCustomer { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public virtual ICollection<Activity> Activities { get; set; }
        public virtual ICollection<Opportunity> Opportunities { get; set; }
    }
}
