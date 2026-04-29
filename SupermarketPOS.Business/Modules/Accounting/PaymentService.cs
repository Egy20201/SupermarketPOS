using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class PaymentService
    {
        private const string CashMethod = "نقدي";
        private static readonly string[] AllowedPaymentMethods = { "نقدي", "تحويل", "بطاقة", "شيك" };
        private readonly TransactionExecutor transactionExecutor;

        public PaymentService(TransactionExecutor transactionExecutor)
        {
            this.transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
        }

        public List<PaymentCustomerDto> GetActiveCustomers()
        {
            using (var db = new AppDbContext())
            {
                return db.Customers
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new PaymentCustomerDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Balance = c.Balance
                    })
                    .ToList();
            }
        }

        public List<PaymentSupplierDto> GetSuppliers()
        {
            using (var db = new AppDbContext())
            {
                return db.Suppliers
                    .OrderBy(s => s.Name)
                    .Select(s => new PaymentSupplierDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Balance = s.Balance
                    })
                    .ToList();
            }
        }

        public List<PaymentInvoiceDto> GetCustomerOutstandingInvoices(int customerId)
        {
            using (var db = new AppDbContext())
            {
                var invoices = db.SaleInvoices
                    .Where(i => i.CustomerId == customerId && i.PaidAmount < i.NetAmount)
                    .OrderBy(i => i.Date)
                    .Select(i => new PaymentInvoiceDto
                    {
                        Id = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        RemainingAmount = i.NetAmount - i.PaidAmount
                    })
                    .ToList();

                invoices.Insert(0, new PaymentInvoiceDto { Id = 0, InvoiceNumber = "سداد عام", RemainingAmount = 0m });
                return invoices;
            }
        }

        public List<PaymentInvoiceDto> GetSupplierOutstandingInvoices(int supplierId)
        {
            using (var db = new AppDbContext())
            {
                var invoices = db.PurchaseInvoices
                    .Where(i => i.SupplierId == supplierId && i.PaidAmount < i.TotalAmount)
                    .OrderBy(i => i.Date)
                    .Select(i => new PaymentInvoiceDto
                    {
                        Id = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        RemainingAmount = i.TotalAmount - i.PaidAmount
                    })
                    .ToList();

                invoices.Insert(0, new PaymentInvoiceDto { Id = 0, InvoiceNumber = "سداد عام", RemainingAmount = 0m });
                return invoices;
            }
        }

        public List<PaymentTransactionDto> GetRecentCustomerPayments(int customerId, int limit = 5)
        {
            using (var db = new AppDbContext())
            {
                return db.CustomerPayments
                    .Where(p => p.CustomerId == customerId)
                    .OrderByDescending(p => p.Date)
                    .Take(limit)
                    .Select(p => new PaymentTransactionDto
                    {
                        Date = p.Date,
                        Amount = p.Amount,
                        Reference = p.SaleInvoiceId.HasValue
                            ? db.SaleInvoices.Where(i => i.Id == p.SaleInvoiceId.Value).Select(i => i.InvoiceNumber).FirstOrDefault()
                            : "سداد عام"
                    })
                    .ToList();
            }
        }

        public List<PaymentTransactionDto> GetRecentSupplierPayments(int supplierId, int limit = 5)
        {
            using (var db = new AppDbContext())
            {
                return db.SupplierPayments
                    .Where(p => p.SupplierId == supplierId)
                    .OrderByDescending(p => p.Date)
                    .Take(limit)
                    .Select(p => new PaymentTransactionDto
                    {
                        Date = p.Date,
                        Amount = p.Amount,
                        Reference = p.PurchaseInvoiceId.HasValue
                            ? db.PurchaseInvoices.Where(i => i.Id == p.PurchaseInvoiceId.Value).Select(i => i.InvoiceNumber).FirstOrDefault()
                            : "سداد عام"
                    })
                    .ToList();
            }
        }

        public PaymentResult CreateCustomerPayment(CustomerPaymentRequest request)
        {
            return CreatePayment(new PaymentRequest
            {
                PaymentType = PaymentFlowType.CustomerReceipt,
                EntityId = request.CustomerId,
                InvoiceId = request.SaleInvoiceId,
                Amount = request.Amount,
                Date = request.Date,
                Notes = request.Notes,
                Method = request.Method
            });
        }

        public PaymentResult CreateSupplierPayment(SupplierPaymentRequest request)
        {
            return CreatePayment(new PaymentRequest
            {
                PaymentType = PaymentFlowType.SupplierDisbursement,
                EntityId = request.SupplierId,
                InvoiceId = request.PurchaseInvoiceId,
                Amount = request.Amount,
                Date = request.Date,
                Notes = request.Notes,
                Method = request.Method
            });
        }

        private PaymentResult CreatePayment(PaymentRequest request)
        {
            try
            {
                var paymentResult = transactionExecutor.Execute(
                    db =>
                    {
                        var validation = Validate(request, db);
                        if (!validation.IsValid)
                        {
                            return PaymentResult.Fail(validation.ErrorMessage);
                        }

                        var command = Process(validation);
                        return Commit(command, db);
                    },
                    executionResult => executionResult != null && executionResult.Success);

                if (paymentResult == null)
                {
                    return PaymentResult.Fail("فشل تسجيل السداد. حاول مرة أخرى.");
                }

                return paymentResult;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "CreatePayment failed");
                return PaymentResult.Fail("فشل تسجيل السداد. حاول مرة أخرى.");
            }
        }

        private PaymentValidationResult Validate(PaymentRequest request, AppDbContext db)
        {
            if (request == null || request.EntityId <= 0 || request.Amount <= 0)
            {
                return PaymentValidationResult.Fail("أدخل بيانات صحيحة");
            }

            var method = string.IsNullOrWhiteSpace(request.Method) ? CashMethod : request.Method.Trim();
            if (!AllowedPaymentMethods.Contains(method))
            {
                return PaymentValidationResult.Fail("طريقة الدفع غير صالحة");
            }

            if (request.PaymentType == PaymentFlowType.CustomerReceipt)
            {
                var customer = db.Customers.Find(request.EntityId);
                if (customer == null)
                {
                    return PaymentValidationResult.Fail("العميل غير موجود");
                }

                if (request.Amount > customer.Balance)
                {
                    return PaymentValidationResult.Fail("المبلغ أكبر من الرصيد");
                }

                SaleInvoice invoice = null;
                if (request.InvoiceId.HasValue)
                {
                    invoice = db.SaleInvoices.Find(request.InvoiceId.Value);
                    if (invoice == null || invoice.CustomerId != request.EntityId)
                    {
                        return PaymentValidationResult.Fail("الفاتورة غير موجودة");
                    }

                    var remaining = invoice.NetAmount - invoice.PaidAmount;
                    if (request.Amount > remaining)
                    {
                        return PaymentValidationResult.Fail("المبلغ أكبر من المتبقي");
                    }
                }

                return PaymentValidationResult.Valid(request, method, customer.Balance, invoice == null ? (decimal?)null : invoice.NetAmount - invoice.PaidAmount);
            }

            var supplier = db.Suppliers.Find(request.EntityId);
            if (supplier == null)
            {
                return PaymentValidationResult.Fail("المورد غير موجود");
            }

            if (request.Amount > supplier.Balance)
            {
                return PaymentValidationResult.Fail("المبلغ أكبر من الرصيد");
            }

            PurchaseInvoice purchaseInvoice = null;
            if (request.InvoiceId.HasValue)
            {
                purchaseInvoice = db.PurchaseInvoices.Find(request.InvoiceId.Value);
                if (purchaseInvoice == null || purchaseInvoice.SupplierId != request.EntityId)
                {
                    return PaymentValidationResult.Fail("الفاتورة غير موجودة");
                }

                var remaining = purchaseInvoice.TotalAmount - purchaseInvoice.PaidAmount;
                if (request.Amount > remaining)
                {
                    return PaymentValidationResult.Fail("المبلغ أكبر من المتبقي");
                }
            }

            return PaymentValidationResult.Valid(request, method, supplier.Balance, purchaseInvoice == null ? (decimal?)null : purchaseInvoice.TotalAmount - purchaseInvoice.PaidAmount);
        }

        private PaymentCommand Process(PaymentValidationResult validation)
        {
            return new PaymentCommand
            {
                PaymentType = validation.Request.PaymentType,
                EntityId = validation.Request.EntityId,
                InvoiceId = validation.Request.InvoiceId,
                Amount = validation.Request.Amount,
                Date = validation.Request.Date,
                Notes = validation.Request.Notes,
                Method = validation.Method
            };
        }

        /// <summary>
        /// Dispatches to the correct atomic commit method based on payment type.
        /// </summary>
        private PaymentResult Commit(PaymentCommand command, AppDbContext db)
        {
            if (command.PaymentType == PaymentFlowType.CustomerReceipt)
            {
                return CommitCustomerPayment(command, db);
            }
            else
            {
                return CommitSupplierPayment(command, db);
            }
        }

        /// <summary>
        /// Processes customer payment atomically within a single database transaction.
        /// 
        /// Optimized order:
        ///   1. If invoice-linked: UPDATE invoice PaidAmount FIRST (fail-fast on overpayment)
        ///   2. UPDATE customer balance
        ///   3. INSERT payment record
        ///   4. INSERT journal entry
        ///   5. COMMIT
        /// 
        /// Using GETDATE() for all timestamps ensures consistent server-side time.
        /// If any step affects 0 rows, entire transaction rolls back immediately.
        /// </summary>
        private PaymentResult CommitCustomerPayment(PaymentCommand command, AppDbContext db)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // ── STEP 1: UPDATE INVOICE FIRST (fail-fast on overpayment) ──
                    // Invoice update is more likely to fail than customer balance update
                    // because remaining amounts are typically smaller than balances
                    if (command.InvoiceId.HasValue)
                    {
                        int invoiceRows = db.Database.ExecuteSqlCommand(
                            @"UPDATE SaleInvoices 
                              SET PaidAmount = PaidAmount + @p0 
                              WHERE Id = @p1 
                                AND (PaidAmount + @p0) <= NetAmount",
                            command.Amount, command.InvoiceId.Value);

                        if (invoiceRows == 0)
                        {
                            transaction.Rollback();
                            var invoice = db.SaleInvoices.Find(command.InvoiceId.Value);
                            if (invoice == null)
                                return PaymentResult.Fail("الفاتورة غير موجودة");

                            decimal remaining = invoice.NetAmount - invoice.PaidAmount;
                            if (command.Amount > remaining)
                                return PaymentResult.Fail(string.Format("المبلغ أكبر من المتبقي ({0:N2} ج.م)", remaining));

                            return PaymentResult.Fail("تم تعديل الفاتورة بواسطة مستخدم آخر. حاول مرة أخرى.");
                        }
                    }

                    // ── STEP 2: ATOMIC CUSTOMER BALANCE UPDATE ──
                    // Only executes after invoice update succeeds (or if no invoice link)
                    int balanceRows = db.Database.ExecuteSqlCommand(
                        @"UPDATE Customers 
                          SET Balance = Balance - @p0 
                          WHERE Id = @p1 
                            AND Balance >= @p0",
                        command.Amount, command.EntityId);

                    if (balanceRows == 0)
                    {
                        transaction.Rollback();
                        var customer = db.Customers.Find(command.EntityId);
                        if (customer == null)
                            return PaymentResult.Fail("العميل غير موجود");

                        if (customer.Balance < command.Amount)
                            return PaymentResult.Fail(string.Format("المبلغ أكبر من الرصيد ({0:N2} ج.م)", customer.Balance));

                        return PaymentResult.Fail("تم تعديل الرصيد بواسطة مستخدم آخر. حاول مرة أخرى.");
                    }

                    // ── STEP 3: INSERT PAYMENT RECORD ──
                    // Use GETDATE() for database-server-side timestamp
                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO CustomerPayments (CustomerId, SaleInvoiceId, Amount, Date, Notes)
                          VALUES (@p0, @p1, @p2, GETDATE(), @p3)",
                        command.EntityId,
                        (object)command.InvoiceId ?? DBNull.Value,
                        command.Amount,
                        (object)command.Notes ?? DBNull.Value);

                    // ── STEP 4: INSERT JOURNAL ENTRY ──
                    AddCustomerPaymentJournalEntry(db, command);

                    // ── STEP 5: COMMIT ALL ──
                    db.SaveChanges(); // Persists journal entry lines (EF-tracked entities)
                    transaction.Commit();

                    Logger.Info(string.Format("Customer payment committed. CustomerId={0}, Amount={1:N2}, InvoiceId={2}",
                        command.EntityId, command.Amount, command.InvoiceId));
                    return PaymentResult.Ok();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error(ex, "CommitCustomerPayment failed");
                    return PaymentResult.Fail("فشل تسجيل السداد. حاول مرة أخرى.");
                }
            }
        }

        /// <summary>
        /// Processes supplier payment atomically within a single database transaction.
        /// Follows the same pattern as CommitCustomerPayment:
        ///   1. UPDATE purchase invoice PaidAmount (if linked) — fail-fast
        ///   2. UPDATE supplier balance
        ///   3. INSERT payment record
        ///   4. INSERT journal entry
        ///   5. COMMIT
        /// </summary>
        private PaymentResult CommitSupplierPayment(PaymentCommand command, AppDbContext db)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // ── STEP 1: UPDATE PURCHASE INVOICE FIRST (fail-fast on overpayment) ──
                    if (command.InvoiceId.HasValue)
                    {
                        int invoiceRows = db.Database.ExecuteSqlCommand(
                            @"UPDATE PurchaseInvoices 
                              SET PaidAmount = PaidAmount + @p0 
                              WHERE Id = @p1 
                                AND (PaidAmount + @p0) <= TotalAmount",
                            command.Amount, command.InvoiceId.Value);

                        if (invoiceRows == 0)
                        {
                            transaction.Rollback();
                            var invoice = db.PurchaseInvoices.Find(command.InvoiceId.Value);
                            if (invoice == null)
                                return PaymentResult.Fail("الفاتورة غير موجودة");

                            decimal remaining = invoice.TotalAmount - invoice.PaidAmount;
                            if (command.Amount > remaining)
                                return PaymentResult.Fail(string.Format("المبلغ أكبر من المتبقي ({0:N2} ج.م)", remaining));

                            return PaymentResult.Fail("تم تعديل الفاتورة بواسطة مستخدم آخر. حاول مرة أخرى.");
                        }
                    }

                    // ── STEP 2: ATOMIC SUPPLIER BALANCE UPDATE ──
                    int balanceRows = db.Database.ExecuteSqlCommand(
                        @"UPDATE Suppliers 
                          SET Balance = Balance - @p0 
                          WHERE Id = @p1 
                            AND Balance >= @p0",
                        command.Amount, command.EntityId);

                    if (balanceRows == 0)
                    {
                        transaction.Rollback();
                        var supplier = db.Suppliers.Find(command.EntityId);
                        if (supplier == null)
                            return PaymentResult.Fail("المورد غير موجود");

                        if (supplier.Balance < command.Amount)
                            return PaymentResult.Fail(string.Format("المبلغ أكبر من الرصيد ({0:N2} ج.م)", supplier.Balance));

                        return PaymentResult.Fail("تم تعديل الرصيد بواسطة مستخدم آخر. حاول مرة أخرى.");
                    }

                    // ── STEP 3: INSERT PAYMENT RECORD ──
                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO SupplierPayments (SupplierId, PurchaseInvoiceId, Amount, Date, Notes)
                          VALUES (@p0, @p1, @p2, GETDATE(), @p3)",
                        command.EntityId,
                        (object)command.InvoiceId ?? DBNull.Value,
                        command.Amount,
                        (object)command.Notes ?? DBNull.Value);

                    // ── STEP 4: INSERT JOURNAL ENTRY ──
                    AddSupplierPaymentJournalEntry(db, command);

                    // ── STEP 5: COMMIT ALL ──
                    db.SaveChanges();
                    transaction.Commit();

                    Logger.Info(string.Format("Supplier payment committed. SupplierId={0}, Amount={1:N2}, InvoiceId={2}",
                        command.EntityId, command.Amount, command.InvoiceId));
                    return PaymentResult.Ok();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error(ex, "CommitSupplierPayment failed");
                    return PaymentResult.Fail("فشل تسجيل السداد. حاول مرة أخرى.");
                }
            }
        }

        private static void AddCustomerPaymentJournalEntry(AppDbContext db, PaymentCommand command)
        {
            var cashAccountId = db.Accounts.Where(a => a.Code == "1.1.1").Select(a => (int?)a.Id).FirstOrDefault();
            var receivableAccountId = db.Accounts.Where(a => a.Code == "1.1.2").Select(a => (int?)a.Id).FirstOrDefault();
            if (!cashAccountId.HasValue || !receivableAccountId.HasValue)
            {
                throw new InvalidOperationException("حسابات قيود سداد العملاء غير معرفة");
            }

            var journal = new JournalEntry
            {
                Date = command.Date,
                EntryNumber = BuildEntryNumber("CPM"),
                Description = "سداد عميل",
                SourceType = "CustomerPayment"
            };
            db.JournalEntries.Add(journal);

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                AccountId = cashAccountId.Value,
                Description = "مدين خزينة",
                Debit = command.Amount,
                Credit = 0m
            });

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                AccountId = receivableAccountId.Value,
                Description = "دائن عملاء",
                Debit = 0m,
                Credit = command.Amount
            });
        }

        private static void AddSupplierPaymentJournalEntry(AppDbContext db, PaymentCommand command)
        {
            var cashAccountId = db.Accounts.Where(a => a.Code == "1.1.1").Select(a => (int?)a.Id).FirstOrDefault();
            var payableAccountId = db.Accounts.Where(a => a.Code == "2.1.1").Select(a => (int?)a.Id).FirstOrDefault();
            if (!cashAccountId.HasValue || !payableAccountId.HasValue)
            {
                throw new InvalidOperationException("حسابات قيود سداد الموردين غير معرفة");
            }

            var journal = new JournalEntry
            {
                Date = command.Date,
                EntryNumber = BuildEntryNumber("SPM"),
                Description = "سداد مورد",
                SourceType = "SupplierPayment"
            };
            db.JournalEntries.Add(journal);

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                AccountId = payableAccountId.Value,
                Description = "مدين موردين",
                Debit = command.Amount,
                Credit = 0m
            });

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                AccountId = cashAccountId.Value,
                Description = "دائن خزينة",
                Debit = 0m,
                Credit = command.Amount
            });
        }

        private static string BuildEntryNumber(string prefix)
        {
            return prefix + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }

        private class PaymentValidationResult
        {
            public bool IsValid { get; private set; }
            public string ErrorMessage { get; private set; }
            public PaymentRequest Request { get; private set; }
            public string Method { get; private set; }
            public decimal CurrentBalance { get; private set; }
            public decimal? InvoiceRemaining { get; private set; }

            public static PaymentValidationResult Valid(PaymentRequest request, string method, decimal currentBalance, decimal? invoiceRemaining)
            {
                return new PaymentValidationResult
                {
                    IsValid = true,
                    Request = request,
                    Method = method,
                    CurrentBalance = currentBalance,
                    InvoiceRemaining = invoiceRemaining
                };
            }

            public static PaymentValidationResult Fail(string errorMessage)
            {
                return new PaymentValidationResult { IsValid = false, ErrorMessage = errorMessage };
            }
        }

        private class PaymentCommand
        {
            public PaymentFlowType PaymentType { get; set; }
            public int EntityId { get; set; }
            public int? InvoiceId { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
            public string Notes { get; set; }
            public string Method { get; set; }
        }

        private class PaymentRequest
        {
            public PaymentFlowType PaymentType { get; set; }
            public int EntityId { get; set; }
            public int? InvoiceId { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
            public string Notes { get; set; }
            public string Method { get; set; }
        }
    }

    public enum PaymentFlowType
    {
        CustomerReceipt = 1,
        SupplierDisbursement = 2
    }

    public class CustomerPaymentRequest
    {
        public int CustomerId { get; set; }
        public int? SaleInvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Notes { get; set; }
        public string Method { get; set; }
    }

    public class SupplierPaymentRequest
    {
        public int SupplierId { get; set; }
        public int? PurchaseInvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Notes { get; set; }
        public string Method { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static PaymentResult Ok()
        {
            return new PaymentResult { Success = true };
        }

        public static PaymentResult Fail(string error)
        {
            return new PaymentResult { Success = false, ErrorMessage = error };
        }
    }

    public class PaymentCustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Balance { get; set; }
    }

    public class PaymentSupplierDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Balance { get; set; }
    }

    public class PaymentInvoiceDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal RemainingAmount { get; set; }
    }

    public class PaymentTransactionDto
    {
        public DateTime Date { get; set; }
        public string Reference { get; set; }
        public decimal Amount { get; set; }
    }
}
