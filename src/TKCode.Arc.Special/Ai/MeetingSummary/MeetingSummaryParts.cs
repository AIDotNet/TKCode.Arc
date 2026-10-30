using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A transcript timestamp ("2:14") at the end of a note line: a small mono button that shows the line's quote on keyboard
/// focus and pins or closes it on Enter, Space or a tap. It reports expanded while its quote is open (Arc's
/// <c>aria-expanded</c>).
/// </summary>
internal sealed class MeetingSummaryStamp : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(MeetingSummaryStamp);
}

/// <summary>
/// One line of the notes: a Summary or Decisions point (<see cref="MeetingSummaryLineView"/>) or an action item
/// (<see cref="MeetingSummaryActionRow"/>). It holds the streaming text and the timestamp, and is highlighted while its
/// quote is open. Owner, date and send controls keep their space while the text streams and fade in once it is whole.
/// </summary>
[TemplatePart(TextPart, typeof(MeetingSummaryLineText))]
[TemplatePart(StampPart, typeof(MeetingSummaryStamp))]
[PseudoClasses(QuotedPseudoClass, StreamingPseudoClass)]
internal abstract class MeetingSummaryRow : TemplatedControl
{
    internal const string TextPart = "PART_Text";
    internal const string StampPart = "PART_Stamp";
    internal const string QuotedPseudoClass = ":quoted";
    internal const string StreamingPseudoClass = ":streaming";

    private string _pendingText = string.Empty;
    private bool _quoted;
    private bool _streaming;

    protected MeetingSummaryRow(MeetingSummaryPoint point)
    {
        Point = point;
    }

    /// <summary>The point this row shows.</summary>
    public MeetingSummaryPoint Point { get; private set; }

    /// <summary>The streaming text (after the template is applied).</summary>
    public MeetingSummaryLineText? TextView { get; private set; }

    /// <summary>The timestamp button (after the template is applied).</summary>
    public MeetingSummaryStamp? Stamp { get; private set; }

    /// <summary>The text shown now.</summary>
    public string ShownText => TextView?.Text ?? _pendingText;

    /// <summary>Whether its quote is open.</summary>
    public bool IsQuoted
    {
        get => _quoted;
        set
        {
            _quoted = value;
            PseudoClasses.Set(QuotedPseudoClass, value);
            if (Stamp is not null)
            {
                Stamp.IsExpanded = value;
            }
        }
    }

    /// <summary>Whether its text is still streaming (meta and buttons wait, keeping their space).</summary>
    public bool IsStreaming
    {
        get => _streaming;
        set
        {
            _streaming = value;
            PseudoClasses.Set(StreamingPseudoClass, value);
            if (Stamp is not null)
            {
                Stamp.IsHitTestVisible = !value;
                Stamp.IsTabStop = !value && Point.Quote is not null;
            }
            OnStreamingChanged(value);
        }
    }

    /// <summary>Replaces the point (a new snapshot of the notes) and its static parts.</summary>
    public virtual void SetPoint(MeetingSummaryPoint point, MeetingSummaryLabels labels)
    {
        Point = point;
        UpdateStamp(labels);
    }

    /// <summary>Shows <paramref name="text"/> at once.</summary>
    public void SetText(string text)
    {
        _pendingText = text;
        TextView?.SetText(text);
    }

    /// <summary>Adds a streamed chunk.</summary>
    public void Append(string chunk, bool fade)
    {
        _pendingText += chunk;
        TextView?.Append(chunk, fade);
    }

    protected virtual void OnStreamingChanged(bool streaming)
    {
    }

    protected void UpdateStamp(MeetingSummaryLabels labels)
    {
        if (Stamp is null)
        {
            return;
        }
        var quote = Point.Quote;
        Stamp.IsVisible = quote is not null;
        if (quote is not null)
        {
            var time = MeetingSummaryFormat.Timestamp(quote.At);
            Stamp.Content = time;
            AutomationProperties.SetName(Stamp, labels.ShowQuote(time));
            AutomationProperties.SetHelpText(Stamp, quote.Text);
        }
        Stamp.IsTabStop = !_streaming && quote is not null;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        TextView = e.NameScope.Find<MeetingSummaryLineText>(TextPart);
        Stamp = e.NameScope.Find<MeetingSummaryStamp>(StampPart);
        TextView?.SetText(_pendingText);
        IsQuoted = _quoted;
        IsStreaming = _streaming;
    }
}

