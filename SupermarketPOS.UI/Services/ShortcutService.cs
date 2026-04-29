using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace SupermarketPOS.UI.Services
{
    public static class ShortcutService
    {
        private static Window _currentWindow;
        private static readonly Dictionary<string, Action> _shortcuts = new Dictionary<string, Action>();

        public static void RegisterWindow(Window window)
        {
            _currentWindow = window;
            _currentWindow.PreviewKeyDown += OnKeyDown;
        }

        public static void UnregisterWindow(Window window)
        {
            if (_currentWindow == window)
            {
                _currentWindow.PreviewKeyDown -= OnKeyDown;
                _currentWindow = null;
            }
            _shortcuts.Clear();
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key.ToString();
            var modifiers = Keyboard.Modifiers;
            var shortcutKey = GetShortcutKey(modifiers, key);

            if (_shortcuts.TryGetValue(shortcutKey, out var action))
            {
                action?.Invoke();
                e.Handled = true;
            }
        }

        private static string GetShortcutKey(ModifierKeys modifiers, string key)
        {
            var parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) parts.Add("Shift");
            parts.Add(key);
            return string.Join("+", parts);
        }

        public static void Register(string shortcut, Action action) => _shortcuts[shortcut] = action;
        public static void Unregister(string shortcut) => _shortcuts.Remove(shortcut);
        public static void Clear() => _shortcuts.Clear();
    }
}