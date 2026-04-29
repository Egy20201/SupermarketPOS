using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class AttendanceService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly SettingsService _settings;
        private readonly AuditService _auditService;

        public AttendanceService(Func<AppDbContext> dbFactory, SettingsService settings, AuditService auditService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public AttendanceRecord CheckIn(int employeeId, int? branchId, int userId)
        {
            using (var db = _dbFactory())
            {
                var today = DateTime.UtcNow.Date;
                var existing = db.AttendanceRecords
                    .FirstOrDefault(a => a.EmployeeId == employeeId && a.Date == today);

                if (existing != null)
                    throw new InvalidOperationException("Employee already checked in today");

                var shiftStart = _settings.Get<TimeSpan>("hr.shiftStart", new TimeSpan(8, 0, 0));
                var now = DateTime.UtcNow;
                var lateMinutes = 0m;

                if (now.TimeOfDay > shiftStart)
                    lateMinutes = (decimal)(now.TimeOfDay - shiftStart).TotalMinutes;

                var record = new AttendanceRecord
                {
                    EmployeeId = employeeId,
                    Date = today,
                    CheckIn = now,
                    LateMinutes = lateMinutes,
                    BranchId = branchId
                };

                db.AttendanceRecords.Add(record);
                db.SaveChanges();
                _auditService.Log("CHECKIN", "Attendance", record.Id, userId);
                return record;
            }
        }

        public AttendanceRecord CheckOut(int employeeId, int userId)
        {
            using (var db = _dbFactory())
            {
                var today = DateTime.UtcNow.Date;
                var record = db.AttendanceRecords
                    .FirstOrDefault(a => a.EmployeeId == employeeId && a.Date == today);

                if (record == null)
                    throw new InvalidOperationException("No check-in record found for today");

                if (record.CheckOut.HasValue)
                    throw new InvalidOperationException("Employee already checked out today");

                var shiftEnd = _settings.Get<TimeSpan>("hr.shiftEnd", new TimeSpan(16, 0, 0));
                var now = DateTime.UtcNow;

                record.CheckOut = now;

                if (now.TimeOfDay > shiftEnd)
                    record.OvertimeMinutes = (decimal)(now.TimeOfDay - shiftEnd).TotalMinutes;

                db.SaveChanges();
                _auditService.Log("CHECKOUT", "Attendance", record.Id, userId);
                return record;
            }
        }

        public List<AttendanceRecord> GetMonthlyAttendance(int employeeId, int year, int month)
        {
            using (var db = _dbFactory())
            {
                var from = new DateTime(year, month, 1);
                var to = from.AddMonths(1);
                return db.AttendanceRecords
                    .Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date < to)
                    .OrderBy(a => a.Date)
                    .ToList();
            }
        }

        public int MarkAbsentForMissing(DateTime date, int? branchId, int userId)
        {
            using (var db = _dbFactory())
            {
                var employees = db.Employees
                    .Where(e => e.IsActive)
                    .Where(e => branchId == null || e.BranchId == branchId)
                    .ToList();

                var presentEmployeeIds = db.AttendanceRecords
                    .Where(a => a.Date == date)
                    .Select(a => a.EmployeeId)
                    .ToList();

                var missingEmployees = employees
                    .Where(e => !presentEmployeeIds.Contains(e.Id))
                    .ToList();

                foreach (var emp in missingEmployees)
                {
                    db.AttendanceRecords.Add(new AttendanceRecord
                    {
                        EmployeeId = emp.Id,
                        Date = date,
                        Status = AttendanceStatus.Absent,
                        BranchId = branchId,
                        Notes = "غياب تلقائي"
                    });
                }

                if (missingEmployees.Any())
                {
                    db.SaveChanges();
                    _auditService.Log("MARK_ABSENT", "Attendance", null, userId);
                }

                return missingEmployees.Count;
            }
        }

        /// <summary>
        /// CheckIn using employee's assigned shift instead of global settings.
        /// </summary>
        public AttendanceRecord CheckInWithShift(int employeeId, int? branchId, int userId)
        {
            using (var db = _dbFactory())
            {
                var employee = db.Employees.Include("Shift").FirstOrDefault(e => e.Id == employeeId);
                if (employee == null || !employee.IsActive)
                    throw new InvalidOperationException("الموظف غير موجود أو غير نشط");

                var today = DateTime.UtcNow.Date;
                var existing = db.AttendanceRecords
                    .FirstOrDefault(a => a.EmployeeId == employeeId && a.Date == today);

                if (existing != null)
                    throw new InvalidOperationException("تم تسجيل الحضور بالفعل اليوم");

                var shiftStart = employee.Shift?.StartTime
                    ?? _settings.Get<TimeSpan>("work.start.time", new TimeSpan(9, 0, 0));
                var grace = employee.Shift?.GraceMinutes
                    ?? _settings.Get<decimal>("work.graceMinutes", 15);

                var now = DateTime.UtcNow;
                var lateMinutes = 0m;
                var allowedTime = shiftStart.Add(TimeSpan.FromMinutes((double)grace));

                if (now.TimeOfDay > allowedTime)
                    lateMinutes = (decimal)(now.TimeOfDay - shiftStart).TotalMinutes;

                var status = lateMinutes > 0 ? AttendanceStatus.Late : AttendanceStatus.Present;

                var record = new AttendanceRecord
                {
                    EmployeeId = employeeId,
                    Date = today,
                    CheckIn = now,
                    LateMinutes = lateMinutes,
                    Status = status,
                    BranchId = branchId
                };

                db.AttendanceRecords.Add(record);
                db.SaveChanges();
                _auditService.Log("CHECKIN", "Attendance", record.Id, userId);
                return record;
            }
        }

        /// <summary>
        /// Imports attendance from biometric device logs.
        /// Matches by EmployeeCode.
        /// </summary>
        public int ImportFromBiometric(DateTime date, int? branchId, int userId)
        {
            using (var db = _dbFactory())
            {
                // Idempotency: check if logs already processed for this date
                var alreadyProcessed = db.BiometricLogs.Any(b =>
                    b.Timestamp.Date == date.Date && b.IsProcessed);

                if (alreadyProcessed)
                {
                    _auditService.Log("IMPORT_SKIPPED", "Biometric", null, userId);
                    return 0;  // Already imported — nothing to do
                }

                var logs = db.BiometricLogs
                    .Where(b => !b.IsProcessed && b.Timestamp.Date == date.Date)
                    .OrderBy(b => b.Timestamp)
                    .ToList();

                if (!logs.Any()) return 0;

                var employeeDict = db.Employees
                    .Where(e => e.IsActive && e.Code != null)
                    .ToDictionary(e => e.Code, e => e);

                var existingAttendance = db.AttendanceRecords
                    .Where(a => a.Date == date.Date && (branchId == null || a.BranchId == branchId))
                    .ToDictionary(a => a.EmployeeId, a => a);

                var processed = 0;

                foreach (var log in logs)
                {
                    if (!employeeDict.TryGetValue(log.EmployeeCode, out var employee))
                        continue;

                    existingAttendance.TryGetValue(employee.Id, out var attendance);

                    if (log.Direction == "IN" && attendance == null)
                    {
                        db.AttendanceRecords.Add(new AttendanceRecord
                        {
                            EmployeeId = employee.Id,
                            Date = date.Date,
                            CheckIn = log.Timestamp,
                            Status = AttendanceStatus.Present,
                            BranchId = branchId
                        });
                        log.IsProcessed = true;
                        processed++;
                    }
                    else if (log.Direction == "OUT" && attendance != null && !attendance.CheckOut.HasValue)
                    {
                        attendance.CheckOut = log.Timestamp;
                        log.IsProcessed = true;
                        processed++;
                    }
                }

                db.SaveChanges();
                _auditService.Log("IMPORT_BIOMETRIC", "Attendance", null, userId);
                return processed;
            }
        }
    }
}