/// <summary>A Summary or Decisions point: a dot, the text and its timestamp.</summary>
[PseudoClasses(DecisionPseudoClass)]
internal sealed class MeetingSummaryLineView : MeetingSummaryRow
{
    internal const string DecisionPseudoClass = ":decision";

    private MeetingSummaryLabels _labels;

    public MeetingSummaryLineView(MeetingSummaryPoint point, bool decision, MeetingSummaryLabels labels)
        : base(point)
    {
        _labels = labels;
        PseudoClasses.Set(DecisionPseudoClass, decision);
    }

    protected override Type StyleKeyOverride => typeof(MeetingSummaryLineView);

    public override void SetPoint(MeetingSummaryPoint point, MeetingSummaryLabels labels)
    {
        _labels = labels;
        base.SetPoint(point, labels);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateStamp(_labels);
    }
}

/// <summary>
/// An action item: the checkbox, the text over its owner and due date, then the timestamp and one send button per
/// target. Checking it grows the fill and draws the check while a strikethrough sweeps across every wrapped line and the
/// text eases to muted; unchecking reverses it faster.
/// </summary>
[TemplatePart(CheckPart, typeof(MeetingSummaryCheck))]
[TemplatePart(MetaPart, typeof(Panel))]
[TemplatePart(OwnerAvatarPart, typeof(Avatar))]
[TemplatePart(OwnerPart, typeof(TextBlock))]
[TemplatePart(DuePart, typeof(TextBlock))]
[TemplatePart(SendsPart, typeof(StackPanel))]
[PseudoClasses(DonePseudoClass, CompactPseudoClass)]
internal sealed class MeetingSummaryActionRow : MeetingSummaryRow
{
    internal const string CheckPart = "PART_Check";
    internal const string MetaPart = "PART_Meta";
    internal const string OwnerAvatarPart = "PART_OwnerAvatar";
    internal const string OwnerPart = "PART_Owner";
    internal const string DuePart = "PART_Due";
    internal const string SendsPart = "PART_Sends";
    internal const string DonePseudoClass = ":done";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>The fill grows in over 160ms.</summary>
    internal static readonly TimeSpan FillIn = TimeSpan.FromMilliseconds(160);

    /// <summary>The check draws on over 200ms, ease-out.</summary>
    internal static readonly TimeSpan DrawIn = TimeSpan.FromMilliseconds(200);

    /// <summary>The strikethrough sweeps across the lines over 240ms.</summary>
    internal static readonly TimeSpan StrikeIn = TimeSpan.FromMilliseconds(240);

    /// <summary>Unchecking reverses everything over 120ms.</summary>
    internal static readonly TimeSpan Reverse = TimeSpan.FromMilliseconds(120);

    private static readonly Easing s_easeOut = new SplineEasing(0.215, 0.61, 0.355, 1);

    private readonly List<MeetingSummarySendButton> _sends = [];
    private MeetingSummaryLabels _labels;
    private MeetingAttendee? _owner;
    private IReadOnlyList<MeetingSummarySendTarget> _targets = [];
    private bool _done;
    private bool _compact;
    private MotionValue<double>? _strike;
    private MotionValue<double>? _muted;

    public MeetingSummaryActionRow(MeetingActionItem item, MeetingSummaryLabels labels)
        : base(item)
    {
        _labels = labels;
    }

    protected override Type StyleKeyOverride => typeof(MeetingSummaryActionRow);

    /// <summary>The action item.</summary>
    public MeetingActionItem Item => (MeetingActionItem)Point;

    /// <summary>The checkbox (after the template is applied).</summary>
    public MeetingSummaryCheck? Check { get; private set; }

    /// <summary>The owner's name as shown (first name when compact).</summary>
    public TextBlock? OwnerText { get; private set; }

    /// <summary>The due date as shown.</summary>
    public TextBlock? DueText { get; private set; }

    /// <summary>The send buttons, one per target.</summary>
    public IReadOnlyList<MeetingSummarySendButton> Sends => _sends;

    private Panel? _meta;
    private Avatar? _ownerAvatar;
    private StackPanel? _sendsPanel;

    /// <summary>Whether it is checked off.</summary>
    public bool IsDone => _done;

    /// <summary>Raised when the checkbox is pressed.</summary>
    public event EventHandler? CheckRequested;

    /// <summary>Raised when a send button is pressed.</summary>
    public event EventHandler<MeetingSummarySendButton>? SendPressed;

