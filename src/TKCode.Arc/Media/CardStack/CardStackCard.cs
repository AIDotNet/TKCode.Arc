using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One card of a <see cref="CardStack"/> (container of an item). Holds the card's motion: drag offset, tilt sign,
/// opacity and stack depth. Every frame the depth, offset and tilt become one render matrix (the slot scales from its
/// bottom edge and drops by the peek; the card translates and rotates about its center), so nothing re-lays out while
/// cards are dragged, thrown, rise or fly back. MotionValues rather than ControlTheme transitions: drags write the
/// offset every move, releases and throws hand their velocity to springs, and the depth follows a moving target.
/// </summary>
[TemplatePart(ShadePart, typeof(Control))]
[TemplatePart(KeepStampPart, typeof(Control))]
[TemplatePart(PassStampPart, typeof(Control))]
[PseudoClasses(TopPseudoClass, FlyingPseudoClass, LiftedPseudoClass, StageFocusPseudoClass)]
internal sealed class CardStackCard : ContentControl
{
    public const string ShadePart = "PART_Shade";
    public const string KeepStampPart = "PART_KeepStamp";
    public const string PassStampPart = "PART_PassStamp";

    /// <summary>The interactive top card.</summary>
    public const string TopPseudoClass = ":top";

    /// <summary>A decided card on its way out.</summary>
    public const string FlyingPseudoClass = ":flying";

    /// <summary>In the hand or in flight: the card floats and casts a shadow.</summary>
    public const string LiftedPseudoClass = ":lifted";

    /// <summary>The stage has keyboard focus: the top card carries the focus outline.</summary>
    public const string StageFocusPseudoClass = ":stage-focus";

    public static readonly StyledProperty<string?> KeepLabelProperty =
        AvaloniaProperty.Register<CardStackCard, string?>(nameof(KeepLabel));

    public static readonly StyledProperty<string?> PassLabelProperty =
        AvaloniaProperty.Register<CardStackCard, string?>(nameof(PassLabel));

    private readonly MatrixTransform _transform = new();
    private readonly DragTracker _drag;
    private Control? _shade;
    private Control? _keepStamp;
    private Control? _passStamp;
    private bool _dragging;
    private bool _flying;
    private double _originX;
    private double _originY;

    public CardStackCard()
    {
        X = new MotionValue<double>(this, 0, MotionVectors.Double);
        Y = new MotionValue<double>(this, 0, MotionVectors.Double);
        Fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        Depth = new MotionValue<double>(this, 0, MotionVectors.Double);
        X.Changed += OnOffsetChanged;
        Y.Changed += OnOffsetChanged;
        Fade.Changed += OnVisualChanged;
        Depth.Changed += OnVisualChanged;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.TopLeft;
        Focusable = false;
        IsTabStop = false;
        _drag = new DragTracker(this, new DragOptions
        {
            Threshold = 3,
            VelocityWindow = TimeSpan.FromMilliseconds(80),
            VelocityStaleAfter = TimeSpan.FromMilliseconds(60)
        })
        {
            CanBegin = CanBeginDrag
        };
        _drag.Pressed += OnDragPressed;
        _drag.Moved += OnDragMoved;
        _drag.Completed += (_, e) => Release(e.Velocity);
        _drag.Released += (_, _) => Release(default);
        _drag.Cancelled += (_, _) => Release(default);
    }

    public string? KeepLabel { get => GetValue(KeepLabelProperty); set => SetValue(KeepLabelProperty, value); }

    public string? PassLabel { get => GetValue(PassLabelProperty); set => SetValue(PassLabelProperty, value); }

    internal CardStack? Owner { get; set; }

    /// <summary>Item index this container shows.</summary>
    public int Index { get; set; } = -1;

    public MotionValue<double> X { get; }

    public MotionValue<double> Y { get; }

    /// <summary>The card's own opacity (the stack depth adds its presence on top).</summary>
    public MotionValue<double> Fade { get; }

    /// <summary>Shown stack depth (0 = top); springs toward the slot, rising while the top card is dragged away.</summary>
    public MotionValue<double> Depth { get; }

    /// <summary>Grabbing the top half tips the card the way the hand moves; grabbing the bottom half tips it the other way.</summary>
    public double Tilt { get; set; } = 1;

    /// <summary>Integer slot in the stack (0 = top). Decided cards keep 0.</summary>
    public int Slot { get; set; }

    public bool IsInteractive { get; private set; }

    /// <summary>A card flying home from undo or start over leaves the stack alone until it lands.</summary>
    public bool IsHoming { get; set; }

    /// <summary>The card was thrown (so an undo brings it back).</summary>
    public bool WasDecided { get; set; }

    /// <summary>False while a card mounted from where it left still has to come home.</summary>
    public bool IsSettled { get; set; } = true;

    public bool IsDragging => _dragging;

    public bool IsFlying => _flying;

    protected override Type StyleKeyOverride => typeof(CardStackCard);

