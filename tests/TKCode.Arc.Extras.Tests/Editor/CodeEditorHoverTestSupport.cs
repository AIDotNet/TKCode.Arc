using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

/// <summary>A virtual clock for the timers of the hover, parameter hints and marker features in control tests.</summary>
internal sealed class ManualTimers
{
    private readonly List<(double Due, long Seq, Action Callback, Handle Handle)> _timers = [];
    private long _seq;

    public ManualTimers() =>
        Scheduler = new IdleScheduler(
            runWhenIdle: callback => Add(0, callback),
            clock: () => Now,
            runAfter: (delay, callback) => Add(delay.TotalMilliseconds, callback));

    public double Now { get; private set; }

    public IdleScheduler Scheduler { get; }

    private IDisposable Add(double ms, Action callback)
    {
        var handle = new Handle(this);
        _timers.Add((Now + Math.Max(0, ms), _seq++, callback, handle));
        return handle;
    }

    /// <summary>Moves the clock, running the timers that fall due and the dispatcher's jobs after each.</summary>
    public void Advance(double ms)
    {
        var end = Now + ms;
        Dispatcher.UIThread.RunJobs();
        while (true)
        {
            var next = _timers.Where(t => t.Due <= end).OrderBy(t => t.Due).ThenBy(t => t.Seq).FirstOrDefault();
            if (next.Callback is null)
            {
                break;
            }

            _timers.Remove(next);
            Now = Math.Max(Now, next.Due);
            next.Callback();
            Dispatcher.UIThread.RunJobs();
        }

        Now = end;
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Handle(ManualTimers owner) : IDisposable
    {
        public void Dispose() => owner._timers.RemoveAll(t => ReferenceEquals(t.Handle, this));
    }
}

internal static class HoverTestSupport
{
    public static readonly Avalonia.Input.RawInputModifiers CtrlCmd =
        OperatingSystem.IsMacOS() ? Avalonia.Input.RawInputModifiers.Meta : Avalonia.Input.RawInputModifiers.Control;

    public static void Render(Window window)
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The middle of the character at (line, column) in window coordinates.</summary>
    public static Point At(Window window, CodeEditorView view, int line, int column)
    {
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var x = origin.X + view.GutterWidth + (view.CharWidth * (column - 1)) + 1 - view.ScrollLeft;
        var y = origin.Y + (view.LineHeight * (line - 0.5)) - view.ScrollTop;
        return new Point(x, y);
    }

    public static string TextOf(Control control) => string.Join("\n", control.GetVisualDescendantsAndSelf().OfType<TextBlock>()
        .Where(t => t.IsEffectivelyVisible)
        .Select(t => t.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : t.Text)
        .Where(s => !string.IsNullOrEmpty(s)));

    private static IEnumerable<Visual> GetVisualDescendantsAndSelf(this Visual visual)
    {
        yield return visual;
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(visual))
        {
            foreach (var descendant in child.GetVisualDescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    internal sealed class HoverProvider(Func<TextModel, TextPosition, HoverContext?, Hover?> provide) : IHoverProvider
    {
        public int Calls { get; private set; }

        public Task<Hover?> ProvideHover(TextModel model, TextPosition position, CancellationToken token, HoverContext? context = null)
        {
            Calls++;
            return Task.FromResult(provide(model, position, context));
        }
    }

    internal sealed class SignatureProvider(IReadOnlyList<string> triggers, Func<SignatureHelpContext, SignatureHelpResult?> provide) : ISignatureHelpProvider
    {
        public List<SignatureHelpContext> Contexts { get; } = [];

        public IReadOnlyList<string>? SignatureHelpTriggerCharacters => triggers;

        public IReadOnlyList<string>? SignatureHelpRetriggerCharacters => [","];

        public Task<SignatureHelpResult?> ProvideSignatureHelp(TextModel model, TextPosition position, CancellationToken token, SignatureHelpContext context)
        {
            Contexts.Add(context);
            return Task.FromResult(provide(context));
        }
    }
}
