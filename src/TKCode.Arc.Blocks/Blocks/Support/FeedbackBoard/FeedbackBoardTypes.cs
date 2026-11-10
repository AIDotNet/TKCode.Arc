using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a request on a <see cref="FeedbackBoard"/> stands.</summary>
public enum FeedbackStatus
{
    /// <summary>New, not yet decided.</summary>
    UnderReview,

    /// <summary>Accepted and on the plan.</summary>
    Planned,

    /// <summary>Being built.</summary>
    InProgress,

    /// <summary>Shipped.</summary>
    Done
}

/// <summary>How a <see cref="FeedbackBoard"/> orders its requests.</summary>
public enum FeedbackSort
{
    /// <summary>Most votes first.</summary>
    Top,

    /// <summary>Newest first.</summary>
    Newest,

    /// <summary>Votes weighed by age: recent requests that gather votes quickly first.</summary>
    Trending
}

/// <summary>
/// One feature request on a <see cref="FeedbackBoard"/>. Its fields change in place (it raises
/// <see cref="PropertyChanged"/>): the board updates <see cref="Votes"/>, <see cref="HasVoted"/> and
/// <see cref="Comments"/> as people vote and comment, and your code can move <see cref="Status"/>.
/// </summary>
public sealed class FeedbackRequest : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string? _description;
    private FeedbackStatus _status;
    private int _votes;
    private bool _hasVoted;
    private IReadOnlyList<ThreadComment> _comments = Array.Empty<ThreadComment>();

    /// <summary>Creates an empty request (XAML).</summary>
    public FeedbackRequest()
    {
    }

    /// <summary>Creates a request.</summary>
    public FeedbackRequest(string id, string title, string? description, FeedbackStatus status, int votes, DateTimeOffset createdAt)
    {
        Id = id;
        _title = title;
        _description = description;
        _status = status;
        _votes = votes;
        CreatedAt = createdAt;
    }

    /// <summary>Raised when a property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The request in a line.</summary>
    public string Title { get => _title; set => Set(ref _title, value); }

    /// <summary>The longer explanation; null shows none.</summary>
    public string? Description { get => _description; set => Set(ref _description, value); }

    /// <summary>Where it stands.</summary>
    public FeedbackStatus Status { get => _status; set => Set(ref _status, value); }

    /// <summary>Votes, the viewer's included when <see cref="HasVoted"/>.</summary>
    public int Votes { get => _votes; set => Set(ref _votes, Math.Max(0, value)); }

    /// <summary>Whether the viewer voted for it.</summary>
    public bool HasVoted { get => _hasVoted; set => Set(ref _hasVoted, value); }

    /// <summary>When it was posted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Who posted it; null shows no name.</summary>
    public string? Author { get; set; }

    /// <summary>The discussion, oldest first (the board replaces it as people comment).</summary>
    public IReadOnlyList<ThreadComment> Comments { get => _comments; set => Set(ref _comments, value ?? Array.Empty<ThreadComment>()); }

    /// <summary>Every comment and reply, deleted ones left out.</summary>
    public int CommentCount => Count(_comments);

    private static int Count(IReadOnlyList<ThreadComment> comments)
    {
        var count = 0;
        foreach (var comment in comments)
        {
            count += (comment.IsDeleted ? 0 : 1) + Count(comment.Replies);
        }
        return count;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Arguments of <see cref="FeedbackBoard.VoteChanged"/> and <see cref="FeedbackBoard.RequestSubmitted"/>.</summary>
public sealed class FeedbackRequestEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FeedbackRequestEventArgs(RoutedEvent routedEvent, FeedbackRequest request)
        : base(routedEvent)
    {
        Request = request;
    }

    /// <summary>The request voted on or submitted (read <see cref="FeedbackRequest.HasVoted"/> for the new vote).</summary>
    public FeedbackRequest Request { get; }
}
