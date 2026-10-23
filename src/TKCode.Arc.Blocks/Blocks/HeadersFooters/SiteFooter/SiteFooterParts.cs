// Ported from Arc's free site-footer block (MIT, Copyright (c) 2026 Elia Kuratli): the parts of site-footer.tsx / .module.css.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// A link of the footer: text with an optional outward arrow (<see cref="SiteFooterLink.IsExternal"/>), or the brand name,
/// or the status (a dot and text). Hover uses <c>:hover</c>, set for mouse and pen only (Arc's
/// <c>(hover: hover) and (pointer: fine)</c>); the ring shows for keyboard focus only. Pressing raises the owning
/// footer's navigate event.
/// </summary>
[TemplatePart(RingPart, typeof(Border))]
[PseudoClasses(HoverPseudoClass)]
internal sealed class SiteFooterLinkButton : AvaloniaButton
{
    public const string RingPart = "PART_FocusRing";
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<SiteFooterLink?> LinkProperty =
        AvaloniaProperty.Register<SiteFooterLinkButton, SiteFooterLink?>(nameof(Link));

    public static readonly StyledProperty<SiteFooterNavigationKind> KindProperty =
        AvaloniaProperty.Register<SiteFooterLinkButton, SiteFooterNavigationKind>(nameof(Kind), SiteFooterNavigationKind.Column);

    public static readonly StyledProperty<SiteStatusTone> ToneProperty =
        AvaloniaProperty.Register<SiteFooterLinkButton, SiteStatusTone>(nameof(Tone));

    public static readonly DirectProperty<SiteFooterLinkButton, string> LabelProperty =
        AvaloniaProperty.RegisterDirect<SiteFooterLinkButton, string>(nameof(Label), o => o.Label);

    public static readonly DirectProperty<SiteFooterLinkButton, bool> IsExternalProperty =
        AvaloniaProperty.RegisterDirect<SiteFooterLinkButton, bool>(nameof(IsExternal), o => o.IsExternal);

    private string _label = string.Empty;
    private bool _isExternal;
    private SiteFooterLink? _watched;

    public SiteFooterLink? Link { get => GetValue(LinkProperty); set => SetValue(LinkProperty, value); }

    public SiteFooterNavigationKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public SiteStatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>The text shown.</summary>
    public string Label { get => _label; private set => SetAndRaise(LabelProperty, ref _label, value); }

    /// <summary>Whether the outward arrow shows.</summary>
    public bool IsExternal { get => _isExternal; private set => SetAndRaise(IsExternalProperty, ref _isExternal, value); }

    protected override Type StyleKeyOverride => typeof(SiteFooterLinkButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinkProperty)
        {
            Watch(change.GetNewValue<SiteFooterLink?>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Link);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unwatch();
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void Watch(SiteFooterLink? link)
    {
        Unwatch();
        Sync(link);
        if (link is not null && this.IsAttachedToVisualTree())
        {
            link.PropertyChanged += OnLinkChanged;
            _watched = link;
        }
    }

    private void Unwatch()
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnLinkChanged;
            _watched = null;
        }
    }

    private void OnLinkChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Sync(Link);

    private void Sync(SiteFooterLink? link)
    {
        Label = link?.Label ?? string.Empty;
        IsExternal = link?.IsExternal == true;
        // The accessible name is the text (the arrow is decorative), the status adds nothing but its text as well.
        AutomationProperties.SetName(this, Label);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Link is { } link && this.FindAncestorOfType<SiteFooter>() is { } footer)
        {
            footer.RaiseNavigate(link, Kind);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    /// <summary>A link with an address is a hyperlink for screen readers; one without is a button.</summary>
    private sealed class LinkPeer(SiteFooterLinkButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            string.IsNullOrEmpty(((SiteFooterLinkButton)Owner).Link?.Href) ? AutomationControlType.Button : AutomationControlType.Hyperlink;
    }
}

/// <summary>What a <see cref="SiteFooterLabel"/> is.</summary>
internal enum SiteFooterLabelKind
{
    /// <summary>A column title or the newsletter title: 14, medium, foreground.</summary>
    Heading,

    /// <summary>The line under the brand: 14, secondary, at most 320 wide.</summary>
    Tagline,

    /// <summary>The copyright: 14, muted.</summary>
    Copyright,

    /// <summary>The newsletter's message: 12 / 20, muted, danger or success.</summary>
    Message
}

/// <summary>A text of the footer, styled by <see cref="Kind"/> (and <see cref="Tone"/> for messages).</summary>
internal sealed class SiteFooterLabel : TextBlock
{
    public static readonly StyledProperty<SiteFooterLabelKind> KindProperty =
        AvaloniaProperty.Register<SiteFooterLabel, SiteFooterLabelKind>(nameof(Kind));

