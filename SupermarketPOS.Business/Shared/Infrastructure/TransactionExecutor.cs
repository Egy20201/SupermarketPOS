using SupermarketPOS.Data;
using System;

namespace SupermarketPOS.Business
{
    public class TransactionExecutor
    {
        private readonly Func<AppDbContext> _dbFactory;

        public TransactionExecutor(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public T Execute<T>(Func<AppDbContext, T> operation, Func<T, bool> shouldCommit)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (shouldCommit == null) throw new ArgumentNullException(nameof(shouldCommit));

            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var result = operation(db);
                    if (shouldCommit(result))
                    {
                        tx.Commit();
                    }
                    else
                    {
                        tx.Rollback();
                    }

                    return result;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }
    }
}