    /// <summary>Sets the owner, the targets and the labels.</summary>
    public void Configure(MeetingActionItem item, MeetingAttendee? owner, IReadOnlyList<MeetingSummarySendTarget> targets,
        MeetingSummaryLabels labels)
    {
        _owner = owner;
        _targets = targets;
        _labels = labels;
        SetPoint(item, labels);
        UpdateMeta();
        SyncSends();
    }

    /// <summary>Owners read by first name on narrow cards (the full name stays for screen readers).</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        UpdateMeta();
    }

    /// <summary>
    /// Checks or unchecks it. Animated: the check's fill and stroke, the strikethrough sweep and the color run on
    /// <see cref="MotionValue{T}"/>s (code-driven because the sweep runs along the measured wrapped lines and the check
    /// along its stroke, which a ControlTheme transition cannot express). Reduced motion and Off jump to the end state.
    /// </summary>
    public void SetDone(bool done, bool animate)
    {
        _done = done;
        PseudoClasses.Set(DonePseudoClass, done);
        if (Check is not null)
        {
            Check.IsChecked = done;
            Check.Mark?.SetChecked(done, animate);
            AutomationProperties.SetName(Check, _labels.Check(Item.Text));
        }
        var text = TextView;
        if (text is null)
        {
            return;
        }
        _strike ??= Make(text, v => text.Strike = v, text.Strike);
        _muted ??= Make(text, v => text.Muted = v, text.Muted);
        var target = done ? 1.0 : 0.0;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _strike.Jump(target);
            _muted.Jump(target);
            text.Strike = target;
            text.Muted = target;
            return;
        }
        _ = _strike.AnimateTo(target, MotionSpec.Tween(done ? StrikeIn : Reverse, done ? s_easeOut : ArcEasings.Standard));
        _ = _muted.AnimateTo(target, MotionSpec.Tween(done ? DrawIn : Reverse, ArcEasings.Standard));
    }

    private static MotionValue<double> Make(Visual owner, Action<double> apply, double start)
    {
        var value = new MotionValue<double>(owner, start, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (Check is not null)
        {
            Check.Click -= OnCheckClick;
        }
        base.OnApplyTemplate(e);
        Check = e.NameScope.Find<MeetingSummaryCheck>(CheckPart);
        _meta = e.NameScope.Find<Panel>(MetaPart);
        _ownerAvatar = e.NameScope.Find<Avatar>(OwnerAvatarPart);
        OwnerText = e.NameScope.Find<TextBlock>(OwnerPart);
        DueText = e.NameScope.Find<TextBlock>(DuePart);
        _sendsPanel = e.NameScope.Find<StackPanel>(SendsPart);
        if (Check is not null)
        {
            Check.Click += OnCheckClick;
        }
        _strike = null;
        _muted = null;
        UpdateStamp(_labels);
        UpdateMeta();
        _sends.Clear();
        SyncSends();
        SetDone(_done, animate: false);
    }

    private void OnCheckClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CheckRequested?.Invoke(this, EventArgs.Empty);

    protected override void OnStreamingChanged(bool streaming)
    {
        if (Check is not null)
        {
            Check.IsHitTestVisible = !streaming;
            Check.IsTabStop = !streaming;
        }
        foreach (var send in _sends)
        {
            send.IsHitTestVisible = !streaming;
            send.IsTabStop = !streaming;
        }
    }

    private void UpdateMeta()
    {
        if (OwnerText is null)
        {
            return;
        }
        var owner = _owner;
        var due = Item.Due;
        if (_ownerAvatar is not null)
        {
            _ownerAvatar.IsVisible = owner is not null;
            _ownerAvatar.DisplayName = owner?.Name;
            _ownerAvatar.Source = owner?.Portrait;
        }
        OwnerText.IsVisible = owner is not null;
        OwnerText.Text = owner is null ? null : _compact ? MeetingSummaryFormat.FirstName(owner.Name) : owner.Name;
        if (owner is not null)
        {
            AutomationProperties.SetName(OwnerText, owner.Name);
        }
        if (DueText is not null)
        {
            DueText.IsVisible = due is not null;
            DueText.Text = due is { } d ? _labels.Due(d) : null;
        }
        if (_meta is not null)
        {
            _meta.IsVisible = owner is not null || due is not null;
            foreach (var child in _meta.Children)
            {
                if (child is TextBlock { Name: "PART_Dot" } dot)
                {
                    dot.IsVisible = owner is not null && due is not null;
                }
            }
        }
        if (Check is not null)
        {
            AutomationProperties.SetName(Check, _labels.Check(Item.Text));
        }
    }

    private void SyncSends()
    {
        if (_sendsPanel is null)
        {
            return;
        }
        var same = _sends.Count == _targets.Count;
        for (var i = 0; same && i < _sends.Count; i++)
        {
            same = ReferenceEquals(_sends[i].Target, _targets[i]);
        }
        if (!same)
        {
            foreach (var send in _sends)
            {
                send.Click -= OnSendClick;
                _sendsPanel.Children.Remove(send);
            }
            _sends.Clear();
            foreach (var target in _targets)
            {
                var send = new MeetingSummarySendButton(target);
                send.Click += OnSendClick;
                _sends.Add(send);
                _sendsPanel.Children.Add(send);
            }
        }
        foreach (var send in _sends)
        {
            send.SetLabels(Item.Text, _labels);
            send.IsHitTestVisible = !IsStreaming;
            send.IsTabStop = !IsStreaming;
        }
    }

    private void OnSendClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MeetingSummarySendButton send)
        {
            SendPressed?.Invoke(this, send);
        }
    }
}

