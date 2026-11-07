using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One card of the stack: a focusable button-like surface that draws its baked face, the frosted backdrop (glass), the
/// pointer light and its keyboard focus ring, all inside its own tilt so the ring leans with the card. Pressing scales it
/// to 0.985 (160ms, standard ease).
/// </summary>
internal sealed class WalletStackCardView : Control
{
    private static readonly Avalonia.Animation.Easings.Easing s_pressEase = ArcEasings.Standard;

    private readonly WalletStackStage _stage;
    private readonly MatrixTransform _transform = new();
    private readonly MotionValue<double> _press;
    private bool _focusVisible;
    private bool _expanded;

    public WalletStackCardView(WalletStackStage stage, WalletStackCard card, int index)
    {
        _stage = stage;
        Card = card;
        Index = index;
        Focusable = true;
        IsTabStop = false;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransformOrigin = RelativePoint.TopLeft;
        RenderTransform = _transform;
        ClipToBounds = false;
        Tilt = new WalletStackTiltLight(this);
        Tilt.Changed += OnTiltChanged;
        _press = new MotionValue<double>(this, 1, MotionVectors.Double);
        _press.Changed += _ => UpdateTransform();
    }

    public WalletStackCard Card { get; }

    public int Index { get; }

    public WalletStackTiltLight Tilt { get; }

    /// <summary>The baked face (null until the first bake, or where offscreen rendering is unavailable).</summary>
    public WalletStackCardArt? Art { get; set; }

    /// <summary>The current press scale (tests read it).</summary>
    public double PressScale => _press.Value;

    /// <summary>Whether the keyboard focus ring shows.</summary>
    public bool IsFocusVisible => _focusVisible && IsFocused;

    /// <summary>Whether this card is the open one (exposed to automation as expanded).</summary>
    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }
            _expanded = value;
            if (ControlAutomationPeer.FromElement(this) is { } peer)
            {
                peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    value ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                    value ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            }
        }
    }

    public void Press(bool down)
    {
        var target = down ? 0.985 : 1;
        if (!ArcMotion.IsFull(this))
        {
            _press.Jump(target);
            return;
        }
        _ = _press.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, s_pressEase));
    }

    private void OnTiltChanged()
    {
        UpdateTransform();
        InvalidateVisual();
    }

    public void UpdateTransform()
    {
        var size = Bounds.Size;
        var matrix = Tilt.ToMatrix(size);
        var s = _press.Value;
        if (s != 1)
        {
            var c = new Point(size.Width / 2, size.Height / 2);
            matrix = Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(c.X, c.Y) * matrix;
        }
        if (_transform.Matrix != matrix)
        {
            _transform.Matrix = matrix;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateTransform();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusVisible = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var rect = new Rect(size);
        var shape = WalletStackCardArt.Shape(size);
        // Transparent fill: the whole card takes input; the contact shadow every card keeps.
        context.DrawRectangle(Brushes.Transparent, null, shape, _stage.ContactShadow);
        var art = Art;
        var style = art?.Style;
        using (context.PushClip(shape))
        {
            if (Card.Material == WalletStackMaterial.Glass)
            {
                _stage.DrawBackdrop(context, this);
            }
            if (art is not null)
            {
                var pixels = art.Face.PixelSize;
                context.DrawImage(art.Face, new Rect(0, 0, pixels.Width, pixels.Height), rect);
            }
            else
            {
                context.DrawRectangle(_stage.FallbackBrush(Card), null, rect);
            }
            if (style is not null && _stage.GetLight(style, size) is { } light)
            {
                var tilt = Tilt.Tilt;
                var dest = new Rect(-size.Width / 2 + size.Width / 2 * tilt.X, -size.Height / 2 + size.Height / 2 * tilt.Y,
                    size.Width * 2, size.Height * 2);
                using (context.PushOpacity(Tilt.Light))
                using (context.PushRenderOptions(new RenderOptions { BitmapBlendingMode = style.LightBlend, BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality }))
                {
                    var pixels = light.PixelSize;
                    context.DrawImage(light, new Rect(0, 0, pixels.Width, pixels.Height), dest);
                }
            }
        }
        if (IsFocusVisible && _stage.FocusPen is { } pen)
        {
            // 2px ring 2px outside the card, drawn inside the card's tilt so it leans with it.
            var ring = new RoundedRect(rect.Inflate(3), shape.RadiiTopLeft.X + 3);
            context.DrawRectangle(null, pen, ring);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>Exposes the card as an expandable button: Invoke opens or closes it.</summary>
    private sealed class Peer : ControlAutomationPeer, IInvokeProvider, IExpandCollapseProvider
    {
        private readonly WalletStackCardView _view;

        public Peer(WalletStackCardView view) : base(view) => _view = view;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => _view._stage.Owner?.ActivateCard(_view.Index, keyboard: true);

        public ExpandCollapseState ExpandCollapseState => _view.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!_view.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (_view.IsExpanded)
            {
                _view._stage.Owner?.Close();
            }
        }
    }
}

/// <summary>
/// The slot that carries one card through the layout: it is moved and scaled about its top centre by the layout
/// function and draws the card's soft drop shadow (it only fades).
/// </summary>
internal sealed class WalletStackCardSlot : Control
{
    private readonly WalletStackStage _stage;
    private readonly MatrixTransform _transform = new();
    private double _shadow;

    public WalletStackCardSlot(WalletStackStage stage, WalletStackCardView view)
    {
        _stage = stage;
        View = view;
        IsHitTestVisible = true;
        ClipToBounds = false;
        RenderTransformOrigin = RelativePoint.TopLeft;
        RenderTransform = _transform;
        VisualChildren.Add(view);
        LogicalChildren.Add(view);
    }

    public WalletStackCardView View { get; }

    /// <summary>The pose applied last (tests read it).</summary>
    public WalletStackPose Pose { get; private set; }

    public void Apply(WalletStackPose pose, double cardWidth)
    {
        Pose = pose;
        var matrix = Matrix.CreateTranslation(-cardWidth / 2, 0) * Matrix.CreateScale(pose.Scale, pose.Scale) *
                     Matrix.CreateTranslation(cardWidth / 2, pose.Y);
        if (_transform.Matrix != matrix)
        {
            _transform.Matrix = matrix;
        }
        var opacity = Math.Clamp(pose.Opacity, 0, 1);
        if (Opacity != opacity)
        {
            Opacity = opacity;
        }
        IsHitTestVisible = opacity > 0.01;
        if (Math.Abs(_shadow - pose.Shadow) > 1e-4)
        {
            _shadow = pose.Shadow;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        View.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        View.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_shadow <= 0.001)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(_shadow, 0, 1)))
        {
            context.DrawRectangle(Brushes.Transparent, null, WalletStackStage.ShadowBody(Bounds.Size), _stage.SoftShadow);
        }
    }
}
