using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Logs every accept, dismiss and status change, and drives the field through its methods.</summary>
public partial class GhostTextEventsDemo : UserControl
{
    private int _shown;
    private int _accepted;

    public GhostTextEventsDemo()
    {
        InitializeComponent();
        Field.Suggest = GhostTextDemoModel.Create();
        Field.StatusChanged += (_, e) =>
        {
            if (e.NewStatus == GhostTextStatus.Ready)
            {
                _shown++;
            }
            Write($"Status: {e.NewStatus}");
        };
        Field.Accepted += (_, e) =>
        {
            if (e.Mode == GhostTextAcceptMode.All)
            {
                _accepted++;
            }
            Write($"Accepted ({e.Mode}): \"{e.AcceptedText.Trim()}\"");
        };
        Field.Dismissed += (_, e) => Write($"Dismissed: \"{e.Suggestion.Trim()}\"");
        Write("Press Ask now, or click the field and type.");
    }

    private void Write(string line)
    {
        var rate = _shown == 0 ? 0 : _accepted * 100 / _shown;
        Log.Text = $"{line}\nShown {_shown}, accepted {_accepted} ({rate}%)";
    }

    private void OnAsk(object? sender, RoutedEventArgs e)
    {
        Field.FocusField();
        Field.Request();
    }

    private void OnWord(object? sender, RoutedEventArgs e) => Field.AcceptWord();

    private void OnAll(object? sender, RoutedEventArgs e) => Field.AcceptAll();

    private void OnDismiss(object? sender, RoutedEventArgs e) => Field.Dismiss();
}
