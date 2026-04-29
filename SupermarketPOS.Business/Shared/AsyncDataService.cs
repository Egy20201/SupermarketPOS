using System;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business
{
    public static class AsyncDataService
    {
        private static readonly object _lock = new object();
        private static int _activeOperationCount;
        private static SynchronizationContext _syncContext;

        public static event EventHandler<AsyncOperationEventArgs> OperationStatusChanged;
        public static int ActiveOperationCount => _activeOperationCount;
        public static bool IsLoading => _activeOperationCount > 0;

        public static void Initialize(SynchronizationContext syncContext)
        {
            _syncContext = syncContext ?? throw new ArgumentNullException(nameof(syncContext));
        }

        public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, string operationName = null, CancellationToken cancellationToken = default)
        {
            IncrementActiveOperations();
            try { return await Task.Run(() => operation(cancellationToken), cancellationToken); }
            catch (OperationCanceledException) { return default; }
            catch (Exception ex) { Logger.Error(ex, operationName ?? "Async operation"); return default; }
            finally { DecrementActiveOperations(); }
        }

        public static async Task ExecuteAsync(Func<CancellationToken, Task> operation, string operationName = null, CancellationToken cancellationToken = default)
        {
            IncrementActiveOperations();
            try { await Task.Run(() => operation(cancellationToken), cancellationToken); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.Error(ex, operationName ?? "Async operation"); }
            finally { DecrementActiveOperations(); }
        }

        public static void RunOnUIThread(Action action)
        {
            var ctx = _syncContext;
            if (ctx != null)
                ctx.Send(_ => action(), null);
            else
                action();
        }

        private static void IncrementActiveOperations()
        {
            lock (_lock) { var wasLoading = _activeOperationCount > 0; _activeOperationCount++; if (!wasLoading) OnOperationStatusChanged(true); }
        }

        private static void DecrementActiveOperations()
        {
            lock (_lock) { _activeOperationCount--; if (_activeOperationCount == 0) OnOperationStatusChanged(false); }
        }

        private static void OnOperationStatusChanged(bool isLoading) => RunOnUIThread(() => OperationStatusChanged?.Invoke(null, new AsyncOperationEventArgs { IsLoading = isLoading, ActiveOperationCount = _activeOperationCount }));
    }

    public class AsyncOperationEventArgs : EventArgs { public bool IsLoading { get; set; } public int ActiveOperationCount { get; set; } }
}
