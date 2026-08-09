using System.Windows.Input;

namespace DriverManager.App.ViewModels;

public interface IAsyncCommand : ICommand
{
    Task ExecuteAsync(object? parameter);

    void RaiseCanExecuteChanged();
}
