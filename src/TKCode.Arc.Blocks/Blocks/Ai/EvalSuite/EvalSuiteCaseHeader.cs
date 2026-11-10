using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The column titles over the case list (Status, Case, Check and the two version names), drawn on the list's own columns
/// so they always line up. Hidden from screen readers: each row's accessible text names its columns.
/// </summary>
internal sealed class EvalSuiteCaseHeader : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<EvalSuiteCaseHeader, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<EvalSuiteCaseHeader>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<EvalSuiteCaseHeader>();

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    private readonly string[] _titles = new string[5];
    private readonly TextLayout?[] _layouts = new TextLayout?[5];
    private double _layoutWidth = -1;

    static EvalSuiteCaseHeader()
    {
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<EvalSuiteCaseHeader>(AccessibilityView.Raw);
        IsHitTestVisibleProperty.OverrideDefaultValue<EvalSuiteCaseHeader>(false);
    }

    /// <summary>The list whose columns the titles follow.</summary>
    public EvalSuiteCaseList? List { get; set; }

    /// <summary>The five titles: status, case, check, baseline, candidate.</summary>
    internal IReadOnlyList<string> Titles => _titles;

    public void SetTitles(string status, string caseTitle, string check, string baseline, string candidate)
    {
        _titles[0] = status;
        _titles[1] = caseTitle;
        _titles[2] = check;
        _titles[3] = baseline;
        _titles[4] = candidate;
        Clear();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            Clear();
            InvalidateVisual();
        }
    }

    private void Clear()
    {
        for (var i = 0; i < _layouts.Length; i++)
        {
            _layouts[i]?.Dispose();
            _layouts[i] = null;
        }
        _layoutWidth = -1;
    }

    public override void Render(DrawingContext context)
    {
        if (List is not { } list)
        {
            return;
        }
        var c = EvalSuiteColumns.Compute(Bounds.Width, list.RowInset, list.StatusColumnWidth, list.CheckColumnWidth,
            list.VerdictColumnWidth, list.IsCompact);
        if (Math.Abs(_layoutWidth - Bounds.Width) > 0.5)
        {
            Clear();
            _layoutWidth = Bounds.Width;
        }
        Draw(context, 0, c.StatusX, c.StatusWidth);
        Draw(context, 1, c.CaseX, c.CaseWidth);
        if (c.CheckWidth > 0)
        {
            Draw(context, 2, c.CheckX, c.CheckWidth);
        }
        // The verdict titles start where the verdict text starts, after its glyph... unless the column only holds the glyph.
        var offset = list.IsCompact ? 0 : 22;
        Draw(context, 3, c.BaseX + offset, c.VerdictWidth - offset);
        Draw(context, 4, c.CandidateX + offset, c.VerdictWidth - offset);
    }

    private void Draw(DrawingContext context, int index, double x, double width)
    {
        var text = _titles[index];
        if (string.IsNullOrEmpty(text) || width <= 4)
        {
            return;
        }
        var layout = _layouts[index] ??= new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), FontSize,
            Foreground ?? Brushes.Gray, maxWidth: width, textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1);
        layout.Draw(context, new Point(x, (Bounds.Height - layout.Height) / 2));
    }
}
