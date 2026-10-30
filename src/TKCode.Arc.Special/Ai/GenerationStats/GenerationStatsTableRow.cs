using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One row of the breakdown's token table: its name, then the tokens, the rate and the cost in fixed, right-aligned,
/// tabular columns (so streaming numbers never move the columns). The header row names the columns; the total row is
/// medium weight under a hairline. The rate column hides without prices and below 440px.
/// </summary>
internal sealed class GenerationStatsTableRow : TemplatedControl
{
    internal const string HeaderPseudoClass = ":header";
    internal const string TotalPseudoClass = ":total";
    internal const string NoRatePseudoClass = ":no-rate";

    /// <summary>The row's name (or the first column's header).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<GenerationStatsTableRow, string?>(nameof(Label));

    /// <summary>The token count.</summary>
    public static readonly StyledProperty<string?> TokensProperty =
        AvaloniaProperty.Register<GenerationStatsTableRow, string?>(nameof(Tokens));

    /// <summary>The rate.</summary>
    public static readonly StyledProperty<string?> RateProperty =
        AvaloniaProperty.Register<GenerationStatsTableRow, string?>(nameof(Rate));

    /// <summary>The cost.</summary>
    public static readonly StyledProperty<string?> CostProperty =
        AvaloniaProperty.Register<GenerationStatsTableRow, string?>(nameof(Cost));

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="TokensProperty"/>
    public string? Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <inheritdoc cref="RateProperty"/>
    public string? Rate { get => GetValue(RateProperty); set => SetValue(RateProperty, value); }

    /// <inheritdoc cref="CostProperty"/>
    public string? Cost { get => GetValue(CostProperty); set => SetValue(CostProperty, value); }

    /// <summary>Whether the rate column shows.</summary>
    internal bool ShowsRate => !PseudoClasses.Contains(NoRatePseudoClass);

    /// <summary>Tabular figures for the number columns.</summary>
    internal static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var name in new[] { "PART_Tokens", "PART_Rate", "PART_Cost" })
        {
            if (e.NameScope.Find<TextBlock>(name) is { } text)
            {
                text.FontFeatures = TabularNumbers;
            }
        }
    }

    /// <summary>Marks the header and total rows and whether the rate column shows.</summary>
    internal void SetKind(bool header, bool total, bool showsRate)
    {
        PseudoClasses.Set(HeaderPseudoClass, header);
        PseudoClasses.Set(TotalPseudoClass, total);
        PseudoClasses.Set(NoRatePseudoClass, !showsRate);
    }
}
