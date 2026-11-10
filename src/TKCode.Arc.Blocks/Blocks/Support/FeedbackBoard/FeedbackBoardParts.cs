using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The status pill of a feedback request: a word on a tint that says where it stands (never color alone).</summary>
internal static class FeedbackBoardStatus
{
    public static string Label(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Planned => "Planned",
        FeedbackStatus.InProgress => "In progress",
        FeedbackStatus.Done => "Done",
        _ => "Under review",
    };

    public static Border Pill(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Planned => SupportKit.Pill(Label(status), ArcDesignTokenKind.AccentSubtle, ArcDesignTokenKind.AccentStrong),
        FeedbackStatus.InProgress => SupportKit.Pill(Label(status), ArcDesignTokenKind.WarningSubtle, ArcDesignTokenKind.Warning),
        FeedbackStatus.Done => SupportKit.Pill(Label(status), ArcDesignTokenKind.SuccessSubtle, ArcDesignTokenKind.Success, ArcIcons.Check),
        _ => SupportKit.Pill(Label(status)),
    };
}

/// <summary>
/// The vote button of a feedback request: an up chevron over a rolling count, filled with the accent once the viewer
/// voted. Its name says the count and whether the viewer voted.
/// </summary>
internal sealed class FeedbackBoardVote
{
    private readonly RollingDigits _digits;

    public FeedbackBoardVote(FeedbackBoard board, FeedbackRequest request)
    {
        _digits = new RollingDigits { UseGrouping = true, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        ArcTokens.Bind(_digits, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        var face = new StackPanel { Spacing = 0, Children = { new ArcGlyph { Data = ArcIcons.ChevronUp, Size = 16, HorizontalAlignment = HorizontalAlignment.Center }, _digits } };
        Button = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Large, Content = face, MinWidth = 52, Padding = new Thickness(8, 4), VerticalAlignment = VerticalAlignment.Top };
        Button.Click += (_, e) =>
        {
            e.Handled = true;
            board.ToggleVote(request);
        };
        // The count follows the button's own text color (accent foreground once voted).
        _digits[!RollingDigits.ForegroundProperty] = Button[!Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty];
        Request = request;
        Update();
    }

    public Button Button { get; }

    public FeedbackRequest Request { get; }

    public double ShownValue => _digits.Value;

    public void Update()
    {
        var voted = Request.HasVoted;
        Button.Variant = voted ? ButtonVariant.Primary : ButtonVariant.Secondary;
        _digits.Value = Request.Votes;
        AutomationProperties.SetName(Button, voted
            ? $"Remove your vote, {SupportKit.Count(Request.Votes, "vote", "votes")}"
            : $"Vote for {Request.Title}, {SupportKit.Count(Request.Votes, "vote", "votes")}");
    }
}

/// <summary>One request in the list: the vote button, the title, two lines of the description, the status and the comment count.</summary>
internal sealed class FeedbackBoardRow
{
    private readonly FeedbackBoardVote _vote;
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly ContentControl _status;
    private readonly TextBlock _comments;

    public FeedbackBoardRow(FeedbackBoard board, FeedbackRequest request)
    {
        Request = request;
        _vote = new FeedbackBoardVote(board, request);
        _title = SupportKit.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.SemiBold, wrap: true);
        _description = SupportKit.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true);
        _description.MaxLines = 2;
        _description.TextTrimming = TextTrimming.CharacterEllipsis;
        _status = new ContentControl();
        _comments = SupportKit.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        var meta = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                _status,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SupportKit.Glyph(ArcIcons.MessageSquare, 13, ArcDesignTokenKind.TextMuted), _comments } },
            },
        };
        var text = new StackPanel { Spacing = 2, Children = { _title, _description, meta } };
        var grid = SupportKit.Grid("Auto,*", columnSpacing: 14);
        grid.Children.Add(_vote.Button.At(0));
        grid.Children.Add(text.At(1));
        Row = new SupportKitRow { Content = grid, Item = this, Padding = new Thickness(10, 10) };
        Row.Click += (_, _) => board.SelectedRequest = ReferenceEquals(board.SelectedRequest, request) ? null : request;
        Update();
    }

    public FeedbackRequest Request { get; }

    public SupportKitRow Row { get; }

    public FeedbackBoardVote Vote => _vote;

    private FeedbackStatus? _shownStatus;

    public void Update()
    {
        var request = Request;
        _title.Text = request.Title;
        _description.Text = request.Description;
        _description.IsVisible = !string.IsNullOrEmpty(request.Description);
        if (_shownStatus != request.Status)
        {
            _shownStatus = request.Status;
            _status.Content = FeedbackBoardStatus.Pill(request.Status);
        }
        var count = request.CommentCount;
        _comments.Text = count.ToString("N0", FeedbackBoard.Culture);
        _vote.Update();
        AutomationProperties.SetName(Row, $"{request.Title}, {FeedbackBoardStatus.Label(request.Status)}, {SupportKit.Count(request.Votes, "vote", "votes")}, {SupportKit.Count(count, "comment", "comments")}");
    }
}

/// <summary>
/// The open request: a back button when the board is stacked, the vote button, the title, status, author and date, the
/// description, and the comment thread whose changes go back into the request.
/// </summary>
internal sealed class FeedbackBoardDetail : Decorator
{
    private readonly FeedbackBoard _board;
    private readonly Button _back;
    private readonly Button _close;
    private readonly ContentControl _vote;
    private readonly TextBlock _title;
    private readonly ContentControl _status;
    private readonly TextBlock _meta;
    private readonly TextBlock _description;
    private FeedbackBoardVote? _voteButton;
    private FeedbackRequest? _request;
    private bool _syncing;

