using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The quiet output panel at the bottom of Prompt studio's card (SurfaceMuted, a top border, bottom corners 33). Idle it
/// is one muted line (<see cref="IdleText"/>, with <see cref="CaseText"/> in it in the secondary text color). A run
/// shows a 36px head (the status: a glyph and <see cref="StatusText"/>, then "· <see cref="CaseText"/>"; Thinking and
/// Writing shimmer, a reply gets a green check, a stop a filled square, a failure a red alert circle), the reply as
/// paragraphs, an error box with Retry when the run failed, and a footer line in tabular figures
/// (<see cref="FooterText"/>). Copy reply appears only once the reply is done or stopped.
/// </summary>
/// <remarks>
/// <para>
/// The owner drives it: <see cref="Reset"/> when a run starts, <see cref="State"/> and the words as the run moves on,
/// <see cref="Push"/> for streamed text, <see cref="Complete"/> when the text is all there (the rest is still shown at
/// the reading pace; <see cref="RevealCompleted"/> says when it is all on screen, which is when Arc says "Reply"),
/// <see cref="Stop"/> to keep only what is shown. Setting <see cref="Text"/> shows a whole reply at once.
/// </para>
/// <para>
/// Pacing (Arc 9.4): whole words on Core's <see cref="WordPacer"/> clock (3–7 words about every 50 ms), driven by a
/// <see cref="LiveLoop"/> that runs at every motion level; at full motion each batch fades in over 150 ms without moving
/// what is shown (Reduced and Off: same cadence, no fade).
/// </para>
/// <para>
/// Height: the panel follows its content on the smooth spring. A run started over an earlier reply keeps the old height
/// (the new content is clipped) until the new content fills it or the run ends, so a rerun does not collapse and grow
/// again. The status, footer and reply swap in Arc's label mode / crossfade. Reduced motion jumps the height and keeps
/// 120 ms crossfades; Off jumps everything.
/// </para>
/// <para>
/// Focus: after Retry the error box goes away; the owner moves the focus (Arc's improvement: to the Run / Stop button)
/// in <see cref="RetryRequested"/>.
/// </para>
/// </remarks>
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(IdlePart, typeof(Control))]
[TemplatePart(IdleTextPart, typeof(TextBlock))]
[TemplatePart(RunPart, typeof(Control))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(ReplyPart, typeof(ArcContentSwap))]
[TemplatePart(ErrorPart, typeof(Control))]
[TemplatePart(RetryPart, typeof(Button))]
[TemplatePart(FooterPart, typeof(ArcContentSwap))]
[PseudoClasses(IdlePseudoClass, ThinkingPseudoClass, StreamingPseudoClass, DonePseudoClass, StoppedPseudoClass, FailedPseudoClass,
    CanCopyPseudoClass, HasFooterPseudoClass, RevealingPseudoClass)]
internal partial class PromptRunPanel : TemplatedControl
{
    public const string SizePart = "PART_Size";
    public const string IdlePart = "PART_Idle";
    public const string IdleTextPart = "PART_IdleText";
    public const string RunPart = "PART_Run";
    public const string StatusPart = "PART_Status";
    public const string CopyPart = "PART_Copy";
    public const string ReplyPart = "PART_Reply";
    public const string ErrorPart = "PART_Error";
    public const string RetryPart = "PART_Retry";
    public const string FooterPart = "PART_Footer";

    public const string IdlePseudoClass = ":idle";
    public const string ThinkingPseudoClass = ":thinking";
    public const string StreamingPseudoClass = ":streaming";
    public const string DonePseudoClass = ":done";
    public const string StoppedPseudoClass = ":stopped";
    public const string FailedPseudoClass = ":failed";
    public const string CanCopyPseudoClass = ":can-copy";
    public const string HasFooterPseudoClass = ":has-footer";
    public const string RevealingPseudoClass = ":revealing";

    #region Properties

    /// <summary>Where the run is: Idle shows the idle line, the others the run view.</summary>
    public static readonly StyledProperty<PromptRunState> StateProperty =
        AvaloniaProperty.Register<PromptRunPanel, PromptRunState>(nameof(State));

