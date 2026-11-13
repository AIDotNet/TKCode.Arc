using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which part of a lead's score a <see cref="LeadScoringFactor"/> belongs to.</summary>
public enum LeadScoringFactorKind
{
    /// <summary>How well the company and the person match the customers you win (size, industry, role).</summary>
    Fit,

    /// <summary>What the person did (visits, opens, replies, events).</summary>
    Engagement,

    /// <summary>Signs that they are ready to buy (a pricing page, a demo request, a trial).</summary>
    Intent,
}

/// <summary>Which score band leads are filtered to.</summary>
public enum LeadScoringBand
{
    /// <summary>Every score.</summary>
    All,

    /// <summary>At or above the hot threshold.</summary>
    Hot,

    /// <summary>Between the warm and the hot threshold.</summary>
    Warm,

    /// <summary>Below the warm threshold.</summary>
    Cold,
}

/// <summary>One reason behind a lead's score: a short label and the points it adds (or, when negative, takes away).</summary>
public sealed class LeadScoringFactor
{
    /// <summary>Creates an empty factor.</summary>
    public LeadScoringFactor()
    {
    }

    /// <summary>Creates a factor from a label, its points and its kind.</summary>
    public LeadScoringFactor(string label, int points, LeadScoringFactorKind kind)
    {
        Label = label;
        Points = points;
        Kind = kind;
    }

    /// <summary>What the factor is ("Visited pricing 3 times").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The points it adds to the score; negative points take some away.</summary>
    public int Points { get; set; }

