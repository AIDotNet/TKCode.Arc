// Ported from Arc's free comparison-table block (MIT, Copyright (c) 2026 Elia Kuratli): the data model of comparison-table-data.ts.
using System.Windows.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a <see cref="ComparisonValue"/> shows.</summary>
public enum ComparisonMark
{
    /// <summary>A dash: not included.</summary>
    NotIncluded,

    /// <summary>A check in a circle: included.</summary>
    Included,

    /// <summary>A half filled circle: limited, usually with a note.</summary>
    Partial,

    /// <summary>Printed text, such as a limit or a price.</summary>
    Text
}

/// <summary>
/// One cell of a <see cref="ComparisonTable"/>. A <see cref="bool"/> converts to a check or a dash, a string to printed text.
/// Two values are the same for "only differences" when their mark and text match; notes do not count.
/// </summary>
/// <param name="Mark">What the cell shows.</param>
/// <param name="Text">The printed text of a <see cref="ComparisonMark.Text"/> value.</param>
/// <param name="Note">A short line under a partial mark.</param>
public readonly record struct ComparisonValue(ComparisonMark Mark, string? Text = null, string? Note = null)
{
    /// <summary>Included.</summary>
    public static readonly ComparisonValue Included = new(ComparisonMark.Included);

    /// <summary>Not included.</summary>
    public static readonly ComparisonValue NotIncluded = new(ComparisonMark.NotIncluded);

    /// <summary>Limited, with an optional note under the mark.</summary>
    public static ComparisonValue Partial(string? note = null) => new(ComparisonMark.Partial, null, note);

    /// <summary>Converts to <see cref="Included"/> or <see cref="NotIncluded"/>.</summary>
    public static implicit operator ComparisonValue(bool included) => included ? Included : NotIncluded;

    /// <summary>Converts to printed text.</summary>
    public static implicit operator ComparisonValue(string text) => new(ComparisonMark.Text, text);

    internal string Key => Mark == ComparisonMark.Text ? "t:" + Text : Mark.ToString();

    /// <summary>The text alternative of the cell, with the labels of the table.</summary>
    internal string AccessibleName(string included, string partial, string notIncluded) => Mark switch
    {
        ComparisonMark.Included => included,
        ComparisonMark.Partial => string.IsNullOrEmpty(Note) ? partial : $"{partial}, {Note}",
        ComparisonMark.Text => Text ?? string.Empty,
        _ => notIncluded
    };
}

/// <summary>A product to compare: one column of a <see cref="ComparisonTable"/>.</summary>
public sealed class ComparisonColumn
{
    /// <summary>The stable id that <see cref="ComparisonRow.Values"/> uses.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The column heading.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A short line under the name, such as a price.</summary>
    public string? Caption { get; set; }

    /// <summary>Your product: it gets the tinted band and stays visible in the stacked layout.</summary>
    public bool IsHighlighted { get; set; }
}

/// <summary>One feature row of a <see cref="ComparisonTable"/>.</summary>
public sealed class ComparisonRow
{
    /// <summary>The stable id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The feature name.</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>Secondary text under the feature name.</summary>
    public string? Hint { get; set; }

    /// <summary>The value per column id. A column without a value shows <see cref="ComparisonValue.NotIncluded"/>.</summary>
    public Dictionary<string, ComparisonValue> Values { get; set; } = new();
}

/// <summary>A titled group of rows of a <see cref="ComparisonTable"/>.</summary>
public sealed class ComparisonSection
{
    /// <summary>The stable id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The group title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The rows of the group.</summary>
    public List<ComparisonRow> Rows { get; set; } = new();
}

/// <summary>
/// The call to action at the foot of your column. With <see cref="Href"/> and no <see cref="Command"/> it opens a link; otherwise
/// it runs the command and, with a <see cref="DoneLabel"/>, turns into a quiet confirmation.
/// </summary>
public sealed class ComparisonCta
{
    /// <summary>The button text.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The destination of a link.</summary>
    public string? Href { get; set; }

    /// <summary>Runs when the button is pressed.</summary>
    public ICommand? Command { get; set; }

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get; set; }

    /// <summary>The text after a press; it morphs in with a check.</summary>
    public string? DoneLabel { get; set; }
}

/// <summary>Arguments of <see cref="ComparisonTable.CtaInvoked"/>.</summary>
public sealed class ComparisonCtaEventArgs : RoutedEventArgs
{
    internal ComparisonCtaEventArgs(RoutedEvent routedEvent, object source, ComparisonCta cta) : base(routedEvent, source) => Cta = cta;

    /// <summary>The action that was pressed. Mark the event handled to stop a link from opening.</summary>
    public ComparisonCta Cta { get; }
}

/// <summary>Invented sample data for the default table; the competitors are generic categories, not real products.</summary>
internal static class ComparisonTableSamples
{
    public static List<ComparisonColumn> Columns() =>
    [
        new() { Id = "fieldnote", Name = "Fieldnote", Caption = "$12 per seat", IsHighlighted = true },
        new() { Id = "suite", Name = "Big suite", Caption = "$27 per seat" },
        new() { Id = "docs", Name = "Shared docs", Caption = "Free" },
        new() { Id = "apps", Name = "Single apps", Caption = "$15 per seat" },
    ];

    public static List<ComparisonSection> Sections() =>
    [
        new()
        {
            Id = "plan", Title = "Planning",
            Rows =
            [
                Row("boards", "Boards", null, ("fieldnote", "Unlimited"), ("suite", "Up to 40"), ("docs", "Unlimited"), ("apps", "Up to 8")),
                Row("calendar", "Calendar view", null, ("fieldnote", true), ("suite", true), ("docs", false), ("apps", ComparisonValue.Partial("Read only"))),
                Row("links", "Linked tasks", "A task moves when the one before it does", ("fieldnote", true), ("suite", true), ("docs", false), ("apps", false)),
                Row("starters", "Starter layouts", null, ("fieldnote", true), ("suite", true), ("docs", ComparisonValue.Partial("Copy by hand")), ("apps", true)),
            ]
        },
        new()
        {
            Id = "team", Title = "Working together",
            Rows =
            [
                Row("comments", "Threaded comments", null, ("fieldnote", true), ("suite", true), ("docs", true), ("apps", true)),
                Row("guests", "Free guests", "Invite people outside your company", ("fieldnote", true), ("suite", ComparisonValue.Partial("Paid add-on")), ("docs", true), ("apps", false)),
                Row("offline", "Works offline", null, ("fieldnote", true), ("suite", false), ("docs", ComparisonValue.Partial("Mobile only")), ("apps", false)),
            ]
        },
        new()
        {
            Id = "admin", Title = "Security and support",
            Rows =
            [
                Row("sso", "Single sign-on", null, ("fieldnote", true), ("suite", true), ("docs", false), ("apps", ComparisonValue.Partial("Top plan"))),
                Row("audit", "Audit log", null, ("fieldnote", true), ("suite", true), ("docs", false), ("apps", false)),
                Row("replies", "First reply", "Median on weekdays", ("fieldnote", "Under 2 hours"), ("suite", "1 day"), ("docs", "Forum"), ("apps", "2 days")),
            ]
        },
    ];

    private static ComparisonRow Row(string id, string feature, string? hint, params (string Column, ComparisonValue Value)[] values)
    {
        var row = new ComparisonRow { Id = id, Feature = feature, Hint = hint };
        foreach (var (column, value) in values)
        {
            row.Values[column] = value;
        }
        return row;
    }
}
