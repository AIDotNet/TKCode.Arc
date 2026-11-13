using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A status or verdict pill for the open case (a glyph and a word on a soft tint), drawn like the list's chips. Call
/// <see cref="ShowStatus"/> for a status or <see cref="ShowVerdict"/> for a verdict. Its accessible name is its word.
/// </summary>
internal sealed class EvalSuiteChip : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(SecondaryBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(SuccessBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(DangerBrush));

    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(WarningBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<EvalSuiteChip, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<EvalSuiteChip, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<EvalSuiteChip>();

    public static readonly StyledProperty<double> ChipHeightProperty =
        AvaloniaProperty.Register<EvalSuiteChip, double>(nameof(ChipHeight), 24);

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double ChipHeight { get => GetValue(ChipHeightProperty); set => SetValue(ChipHeightProperty, value); }

    private readonly EvalSuitePainter _painter = new();
    private TextLayout? _layout;
    private EvalCaseStatus _status;
    private EvalResult? _result;
    private bool _verdict;
    private EvalSuiteLabels _labels = EvalSuiteLabels.Default;
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    static EvalSuiteChip()
    {
        AffectsMeasure<EvalSuiteChip>(FontFamilyProperty, ChipHeightProperty);
        AffectsRender<EvalSuiteChip>(CornerRadiusProperty);
    }

    /// <summary>The word on the chip.</summary>
    public string Text => _verdict ? _labels.Verdict(_result, _culture) : _labels.StatusName(_status);

    /// <summary>Shows a status.</summary>
    public void ShowStatus(EvalCaseStatus status, EvalSuiteLabels labels)
    {
        _verdict = false;
        _status = status;
        _labels = labels;
        Changed();
    }

    /// <summary>Shows a verdict (Pass, Fail, Error, No result).</summary>
    public void ShowVerdict(EvalResult? result, EvalSuiteLabels labels, CultureInfo culture)
    {
        _verdict = true;
        _result = result;
        _labels = labels;
        _culture = culture;
        Changed();
    }

    private void Changed()
    {
        _layout?.Dispose();
        _layout = null;
        AutomationProperties.SetName(this, Text);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == typeof(EvalSuiteChip) && change.Property != CornerRadiusProperty)
        {
            _layout?.Dispose();
            _layout = null;
            InvalidateVisual();
        }
    }

    private EvalSuiteMark Mark()
    {
        var palette = EvalSuitePalette.From(Foreground, SecondaryBrush, MutedBrush, SuccessBrush, DangerBrush, WarningBrush, AccentBrush);
        return _verdict ? palette.VerdictMark(_result) with { Tinted = true } : palette.StatusMark(_status);
    }

    private TextLayout Layout()
    {
        var mark = Mark();
        return _layout ??= new TextLayout(Text, new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), 12,
            _painter.Solid(mark.Color));
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(EvalSuitePainter.ChipWidth(Layout(), ChipHeight), ChipHeight);

    public override void Render(DrawingContext context)
    {
        _painter.Chip(context, new Rect(Bounds.Size), Mark(), Layout(), CornerRadius.TopLeft);
    }
}
