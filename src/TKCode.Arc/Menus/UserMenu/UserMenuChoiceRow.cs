using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>One option of an inline user-menu switch: a Lucide glyph or a presence status.</summary>
internal readonly record struct UserMenuChoiceOption(string Label, Geometry? Glyph, UserMenuStatus? Status);

/// <summary>
/// An inline switch row of the user menu (Theme, Status). It is one stop in the menu: ArrowUp / ArrowDown pass over
/// it, ArrowLeft / ArrowRight choose within it, and choosing keeps the menu open. The pointer over it releases the
/// shared highlight instead of dragging it along (the switch is not a command row).
/// </summary>
internal sealed class UserMenuChoiceRow : MenuEntry
{
    private readonly TextBlock _label;

    public UserMenuChoiceRow(string label, object icon, IReadOnlyList<UserMenuChoiceOption> options)
    {
        Segments = new UserMenuSegments(options, label);
        _label = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        DockPanel.SetDock(Segments, Dock.Right);
        var panel = new DockPanel();
        panel.Children.Add(Segments);
        panel.Children.Add(_label);
        Header = panel;
        Icon = icon;
        Padding = new Thickness(8, 0, 4, 0);
        TextSearch.SetText(this, label);
        AutomationProperties.SetName(this, label);
        Segments.SelectionChanged += _ => UpdateHelpText();
        UpdateHelpText();
    }

    protected override Type StyleKeyOverride => typeof(MenuEntry);

    public UserMenuSegments Segments { get; }

    /// <summary>Sheet rows are 52px tall with 40px segments.</summary>
    private bool IsSheet => Variant == MenuSurfaceVariant.Sheet;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
        {
            Segments.IsSheet = IsSheet;
            ApplyRowHeight();
        }
    }

    internal override bool TracksPointerHighlight => false;

    internal override bool IsActivatable => false;

    internal override bool HandleMenuKey(KeyEventArgs e)
    {
        Segments.Step(e.Key == Key.Right ? 1 : -1);
        return true;
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyRowHeight();
    }

    private void ApplyRowHeight()
    {
        if (RowVisual is Border row && !ReferenceEquals(row, this))
        {
            row.MinHeight = IsSheet ? 52 : 36;
        }
    }

    private void UpdateHelpText() => AutomationProperties.SetHelpText(this, Segments.SelectedLabel);
}

/// <summary>
/// The compact segmented switch inside a <see cref="UserMenuChoiceRow"/>: a tinted track, a raised thumb that slides
/// on Arc's CSS spring, and one glyph per option (checked: foreground, hover: secondary, rest: muted). Drawn in
/// <see cref="Render"/>; the thumb slide is code-driven because it moves a drawn shape, not a template part.
/// </summary>
internal sealed class UserMenuSegments : Control
{
    private static readonly StyledProperty<IBrush?> ForegroundBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("ForegroundBrush");

    private static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("MutedBrush");

    private static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("SecondaryBrush");

    private static readonly StyledProperty<IBrush?> RaisedBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("RaisedBrush");

    private static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("BackgroundBrush");

    private static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("SuccessBrush");

    private static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("WarningBrush");

    private static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<UserMenuSegments, IBrush?>("DangerBrush");

    private const double Inset = 2;

    private readonly IReadOnlyList<UserMenuChoiceOption> _options;
    private readonly string _group;
    private readonly Pen?[] _glyphPens = new Pen?[3];
    private readonly IBrush?[] _glyphPenBrushes = new IBrush?[3];
    private MotionValue<double>? _thumb;
    private int _selected;
    private int _hover = -1;
    private bool _isSheet;
    private IBrush? _track;
    private IBrush? _thumbFill;
    private Pen? _thumbRing;
    private BoxShadows _thumbShadow;