/// <summary>
/// The 16px checkbox of an action item inside a 32px hit area. Screen readers get a checkbox with its checked state;
/// Enter and Space toggle it. A press scales the square to 0.9, never the row, and the keyboard ring hugs the square.
/// </summary>
[TemplatePart(MarkPart, typeof(MeetingSummaryCheckMark))]
internal sealed class MeetingSummaryCheck : AvaloniaButton
{
    internal const string MarkPart = "PART_Mark";

    private bool _checked;

    protected override Type StyleKeyOverride => typeof(MeetingSummaryCheck);

    /// <summary>The drawn square (after the template is applied).</summary>
    public MeetingSummaryCheckMark? Mark { get; private set; }

    /// <summary>Whether it is checked.</summary>
    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }
            _checked = value;
            if (ControlAutomationPeer.FromElement(this) is Peer peer)
            {
                peer.RaiseToggled(!value, value);
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Mark = e.NameScope.Find<MeetingSummaryCheckMark>(MarkPart);
        Mark?.SetChecked(_checked, animate: false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(MeetingSummaryCheck owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => ((MeetingSummaryCheck)Owner).IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => ((IInvokeProvider)this).Invoke();

        public void RaiseToggled(bool before, bool after) =>
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                before ? ToggleState.On : ToggleState.Off, after ? ToggleState.On : ToggleState.Off);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    }
}

/// <summary>
/// The drawn square: a 1.5px outline, an accent fill that grows from the center (160ms) and a check stroked on along
/// its path (200ms, ease-out). Code-driven because the check draws along its own stroke length.
/// </summary>
internal sealed class MeetingSummaryCheckMark : Control
{
    /// <summary>The outline color.</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<MeetingSummaryCheckMark, IBrush?>(nameof(BorderBrush));

    /// <summary>The fill color when checked.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MeetingSummaryCheckMark, IBrush?>(nameof(Fill));

    /// <summary>The check's color.</summary>
    public static readonly StyledProperty<IBrush?> CheckBrushProperty =
        AvaloniaProperty.Register<MeetingSummaryCheckMark, IBrush?>(nameof(CheckBrush));

    /// <summary>The square's corner radius.</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<MeetingSummaryCheckMark, double>(nameof(Radius), 4.5);

    private static readonly Point[] s_check = [new(4.25, 8.25), new(6.9, 10.9), new(11.75, 5.6)];
    private static readonly Easing s_easeOut = new SplineEasing(0.215, 0.61, 0.355, 1);

    private MotionValue<double>? _fill;
    private MotionValue<double>? _draw;
    private double _fillValue;
    private double _drawValue;
    private ImmutablePen? _borderPen;
    private ImmutablePen? _checkPen;

    static MeetingSummaryCheckMark()
    {
        AffectsRender<MeetingSummaryCheckMark>(BorderBrushProperty, FillProperty, CheckBrushProperty, RadiusProperty);
    }

    /// <inheritdoc cref="BorderBrushProperty"/>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <inheritdoc cref="CheckBrushProperty"/>
    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>The fill's progress (0 to 1).</summary>
    internal double FillProgress => _fillValue;

    /// <summary>The check's drawn length (0 to 1).</summary>
    internal double DrawProgress => _drawValue;

