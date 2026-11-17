using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A chat in a window (1100 × 800 by default) with the sample models and suggestions, a scripted responder, a manual
/// frame clock and time, a fake clipboard and launcher, and every event it raises recorded.
/// </summary>
internal sealed class AiChatRig : IDisposable
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);
    private readonly IDisposable _clock;

    public AiChatRig(Action<AiChat>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1100, double height = 800,
                     bool responder = true, bool history = false)
    {
        Frames = new ManualFrameClock();
        _clock = Frames.Install();
        Responder = responder ? new ScriptedResponder() : null;
        Chat = AiChatSamples.Chat(Responder, history);
        Chat.Time = Time;
        Chat.CopyTextAsync = text =>
        {
            Copied.Add(text);
            return Task.CompletedTask;
        };
        Chat.OpenUri = uri => Opened.Add(uri);
        Chat.MessageSent += (_, e) => Sent.Add(e);
        Chat.ReplyCompleted += (_, e) => Completed.Add((e.Reply, e.Reply.Status));
        Chat.FeedbackChanged += (_, e) => Feedback.Add((e.Reply, e.Value));
        Chat.ModelChanged += (_, e) => ModelChanges.Add(e);
        Chat.ConversationOpened += (_, e) => Opened2.Add(e.Conversation);
        Chat.ConversationCreated += (_, e) => Created.Add(e.Conversation);
        Chat.NewChatRequested += (_, _) => NewChats++;
        Chat.CitationInvoked += (_, e) => Citations.Add(e.Item);
        configure?.Invoke(Chat);
        Window = TestHelpers.Show(Chat, level);
        Window.Width = width;
        Window.Height = height;
        Settle();
        // Let ArcMotion.Tracks hand out the motion pseudo-classes (after Loaded) and every first layout settle.
        Run(0.1);
    }

    public AiChat Chat { get; }
    public Window Window { get; }
    public ScriptedResponder? Responder { get; }
    public ManualFrameClock Frames { get; }
    public ManualTime Time { get; } = new();
    public List<string> Copied { get; } = new();
    public List<Uri> Opened { get; } = new();
    public List<AiChatMessageSentEventArgs> Sent { get; } = new();
    public List<(AiChatReply Reply, AiChatReplyStatus Status)> Completed { get; } = new();
    public List<(AiChatReply Reply, AiChatFeedback Value)> Feedback { get; } = new();
    public List<ModelPickerModelChangedEventArgs> ModelChanges { get; } = new();
    public List<AiChatConversation> Opened2 { get; } = new();
    public List<AiChatConversation> Created { get; } = new();
    public List<AiSource> Citations { get; } = new();
    public int NewChats { get; private set; }

    public ScriptedCall Call => Responder!.Last;
    public AiComposer Composer => Chat.Composer!;
    public TokenTextEditor Editor => Composer.Editor!;
    public string? Announcement => Chat.LastAnnouncement;
    public IReadOnlyList<AiChatTurnView> Views => Chat.TurnViews;
    public AiChatTurnView LastView => Views[^1];
    public AiChatReplyView LastReply => LastView.ReplyView!;
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    /// <summary>Runs the jobs and a layout pass.</summary>
    public void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs 60 Hz frames for <paramref name="seconds"/> with the jobs and a layout between them.</summary>
    public void Run(double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds - 1e-9; elapsed += Frame.TotalSeconds)
        {
            Frames.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }
        Settle();
    }

    /// <summary>Runs frames until <paramref name="done"/> or <paramref name="seconds"/> pass; returns whether it got there.</summary>
    public bool RunUntil(Func<bool> done, double seconds = 5)
    {
        for (var elapsed = 0.0; elapsed < seconds && !done(); elapsed += Frame.TotalSeconds)
        {
            Frames.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }
        Settle();
        return done();
    }

    /// <summary>Gives continuations that left the UI thread (the thread pool) time to come back, until <paramref name="done"/>.</summary>
    public bool Drain(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            Thread.Sleep(5);
            Settle();
        }
        return done();
    }

    /// <summary>Types <paramref name="text"/> into the message box and presses Enter.</summary>
    public void Send(string text)
    {
        Composer.FocusInput();
        Settle();
        Window.KeyTextInput(text);
        Settle();
        Key(PhysicalKey.Enter);
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Settle();
        Window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

    public void Hover(Visual visual)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Window.MouseMove(CenterOf(visual), RawInputModifiers.None);
        Settle();
    }

    public void Click(Visual visual)
    {
        var point = CenterOf(visual);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Settle();
    }

    /// <summary>The first descendant of the window (popups included) of type <typeparamref name="T"/>.</summary>
    public T? Find<T>(Func<T, bool>? where = null) where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().FirstOrDefault(v => where is null || where(v));

    public IEnumerable<T> FindAll<T>() where T : Visual => Window.GetVisualDescendants().OfType<T>();

    /// <summary>Whether <paramref name="visual"/> or something inside it has the focus.</summary>
    public bool HasFocus(Visual visual) => Focused is Visual focused && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));

    public void Dispose()
    {
        Window.Close();
        _clock.Dispose();
    }
}
