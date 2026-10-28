namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/projects: a List view (status chips, a sortable table, a project drawer) and a Board view (the
/// <see cref="ProjectBoard"/> block with Arc's defaults). A search result for a project opens the list on its drawer.
/// </summary>
public sealed class DemoProjectsPage : DemoPageView
{
    private DemoProjectsList? _list;

    public DemoProjectsPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        SetViews(new DemoView[]
        {
            new("list", "List", () => _list = new DemoProjectsList(Host)),
            new("board", "Board", BuildBoard)
        });
    }

    /// <summary>A project intent shows the list with no status filter and opens that project's drawer.</summary>
    public override void ApplyIntent(DemoIntent intent)
    {
        if (intent.Project is not { } project)
        {
            base.ApplyIntent(intent);
            return;
        }
        SelectView("list");
        _list?.Reveal(project);
    }

    private static ProjectBoard BuildBoard()
    {
        var board = new ProjectBoard();
        DemoData.FillBoard(board);
        return board;
    }
}
