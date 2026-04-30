using SupermarketPOS.Business.Posting;
using System;
using Xunit;

namespace SupermarketPOS.Workflow.Tests
{
    public class AccountingPeriodLockTests
    {
        [Fact]
        public void No_locks_means_every_date_is_postable()
        {
            Assert.False(AccountingPeriodLock.IsLocked(
                new AccountingPeriodLock.LockedPeriod[0],
                new DateTime(2025, 6, 15)));
        }

        [Fact]
        public void Locked_month_blocks_posting_in_that_month_only()
        {
            var locks = new[] { new AccountingPeriodLock.LockedPeriod(2025, 6) };
            Assert.True(AccountingPeriodLock.IsLocked(locks, new DateTime(2025, 6, 1)));
            Assert.True(AccountingPeriodLock.IsLocked(locks, new DateTime(2025, 6, 30)));
            Assert.False(AccountingPeriodLock.IsLocked(locks, new DateTime(2025, 7, 1)));
            Assert.False(AccountingPeriodLock.IsLocked(locks, new DateTime(2024, 6, 15)));
        }

        [Fact]
        public void Whole_year_lock_blocks_every_month_in_that_year()
        {
            var locks = new[] { new AccountingPeriodLock.LockedPeriod(2024, -1) };
            Assert.True(AccountingPeriodLock.IsLocked(locks, new DateTime(2024, 1, 1)));
            Assert.True(AccountingPeriodLock.IsLocked(locks, new DateTime(2024, 12, 31)));
            Assert.False(AccountingPeriodLock.IsLocked(locks, new DateTime(2025, 1, 1)));
        }

        [Fact]
        public void Ensure_unlocked_throws_inside_locked_range()
        {
            var locks = new[] { new AccountingPeriodLock.LockedPeriod(2025, 3) };
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AccountingPeriodLock.EnsureUnlocked(locks, new DateTime(2025, 3, 15)));
            Assert.Contains("2025-03-15", ex.Message);
        }

        [Fact]
        public void Ensure_unlocked_passes_outside_locked_range()
        {
            var locks = new[] { new AccountingPeriodLock.LockedPeriod(2025, 3) };
            AccountingPeriodLock.EnsureUnlocked(locks, new DateTime(2025, 4, 15));
        }
    }
}
