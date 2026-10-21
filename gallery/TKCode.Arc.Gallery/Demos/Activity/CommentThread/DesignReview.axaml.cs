using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CommentThreadDesignReviewDemo : UserControl
{
    public CommentThreadDesignReviewDemo()
    {
        InitializeComponent();
    }

    private void OnCommentsChanged(object? sender, CommentsChangedEventArgs e)
    {
        Status.Text = e.Change.Kind switch
        {
            CommentThreadChangeKind.Reply => e.Change.ParentId is null ? "New comment" : $"Reply to {e.Change.ParentId}",
            CommentThreadChangeKind.Edit => $"Edited {e.Change.Id}",
            CommentThreadChangeKind.Delete => $"Deleted {e.Change.Id}",
            _ => $"{(e.Change.Added ? "Added" : "Removed")} {e.Change.Emoji} on {e.Change.Id}"
        };
    }

    private void OnResolvedChanged(object? sender, CommentThreadResolvedChangedEventArgs e)
    {
        Status.Text = e.IsResolved ? "Resolved" : "Reopened";
    }
}
