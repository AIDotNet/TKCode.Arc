using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a <see cref="KanbanBoard"/> arranges its cards.</summary>
public enum KanbanGroupBy
{
    /// <summary>One row of status columns.</summary>
    Status,

    /// <summary>One swimlane per person, each with the same status columns.</summary>
    Assignee
}

/// <summary>The color of a card label's dot.</summary>
public enum KanbanLabelTone
{
    /// <summary>The accent color.</summary>
    Accent,

    /// <summary>The warning color (amber).</summary>
    Warning,

    /// <summary>The success color (green).</summary>
    Success,

    /// <summary>The danger color (red).</summary>
    Danger,

    /// <summary>A violet that reads on both themes.</summary>
    Violet,

    /// <summary>A blue that reads on both themes.</summary>
    Blue,

    /// <summary>A teal that reads on both themes.</summary>
    Teal,

    /// <summary>A rose that reads on both themes.</summary>
    Rose,

    /// <summary>The muted text color.</summary>
    Neutral
}

/// <summary>What caused a card to move or change.</summary>
public enum KanbanMoveSource
{
    /// <summary>A mouse or pen drag.</summary>
    Pointer,

    /// <summary>A touch drag (after the press was held).</summary>
    Touch,

    /// <summary>A keyboard drag (Space, arrows, Space).</summary>
    Keyboard,

    /// <summary>The details drawer (status or owner changed there).</summary>
    Details,

    /// <summary>A call to a public method such as <see cref="KanbanBoard.MoveCard"/>.</summary>
    Programmatic
}

/// <summary>Why <see cref="KanbanBoard.CardsChanged"/> was raised.</summary>
public enum KanbanChangeReason
{
    /// <summary>A card was dropped somewhere (it may be the same place).</summary>
    Moved,

    /// <summary>A card was added with quick add.</summary>
    Created,

    /// <summary>A card's title was edited in the drawer.</summary>
    TitleEdited,

    /// <summary>A card's notes were edited in the drawer.</summary>
    DescriptionEdited,

    /// <summary>A card's status was changed in the drawer.</summary>
    StatusChanged,

    /// <summary>A card's owner was changed in the drawer.</summary>
    AssigneeChanged,

    /// <summary>A checklist step was ticked or unticked.</summary>
    ChecklistToggled,

    /// <summary>A checklist step was added in the drawer.</summary>
    ChecklistItemAdded
}

/// <summary>Why a drag ended without a drop.</summary>
public enum KanbanDragCancelReason
{
    /// <summary>Escape was pressed.</summary>
    Escape,

    /// <summary>The pointer was lost (capture taken away, touch cancelled).</summary>
    PointerCancelled,

    /// <summary>Keyboard focus left a card lifted with the keyboard.</summary>
    FocusLost,

    /// <summary><see cref="KanbanBoard.CancelDrag"/> was called, or the data changed under the drag.</summary>
    Programmatic,

    /// <summary>A <see cref="KanbanBoard.CardMoving"/> handler refused the drop.</summary>
    Refused
}

/// <summary>A place on the board: a column, a swimlane (when grouped by person) and a position inside that cell.</summary>
/// <param name="ColumnKey">The column's <see cref="KanbanColumn.Identity"/>.</param>
/// <param name="AssigneeKey">The person's <see cref="ProjectPerson.Identity"/>, or null for no owner.</param>
/// <param name="Index">The position among the cell's other cards (0 = top).</param>
public readonly record struct KanbanSlot(string ColumnKey, string? AssigneeKey, int Index);

/// <summary>A status column of a <see cref="KanbanBoard"/>, with an optional work-in-progress limit.</summary>
public sealed class KanbanColumn : AvaloniaObject
{
    /// <summary>A stable id that cards refer to in <see cref="KanbanCard.ColumnKey"/>; empty uses <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<KanbanColumn, string>(nameof(Key), string.Empty);

    /// <summary>The column's heading.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<KanbanColumn, string>(nameof(Name), string.Empty);

    /// <summary>The most cards the column should hold; more shows a warning (null: no limit). Equal to the limit is fine.</summary>
    public static readonly StyledProperty<int?> LimitProperty =
        AvaloniaProperty.Register<KanbanColumn, int?>(nameof(Limit));

    /// <summary>How many cards the column holds (set by the board, across every swimlane).</summary>
    public static readonly DirectProperty<KanbanColumn, int> CountProperty =
        AvaloniaProperty.RegisterDirect<KanbanColumn, int>(nameof(Count), o => o.Count);

    /// <summary>True while the column holds more cards than its <see cref="Limit"/>.</summary>
    public static readonly DirectProperty<KanbanColumn, bool> IsOverLimitProperty =
        AvaloniaProperty.RegisterDirect<KanbanColumn, bool>(nameof(IsOverLimit), o => o.IsOverLimit);

    private int _count;
    private bool _isOverLimit;

    /// <summary>Creates an empty column.</summary>
    public KanbanColumn()
    {
    }