    public static readonly StyledProperty<SiteFooterMessageTone> ToneProperty =
        AvaloniaProperty.Register<SiteFooterLabel, SiteFooterMessageTone>(nameof(Tone));

    public SiteFooterLabelKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public SiteFooterMessageTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    protected override Type StyleKeyOverride => typeof(SiteFooterLabel);
}

/// <summary>
/// The brand of the footer: the mark (the arch by default, any content when <see cref="Mark"/> is set) and the name as a
/// link, 8 apart.
/// </summary>
internal sealed class SiteFooterBrand : TemplatedControl
{
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<SiteFooterBrand, object?>(nameof(Mark));

    public static readonly StyledProperty<SiteFooterLink?> BrandLinkProperty =
        AvaloniaProperty.Register<SiteFooterBrand, SiteFooterLink?>(nameof(BrandLink));

    static SiteFooterBrand()
    {
        FocusableProperty.OverrideDefaultValue<SiteFooterBrand>(false);
    }

    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public SiteFooterLink? BrandLink { get => GetValue(BrandLinkProperty); set => SetValue(BrandLinkProperty, value); }
}

/// <summary>
/// A row of groups that sit on one line, the first at the start and the last at the end (flex <c>space-between</c>),
/// while they fit side by side with <see cref="MinimumGap"/>; otherwise each takes its own line, left aligned, with
/// <see cref="LineGap"/> between them (flex-wrap, or <see cref="IsStacked"/> for a column).
/// </summary>
internal sealed class SiteFooterSplitRow : Panel
{
    public static readonly StyledProperty<double> MinimumGapProperty =
        AvaloniaProperty.Register<SiteFooterSplitRow, double>(nameof(MinimumGap), 24);

    public static readonly StyledProperty<double> LineGapProperty =
        AvaloniaProperty.Register<SiteFooterSplitRow, double>(nameof(LineGap), 12);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<SiteFooterSplitRow, bool>(nameof(IsStacked));

    private bool _single;

    static SiteFooterSplitRow()
    {
        AffectsMeasure<SiteFooterSplitRow>(MinimumGapProperty, LineGapProperty, IsStackedProperty);
    }

    /// <summary>The smallest space between two groups on one line.</summary>
    public double MinimumGap { get => GetValue(MinimumGapProperty); set => SetValue(MinimumGapProperty, value); }

    /// <summary>The space between lines when the groups wrap.</summary>
    public double LineGap { get => GetValue(LineGapProperty); set => SetValue(LineGapProperty, value); }

    /// <summary>Forces one group per line.</summary>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <summary>True while the groups share one line (tests read it).</summary>
    public bool IsSingleLine => _single;

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = new List<Control>();
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                visible.Add(child);
            }
        }
        var natural = 0.0;
        foreach (var child in visible)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            natural += child.DesiredSize.Width;
        }
        natural += Math.Max(0, visible.Count - 1) * MinimumGap;
        _single = !IsStacked && natural <= availableSize.Width + 0.01;
        var width = 0.0;
        var height = 0.0;
        if (_single)
        {
            foreach (var child in visible)
            {
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(double.IsInfinity(availableSize.Width) ? natural : availableSize.Width, height);
        }
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Measure(new Size(availableSize.Width, availableSize.Height));
            width = Math.Max(width, visible[i].DesiredSize.Width);
            height += visible[i].DesiredSize.Height + (i > 0 ? LineGap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = new List<Control>();
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                visible.Add(child);
            }
        }
        if (_single)
        {
            for (var i = 0; i < visible.Count; i++)
            {
                var size = visible[i].DesiredSize;
                var x = i == 0 ? 0 : i == visible.Count - 1 ? finalSize.Width - size.Width : (finalSize.Width - size.Width) / 2;
                visible[i].Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
            }
            return finalSize;
        }
        var y = 0.0;
        foreach (var child in visible)
        {
            child.Arrange(new Rect(0, y, Math.Min(child.DesiredSize.Width, finalSize.Width), child.DesiredSize.Height));
            y += child.DesiredSize.Height + LineGap;
        }
        return finalSize;
    }
}

/// <summary>
/// The big arch of the Logo layout: the top half of the arch in the brand gradient (each stroke fades from
/// <see cref="FromBrush"/> on its left to <see cref="ToBrush"/> on its right), fading out toward the bottom edge, and
/// rising in once when 30% of it is on screen (opacity 0 to 1, 40 px up, 900 ms enter).
/// </summary>
/// <remarks>
/// Code-driven because the reveal waits for the viewport and has to run once, which a ControlTheme transition cannot
/// express (it has no trigger for "scrolled into view"); the pictures are drawn, not templated, so the strokes follow the
/// width in one pass.
/// </remarks>
internal sealed class SiteFooterMark : Control
{
    /// <summary>The visible part of the viewBox: <c>0 3.5 64 30</c>.</summary>
    public const double ViewBoxTop = 3.5;

