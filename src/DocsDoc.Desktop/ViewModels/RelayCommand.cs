using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DocsDoc.Desktop.ViewModels
{
    /// <summary>
    /// Shared RelayCommand for async command binding in ViewModels.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Predicate<object?> _canExecute;
        private readonly Func<object?, Task> _execute;
        
        public RelayCommand(Func<object?, Task> execute, Predicate<object?> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        
        public async void Execute(object? parameter) 
        {
            if (CanExecute(parameter))
            {
                await _execute(parameter);
            }
        }
        
        public event EventHandler? CanExecuteChanged;
        
        /// <summary>
        /// Manually raise the CanExecuteChanged event to update UI command state.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
} 