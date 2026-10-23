namespace TKCode.Arc;

/// <summary>A row of cells: everyone's cards (Status view), one person's, or the cards nobody owns.</summary>
internal sealed class KanbanLane
{
    public KanbanLane(ProjectPerson? person, bool isUnassigned)
    {
        Person = person;
        IsUnassigned = isUnassigned;
    }

    /// <summary>The lane's person (null in Status view and for the unassigned lane).</summary>
    public ProjectPerson? Person { get; }

    /// <summary>True for the trailing lane of cards whose owner is not on the board.</summary>
    public bool IsUnassigned { get; }

    /// <summary>True for Status view's single lane.</summary>
    public bool IsAll => Person is null && !IsUnassigned;

    /// <summary>The owner key a card dropped in this lane gets (null: nobody).</summary>
    public string? AssigneeKey => Person?.Identity;
}

/// <summary>
/// Which card sits in which cell: the board's flat card list split by column and, in swimlanes, by owner, keeping the
/// list order inside every cell (Arc: the array order is the order within each column). Cards whose column is not on
/// the board are left out; in swimlanes, cards whose owner is not among the people form a trailing unassigned lane.
/// </summary>
internal sealed class KanbanGrid
{
    private readonly List<KanbanCard>[] _cells;
    private readonly Dictionary<KanbanCard, (int Lane, int Column, int Index)> _where = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, int> _columnIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _laneIndex = new(StringComparer.Ordinal);

    private KanbanGrid(IReadOnlyList<KanbanColumn> columns, List<KanbanLane> lanes, KanbanGroupBy groupBy)
    {
        Columns = columns;
        Lanes = lanes;
        GroupBy = groupBy;
        _cells = new List<KanbanCard>[lanes.Count * columns.Count];
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i] = new List<KanbanCard>();
        }
        for (var i = 0; i < columns.Count; i++)
        {
            _columnIndex.TryAdd(columns[i].Identity, i);
        }
        for (var i = 0; i < lanes.Count; i++)
        {
            if (lanes[i].Person is { } person)
            {
                _laneIndex.TryAdd(person.Identity, i);
            }
        }
    }

    public IReadOnlyList<KanbanColumn> Columns { get; }

    public IReadOnlyList<KanbanLane> Lanes { get; }

    public KanbanGroupBy GroupBy { get; }

    public bool HasLaneHeads => GroupBy == KanbanGroupBy.Assignee;

    /// <summary>Every placed card, in board order.</summary>
    public List<KanbanCard> Placed { get; } = new();

    /// <summary>Splits <paramref name="cards"/> into cells.</summary>
    public static KanbanGrid Build(IReadOnlyList<KanbanCard> cards, IReadOnlyList<KanbanColumn> columns,
                                   IReadOnlyList<ProjectPerson> people, KanbanGroupBy groupBy)
    {
        var lanes = new List<KanbanLane>();
        if (groupBy == KanbanGroupBy.Assignee)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var person in people)
            {
                if (keys.Add(person.Identity))
                {
                    lanes.Add(new KanbanLane(person, false));
                }
            }
            var columnKeys = new HashSet<string>(columns.Select(c => c.Identity), StringComparer.Ordinal);
            if (cards.Any(c => columnKeys.Contains(c.ColumnKey) && (c.AssigneeKey is null || !keys.Contains(c.AssigneeKey))))
            {
                lanes.Add(new KanbanLane(null, true));
            }
        }
        else
        {
            lanes.Add(new KanbanLane(null, false));
        }
        var grid = new KanbanGrid(columns, lanes, groupBy);
        foreach (var card in cards)
        {
            grid.Add(card);
        }
        return grid;
    }

    private void Add(KanbanCard card)
    {
        var column = ColumnIndex(card.ColumnKey);
        if (column < 0)
        {
            return;
        }
        var lane = LaneIndexFor(card.AssigneeKey);
        if (lane < 0)
        {
            return;
        }
        var cell = _cells[lane * Columns.Count + column];
        _where[card] = (lane, column, cell.Count);
        cell.Add(card);
        Placed.Add(card);
    }

    /// <summary>The index of the column with <paramref name="key"/>, or −1.</summary>
    public int ColumnIndex(string? key) => key is not null && _columnIndex.TryGetValue(key, out var i) ? i : -1;

    /// <summary>The lane a card with owner <paramref name="assigneeKey"/> belongs to (−1 when there is none).</summary>
    public int LaneIndexFor(string? assigneeKey)
    {
        if (GroupBy == KanbanGroupBy.Status)
        {
            return 0;
        }
        if (assigneeKey is not null && _laneIndex.TryGetValue(assigneeKey, out var lane))
        {
            return lane;
        }
        return Lanes.Count > 0 && Lanes[^1].IsUnassigned ? Lanes.Count - 1 : -1;
    }

    /// <summary>The cards of one cell, in board order.</summary>
    public IReadOnlyList<KanbanCard> Cell(int lane, int column) => _cells[lane * Columns.Count + column];

    /// <summary>Where <paramref name="card"/> sits, if it is on the board.</summary>
    public bool TryLocate(KanbanCard card, out int lane, out int column, out int index)
    {
        if (_where.TryGetValue(card, out var at))
        {
            (lane, column, index) = at;
            return true;
        }
        lane = column = index = -1;
        return false;
    }

    /// <summary>The cards in column <paramref name="column"/> across every lane.</summary>
    public int ColumnCount(int column)
    {
        var count = 0;
        for (var lane = 0; lane < Lanes.Count; lane++)
        {
            count += _cells[lane * Columns.Count + column].Count;
        }
        return count;
    }

    /// <summary>The cards in lane <paramref name="lane"/> across every column.</summary>
    public int LaneCount(int lane)
    {
        var count = 0;
        for (var column = 0; column < Columns.Count; column++)
        {
            count += _cells[lane * Columns.Count + column].Count;
        }
        return count;
    }

    /// <summary>The slot <paramref name="card"/> is in now.</summary>
    public KanbanSlot? SlotOf(KanbanCard card)
    {
        if (!TryLocate(card, out var lane, out var column, out var index))
        {
            return null;
        }
        // In Status view a move never changes the owner: the slot keeps the card's own.
        var assignee = GroupBy == KanbanGroupBy.Status ? card.AssigneeKey : Lanes[lane].AssigneeKey;
        return new KanbanSlot(Columns[column].Identity, assignee, index);
    }
}
