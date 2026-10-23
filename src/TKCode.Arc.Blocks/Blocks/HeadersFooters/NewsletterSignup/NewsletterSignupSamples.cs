// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): the sample publication of newsletter-signup-data.ts, without Arc's photos.
namespace TKCode.Arc;

/// <summary>Arc's sample publication "Margins": issue 149 upcoming and 146 to 148 recent, three stories each, no thumbnails.</summary>
internal static class NewsletterSignupSamples
{
    /// <summary>Creates a new sample publication (each block gets its own, so nobody edits a shared one).</summary>
    public static NewsletterPublication CreatePublication()
    {
        var publication = new NewsletterPublication
        {
            Name = "Margins",
            Upcoming = Issue(149, "Friday, October 2", "Designing for the second visit, not the first",
                ("Empty states that invite a first step", 5),
                ("Texture without noise, from a ceramics studio", 4),
                ("How a concert hall seats two thousand people", 7))
        };
        publication.Recent.Add(Issue(148, "Friday, September 25", "Why the best settings pages feel quiet",
            ("What a sunroom teaches about contrast", 4),
            ("The case for a single accent color", 3),
            ("Lisbon tram signs and wayfinding at scale", 6)));
        publication.Recent.Add(Issue(147, "Friday, September 18", "Springs that settle instead of bounce",
            ("Reading the ridge line of an animation curve", 5),
            ("A table lamp and the warmth of dark mode", 3),
            ("Curves in architecture and in corners", 4)));
        publication.Recent.Add(Issue(146, "Friday, September 11", "Copy that sounds like a person",
            ("Error messages that take the blame", 4),
            ("Writing buttons as verbs", 3),
            ("Tone of voice at the dinner table", 5)));
        return publication;
    }

    private static NewsletterIssue Issue(int number, string date, string subject, params (string Title, int Minutes)[] stories)
    {
        var issue = new NewsletterIssue { Number = number, Date = date, Subject = subject };
        foreach (var (title, minutes) in stories)
        {
            issue.Stories.Add(new NewsletterStory { Title = title, Minutes = minutes });
        }
        return issue;
    }
}