    public void SetChecked(bool on, bool animate)
    {
        _fill ??= Make(v => _fillValue = v);
        _draw ??= Make(v => _drawValue = v);
        var target = on ? 1.0 : 0.0;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _fill.Jump(target);
            _draw.Jump(target);
            _fillValue = target;
            _drawValue = target;
            InvalidateVisual();
            return;
        }
        if (on)
        {
            _ = _fill.AnimateTo(1, MotionSpec.Tween(MeetingSummaryActionRow.FillIn, ArcEasings.Standard));
            _ = _draw.AnimateTo(1, MotionSpec.Tween(MeetingSummaryActionRow.DrawIn, s_easeOut));
        }
        else
        {
            _ = _draw.AnimateTo(0, MotionSpec.Tween(MeetingSummaryActionRow.Reverse, ArcEasings.Standard));
            _ = _fill.AnimateTo(0, MotionSpec.Tween(MeetingSummaryActionRow.Reverse, ArcEasings.Standard));
        }
    }

    private MotionValue<double> Make(Action<double> apply)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            apply(v);
            InvalidateVisual();
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BorderBrushProperty)
        {
            _borderPen = null;
        }
        else if (change.Property == CheckBrushProperty)
        {
            _checkPen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var box = new Rect(0.75, 0.75, size.Width - 1.5, size.Height - 1.5);
        var radius = Radius;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        _borderPen ??= BorderBrush is ISolidColorBrush b ? new ImmutablePen(new ImmutableSolidColorBrush(b.Color), 1.5) : null;
        if (_borderPen is not null && _fillValue < 0.999)
        {
            context.DrawRectangle(null, _borderPen, box, radius, radius);
        }
        if (_fillValue > 0.001 && Fill is { } fill)
        {
            var w = size.Width * _fillValue;
            var h = size.Height * _fillValue;
            var rect = new Rect((size.Width - w) / 2, (size.Height - h) / 2, w, h);
            var r = (radius + 0.75) * _fillValue;
            context.DrawRectangle(fill, null, rect, r, r);
        }
        if (_drawValue > 0.001 && CheckBrush is ISolidColorBrush check)
        {
            _checkPen ??= new ImmutablePen(new ImmutableSolidColorBrush(check.Color), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            DrawPartial(context, _checkPen, _drawValue);
        }
    }

    private static void DrawPartial(DrawingContext context, IPen pen, double progress)
    {
        var p0 = s_check[0];
        var p1 = s_check[1];
        var p2 = s_check[2];
        var first = Distance(p0, p1);
        var second = Distance(p1, p2);
        var drawn = Math.Min(1, progress) * (first + second);
        if (drawn <= first)
        {
            context.DrawLine(pen, p0, Lerp(p0, p1, drawn / first));
            return;
        }
        context.DrawLine(pen, p0, p1);
        context.DrawLine(pen, p1, Lerp(p1, p2, (drawn - first) / second));
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}

/// <summary>
/// A send button of an action item: the target's mark, then a spinner while it sends, then a chip with the mark, the
/// reference and a check, or an alert icon and Retry when it failed. The parts crossfade while the slot springs to the
/// new width (Core's width spring for labels, 0.25s, no bounce), the parts pinned to its start.
/// </summary>
[PseudoClasses(SentPseudoClass, FailedPseudoClass, SendingPseudoClass)]
internal sealed class MeetingSummarySendButton : AvaloniaButton
{
    internal const string SentPseudoClass = ":sent";
    internal const string FailedPseudoClass = ":failed";
    internal const string SendingPseudoClass = ":sending";

    private readonly ArcContentSwap _swap = new()
    {
        Mode = ContentSwapMode.Crossfade,
        HorizontalAlignment = HorizontalAlignment.Left,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private string _itemText = string.Empty;
    private MeetingSummaryLabels _labels = MeetingSummaryLabels.Default;
    private CancellationTokenSource? _cancel;

    public MeetingSummarySendButton(MeetingSummarySendTarget target)
    {
        Target = target;
        Content = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Width,
            Spring = ArcSpring.Quick,
            ClipOverflow = 0,
            CrossClipOverflow = 4,
            Child = _swap,
        };
        Show();
    }

    protected override Type StyleKeyOverride => typeof(MeetingSummarySendButton);

    /// <summary>Where it sends.</summary>
    public MeetingSummarySendTarget Target { get; }

    /// <summary>Where the send is.</summary>
    public MeetingSummarySendState State { get; private set; }

    /// <summary>What the send returned.</summary>
    public MeetingSummarySendResult? Result { get; private set; }

    /// <summary>The text of the chip or the failure label.</summary>
    public string? ShownLabel { get; private set; }

    /// <summary>The in-flight send's cancellation (owned by the card).</summary>
    internal CancellationTokenSource? Cancellation
    {
        get => _cancel;
        set => _cancel = value;
    }

    public void SetLabels(string itemText, MeetingSummaryLabels labels)
    {
        _itemText = itemText;
        _labels = labels;
        UpdateName();
    }

    public void SetState(MeetingSummarySendState state, MeetingSummarySendResult? result = null)
    {
        State = state;
        Result = result;
        PseudoClasses.Set(SentPseudoClass, state == MeetingSummarySendState.Sent);
        PseudoClasses.Set(FailedPseudoClass, state == MeetingSummarySendState.Failed);
        PseudoClasses.Set(SendingPseudoClass, state == MeetingSummarySendState.Sending);
        Show();
        UpdateName();
    }

    private void UpdateName()
    {
        var name = State switch
        {
            MeetingSummarySendState.Sent => _labels.AnnounceSent(Target.Name, Result?.Reference).TrimEnd('.'),
            MeetingSummarySendState.Failed => _labels.Retry + ", " + _labels.SendTo(_itemText, Target.Name),
            _ => _labels.SendTo(_itemText, Target.Name),
        };
        AutomationProperties.SetName(this, name);
        Tooltip.SetTip(this, State == MeetingSummarySendState.Idle ? _labels.SendToShort(Target.Name) : null);
    }

    private void Show()
    {
        var mark = Target.Mark ?? ArcIcons.Send;
        Control content;
        switch (State)
        {
            case MeetingSummarySendState.Sending:
                ShownLabel = null;
                content = new ArcSpinner { Width = 13, Height = 13, Thickness = 1.5, Margin = new Thickness(6.5, 0) };
                content.Classes.Add("send-spinner");
                break;
            case MeetingSummarySendState.Sent:
                ShownLabel = Result?.Reference ?? _labels.Sent;
                content = Row(new ArcGlyph { Data = mark, Size = 13 },
                    new TextBlock { Text = ShownLabel, Classes = { "send-label" } },
                    new ArcGlyph { Data = ArcIcons.Check, Size = 12, Classes = { "send-check" } });
                break;
            case MeetingSummarySendState.Failed:
                ShownLabel = _labels.Retry;
                content = Row(new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 13, Classes = { "send-alert" } },
                    new TextBlock { Text = ShownLabel, Classes = { "send-label" } });
                break;
            default:
                ShownLabel = null;
                content = new ArcGlyph { Data = mark, Size = 14, Margin = new Thickness(6, 0) };
                break;
        }
        _swap.Content = content;
    }

    private static StackPanel Row(params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(7, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children)
        {
            child.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(child);
        }
        return row;
    }
}

