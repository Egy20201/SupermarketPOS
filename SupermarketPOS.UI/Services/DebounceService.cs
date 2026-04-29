using System;
using System.Windows.Threading;

namespace SupermarketPOS.UI.Services
{
    public class DebounceService
    {
        private DispatcherTimer _timer;
        private Action _action;
        private int _delayMs;

        public DebounceService(int delayMs = 300)
        {
            _delayMs = delayMs;
        }

        public void Debounce(Action action)
        {
            _action = action;
            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_delayMs) };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                _action?.Invoke();
            };
            _timer.Start();
        }

        public void Cancel() => _timer?.Stop();
    }
}