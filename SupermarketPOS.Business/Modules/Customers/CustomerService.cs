using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SupermarketPOS.Business
{
    public interface ICustomerService
    {
        Task<List<CustomerListItemDto>> GetCustomersAsync(string searchText = null);
        Task<CustomerListItemDto> GetCustomerByIdAsync(int id);
        Task<CustomerSaveResult> SaveAsync(CustomerSaveRequest request);
        Task<bool> DeleteAsync(int customerId);
        Task<int> GetTotalCountAsync(string searchText = null);
    }

    public class CustomerService : ICustomerService
    {
        private static readonly Regex PhoneRegex = new Regex(@"^[\d\+\-\s\(\)]+$", RegexOptions.Compiled);
        private const string RequiredNameMessage = "أدخل الاسم";
        private const string InvalidPhoneMessage = "رقم الهاتف غير صالح";
        private const string DuplicatePhoneMessage = "رقم الهاتف موجود";
        private const string CustomerNotFoundMessage = "العميل غير موجود";

        public List<CustomerListItemDto> GetCustomers(string searchText = null)
        {
            using (var db = new AppDbContext())
            {
                var query = db.Customers.AsQueryable();
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var search = searchText.Trim().ToLower();
                    query = query.Where(c => c.Name.ToLower().Contains(search) || (c.Phone != null && c.Phone.Contains(search)));
                }

                return query
                    .OrderBy(c => c.Name)
                    .Select(c => new CustomerListItemDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Phone = c.Phone,
                        Email = c.Email,
                        Address = c.Address,
                        Notes = c.Notes,
                        Balance = c.Balance
                    })
                    .ToList();
            }
        }

        public CustomerSaveResult Save(CustomerSaveRequest request)
        {
            using (var db = new AppDbContext())
            {
                var validation = Validate(request, db);
                if (!validation.IsValid)
                {
                    return CustomerSaveResult.Fail(validation.ErrorMessage);
                }

                var command = Process(request);
                return Commit(command, db);
            }
        }

        public bool Delete(int customerId)
        {
            using (var db = new AppDbContext())
            {
                var customer = db.Customers.Find(customerId);
                if (customer == null)
                {
                    return false;
                }

                db.Customers.Remove(customer);
                db.SaveChanges();
                return true;
            }
        }

        private CustomerSaveValidationResult Validate(CustomerSaveRequest request, AppDbContext db)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return CustomerSaveValidationResult.Fail(RequiredNameMessage);
            }

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                var phone = request.Phone.Trim();
                if (!IsValidPhone(phone))
                {
                    return CustomerSaveValidationResult.Fail(InvalidPhoneMessage);
                }

                var duplicatePhoneQuery = db.Customers.Where(c => c.Phone == phone);
                if (request.CustomerId.HasValue)
                {
                    duplicatePhoneQuery = duplicatePhoneQuery.Where(c => c.Id != request.CustomerId.Value);
                }

                if (duplicatePhoneQuery.Any())
                {
                    return CustomerSaveValidationResult.Fail(DuplicatePhoneMessage);
                }
            }

            if (request.CustomerId.HasValue && !db.Customers.Any(c => c.Id == request.CustomerId.Value))
            {
                return CustomerSaveValidationResult.Fail(CustomerNotFoundMessage);
            }

            return CustomerSaveValidationResult.Valid();
        }

        private CustomerSaveCommand Process(CustomerSaveRequest request)
        {
            return new CustomerSaveCommand
            {
                CustomerId = request.CustomerId,
                Name = request.Name,
                Phone = request.Phone,
                Email = request.Email,
                Address = request.Address,
                Notes = request.Notes
            };
        }

        private CustomerSaveResult Commit(CustomerSaveCommand command, AppDbContext db)
        {
            if (!command.CustomerId.HasValue)
            {
                var customer = new Customer
                {
                    Name = command.Name,
                    Phone = command.Phone,
                    Email = command.Email,
                    Address = command.Address,
                    Notes = command.Notes,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                db.Customers.Add(customer);
                db.SaveChanges();
                return CustomerSaveResult.Ok(customer.Id);
            }

            var existing = db.Customers.Find(command.CustomerId.Value);
            existing.Name = command.Name;
            existing.Phone = command.Phone;
            existing.Email = command.Email;
            existing.Address = command.Address;
            existing.Notes = command.Notes;
            db.SaveChanges();
            return CustomerSaveResult.Ok(existing.Id);
        }

        public Task<List<CustomerListItemDto>> GetCustomersAsync(string searchText = null)
        {
            return Task.FromResult(GetCustomers(searchText));
        }

        public Task<CustomerListItemDto> GetCustomerByIdAsync(int id)
        {
            using (var db = new AppDbContext())
            {
                var customer = db.Customers.Find(id);
                if (customer == null) return Task.FromResult<CustomerListItemDto>(null);

                return Task.FromResult(new CustomerListItemDto
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Phone = customer.Phone,
                    Email = customer.Email,
                    Address = customer.Address,
                    Notes = customer.Notes,
                    Balance = customer.Balance
                });
            }
        }

        public Task<CustomerSaveResult> SaveAsync(CustomerSaveRequest request)
        {
            return Task.FromResult(Save(request));
        }

        public Task<bool> DeleteAsync(int customerId)
        {
            return Task.FromResult(Delete(customerId));
        }

        public Task<int> GetTotalCountAsync(string searchText = null)
        {
            return Task.FromResult(GetCustomers(searchText).Count);
        }

        private static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return true;
            }

            if (phone == "-")
            {
                return true;
            }

            return PhoneRegex.IsMatch(phone);
        }
    }

    public class CustomerListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
        public decimal Balance { get; set; }
    }

    public class CustomerSaveRequest
    {
        public int? CustomerId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
    }

    public class CustomerSaveResult
    {
        public bool Success { get; set; }
        public int? CustomerId { get; set; }
        public string ErrorMessage { get; set; }

        public static CustomerSaveResult Ok(int customerId)
        {
            return new CustomerSaveResult { Success = true, CustomerId = customerId };
        }

        public static CustomerSaveResult Fail(string error)
        {
            return new CustomerSaveResult { Success = false, ErrorMessage = error };
        }
    }

    internal class CustomerSaveValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }

        public static CustomerSaveValidationResult Valid()
        {
            return new CustomerSaveValidationResult { IsValid = true };
        }

        public static CustomerSaveValidationResult Fail(string errorMessage)
        {
            return new CustomerSaveValidationResult { IsValid = false, ErrorMessage = errorMessage };
        }
    }

    internal class CustomerSaveCommand
    {
        public int? CustomerId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
    }
}
