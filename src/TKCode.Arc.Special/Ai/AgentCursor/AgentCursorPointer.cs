using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A labeled multiplayer cursor of an <see cref="AgentCursor"/>: an arrow in the cursor's color with a light outline, its tip
/// at the control's origin, and a tag beside it with the name (and, for a person, an initials disc). The tag sits right of
/// the arrow, or left of it near the card's right edge. Decorative: hidden from assistive tech and from the pointer.
/// </summary>
internal sealed class AgentCursorPointer : Control
{
    /// <summary>The name on the tag.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AgentCursorPointer, string>(nameof(Label), string.Empty);

    /// <summary>The initials in the tag's disc; none for the agent.</summary>
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<AgentCursorPointer, string?>(nameof(Initials));

    /// <summary>The cursor's color.</summary>
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<AgentCursorPointer, Color>(nameof(Color), Colors.Gray);

    /// <summary>Whether the tag sits left of the arrow.</summary>
    public static readonly StyledProperty<bool> TagOnLeftProperty =
        AvaloniaProperty.Register<AgentCursorPointer, bool>(nameof(TagOnLeft));

    /// <summary>The arrow's outline color.</summary>
    public static readonly StyledProperty<IBrush?> OutlineBrushProperty =
        AvaloniaProperty.Register<AgentCursorPointer, IBrush?>(nameof(OutlineBrush));

    /// <summary>The arrow's outline width.</summary>
    public static readonly StyledProperty<double> OutlineWidthProperty =
        AvaloniaProperty.Register<AgentCursorPointer, double>(nameof(OutlineWidth), 1.5);

    /// <summary>The tag's font family.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<AgentCursorPointer, FontFamily>(nameof(FontFamily), FontFamily.Default);

    /// <summary>The tag's font size.</summary>
    public static readonly StyledProperty<double> TagFontSizeProperty =
        AvaloniaProperty.Register<AgentCursorPointer, double>(nameof(TagFontSize), 12);

    /// <summary>The tag's height.</summary>
    public static readonly StyledProperty<double> TagHeightProperty =
        AvaloniaProperty.Register<AgentCursorPointer, double>(nameof(TagHeight), 20);

    /// <summary>The tag's side padding.</summary>
    public static readonly StyledProperty<double> TagPaddingProperty =
        AvaloniaProperty.Register<AgentCursorPointer, double>(nameof(TagPadding), 7);

    /// <summary>The tag's corner radius (its top-left value is used).</summary>
    public static readonly StyledProperty<CornerRadius> TagCornerRadiusProperty =
        AvaloniaProperty.Register<AgentCursorPointer, CornerRadius>(nameof(TagCornerRadius), new CornerRadius(6));

    /// <summary>The initials disc's size.</summary>
    public static readonly StyledProperty<double> PortraitSizeProperty =
        AvaloniaProperty.Register<AgentCursorPointer, double>(nameof(PortraitSize), 14);

    /// <summary>The tag's text on a dark color.</summary>
    public static readonly StyledProperty<IBrush?> LightTextProperty =
        AvaloniaProperty.Register<AgentCursorPointer, IBrush?>(nameof(LightText), Brushes.White);

    /// <summary>The tag's text on a light color.</summary>
    public static readonly StyledProperty<IBrush?> DarkTextProperty =
        AvaloniaProperty.Register<AgentCursorPointer, IBrush?>(nameof(DarkText), Brushes.Black);

    /// <summary>The arrow (Lucide-like pointer, 12 × 18), tip at the origin.</summary>
    private static readonly Geometry s_arrow = Geometry.Parse("M0,0 L0,15.2 L4.1,11.4 L6.8,17.6 L9.4,16.5 L6.8,10.4 L12,10.4 Z");

    /// <summary>Where the tag starts, down and right of the tip.</summary>
    private const double TagLeft = 11;

    private const double TagTop = 15;

    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private FormattedText? _label;
    private FormattedText? _initials;

