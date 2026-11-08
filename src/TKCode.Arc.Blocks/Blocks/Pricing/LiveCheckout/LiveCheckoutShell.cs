using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// The checkout's two-part layout (Arc's shell grid), by the block's own width:
/// <list type="bullet">
/// <item>from <see cref="TwoColumnWidth"/>: the form on the left, the summary (<see cref="SummaryWidth"/> wide) on the right,
/// <see cref="ColumnGap"/> apart; the summary sticks <see cref="StickyTop"/> below the top of a scrolled view;</item>
/// <item>below it: the summary on top at full width, the form under it;</item>
/// <item>paid: only the summary counts, centred (two columns: at its column width; one column: full width). A leaving
/// form stays where it was, out of the layout, until <see cref="EndFormExit"/>.</item>
/// </list>
/// The first child is the form, the second the summary.
/// </summary>
internal sealed class LiveCheckoutShell : Panel
{
    private Rect? _formExitRect;
    private double _viewportTop = double.NegativeInfinity;
    private bool _isPaid;

    public LiveCheckoutShell()
    {
        EffectiveViewportChanged += (_, e) =>
        {
            var top = e.EffectiveViewport.Top;
            if (Math.Abs(top - _viewportTop) > 0.01)
            {
                _viewportTop = top;
                if (!IsNarrow && !_isPaid)
                {
                    InvalidateArrange();
                }
            }
        };
    }

    public static readonly StyledProperty<double> TwoColumnWidthProperty =
        AvaloniaProperty.Register<LiveCheckoutShell, double>(nameof(TwoColumnWidth), 760);

    public double TwoColumnWidth { get => GetValue(TwoColumnWidthProperty); set => SetValue(TwoColumnWidthProperty, value); }

    public static readonly StyledProperty<double> SummaryWidthProperty =
        AvaloniaProperty.Register<LiveCheckoutShell, double>(nameof(SummaryWidth), 380);

    public double SummaryWidth { get => GetValue(SummaryWidthProperty); set => SetValue(SummaryWidthProperty, value); }

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<LiveCheckoutShell, double>(nameof(ColumnGap), 32);

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public static readonly StyledProperty<double> NarrowGapProperty =
        AvaloniaProperty.Register<LiveCheckoutShell, double>(nameof(NarrowGap), 20);

    public double NarrowGap { get => GetValue(NarrowGapProperty); set => SetValue(NarrowGapProperty, value); }

    public static readonly StyledProperty<double> StickyTopProperty =
        AvaloniaProperty.Register<LiveCheckoutShell, double>(nameof(StickyTop), 24);

    public double StickyTop { get => GetValue(StickyTopProperty); set => SetValue(StickyTopProperty, value); }

    static LiveCheckoutShell()
    {
        AffectsMeasure<LiveCheckoutShell>(TwoColumnWidthProperty, SummaryWidthProperty, ColumnGapProperty, NarrowGapProperty);
        AffectsArrange<LiveCheckoutShell>(StickyTopProperty);
    }

    public Control? Form => Children.Count > 0 ? Children[0] : null;
    public Control? Summary => Children.Count > 1 ? Children[1] : null;

    /// <summary>True below <see cref="TwoColumnWidth"/> (decided at the last measure).</summary>
    public bool IsNarrow { get; private set; }

    /// <summary>The receipt layout.</summary>
    public bool IsPaid
    {
        get => _isPaid;
        set
        {
            if (_isPaid != value)
            {
                _isPaid = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The summary's sticky offset at the last arrange (tests).</summary>
    public double StickyOffset { get; private set; }

    /// <summary>Keeps the form at its current place, out of the layout, while it leaves.</summary>
    public void BeginFormExit()
    {
        if (Form is { } form)
        {
            _formExitRect = form.Bounds;
        }
    }

    /// <summary>The form has left (hidden by the caller).</summary>
    public void EndFormExit()
    {
        _formExitRect = null;
        InvalidateMeasure();
    }

    public static bool IsNarrowFor(double width, double threshold) => !double.IsInfinity(width) && width < threshold;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? TwoColumnWidth + 280 : availableSize.Width;
        IsNarrow = IsNarrowFor(width, TwoColumnWidth);
        var form = Form;
        var summary = Summary;
        if (_isPaid)
        {
            summary?.Measure(new Size(IsNarrow ? width : Math.Min(SummaryWidth, width), double.PositiveInfinity));
            if (form is not null && form.IsVisible)
            {
                var w = _formExitRect?.Width ?? width;
                form.Measure(new Size(w, double.PositiveInfinity));
            }
            return new Size(width, summary?.DesiredSize.Height ?? 0);
        }
        if (IsNarrow)
        {
            summary?.Measure(new Size(width, double.PositiveInfinity));
            form?.Measure(new Size(width, double.PositiveInfinity));
            var top = summary?.DesiredSize.Height ?? 0;
            var bottom = form is { IsVisible: true } ? form.DesiredSize.Height : 0;
            return new Size(width, top + (bottom > 0 ? NarrowGap + bottom : 0));
        }
        var formWidth = Math.Max(0, width - SummaryWidth - ColumnGap);
        form?.Measure(new Size(formWidth, double.PositiveInfinity));
        summary?.Measure(new Size(SummaryWidth, double.PositiveInfinity));
        var height = Math.Max(form is { IsVisible: true } ? form.DesiredSize.Height : 0, summary?.DesiredSize.Height ?? 0);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var form = Form;
        var summary = Summary;
        StickyOffset = 0;
        if (_isPaid)
        {
            var w = IsNarrow ? width : Math.Min(SummaryWidth, width);
            summary?.Arrange(new Rect((width - w) / 2, 0, w, summary.DesiredSize.Height));
            if (form is not null)
            {
                form.Arrange(_formExitRect ?? new Rect(0, 0, width, form.DesiredSize.Height));
            }
            return finalSize;
        }
        if (IsNarrow)
        {
            var top = summary?.DesiredSize.Height ?? 0;
            summary?.Arrange(new Rect(0, 0, width, top));
            form?.Arrange(new Rect(0, top + NarrowGap, width, form.DesiredSize.Height));
            return finalSize;
        }
        var formWidth = Math.Max(0, width - SummaryWidth - ColumnGap);
        form?.Arrange(new Rect(0, 0, formWidth, form.DesiredSize.Height));
        if (summary is not null)
        {
            var h = summary.DesiredSize.Height;
            // Sticky: follow the top of the visible area (plus the gap), but stay inside the shell.
            var sticky = double.IsNegativeInfinity(_viewportTop) ? 0 : _viewportTop + StickyTop;
            StickyOffset = Math.Clamp(sticky, 0, Math.Max(0, finalSize.Height - h));
            summary.Arrange(new Rect(width - SummaryWidth, StickyOffset, SummaryWidth, h));
        }
        return finalSize;
    }
}
