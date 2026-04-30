using SupermarketPOS.Business.Posting;
using System;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class JournalBalanceTests
    {
        [Fact]
        public void Balanced_simple_two_line_entry()
        {
            decimal d, c;
            var ok = JournalBalance.IsBalanced(new[]
            {
                new JournalBalance.Line(100m, 0m),
                new JournalBalance.Line(0m, 100m)
            }, out d, out c);
            Assert.True(ok);
            Assert.Equal(100m, d);
            Assert.Equal(100m, c);
        }

        [Fact]
        public void Balanced_multi_credit_split()
        {
            decimal d, c;
            var ok = JournalBalance.IsBalanced(new[]
            {
                new JournalBalance.Line(500m, 0m),
                new JournalBalance.Line(0m, 200m),
                new JournalBalance.Line(0m, 300m)
            }, out d, out c);
            Assert.True(ok);
            Assert.Equal(500m, d);
            Assert.Equal(500m, c);
        }

        [Fact]
        public void Unbalanced_throws_on_ensure()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                JournalBalance.EnsureBalanced(new[]
                {
                    new JournalBalance.Line(100m, 0m),
                    new JournalBalance.Line(0m, 99m)
                }));
            Assert.Contains("not balanced", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Negative_amount_is_rejected()
        {
            decimal d, c;
            Assert.False(JournalBalance.IsBalanced(new[]
            {
                new JournalBalance.Line(-50m, 0m),
                new JournalBalance.Line(0m, -50m)
            }, out d, out c));
        }

        [Fact]
        public void Mixed_debit_and_credit_on_single_line_is_rejected()
        {
            decimal d, c;
            Assert.False(JournalBalance.IsBalanced(new[]
            {
                new JournalBalance.Line(50m, 50m)
            }, out d, out c));
        }

        [Fact]
        public void Tolerance_allows_rounding_drift()
        {
            decimal d, c;
            var ok = JournalBalance.IsBalanced(new[]
            {
                new JournalBalance.Line(100.001m, 0m),
                new JournalBalance.Line(0m, 100m)
            }, out d, out c);
            Assert.True(ok);
        }
    }
}