    /// <summary>Creates a column with a key, a name and an optional limit.</summary>
    public KanbanColumn(string key, string name, int? limit = null)
    {
        Key = key;
        Name = name;
        Limit = limit;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="LimitProperty"/>
    public int? Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => _count; private set => SetAndRaise(CountProperty, ref _count, value); }

    /// <inheritdoc cref="IsOverLimitProperty"/>
    public bool IsOverLimit { get => _isOverLimit; private set => SetAndRaise(IsOverLimitProperty, ref _isOverLimit, value); }

    /// <summary>The key, or the name when no key is set.</summary>
    public string Identity => string.IsNullOrEmpty(Key) ? Name : Key;

    /// <summary>Records the column's committed card count; returns true when the over-limit state flipped.</summary>
    internal bool SetCount(int count)
    {
        var was = IsOverLimit;
        Count = count;
        IsOverLimit = KanbanLayout.IsOverLimit(count, Limit);
        return was != IsOverLimit;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LimitProperty)
        {
            IsOverLimit = KanbanLayout.IsOverLimit(Count, Limit);
        }
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A label that cards can carry (a colored dot and a name).</summary>
public sealed class KanbanLabel : AvaloniaObject
{
    /// <summary>A stable id that cards refer to in <see cref="KanbanCard.LabelKey"/>; empty uses <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<KanbanLabel, string>(nameof(Key), string.Empty);

    /// <summary>The label's text.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<KanbanLabel, string>(nameof(Name), string.Empty);

    /// <summary>The dot's color family.</summary>
    public static readonly StyledProperty<KanbanLabelTone> ToneProperty =
        AvaloniaProperty.Register<KanbanLabel, KanbanLabelTone>(nameof(Tone));

    /// <summary>An exact dot color; wins over <see cref="Tone"/> when set.</summary>
    public static readonly StyledProperty<Color?> ColorProperty =
        AvaloniaProperty.Register<KanbanLabel, Color?>(nameof(Color));

    /// <summary>Creates an empty label.</summary>
    public KanbanLabel()
    {
    }

