namespace TKCode.Arc.Gallery.Demos;

/// <summary>The people and comments that the CommentThread demos show.</summary>
public static class CommentThreadDemoData
{
    public static CommentAuthor Mina { get; } = new("mina", "Mina Park");

    public static CommentAuthor Theo { get; } = new("theo", "Theo Walsh");

    public static CommentAuthor Ana { get; } = new("ana", "Ana Ruiz");

    public static CommentAuthor Kai { get; } = new("kai", "Kai Moreno");

    public static IReadOnlyList<CommentAuthor> People { get; } = [Mina, Theo, Ana, Kai];

    /// <summary>A design review: replies, reactions, a mention and an edited comment.</summary>
    public static IReadOnlyList<ThreadComment> Review { get; } =
    [
        new ThreadComment("c1", Mina, "The new headline reads well. @Theo Walsh can we tighten the subhead to a single line on mobile?", "2h")
        {
            Reactions = [new CommentReaction("👍", ["theo", "ana"]), new CommentReaction("👀", ["kai"])],
            Replies =
            [
                new ThreadComment("c2", Theo, "Yes, trimming it to \"Ship calmer interfaces.\" Pushing in a few minutes.", "1h")
                {
                    Reactions = [new CommentReaction("🎉", ["mina"])]
                },
                new ThreadComment("c3", Ana, "Checked it at 375px, it fits on one line now.", "45m") { IsEdited = true }
            ]
        },
        new ThreadComment("c4", Kai, "Should the primary button say \"Start free\" or \"Get started\"? @Mina Park has the copy doc.", "20m")
    ];

    /// <summary>A thread that was settled and folded away.</summary>
    public static IReadOnlyList<ThreadComment> Settled { get; } =
    [
        new ThreadComment("r1", Theo, "The gap between plans feels tight at 1024px.", "Sep 18")
        {
            Replies = [new ThreadComment("r2", Mina, "Bumped it to 24px, take a look.", "Sep 18")]
        },
        new ThreadComment("r3", Theo, "Perfect, thanks!", "Sep 19")
    ];
}
