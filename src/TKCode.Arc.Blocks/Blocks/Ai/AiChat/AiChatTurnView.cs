using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One turn of <see cref="AiChat"/>'s thread (Arc's <c>article</c>): the person's row, 14px, then the reply. The view is
/// kept while its turn stays on the visible branch; a new version is bound into the same view (the focus stays on the
/// button that made it), its content fading in over 160ms while the turn's height follows on <see cref="ArcSpring.Settle"/>
/// (ai.md 8: the documented slide, not the measured jump). A new turn fades in over 160ms without moving. The reply's
/// actions show while a mouse or pen is over the turn or the focus is inside it, and always on the last turn.
/// </summary>
[TemplatePart(SizerPart, typeof(AiChatTurnSizer))]
[TemplatePart(BodyPart, typeof(Control))]
[TemplatePart(UserPart, typeof(AiUserBubble))]
[TemplatePart(ReplyPart, typeof(AiChatReplyView))]
internal sealed class AiChatTurnView : TemplatedControl
{
    public const string SizerPart = "PART_Sizer";
    public const string BodyPart = "PART_Body";
    public const string UserPart = "PART_User";
    public const string ReplyPart = "PART_Reply";

    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();

    private readonly AiVersionPager _pager = new();
    private AiChat? _owner;
    private AiChatTurnSizer? _sizer;
    private Control? _body;
    private AiUserBubble? _user;
    private AiChatReplyView? _reply;
    private MotionValue<double>? _fade;
    private AiChatTurn? _watchedTurn;
    private bool _hover;
    private bool _fadePending;
    private bool _isLast;
    private bool _isNarrow;
    private bool _isTouch;
    private int _position = 1;
    private int _count = 1;

    static AiChatTurnView()
    {
        FocusableProperty.OverrideDefaultValue<AiChatTurnView>(false);
    }

    /// <summary>The turn shown.</summary>
    public AiChatTurn? Turn { get; private set; }

    /// <summary>The version of <see cref="Turn"/> shown.</summary>
    public AiChatTurnVersion? Version { get; private set; }

    internal AiUserBubble? User => _user;
    internal AiChatReplyView? ReplyView => _reply;
    internal AiVersionPager Pager => _pager;
    internal AiChatTurnSizer? Sizer => _sizer;

    /// <summary>How often the turn was measured (tests: a streamed reply must not lay out the other turns).</summary>
    internal int MeasureCount { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCount++;
        return base.MeasureOverride(availableSize);
    }

    /// <summary>The opacity of the turn's content (1 at rest; a new turn or version fades it in).</summary>
    internal double ContentOpacity => _body?.Opacity ?? 1;

