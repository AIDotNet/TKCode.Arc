using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Support;

/// <summary>FeedbackBoard: the sample, sorting and filtering, votes, the open request and its comments, the form and the stacked layout.</summary>
public class FeedbackBoardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static (FeedbackBoard Board, Avalonia.Controls.Window Window) Show(double width = 1000, Action<FeedbackBoard>? configure = null)
    {
        var board = new FeedbackBoard { Clock = () => Now };
        configure?.Invoke(board);
        var window = TestHelpers.Show(board);
        window.Width = width;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        return (board, window);
    }

    private static FeedbackRequest Request(string id, int votes, FeedbackStatus status, int ageDays) =>
        new(id, "Request " + id, null, status, votes, Now.AddDays(-ageDays));

    [AvaloniaFact]
    public void The_Sample_Lists_Every_Request_Most_Votes_First()
    {
        var (board, window) = Show();
        try
        {
            board.Requests.Count.ShouldBe(6);
            var votes = board.VisibleRows.Select(r => r.Request.Votes).ToList();
            votes.ShouldBe(votes.OrderByDescending(v => v).ToList());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sorting_And_Filtering_Change_The_Rows()
    {
        var a = Request("a", 50, FeedbackStatus.Planned, 30);
        var b = Request("b", 4, FeedbackStatus.UnderReview, 1);
        var c = Request("c", 30, FeedbackStatus.Planned, 5);
        var (board, window) = Show(configure: x => x.Requests.AddRange([a, b, c]));
        try
        {
            board.VisibleRows.Select(r => r.Request).ShouldBe([a, c, b]);
            board.SortOrder = FeedbackSort.Newest;
            board.VisibleRows.Select(r => r.Request).ShouldBe([b, c, a]);
            board.SortOrder = FeedbackSort.Trending;
            board.VisibleRows[0].Request.ShouldBe(c, "votes per day weigh recent requests up");
            board.StatusFilter = FeedbackStatus.Planned;
            board.VisibleRows.Select(r => r.Request).ShouldBe([c, a]);
            board.ChipFor(FeedbackStatus.Planned).IsChecked.ShouldBe(true);
            board.ChipFor(null).IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Vote_Counts_Reorders_And_Can_Be_Taken_Back()
    {
        var a = Request("a", 10, FeedbackStatus.Planned, 3);
        var b = Request("b", 10, FeedbackStatus.Planned, 5);
        var (board, window) = Show(configure: x => x.Requests.AddRange([a, b]));
        try
        {
            board.VisibleRows[0].Request.ShouldBe(a, "a tie goes to the newer request");
            FeedbackRequest? changed = null;
            board.VoteChanged += (_, e) => changed = e.Request;
            board.ToggleVote(b);
            b.Votes.ShouldBe(11);
            b.HasVoted.ShouldBeTrue();
            changed.ShouldBe(b);
            board.VisibleRows[0].Request.ShouldBe(b);
            board.VisibleRows[0].Vote.ShownValue.ShouldBe(11);
            board.ToggleVote(b);
            b.Votes.ShouldBe(10);
            b.HasVoted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_A_Request_Shows_It_With_Its_Comments()
    {
        var a = Request("a", 3, FeedbackStatus.Done, 2);
        var (board, window) = Show(configure: x => x.Requests.Add(a));
        try
        {
            board.IsDetailShown.ShouldBeFalse();
            board.SelectedRequest = a;
            board.IsDetailShown.ShouldBeTrue();
            board.Detail.Request.ShouldBe(a);
            board.Detail.IsBackVisible.ShouldBeFalse();

            var comment = new ThreadComment("x", new CommentAuthor("me", "You"), "Same here", "now");
            a.Comments = [comment];
            board.Detail.Thread.Comments.ShouldBe([comment]);
            a.CommentCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Submitting_Adds_A_Request_Under_Review_With_The_Authors_Vote()
    {
        var (board, window) = Show(configure: x => x.UsesSampleData = false);
        try
        {
            FeedbackRequest? submitted = null;
            board.RequestSubmitted += (_, e) => submitted = e.Request;
            board.Submit("   ", null).ShouldBeNull();
            var request = board.Submit("Export to PDF", "For the monthly report")!;
            request.Status.ShouldBe(FeedbackStatus.UnderReview);
            request.Votes.ShouldBe(1);
            request.HasVoted.ShouldBeTrue();
            submitted.ShouldBe(request);
            board.SelectedRequest.ShouldBe(request);
            board.Requests.ShouldContain(request);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Boards_Open_A_Request_In_Place_Of_The_List()
    {
        var a = Request("a", 3, FeedbackStatus.Planned, 2);
        var (board, window) = Show(width: 520, configure: x => x.Requests.Add(a));
        try
        {
            board.IsStacked.ShouldBeTrue();
            board.SelectedRequest = a;
            board.Detail.IsBackVisible.ShouldBeTrue();
            board.SelectedRequest = null;
            board.IsDetailShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
