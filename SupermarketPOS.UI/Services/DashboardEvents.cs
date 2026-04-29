using System;

namespace SupermarketPOS.UI.Services
{
    public static class DashboardEvents
    {
        private static EventHandler _refreshRequested;
        public static event EventHandler RefreshRequested
        {
            add { _refreshRequested += value; }
            remove { _refreshRequested -= value; }
        }
        public static void RequestRefresh() => _refreshRequested?.Invoke(null, EventArgs.Empty);
    }
}