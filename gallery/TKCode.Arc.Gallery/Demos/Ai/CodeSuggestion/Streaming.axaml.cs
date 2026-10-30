using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Streams the demo suggestion through the public API the way an app would while tokens arrive: Thinking, then each hunk
/// appended with no lines and its lines appended one by one, then Ready.
/// </summary>
public partial class CodeSuggestionStreamingDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private CodeSuggestionHunk[] _script = [];
    private int _hunk;
    private int _line;
    private int _wait;

    public CodeSuggestionStreamingDemo()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        AttachedToVisualTree += (_, _) => Run();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Run()
    {
        _timer.Stop();
        _script = CodeSuggestionDemoData.Hunks();
        Card.FileName = CodeSuggestionDemoData.FileName;
        Card.FilePath = CodeSuggestionDemoData.FilePath;
        Card.Instruction = CodeSuggestionDemoData.Instruction;
        Card.Model = CodeSuggestionDemoData.Model;
        Card.Hunks.Clear();
        Card.Code = CodeSuggestionDemoData.Code;
        Card.Status = CodeSuggestionStatus.Thinking;
        Log.Text = "Waiting for the model…";
        _hunk = 0;
        _line = 0;
        _wait = 12;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_wait-- > 0)
        {
            return;
        }
        if (Card.Status == CodeSuggestionStatus.Thinking)
        {
            Card.Status = CodeSuggestionStatus.Streaming;
        }
        if (_hunk >= _script.Length)
        {
            _timer.Stop();
            Card.Status = CodeSuggestionStatus.Ready;
            return;
        }
        var source = _script[_hunk];
        if (_line == 0 && (Card.Hunks.Count == 0 || Card.Hunks[^1].Id != source.Id))
        {
            // A new hunk arrives with no lines yet; its removed lines tint while the first line is on its way.
            Card.Hunks.Add(new CodeSuggestionHunk(source.Id, source.Start, source.Remove) { Summary = source.Summary });
            _wait = 3;
            return;
        }
        if (_line < source.Insert.Count)
        {
            Card.Hunks[^1].AppendLine(source.Insert[_line++]);
            return;
        }
        _hunk++;
        _line = 0;
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        // Abort the request, drop a hunk that had not finished, and let the person review the rest.
        _timer.Stop();
        if (Card.Hunks.Count > 0 && _hunk < _script.Length && Card.Hunks[^1].Insert.Count < _script[_hunk].Insert.Count)
        {
            Card.Hunks.RemoveAt(Card.Hunks.Count - 1);
        }
        Card.Status = CodeSuggestionStatus.Ready;
    }

    private void OnRegenerate(object? sender, RoutedEventArgs e) => Run();

    private void OnDecision(object? sender, CodeSuggestionDecisionEventArgs e)
    {
        var verb = e.Decision == CodeSuggestionDecision.Accepted ? "Accepted" : "Rejected";
        Log.Text = $"{verb} \"{e.Hunk.Summary}\". The file now has {e.Code.Split('\n').Length} lines.";
    }

    private void OnResolved(object? sender, CodeSuggestionResolvedEventArgs e) =>
        Log.Text = $"Done: {e.Accepted.Count} accepted, {e.Rejected.Count} rejected. A real app writes {CodeSuggestionDemoData.FileName} here.";
}