    public const double ViewBoxHeight = 30;

    /// <summary>Start of the entrance: how far below its place the arch begins (px).</summary>
    public const double EntranceOffset = 40;

    public static readonly StyledProperty<IBrush?> FromBrushProperty =
        AvaloniaProperty.Register<SiteFooterMark, IBrush?>(nameof(FromBrush));

    public static readonly StyledProperty<IBrush?> ToBrushProperty =
        AvaloniaProperty.Register<SiteFooterMark, IBrush?>(nameof(ToBrush));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<SiteFooterMark, double>(nameof(StrokeWidth), 3.4);

    private static Geometry[]? s_geometries;
    private static readonly double[] s_left = [9, 20];
    private static readonly double[] s_right = [55, 44];
    private static readonly IBrush s_mask = CreateMask();

    private readonly TranslateTransform _translate = new();
    private IPen[]? _pens;
    private ViewportVisibility? _watch;
    private MotionValue<double>? _reveal;
    private bool _revealed;

    static SiteFooterMark()
    {
        AffectsRender<SiteFooterMark>(FromBrushProperty, ToBrushProperty, StrokeWidthProperty);
        AffectsMeasure<SiteFooterMark>(StrokeWidthProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SiteFooterMark>(false);
        FocusableProperty.OverrideDefaultValue<SiteFooterMark>(false);
        ClipToBoundsProperty.OverrideDefaultValue<SiteFooterMark>(true);
    }

    public SiteFooterMark()
    {
        // Decorative: the brand name carries the meaning.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        OpacityMask = s_mask;
        RenderTransform = _translate;
    }

    /// <summary>The brand gradient's start (a solid brush; its color is used).</summary>
    public IBrush? FromBrush { get => GetValue(FromBrushProperty); set => SetValue(FromBrushProperty, value); }

    /// <summary>The brand gradient's end (a solid brush; its color is used).</summary>
    public IBrush? ToBrush { get => GetValue(ToBrushProperty); set => SetValue(ToBrushProperty, value); }

    public double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    /// <summary>True once the entrance has started or been skipped (tests read it).</summary>
    public bool IsRevealed => _revealed;

    /// <summary>The entrance progress 0..1 (tests read it).</summary>
    public double RevealProgress => _revealed && _reveal is null ? 1 : _reveal?.Value ?? 0;

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    private static IBrush CreateMask() => new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, Colors.Black),
            new ImmutableGradientStop(0.18, Colors.Black),
            new ImmutableGradientStop(0.96, Colors.Transparent),
            new ImmutableGradientStop(1, Colors.Transparent)
        ],
        opacity: 1, transform: null, transformOrigin: null, spreadMethod: GradientSpreadMethod.Pad,
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));

    private static Geometry[] Geometries()
    {
        if (s_geometries is null)
        {
            // The first two strokes of the shared arch; the third (the stem) lies below the visible part.
            var parts = SiteArcMark.PathData.Split('M', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            s_geometries = [Geometry.Parse("M" + parts[0]), Geometry.Parse("M" + parts[1])];
        }
        return s_geometries;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        return new Size(width, width * ViewBoxHeight / SiteArcMark.ViewBoxSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_revealed)
        {
            return;
        }
        if (ArcMotion.IsFull(this))
        {
            // Hidden until 30% is in view; before that the arch waits below its place.
            Opacity = 0;
            _translate.Y = EntranceOffset;
            _watch = new ViewportVisibility(this, 0.3, once: true);
            _watch.VisibilityChanged += OnVisible;
        }
        else
        {
            Skip();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watch?.Dispose();
        _watch = null;
        if (!_revealed)
        {
            // Not shown yet: it starts over (hidden, waiting) when it comes back.
            return;
        }
        _reveal?.Stop();
    }

    private void Skip()
    {
        _revealed = true;
        Opacity = 1;
        _translate.Y = 0;
    }

    private void OnVisible(object? sender, bool visible)
    {
        if (!visible || _revealed)
        {
            return;
        }
        _revealed = true;
        _watch?.Dispose();
        _watch = null;
        if (!ArcMotion.IsFull(this))
        {
            Skip();
            return;
        }
        // A tween (900 ms enter), not a spring: Arc declares a duration and the enter curve.
        _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
        _reveal.Changed += p =>
        {
            Opacity = p;
            _translate.Y = EntranceOffset * (1 - p);
        };
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(900), ArcEasings.Enter));
    }

    /// <summary>Starts the entrance as if the arch had scrolled into view (tests and programmatic use).</summary>
    public void Reveal() => OnVisible(this, true);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FromBrushProperty || change.Property == ToBrushProperty || change.Property == StrokeWidthProperty)
        {
            _pens = null;
        }
    }

    private static Color ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color ?? Colors.Transparent;

    private IPen[] Pens()
    {
        if (_pens is null)
        {
            _pens = new IPen[s_left.Length];
            for (var i = 0; i < _pens.Length; i++)
            {
                // Arc's gradient is relative to each stroke's own box, which for a path is its fill box: x 9 to 55 and 20 to 44.
                var brush = new ImmutableLinearGradientBrush(
                    [new ImmutableGradientStop(0, ColorOf(FromBrush)), new ImmutableGradientStop(1, ColorOf(ToBrush))],
                    opacity: 1, transform: null, transformOrigin: null, spreadMethod: GradientSpreadMethod.Pad,
                    startPoint: new RelativePoint(s_left[i], 0, RelativeUnit.Absolute), endPoint: new RelativePoint(s_right[i], 0, RelativeUnit.Absolute));
                _pens[i] = new ImmutablePen(brush, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            }
        }
        return _pens;
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }
        var scale = Bounds.Width / SiteArcMark.ViewBoxSize;
        var pens = Pens();
        var geometries = Geometries();
        using (context.PushTransform(Matrix.CreateTranslation(0, -ViewBoxTop) * Matrix.CreateScale(scale, scale)))
        {
            for (var i = 0; i < geometries.Length; i++)
            {
                context.DrawGeometry(null, pens[i], geometries[i]);
            }
        }
    }
}

