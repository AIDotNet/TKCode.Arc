using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The overlapping site marks in front of "8 sources" (Arc answer's mark stack): the first four sources as 20px round discs,
/// each after the first 4px over the one before (the later one on top), 1px in on each side. A disc is the source's 12px
/// mark on a Surface fill (SurfaceRaised in dark) inside a 1px Hairline, ringed 1px outside in <see cref="RingBrush"/> laid
/// over the page Background, so the ring always matches what is behind the stack and cuts the overlap cleanly; the row
/// binds <see cref="RingBrush"/> to its own fill, which fades to SurfaceMuted on hover.
/// <para>
/// While <see cref="IsFanned"/> (the row is hovered or has keyboard focus) the discs spread from the middle in 2.5px steps:
/// disc i of n moves (i − (n − 1) / 2) × 2.5 px, so four discs move −3.75, −1.25, 1.25 and 3.75. The move is a transition of
/// each disc's <c>RenderTransform</c> declared in its ControlTheme (Arc's 580ms CSS spring) at full motion; Reduced and Off
/// keep the stack still. The stack follows the source list as it grows (sources arriving while the model thinks).
/// Decorative: automation skips it (the row's label says how many sources there are).
/// </para>
/// </summary>
internal sealed class AiSourceStack : Control
{
    /// <summary>At most this many discs.</summary>
    public const int MaxMarks = 4;

    /// <summary>A disc's diameter.</summary>
    public const double DiscSize = 20;

    /// <summary>Each disc after the first starts this far over the one before.</summary>
    public const double Overlap = 4;

    /// <summary>The stack's padding at either end (Arc's 0 1).</summary>
    public const double EndPadding = 1;

    /// <summary>The site mark inside a disc.</summary>
    public const double MarkSize = 12;

    /// <summary>The fan's step between neighbours.</summary>
    public const double FanStep = 2.5;

    /// <summary>The sources whose first <see cref="MaxMarks"/> marks are shown; a collection that notifies is followed.</summary>
    public static readonly StyledProperty<IList<AiSource>?> SourcesProperty =
        AvaloniaProperty.Register<AiSourceStack, IList<AiSource>?>(nameof(Sources));

    /// <summary>Whether the discs spread apart (full motion only).</summary>
    public static readonly StyledProperty<bool> IsFannedProperty =
        AvaloniaProperty.Register<AiSourceStack, bool>(nameof(IsFanned));

    /// <summary>The ring's color over the page background (the row's current fill); null leaves the page background.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<AiSourceStack, IBrush?>(nameof(RingBrush));

    // The page background under the ring, and the probe that tells a dark theme.
    private static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<AiSourceStack, IBrush?>("PageBrush");

    private static readonly TransformOperations s_rest = Translate(0);

    private readonly List<AiSourceStackDisc> _discs = new(MaxMarks);
    private INotifyCollectionChanged? _observed;
    private bool _fanApplied;
    private bool _isDark;

    static AiSourceStack()
    {
        AffectsMeasure<AiSourceStack>(SourcesProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiSourceStack>(false);
        FocusableProperty.OverrideDefaultValue<AiSourceStack>(false);
        ClipToBoundsProperty.OverrideDefaultValue<AiSourceStack>(false);
        VerticalAlignmentProperty.OverrideDefaultValue<AiSourceStack>(Avalonia.Layout.VerticalAlignment.Center);
    }

    public AiSourceStack()
    {
        ArcTokens.Bind(this, PageBrushProperty, ArcDesignTokenKind.Background);
    }

    /// <inheritdoc cref="SourcesProperty"/>
    public IList<AiSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="IsFannedProperty"/>
    public bool IsFanned { get => GetValue(IsFannedProperty); set => SetValue(IsFannedProperty, value); }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <summary>The discs on screen, in order.</summary>
    internal IReadOnlyList<AiSourceStackDisc> Discs => _discs;

    /// <summary>The number of discs shown.</summary>
    internal int Count => _discs.Count;

    /// <summary>Whether the discs are spread now (at full motion while fanned).</summary>
    internal bool IsSpread => _fanApplied;

    /// <summary>The horizontal offset of disc <paramref name="index"/> of <paramref name="count"/> when fanned.</summary>
    internal static double FanOffset(int index, int count) => (index - (count - 1) / 2.0) * FanStep;

    /// <summary>The stack's width for <paramref name="count"/> discs (0 for none).</summary>
    internal static double WidthFor(int count) =>
        count <= 0 ? 0 : 2 * EndPadding + DiscSize + (count - 1) * (DiscSize - Overlap);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty)
        {
            Observe(this.IsAttachedToVisualTree() ? change.GetNewValue<IList<AiSource>?>() : null);
            Sync();
        }
        else if (change.Property == IsFannedProperty)
        {
            UpdateFan();
        }
        else if (change.Property == RingBrushProperty || change.Property == PageBrushProperty)
        {
            if (change.Property == PageBrushProperty)
            {
                // A dark page: the discs take SurfaceRaised and a Border hairline (Arc maps its hairline per theme).
                _isDark = change.GetNewValue<IBrush?>() is ISolidColorBrush page && Oklch.FromColor(page.Color).L < 0.5;
            }
            foreach (var disc in _discs)
            {
                Paint(disc);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Sources);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    /// <summary>The list is followed only while the stack is in a window, so a long-lived list does not hold it.</summary>
    private void Observe(IList<AiSource>? sources)
    {
        var next = sources as INotifyCollectionChanged;
        if (ReferenceEquals(_observed, next))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourcesChanged;
        }
        _observed = next;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnSourcesChanged;
        }
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    /// <summary>One disc per shown source, reused as the list changes; a new disc appears at once (Arc adds marks without motion).</summary>
    private void Sync()
    {
        var sources = Sources;
        var count = sources is null ? 0 : Math.Min(sources.Count, MaxMarks);
        while (_discs.Count > count)
        {
            var last = _discs[^1];
            _discs.RemoveAt(_discs.Count - 1);
            VisualChildren.Remove(last);
            LogicalChildren.Remove(last);
        }
        for (var i = 0; i < count; i++)
        {
            if (i == _discs.Count)
            {
                var disc = new AiSourceStackDisc { RenderTransform = s_rest };
                _discs.Add(disc);
                LogicalChildren.Add(disc);
                VisualChildren.Add(disc);
                Paint(disc);
            }
            _discs[i].Source = sources![i];
        }
        UpdateFan();
        InvalidateMeasure();
    }

    private void Paint(AiSourceStackDisc disc)
    {
        disc.PageBrush = GetValue(PageBrushProperty);
        disc.RingBrush = RingBrush;
        disc.SetDark(_isDark);
    }

    /// <summary>
    /// Spreads or gathers the discs. Only full motion spreads them (the assignment keeps the stack still at Reduced and
    /// Off); the spring is the discs' theme transition, so a change of mind mid-way turns around from where they are.
    /// </summary>
    private void UpdateFan()
    {
        var spread = IsFanned && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var count = _discs.Count;
        if (!spread && !_fanApplied)
        {
            return;
        }
        _fanApplied = spread;
        for (var i = 0; i < count; i++)
        {
            _discs[i].RenderTransform = spread ? Translate(FanOffset(i, count)) : s_rest;
        }
    }

    /// <summary>A horizontal move, built only when the fan changes (not per frame).</summary>
    private static TransformOperations Translate(double x)
    {
        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendTranslate(x, 0);
        return builder.Build();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var item = new Size(DiscSize, DiscSize);
        foreach (var disc in _discs)
        {
            disc.Measure(item);
        }
        return new Size(WidthFor(_discs.Count), _discs.Count == 0 ? 0 : DiscSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = Math.Max(0, (finalSize.Height - DiscSize) / 2);
        for (var i = 0; i < _discs.Count; i++)
        {
            _discs[i].Arrange(new Rect(EndPadding + i * (DiscSize - Overlap), y, DiscSize, DiscSize));
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}

/// <summary>
/// One disc of an <see cref="AiSourceStack"/>: the 1px ring outside (page background, then the row's fill over it), the 20px
/// fill, a 1px hairline just inside its edge (Arc's inset box-shadow) and the 12px site mark in the middle. Its fill and
/// hairline come from the ControlTheme (Surface and BorderSubtle; SurfaceRaised and Border when <c>:dark</c>), as does the
/// fan's spring.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(DarkPseudoClass)]
internal sealed class AiSourceStackDisc : Control
{
    /// <summary>The page is dark.</summary>
    public const string DarkPseudoClass = ":dark";

    /// <summary>The source whose mark is shown.</summary>
    public static readonly StyledProperty<AiSource?> SourceProperty =
        AvaloniaProperty.Register<AiSourceStackDisc, AiSource?>(nameof(Source));

    /// <summary>The disc's fill (theme).</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AiSourceStackDisc, IBrush?>(nameof(Fill));

    /// <summary>The 1px line just inside the disc's edge (theme).</summary>
    public static readonly StyledProperty<IBrush?> HairlineProperty =
        AvaloniaProperty.Register<AiSourceStackDisc, IBrush?>(nameof(Hairline));

    /// <summary>The page background under the ring (set by the stack).</summary>
    public static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<AiSourceStackDisc, IBrush?>(nameof(PageBrush));

    /// <summary>The ring's color laid over <see cref="PageBrush"/> (set by the stack).</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<AiSourceStackDisc, IBrush?>(nameof(RingBrush));

    private readonly AiSourceMark _mark = new() { Size = AiSourceStack.MarkSize };
    private IBrush? _hairlineBrush;
    private IPen? _hairlinePen;

    static AiSourceStackDisc()
    {
        AffectsRender<AiSourceStackDisc>(FillProperty, HairlineProperty, PageBrushProperty, RingBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiSourceStackDisc>(false);
        ClipToBoundsProperty.OverrideDefaultValue<AiSourceStackDisc>(false);
    }

    public AiSourceStackDisc()
    {
        VisualChildren.Add(_mark);
        LogicalChildren.Add(_mark);
    }

    /// <inheritdoc cref="SourceProperty"/>
    public AiSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <inheritdoc cref="HairlineProperty"/>
    public IBrush? Hairline { get => GetValue(HairlineProperty); set => SetValue(HairlineProperty, value); }

    /// <inheritdoc cref="PageBrushProperty"/>
    public IBrush? PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <summary>The mark inside.</summary>
    internal AiSourceMark Mark => _mark;

    /// <summary>Whether the theme's dark colors apply.</summary>
    internal void SetDark(bool dark) => PseudoClasses.Set(DarkPseudoClass, dark);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            _mark.Source = change.GetNewValue<AiSource?>();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _mark.Measure(new Size(AiSourceStack.MarkSize, AiSourceStack.MarkSize));
        return new Size(AiSourceStack.DiscSize, AiSourceStack.DiscSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = (AiSourceStack.DiscSize - AiSourceStack.MarkSize) / 2;
        _mark.Arrange(new Rect(inset, inset, AiSourceStack.MarkSize, AiSourceStack.MarkSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var radius = AiSourceStack.DiscSize / 2;
        var center = new Point(radius, radius);
        // The ring: 1px outside the disc, page background first, then the row's fill (transparent at rest) over it.
        if (PageBrush is { } page)
        {
            context.DrawEllipse(page, null, center, radius + 1, radius + 1);
        }
        if (RingBrush is { } ring)
        {
            context.DrawEllipse(ring, null, center, radius + 1, radius + 1);
        }
        if (Fill is { } fill)
        {
            context.DrawEllipse(fill, null, center, radius, radius);
        }
        if (Hairline is { } hairline)
        {
            if (!ReferenceEquals(hairline, _hairlineBrush))
            {
                // The pen is rebuilt only when the theme's brush changes.
                _hairlineBrush = hairline;
                _hairlinePen = new ImmutablePen(hairline.ToImmutable(), 1);
            }
            context.DrawEllipse(null, _hairlinePen, center, radius - 0.5, radius - 0.5);
        }
    }
}