    static AgentCursorPointer()
    {
        AffectsMeasure<AgentCursorPointer>(LabelProperty, InitialsProperty, FontFamilyProperty, TagFontSizeProperty, TagHeightProperty,
            TagPaddingProperty, PortraitSizeProperty);
        AffectsRender<AgentCursorPointer>(ColorProperty, TagOnLeftProperty, OutlineBrushProperty, OutlineWidthProperty,
            TagCornerRadiusProperty, LightTextProperty, DarkTextProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AgentCursorPointer>(false);
        FocusableProperty.OverrideDefaultValue<AgentCursorPointer>(false);
    }

    public AgentCursorPointer()
    {
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="InitialsProperty"/>
    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    /// <inheritdoc cref="ColorProperty"/>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <inheritdoc cref="TagOnLeftProperty"/>
    public bool TagOnLeft { get => GetValue(TagOnLeftProperty); set => SetValue(TagOnLeftProperty, value); }

    /// <inheritdoc cref="OutlineBrushProperty"/>
    public IBrush? OutlineBrush { get => GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }

    /// <inheritdoc cref="OutlineWidthProperty"/>
    public double OutlineWidth { get => GetValue(OutlineWidthProperty); set => SetValue(OutlineWidthProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="TagFontSizeProperty"/>
    public double TagFontSize { get => GetValue(TagFontSizeProperty); set => SetValue(TagFontSizeProperty, value); }

    /// <inheritdoc cref="TagHeightProperty"/>
    public double TagHeight { get => GetValue(TagHeightProperty); set => SetValue(TagHeightProperty, value); }

    /// <inheritdoc cref="TagPaddingProperty"/>
    public double TagPadding { get => GetValue(TagPaddingProperty); set => SetValue(TagPaddingProperty, value); }

    /// <inheritdoc cref="TagCornerRadiusProperty"/>
    public CornerRadius TagCornerRadius { get => GetValue(TagCornerRadiusProperty); set => SetValue(TagCornerRadiusProperty, value); }

    /// <inheritdoc cref="PortraitSizeProperty"/>
    public double PortraitSize { get => GetValue(PortraitSizeProperty); set => SetValue(PortraitSizeProperty, value); }

    /// <inheritdoc cref="LightTextProperty"/>
    public IBrush? LightText { get => GetValue(LightTextProperty); set => SetValue(LightTextProperty, value); }

    /// <inheritdoc cref="DarkTextProperty"/>
    public IBrush? DarkText { get => GetValue(DarkTextProperty); set => SetValue(DarkTextProperty, value); }

    /// <summary>Where the tip is in the overlay (the render translation).</summary>
    public Point Position => new(_translate.X, _translate.Y);

    /// <summary>The cursor's presence scale (it grows out of the avatar).</summary>
    public double Scale => _scale.ScaleX;

    /// <summary>The tag's width (the arrow is about 12 wide).</summary>
    public double TagWidth
    {
        get
        {
            EnsureText();
            var width = TagPadding * 2 + (_label?.Width ?? 0);
            if (_initials is not null)
            {
                width += PortraitSize + 5 - TagPadding / 2;
            }
            return Math.Ceiling(width);
        }
    }

    /// <summary>Moves the tip to <paramref name="point"/> without layout.</summary>
    public void MoveTo(Point point)
    {
        _translate.X = point.X;
        _translate.Y = point.Y;
    }

    /// <summary>Sets the presence: opacity and a scale about the tip.</summary>
    public void SetPresence(double presence, bool scales)
    {
        Opacity = Math.Clamp(presence, 0, 1);
        var scale = scales ? 0.5 + 0.5 * Math.Clamp(presence, 0, 1) : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        IsVisible = presence > 0.001;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == InitialsProperty || change.Property == FontFamilyProperty
            || change.Property == TagFontSizeProperty || change.Property == ColorProperty || change.Property == LightTextProperty
            || change.Property == DarkTextProperty || change.Property == PortraitSizeProperty)
        {
            _label = null;
            _initials = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(TagLeft + TagWidth, TagTop + TagHeight);

    private IBrush TextBrush()
    {
        var c = Color;
        // Relative luminance: light colors take dark text.
        var luminance = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
        return (luminance > 0.62 ? DarkText : LightText) ?? Brushes.White;
    }

    private void EnsureText()
    {
        if (_label is not null)
        {
            return;
        }
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium);
        var brush = TextBrush();
        _label = new FormattedText(Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, TagFontSize, brush);
        _initials = string.IsNullOrEmpty(Initials)
            ? null
            : new FormattedText(Initials, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold), Math.Max(6, PortraitSize * 0.5), new ImmutableSolidColorBrush(Color));
    }

    public override void Render(DrawingContext context)
    {
        EnsureText();
        var fill = new ImmutableSolidColorBrush(Color);
        var outline = OutlineBrush is { } o ? new Pen(o, OutlineWidth, lineJoin: PenLineJoin.Round) : null;
        context.DrawGeometry(fill, outline, s_arrow);

        var width = TagWidth;
        var height = TagHeight;
        var left = TagOnLeft ? -width + 2 : TagLeft;
        var tag = new Rect(left, TagTop, width, height);
        var radius = Math.Min(TagCornerRadius.TopLeft, height / 2);
        context.DrawRectangle(fill, null, new RoundedRect(tag, radius));
        var x = tag.X + TagPadding;
        if (_initials is not null)
        {
            var size = PortraitSize;
            var disc = new Rect(tag.X + (height - size) / 2, tag.Y + (height - size) / 2, size, size);
            context.DrawEllipse(TextBrush(), null, disc);
            context.DrawText(_initials, new Point(disc.X + (size - _initials.Width) / 2, disc.Y + (size - _initials.Height) / 2));
            x = disc.Right + 5;
        }
        if (_label is not null)
        {
            context.DrawText(_label, new Point(x, tag.Y + (height - _label.Height) / 2));
        }
    }
}

/// <summary>
/// Moves one <see cref="AgentCursorPointer"/>: flights on a gentle quadratic arc with an ease-in-out curve, 280 to 600ms by
/// distance (a <see cref="MotionValue{T}"/> of the arc's progress); a position spring for short follows (the caret while
/// text streams) and the teammate's idle drift (a <see cref="MotionValue{T}"/> of the point, keeping its velocity when
/// retargeted); and the presence (scale and fade) as it comes out of or goes back into its avatar. Code-driven because the
/// path and its target are measured from live text, which a ControlTheme transition cannot express. Below full motion the
/// cursor jumps to each target. Nothing re-lays out: the pointer moves by its render transform.
/// </summary>
internal sealed class AgentCursorFlight
{
    /// <summary>The follow and drift spring: bounce-free, 0.25s.</summary>
    private static readonly SpringSpec s_follow = SpringSpec.FromVisual(0.25, 0);

    /// <summary>The idle drift spring: slow and soft.</summary>
    private static readonly SpringSpec s_drift = SpringSpec.FromVisual(1.1, 0);

    private readonly Control _owner;
    private readonly MotionValue<double> _progress;
    private readonly MotionValue<Point> _point;
    private readonly MotionValue<double> _presence;
    private Point _from;
    private Point _to;
    private Point _control;
    private Point _position;

    public AgentCursorFlight(Control owner, AgentCursorPointer pointer)
    {
        _owner = owner;
        Pointer = pointer;
        _progress = new MotionValue<double>(owner, 1, MotionVectors.Double);
        _progress.Changed += OnProgress;
        _point = new MotionValue<Point>(owner, default, MotionVectors.Point);
        _point.Changed += Place;
        _presence = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _presence.Changed += p => Pointer.SetPresence(p, Full);
        pointer.SetPresence(0, false);
    }

    /// <summary>Raised whenever the tip moves, with its new position.</summary>
    public event Action<Point>? Moved;

    public AgentCursorPointer Pointer { get; }

    /// <summary>Where the tip is now.</summary>
    public Point Position => _position;

    /// <summary>Where the current or last move ends.</summary>
    public Point Target { get; private set; }

    /// <summary>Recomputes where a resting cursor belongs (it re-anchors when the card resizes).</summary>
    public Func<Point?>? Anchor { get; set; }

    /// <summary>Whether the cursor is on screen.</summary>
    public bool IsShown => _presence.Target > 0.5;

    /// <summary>Whether an arc flight is running.</summary>
    public bool IsFlying => _progress.IsAnimating;

    /// <summary>Whether a flight or a follow is running.</summary>
    public bool IsMoving => _progress.IsAnimating || _point.IsAnimating;

    /// <summary>The stage width, so tags flip left of the arrow near the right edge.</summary>
    public double StageWidth { get; set; } = double.PositiveInfinity;

    private bool Full => ArcMotion.IsFull(_owner);

    /// <summary>Flies to <paramref name="target"/> on the arc; returns the flight's length in seconds at <paramref name="speed"/>.</summary>
    public double FlyTo(Point target, double speed)
    {
        Target = target;
        _from = _position;
        _to = target;
        var distance = Distance(_position, target);
        var seconds = AgentCursorText.FlightSeconds(distance) / speed;
        _point.Stop();
        if (!Full || distance < 0.5)
        {
            _progress.Jump(1);
            Place(target);
            return seconds;
        }
        // A gentle bow: the control point sits off the chord's middle by 12% of its length (at most 48px), on the upper side.
        var mid = new Point((_from.X + _to.X) / 2, (_from.Y + _to.Y) / 2);
        var dx = _to.X - _from.X;
        var dy = _to.Y - _from.Y;
        var normal = new Vector(-dy, dx) / Math.Max(distance, 0.001);
        if (normal.Y > 0)
        {
            normal = -normal;
        }
        _control = mid + normal * Math.Min(distance * 0.12, 48);
        _progress.Jump(0);
        _ = _progress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(seconds), ArcEasings.InOut));
        return seconds;
    }

    /// <summary>Glides a short way to <paramref name="target"/> on the position spring (keeps its velocity when retargeted).</summary>
    public void Follow(Point target, bool drift = false)
    {
        Target = target;
        _to = target;
        if (!Full)
        {
            _progress.Jump(1);
            Place(target);
            return;
        }
        if (_progress.IsAnimating)
        {
            _progress.Stop();
        }
        if (!_point.IsAnimating)
        {
            _point.Jump(_position);
        }
        _ = _point.AnimateTo(target, MotionSpec.Spring(drift ? s_drift : s_follow));
    }

    /// <summary>Puts the tip at <paramref name="target"/> at once.</summary>
    public void Jump(Point target)
    {
        Target = target;
        _to = target;
        _point.Stop();
        _progress.Jump(1);
        Place(target);
    }

    /// <summary>Comes out of (or goes back into) the avatar: scale and fade over 200ms.</summary>
    public void SetShown(bool shown)
    {
        var target = shown ? 1.0 : 0.0;
        if (Math.Abs(_presence.Target - target) < 0.001 && !_presence.IsAnimating)
        {
            if (Math.Abs(_presence.Value - target) < 0.001)
            {
                return;
            }
        }
        if (ArcMotion.GetEffectiveLevel(_owner) == ArcMotionLevel.Off)
        {
            _presence.Jump(target);
            return;
        }
        _ = _presence.AnimateTo(target, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), shown ? ArcEasings.Enter : ArcEasings.Exit));
    }

    /// <summary>Puts the cursor where its anchor says, if it rests (the card resized).</summary>
    public void Reanchor()
    {
        if (IsMoving || Anchor?.Invoke() is not { } point)
        {
            return;
        }
        Jump(point);
    }

    private void OnProgress(double t)
    {
        if (t >= 1)
        {
            Place(_to);
            return;
        }
        var u = 1 - t;
        var x = u * u * _from.X + 2 * u * t * _control.X + t * t * _to.X;
        var y = u * u * _from.Y + 2 * u * t * _control.Y + t * t * _to.Y;
        Place(new Point(x, y));
    }

    private void Place(Point point)
    {
        _position = point;
        Pointer.MoveTo(point);
        var flip = point.X + 11 + Pointer.TagWidth + 8 > StageWidth;
        if (Pointer.TagOnLeft != flip)
        {
            Pointer.TagOnLeft = flip;
        }
        Moved?.Invoke(point);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
