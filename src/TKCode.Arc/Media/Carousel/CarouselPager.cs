using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The page indicator of a <see cref="Carousel"/> (Arc <c>role="tablist" aria-label="Choose a slide"</c>). Each dot
/// grows toward a pill as the track nears its slide, so the pill hands over continuously instead of hopping, and the
/// active pill drains from the right while rotation runs. All dots are drawn in this panel's Render from the
/// continuous track position; the tab children are transparent hit and focus targets laid out at their resting
/// positions (re-laid out only when the selection changes).
/// </summary>
internal sealed class CarouselPager : Control
{
    public const double TabHeight = 36;

    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<CarouselPager, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<IBrush?> DotHoverBrushProperty =
        AvaloniaProperty.Register<CarouselPager, IBrush?>(nameof(DotHoverBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<CarouselPager, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> DrainBrushProperty =
        AvaloniaProperty.Register<CarouselPager, IBrush?>(nameof(DrainBrush));

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<CarouselPager, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<CarouselPager, bool>(nameof(IsCompact));

    private readonly List<CarouselPagerTab> _tabs = new();
    private MotionValue<double>? _hover;
    private int _hoverIndex = -1;
    private int _selected;
    private double _progress;

    static CarouselPager()
    {
        AffectsRender<CarouselPager>(DotBrushProperty, DotHoverBrushProperty, FillBrushProperty, DrainBrushProperty);
        AffectsMeasure<CarouselPager>(IsCompactProperty);
    }

    public CarouselPager()
    {
        AutomationProperties.SetName(this, "Choose a slide");
        ClipToBounds = true;
    }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public IBrush? DotHoverBrush { get => GetValue(DotHoverBrushProperty); set => SetValue(DotHoverBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public IBrush? DrainBrush { get => GetValue(DrainBrushProperty); set => SetValue(DrainBrushProperty, value); }

    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    /// <summary>Phone widths tighten the tab padding from 5px to 4px.</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    internal Carousel? Owner { get; set; }

    internal IReadOnlyList<CarouselPagerTab> Tabs => _tabs;

    private double Pad => IsCompact ? 4 : 5;

    /// <summary>Matches the tabs to the slide count and the selected slide.</summary>
    public void Sync(int count, int selected, Func<int, string> label)
    {
        while (_tabs.Count < count)
        {
            var tab = new CarouselPagerTab { Pager = this, Index = _tabs.Count };
            _tabs.Add(tab);
            LogicalChildren.Add(tab);
            VisualChildren.Add(tab);
        }
        while (_tabs.Count > count)
        {
            var tab = _tabs[^1];
            _tabs.RemoveAt(_tabs.Count - 1);
            VisualChildren.Remove(tab);
            LogicalChildren.Remove(tab);
        }
        _selected = Math.Clamp(selected, 0, Math.Max(0, count - 1));
        for (var i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            tab.SetSelected(i == _selected);
            AutomationProperties.SetName(tab, label(i));
            AutomationProperties.SetPositionInSet(tab, i + 1);
            AutomationProperties.SetSizeOfSet(tab, count);
        }
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    public void SetProgress(double progress)
    {
        if (progress == _progress)
        {
            return;
        }
        _progress = progress;
        InvalidateVisual();
    }

    internal void OnTabHover(CarouselPagerTab tab, bool over)
    {
        _hover ??= CreateHoverMotion();
        var full = ArcMotion.IsFull(this);
        if (over)
        {
            _hoverIndex = tab.Index;
            _hover.Jump(0);
            if (full)
            {
                _ = _hover.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _hover.Jump(1);
            }
        }
        else if (_hoverIndex == tab.Index)
        {
            if (full)
            {
                _ = _hover.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _hover.Jump(0);
            }
        }
    }

    private MotionValue<double> CreateHoverMotion()
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += _ => InvalidateVisual();
        return motion;
    }

    private double TabWidth(int index) => (index == _selected ? CarouselMath.Pill : CarouselMath.Dot) + 2 * Pad;

    private double TotalWidth => _tabs.Count == 0 ? 0 : _tabs.Count * (CarouselMath.Dot + 2 * Pad) + CarouselMath.Pill - CarouselMath.Dot;

    /// <summary>
    /// Long carousels have more dots than fit: the row then scrolls so the pill stays centered (clamped at the ends).
    /// </summary>
    internal double ScrollFor(double progress, double viewWidth)
    {
        var total = TotalWidth;
        if (viewWidth <= 0 || total <= viewWidth)
        {
            return 0;
        }
        var pad = Pad;
        var rest = CarouselMath.Dot + 2 * pad;
        var p = Math.Clamp(progress, 0, _tabs.Count - 1);
        var i = Math.Floor(p);
        var f = p - i;
        var grow = CarouselMath.Pill - CarouselMath.Dot;
        var x = i * rest;
        var w = CarouselMath.Dot + grow * (1 - f);
        var next = CarouselMath.Dot + grow * f;
        var center = (1 - f) * (x + pad + w / 2) + f * (x + w + 2 * pad + pad + next / 2);
        return Math.Clamp(center - viewWidth / 2, 0, total - viewWidth);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var tabWidth = TabWidth(i);
            _tabs[i].Measure(new Size(tabWidth, TabHeight));
            width += tabWidth;
        }
        if (double.IsFinite(availableSize.Width))
        {
            width = Math.Min(width, availableSize.Width);
        }
        return new Size(width, TabHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = -ScrollFor(_selected, finalSize.Width);
        var top = (finalSize.Height - TabHeight) / 2;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var width = TabWidth(i);
            _tabs[i].Arrange(new Rect(x, top, width, TabHeight));
            x += width;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var count = _tabs.Count;
        if (count == 0 || DotBrush is not { } dot)
        {
            return;
        }
        var last = count - 1;
        var pad = Pad;
        var view = Bounds.Width;
        var y = (Bounds.Height - CarouselMath.Dot) / 2;
        var hoverMix = _hover?.Value ?? 0;
        var scroll = ScrollFor(_progress, view);
        var fadeStart = scroll > 0;
        var fadeEnd = scroll < TotalWidth - view - 0.5;
        var x = -scroll;
        for (var i = 0; i < count; i++)
        {
            var emphasis = CarouselMath.Emphasis(_progress, i, last);
            var width = CarouselMath.Dot + (CarouselMath.Pill - CarouselMath.Dot) * emphasis;
            var step = width + 2 * pad;
            if (x + step < 0 || x > view)
            {
                x += step;
                continue;
            }
            var rect = new Rect(x + pad, y, width, CarouselMath.Dot);
            var pill = new RoundedRect(rect, CarouselMath.Dot / 2);
            // Dots that scroll under an edge fade out over 18px.
            var edge = 1.0;
            if (fadeStart)
            {
                edge = Math.Min(edge, Math.Clamp(rect.Center.X / 18, 0, 1));
            }
            if (fadeEnd)
            {
                edge = Math.Min(edge, Math.Clamp((view - rect.Center.X) / 18, 0, 1));
            }
            using (context.PushOpacity(edge))
            {
                context.DrawRectangle(dot, null, pill);
                if (i == _hoverIndex && hoverMix > 0 && DotHoverBrush is { } hover)
                {
                    using (context.PushOpacity(hoverMix))
                    {
                        context.DrawRectangle(hover, null, pill);
                    }
                }
                if (emphasis > 0 && FillBrush is { } fill)
                {
                    using (context.PushOpacity(emphasis))
                    {
                        context.DrawRectangle(fill, null, pill);
                    }
                }
                var drain = Owner?.GetDrain(i) ?? 0;
                if (drain > 0 && DrainBrush is { } drainBrush)
                {
                    // The drain covers the fill from the right (transform-origin: 100% 50%).
                    using (context.PushClip(pill))
                    {
                        var drained = width * Math.Min(1, drain);
                        context.FillRectangle(drainBrush, new Rect(rect.Right - drained, rect.Y, drained, rect.Height));
                    }
                }
            }
            x += step;
        }
    }

    internal void Select(CarouselPagerTab tab) => Owner?.SelectFromTab(tab.Index);

    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselPagerPeer(this);

    private sealed class CarouselPagerPeer : ControlAutomationPeer
    {
        public CarouselPagerPeer(CarouselPager owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// One indicator of a <see cref="CarouselPager"/> (Arc <c>role="tab"</c>, roving tab stop). Transparent: the pager
/// draws the dot; the tab draws only its focus ring (2px, 4px inside its box).
/// </summary>
internal sealed class CarouselPagerTab : AvaloniaButton
{
    private ImmutablePen? _focusPen;
    private Color _focusColor;
    private bool _selected;

    public CarouselPagerTab()
    {
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        IsTabStop = false;
    }

    public CarouselPager? Pager { get; init; }

    public int Index { get; init; }

    public bool IsSelected => _selected;

    protected override Type StyleKeyOverride => typeof(CarouselPagerTab);

    public void SetSelected(bool selected)
    {
        _selected = selected;
        IsTabStop = selected;
    }

    protected override void OnClick()
    {
        base.OnClick();
        Pager?.Select(this);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            Pager?.OnTabHover(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Pager?.OnTabHover(this, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (IsFocused && Classes.Contains(":focus-visible") && Pager?.FocusRingBrush is ISolidColorBrush ring)
        {
            if (_focusPen is null || _focusColor != ring.Color)
            {
                _focusColor = ring.Color;
                _focusPen = new ImmutablePen(new ImmutableSolidColorBrush(ring.Color), 2);
            }
            var outline = bounds.Deflate(3);
            var radius = Math.Min(outline.Width, outline.Height) / 2;
            context.DrawRectangle(null, _focusPen, new RoundedRect(outline, radius));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselPagerTabPeer(this);

    private sealed class CarouselPagerTabPeer : ControlAutomationPeer, ISelectionItemProvider, IInvokeProvider
    {
        public CarouselPagerTabPeer(CarouselPagerTab owner)
            : base(owner)
        {
        }

        private CarouselPagerTab Tab => (CarouselPagerTab)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsSelected => Tab.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select() => Tab.Pager?.Select(Tab);

        public void Invoke() => Select();
    }
}