    /// <summary>Which part of the score it belongs to.</summary>
    public LeadScoringFactorKind Kind { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>A stage of the lead pipeline ("New", "Working", "Qualified").</summary>
public sealed class LeadScoringStage
{
    /// <summary>Creates an empty stage.</summary>
    public LeadScoringStage()
    {
    }

    /// <summary>Creates a stage from its key and its title.</summary>
    public LeadScoringStage(string key, string title)
    {
        Key = key;
        Title = title;
    }

    /// <summary>The key leads use in <see cref="LeadScoringLead.StageKey"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The name shown in the stage tabs and the stage picker.</summary>
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>A lead: a person at a company, the stage they are in and the factors that make up their score.</summary>
public sealed class LeadScoringLead
{
    /// <summary>Creates an empty lead.</summary>
    public LeadScoringLead()
    {
    }

    /// <summary>Creates a lead from a key, a name, a company and a stage key.</summary>
    public LeadScoringLead(string key, string name, string? company, string stageKey)
    {
        Key = key;
        Name = name;
        Company = company;
        StageKey = stageKey;
    }

    /// <summary>A stable key (keeps the selection across filters).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The person's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The company; null leaves it out.</summary>
    public string? Company { get; set; }

    /// <summary>The person's job title; null leaves it out.</summary>
    public string? JobTitle { get; set; }

    /// <summary>The <see cref="LeadScoringStage.Key"/> of the stage the lead is in; the block writes it when the lead is moved.</summary>
    public string StageKey { get; set; } = string.Empty;

    /// <summary>When the lead last did something, as shown ("2h ago"); null leaves it out.</summary>
    public string? LastActivity { get; set; }

    /// <summary>The reasons behind the score.</summary>
    public List<LeadScoringFactor> Factors { get; set; } = new();

    /// <summary>A score set by hand; null adds up <see cref="Factors"/>.</summary>
    public int? ScoreOverride { get; set; }

    /// <summary>The score from 0 to 100: <see cref="ScoreOverride"/>, or the factors' points added up and clamped.</summary>
    public int Score => Math.Clamp(ScoreOverride ?? Factors.Sum(f => f.Points), 0, 100);

    /// <summary>The factor that adds the most points, or null.</summary>
    public LeadScoringFactor? TopFactor => Factors.Where(f => f.Points > 0).OrderByDescending(f => f.Points).FirstOrDefault();

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Arguments of <see cref="LeadScoring.LeadsMoved"/>.</summary>
public sealed class LeadScoringLeadsMovedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LeadScoringLeadsMovedEventArgs(RoutedEvent routedEvent, IReadOnlyList<LeadScoringLead> leads,
        IReadOnlyList<string> fromStageKeys, LeadScoringStage stage)
        : base(routedEvent)
    {
        Leads = leads;
        FromStageKeys = fromStageKeys;
        Stage = stage;
    }

    /// <summary>The leads that moved (their <see cref="LeadScoringLead.StageKey"/> is already the new stage).</summary>
    public IReadOnlyList<LeadScoringLead> Leads { get; }

    /// <summary>The stage each lead was in before, in the order of <see cref="Leads"/>.</summary>
    public IReadOnlyList<string> FromStageKeys { get; }

    /// <summary>The stage they moved to.</summary>
    public LeadScoringStage Stage { get; }
}

/// <summary>The words <see cref="LeadScoring"/> shows and reads out; every one has an English default.</summary>
public sealed class LeadScoringLabels
{
    /// <summary>The first stage tab, which shows every stage.</summary>
    public string AllStages { get; init; } = "All";

    /// <summary>The score band options, in the order of <see cref="LeadScoringBand"/>.</summary>
    public IReadOnlyList<string> Bands { get; init; } = ["Any score", "Hot", "Warm", "Cold"];

    /// <summary>The screen reader name of the stage tabs.</summary>
    public string StagesName { get; init; } = "Stage";

    /// <summary>The screen reader name of the score band switch.</summary>
    public string BandName { get; init; } = "Score";

    /// <summary>The search placeholder.</summary>
    public string Search { get; init; } = "Search leads";

    /// <summary>The table's caption (its screen reader name).</summary>
    public string TableCaption { get; init; } = "Leads";

    /// <summary>The lead column's header.</summary>
    public string LeadColumn { get; init; } = "Lead";

    /// <summary>The score column's header.</summary>
    public string ScoreColumn { get; init; } = "Score";

    /// <summary>The stage column's header.</summary>
    public string StageColumn { get; init; } = "Stage";

    /// <summary>The top signal column's header.</summary>
    public string SignalColumn { get; init; } = "Top signal";

    /// <summary>The last activity column's header.</summary>
    public string ActivityColumn { get; init; } = "Last activity";

    /// <summary>The table's empty line.</summary>
    public string Empty { get; init; } = "No leads match these filters.";

    /// <summary>The selection line of the bulk bar; {0} is the count.</summary>
    public string SelectedFormat { get; init; } = "{0} selected";

    /// <summary>The stage picker's placeholder in the bulk bar.</summary>
    public string MoveTo { get; init; } = "Move to…";

    /// <summary>The bulk bar's move button.</summary>
    public string Move { get; init; } = "Move";

    /// <summary>The detail panel's heading above the factors.</summary>
    public string WhyHeading { get; init; } = "Why this score";

    /// <summary>The factor kinds' names, in the order of <see cref="LeadScoringFactorKind"/>.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = ["Fit", "Engagement", "Intent"];

    /// <summary>The detail panel's heading above the stage switch.</summary>
    public string StageHeading { get; init; } = "Stage";

    /// <summary>The button that moves the lead to the next stage; {0} is that stage.</summary>
    public string AdvanceFormat { get; init; } = "Move to {0}";

    /// <summary>The detail panel's line while no lead is chosen.</summary>
    public string NoLead { get; init; } = "Choose a lead to see what drives its score.";

    /// <summary>The announcement after a move; {0} is the count, {1} the stage.</summary>
    public string MovedFormat { get; init; } = "Moved {0} to {1}";

    /// <summary>"lead" and "leads" for the announcements.</summary>
    public string LeadOne { get; init; } = "lead";

    /// <summary>The plural of <see cref="LeadOne"/>.</summary>
    public string LeadOther { get; init; } = "leads";

    /// <summary>The count line under the title; {0} is the leads shown, {1} all leads.</summary>
    public string CountFormat { get; init; } = "{0} of {1} leads";
}
