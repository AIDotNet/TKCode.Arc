using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A sample composer in a window (745 wide by default, the reference's dock width) with its waits in the test's hands
/// (<see cref="Delays"/>: the check a finished upload shows) and, at full motion, its frames (<see cref="Frames"/>).
/// Records every event the composer raises.
/// </summary>
internal sealed class AiComposerRig : IDisposable
{
    private readonly IDisposable? _clock;

    public AiComposerRig(Action<AiComposer>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 745,
                         double height = 520, bool samples = true, bool useFrameClock = false)
    {
        if (useFrameClock || level == ArcMotionLevel.Full)
        {
            Frames = new ManualFrameClock();
            _clock = Frames.Install();
        }
        Composer = samples ? AiComposerSamples.Create() : new AiComposer();
        Composer.DelayAsync = Delays.Delay;
        Composer.Sent += (_, e) => Sent.Add(e.Message);
        Composer.StopRequested += (_, _) => Stops++;
        Composer.AttachmentAdded += (_, e) => Added.Add(e.Attachment);
        Composer.AttachmentRemoved += (_, e) => Removed.Add(e.Attachment);
        Composer.ToolToggled += (_, e) => Toggled.Add((e.Tool.Id, e.Tool.IsChecked));
        Composer.ModelChanged += (_, e) => ModelChanges.Add(e);
        Composer.EffortChanged += (_, e) => EffortChanges.Add(e);
        configure?.Invoke(Composer);
        // The composer sits at the bottom of a column, as in a chat, with room above it for its lists and menus.
        var column = new Panel { Children = { Composer } };
        Composer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        Window = TestHelpers.Show(column, level);
        Window.Width = width;
        Window.Height = height;
        Pump();
    }

    public AiComposer Composer { get; }
    public Window Window { get; }
    public ManualDelays Delays { get; } = new();
    public ManualFrameClock? Frames { get; }
    public List<AiComposerMessage> Sent { get; } = new();
    public int Stops { get; private set; }
    public List<AiAttachment> Added { get; } = new();
    public List<AiAttachment> Removed { get; } = new();
    public List<(string Id, bool On)> Toggled { get; } = new();
    public List<ModelPickerModelChangedEventArgs> ModelChanges { get; } = new();
    public List<ModelPickerEffortChangedEventArgs> EffortChanges { get; } = new();

    public TokenTextEditor Editor => Composer.Editor!;
    public AiSendButton Send => Composer.SendButton!;
    public AiSuggestionPopup? Suggest => Composer.Suggestions;
    public bool ListOpen => Composer.Suggestions is { IsOpen: true };
    public string? Announcement => Composer.LastAnnouncement;

    /// <summary>The focused element.</summary>
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    /// <summary>Whether the focus is in the text area.</summary>
    public bool EditorFocused => Focused is Visual v && (ReferenceEquals(v, Editor) || Editor.IsVisualAncestorOf(v));

    /// <summary>Runs the jobs and a layout.</summary>
    public void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Focuses the text area.</summary>
    public AiComposerRig Focus()
    {
        Composer.FocusInput();
        Pump();
        return this;
    }

    /// <summary>Types <paramref name="text"/> into the focused element, one input per call.</summary>
    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Pump();
    }

    /// <summary>Types character by character, as a person does (every character refreshes the query).</summary>
    public void TypeEach(string text)
    {
        foreach (var c in text)
        {
            if (c == '\n')
            {
                Key(PhysicalKey.Enter, RawInputModifiers.Shift);
                continue;
            }
            Window.KeyTextInput(c.ToString());
            Pump();
        }
    }

    /// <summary>Presses and releases a key.</summary>
    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Pump();
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    /// <summary>The centre of <paramref name="visual"/> in window coordinates.</summary>
    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

    /// <summary>A left click at <paramref name="point"/>.</summary>
    public void Click(Point point)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    public void Click(Visual visual) => Click(CenterOf(visual));

    /// <summary>
    /// Runs frames (full motion) for <paramref name="seconds"/> in 16ms steps with the posted jobs in between.
    /// </summary>
    public void Run(double seconds)
    {
        var step = TimeSpan.FromMilliseconds(16);
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += step.TotalSeconds)
        {
            Frames?.Advance(step);
            Pump();
        }
    }

    /// <summary>The first descendant of the window (popups included) of type <typeparamref name="T"/>.</summary>
    public T? Find<T>(Func<T, bool>? where = null) where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(v => where is null || where(v));

    public void Dispose()
    {
        Window.Close();
        _clock?.Dispose();
    }
}
