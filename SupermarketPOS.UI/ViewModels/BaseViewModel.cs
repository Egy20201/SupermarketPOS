using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SupermarketPOS.UI.ViewModels
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        private readonly Action<object> _executeWithParam;
        private readonly Func<bool> _canExecute;
        private readonly Func<object, bool> _canExecuteWithParam;

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _executeWithParam = execute;
            _canExecuteWithParam = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? _canExecuteWithParam?.Invoke(parameter) ?? true;

        public void Execute(object parameter)
        {
            if (_execute != null) _execute();
            else _executeWithParam?.Invoke(parameter);
        }
    }

    public class RelayCommand<T> : System.Windows.Input.ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke((T)parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}