/// <summary>
/// The borderless one-line input of the newsletter: text on a transparent background with a muted placeholder; the row
/// around it shows the focus, not the box itself.
/// </summary>
[TemplatePart(TextPresenterPart, typeof(TextPresenter))]
internal sealed class SiteFooterInput : AvaloniaTextBox
{
    public const string TextPresenterPart = "PART_TextPresenter";

    /// <summary>The newsletter is subscribed: the text turns secondary.</summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<SiteFooterInput, bool>(nameof(IsDone));

    /// <summary>The presenter, whose pre-edit text tells whether an input method is composing.</summary>
    public TextPresenter? Presenter { get; private set; }

    /// <inheritdoc cref="IsDoneProperty"/>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <summary>True while an input method is composing text (Enter belongs to it then).</summary>
    public bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(SiteFooterInput);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Presenter = e.NameScope.Find<TextPresenter>(TextPresenterPart);
    }
}

/// <summary>
/// The link columns: equal-width cells, <see cref="Columns"/> across, rows as tall as their tallest cell (CSS
/// <c>repeat(n, minmax(0, 1fr))</c> with <c>auto</c> rows), so a short second row does not stretch.
/// </summary>
internal sealed class SiteFooterColumnGrid : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<SiteFooterColumnGrid, int>(nameof(Columns), 3);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<SiteFooterColumnGrid, double>(nameof(ColumnGap), 24);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SiteFooterColumnGrid, double>(nameof(RowGap), 24);

    private double[] _rowHeights = [];

    static SiteFooterColumnGrid()
    {
        AffectsMeasure<SiteFooterColumnGrid>(ColumnsProperty, ColumnGapProperty, RowGapProperty);
    }

    /// <summary>Columns across.</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>The space between columns.</summary>
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <summary>The space between rows.</summary>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private double CellWidth(double width)
    {
        var columns = Math.Max(1, Columns);
        return double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, (width - ColumnGap * (columns - 1)) / columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = CellWidth(availableSize.Width);
        var rows = (Children.Count + columns - 1) / columns;
        _rowHeights = new double[rows];
        var widest = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(cell, double.PositiveInfinity));
            _rowHeights[i / columns] = Math.Max(_rowHeights[i / columns], Children[i].DesiredSize.Height);
            widest = Math.Max(widest, Children[i].DesiredSize.Width);
        }
        var height = _rowHeights.Sum() + Math.Max(0, rows - 1) * RowGap;
        var width = double.IsInfinity(availableSize.Width) ? widest * columns + ColumnGap * (columns - 1) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = CellWidth(finalSize.Width);
        var y = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            if (i > 0 && i % columns == 0)
            {
                y += _rowHeights[row - 1] + RowGap;
            }
            Children[i].Arrange(new Rect((i % columns) * (cell + ColumnGap), y, cell, _rowHeights[row]));
        }
        return finalSize;
    }
}
