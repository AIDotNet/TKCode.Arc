namespace TKCode.Arc;

/// <summary>The words of a toast's second line: the meta, the relative time and the accessible sentence.</summary>
internal static class SocialProofToastFormat
{
    /// <summary>Relative time: under a minute "Just now", then minutes, hours and days (whole units, rounded down).</summary>
    public static string Relative(DateTimeOffset time, DateTimeOffset now, SocialProofToastLabels labels)
    {
        var ago = now - time;
        if (ago < TimeSpan.FromMinutes(1))
        {
            return labels.JustNow;
        }
        if (ago < TimeSpan.FromHours(1))
        {
            return labels.MinutesAgo((long)ago.TotalMinutes);
        }
        if (ago < TimeSpan.FromDays(1))
        {
            return labels.HoursAgo((long)ago.TotalHours);
        }
        return labels.DaysAgo((long)ago.TotalDays);
    }

    /// <summary>The event's time as shown: its own text, the relative time, or empty.</summary>
    public static string Time(SocialProofEvent e, DateTimeOffset now, SocialProofToastLabels labels)
    {
        if (!string.IsNullOrEmpty(e.TimeText))
        {
            return e.TimeText;
        }
        return e.Time is { } time ? Relative(time, now, labels) : string.Empty;
    }

    /// <summary>The second line without the badge: "Madrid · 2 min ago".</summary>
    public static string Meta(SocialProofEvent e, DateTimeOffset now, SocialProofToastLabels labels)
    {
        var time = Time(e, now, labels);
        if (string.IsNullOrEmpty(e.Meta))
        {
            return time;
        }
        return time.Length == 0 ? e.Meta : e.Meta + labels.Separator + time;
    }

    /// <summary>The badge's label, or null when the event is not verified.</summary>
    public static string? Verified(SocialProofEvent e, SocialProofToastLabels labels) =>
        !e.ShowsVerified ? null : string.IsNullOrEmpty(e.VerifiedLabel) ? labels.Verified : e.VerifiedLabel;

    /// <summary>What a screen reader reads for a toast: "Sofia started a Pro trial, Madrid · 2 min ago, Verified".</summary>
    public static string Accessible(SocialProofEvent e, DateTimeOffset now, SocialProofToastLabels labels)
    {
        var text = e.Sentence;
        var meta = Meta(e, now, labels);
        if (meta.Length > 0)
        {
            text += ", " + meta;
        }
        if (Verified(e, labels) is { } verified)
        {
            text += ", " + verified;
        }
        return text;
    }

    /// <summary>The letter of a company's monogram: the first letter or digit of its name, upper-cased.</summary>
    public static string Monogram(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                return char.ToUpperInvariant(c).ToString();
            }
        }
        return string.Empty;
    }
}
