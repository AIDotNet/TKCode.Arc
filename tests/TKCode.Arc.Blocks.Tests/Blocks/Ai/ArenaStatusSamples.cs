using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the arena status parts' tests and snapshots (spec section 10: "Lumen 3 Pro", "Kite 5"; the
/// marks are plain filled shapes on a 24 grid). The words are the arena's English defaults, set the way the arena sets them.
/// </summary>
internal static class ArenaStatusSamples
{
    public static AiModel Lumen() => new()
    {
        Id = "lumen-pro",
        Name = "Lumen 3 Pro",
        ShortName = "Lumen Pro",
        ProviderId = "lumen-labs",
        Mark = Geometry.Parse("M12 7A5 5 0 1 0 12 17A5 5 0 1 0 12 7Z M11 1H13V5H11Z M11 19H13V23H11Z M1 11H5V13H1Z M19 11H23V13H19Z"),
        MarkColor = new ImmutableSolidColorBrush(Color.Parse("#D9822B"))
    };

    public static AiModel Kite() => new()
    {
        Id = "kite-5",
        Name = "Kite 5",
        ProviderId = "kitework",
        Mark = Geometry.Parse("M12 2L20.66 7V17L12 22L3.34 17V7Z")
    };

    public static AiStatusPill Pill(ArenaPaneStatus status = ArenaPaneStatus.Idle, bool compact = false) => new()
    {
        Status = status,
        IsCompact = compact,
        ThinkingText = "Thinking",
        WritingText = "Writing",
        DoneText = "Done",
        FinishedFirstText = "Finished first",
        FinishedFirstShortText = "First",
        StoppedText = "Stopped",
        FailedText = "Failed"
    };

    public static AiMetricLine Metrics(TimeSpan? first = null, bool timing = false, double? speed = null, decimal? cost = null) => new()
    {
        FirstToken = first,
        IsTiming = timing,
        TokensPerSecond = speed,
        CostUsd = cost,
        FirstTokenText = "First token",
        SpeedUnitText = "tok/s",
        SpeedName = "Speed",
        CostName = "Cost",
        Placeholder = "–"
    };

    public static ArenaIdentity Identity(string slot = "A", AiModel? model = null, bool revealed = false) => new()
    {
        BlindText = "Model " + slot,
        Model = model,
        IsRevealed = revealed,
        RevealedAutomationName = model is null ? null : "Model " + slot + ", " + model.Name
    };

    /// <summary>
    /// A pane as the arena lays it out: the head (48, 20 4 12 0 inside) with the identity on the left and the status on the
    /// right, an empty answer area and the metrics (8 20 14 inside), framed by a 1px Border.
    /// </summary>
    public static Border Pane(ArenaIdentity identity, AiStatusPill pill, AiMetricLine metrics, double width = 392, double answerHeight = 60)
    {
        var head = new Grid { Height = 48, Margin = new Thickness(20, 4, 12, 0), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(identity);
        pill.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(pill, 1);
        head.Children.Add(pill);
        metrics.Margin = new Thickness(20, 8, 20, 14);
        var pane = new Border
        {
            Width = width,
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { head, new Border { Height = answerHeight }, metrics } }
        };
        ArcTokens.Bind(pane, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(pane, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        return pane;
    }
}
