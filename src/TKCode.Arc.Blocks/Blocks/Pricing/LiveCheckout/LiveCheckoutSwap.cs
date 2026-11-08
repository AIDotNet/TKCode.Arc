using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Swaps one piece of content for another the way the reference swaps button labels: both at once, the old one fading out
/// while it rises <see cref="Distance"/> and blurs to <see cref="BlurRadius"/>, the new one fading in from as far below
/// and sharpening, on the ω 20 spring; the old layer leaves after about 470 ms. Reduced motion keeps only the crossfade;
/// Off swaps at once. With <see cref="KeepsWidth"/> the slot never narrows (the receipt button keeps its first width).
/// </summary>
/// <remarks>Code-driven: two layers move on one spring with mirrored offsets and blur, which a ControlTheme cannot express.</remarks>
internal sealed class LiveCheckoutSwap : Control
{
    private readonly ContentPresenter _a = new();
    private readonly ContentPresenter _b = new();
    private readonly MotionValue<double> _progress;
    private ContentPresenter _current;
    private ContentPresenter? _leaving;
    private double _widest;
    private object? _content;

    public LiveCheckoutSwap()
    {
        _current = _a;
        foreach (var layer in new[] { _a, _b })
        {
            layer.HorizontalAlignment = HorizontalAlignment.Center;
            layer.VerticalAlignment = VerticalAlignment.Center;
            layer.RenderTransform = new TranslateTransform();
            LogicalChildren.Add(layer);
            VisualChildren.Add(layer);
        }
        _b.IsVisible = false;
        _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Changed += _ => Apply();
    }

    /// <summary>Vertical travel of each layer.</summary>
    public double Distance { get; set; } = 8;

    /// <summary>Blur of a layer at the far end of its travel.</summary>
    public double BlurRadius { get; set; } = 4;

    /// <summary>When true the slot keeps the widest width it has shown.</summary>
    public bool KeepsWidth { get; set; }

    /// <summary>Horizontal placement of both layers.</summary>
    public HorizontalAlignment ContentAlignment
    {
        get => _a.HorizontalAlignment;
        set
        {
            _a.HorizontalAlignment = value;
            _b.HorizontalAlignment = value;
        }
    }

    /// <summary>The content shown now.</summary>
    public object? Content => _content;

    /// <summary>True while an old layer is still leaving (tests).</summary>
    public bool IsSwapping => _leaving is not null;

    /// <summary>Shows <paramref name="content"/>: swapped when <paramref name="animate"/> and motion allows, at once otherwise.</summary>
    public async void Show(object? content, bool animate)
    {
        if (ReferenceEquals(content, _content))
        {
            return;
        }
        _content = content;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || !IsLoaded || level == ArcMotionLevel.Off || _current.Content is null)
        {
            Clear(_leaving);
            _leaving = null;
            _current.Content = content;
            _current.IsVisible = true;
            _progress.Jump(1);
            Apply();
            InvalidateMeasure();
            return;
        }
        Clear(_leaving);
        _leaving = _current;
        _current = ReferenceEquals(_current, _a) ? _b : _a;
        _current.Content = content;
        _current.IsVisible = true;
        _progress.Jump(0);
        InvalidateMeasure();
        var spec = level == ArcMotionLevel.Full
            ? MotionSpec.Spring(LiveCheckoutMotion.Roll)
            : MotionSpec.Tween(ArcMotionPresets.Instant, LiveCheckoutMotion.Standard);
        var leaving = _leaving;
        if (await _progress.AnimateTo(1, spec) && ReferenceEquals(leaving, _leaving))
        {
            Clear(_leaving);
            _leaving = null;
            InvalidateMeasure();
        }
    }

    private static void Clear(ContentPresenter? layer)
    {
        if (layer is null)
        {
            return;
        }
        layer.Content = null;
        layer.IsVisible = false;
        layer.Opacity = 1;
        layer.Effect = null;
        ((TranslateTransform)layer.RenderTransform!).Y = 0;
    }

    private void Apply()
    {
        var p = Math.Clamp(_progress.Value, 0, 1);
        var full = ArcMotion.IsFull(this);
        Pose(_current, p, Distance * (1 - p), full);
        if (_leaving is not null)
        {
            Pose(_leaving, 1 - p, -Distance * p, full);
        }
    }

    private void Pose(ContentPresenter layer, double presence, double offset, bool full)
    {
        layer.Opacity = presence;
        ((TranslateTransform)layer.RenderTransform!).Y = full ? offset : 0;
        layer.Effect = full && BlurRadius > 0 ? ArcBlur.Get(BlurRadius * (1 - presence)) : null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _current.Measure(availableSize);
        var size = _current.DesiredSize;
        if (_leaving is not null)
        {
            _leaving.Measure(availableSize);
            size = new Size(Math.Max(size.Width, _leaving.DesiredSize.Width), Math.Max(size.Height, _leaving.DesiredSize.Height));
        }
        if (KeepsWidth)
        {
            _widest = Math.Max(_widest, size.Width);
            size = size.WithWidth(_widest);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _current.Arrange(rect);
        _leaving?.Arrange(rect);
        return finalSize;
    }
}
