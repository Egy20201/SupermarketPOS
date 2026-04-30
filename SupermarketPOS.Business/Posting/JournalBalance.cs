using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Asserts the fundamental accounting invariant: total debits == total credits.
    /// Every journal entry MUST be balanced before it can be posted to the ledger.
    /// </summary>
    public static class JournalBalance
    {
        /// <summary>
        /// Tolerance used when comparing the sum of debits to the sum of credits.
        /// Two thousandths of a currency unit absorbs typical rounding while still
        /// rejecting any genuine imbalance.
        /// </summary>
        public const decimal Tolerance = 0.005m;

        public readonly struct Line
        {
            public Line(decimal debit, decimal credit)
            {
                Debit = debit;
                Credit = credit;
            }

            public decimal Debit { get; }
            public decimal Credit { get; }
        }

        public static bool IsBalanced(IEnumerable<Line> lines, out decimal debitTotal, out decimal creditTotal)
        {
            debitTotal = 0m;
            creditTotal = 0m;
            if (lines == null) return true;
            foreach (var line in lines)
            {
                if (line.Debit < 0m || line.Credit < 0m)
                    return false;
                if (line.Debit > 0m && line.Credit > 0m)
                    return false;
                debitTotal += line.Debit;
                creditTotal += line.Credit;
            }
            return Math.Abs(debitTotal - creditTotal) <= Tolerance;
        }

        public static void EnsureBalanced(IEnumerable<Line> lines)
        {
            decimal debit, credit;
            if (!IsBalanced(lines, out debit, out credit))
                throw new InvalidOperationException(
                    "Journal entry is not balanced (debits=" + debit.ToString("0.##") +
                    ", credits=" + credit.ToString("0.##") + ").");
        }
    }
}
