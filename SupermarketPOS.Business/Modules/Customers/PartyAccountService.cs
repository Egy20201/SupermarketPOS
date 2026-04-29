using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Modules
{
    public class PartyAccountService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public PartyAccountService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public List<PartyBalanceDto> GetCustomerBalances()
        {
            using (var db = _dbFactory())
            {
                return db.Customers.OrderBy(c => c.Name)
                    .Select(c => new PartyBalanceDto { Id = c.Id, Name = c.Name, Phone = c.Phone, Balance = c.Balance })
                    .ToList();
            }
        }

        public List<PartyBalanceDto> GetSupplierBalances()
        {
            using (var db = _dbFactory())
            {
                return db.Suppliers.OrderBy(s => s.Name)
                    .Select(s => new PartyBalanceDto { Id = s.Id, Name = s.Name, Phone = s.Phone, Balance = s.Balance })
                    .ToList();
            }
        }

        public List<Customer> GetCustomersForStatement()
        {
            using (var db = _dbFactory())
            {
                return db.Customers.AsNoTracking().OrderBy(c => c.Name).ToList();
            }
        }

        public List<Supplier> GetSuppliersForStatement()
        {
            using (var db = _dbFactory())
            {
                return db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToList();
            }
        }

        public StatementReportDto GetCustomerStatement(int customerId, DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var customer = db.Customers.AsNoTracking().FirstOrDefault(c => c.Id == customerId);
                var previousInvoices = db.SaleInvoices.AsNoTracking().Where(i => i.CustomerId == customerId && i.Date < from).ToList();
                var previousPayments = db.CustomerPayments.AsNoTracking().Where(p => p.CustomerId == customerId && p.Date < from).ToList();
                var opening = previousInvoices.Sum(i => i.NetAmount) - previousPayments.Sum(p => p.Amount);

                var invoices = db.SaleInvoices.AsNoTracking().Where(i => i.CustomerId == customerId && i.Date >= from && i.Date < toExclusive).ToList();
                var payments = db.CustomerPayments.AsNoTracking().Where(p => p.CustomerId == customerId && p.Date >= from && p.Date < toExclusive).ToList();

                var result = new StatementReportDto
                {
                    PartyName = customer == null ? "-" : customer.Name,
                    Phone = customer == null ? null : customer.Phone,
                    CurrentBalance = customer == null ? 0 : customer.Balance,
                    OpeningBalance = opening
                };

                var balance = opening;
                if (opening != 0)
                    result.Rows.Add(new StatementRowDto { Date = from, Type = "رصيد افتتاحي", Reference = "-", Debit = opening > 0 ? opening : 0, Credit = opening < 0 ? -opening : 0, Balance = balance });

                foreach (var invoice in invoices)
                {
                    balance += invoice.NetAmount;
                    result.Rows.Add(new StatementRowDto { Date = invoice.Date, Type = "فاتورة", Reference = invoice.InvoiceNumber, Debit = invoice.NetAmount, Credit = 0, Balance = balance });
                }

                foreach (var payment in payments)
                {
                    balance -= payment.Amount;
                    result.Rows.Add(new StatementRowDto { Date = payment.Date, Type = "سداد", Reference = payment.Id.ToString(), Debit = 0, Credit = payment.Amount, Balance = balance });
                }

                result.Rows = result.Rows.OrderBy(r => r.Date).ToList();
                result.TotalDebit = result.Rows.Sum(r => r.Debit);
                result.TotalCredit = result.Rows.Sum(r => r.Credit);
                result.ClosingBalance = balance;
                return result;
            }
        }

        public StatementReportDto GetSupplierStatement(int supplierId, DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var supplier = db.Suppliers.AsNoTracking().FirstOrDefault(s => s.Id == supplierId);
                var previousInvoices = db.PurchaseInvoices.AsNoTracking().Where(i => i.SupplierId == supplierId && i.Date < from).ToList();
                var previousPayments = db.SupplierPayments.AsNoTracking().Where(p => p.SupplierId == supplierId && p.Date < from).ToList();
                var opening = previousInvoices.Sum(i => i.TotalAmount) - previousPayments.Sum(p => p.Amount);

                var invoices = db.PurchaseInvoices.AsNoTracking().Where(i => i.SupplierId == supplierId && i.Date >= from && i.Date < toExclusive).ToList();
                var payments = db.SupplierPayments.AsNoTracking().Where(p => p.SupplierId == supplierId && p.Date >= from && p.Date < toExclusive).ToList();

                var result = new StatementReportDto
                {
                    PartyName = supplier == null ? "-" : supplier.Name,
                    Phone = supplier == null ? null : supplier.Phone,
                    CurrentBalance = supplier == null ? 0 : supplier.Balance,
                    OpeningBalance = opening
                };

                var balance = opening;
                if (opening != 0)
                    result.Rows.Add(new StatementRowDto { Date = from, Type = "رصيد افتتاحي", Reference = "-", Debit = opening > 0 ? opening : 0, Credit = opening < 0 ? -opening : 0, Balance = balance });

                foreach (var invoice in invoices)
                {
                    balance += invoice.TotalAmount;
                    result.Rows.Add(new StatementRowDto { Date = invoice.Date, Type = "فاتورة شراء", Reference = invoice.InvoiceNumber, Debit = invoice.TotalAmount, Credit = 0, Balance = balance });
                }

                foreach (var payment in payments)
                {
                    balance -= payment.Amount;
                    result.Rows.Add(new StatementRowDto { Date = payment.Date, Type = "سداد", Reference = payment.Id.ToString(), Debit = 0, Credit = payment.Amount, Balance = balance });
                }

                result.Rows = result.Rows.OrderBy(r => r.Date).ToList();
                result.TotalDebit = result.Rows.Sum(r => r.Debit);
                result.TotalCredit = result.Rows.Sum(r => r.Credit);
                result.ClosingBalance = balance;
                return result;
            }
        }
    }

    public class PartyBalanceDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public decimal Balance { get; set; }
    }

    public class StatementReportDto
    {
        public string PartyName { get; set; }
        public string Phone { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<StatementRowDto> Rows { get; set; } = new List<StatementRowDto>();
    }

    public class StatementRowDto
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Reference { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
    }
}
