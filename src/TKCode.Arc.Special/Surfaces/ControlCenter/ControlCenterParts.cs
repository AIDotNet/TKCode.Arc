using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A tile of the control center: a toggle-button surface with radius 22 that mixes 9% accent into its muted fill while on
/// (240ms), with a keyboard ring 2px outside. A click does not flip it by itself: the control center decides what the
/// tile means (start or end a session, notifications, presenting) and sets <see cref="ToggleButton.IsChecked"/>.
/// </summary>
internal sealed class ControlCenterTile : ToggleButton
{
    protected override void Toggle()
    {
    }
}

/// <summary>
/// The 36px icon circle of a tile and of the detail header: raised surface, resting shadow and secondary icon while off;
/// accent fill and accent-foreground icon while on (240ms).
/// </summary>
[PseudoClasses(OnPseudoClass)]
internal sealed class ControlCenterBadge : ContentControl
{
    public const string OnPseudoClass = ":on";

    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<ControlCenterBadge, bool>(nameof(IsOn));

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty)
        {
            PseudoClasses.Set(OnPseudoClass, IsOn);
        }
    }
}

/// <summary>
/// A round or pill button of the control center, styled by class: <c>chevron</c> (32, transparent, a 6% wash on hover),
/// <c>close</c> (32, raised surface, presses to 0.94), <c>primary</c> (text-colored pill) and <c>secondary</c> (surface
/// pill with a 1px ring); pills press to 0.97 over 160ms.
/// </summary>
internal sealed class ControlCenterButton : Button
{
    /// <summary>True when the last press came from the keyboard (Enter or Space), so focus moves can show the ring.</summary>
    public bool PressedByKeyboard { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        PressedByKeyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            PressedByKeyboard = true;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>
/// The tile wrapper: the tile and its optional chevron, scaled to 0.975 on a layout spring while the tile is pressed (the
/// whole tile shrinks, as in Arc). Reduced motion drops the scale.
/// <para>Why code: the press scale is a retargetable spring (press and release interrupt each other with velocity).</para>
/// </summary>
internal sealed class ControlCenterTileHost : Panel
{
    private readonly MatrixTransform _transform = new();
    private readonly MotionValue<double> _scale;

    public ControlCenterTileHost(ControlCenterTile tile, ControlCenterButton? chevron)
    {
        Tile = tile;
        Chevron = chevron;
        Children.Add(tile);
        if (chevron is not null)
        {
            Children.Add(chevron);
        }
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _scale.Changed += v => _transform.Matrix = Matrix.CreateScale(v, v);
        tile.PropertyChanged += (_, e) =>
        {
            if (e.Property == Button.IsPressedProperty)
            {
                var pressed = tile.IsPressed && ArcMotion.IsFull(this);
                _ = _scale.AnimateTo(pressed ? 0.975 : 1, MotionSpec.Spring(ControlCenterMath.LayoutSpring));
            }
        };
    }

    public ControlCenterTile Tile { get; }

    public ControlCenterButton? Chevron { get; }

    /// <summary>The press scale now.</summary>
    public double Scale => _scale.Value;
}

/// <summary>A segment of the mode switch: 12/500 text, secondary while unselected and the text color while selected (160ms).</summary>
internal sealed class ControlCenterModeButton : RadioButton
{
    protected override void Toggle()
    {
        // The mode bar owns the selection (one tab stop, arrow keys); a click asks it to select this segment.
        SelectRequested?.Invoke(this);
    }

    public event Action<ControlCenterModeButton>? SelectRequested;
}

/// <summary>
/// The mode switch of the Focus detail: a 36px pill well (text at 6%, 3px padding) with equal segments and a raised
/// highlight pill that glides to the selected segment on the layout spring (ω ≈ 12.5). It is a WAI-ARIA radio group: one
/// tab stop (the selected segment) and the arrow keys, Home and End move the selection (Arc puts every segment in the tab
/// order and ignores the arrows; this is a deliberate improvement).
/// </summary>
internal sealed class ControlCenterModeBar : Panel
{
    public static readonly StyledProperty<IBrush?> WellInkProperty =
        AvaloniaProperty.Register<ControlCenterModeBar, IBrush?>(nameof(WellInk));

    public static readonly StyledProperty<IBrush?> PillInkProperty =
        AvaloniaProperty.Register<ControlCenterModeBar, IBrush?>(nameof(PillInk));

    public static readonly StyledProperty<BoxShadows> PillShadowProperty =
        AvaloniaProperty.Register<ControlCenterModeBar, BoxShadows>(nameof(PillShadow));

    public IBrush? WellInk { get => GetValue(WellInkProperty); set => SetValue(WellInkProperty, value); }
    public IBrush? PillInk { get => GetValue(PillInkProperty); set => SetValue(PillInkProperty, value); }
    public BoxShadows PillShadow { get => GetValue(PillShadowProperty); set => SetValue(PillShadowProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WellInkProperty)
        {
            _well.Background = WellInk;
        }
        else if (change.Property == PillInkProperty)
        {
            _pill.Fill = PillInk;
        }
        else if (change.Property == PillShadowProperty)
        {
            _pill.BoxShadow = PillShadow;
        }
    }

    private readonly Border _well;
    private readonly GlidingIndicator _pill;
    private readonly List<ControlCenterModeButton> _buttons = [];
    private int _selected = -1;
    private bool _animateNext;
    private Rect _pillTarget;

    public ControlCenterModeBar()
    {
        _well = new Border { IsHitTestVisible = false }.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusPill);
        _pill = new GlidingIndicator { IsHitTestVisible = false }.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusPill);
        Children.Add(_well);
        Children.Add(_pill);
        Height = 36;
    }

