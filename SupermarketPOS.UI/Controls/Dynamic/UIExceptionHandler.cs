using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3.6: Global error pipeline for the Dynamic UI engine.
    /// 
    /// Rules:
    ///   - Log full exception (Debug + Trace)
    ///   - Show friendly notification via NotificationService
    ///   - NEVER throw to UI thread
    ///   - All public methods are safe to call from any thread
    /// </summary>
    public static class UIExceptionHandler
    {
        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// Handle a caught exception. Logs it and shows a friendly notification.
        /// Safe to call from any thread.
        /// </summary>
        public static void Handle(Exception ex, string context = null)
        {
            if (ex == null) return;

            // Log full details
            var contextLabel = string.IsNullOrEmpty(context) ? "DynamicUI" : context;
            var logMessage = $"[{contextLabel}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            Debug.WriteLine(logMessage);
            Trace.TraceError(logMessage);

            // Show friendly notification (non-blocking)
            var friendlyMessage = BuildFriendlyMessage(ex, context);
            DynamicNotificationService.Instance.ShowError(friendlyMessage);
        }

        /// <summary>
        /// Wrap an async action with exception handling. Returns a safe Task.
        /// Use this instead of try/catch boilerplate everywhere.
        /// </summary>
        public static async Task RunSafeAsync(Func<Task> action, string context = null)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation — silently ignore
            }
            catch (Exception ex)
            {
                Handle(ex, context);
            }
        }

        /// <summary>
        /// Wrap an async action with exception handling and return a result.
        /// Returns default(T) on failure.
        /// </summary>
        public static async Task<T> RunSafeAsync<T>(Func<Task<T>> action, string context = null)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return default;
            }
            catch (Exception ex)
            {
                Handle(ex, context);
                return default;
            }
        }

        /// <summary>
        /// Wrap a synchronous action with exception handling.
        /// </summary>
        public static void RunSafe(Action action, string context = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Handle(ex, context);
            }
        }

        /// <summary>
        /// Wire up global unhandled exception handlers.
        /// Call once from App.xaml.cs during startup.
        /// </summary>
        public static void WireGlobalHandlers()
        {
            // UI thread exceptions
            if (Application.Current != null)
            {
                Application.Current.DispatcherUnhandledException += (s, e) =>
                {
                    Handle(e.Exception, "UnhandledUI");
                    e.Handled = true; // Prevent crash
                };
            }

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Handle(ex, "UnhandledDomain");
            };

            // Task exceptions (unobserved)
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Handle(e.Exception, "UnobservedTask");
                e.SetObserved(); // Prevent crash
            };
        }

        // ================================================================
        //  Internal
        // ================================================================

        private static string BuildFriendlyMessage(Exception ex, string context)
        {
            // Map known exception types to user-friendly Arabic messages
            if (ex is TimeoutException)
                return "انتهت المهلة الزمنية. يرجى المحاولة مرة أخرى.";

            if (ex is UnauthorizedAccessException)
                return "لا توجد صلاحيات كافية لهذه العملية.";

            if (ex is InvalidOperationException && ex.Message.Contains("connection"))
                return "خطأ في الاتصال بقاعدة البيانات.";

            if (ex is OperationCanceledException)
                return "تم إلغاء العملية.";

            // Generic fallback
            var prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            return $"{prefix}خطأ: {ex.Message}";
        }
    }
}
