using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A clock the extraction tests move by hand.</summary>
internal sealed class ExtractionTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan time) => Now += time;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// The waits of a <see cref="ExtractionFixtures.ScriptedExtractor"/> held until the test releases them: the first wait is
/// the reading time, every later one comes before an event.
/// </summary>
internal sealed class ExtractionGate
{
    private readonly Queue<TaskCompletionSource> _waiting = new();

    /// <summary>How many waits are pending.</summary>
    public int Waiting => _waiting.Count;

    /// <summary>Whether a wait saw its token cancelled.</summary>
    public bool SawCancel { get; private set; }

    public Task Wait(TimeSpan time, CancellationToken token)
    {
        var wait = new TaskCompletionSource();
        token.Register(() =>
        {
            SawCancel = true;
            wait.TrySetCanceled(token);
        });
        _waiting.Enqueue(wait);
        return wait.Task;
    }

    /// <summary>Lets the oldest wait go.</summary>
    public bool Release() => _waiting.TryDequeue(out var wait) && wait.TrySetResult();
}

/// <summary>
/// A window with a document extraction of the invoice and the email samples, a scripted extractor whose waits the test
/// releases one by one (or none, <paramref name="instant"/>), the producer run inline on the UI thread, and hand-moved
/// clocks for time and frames.
/// </summary>
internal sealed class DocumentExtractionRig : IDisposable
{
    public static readonly TimeSpan FrameStep = TimeSpan.FromMilliseconds(1000.0 / 60);

    private readonly IDisposable _frames;

    public DocumentExtractionRig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 1080, double height = 1000, bool instant = false,
        Action<DocumentExtraction>? configure = null, IReadOnlyList<ExtractionEvent>? invoiceEvents = null)
    {
        _frames = Frames.Install();
        Extractor = new ExtractionFixtures.ScriptedExtractor(invoiceEvents ?? ExtractionFixtures.InvoiceEvents(),
            instant ? (_, _) => Task.CompletedTask : Gate.Wait);
        EmailExtractor = new ExtractionFixtures.ScriptedExtractor(ExtractionFixtures.EmailEvents(),
            instant ? (_, _) => Task.CompletedTask : Gate.Wait);
        Invoice = new ExtractionSample { Document = ExtractionFixtures.Invoice(), Schema = ExtractionFixtures.InvoiceSchema() };
        Email = new ExtractionSample { Document = ExtractionFixtures.Email(), Schema = ExtractionFixtures.EmailSchema() };
        Extraction = new DocumentExtraction
        {
            Extractor = Extractor,
            Model = new ExtractionModelInfo { Name = "Lumen Extract S", Id = "lumen-extract-s", InputPricePerMillion = 0.12m, OutputPricePerMillion = 0.60m },
            Time = Time,
            StartProducer = work => work()
        };
        Extraction.Samples.Add(Invoice);
        Extraction.Samples.Add(Email);
        configure?.Invoke(Extraction);
        Host = new Border { Width = width, Child = Extraction, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        Window = TestHelpers.Show(Host, level);
        Window.Width = width + 40;
        Window.Height = height;
        Pump();
        // Let ArcMotion.Tracks hand out the motion pseudo-classes (after Loaded), then start from rest.
        Frame(30);
    }

    public DocumentExtraction Extraction { get; }

    public Border Host { get; }

    public Window Window { get; }

    public ExtractionFixtures.ScriptedExtractor Extractor { get; }

    public ExtractionFixtures.ScriptedExtractor EmailExtractor { get; }

    public ExtractionSample Invoice { get; }

    public ExtractionSample Email { get; }

    public ExtractionGate Gate { get; } = new();

    public ExtractionTestClock Time { get; } = new();

    public ManualFrameClock Frames { get; } = new();

    public ExtractionStore Store => Extraction.Store!;

    /// <summary>Runs layout, render ticks and posted jobs a few times.</summary>
    public void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        Window.UpdateLayout();
    }

    /// <summary>Advances both clocks by <paramref name="count"/> 60 Hz frames, running posted jobs between them.</summary>
    public void Frame(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            Time.Advance(FrameStep);
            Frames.Advance(FrameStep, FrameStep);
            Dispatcher.UIThread.RunJobs();
        }
        Window.UpdateLayout();
    }

    /// <summary>Advances both clocks by about <paramref name="time"/>.</summary>
    public void Advance(TimeSpan time) => Frame((int)Math.Ceiling(time / FrameStep));

    /// <summary>Starts a run (Extract) and lets the extractor begin.</summary>
    public Task Start()
    {
        var task = Extraction.StartAsync();
        Pump();
        return task;
    }

    /// <summary>Releases the reading wait and then <paramref name="events"/> more, pumping after each.</summary>
    public void Step(int events = 1)
    {
        for (var i = 0; i < events; i++)
        {
            Gate.Release();
            Pump(2);
        }
    }

    /// <summary>Releases every wait until the run ends (at most 200 steps).</summary>
    public void RunToEnd()
    {
        for (var i = 0; i < 200 && Extraction.State is ExtractionState.Reading or ExtractionState.Extracting; i++)
        {
            if (!Gate.Release())
            {
                Pump(1);
            }
            Pump(2);
        }
        Pump();
    }

    /// <summary>A whole run: start and release everything.</summary>
    public void FullRun()
    {
        _ = Start();
        RunToEnd();
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

    public void Click(Visual visual) => Click(CenterOf(visual));

    public void Click(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump(1);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    public void Move(Visual visual) => Move(CenterOf(visual));

    public void Move(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Pump();
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public void Type(string text)
    {
        foreach (var c in text)
        {
            Window.KeyTextInput(c.ToString());
        }
        Pump();
    }

    /// <summary>Selects all of the focused box's text and types <paramref name="text"/> over it.</summary>
    public void Retype(string text)
    {
        if (Focused is TextBox box)
        {
            box.SelectAll();
        }
        Type(text);
    }

    /// <summary>The focused element, if any.</summary>
    public IInputElement? Focused => TopLevel.GetTopLevel(Window)?.FocusManager?.GetFocusedElement();

    /// <summary>Whether the focus is on <paramref name="visual"/> or inside it.</summary>
    public bool FocusIsIn(Visual? visual) =>
        visual is not null && Focused is Visual focused && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));

    /// <summary>The row of a scalar field.</summary>
    public ExtractionFieldRow Row(string key) => Extraction.RowOf(key)!;

    public void Dispose()
    {
        Window.Close();
        _frames.Dispose();
    }
}