    public Border Well => _well;

    public GlidingIndicator Pill => _pill;

    public IReadOnlyList<ControlCenterModeButton> Buttons => _buttons;

    public int SelectedIndex => _selected;

    /// <summary>The user picked a segment (click or keys).</summary>
    public event Action<int>? SelectionChanged;

    public void SetModes(IReadOnlyList<string> labels)
    {
        foreach (var button in _buttons)
        {
            Children.Remove(button);
        }
        _buttons.Clear();
        for (var i = 0; i < labels.Count; i++)
        {
            var button = new ControlCenterModeButton { Content = labels[i] };
            AutomationProperties.SetName(button, labels[i]);
            button.SelectRequested += OnSelectRequested;
            _buttons.Add(button);
            Children.Add(button);
        }
        _selected = -1;
    }

    private void OnSelectRequested(ControlCenterModeButton button)
    {
        var index = _buttons.IndexOf(button);
        if (index >= 0 && index != _selected)
        {
            Select(index, true);
            SelectionChanged?.Invoke(index);
        }
    }

    /// <summary>Selects a segment; <paramref name="animate"/> glides the pill, otherwise it jumps.</summary>
    public void Select(int index, bool animate)
    {
        _selected = index;
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsChecked = i == index;
            KeyboardNavigation.SetIsTabStop(_buttons[i], i == index || (index < 0 && i == 0));
        }
        _animateNext = animate;
        InvalidateArrange();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _buttons.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var current = Math.Max(0, _selected);
        int next;
        switch (e.Key)
        {
            case Key.Right or Key.Down:
                next = (current + 1) % _buttons.Count;
                break;
            case Key.Left or Key.Up:
                next = (current - 1 + _buttons.Count) % _buttons.Count;
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = _buttons.Count - 1;
                break;
            default:
                return;
        }
        e.Handled = true;
        _buttons[next].Focus(NavigationMethod.Directional);
        if (next != _selected)
        {
            Select(next, true);
            SelectionChanged?.Invoke(next);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsNaN(Height) ? 36 : Height;
        var count = Math.Max(1, _buttons.Count);
        var inner = double.IsInfinity(availableSize.Width) ? 300 : Math.Max(0, availableSize.Width - 6);
        foreach (var button in _buttons)
        {
            button.Measure(new Size(inner / count, height - 6));
        }
        _well.Measure(availableSize);
        _pill.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? inner + 6 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _well.Arrange(new Rect(finalSize));
        _pill.Arrange(new Rect(finalSize));
        var count = Math.Max(1, _buttons.Count);
        var width = (finalSize.Width - 6) / count;
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Arrange(new Rect(3 + i * width, 3, width, finalSize.Height - 6));
        }
        if (_selected >= 0 && _selected < _buttons.Count)
        {
            var target = new Rect(3 + _selected * width, 3, width, finalSize.Height - 6);
            if (target != _pillTarget || _animateNext)
            {
                var animate = _animateNext && _pill.HasTarget;
                _pillTarget = target;
                _pill.MoveTo(target, ControlCenterMath.LayoutSpring, animate);
            }
        }
        _animateNext = false;
        return finalSize;
    }
}

