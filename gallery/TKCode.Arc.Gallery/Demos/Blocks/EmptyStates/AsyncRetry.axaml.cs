using System.Windows.Input;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmptyStatesAsyncRetryDemo : UserControl
{
    private const string Saved = "Your changes are saved here and upload when the connection is back.";
    private int _attempts;

    public EmptyStatesAsyncRetryDemo()
    {
        InitializeComponent();
        States.ActionCommand = new AsyncCommand(RunAsync);
    }

    private async Task RunAsync(EmptyStatesActionEventArgs e)
    {
        var connection = e.Item;
        if (e.Phase == EmptyStatesPhase.Done)
        {
            // "Disconnect": back to the empty state for another round.
            _attempts = 0;
            connection.Description = Saved;
            States.Phase = EmptyStatesPhase.Idle;
            return;
        }
        States.Phase = EmptyStatesPhase.Loading;
        await Task.Delay(1500);
        if (++_attempts == 1)
        {
            // The first attempt fails. Change the copy while it still shows the loading line, then go back to Idle.
            connection.Description = "Still no connection. Check your network and try again.";
            States.Phase = EmptyStatesPhase.Idle;
            return;
        }
        States.Phase = EmptyStatesPhase.Done;
    }

    /// <summary>A command that runs one task at a time.</summary>
    private sealed class AsyncCommand(Func<EmptyStatesActionEventArgs, Task> run) : ICommand
    {
        private bool _running;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_running && parameter is EmptyStatesActionEventArgs;

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }
            _running = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await run((EmptyStatesActionEventArgs)parameter!);
            }
            finally
            {
                _running = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