    public UserMenuSegments(IReadOnlyList<UserMenuChoiceOption> options, string group)
    {
        _options = options;
        _group = group;
        Cursor = new Cursor(StandardCursorType.Hand);
        VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(this, ForegroundBrushProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, MutedBrushProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, SecondaryBrushProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, RaisedBrushProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, BackgroundBrushProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(this, SuccessBrushProperty, ArcDesignTokenKind.Success);
        ArcTokens.Bind(this, WarningBrushProperty, ArcDesignTokenKind.Warning);
        ArcTokens.Bind(this, DangerBrushProperty, ArcDesignTokenKind.Danger);
    }

    /// <summary>Raised when a person picks an option (pointer or arrow keys).</summary>
    public event Action<int>? SelectionChanged;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            var index = Math.Clamp(value, 0, _options.Count - 1);
            if (index == _selected)
            {
                return;
            }
            _selected = index;
            _thumb ??= CreateThumb();
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _ = _thumb.AnimateTo(index, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
            }
            else
            {
                _thumb.Jump(index);
            }
            InvalidateVisual();
        }
    }

    public string SelectedLabel => _options[_selected].Label;

    public bool IsSheet
    {
        get => _isSheet;
        set
        {
            if (_isSheet != value)
            {
                _isSheet = value;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    private double SegmentWidth => _isSheet ? 40 : 28;

    private double TrackHeight => _isSheet ? 34 : 26;

    /// <summary>Moves the choice by <paramref name="delta"/>, wrapping at the ends (ArrowLeft / ArrowRight).</summary>
    public void Step(int delta)
    {
        var count = _options.Count;
        Choose(((_selected + delta) % count + count) % count);
    }

    private void Choose(int index)
    {
        if (index == _selected)
        {
            return;
        }
        SelectedIndex = index;
        SelectionChanged?.Invoke(index);
    }

    private MotionValue<double> CreateThumb()
    {
        var value = new MotionValue<double>(this, _selected, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundBrushProperty || change.Property == RaisedBrushProperty ||
            change.Property == BackgroundBrushProperty)
        {
            UpdatePaints();
        }
        else if (change.Property == MutedBrushProperty || change.Property == SecondaryBrushProperty ||
                 change.Property == SuccessBrushProperty || change.Property == WarningBrushProperty ||
                 change.Property == DangerBrushProperty)
        {
            InvalidateVisual();
        }
    }

    private void UpdatePaints()
    {
        var foreground = (GetValue(ForegroundBrushProperty) as ISolidColorBrush)?.Color ?? Colors.Black;
        var raised = (GetValue(RaisedBrushProperty) as ISolidColorBrush)?.Color ?? Colors.White;
        var background = (GetValue(BackgroundBrushProperty) as ISolidColorBrush)?.Color ?? Colors.White;
        var dark = Oklch.FromColor(background).L < 0.5;
        _track = new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, 0.05));
        // Light: a raised chip with a hairline and a soft shadow. Dark: a lifted fill without a shadow.
        _thumbFill = new ImmutableSolidColorBrush(dark ? Oklch.MixOklab(raised, foreground, 0.2) : raised);
        _thumbRing = dark ? null : new Pen(new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, 0.09)), 0.5);
        _thumbShadow = dark
            ? default
            : new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 1.5, Color = Color.FromArgb(20, 0, 0, 0) });
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Inset * 2 + SegmentWidth * _options.Count, TrackHeight);

    private int SegmentAt(Point point)
    {
        var index = (int)Math.Floor((point.X - Inset) / SegmentWidth);
        return point.Y < 0 || point.Y > Bounds.Height || index < 0 || index >= _options.Count ? -1 : index;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = e.Pointer.Type == PointerType.Touch ? -1 : SegmentAt(e.GetPosition(this));
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover != -1)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var index = SegmentAt(e.GetPosition(this));
        if (index >= 0)
        {
            e.Handled = true;
            Choose(index);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_track is null)
        {
            UpdatePaints();
        }
        var segment = SegmentWidth;
        var height = TrackHeight;
        context.DrawRectangle(_track, null, new RoundedRect(new Rect(0, 0, Inset * 2 + segment * _options.Count, height), 7));
        var position = _thumb?.Value ?? _selected;
        var thumb = new RoundedRect(new Rect(Inset + position * segment, Inset, segment, height - Inset * 2), 5);
        context.DrawRectangle(_thumbFill, _thumbRing, thumb, _thumbShadow);
        var raised = GetValue(RaisedBrushProperty);
        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            var center = new Point(Inset + segment * (i + 0.5), height / 2);
            if (option.Status is { } status)
            {
                var fill = status switch
                {
                    UserMenuStatus.Busy => GetValue(DangerBrushProperty),
                    UserMenuStatus.Away => GetValue(WarningBrushProperty),
                    _ => GetValue(SuccessBrushProperty)
                };
                context.DrawEllipse(fill, null, center, 4.5, 4.5);
                if (raised is not null)
                {
                    UserMenuPresenceDot.DrawMarks(context, center, 9, raised,
                        status == UserMenuStatus.Away ? 1 : 0, status == UserMenuStatus.Busy ? 1 : 0);
                }
                continue;
            }
            if (option.Glyph is not { } glyph)
            {
                continue;
            }
            var brush = i == _selected
                ? GetValue(ForegroundBrushProperty)
                : i == _hover ? GetValue(SecondaryBrushProperty) : GetValue(MutedBrushProperty);
            if (brush is null)
            {
                continue;
            }
            var slot = i % _glyphPens.Length;
            if (_glyphPens[slot] is null || !ReferenceEquals(_glyphPenBrushes[slot], brush))
            {
                _glyphPens[slot] = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                _glyphPenBrushes[slot] = brush;
            }
            var size = _isSheet ? 16 : 14;
            var scale = size / 24.0;
            using (context.PushTransform(Matrix.CreateScale(scale, scale) *
                                         Matrix.CreateTranslation(center.X - size / 2, center.Y - size / 2)))
            {
                context.DrawGeometry(null, _glyphPens[slot], glyph);
            }
        }
    }
}