    /// <summary>Resets the motion state for a freshly realized card.</summary>
    public void Reset(int slot)
    {
        _drag.Cancel();
        X.Jump(0);
        Y.Jump(0);
        Fade.Jump(1);
        Depth.Jump(slot);
        Tilt = 1;
        Slot = slot;
        IsHoming = false;
        WasDecided = false;
        IsSettled = true;
        SetDragging(false);
        SetFlying(false);
    }

    public void SetInteractive(bool interactive, string? label)
    {
        IsInteractive = interactive;
        IsHitTestVisible = interactive;
        // Cards behind the top one are inert: nothing inside them takes focus.
        KeyboardNavigation.SetTabNavigation(this, interactive ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        PseudoClasses.Set(TopPseudoClass, interactive);
        AutomationProperties.SetName(this, interactive ? label : null);
        if (!interactive)
        {
            _drag.Cancel();
        }
    }

    public void SetFlying(bool flying)
    {
        _flying = flying;
        PseudoClasses.Set(FlyingPseudoClass, flying);
        PseudoClasses.Set(LiftedPseudoClass, _flying || _dragging);
    }

    public void SetStageFocus(bool focused) => PseudoClasses.Set(StageFocusPseudoClass, focused);

    /// <summary>A key or button decided the card mid-drag: the throw takes over from the finger.</summary>
    public void CancelDrag()
    {
        _drag.Cancel();
        SetDragging(false);
    }

    private void SetDragging(bool dragging)
    {
        _dragging = dragging;
        PseudoClasses.Set(LiftedPseudoClass, _flying || _dragging);
    }

    private bool CanBeginDrag(PointerPressedEventArgs e)
    {
        if (!IsInteractive || Owner is null)
        {
            return false;
        }
        // Presses on buttons, links and fields inside the card keep working.
        for (var visual = e.Source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is Avalonia.Controls.Button or TextBox or ComboBox or ToggleButton)
            {
                return false;
            }
        }
        return true;
    }

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        X.Stop();
        Y.Stop();
        IsHoming = false;
        if (Math.Abs(X.Value) < 2 && TopLevel.GetTopLevel(this) is { } top && top.TranslatePoint(e.Start, this) is { } local)
        {
            Tilt = local.Y < Bounds.Height / 2 ? 1 : -1;
        }
        _originX = X.Value;
        _originY = CardStackMath.UnresistY(Y.Value);
        SetDragging(true);
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (!_dragging || !IsInteractive)
        {
            return;
        }
        X.Jump(_originX + e.Offset.X);
        Y.Jump(CardStackMath.ResistY(_originY + e.Offset.Y));
    }

    private void Release(Vector velocity)
    {
        if (!_dragging)
        {
            return;
        }
        SetDragging(false);
        if (!IsInteractive)
        {
            return;
        }
        Owner?.OnCardReleased(this, new Point(X.Value, Y.Value), velocity);
    }

    private void OnOffsetChanged(double _)
    {
        UpdateVisual();
        if (IsInteractive && !IsHoming)
        {
            Owner?.OnTopCardMoved(this);
        }
    }

    private void OnVisualChanged(double _) => UpdateVisual();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _shade = e.NameScope.Find<Control>(ShadePart);
        _keepStamp = e.NameScope.Find<Control>(KeepStampPart);
        _passStamp = e.NameScope.Find<Control>(PassStampPart);
        UpdateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        UpdateVisual(finalSize);
        return arranged;
    }

    private void UpdateVisual() => UpdateVisual(Bounds.Size);

    private void UpdateVisual(Size size)
    {
        var depth = Depth.Value;
        var x = X.Value;
        var y = Y.Value;
        var w = size.Width;
        var h = size.Height;
        // Card: translate(x, y) rotate(r) about its center.
        var radians = x * CardStackMath.Rotate * Tilt * Math.PI / 180;
        var card = Matrix.CreateTranslation(-w / 2, -h / 2) * Matrix.CreateRotation(radians) *
                   Matrix.CreateTranslation(w / 2 + x, h / 2 + y);
        // Slot: translateY(depth × peek) scale(1 − depth × shrink) about its bottom edge.
        var scale = 1 - depth * CardStackMath.Shrink;
        var slot = Matrix.CreateTranslation(-w / 2, -h) * Matrix.CreateScale(scale, scale) *
                   Matrix.CreateTranslation(w / 2, h + depth * CardStackMath.Peek);
        _transform.Matrix = card * slot;
        Opacity = CardStackMath.Presence(depth) * Fade.Value;
        if (_shade is not null)
        {
            _shade.Opacity = CardStackMath.Shade(depth);
        }
        if (_keepStamp is not null)
        {
            _keepStamp.Opacity = CardStackMath.KeepStamp(x);
        }
        if (_passStamp is not null)
        {
            _passStamp.Opacity = CardStackMath.PassStamp(x);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardStackCardPeer(this);

    private sealed class CardStackCardPeer : ControlAutomationPeer
    {
        public CardStackCardPeer(CardStackCard owner)
            : base(owner)
        {
        }

        private bool Top => ((CardStackCard)Owner).IsInteractive;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => Top;

        protected override bool IsControlElementCore() => Top;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => Top ? base.GetChildrenCore() : [];
    }
}
