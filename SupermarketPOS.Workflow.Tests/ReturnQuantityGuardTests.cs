using SupermarketPOS.Business.Posting;
using System;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class ReturnQuantityGuardTests
    {
        [Fact]
        public void Allows_full_quantity_when_nothing_returned_yet()
        {
            Assert.True(ReturnQuantityGuard.IsAllowed(10, 0, 10));
        }

        [Fact]
        public void Allows_partial_returns_until_total_matches_original()
        {
            Assert.True(ReturnQuantityGuard.IsAllowed(10, 4, 6));
            Assert.False(ReturnQuantityGuard.IsAllowed(10, 4, 7));
        }

        [Fact]
        public void Rejects_over_return()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ReturnQuantityGuard.EnsureAllowed(5, 3, 3, "Sample Product"));
            Assert.Contains("Sample Product", ex.Message);
        }

        [Fact]
        public void Rejects_zero_or_negative_request()
        {
            Assert.False(ReturnQuantityGuard.IsAllowed(10, 0, 0));
            Assert.False(ReturnQuantityGuard.IsAllowed(10, 0, -1));
        }

        [Fact]
        public void Rejects_when_original_quantity_invalid()
        {
            Assert.False(ReturnQuantityGuard.IsAllowed(0, 0, 1));
        }
    }
}
