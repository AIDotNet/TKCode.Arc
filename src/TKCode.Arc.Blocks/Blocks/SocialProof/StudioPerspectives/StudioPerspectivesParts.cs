using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>The quote text on the stage: one per quote, swapped by the stage's content swap.</summary>
internal sealed class StudioPerspectivesQuoteView : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StudioPerspectivesQuoteView, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<StudioPerspectivesQuoteView, bool>(nameof(IsCompact));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The smaller type of a compact stage.</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(":compact", IsCompact);
        }
    }
}

/// <summary>The person under a quote: portrait, name and byline.</summary>
internal sealed class StudioPerspectivesPerson : TemplatedControl
{
    public static readonly StyledProperty<string?> PersonNameProperty =
        AvaloniaProperty.Register<StudioPerspectivesPerson, string?>(nameof(PersonName));

    public static readonly StyledProperty<string?> BylineProperty =
        AvaloniaProperty.Register<StudioPerspectivesPerson, string?>(nameof(Byline));

    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<StudioPerspectivesPerson, int>(nameof(Seed));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<StudioPerspectivesPerson, IImage?>(nameof(Image));

    public string? PersonName { get => GetValue(PersonNameProperty); set => SetValue(PersonNameProperty, value); }

    public string? Byline { get => GetValue(BylineProperty); set => SetValue(BylineProperty, value); }

    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
}

/// <summary>
/// The progress row of a <see cref="StudioPerspectives"/> stage: one short bar per quote. Bars before the current one are
/// full, the current one fills with the time until the next quote (<see cref="Progress"/>), the rest are empty tracks.
/// A press on a bar raises <see cref="SegmentPressed"/>. Drawn in one pass; brushes are cached per color.
/// </summary>
internal sealed class StudioPerspectivesProgress : Control
{
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, int>(nameof(Count));

    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, int>(nameof(Index));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, double>(nameof(Progress));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, double>(nameof(BarHeight), 3);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, double>(nameof(Gap), 6);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> DoneBrushProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, IBrush?>(nameof(DoneBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<StudioPerspectivesProgress, IBrush?>(nameof(FillBrush));

    static StudioPerspectivesProgress()
    {
        AffectsRender<StudioPerspectivesProgress>(CountProperty, IndexProperty, ProgressProperty, BarHeightProperty, GapProperty,
            TrackBrushProperty, DoneBrushProperty, FillBrushProperty);
        AffectsMeasure<StudioPerspectivesProgress>(BarHeightProperty);
    }

    public StudioPerspectivesProgress()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? DoneBrush { get => GetValue(DoneBrushProperty); set => SetValue(DoneBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>Raised with the index of the bar that was pressed.</summary>
    public event EventHandler<int>? SegmentPressed;

    /// <summary>The bar under <paramref name="x"/>, or -1.</summary>
    internal int SegmentAt(double x)
    {
        var count = Count;
        if (count <= 0 || Bounds.Width <= 0)
        {
            return -1;
        }
        var width = SegmentWidth();
        var index = (int)Math.Floor(x / (width + Gap));
        return index >= 0 && index < count ? index : -1;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width, Math.Max(16, BarHeight));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && SegmentAt(e.GetPosition(this).X) is var index and >= 0)
        {
            SegmentPressed?.Invoke(this, index);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        // The whole row is a press target, not only the painted bars.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var count = Count;
        if (count <= 0)
        {
            return;
        }
        var width = SegmentWidth();
        var h = BarHeight;
        var y = (size.Height - h) / 2;
        var radius = h / 2;
        for (var i = 0; i < count; i++)
        {
            var rect = new Rect(i * (width + Gap), y, width, h);
            if (TrackBrush is { } track)
            {
                context.DrawRectangle(track, null, new RoundedRect(rect, radius));
            }
            if (i < Index && DoneBrush is { } done)
            {
                context.DrawRectangle(done, null, new RoundedRect(rect, radius));
            }
            else if (i == Index && FillBrush is { } fill)
            {
                var fraction = Math.Clamp(Progress, 0, 1);
                if (fraction > 0)
                {
                    context.DrawRectangle(fill, null, new RoundedRect(rect.WithWidth(Math.Max(h, width * fraction)), radius));
                }
            }
        }
    }

    private double SegmentWidth() => Math.Max(0, (Bounds.Width - Gap * (Count - 1)) / Math.Max(1, Count));
}
