using SupermarketPOS.UI.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SupermarketPOS.UI
{
    public class BaseWindow : Window
    {
        private bool _isDirty;

        public BaseWindow()
        {
            FlowDirection = FlowDirection.RightToLeft;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#121212"));
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
            Loaded += (s, e) => ShortcutService.RegisterWindow(this);
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        protected void RegisterFormShortcuts()
        {
        }

        protected void RegisterFormShortcuts(Action saveAction, Action cancelAction)
        {
        }

        protected void RegisterListShortcuts()
        {
        }

        protected void RegisterListShortcuts(Action addAction, Action editAction, Action deleteAction, Action searchAction, Action refreshAction, Action printAction)
        {
        }

        protected void RegisterReportShortcuts()
        {
        }

        protected void RegisterReportShortcuts(Action refreshAction, Action exportAction, Action printAction)
        {
        }

        protected void RegisterTransactionShortcuts()
        {
        }

        protected void RegisterTransactionShortcuts(Action saveAction, Action cancelAction, Action printAction)
        {
        }

        protected void MarkAsClean()
        {
            _isDirty = false;
        }

        protected void MarkAsDirty()
        {
            _isDirty = true;
        }

        protected bool ConfirmDelete(string message = "هل تريد الحذف؟")
        {
            return MessageBox.Show(message, "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("يوجد تغييرات غير محفوظة. هل تريد الإغلاق؟", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }
    }
}