    /// <summary>The idle line ("Run Broken spar to see the reply here. Runs are simulated.").</summary>
    public static readonly StyledProperty<string?> IdleTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(IdleText));

    /// <summary>The status word of the run ("Thinking", "Writing", "Reply", "Stopped", "Run failed").</summary>
    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(StatusText));

    /// <summary>The test case's label: after the status ("· Broken spar") and picked out in the idle line.</summary>
    public static readonly StyledProperty<string?> CaseTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(CaseText));

    /// <summary>The reply shown (two-way: the panel writes what it has revealed; setting it shows a whole reply at once).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string>(nameof(Text), string.Empty);

    /// <summary>The footer line ("Simulated · 1.57 s · 245 tokens · $0.0014"); null or empty hides it.</summary>
    public static readonly StyledProperty<string?> FooterTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(FooterText));

    /// <summary>The message of a failed run, shown in the error box.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(ErrorText));

    /// <summary>The label of the Retry button.</summary>
    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(RetryText));

    /// <summary>The name of the copy button ("Copy reply").</summary>
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(CopyText));

    /// <summary>The copy button's name after a copy ("Copied").</summary>
    public static readonly StyledProperty<string?> CopiedTextProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(CopiedText));

    /// <summary>The accessible name of the panel, or null.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PromptRunPanel, string?>(nameof(AccessibleName));

    /// <summary>True while streamed text is still being shown (read-only).</summary>
    public static readonly DirectProperty<PromptRunPanel, bool> IsRevealingProperty =
        AvaloniaProperty.RegisterDirect<PromptRunPanel, bool>(nameof(IsRevealing), o => o.IsRevealing);

    /// <summary>Raised when Retry is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<PromptRunPanel, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after Copy reply put the reply on the clipboard (the button copies it itself; the owner announces it).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CopyRequestedEvent =
        RoutedEvent.Register<PromptRunPanel, RoutedEventArgs>(nameof(CopyRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when everything after <see cref="Complete"/> has been shown (not after <see cref="Stop"/> or a set <see cref="Text"/>).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RevealCompletedEvent =
        RoutedEvent.Register<PromptRunPanel, RoutedEventArgs>(nameof(RevealCompleted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="StateProperty"/>
    public PromptRunState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="IdleTextProperty"/>
    public string? IdleText { get => GetValue(IdleTextProperty); set => SetValue(IdleTextProperty, value); }

    /// <inheritdoc cref="StatusTextProperty"/>
    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    /// <inheritdoc cref="CaseTextProperty"/>
    public string? CaseText { get => GetValue(CaseTextProperty); set => SetValue(CaseTextProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="FooterTextProperty"/>
    public string? FooterText { get => GetValue(FooterTextProperty); set => SetValue(FooterTextProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="RetryTextProperty"/>
    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    /// <inheritdoc cref="CopiedTextProperty"/>
    public string? CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="IsRevealingProperty"/>
    public bool IsRevealing
    {
        get => _isRevealing;
        private set
        {
            if (SetAndRaise(IsRevealingProperty, ref _isRevealing, value))
            {
                PseudoClasses.Set(RevealingPseudoClass, value);
            }
        }
    }

    /// <summary>Raised when Retry is pressed.</summary>
    public event EventHandler<RoutedEventArgs>? RetryRequested { add => AddHandler(RetryRequestedEvent, value); remove => RemoveHandler(RetryRequestedEvent, value); }

    /// <summary>Raised after the reply was copied.</summary>
    public event EventHandler<RoutedEventArgs>? CopyRequested { add => AddHandler(CopyRequestedEvent, value); remove => RemoveHandler(CopyRequestedEvent, value); }

    /// <summary>Raised when the completed reply is all on screen.</summary>
    public event EventHandler<RoutedEventArgs>? RevealCompleted { add => AddHandler(RevealCompletedEvent, value); remove => RemoveHandler(RevealCompletedEvent, value); }

    #endregion

    private readonly LiveLoop _loop;
    private WordPacer _pacer = new();
    private TimeSpan _clock;
    private bool _completed;
    private bool _isRevealing;
    private int _writingText;
    private PromptRunReply _reply = new();
    private SpringSizeDecorator? _size;
    private Control? _idle;
    private TextBlock? _idleText;
    private Control? _run;
    private ArcContentSwap? _status;
    private ArcContentSwap? _replySwap;
    private ArcContentSwap? _footer;
    private CopyButton? _copy;
    private Button? _retry;
    private Control? _error;
    private readonly Run _idleBefore = new();
    private readonly Run _idleCase = new();
    private readonly Run _idleAfter = new();
    private (PromptRunState State, string? Status, string? Case, bool Full)? _statusKey;
    private string? _footerShown;
    private bool _holding;
    private bool _holdHooked;

    static PromptRunPanel()
    {
        FocusableProperty.OverrideDefaultValue<PromptRunPanel>(false);
    }

    public PromptRunPanel()
    {
        // Text keeps its cadence at every motion level (only the fades go) and while scrolled away.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        ArcTokens.Bind(_idleCase, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        PseudoClasses.Set(IdlePseudoClass, true);
    }

    #region Internals for tests

    /// <summary>The reply view of the current run.</summary>
    internal PromptRunReply Reply => _reply;

    /// <summary>The pacer of the current run.</summary>
    internal WordPacer Pacer => _pacer;

    /// <summary>The height spring (template part).</summary>
    internal SpringSizeDecorator? Sizer => _size;

    /// <summary>The copy button (template part).</summary>
    internal CopyButton? CopyButton => _copy;

    /// <summary>The Retry button (template part).</summary>
    internal Button? RetryButton => _retry;

    /// <summary>The status swap (template part); its content is the status row.</summary>
    internal ArcContentSwap? StatusSwap => _status;

    /// <summary>The footer swap (template part); its content is the footer line.</summary>
    internal ArcContentSwap? FooterSwap => _footer;

    /// <summary>The idle line (template part).</summary>
    internal TextBlock? IdleTextBlock => _idleText;

    /// <summary>Whether the panel keeps its height for a rerun.</summary>
    internal bool IsHolding => _holding;

    #endregion

    #region Public API

    /// <summary>
    /// Starts a new run: drops the pacer's text, fades the previous reply out and, when there was one, keeps the panel's
    /// height until the new content fills it or the run ends.
    /// </summary>
    public void Reset()
    {
        _loop.Stop();
        _pacer = new WordPacer();
        _completed = false;
        IsRevealing = false;
        // An earlier run left something on screen (a reply, an error box): its height is kept for the new run.
        var hadContent = _reply.HasText || (_size is { Bounds.Height: > 0 } && State is not PromptRunState.Idle);
        if (_reply.HasText)
        {
            _reply = new PromptRunReply();
            if (_replySwap is not null)
            {
                _replySwap.Content = _reply;
            }
        }
        WriteText(string.Empty);
        if (hadContent)
        {
            Hold();
        }
    }

    /// <summary>Adds streamed text of any size (UI thread); ignored after <see cref="Complete"/> or <see cref="Stop"/>.</summary>
    public void Push(string delta)
    {
        if (string.IsNullOrEmpty(delta) || _completed)
        {
            return;
        }
        var text = PromptRunReply.Normalize(delta);
        if (text.Length == 0)
        {
            return;
        }
        IsRevealing = true;
        _pacer.Push(text);
        _loop.Start();
    }

    /// <summary>No more text: the rest is shown at the reading pace (within 600 ms), then <see cref="RevealCompleted"/> is raised.</summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }
        _completed = true;
        _pacer.Complete();
        IsRevealing = true;
        _loop.Start();
    }

    /// <summary>Keeps what is shown, drops the text not shown yet and ends the stream (no <see cref="RevealCompleted"/>).</summary>
    public void Stop()
    {
        _loop.Stop();
        _pacer.Reset();
        _completed = true;
        IsRevealing = false;
        Release();
    }

    #endregion

    #region Pacing

    private bool Step(double dt)
    {
        _clock += TimeSpan.FromSeconds(dt);
        if (_pacer.Tick(_clock) is { } batch)
        {
            _reply.Append(batch, fade: ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full);
            WriteText(_reply.Text);
            UpdateCopy();
            return true;
        }
        if (_completed && _pacer.IsFinished)
        {
            IsRevealing = false;
            RaiseEvent(new RoutedEventArgs(RevealCompletedEvent, this));
            return false;
        }
        // Nothing can be shown until more text arrives: sleep until the next Push.
        return _pacer.PendingWords > 0;
    }

    /// <summary>Writes <see cref="Text"/> from the reveal (not a request to show a whole reply).</summary>
    private void WriteText(string text)
    {
        _writingText++;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _writingText--;
        }
    }

    /// <summary>The owner set <see cref="Text"/>: show it whole, at once.</summary>
    private void ShowWholeText(string text)
    {
        _loop.Stop();
        _pacer.Reset();
        _completed = true;
        IsRevealing = false;
        _reply.SetText(text);
        Release();
        UpdateCopy();
    }

    #endregion

    #region Height hold

    private void Hold()
    {
        if (_size is null)
        {
            return;
        }
        _holding = true;
        _size.IsHeld = true;
        if (!_holdHooked)
        {
            _holdHooked = true;
            _size.LayoutUpdated += OnHeldLayout;
        }
    }

    private void Release()
    {
        _holding = false;
        if (_holdHooked && _size is not null)
        {
            _size.LayoutUpdated -= OnHeldLayout;
        }
        _holdHooked = false;
        if (_size is not null)
        {
            _size.IsHeld = false;
        }
    }

    /// <summary>While held, the new content taking as much room as the held height ends the hold.</summary>
    private void OnHeldLayout(object? sender, EventArgs e)
    {
        if (_holding && _size?.Child is { } child && child.DesiredSize.Height >= _size.Bounds.Height - 0.5)
        {
            Release();
        }
    }

    #endregion
}
