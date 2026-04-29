using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class EmployeeService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly AuditService _auditService;

        public EmployeeService(Func<AppDbContext> dbFactory, AuditService auditService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public Employee Create(Employee employee, int userId)
        {
            using (var db = _dbFactory())
            {
                if (string.IsNullOrWhiteSpace(employee.Code))
                {
                    var nextSeq = db.Database.SqlQuery<long>("SELECT NEXT VALUE FOR dbo.EmployeeSeq").First();
                    employee.Code = $"EMP{nextSeq:D6}";
                }
                employee.HireDate = DateTime.UtcNow;
                db.Employees.Add(employee);
                db.SaveChanges();
                _auditService.Log("CREATE_EMPLOYEE", "Employee", employee.Id, userId);
                return employee;
            }
        }

        public void Update(Employee employee, int userId)
        {
            using (var db = _dbFactory())
            {
                var existing = db.Employees.Find(employee.Id);
                if (existing == null) throw new InvalidOperationException("Employee not found");
                existing.FullName = employee.FullName;
                existing.Phone = employee.Phone;
                existing.Email = employee.Email;
                existing.Position = employee.Position;
                existing.BasicSalary = employee.BasicSalary;
                existing.Allowances = employee.Allowances;
                existing.Deductions = employee.Deductions;
                existing.DepartmentId = employee.DepartmentId;
                existing.BranchId = employee.BranchId;
                db.SaveChanges();
                _auditService.Log("UPDATE_EMPLOYEE", "Employee", employee.Id, userId);
            }
        }

        public List<Employee> GetActive(int? branchId = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.Employees.Where(e => e.IsActive).AsQueryable();
                if (branchId.HasValue) query = query.Where(e => e.BranchId == branchId);
                return query.OrderBy(e => e.FullName).ToList();
            }
        }

        public List<Department> GetDepartments()
        {
            using (var db = _dbFactory())
            {
                return db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToList();
            }
        }
    }
}