    public FeedbackBoardDetail(FeedbackBoard board)
    {
        _board = board;
        _back = SupportKit.Button("All requests", ButtonVariant.Ghost, ArcIcons.ArrowLeft);
        _back.HorizontalAlignment = HorizontalAlignment.Left;
        _back.Click += (_, _) => board.SelectedRequest = null;
        _close = SupportKit.IconButton(ArcIcons.X, "Close request");
        _close.HorizontalAlignment = HorizontalAlignment.Right;
        _close.Click += (_, _) => board.SelectedRequest = null;
        var top = SupportKit.Grid("*,Auto");
        top.Children.Add(_back.At(0));
        top.Children.Add(_close.At(1));

        _vote = new ContentControl { VerticalAlignment = VerticalAlignment.Top };
        _title = SupportKit.Heading(null, ArcDesignTokenKind.TypeLG, 3);
        _status = new ContentControl();
        _meta = SupportKit.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, wrap: true);
        var titleBlock = new StackPanel { Spacing = 6, Children = { _title, new WrapPanel { ItemSpacing = 8, LineSpacing = 4, Children = { _status, _meta } } } };
        var head = SupportKit.Grid("Auto,*", columnSpacing: 14);
        head.Children.Add(_vote.At(0));
        head.Children.Add(titleBlock.At(1));
        _description = SupportKit.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeSM, wrap: true);
        Thread = new CommentThread { Title = "Comments", Placeholder = "Add a comment…", CurrentUser = board.CurrentUser };
        Thread.AddHandler(CommentThread.CommentsChangedEvent, OnCommentsChanged);
        Child = new StackPanel { Spacing = 14, Children = { top, head, _description, SupportKit.Rule(), Thread } };
        AutomationProperties.SetName(this, "Request");
    }

    public CommentThread Thread { get; }

    public FeedbackRequest? Request => _request;

    public FeedbackBoardVote? Vote => _voteButton;

    public bool IsBackVisible
    {
        get => _back.IsVisible;
        set
        {
            _back.IsVisible = value;
            _close.IsVisible = !value;
        }
    }

    public void Show(FeedbackRequest? request)
    {
        _request = request;
        _voteButton = request is null ? null : new FeedbackBoardVote(_board, request);
        _vote.Content = _voteButton?.Button;
        Update();
    }

    public void Update()
    {
        if (_request is not { } request)
        {
            return;
        }
        _voteButton?.Update();
        _title.Text = request.Title;
        _status.Content = FeedbackBoardStatus.Pill(request.Status);
        var date = request.CreatedAt.ToString("MMM d, yyyy", FeedbackBoard.Culture);
        _meta.Text = request.Author is { Length: > 0 } author ? $"Posted by {author} on {date}" : $"Posted on {date}";
        _description.Text = request.Description;
        _description.IsVisible = !string.IsNullOrEmpty(request.Description);
        if (!ReferenceEquals(Thread.Comments, request.Comments))
        {
            _syncing = true;
            Thread.Comments = request.Comments;
            _syncing = false;
        }
    }

    private void OnCommentsChanged(object? sender, CommentsChangedEventArgs e)
    {
        e.Handled = true;
        if (_syncing || _request is null)
        {
            return;
        }
        _request.Comments = e.Comments;
        _board.RaiseCommentsChanged(e);
    }
}

/// <summary>The sample board of a <see cref="FeedbackBoard"/> that has no requests when it first loads.</summary>
internal static class FeedbackBoardSamples
{
    public static IEnumerable<FeedbackRequest> Create(DateTimeOffset now)
    {
        var ana = new CommentAuthor("ana", "Ana Ferreira");
        var jon = new CommentAuthor("jon", "Jonah Pike");
        var team = new CommentAuthor("team", "Wren from the team");
        return
        [
            new("f1", "Dark mode for the mobile app", "Night reading is rough on the eyes. The desktop app has it; the phone app should too.",
                FeedbackStatus.Planned, 214, now.AddDays(-41))
            {
                Author = "Ana Ferreira",
                Comments =
                [
                    new ThreadComment("c1", ana, "Would love a true black option for OLED screens.", "3d")
                    {
                        Replies = [new ThreadComment("c2", team, "Noted. We are designing both a dim and a black theme.", "2d")],
                    },
                    new ThreadComment("c3", jon, "Please follow the system setting by default.", "1d"),
                ],
            },
            new("f2", "Recurring tasks", "Let a task repeat every week or month without copying it by hand.",
                FeedbackStatus.InProgress, 187, now.AddDays(-63)) { Author = "Jonah Pike" },
            new("f3", "Bulk edit due dates", "Select many tasks and move their dates together.",
                FeedbackStatus.UnderReview, 96, now.AddDays(-5)) { Author = "Mira Osei" },
            new("f4", "Calendar sync with two-way updates", "Changes made in my calendar should flow back into the project.",
                FeedbackStatus.UnderReview, 71, now.AddDays(-2)) { Author = "Theo Lambert" },
            new("f5", "Keyboard shortcut cheat sheet", "A panel listing every shortcut, opened with the question mark key.",
                FeedbackStatus.Done, 132, now.AddDays(-90)) { Author = "Ana Ferreira" },
            new("f6", "Guest access for clients", "Invite a client to one project without giving them a seat.",
                FeedbackStatus.Planned, 158, now.AddDays(-20)) { Author = "Priya Shah" },
        ];
    }
}