/// <summary>
/// The overlapping 28px avatars of the people who is always let through (20px apart, each ringed 2px in the detail's
/// surface): a supplied picture clipped to the circle, otherwise the initials on a gradient generated from the series
/// colors. Decorative: the names are in the sentence next to it.
/// </summary>
internal sealed class ControlCenterPeople : Control
{
    public static readonly StyledProperty<IBrush?> RingInkProperty =
        AvaloniaProperty.Register<ControlCenterPeople, IBrush?>(nameof(RingInk));

    public static readonly StyledProperty<IBrush?> Series1InkProperty =
        AvaloniaProperty.Register<ControlCenterPeople, IBrush?>(nameof(Series1Ink));

    public static readonly StyledProperty<IBrush?> Series2InkProperty =
        AvaloniaProperty.Register<ControlCenterPeople, IBrush?>(nameof(Series2Ink));

    public static readonly StyledProperty<IBrush?> Series3InkProperty =
        AvaloniaProperty.Register<ControlCenterPeople, IBrush?>(nameof(Series3Ink));

    public static readonly StyledProperty<IBrush?> Series4InkProperty =
        AvaloniaProperty.Register<ControlCenterPeople, IBrush?>(nameof(Series4Ink));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<ControlCenterPeople, FontFamily>(nameof(FontFamily), FontFamily.Default);

    private const double Diameter = 28;
    private const double Pitch = 20;

    private IReadOnlyList<ControlCenterPerson> _people = [];
    private readonly List<(IBrush Fill, FormattedText? Text)> _faces = [];
    private Pen? _ringPen;

    static ControlCenterPeople()
    {
        AffectsMeasure<ControlCenterPeople>(RingInkProperty, Series1InkProperty, Series2InkProperty, Series3InkProperty,
            Series4InkProperty, FontFamilyProperty);
        AffectsRender<ControlCenterPeople>(RingInkProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ControlCenterPeople>(false);
    }

    public IBrush? RingInk { get => GetValue(RingInkProperty); set => SetValue(RingInkProperty, value); }
    public IBrush? Series1Ink { get => GetValue(Series1InkProperty); set => SetValue(Series1InkProperty, value); }
    public IBrush? Series2Ink { get => GetValue(Series2InkProperty); set => SetValue(Series2InkProperty, value); }
    public IBrush? Series3Ink { get => GetValue(Series3InkProperty); set => SetValue(Series3InkProperty, value); }
    public IBrush? Series4Ink { get => GetValue(Series4InkProperty); set => SetValue(Series4InkProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IReadOnlyList<ControlCenterPerson> People
    {
        get => _people;
        set
        {
            _people = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private IBrush? Series(int i) => (i % 4) switch
    {
        0 => Series1Ink,
        1 => Series2Ink,
        2 => Series3Ink,
        _ => Series4Ink
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        // Built here (not while rendering): one gradient and one initials text per person.
        _faces.Clear();
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.SemiBold);
        for (var i = 0; i < _people.Count; i++)
        {
            var color = Series(i) is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            var fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.MixOklab(color, Colors.White, 0.28), 0),
                    new GradientStop(Oklch.MixOklab(color, Colors.Black, 0.12), 1)
                }
            }.ToImmutable();
            var initials = ControlCenterMath.Initials(_people[i].Name);
            var text = _people[i].Avatar is null && initials.Length > 0
                ? new FormattedText(initials, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, 10, Brushes.White)
                : null;
            _faces.Add((fill, text));
        }
        _ringPen = RingInk is null ? null : new Pen(RingInk, 2);
        return _people.Count == 0 ? default : new Size(Diameter + Pitch * (_people.Count - 1), Diameter);
    }

    public override void Render(DrawingContext context)
    {
        for (var i = 0; i < _people.Count && i < _faces.Count; i++)
        {
            var rect = new Rect(i * Pitch, 0, Diameter, Diameter);
            var center = rect.Center;
            var (fill, text) = _faces[i];
            if (_people[i].Avatar is { } image)
            {
                using (context.PushGeometryClip(new EllipseGeometry(rect)))
                {
                    context.DrawImage(image, rect);
                }
            }
            else
            {
                context.DrawEllipse(fill, null, center, Diameter / 2, Diameter / 2);
                if (text is not null)
                {
                    context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
                }
            }
            if (_ringPen is { } ring)
            {
                // The ring is drawn just outside the circle so neighbours appear cut out of one another.
                context.DrawEllipse(null, ring, center, Diameter / 2 + 1, Diameter / 2 + 1);
            }
        }
    }
}

/// <summary>The channel switches of the Notifications detail; dimmed to 0.45 (240ms) while all notifications are off, still usable.</summary>
[PseudoClasses(DimmedPseudoClass)]
internal sealed class ControlCenterChannelList : StackPanel
{
    public const string DimmedPseudoClass = ":dimmed";

