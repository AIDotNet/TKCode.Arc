using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One testimonial of a <see cref="StudioPerspectives"/> stage: the quote, the person who said it and where they work.
/// Without a picture the person gets a generated portrait. Set the properties before the quote is added; later changes
/// are not observed.
/// </summary>
public sealed class StudioPerspectivesQuote
{
    /// <summary>The quote itself, without quotation marks.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The person's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person's job title; null leaves it out.</summary>
    public string? Role { get; set; }

    /// <summary>The company or studio; null leaves it out.</summary>
    public string? Company { get; set; }

    /// <summary>A picture that replaces the generated portrait.</summary>
    public IImage? Image { get; set; }

    /// <summary>Chooses the generated portrait; null derives it from <see cref="Name"/>.</summary>
    public int? Seed { get; set; }

    /// <summary>The line under the name: the role and the company joined by a middle dot.</summary>
    public string Byline => string.Join(" · ", new[] { Role, Company }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Arguments of <see cref="StudioPerspectives.QuoteChanged"/>.</summary>
public sealed class StudioPerspectivesChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public StudioPerspectivesChangedEventArgs(RoutedEvent routedEvent, int index, StudioPerspectivesQuote quote, bool isAutomatic)
        : base(routedEvent)
    {
        Index = index;
        Quote = quote;
        IsAutomatic = isAutomatic;
    }

    /// <summary>The index of the quote now on stage.</summary>
    public int Index { get; }

    /// <summary>The quote now on stage.</summary>
    public StudioPerspectivesQuote Quote { get; }

    /// <summary>True when the timer moved on; false when a person or the app did.</summary>
    public bool IsAutomatic { get; }
}

/// <summary>The invented quotes <see cref="StudioPerspectives"/> shows with no configuration.</summary>
internal static class StudioPerspectivesSamples
{
    public static StudioPerspectivesQuote[] Create() =>
    [
        new() { Text = "We stopped arguing about whose spreadsheet was right. The whole studio plans from one board now, and Monday stand-ups take ten minutes.",
            Name = "Amara Lindqvist", Role = "Creative Director", Company = "Fieldnote Studio" },
        new() { Text = "It feels like the tool was designed by people who have shipped under a deadline. Nothing gets in the way, and everything is where my hands expect it.",
            Name = "Diego Ferraz", Role = "Lead Engineer", Company = "Harbor & Pine" },
        new() { Text = "Our clients see the same timeline we do. That one change did more for trust than any status email we ever wrote.",
            Name = "Yuki Tanabe", Role = "Producer", Company = "Lumen Works" },
        new() { Text = "Onboarding a new designer used to take a week of walkthroughs. Now they open the project and the history explains itself.",
            Name = "Oliver Achebe", Role = "Design Manager", Company = "Northbeam" },
    ];
}
