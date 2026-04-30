using System;
using System.Collections.Generic;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Pure logic determining whether a posting date falls inside any locked
    /// fiscal period. Decoupled from EF so the rule itself is fully unit-testable.
    /// </summary>
    public static class AccountingPeriodLock
    {
        public readonly struct LockedPeriod
        {
            public LockedPeriod(int year, int month)
            {
                Year = year;
                Month = month;
            }

            public int Year { get; }
            /// <summary>0 == single calendar month is encoded as 1..12. -1 means the entire year is locked.</summary>
            public int Month { get; }
        }

        public static bool IsLocked(IEnumerable<LockedPeriod> lockedPeriods, DateTime postingDate)
        {
            if (lockedPeriods == null) return false;
            foreach (var period in lockedPeriods)
            {
                if (period.Year != postingDate.Year) continue;
                if (period.Month == -1) return true;
                if (period.Month >= 1 && period.Month <= 12 && period.Month == postingDate.Month) return true;
            }
            return false;
        }

        public static void EnsureUnlocked(IEnumerable<LockedPeriod> lockedPeriods, DateTime postingDate)
        {
            if (IsLocked(lockedPeriods, postingDate))
                throw new InvalidOperationException(
                    "لا يمكن الترحيل إلى فترة محاسبية مُقفلة (" +
                    postingDate.ToString("yyyy-MM-dd") + ")");
        }
    }
}