    public bool IsDimmed
    {
        get => PseudoClasses.Contains(DimmedPseudoClass);
        set => PseudoClasses.Set(DimmedPseudoClass, value);
    }
}

/// <summary>
/// The detail's surface while it grows out of a tile and back: its background and the clip of its content are a rounded
/// rectangle (constant 22px radius) at <see cref="FrameRect"/>, which the control center moves from the tile's box to the
/// detail's box; the content itself stays laid out at the final size, so the frame reveals it without stretching it (Arc
/// projects a scale and corrects the radius every frame; moving the box gives the same picture without the distortion).
/// </summary>
internal sealed class ControlCenterMorphFrame : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<ControlCenterMorphFrame>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<ControlCenterMorphFrame>();

    // Two clip geometries used in turn: the next one is updated off-screen and assigned, so a frame allocates nothing.
    private readonly RectangleGeometry[] _clips = [new RectangleGeometry(), new RectangleGeometry()];
    private int _clipIndex;
    private Rect? _frame;

    static ControlCenterMorphFrame()
    {
        AffectsRender<ControlCenterMorphFrame>(BackgroundProperty, CornerRadiusProperty);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The box drawn now, in the frame's coordinates (the whole frame when not set).</summary>
    public Rect FrameRect => _frame ?? new Rect(Bounds.Size);

    /// <summary>Moves the drawn box; null rests on the whole frame.</summary>
    public void SetFrame(Rect? frame)
    {
        _frame = frame;
        UpdateClip();
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateClip();
    }

    private void UpdateClip()
    {
        if (Child is not { } child)
        {
            return;
        }
        if (_frame is null)
        {
            // At rest the content stays inside the rounded box by its own padding: no clip (a clip also costs hit testing).
            child.Clip = null;
            return;
        }
        var rect = FrameRect;
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(rect.Width, rect.Height) / 2);
        _clipIndex ^= 1;
        var clip = _clips[_clipIndex];
        clip.Rect = rect;
        clip.RadiusX = radius;
        clip.RadiusY = radius;
        child.Clip = clip;
        child.InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Background is not { } background)
        {
            return;
        }
        var rect = FrameRect;
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(rect.Width, rect.Height) / 2);
        context.DrawRectangle(background, null, rect, radius, radius);
    }
}

/// <summary>A cached transform for the shared header icon and title while they fly between the tile and the header.</summary>
internal sealed class ControlCenterFlight
{
    private readonly TranslateTransform _transform = new();

    public ControlCenterFlight(Control target)
    {
        Target = target;
        target.RenderTransform = _transform;
    }

    public Control Target { get; }

    /// <summary>Where the target is drawn from at progress 0, relative to its own place.</summary>
    public Vector From { get; set; }

    public void Apply(double progress)
    {
        _transform.X = From.X * (1 - progress);
        _transform.Y = From.Y * (1 - progress);
    }
}

/// <summary>Extension used by the parts: an immutable color brush of an ink at an opacity.</summary>
internal static class ControlCenterInk
{
    public static IBrush? WithOpacity(IBrush? brush, double opacity) =>
        brush is ISolidColorBrush solid ? new ImmutableSolidColorBrush(solid.Color, opacity * solid.Opacity) : brush;
}

/// <summary>
/// The panel's surface: raised fill, 1px border, radius 30 and the floating shadow; flush (a viewport of 455 or less)
/// drops all four so the panel melts into the page.
/// </summary>
[PseudoClasses(FlushPseudoClass)]
internal sealed class ControlCenterSurface : Border
{
    public const string FlushPseudoClass = ":flush";

    public ControlCenterSurface()
    {
        IsHitTestVisible = false;
    }

    public bool IsFlush
    {
        get => PseudoClasses.Contains(FlushPseudoClass);
        set => PseudoClasses.Set(FlushPseudoClass, value);
    }
}

/// <summary>The scrim over the tiles while a detail is open: the raised surface at 55%, no blur, the panel's radius.</summary>
[PseudoClasses(FlushPseudoClass)]
internal sealed class ControlCenterScrim : Border
{
    public const string FlushPseudoClass = ":flush";

    public bool IsFlush
    {
        get => PseudoClasses.Contains(FlushPseudoClass);
        set => PseudoClasses.Set(FlushPseudoClass, value);
    }
}
