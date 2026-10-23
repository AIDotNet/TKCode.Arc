namespace TKCode.Arc;

/// <summary>
/// The built-in story of <see cref="ReasoningTree"/> (invented sample content): a team plans to move a photo archive to
/// new storage over a weekend. Three first ideas, two follow-ups each and two refinements of those (21 thoughts).
/// The scores are written so that a greedy search (beam 1) settles for a weaker plan (0.88) and a beam of 2 or 3
/// finds the better one (0.93).
/// </summary>
internal static class ReasoningTreeSamples
{
    /// <summary>The model named in the header (an invented name).</summary>
    public const string ModelName = "Lumen 4";

    /// <summary>The header title.</summary>
    public const string Title = "Tree of thoughts";

    /// <summary>The short question on the root node.</summary>
    public const string Question = "How should we move the photo archive?";

    /// <summary>The full question for the tag and screen readers.</summary>
    public const string QuestionDetail =
        "How do we move 40 TB of customer photos to the new storage cluster this weekend without taking uploads offline?";

    /// <summary>A new copy of the sample thoughts (depth 1 with their children).</summary>
    public static IReadOnlyList<ReasoningThought> CreateThoughts() =>
    [
        Thought("A", "Copy in batches", 0.80,
            "Copy the archive in nightly batches of about 5 TB and keep the old cluster as the source of truth.",
            Thought("A1", "Oldest albums first", 0.85,
                "Start with albums nobody has opened in a year, so mistakes touch the fewest people.",
                Thought("A1a", "Done by Sunday", 0.88, "Finish the last batch Sunday night and switch reads first thing Monday."),
                Thought("A1b", "Verify on Monday", 0.69, "Copy everything by Saturday and spend Monday comparing checksums before the switch.")),
            Thought("A2", "Skip checksums", 0.28, "Skip the checksum pass to save about six hours of copy time.",
                Thought("A2a", "Trust the copy", 0.25, "Switch reads on Saturday and fix any broken photos when customers report them."),
                Thought("A2b", "Spot-check later", 0.20, "Sample one album in a thousand next week and repair what the sample finds."))),
        Thought("B", "Pause uploads", 0.35, "Turn uploads off for the weekend so nothing changes while the archive moves.",
            Thought("B1", "Announce a freeze", 0.33, "Email every customer on Thursday that uploads stop from Friday night to Monday.",
                Thought("B1a", "Freeze two nights", 0.31, "Keep the freeze to Friday and Saturday night and reopen uploads Sunday noon."),
                Thought("B1b", "Freeze all weekend", 0.40, "Keep uploads off until Monday morning and move everything in one pass.")),
            Thought("B2", "Queue new uploads", 0.47, "Accept uploads into a holding queue and replay it after the move.",
                Thought("B2a", "Replay overnight", 0.50, "Replay the queue Sunday night once the main copy has finished."),
                Thought("B2b", "Replay in waves", 0.55, "Replay the queue region by region while watching error rates."))),
        Thought("C", "Mirror, then switch", 0.72,
            "Mirror every new write to both clusters, backfill the old photos, then move reads over.",
            Thought("C1", "Dual-write a week", 0.64, "Run dual writes for a full week before trusting the new cluster.",
                Thought("C1a", "Switch next Friday", 0.74, "Move reads next Friday after a quiet week of dual writes."),
                Thought("C1b", "Shadow reads first", 0.79, "Serve reads from the old cluster but compare every answer with the new one.")),
            Thought("C2", "Switch by region", 0.78, "Move reads one region at a time, starting with the smallest.",
                Thought("C2a", "Small region first", 0.93,
                    "Switch the smallest region Saturday and the rest Sunday if error rates stay flat."),
                Thought("C2b", "Auto roll back", 0.86,
                    "Switch every region Saturday and roll back automatically if errors pass 0.3%."))),
    ];

    private static ReasoningThought Thought(string key, string label, double score, string text, params ReasoningThought[] children)
    {
        var thought = new ReasoningThought(label, score, text) { Key = key };
        thought.Children.AddRange(children);
        return thought;
    }
}
