namespace TKCode.Arc;

/// <summary>
/// The former name of <see cref="Table"/> (Arc calls it the sortable data table). It behaves exactly like
/// <see cref="Table"/> and uses its theme; it will be removed in a later preview.
/// </summary>
[Obsolete("SortableDataTable is now Table. Replace SortableDataTable with Table; nothing else changes.")]
public class SortableDataTable : Table
{
    protected override Type StyleKeyOverride => typeof(Table);
}