    /// <summary>The chat the turn belongs to (labels, models).</summary>
    internal AiChat? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            if (_reply is not null)
            {
                _reply.Owner = value;
            }
            ApplyLabels();
        }
    }

    /// <summary>The last turn: its reply's actions always show.</summary>
    internal bool IsLast
    {
        get => _isLast;
        set
        {
            _isLast = value;
            if (_reply is not null)
            {
                _reply.IsLast = value;
            }
        }
    }

    /// <summary>The chat is narrow (≤ 480): the bubble takes 88% and source rows hide their domains.</summary>
    internal bool IsNarrow
    {
        get => _isNarrow;
        set
        {
            _isNarrow = value;
            if (_user is not null)
            {
                _user.IsNarrow = value;
            }
            _reply?.SetNarrow(value);
        }
    }

    /// <summary>The person uses touch (nothing hovers): the actions of the person's row and of the reply always show.</summary>
    internal bool IsTouch
    {
        get => _isTouch;
        set
        {
            _isTouch = value;
            ApplyTouch();
        }
    }

    private void ApplyTouch()
    {
        if (_user is not null)
        {
            _user.AlwaysVisible = _isTouch;
        }
        if (_reply is not null)
        {
            _reply.AlwaysShowsActions = _isTouch;
        }
    }

    /// <summary>Where the turn is in the thread, for its accessible name ("Message 3 of 8").</summary>
    internal void SetPosition(int position, int count)
    {
        _position = position;
        _count = count;
        UpdateName();
    }

    #region Template and binding

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _sizer = scope.Find<AiChatTurnSizer>(SizerPart);
        _body = scope.Find<Control>(BodyPart);
        _user = scope.Find<AiUserBubble>(UserPart);
        _reply = scope.Find<AiChatReplyView>(ReplyPart);
        _fade = _body is null ? null : new MotionValue<double>(_body, 1, MotionVectors.Double);
        if (_fade is not null)
        {
            _fade.Changed += v =>
            {
                if (_body is not null)
                {
                    _body.Opacity = Math.Clamp(v, 0, 1);
                }
            };
        }
        if (_reply is not null)
        {
            _reply.Owner = _owner;
            _reply.IsLast = _isLast;
            _reply.VersionPager = _pager;
            _reply.SetNarrow(_isNarrow);
        }
        if (_user is not null)
        {
            _user.IsNarrow = _isNarrow;
        }
        ApplyLabels();
        Push();
        ApplyTouch();
        if (_fadePending)
        {
            _fadePending = false;
            FadeIn();
        }
    }

    /// <summary>Shows <paramref name="version"/> of <paramref name="turn"/> in this view (the same controls stay).</summary>
    internal void Bind(AiChatTurn turn, AiChatTurnVersion version)
    {
        Turn = turn;
        Version = version;
        WatchTurn(TopLevel.GetTopLevel(this) is not null ? turn : null);
        Push();
    }

    /// <summary>Lets go of the turn (the view leaves the thread).</summary>
    internal void Unbind() => WatchTurn(null);

    /// <summary>The turn's versions are app data that can outlive the view: they are followed only while it is in a window.</summary>
    private void WatchTurn(AiChatTurn? turn)
    {
        if (ReferenceEquals(turn, _watchedTurn))
        {
            return;
        }
        if (_watchedTurn is not null)
        {
            _watchedTurn.Versions.CollectionChanged -= OnVersionsChanged;
        }
        _watchedTurn = turn;
        if (turn is not null)
        {
            turn.Versions.CollectionChanged += OnVersionsChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchTurn(Turn);
        UpdatePager();
    }

    private void OnVersionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdatePager();

    private void Push()
    {
        if (Version is not { } version)
        {
            return;
        }
        if (_user is not null && !ReferenceEquals(_user.Message, version.Prompt))
        {
            _user.Message = version.Prompt;
        }
        if (_reply is not null && !ReferenceEquals(_reply.Reply, version.Reply))
        {
            _reply.Reply = version.Reply;
        }
        UpdatePager();
    }

    private void UpdatePager()
    {
        var count = Turn?.Versions.Count ?? 0;
        _pager.Count = count;
        _pager.Index = Turn?.SelectedVersionIndex ?? 0;
        if (_reply is not null)
        {
            _reply.HasVersions = count > 1;
        }
    }

    /// <summary>Every word from the chat's labels.</summary>
    internal void ApplyLabels()
    {
        var labels = _owner?.Labels;
        if (labels is null)
        {
            return;
        }
        if (_user is not null)
        {
            _user.CopyText = labels.Copy;
            _user.CopiedText = labels.Copied;
            _user.EditText = labels.Edit;
            _user.EditMessageText = labels.EditMessage;
            _user.CancelText = labels.Cancel;
            _user.SendText = labels.Send;
            _user.AttachmentsText = labels.Attachments;
        }
        _pager.GroupName = labels.Version;
        _pager.CountText = labels.VersionCount;
        _pager.PreviousText = labels.PreviousVersion;
        _pager.NextText = labels.NextVersion;
        _reply?.ApplyLabels();
        UpdateName();
    }

    private void UpdateName()
    {
        if (_owner?.Labels is { } labels)
        {
            AutomationProperties.SetName(this, labels.Turn(_position, _count));
        }
    }

    #endregion

    #region Motion

    /// <summary>A new turn fades in (160ms standard; reduced motion 120ms linear; Off at once), without moving.</summary>
    internal void PlayEnter() => FadeIn();

    /// <summary>
    /// A version was bound: the turn's height follows the new content on <see cref="ArcSpring.Settle"/> (full motion only)
    /// while the content fades in.
    /// </summary>
    internal void PlayVersionSwap()
    {
        _sizer?.Arm();
        FadeIn();
    }

    /// <summary>
    /// Code-driven (<see cref="MotionValue{T}"/>): the content starts again from transparent on a re-mount, which is not a
    /// property change a ControlTheme transition could catch, and the curve depends on the motion level.
    /// </summary>
    private void FadeIn()
    {
        if (_fade is null)
        {
            // Not templated yet (a view made this frame): the fade starts with the template.
            _fadePending = true;
            return;
        }
        var level = TopLevel.GetTopLevel(this) is null ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _fade.Jump(1);
            return;
        }
        _fade.Jump(0);
        _ = _fade.AnimateTo(1, level == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
    }

    #endregion

    #region Reveal

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc reveals on (hover: hover) only: a touch does not hover.
        _hover = e.Pointer.Type != PointerType.Touch;
        UpdateReveal();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        UpdateReveal();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsKeyboardFocusWithinProperty)
        {
            UpdateReveal();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchTurn(null);
        _hover = false;
        UpdateReveal();
        _fade?.Jump(1);
    }

    private void UpdateReveal()
    {
        if (_reply is not null)
        {
            _reply.IsRevealed = _hover || IsKeyboardFocusWithin;
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new TurnAutomationPeer(this);

    /// <summary>A list item of the thread, named by its place ("Message 3 of 8").</summary>
    private sealed class TurnAutomationPeer(AiChatTurnView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}

/// <summary>
/// The height of a turn: it follows its content at once, except after <see cref="Arm"/> (a new version), when it moves to
/// the new height on <see cref="ArcSpring.Settle"/> with the top in place and the bottom clipped while it moves. Reduced
/// motion and Off jump.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): the target is the measured height of new content, which no ControlTheme
/// value holds, and only the one change after a version switch springs (streamed text must not lag).
/// </para>
/// </summary>
internal sealed class AiChatTurnSizer : Decorator
{
    private readonly RectangleGeometry _clip = new();
    private MotionValue<double>? _height;
    private double _target = double.NaN;
    private bool _armed;
    private long _armedAt;

    /// <summary>Overflow kept visible at the sides while the height moves (focus rings, the action bar's -6 margin).</summary>
    internal const double ClipBleed = 8;

    /// <summary>True while the height springs.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>The height shown now.</summary>
    internal double ShownHeight => _height?.Value ?? 0;

    /// <summary>How long an armed sizer waits for the new content's height (text is laid out a pass or two later).</summary>
    internal const int ArmWindowMilliseconds = 300;

    /// <summary>Makes the next change of the content's height spring (within <see cref="ArmWindowMilliseconds"/>).</summary>
    public void Arm()
    {
        _armed = true;
        _armedAt = Environment.TickCount64;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var armed = _armed && Environment.TickCount64 - _armedAt <= ArmWindowMilliseconds;
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = child.DesiredSize.Height;
        _height ??= CreateHeight(target);
        if (double.IsNaN(_target))
        {
            _target = target;
            _height.Jump(target);
        }
        else if (Math.Abs(target - _target) >= 0.5)
        {
            _target = target;
            _armed = false;
            if (IsLoaded && ArcMotion.IsFull(this) && (armed || _height.IsAnimating))
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Settle));
            }
            else
            {
                _height.Jump(target);
            }
        }
        return new Size(child.DesiredSize.Width, _height.IsAnimating ? _height.Value : target);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
        {
            return finalSize;
        }
        var height = Math.Max(child.DesiredSize.Height, finalSize.Height);
        child.Arrange(new Rect(0, 0, finalSize.Width, height));
        if (IsMoving && height > finalSize.Height + 0.5)
        {
            _clip.Rect = new Rect(-ClipBleed, -ClipBleed, finalSize.Width + 2 * ClipBleed, finalSize.Height + ClipBleed);
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }

    private MotionValue<double> CreateHeight(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }
}

/// <summary>The thread: the turns in order, 28px apart (24 in a narrow chat). A list of turns for assistive technology.</summary>
internal sealed class AiChatThreadPanel : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ThreadAutomationPeer(this);

    private sealed class ThreadAutomationPeer(AiChatThreadPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// A round icon button of the chat's header (Arc: New chat): 34px, a 18px Lucide glyph at stroke 1.75 in the secondary text
/// color; under a mouse or pen SurfaceMuted with the glyph in Foreground; the keyboard focus ring 2px outside.
/// </summary>
internal sealed class AiChatIconButton : AvaloniaButton
{
    /// <summary>The glyph (24 × 24 Lucide geometry).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AiChatIconButton, Geometry?>(nameof(Glyph));

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiChatIconButton);
}
