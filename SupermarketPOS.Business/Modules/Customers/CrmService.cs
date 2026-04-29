using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class CrmService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly AuditService _auditService;

        public CrmService(Func<AppDbContext> dbFactory, AuditService auditService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        // ── Leads ──

        public Lead CreateLead(Lead lead, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    lead.Code = GenerateCode(db, "LEAD-", "dbo.LeadSeq");
                    lead.CreatedAt = DateTime.UtcNow;
                    lead.Status = LeadStatus.New;

                    db.Leads.Add(lead);
                    _auditService.Log("CREATE_LEAD", "Lead", lead.Id, userId);
                    db.SaveChanges();
                    tx.Commit();
                    return lead;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void UpdateLeadStatus(int leadId, LeadStatus newStatus, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var lead = db.Leads.Find(leadId);
                    if (lead == null) throw new InvalidOperationException("Lead not found");

                    // Validate transition
                    if (lead.Status == LeadStatus.Won || lead.Status == LeadStatus.Lost)
                        throw new InvalidOperationException($"Cannot change status. Lead is already {lead.Status}");

                    lead.Status = newStatus;
                    _auditService.Log("UPDATE_LEAD_STATUS", "Lead", leadId, userId);
                    db.SaveChanges();
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public Customer ConvertToCustomer(int leadId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var lead = db.Leads.Find(leadId);
                    if (lead == null) throw new InvalidOperationException("Lead not found");

                    // Idempotency: already converted?
                    if (lead.ConvertedToCustomerId.HasValue)
                        return db.Customers.Find(lead.ConvertedToCustomerId.Value);

                    // Create customer FIRST to get Id
                    var customer = new Customer
                    {
                        Name = lead.FullName,
                        Phone = lead.Phone,
                        Email = lead.Email,
                        Address = lead.Address,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Customers.Add(customer);
                    db.SaveChanges();  // Generate customer.Id

                    // NOW assign back to lead
                    lead.Status = LeadStatus.Won;
                    lead.ConvertedToCustomerId = customer.Id;

                    _auditService.Log("CONVERT_LEAD", "Customer", customer.Id, userId);
                    db.SaveChanges();
                    tx.Commit();
                    return customer;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public List<Lead> GetLeads(int? branchId = null, LeadStatus? status = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.Leads.AsQueryable();
                if (branchId.HasValue) query = query.Where(l => l.BranchId == branchId);
                if (status.HasValue) query = query.Where(l => l.Status == status.Value);
                return query.OrderByDescending(l => l.CreatedAt).ToList();
            }
        }

        // ── Opportunities ──

        public Opportunity CreateOpportunity(Opportunity opp, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    opp.Code = GenerateCode(db, "OPP-", "dbo.OpportunitySeq");
                    opp.CreatedAt = DateTime.UtcNow;
                    opp.Stage = OpportunityStage.Qualification;
                    if (opp.Probability <= 0) opp.Probability = 10;

                    db.Opportunities.Add(opp);
                    _auditService.Log("CREATE_OPPORTUNITY", "Opportunity", opp.Id, userId);
                    db.SaveChanges();
                    tx.Commit();
                    return opp;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void UpdateOpportunityStage(int oppId, OpportunityStage newStage, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var opp = db.Opportunities.Find(oppId);
                    if (opp == null) throw new InvalidOperationException("Opportunity not found");

                    if (opp.Stage == OpportunityStage.ClosedWon || opp.Stage == OpportunityStage.ClosedLost)
                        throw new InvalidOperationException($"Cannot change stage. Opportunity is already {opp.Stage}");

                    opp.Stage = newStage;
                    if (newStage == OpportunityStage.ClosedWon) opp.Probability = 100;
                    if (newStage == OpportunityStage.ClosedLost) opp.Probability = 0;

                    _auditService.Log("UPDATE_OPP_STAGE", "Opportunity", oppId, userId);
                    db.SaveChanges();
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public List<Opportunity> GetPipeline(int? branchId = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.Opportunities.AsQueryable();
                if (branchId.HasValue) query = query.Where(o => o.BranchId == branchId);
                return query
                    .Where(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost)
                    .OrderByDescending(o => o.Amount)
                    .ToList();
            }
        }

        // ── Activities ──

        public Activity LogActivity(Activity activity, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // Validate: must reference at least one entity
                    if (!activity.LeadId.HasValue && !activity.OpportunityId.HasValue && !activity.CustomerId.HasValue)
                        throw new InvalidOperationException("Activity must be linked to a Lead, Opportunity, or Customer");

                    activity.ActivityDate = DateTime.UtcNow;
                    activity.CreatedByUserId = userId;

                    db.Activities.Add(activity);
                    _auditService.Log("LOG_ACTIVITY", "Activity", activity.Id, userId);
                    db.SaveChanges();
                    tx.Commit();
                    return activity;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public List<Activity> GetActivities(int? leadId = null, int? oppId = null, int? customerId = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.Activities.AsQueryable();
                if (leadId.HasValue) query = query.Where(a => a.LeadId == leadId);
                if (oppId.HasValue) query = query.Where(a => a.OpportunityId == oppId);
                if (customerId.HasValue) query = query.Where(a => a.CustomerId == customerId);
                return query.OrderByDescending(a => a.ActivityDate).Take(100).ToList();
            }
        }

        // ── FollowUps ──

        public FollowUp ScheduleFollowUp(FollowUp followUp, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // Validate: reminder must be in the future
                    if (followUp.ReminderDate <= DateTime.UtcNow)
                        throw new InvalidOperationException("Reminder date must be in the future");

                    followUp.CreatedByUserId = userId;
                    followUp.IsCompleted = false;

                    db.FollowUps.Add(followUp);
                    _auditService.Log("SCHEDULE_FOLLOWUP", "FollowUp", followUp.Id, userId);
                    db.SaveChanges();
                    tx.Commit();
                    return followUp;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public List<FollowUp> GetPendingFollowUps(int userId)
        {
            using (var db = _dbFactory())
            {
                return db.FollowUps
                    .Where(f => f.CreatedByUserId == userId && !f.IsCompleted && f.ReminderDate <= DateTime.UtcNow)
                    .OrderBy(f => f.ReminderDate)
                    .ToList();
            }
        }

        public void CompleteFollowUp(int followUpId, int userId)
        {
            using (var db = _dbFactory())
            {
                var followUp = db.FollowUps.Find(followUpId);
                if (followUp == null) throw new InvalidOperationException("FollowUp not found");

                followUp.IsCompleted = true;
                db.SaveChanges();
                _auditService.Log("COMPLETE_FOLLOWUP", "FollowUp", followUpId, userId);
            }
        }

        // ── Helpers ──

        private string GenerateCode(AppDbContext db, string prefix, string sequenceName)
        {
            var seq = db.Database.SqlQuery<long>($"SELECT NEXT VALUE FOR {sequenceName}").First();
            return $"{prefix}{seq:D6}";
        }
    }
}
