using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The colors of the extraction form that are mixes of theme tokens (spec 3.4, input states and linked tints), computed once
/// per theme: an empty input is 4% Foreground, a low-confidence one 6% Warning in Surface with a 50% Warning / 50% Border
/// border, an invalid one a 60% Danger / 40% Border border; a lit field row is its group color at 8%, a lit table 6% and a
/// lit cell 12%. Owners (a field row, a table) bind the theme probes (<see cref="BindProbes"/>), build a palette when a
/// probe changes (<see cref="IsProbe"/>, <see cref="Build"/>) and hand it to their inputs; equal colors give the same
/// instance, so every row of a form shares one palette.
/// </summary>
internal sealed class ExtractionPalette
{
    /// <summary>Share of Foreground in an empty input's fill.</summary>
    public const double EmptyAlpha = 0.04;

    /// <summary>Share of Warning in a low-confidence input's fill (mixed into Surface).</summary>
    public const double LowFillMix = 0.06;

    /// <summary>Share of Warning in a low-confidence input's border (mixed into Border).</summary>
    public const double LowBorderMix = 0.5;

    /// <summary>Share of Danger in an invalid input's border (mixed into Border).</summary>
    public const double InvalidBorderMix = 0.6;

    /// <summary>A lit field row: its group color at 8%.</summary>
    public const double RowTintAlpha = 0.08;

    /// <summary>A lit table: its group color at 6%.</summary>
    public const double TableTintAlpha = 0.06;

    /// <summary>A lit table row or cell: its group color at 12%.</summary>
    public const double CellTintAlpha = 0.12;

    public static readonly AttachedProperty<IBrush?> ForegroundProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("ForegroundProbe");

    public static readonly AttachedProperty<IBrush?> SurfaceProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("SurfaceProbe");

    public static readonly AttachedProperty<IBrush?> BorderProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("BorderProbe");

    public static readonly AttachedProperty<IBrush?> BorderStrongProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("BorderStrongProbe");

    public static readonly AttachedProperty<IBrush?> WarningProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("WarningProbe");

    public static readonly AttachedProperty<IBrush?> DangerProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("DangerProbe");

    public static readonly AttachedProperty<IBrush?> Series1ProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("Series1Probe");

    public static readonly AttachedProperty<IBrush?> Series2ProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("Series2Probe");

    public static readonly AttachedProperty<IBrush?> Series3ProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("Series3Probe");

    public static readonly AttachedProperty<IBrush?> Series4ProbeProperty =
        AvaloniaProperty.RegisterAttached<ExtractionPalette, Control, IBrush?>("Series4Probe");

    private static ExtractionPalette? s_last;

    private readonly Color[] _series;
    private readonly IBrush[] _dots = new IBrush[4];
    private readonly IBrush[] _rowTints = new IBrush[4];
    private readonly IBrush[] _tableTints = new IBrush[4];
    private readonly IBrush[] _cellTints = new IBrush[4];

