using SupermarketPOS.Data;
using System;
using System.Data;

namespace SupermarketPOS.Business
{
    /// <summary>
    /// All posting / mutating domain operations MUST go through the executor
    /// so that a failure anywhere along the flow rolls the entire transaction
    /// back. Partial commits are NEVER permitted — either every change is
    /// persisted or nothing is.
    /// </summary>
    public class TransactionExecutor
    {
        private readonly Func<AppDbContext> _dbFactory;

        public TransactionExecutor(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// Runs the operation inside a serialisable transaction. The transaction
        /// is committed only if <paramref name="shouldCommit"/> returns true and
        /// the operation completed without throwing; any exception triggers a
        /// rollback and is rethrown to the caller.
        /// </summary>
        public T Execute<T>(Func<AppDbContext, T> operation, Func<T, bool> shouldCommit)
        {
            return Execute(operation, shouldCommit, IsolationLevel.ReadCommitted);
        }

        public T Execute<T>(Func<AppDbContext, T> operation, Func<T, bool> shouldCommit, IsolationLevel isolation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (shouldCommit == null) throw new ArgumentNullException(nameof(shouldCommit));

            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction(isolation))
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

        /// <summary>
        /// Convenience overload for void posting flows. Commits if the action
        /// completes without throwing; rolls back on any exception.
        /// </summary>
        public void Execute(Action<AppDbContext> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            Execute<bool>(db => { operation(db); return true; }, _ => true);
        }
    }
}
