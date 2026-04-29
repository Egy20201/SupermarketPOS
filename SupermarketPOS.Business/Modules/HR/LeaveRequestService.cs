using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;

namespace SupermarketPOS.Business
{
    public class LeaveRequestService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly DocumentNumberingService _numbering;
        private readonly AuditService _auditService;

        public LeaveRequestService(Func<AppDbContext> dbFactory, DocumentNumberingService numbering, AuditService auditService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public LeaveRequest Create(int employeeId, DateTime from, DateTime to, string type, string reason, int? branchId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    if (from > to)
                        throw new InvalidOperationException("From date must be before To date");

                    var totalDays = (to - from).Days + 1;

                    var request = new LeaveRequest
                    {
                        Number = _numbering.GenerateNumber("LEV-", "dbo.LeaveSeq"),
                        Date = DateTime.UtcNow,
                        EmployeeId = employeeId,
                        LeaveFrom = from,
                        LeaveTo = to,
                        TotalDays = totalDays,
                        LeaveType = type ?? "Annual",
                        Reason = reason,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft
                    };

                    request.Validate();
                    db.LeaveRequests.Add(request);
                    _auditService.Log("CREATE_LEAVE", "LeaveRequest", request.Id, userId);
                    db.SaveChanges();
                    tx.Commit();
                    return request;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public void Approve(int leaveId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var leave = db.LeaveRequests.Find(leaveId);
                    if (leave == null)
                        throw new InvalidOperationException("Leave not found");

                    if (leave.Status != DocumentStatus.Draft)
                        throw new InvalidOperationException($"Cannot approve. Status is {leave.Status}. Must be Draft.");

                    WorkflowEngine.Transition(leave, DocumentStatus.Approved);
                    _auditService.Log("APPROVE_LEAVE", "LeaveRequest", leaveId, userId);
                    db.SaveChanges();
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }
    }
}