    /// <summary>Creates a label with a key, a name and a tone.</summary>
    public KanbanLabel(string key, string name, KanbanLabelTone tone = KanbanLabelTone.Accent)
    {
        Key = key;
        Name = name;
        Tone = tone;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public KanbanLabelTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="ColorProperty"/>
    public Color? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>The key, or the name when no key is set.</summary>
    public string Identity => string.IsNullOrEmpty(Key) ? Name : Key;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One step of a card's checklist.</summary>
public sealed class KanbanChecklistItem : AvaloniaObject
{
    /// <summary>The step's text.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<KanbanChecklistItem, string>(nameof(Text), string.Empty);

    /// <summary>True once the step is done.</summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<KanbanChecklistItem, bool>(nameof(IsDone));

    /// <summary>Creates an empty step.</summary>
    public KanbanChecklistItem()
    {
    }

    /// <summary>Creates a step with its text and state.</summary>
    public KanbanChecklistItem(string text, bool isDone = false)
    {
        Text = text;
        IsDone = isDone;
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsDoneProperty"/>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>
/// A card on a <see cref="KanbanBoard"/>. Cards refer to their column, owner and label by key, so they can come straight
/// from a tracker's JSON; the order of the board's card list is the order inside every column.
/// </summary>
public sealed class KanbanCard : AvaloniaObject
{
    /// <summary>The card's id, shown in its top left corner (for example "FLD-214").</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<KanbanCard, string>(nameof(Key), string.Empty);

    /// <summary>The card's title (it wraps; it is never cut short).</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<KanbanCard, string>(nameof(Title), string.Empty);

    /// <summary>The <see cref="KanbanColumn.Identity"/> of the card's column.</summary>
    public static readonly StyledProperty<string> ColumnKeyProperty =
        AvaloniaProperty.Register<KanbanCard, string>(nameof(ColumnKey), string.Empty);

    /// <summary>The <see cref="ProjectPerson.Identity"/> of the card's owner; null for none.</summary>
    public static readonly StyledProperty<string?> AssigneeKeyProperty =
        AvaloniaProperty.Register<KanbanCard, string?>(nameof(AssigneeKey));

    /// <summary>The <see cref="KanbanLabel.Identity"/> of the card's label; null for none.</summary>
    public static readonly StyledProperty<string?> LabelKeyProperty =
        AvaloniaProperty.Register<KanbanCard, string?>(nameof(LabelKey));

    /// <summary>The due date; null shows none.</summary>
    public static readonly StyledProperty<DateOnly?> DueProperty =
        AvaloniaProperty.Register<KanbanCard, DateOnly?>(nameof(Due));

    /// <summary>The estimate in points; null shows none.</summary>
    public static readonly StyledProperty<int?> PointsProperty =
        AvaloniaProperty.Register<KanbanCard, int?>(nameof(Points));

    /// <summary>Free notes, edited in the details drawer.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<KanbanCard, string?>(nameof(Description));

    /// <summary>The card's checklist steps.</summary>
    public static readonly StyledProperty<AvaloniaList<KanbanChecklistItem>> ChecklistProperty =
        AvaloniaProperty.Register<KanbanCard, AvaloniaList<KanbanChecklistItem>>(nameof(Checklist));

    /// <summary>Any object the app wants to keep with the card.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<KanbanCard, object?>(nameof(Tag));

    /// <summary>How many checklist steps are done.</summary>
    public static readonly DirectProperty<KanbanCard, int> ChecklistDoneProperty =
        AvaloniaProperty.RegisterDirect<KanbanCard, int>(nameof(ChecklistDone), o => o.ChecklistDone);

    /// <summary>How many checklist steps there are.</summary>
    public static readonly DirectProperty<KanbanCard, int> ChecklistTotalProperty =
        AvaloniaProperty.RegisterDirect<KanbanCard, int>(nameof(ChecklistTotal), o => o.ChecklistTotal);

    /// <summary>True when the card has steps and every one is done.</summary>
    public static readonly DirectProperty<KanbanCard, bool> IsChecklistCompleteProperty =
        AvaloniaProperty.RegisterDirect<KanbanCard, bool>(nameof(IsChecklistComplete), o => o.IsChecklistComplete);

    private readonly HashSet<KanbanChecklistItem> _watched = new();
    private AvaloniaList<KanbanChecklistItem>? _observed;
    private int _done;
    private int _total;
    private bool _complete;

    /// <summary>Creates an empty card.</summary>
    public KanbanCard()
    {
        SetCurrentValue(ChecklistProperty, new AvaloniaList<KanbanChecklistItem>());
    }

    /// <summary>Creates a card with an id, a title and a column.</summary>
    public KanbanCard(string key, string title, string columnKey) : this()
    {
        Key = key;
        Title = title;
        ColumnKey = columnKey;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ColumnKeyProperty"/>
    public string ColumnKey { get => GetValue(ColumnKeyProperty); set => SetValue(ColumnKeyProperty, value); }

    /// <inheritdoc cref="AssigneeKeyProperty"/>
    public string? AssigneeKey { get => GetValue(AssigneeKeyProperty); set => SetValue(AssigneeKeyProperty, value); }

    /// <inheritdoc cref="LabelKeyProperty"/>
    public string? LabelKey { get => GetValue(LabelKeyProperty); set => SetValue(LabelKeyProperty, value); }

    /// <inheritdoc cref="DueProperty"/>
    public DateOnly? Due { get => GetValue(DueProperty); set => SetValue(DueProperty, value); }

    /// <inheritdoc cref="PointsProperty"/>
    public int? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ChecklistProperty"/>
    public AvaloniaList<KanbanChecklistItem> Checklist { get => GetValue(ChecklistProperty); set => SetValue(ChecklistProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc cref="ChecklistDoneProperty"/>
    public int ChecklistDone { get => _done; private set => SetAndRaise(ChecklistDoneProperty, ref _done, value); }

    /// <inheritdoc cref="ChecklistTotalProperty"/>
    public int ChecklistTotal { get => _total; private set => SetAndRaise(ChecklistTotalProperty, ref _total, value); }

    /// <inheritdoc cref="IsChecklistCompleteProperty"/>
    public bool IsChecklistComplete { get => _complete; private set => SetAndRaise(IsChecklistCompleteProperty, ref _complete, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChecklistProperty)
        {
            if (_observed is not null)
            {
                _observed.CollectionChanged -= OnChecklistChanged;
            }
            _observed = change.GetNewValue<AvaloniaList<KanbanChecklistItem>?>();
            if (_observed is not null)
            {
                _observed.CollectionChanged += OnChecklistChanged;
            }
            WatchItems();
        }
    }

    private void OnChecklistChanged(object? sender, NotifyCollectionChangedEventArgs e) => WatchItems();

    private void WatchItems()
    {
        var items = _observed ?? (IReadOnlyList<KanbanChecklistItem>)Array.Empty<KanbanChecklistItem>();
        foreach (var item in _watched.Where(w => !items.Contains(w)).ToList())
        {
            item.PropertyChanged -= OnItemChanged;
            _watched.Remove(item);
        }
        foreach (var item in items)
        {
            if (_watched.Add(item))
            {
                item.PropertyChanged += OnItemChanged;
            }
        }
        Recount();
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KanbanChecklistItem.IsDoneProperty)
        {
            Recount();
        }
    }

    private void Recount()
    {
        var total = 0;
        var done = 0;
        if (_observed is not null)
        {
            foreach (var item in _observed)
            {
                total++;
                if (item.IsDone)
                {
                    done++;
                }
            }
        }
        ChecklistTotal = total;
        ChecklistDone = done;
        IsChecklistComplete = total > 0 && done == total;
    }

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrEmpty(Key) ? Title : Key + " " + Title;
}