    private ExtractionPalette(Color foreground, Color surface, Color border, Color borderStrong, Color warning, Color danger, Color[] series)
    {
        Foreground = foreground;
        SurfaceColor = surface;
        BorderColor = border;
        BorderStrongColor = borderStrong;
        WarningColor = warning;
        DangerColor = danger;
        _series = series;
        EmptyFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, foreground.A / 255.0 * EmptyAlpha));
        Surface = new ImmutableSolidColorBrush(surface);
        Border = new ImmutableSolidColorBrush(border);
        BorderStrong = new ImmutableSolidColorBrush(borderStrong);
        LowFill = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, warning, LowFillMix));
        LowBorder = new ImmutableSolidColorBrush(Oklch.MixOklab(border, warning, LowBorderMix));
        InvalidBorder = new ImmutableSolidColorBrush(Oklch.MixOklab(border, danger, InvalidBorderMix));
        for (var i = 0; i < 4; i++)
        {
            var color = series[i];
            _dots[i] = new ImmutableSolidColorBrush(color);
            _rowTints[i] = new ImmutableSolidColorBrush(Oklch.WithAlpha(color, color.A / 255.0 * RowTintAlpha));
            _tableTints[i] = new ImmutableSolidColorBrush(Oklch.WithAlpha(color, color.A / 255.0 * TableTintAlpha));
            _cellTints[i] = new ImmutableSolidColorBrush(Oklch.WithAlpha(color, color.A / 255.0 * CellTintAlpha));
        }
    }

    /// <summary>The Foreground color.</summary>
    public Color Foreground { get; }

    private Color SurfaceColor { get; }

    private Color BorderColor { get; }

    private Color BorderStrongColor { get; }

    private Color WarningColor { get; }

    private Color DangerColor { get; }

    /// <summary>An empty input's fill (4% Foreground).</summary>
    public IBrush EmptyFill { get; }

    /// <summary>Surface (a linked or focused input).</summary>
    public IBrush Surface { get; }

    /// <summary>Border (a linked input, an empty one under the pointer).</summary>
    public IBrush Border { get; }

    /// <summary>BorderStrong (a focused input).</summary>
    public IBrush BorderStrong { get; }

    /// <summary>A low-confidence input's fill (6% Warning in Surface).</summary>
    public IBrush LowFill { get; }

    /// <summary>A low-confidence input's border (50% Warning in Border).</summary>
    public IBrush LowBorder { get; }

    /// <summary>An invalid input's border (60% Danger in Border).</summary>
    public IBrush InvalidBorder { get; }

    /// <summary>The group color of a tone (1 to 4; other values wrap), solid: the dot.</summary>
    public IBrush Dot(int tone) => _dots[Index(tone)];

    /// <summary>A lit field row's fill (the group color at 8%).</summary>
    public IBrush RowTint(int tone) => _rowTints[Index(tone)];

    /// <summary>A lit table's fill (the group color at 6%).</summary>
    public IBrush TableTint(int tone) => _tableTints[Index(tone)];

    /// <summary>A lit table row or cell's fill (the group color at 12%).</summary>
    public IBrush CellTint(int tone) => _cellTints[Index(tone)];

    /// <summary>The group color of a tone.</summary>
    public Color SeriesColor(int tone) => _series[Index(tone)];

    /// <summary>Tones 1 to 4 are the four series colors; any other tone takes its place in the cycle (5 is 1 again).</summary>
    public static int Index(int tone) => ((tone - 1) % 4 + 4) % 4;

    /// <summary>Binds the theme probes on <paramref name="owner"/>; each change shows in <see cref="IsProbe"/>.</summary>
    public static void BindProbes(Control owner)
    {
        ArcTokens.Bind(owner, ForegroundProbeProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(owner, SurfaceProbeProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(owner, BorderProbeProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(owner, BorderStrongProbeProperty, ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(owner, WarningProbeProperty, ArcDesignTokenKind.Warning);
        ArcTokens.Bind(owner, DangerProbeProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(owner, Series1ProbeProperty, ArcDesignTokenKind.Series1);
        ArcTokens.Bind(owner, Series2ProbeProperty, ArcDesignTokenKind.Series2);
        ArcTokens.Bind(owner, Series3ProbeProperty, ArcDesignTokenKind.Series3);
        ArcTokens.Bind(owner, Series4ProbeProperty, ArcDesignTokenKind.Series4);
    }

    /// <summary>Whether <paramref name="property"/> is one of the theme probes.</summary>
    public static bool IsProbe(AvaloniaProperty property) =>
        property == ForegroundProbeProperty || property == SurfaceProbeProperty || property == BorderProbeProperty ||
        property == BorderStrongProbeProperty || property == WarningProbeProperty || property == DangerProbeProperty ||
        property == Series1ProbeProperty || property == Series2ProbeProperty || property == Series3ProbeProperty ||
        property == Series4ProbeProperty;

    /// <summary>The palette of the theme <paramref name="owner"/>'s probes hold now; null until every probe has resolved.</summary>
    public static ExtractionPalette? Build(Control owner)
    {
        if (ProbeColor(owner, ForegroundProbeProperty) is not { } foreground || ProbeColor(owner, SurfaceProbeProperty) is not { } surface ||
            ProbeColor(owner, BorderProbeProperty) is not { } border || ProbeColor(owner, BorderStrongProbeProperty) is not { } strong ||
            ProbeColor(owner, WarningProbeProperty) is not { } warning || ProbeColor(owner, DangerProbeProperty) is not { } danger ||
            ProbeColor(owner, Series1ProbeProperty) is not { } s1 || ProbeColor(owner, Series2ProbeProperty) is not { } s2 ||
            ProbeColor(owner, Series3ProbeProperty) is not { } s3 || ProbeColor(owner, Series4ProbeProperty) is not { } s4)
        {
            return null;
        }
        // Rows of one form share a palette: the same colors give the last instance back.
        if (s_last is { } last && last.Foreground == foreground && last.SurfaceColor == surface && last.BorderColor == border &&
            last.BorderStrongColor == strong && last.WarningColor == warning && last.DangerColor == danger &&
            last._series[0] == s1 && last._series[1] == s2 && last._series[2] == s3 && last._series[3] == s4)
        {
            return last;
        }
        var palette = new ExtractionPalette(foreground, surface, border, strong, warning, danger, [s1, s2, s3, s4]);
        s_last = palette;
        return palette;
    }

    private static Color? ProbeColor(Control owner, AttachedProperty<IBrush?> probe) =>
        owner.GetValue(probe) is ISolidColorBrush solid ? solid.Color : null;
}
