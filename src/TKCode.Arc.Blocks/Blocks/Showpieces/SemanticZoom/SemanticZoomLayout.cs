using Avalonia;

namespace TKCode.Arc;

/// <summary>What a flat node of <see cref="SemanticZoomLayout"/> is.</summary>
internal enum SemanticZoomNodeKind : byte
{
    Team,
    Project,
    Task
}

/// <summary>
/// One laid-out node of a <see cref="SemanticZoom"/> map in world units: its box, its parent and the range of its children
/// in the flat node array.
/// </summary>
internal readonly struct SemanticZoomBox
{
    public SemanticZoomBox(SemanticZoomNode node, SemanticZoomNodeKind kind, int parent, Rect rect, int firstChild, int childCount, int indexInParent)
    {
        Node = node;
        Kind = kind;
        Parent = parent;
        Rect = rect;
        FirstChild = firstChild;
        ChildCount = childCount;
        IndexInParent = indexInParent;
    }

    public SemanticZoomNode Node { get; }

    public SemanticZoomNodeKind Kind { get; }

    /// <summary>Index of the parent box (−1 for teams: their parent is the organization).</summary>
    public int Parent { get; }

    /// <summary>World rectangle (team panel, project card or task block).</summary>
    public Rect Rect { get; }

    public int FirstChild { get; }

    public int ChildCount { get; }

    /// <summary>Position among the parent's children.</summary>
    public int IndexInParent { get; }
}

/// <summary>
/// The map's layout in world units (Arc Semantic zoom 4.2, design reference): teams packed into rows that share one
/// project-card row count, rows justified to the widest one by stretching card widths, project cards of one uniform
/// height with a 3-column grid of task blocks, a people row at the bottom. The layout depends only on the data, never on
/// the stage size (narrow stages only fit it at a smaller scale).
/// </summary>
internal sealed class SemanticZoomLayout
{
    /// <summary>Natural project card width (wu).</summary>
    public const double CardWidth = 150;

    /// <summary>Gap between project cards, in both directions.</summary>
    public const double CardGap = 12;

    /// <summary>Team panel inset at the sides and below the cards.</summary>
    public const double PanelPadding = 15;

    /// <summary>Team panel header band: from the panel's top edge to the first card row.</summary>
    public const double HeaderHeight = 63;

    /// <summary>Gap between teams in a row and between team rows.</summary>
    public const double TeamGap = 24;

    /// <summary>Task block height and the gap between blocks.</summary>
    public const double TileHeight = 21;

    public const double TileGap = 3;

    /// <summary>Task blocks per card row.</summary>
    public const int TileColumns = 3;

    /// <summary>Top of the task block grid inside a card, and the card's side inset.</summary>
    public const double TilesTop = 45;

    public const double CardInset = 9;

    /// <summary>Space below the last task row (the people row lives there): card height = 67.5 + 24 × rows.</summary>
    public const double CardFooter = 25.5;

    /// <summary>People row: dot radius, step between dots, and the row's center above the card bottom.</summary>
    public const double PersonRadius = 7.08;

    public const double PersonStep = 14.24;

    public const double PeopleRowFromBottom = 15;

    /// <summary>First person dot center from the card's left edge.</summary>
    public const double PeopleLeft = 15;

    /// <summary>Most project cards per team row before the row count grows (r = ceil(max projects / 4)).</summary>
    public const int ProjectsPerCardRow = 4;

    /// <summary>The content aspect the team-row packing aims for.</summary>
    public const double TargetAspect = 1.75;

    /// <summary>Brute-force row packing up to this many teams; greedy beyond.</summary>
    private const int ExhaustiveLimit = 14;

    public static readonly SemanticZoomLayout Empty = new(Array.Empty<SemanticZoomBox>(), 0, 0, 0, default, CardHeightFor(1));

    private SemanticZoomLayout(SemanticZoomBox[] boxes, int teams, int projects, int tasks, Rect content, double cardHeight)
    {
        Boxes = boxes;
        TeamCount = teams;
        ProjectCount = projects;
        TaskCount = tasks;
        ContentBounds = content;
        CardHeight = cardHeight;
    }

    /// <summary>Every box: teams first, then projects (team by team), then tasks (project by project).</summary>
    public SemanticZoomBox[] Boxes { get; }

    public int TeamCount { get; }

    public int ProjectCount { get; }

    public int TaskCount { get; }

    public int FirstProject => TeamCount;

    public int FirstTask => TeamCount + ProjectCount;

    /// <summary>World bounds of all teams (origin at the top-left team panel).</summary>
    public Rect ContentBounds { get; }

    /// <summary>The uniform project card height.</summary>
    public double CardHeight { get; }

    public bool IsEmpty => Boxes.Length == 0;

    /// <summary>Card height for a given number of task rows.</summary>
    public static double CardHeightFor(int taskRows) =>
        TilesTop + Math.Max(1, taskRows) * (TileHeight + TileGap) - TileGap + CardFooter;