/// <summary>The quote card: the speaker's avatar, name and role, the time, and the transcript passage.</summary>
internal sealed class MeetingSummaryQuoteCard : TemplatedControl
{
    /// <summary>The speaker's full name.</summary>
    public static readonly StyledProperty<string?> SpeakerProperty =
        AvaloniaProperty.Register<MeetingSummaryQuoteCard, string?>(nameof(Speaker));

    /// <summary>The speaker's role, or null.</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<MeetingSummaryQuoteCard, string?>(nameof(Role));

    /// <summary>The speaker's portrait, or null for initials.</summary>
    public static readonly StyledProperty<ImageSource?> PortraitProperty =
        AvaloniaProperty.Register<MeetingSummaryQuoteCard, ImageSource?>(nameof(Portrait));

    /// <summary>The timestamp ("2:14").</summary>
    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<MeetingSummaryQuoteCard, string?>(nameof(Time));

    /// <summary>The passage, in quotation marks.</summary>
    public static readonly StyledProperty<string?> QuoteProperty =
        AvaloniaProperty.Register<MeetingSummaryQuoteCard, string?>(nameof(Quote));

    /// <summary>Whether a role is shown.</summary>
    public static readonly StyledProperty<bool> HasRoleProperty =
        AvaloniaProperty.Register<MeetingSummaryQuoteCard, bool>(nameof(HasRole));

    public string? Speaker { get => GetValue(SpeakerProperty); set => SetValue(SpeakerProperty, value); }

    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    public ImageSource? Portrait { get => GetValue(PortraitProperty); set => SetValue(PortraitProperty, value); }

    public string? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    public string? Quote { get => GetValue(QuoteProperty); set => SetValue(QuoteProperty, value); }

    public bool HasRole { get => GetValue(HasRoleProperty); set => SetValue(HasRoleProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RoleProperty)
        {
            HasRole = !string.IsNullOrEmpty(Role);
        }
    }
}
