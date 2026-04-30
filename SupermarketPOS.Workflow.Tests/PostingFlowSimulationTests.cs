using SupermarketPOS.Business.Posting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    /// <summary>
    /// Phase 5 test matrix — simulates the posting flows against an in-memory
    /// "ledger + warehouse" so we can prove the domain invariants without
    /// requiring SQL Server / EF6.
    ///
    /// 1. Sale reduces stock and creates a balanced journal
    /// 2. Purchase increases stock correctly
    /// 3. Return cannot exceed original quantity
    /// 4. Failure anywhere -> full rollback
    /// 5. Concurrent sales do not corrupt stock
    /// 6. Accounting entries are always balanced
    /// </summary>
    public class PostingFlowSimulationTests
    {
        // ------------------------------------------------------------------
        // Minimal in-memory ERP core mirroring the production semantics
        // (atomic stock decrement with Quantity >= requested guard, append-only
        // stock movements, append-only journal entries, simple commit/rollback).
        // ------------------------------------------------------------------
        private sealed class Ledger
        {
            private readonly Dictionary<int, int> _stock = new Dictionary<int, int>();
            private readonly object _stockGate = new object();
            private readonly List<(int productId, int delta, string reference, string type)> _movements
                = new List<(int, int, string, string)>();
            private readonly List<List<JournalBalance.Line>> _journals = new List<List<JournalBalance.Line>>();
            private readonly Dictionary<string, int> _returnedByReference = new Dictionary<string, int>();
            private long _saleSeq;

            public int StockOf(int productId)
            {
                lock (_stockGate)
                    return _stock.TryGetValue(productId, out var q) ? q : 0;
            }

            public IReadOnlyList<(int productId, int delta, string reference, string type)> Movements
            {
                get { lock (_stockGate) return _movements.ToArray(); }
            }

            public IReadOnlyList<IReadOnlyList<JournalBalance.Line>> Journals
            {
                get { lock (_stockGate) return _journals.Select(j => (IReadOnlyList<JournalBalance.Line>)j.ToArray()).ToArray(); }
            }

            public string NextSaleNumber() => "S-" + Interlocked.Increment(ref _saleSeq);

            public void IncreaseStock(int productId, int qty, string reference)
            {
                lock (_stockGate)
                {
                    if (!_stock.ContainsKey(productId)) _stock[productId] = 0;
                    _stock[productId] += qty;
                    _movements.Add((productId, +qty, reference, "Purchase"));
                }
            }

            // Returns true iff stock is sufficient AND atomically decremented.
            public bool TryDecreaseStock(int productId, int qty, string reference, string type)
            {
                lock (_stockGate)
                {
                    if (!_stock.TryGetValue(productId, out var current) || current < qty)
                        return false;
                    _stock[productId] = current - qty;
                    _movements.Add((productId, -qty, reference, type));
                    return true;
                }
            }

            public int PreviouslyReturned(string baseReference)
            {
                lock (_stockGate)
                    return _returnedByReference.TryGetValue(baseReference, out var v) ? v : 0;
            }

            public void RecordReturn(string baseReference, int qty, int productId)
            {
                lock (_stockGate)
                {
                    if (!_returnedByReference.ContainsKey(baseReference))
                        _returnedByReference[baseReference] = 0;
                    _returnedByReference[baseReference] += qty;
                    if (!_stock.ContainsKey(productId)) _stock[productId] = 0;
                    _stock[productId] += qty;
                    _movements.Add((productId, +qty, baseReference, "SalesReturn"));
                }
            }

            public void RollbackLastMovement()
            {
                lock (_stockGate)
                {
                    if (_movements.Count == 0) return;
                    var last = _movements[_movements.Count - 1];
                    _movements.RemoveAt(_movements.Count - 1);
                    if (!_stock.ContainsKey(last.productId)) _stock[last.productId] = 0;
                    _stock[last.productId] -= last.delta;
                }
            }

            public void PostJournal(IReadOnlyList<JournalBalance.Line> lines)
            {
                JournalBalance.EnsureBalanced(lines);
                lock (_stockGate)
                    _journals.Add(lines.ToList());
            }
        }

        private sealed class TxScope : IDisposable
        {
            private readonly Action _onRollback;
            private bool _committed;
            public TxScope(Action onRollback) { _onRollback = onRollback; }
            public void Commit() { _committed = true; }
            public void Dispose() { if (!_committed) _onRollback(); }
        }

        // ------------------------------------------------------------------
        // Posting helpers under test
        // ------------------------------------------------------------------
        private static void PostSale(Ledger ledger, int productId, decimal qty, decimal conversion, decimal unitPrice, decimal documentDiscountPercent)
        {
            PriceIntegrity.EnsureValid(
                new[] { new PriceIntegrity.Line(qty, unitPrice, 0m) },
                documentDiscountPercent,
                PriceIntegrity.Limits.Default);

            var baseQty = UnitConversion.ToBaseQuantity(qty, conversion);
            var net = qty * unitPrice * (100m - documentDiscountPercent) / 100m;
            var reference = ledger.NextSaleNumber();

            using (var tx = new TxScope(() => ledger.RollbackLastMovement()))
            {
                if (!ledger.TryDecreaseStock(productId, baseQty, reference, "Sale"))
                    throw new InvalidOperationException("المخزون غير كافٍ");

                ledger.PostJournal(new[]
                {
                    new JournalBalance.Line(net, 0m),  // Debit Cash
                    new JournalBalance.Line(0m, net)   // Credit Sales Revenue
                });

                tx.Commit();
            }
        }

        private static void PostPurchase(Ledger ledger, int productId, int qty, decimal unitPrice)
        {
            PriceIntegrity.EnsureValid(
                new[] { new PriceIntegrity.Line(qty, unitPrice, 0m) },
                0m,
                PriceIntegrity.Limits.Default);

            var net = qty * unitPrice;
            var reference = "P-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            ledger.IncreaseStock(productId, qty, reference);
            try
            {
                ledger.PostJournal(new[]
                {
                    new JournalBalance.Line(net, 0m),  // Debit Inventory
                    new JournalBalance.Line(0m, net)   // Credit Payable
                });
            }
            catch
            {
                ledger.RollbackLastMovement();
                throw;
            }
        }

        private static void PostSalesReturn(Ledger ledger, string originalSaleRef, int productId, int originalQty, int returnQty, decimal unitPrice)
        {
            var prior = ledger.PreviouslyReturned(originalSaleRef);
            ReturnQuantityGuard.EnsureAllowed(originalQty, prior, returnQty, "Sample");

            var total = returnQty * unitPrice;
            ledger.RecordReturn(originalSaleRef, returnQty, productId);
            ledger.PostJournal(new[]
            {
                new JournalBalance.Line(total, 0m),
                new JournalBalance.Line(0m, total)
            });
        }

        // ------------------------------------------------------------------
        // Test matrix
        // ------------------------------------------------------------------

        [Fact] // (1) Sale reduces stock and creates journal
        public void Sale_reduces_stock_and_emits_balanced_journal()
        {
            var ledger = new Ledger();
            ledger.IncreaseStock(productId: 1, qty: 100, reference: "INIT");

            PostSale(ledger, productId: 1, qty: 5m, conversion: 1m, unitPrice: 20m, documentDiscountPercent: 0m);

            Assert.Equal(95, ledger.StockOf(1));
            Assert.Single(ledger.Journals);

            // The sale journal must balance to 5 * 20 = 100 on each side.
            decimal d, c;
            Assert.True(JournalBalance.IsBalanced(ledger.Journals.Last(), out d, out c));
            Assert.Equal(100m, d);
            Assert.Equal(100m, c);
        }

        [Fact] // (1b) Box -> pieces conversion is normalised before stock decrement.
        public void Sale_in_sub_units_decrements_base_quantity()
        {
            var ledger = new Ledger();
            ledger.IncreaseStock(productId: 1, qty: 60, reference: "INIT");

            // Buy 2 boxes of 12 pieces each = 24 pieces deducted.
            PostSale(ledger, productId: 1, qty: 2m, conversion: 12m, unitPrice: 5m, documentDiscountPercent: 0m);

            Assert.Equal(36, ledger.StockOf(1));
        }

        [Fact] // (2) Purchase increases stock correctly.
        public void Purchase_increases_stock_and_balances_journal()
        {
            var ledger = new Ledger();
            PostPurchase(ledger, productId: 1, qty: 50, unitPrice: 7m);
            Assert.Equal(50, ledger.StockOf(1));

            decimal d, c;
            Assert.True(JournalBalance.IsBalanced(ledger.Journals.Single(), out d, out c));
            Assert.Equal(350m, d);
            Assert.Equal(350m, c);
        }

        [Fact] // (3) Return cannot exceed original quantity.
        public void Return_above_sold_quantity_is_rejected()
        {
            var ledger = new Ledger();
            ledger.IncreaseStock(1, 10, "INIT");
            PostSale(ledger, 1, 5m, 1m, 10m, 0m);

            // Returning 4 then 2 (total 6) must fail because only 5 were sold.
            PostSalesReturn(ledger, originalSaleRef: "S-1", productId: 1, originalQty: 5, returnQty: 4, unitPrice: 10m);
            Assert.Throws<InvalidOperationException>(() =>
                PostSalesReturn(ledger, originalSaleRef: "S-1", productId: 1, originalQty: 5, returnQty: 2, unitPrice: 10m));

            // After 4 returned and the second rejected, stock should be 10 - 5 + 4 = 9.
            Assert.Equal(9, ledger.StockOf(1));
        }

        [Fact] // (4) Failure anywhere -> full rollback.
        public void Failure_in_journal_rolls_back_stock_decrement()
        {
            var ledger = new Ledger();
            ledger.IncreaseStock(1, 10, "INIT");

            // Force a failure by feeding a deliberately unbalanced journal AFTER decrementing stock.
            Action attempt = () =>
            {
                using (var tx = new TxScope(() => ledger.RollbackLastMovement()))
                {
                    Assert.True(ledger.TryDecreaseStock(1, 5, "S-bogus", "Sale"));
                    ledger.PostJournal(new[]
                    {
                        new JournalBalance.Line(50m, 0m),
                        new JournalBalance.Line(0m, 49m) // off by one => not balanced
                    });
                    tx.Commit();
                }
            };

            Assert.Throws<InvalidOperationException>(attempt);

            // Stock decrement must have been rolled back.
            Assert.Equal(10, ledger.StockOf(1));
            // No journal should have been recorded.
            Assert.Empty(ledger.Journals);
        }

        [Fact] // (5) Concurrent sales do not corrupt stock.
        public void Concurrent_sales_never_oversell()
        {
            var ledger = new Ledger();
            ledger.IncreaseStock(1, 1000, "INIT");

            const int Workers = 16;
            const int SalesPerWorker = 50;

            var tasks = new List<Task<int>>();
            for (int w = 0; w < Workers; w++)
            {
                tasks.Add(Task.Run(() =>
                {
                    int succeeded = 0;
                    for (int i = 0; i < SalesPerWorker; i++)
                    {
                        try
                        {
                            PostSale(ledger, productId: 1, qty: 1m, conversion: 1m, unitPrice: 1m, documentDiscountPercent: 0m);
                            succeeded++;
                        }
                        catch (InvalidOperationException)
                        {
                            // Stock exhaustion is the expected failure mode.
                        }
                    }
                    return succeeded;
                }));
            }

            Task.WaitAll(tasks.Cast<Task>().ToArray());

            var totalSales = tasks.Sum(t => t.Result);
            // Stock on hand + units sold must equal initial deposit.
            Assert.Equal(1000, totalSales + ledger.StockOf(1));
            // We must never go negative.
            Assert.True(ledger.StockOf(1) >= 0);
            // Total sales bounded by initial stock.
            Assert.True(totalSales <= 1000);
        }

        [Fact] // (6) Accounting entries are always balanced.
        public void All_recorded_journals_are_balanced()
        {
            var ledger = new Ledger();
            PostPurchase(ledger, 1, 100, 5m);
            PostSale(ledger, 1, 3m, 1m, 10m, 0m);
            PostSale(ledger, 1, 2m, 1m, 10m, 50m);
            PostSalesReturn(ledger, "S-1", 1, 3, 1, 10m);

            foreach (var journal in ledger.Journals)
            {
                decimal d, c;
                Assert.True(JournalBalance.IsBalanced(journal, out d, out c),
                    $"journal must balance (d={d}, c={c})");
            }
        }
    }
}
