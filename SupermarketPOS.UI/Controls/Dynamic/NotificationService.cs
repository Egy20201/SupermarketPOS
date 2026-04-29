using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3.6: Non-blocking notification service replacing all MessageBox usage.
    /// 
    /// Provides:
    ///   - Toast-style status messages via StatusMessage property
    ///   - Confirmation requests via async callback (non-blocking)
    ///   - Auto-dismiss after configurable timeout
    ///   - Thread-safe: always marshals to UI via BeginInvoke
    /// </summary>
    public sealed class DynamicNotificationService
    {
        private static readonly Lazy<DynamicNotificationService> _instance =
            new Lazy<DynamicNotificationService>(() => new DynamicNotificationService());

        public static DynamicNotificationService Instance => _instance.Value;

        // ================================================================
        //  Notification State (bindable from UI)
        // ================================================================

        private string _message = string.Empty;
        private DynamicNotificationType _type = DynamicNotificationType.None;
        private bool _isVisible;

        public string Message => _message;
        public DynamicNotificationType Type => _type;
        public bool IsVisible => _isVisible;

        // ================================================================
        //  Events
        // ================================================================

        /// <summary>Fired when a notification is shown or cleared. UI subscribes to update banners.</summary>
        public event EventHandler NotificationChanged;

        /// <summary>Fired when a confirmation is requested. UI subscribes to show inline confirm banner.</summary>
        public event EventHandler<ConfirmationRequestEventArgs> ConfirmationRequested;

        // ================================================================
        //  Auto-dismiss timer
        // ================================================================

        private CancellationTokenSource _dismissCts;
        private const int DefaultDismissMs = 4000;

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>Show a non-blocking info toast.</summary>
        public void ShowInfo(string message, int dismissMs = DefaultDismissMs)
        {
            Show(message, DynamicNotificationType.Info, dismissMs);
        }

        /// <summary>Show a non-blocking success toast.</summary>
        public void ShowSuccess(string message, int dismissMs = DefaultDismissMs)
        {
            Show(message, DynamicNotificationType.Success, dismissMs);
        }

        /// <summary>Show a non-blocking warning toast.</summary>
        public void ShowWarning(string message, int dismissMs = DefaultDismissMs)
        {
            Show(message, DynamicNotificationType.Warning, dismissMs);
        }

        /// <summary>Show a non-blocking error toast.</summary>
        public void ShowError(string message, int dismissMs = 6000)
        {
            Show(message, DynamicNotificationType.Error, dismissMs);
        }

        /// <summary>Clear the current notification.</summary>
        public void Clear()
        {
            _dismissCts?.Cancel();
            _dismissCts = null;
            _message = string.Empty;
            _type = DynamicNotificationType.None;
            _isVisible = false;
            RaiseChanged();
        }

        /// <summary>
        /// Request a non-blocking confirmation. Returns true if user confirms.
        /// The UI layer listens to ConfirmationRequested and calls back with the result.
        /// If no UI is listening, defaults to true (proceed).
        /// </summary>
        public Task<bool> ConfirmAsync(string message, string title = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            var args = new ConfirmationRequestEventArgs(message, title ?? "تأكيد", tcs);

            var handler = ConfirmationRequested;
            if (handler == null)
            {
                // No UI listening — default to proceed
                tcs.TrySetResult(true);
            }
            else
            {
                handler.Invoke(this, args);
            }

            return tcs.Task;
        }

        // ================================================================
        //  Internal
        // ================================================================

        private void Show(string message, DynamicNotificationType type, int dismissMs)
        {
            // Cancel any pending auto-dismiss
            _dismissCts?.Cancel();

            _message = message ?? string.Empty;
            _type = type;
            _isVisible = true;
            RaiseChanged();

            // Schedule auto-dismiss
            if (dismissMs > 0)
            {
                _dismissCts = new CancellationTokenSource();
                var token = _dismissCts.Token;
                _ = AutoDismissAsync(dismissMs, token);
            }
        }

        private async Task AutoDismissAsync(int delayMs, CancellationToken token)
        {
            try
            {
                await Task.Delay(delayMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                    Clear();
            }
            catch (TaskCanceledException)
            {
                // Expected when a new notification replaces the old one
            }
        }

        private void RaiseChanged()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => NotificationChanged?.Invoke(this, EventArgs.Empty)));
            }
            else
            {
                NotificationChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private DynamicNotificationService() { }
    }

    // ================================================================
    //  Supporting Types
    // ================================================================

    public enum DynamicNotificationType
    {
        None,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Event args for non-blocking confirmation requests.
    /// The UI handler must call Complete(true/false) to resolve the awaiting task.
    /// </summary>
    public sealed class ConfirmationRequestEventArgs : EventArgs
    {
        public string Message { get; }
        public string Title { get; }

        private readonly TaskCompletionSource<bool> _tcs;

        public ConfirmationRequestEventArgs(string message, string title, TaskCompletionSource<bool> tcs)
        {
            Message = message;
            Title = title;
            _tcs = tcs;
        }

        /// <summary>Call this from the UI to complete the confirmation.</summary>
        public void Complete(bool confirmed)
        {
            _tcs.TrySetResult(confirmed);
        }
    }
}
