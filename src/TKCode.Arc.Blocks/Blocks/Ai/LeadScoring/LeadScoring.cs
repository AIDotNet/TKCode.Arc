using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A list of leads ranked by score, with the reasons behind each score and the stage each lead is in. Stage tabs, a
/// search and a score band narrow the table; choosing a row opens the lead's score breakdown (fit, engagement and intent
/// factors, each with its points) beside it; a stage switch there, or the bulk bar for several selected rows, moves leads
/// between stages.
/// <para>
/// With no leads the block shows a built-in invented pipeline. Give it <see cref="Leads"/> and <see cref="Stages"/> to
/// show your own; <see cref="LeadsMoved"/> reports every move so you can save it.
/// </para>
/// </summary>
public partial class LeadScoring : TemplatedControl
{
    /// <summary>The heading; null shows the sample heading while the samples are shown.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LeadScoring, string?>(nameof(Title));

    /// <summary>The stage the table is narrowed to (two-way, a <see cref="LeadScoringStage.Key"/>); null shows every stage.</summary>
    public static readonly StyledProperty<string?> StageFilterProperty =
        AvaloniaProperty.Register<LeadScoring, string?>(nameof(StageFilter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The score band the table is narrowed to (two-way).</summary>
    public static readonly StyledProperty<LeadScoringBand> BandProperty =
        AvaloniaProperty.Register<LeadScoring, LeadScoringBand>(nameof(Band), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search (two-way): leads whose name, company or job title contains it are kept.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<LeadScoring, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The lowest score that counts as hot (80).</summary>
    public static readonly StyledProperty<int> HotThresholdProperty =
        AvaloniaProperty.Register<LeadScoring, int>(nameof(HotThreshold), 80);

    /// <summary>The lowest score that counts as warm (50); below it a lead is cold.</summary>
    public static readonly StyledProperty<int> WarmThresholdProperty =
        AvaloniaProperty.Register<LeadScoring, int>(nameof(WarmThreshold), 50);

    /// <summary>The lead whose breakdown the detail panel shows (two-way); choosing a row sets it.</summary>
    public static readonly StyledProperty<LeadScoringLead?> ActiveLeadProperty =
        AvaloniaProperty.Register<LeadScoring, LeadScoringLead?>(nameof(ActiveLead), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Draws a bordered card (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<LeadScoring, bool>(nameof(IsFramed), true);

    /// <summary>The words the block shows and reads out.</summary>
    public static readonly StyledProperty<LeadScoringLabels> LabelsProperty =
        AvaloniaProperty.Register<LeadScoring, LeadScoringLabels>(nameof(Labels), new LeadScoringLabels());

    /// <summary>Raised after leads were moved to another stage, from the detail panel, the bulk bar or <see cref="MoveLeads"/>.</summary>
    public static readonly RoutedEvent<LeadScoringLeadsMovedEventArgs> LeadsMovedEvent =
        RoutedEvent.Register<LeadScoring, LeadScoringLeadsMovedEventArgs>(nameof(LeadsMoved), RoutingStrategies.Bubble);

    /// <summary>The leads. Empty: the built-in sample pipeline is shown.</summary>
    public AvaloniaList<LeadScoringLead> Leads { get; } = new();

    /// <summary>The pipeline's stages, in order. Empty: New, Working, Qualified and Nurture.</summary>
    public AvaloniaList<LeadScoringStage> Stages { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="StageFilterProperty"/>
    public string? StageFilter { get => GetValue(StageFilterProperty); set => SetValue(StageFilterProperty, value); }

    /// <inheritdoc cref="BandProperty"/>
    public LeadScoringBand Band { get => GetValue(BandProperty); set => SetValue(BandProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="HotThresholdProperty"/>
    public int HotThreshold { get => GetValue(HotThresholdProperty); set => SetValue(HotThresholdProperty, value); }

    /// <inheritdoc cref="WarmThresholdProperty"/>
    public int WarmThreshold { get => GetValue(WarmThresholdProperty); set => SetValue(WarmThresholdProperty, value); }

    /// <inheritdoc cref="ActiveLeadProperty"/>
    public LeadScoringLead? ActiveLead { get => GetValue(ActiveLeadProperty); set => SetValue(ActiveLeadProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public LeadScoringLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="LeadsMovedEvent"/>
    public event EventHandler<LeadScoringLeadsMovedEventArgs>? LeadsMoved
    {
        add => AddHandler(LeadsMovedEvent, value);
        remove => RemoveHandler(LeadsMovedEvent, value);
    }
}