    /// <summary>The index of <paramref name="node"/>'s box, or −1.</summary>
    public int IndexOf(SemanticZoomNode? node)
    {
        if (node is null)
        {
            return -1;
        }
        var boxes = Boxes;
        for (var i = 0; i < boxes.Length; i++)
        {
            if (ReferenceEquals(boxes[i].Node, node))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The task block rectangle of task <paramref name="indexInProject"/> on a card at <paramref name="card"/>.</summary>
    public static Rect TileRect(Rect card, int indexInProject)
    {
        var tileWidth = TileWidth(card.Width);
        var column = indexInProject % TileColumns;
        var row = indexInProject / TileColumns;
        return new Rect(card.X + CardInset + column * (tileWidth + TileGap),
            card.Y + TilesTop + row * (TileHeight + TileGap), tileWidth, TileHeight);
    }

    /// <summary>Task block width on a card of <paramref name="cardWidth"/> (42 on a 150 card).</summary>
    public static double TileWidth(double cardWidth) =>
        (cardWidth - 2 * CardInset - (TileColumns - 1) * TileGap) / TileColumns;

    /// <summary>
    /// Packs teams (their project counts, in order) into consecutive rows. Every row uses <c>ceil(max / 4)</c> card rows;
    /// a team takes <c>ceil(projects / r)</c> columns; rows are justified to the widest one. The packing with the best
    /// balance of row widths and an aspect near <see cref="TargetAspect"/> wins (6/4/4/4/3 → [6,4,4 | 4,3]).
    /// </summary>
    /// <returns>For every team, the index of its row.</returns>
    public static int[] PackRows(IReadOnlyList<int> projectCounts)
    {
        var n = projectCounts.Count;
        var best = new int[n];
        if (n <= 1)
        {
            return best;
        }
        if (n > ExhaustiveLimit)
        {
            return GreedyRows(projectCounts);
        }
        var bestScore = double.PositiveInfinity;
        var rowOf = new int[n];
        var cutMasks = 1 << (n - 1);
        for (var mask = 0; mask < cutMasks; mask++)
        {
            var row = 0;
            rowOf[0] = 0;
            for (var i = 1; i < n; i++)
            {
                if ((mask & (1 << (i - 1))) != 0)
                {
                    row++;
                }
                rowOf[i] = row;
            }
            var score = Score(projectCounts, rowOf, row + 1);
            if (score < bestScore - 1e-9)
            {
                bestScore = score;
                Array.Copy(rowOf, best, n);
            }
        }
        return best;
    }

    private static int[] GreedyRows(IReadOnlyList<int> projectCounts)
    {
        // Rows of about sqrt(n · aspect) columns' worth of teams; good enough for very large organizations.
        var n = projectCounts.Count;
        var rows = Math.Max(1, (int)Math.Round(Math.Sqrt(n / TargetAspect)));
        var perRow = (int)Math.Ceiling(n / (double)rows);
        var result = new int[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = i / perRow;
        }
        return result;
    }

    private static double Score(IReadOnlyList<int> counts, int[] rowOf, int rows)
    {
        Span<double> widths = stackalloc double[rows];
        Span<double> heights = stackalloc double[rows];
        Span<int> cardRows = stackalloc int[rows];
        Span<int> teams = stackalloc int[rows];
        for (var i = 0; i < counts.Count; i++)
        {
            cardRows[rowOf[i]] = Math.Max(cardRows[rowOf[i]], CardRowsFor(counts[i]));
        }
        for (var i = 0; i < counts.Count; i++)
        {
            var r = rowOf[i];
            var columns = ColumnsFor(counts[i], cardRows[r]);
            widths[r] += TeamWidth(columns, CardWidth) + (teams[r] > 0 ? TeamGap : 0);
            teams[r]++;
        }
        double maxWidth = 0, total = 0, height = 0;
        for (var r = 0; r < rows; r++)
        {
            maxWidth = Math.Max(maxWidth, widths[r]);
            total += widths[r];
            heights[r] = TeamHeight(cardRows[r], CardHeightFor(4));
            height += heights[r] + (r > 0 ? TeamGap : 0);
        }
        var fill = total / (rows * maxWidth);
        var aspect = maxWidth / Math.Max(1, height);
        return Math.Abs(Math.Log(aspect / TargetAspect)) + 2 * (1 - fill);
    }

    /// <summary>Card rows of a team row whose largest team has <paramref name="projects"/> projects.</summary>
    public static int CardRowsFor(int projects) => Math.Max(1, (int)Math.Ceiling(Math.Max(1, projects) / (double)ProjectsPerCardRow));

    /// <summary>Columns of a team with <paramref name="projects"/> projects in a row of <paramref name="cardRows"/> card rows.</summary>
    public static int ColumnsFor(int projects, int cardRows) => Math.Max(1, (int)Math.Ceiling(Math.Max(1, projects) / (double)Math.Max(1, cardRows)));

    /// <summary>Team panel width for <paramref name="columns"/> columns of cards <paramref name="cardWidth"/> wide.</summary>
    public static double TeamWidth(int columns, double cardWidth) =>
        columns * cardWidth + (columns - 1) * CardGap + 2 * PanelPadding;

    /// <summary>Team panel height for <paramref name="cardRows"/> rows of cards <paramref name="cardHeight"/> high.</summary>
    public static double TeamHeight(int cardRows, double cardHeight) =>
        HeaderHeight + cardRows * cardHeight + (cardRows - 1) * CardGap + PanelPadding;

    /// <summary>Lays out an organization.</summary>
    public static SemanticZoomLayout Build(SemanticZoomOrganization? organization)
    {
        if (organization is null || organization.Teams.Count == 0)
        {
            return Empty;
        }
        var teams = organization.Teams;
        var teamCount = teams.Count;
        var projectCount = 0;
        var taskCount = 0;
        var maxTasks = 0;
        var counts = new int[teamCount];
        for (var t = 0; t < teamCount; t++)
        {
            var projects = teams[t].Projects;
            counts[t] = projects.Count;
            projectCount += projects.Count;
            foreach (var project in projects)
            {
                taskCount += project.Tasks.Count;
                maxTasks = Math.Max(maxTasks, project.Tasks.Count);
            }
        }
        var cardHeight = CardHeightFor((int)Math.Ceiling(Math.Max(1, maxTasks) / (double)TileColumns));
        var rowOf = PackRows(counts);
        var rowCount = rowOf.Length == 0 ? 0 : rowOf[^1] + 1;

        // Card rows per team row, natural widths, the widest row.
        var cardRows = new int[rowCount];
        for (var t = 0; t < teamCount; t++)
        {
            cardRows[rowOf[t]] = Math.Max(cardRows[rowOf[t]], CardRowsFor(counts[t]));
        }
        var columns = new int[teamCount];
        var rowColumns = new int[rowCount];
        var rowTeams = new int[rowCount];
        for (var t = 0; t < teamCount; t++)
        {
            columns[t] = ColumnsFor(counts[t], cardRows[rowOf[t]]);
            rowColumns[rowOf[t]] += columns[t];
            rowTeams[rowOf[t]]++;
        }
        var rowConstant = new double[rowCount];
        for (var r = 0; r < rowCount; r++)
        {
            // Width = columns · cardWidth + constant (card gaps, panel padding, team gaps).
            rowConstant[r] = (rowTeams[r] - 1) * TeamGap;
        }
        for (var t = 0; t < teamCount; t++)
        {
            rowConstant[rowOf[t]] += (columns[t] - 1) * CardGap + 2 * PanelPadding;
        }
        double maxWidth = 0;
        for (var r = 0; r < rowCount; r++)
        {
            maxWidth = Math.Max(maxWidth, rowColumns[r] * CardWidth + rowConstant[r]);
        }

        var boxes = new SemanticZoomBox[teamCount + projectCount + taskCount];
        var projectIndex = teamCount;
        var taskIndex = teamCount + projectCount;
        double y = 0;
        var t0 = 0;
        for (var r = 0; r < rowCount; r++)
        {
            var cardWidth = rowColumns[r] > 0 ? (maxWidth - rowConstant[r]) / rowColumns[r] : CardWidth;
            var teamHeight = TeamHeight(cardRows[r], cardHeight);
            double x = 0;
            for (var t = t0; t < teamCount && rowOf[t] == r; t++)
            {
                var team = teams[t];
                var width = TeamWidth(columns[t], cardWidth);
                var panel = new Rect(x, y, width, teamHeight);
                boxes[t] = new SemanticZoomBox(team, SemanticZoomNodeKind.Team, -1, panel, projectIndex, team.Projects.Count, t);
                for (var p = 0; p < team.Projects.Count; p++)
                {
                    var project = team.Projects[p];
                    var column = p % columns[t];
                    var row = p / columns[t];
                    var card = new Rect(x + PanelPadding + column * (cardWidth + CardGap),
                        y + HeaderHeight + row * (cardHeight + CardGap), cardWidth, cardHeight);
                    boxes[projectIndex] = new SemanticZoomBox(project, SemanticZoomNodeKind.Project, t, card, taskIndex, project.Tasks.Count, p);
                    for (var k = 0; k < project.Tasks.Count; k++)
                    {
                        boxes[taskIndex] = new SemanticZoomBox(project.Tasks[k], SemanticZoomNodeKind.Task, projectIndex, TileRect(card, k), -1, 0, k);
                        taskIndex++;
                    }
                    projectIndex++;
                }
                x += width + TeamGap;
                t0 = t + 1;
            }
            y += teamHeight + TeamGap;
        }
        var height = Math.Max(0, y - TeamGap);
        return new SemanticZoomLayout(boxes, teamCount, projectCount, taskCount, new Rect(0, 0, maxWidth, height), cardHeight);
    }
}
