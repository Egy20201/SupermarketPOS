using System;
using SupermarketPOS.Data;

namespace SupermarketPOS.Business.Posting
{
    /// <summary>
    /// Resolves whether the supplied posting date falls inside a locked fiscal
    /// period. Implementations may consult the database, an in-memory cache, or
    /// a fake for tests.
    /// </summary>
    public interface IFiscalPeriodLockChecker
    {
        bool IsLocked(DateTime postingDate);
        void EnsureUnlocked(DateTime postingDate);
    }

    public sealed class FiscalPeriodLockChecker : IFiscalPeriodLockChecker
    {
        private readonly Func<AppDbContext> _dbFactory;

        public FiscalPeriodLockChecker(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public bool IsLocked(DateTime postingDate)
        {
            using (var db = _dbFactory())
            {
                var year = postingDate.Year;
                var month = postingDate.Month;
                return System.Linq.Queryable.Any(
                    System.Data.Entity.QueryableExtensions.AsNoTracking(db.FiscalPeriods),
                    p => p.IsLocked && p.Year == year && (p.Month == -1 || p.Month == month));
            }
        }

        public void EnsureUnlocked(DateTime postingDate)
        {
            if (IsLocked(postingDate))
                throw new InvalidOperationException(
                    "لا يمكن الترحيل إلى فترة محاسبية مُقفلة (" +
                    postingDate.ToString("yyyy-MM-dd") + ")");
        }
    }
}
