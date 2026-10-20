using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class CommentThreadPage
{
    private static readonly CommentAuthor s_mina = new("mina", "Mina Park");
    private static readonly CommentAuthor s_theo = new("theo", "Theo Walsh");
    private static readonly CommentAuthor s_ana = new("ana", "Ana Ruiz");
    private static readonly CommentAuthor s_kai = new("kai", "Kai Moreno");

    /// <summary>A design review: replies, reactions, a mention and an edited comment.</summary>
    public static IReadOnlyList<ThreadComment> Review() =>
    [
        new ThreadComment("c1", s_mina, "The new headline reads well. @Theo Walsh can we tighten the subhead to a single line on mobile?", "2h")
        {
            Reactions = [new CommentReaction("👍", ["theo", "ana"]), new CommentReaction("👀", ["kai"])],
            Replies =
            [
                new ThreadComment("c2", s_theo, "Yes, trimming it to \"Ship calmer interfaces.\" Pushing in a few minutes.", "1h")
                {
                    Reactions = [new CommentReaction("🎉", ["mina"])]
                },
                new ThreadComment("c3", s_ana, "Checked it at 375px, it fits on one line now.", "45m") { IsEdited = true }
            ]
        },
        new ThreadComment("c4", s_kai, "Should the primary button say \"Start free\" or \"Get started\"? @Mina Park has the copy doc.", "20m")
    ];

    public static Control Create()
    {
        var status = GalleryUi.Text("Reply, react, edit or delete your own comments, then resolve the thread.", ArcDesignTokenKind.TextSecondary);
        var review = new CommentThread
        {
            Title = "Hero headline",
            CurrentUser = s_ana,
            Comments = Review()
        };
        review.CommentsChanged += (_, e) => status.Text = e.Change.Kind switch
        {
            CommentThreadChangeKind.Reply => e.Change.ParentId is null ? "New comment" : $"Reply to {e.Change.ParentId}",
            CommentThreadChangeKind.Edit => $"Edited {e.Change.Id}",
            CommentThreadChangeKind.Delete => $"Deleted {e.Change.Id}",
            _ => $"{(e.Change.Added ? "Added" : "Removed")} {e.Change.Emoji} on {e.Change.Id}"
        };
        review.ResolvedChanged += (_, e) => status.Text = e.IsResolved ? "Resolved" : "Reopened";

        var resolved = new CommentThread
        {
            Title = "Pricing table spacing",
            CurrentUser = s_ana,
            IsResolved = true,
            Comments =
            [
                new ThreadComment("r1", s_theo, "The gap between plans feels tight at 1024px.", "Sep 18")
                {
                    Replies = [new ThreadComment("r2", s_mina, "Bumped it to 24px, take a look.", "Sep 18")]
                },
                new ThreadComment("r3", s_theo, "Perfect, thanks!", "Sep 19")
            ]
        };

        var empty = new CommentThread
        {
            Title = "Footer links",
            CurrentUser = s_ana,
            People = [s_mina, s_theo, s_ana, s_kai]
        };

        var narrow = new CommentThread
        {
            CurrentUser = s_ana,
            Comments = Review(),
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Design review",
                "Replies nest under their comment and collapse; reactions roll their counts; type @ in the composer to mention someone and press Ctrl or Cmd + Enter to send.",
                GalleryUi.Column(12, Center(review), status)),
            GalleryUi.Card("Resolved", "Resolving folds the thread into one chip that keeps who took part; Reopen brings it back.", Center(resolved)),
            GalleryUi.Card("Empty thread", "With no comments the thread invites the first one; mentions offer the People list.", Center(empty)),
            GalleryUi.Card("Narrow", "Under 420px the composer drops its avatar and replies indent 12px.", narrow));
    }

    private static Control Center(Control content)
    {
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        return new Border { MaxWidth = 520, Child = content };
    }
}
