using Avalonia.Controls;

namespace TKCode.Arc;

public partial class SearchResults
{
    private bool _syncingSort;

    private IReadOnlyList<SegmentedOption> BuildSortOptions() =>
    [
        new SegmentedOption(SearchSortOrder.BestMatch, BestMatchText),
        new SegmentedOption(SearchSortOrder.Newest, NewestText),
        new SegmentedOption(SearchSortOrder.Title, TitleSortText)
    ];

    private void SyncSort()
    {
        if (_sort is null)
        {
            return;
        }
        _syncingSort = true;
        try
        {
            _sort.SelectedIndex = (int)SortOrder;
        }
        finally
        {
            _syncingSort = false;
        }
    }

    private void OnSortSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSort || _sort is null)
        {
            return;
        }
        var index = _sort.SelectedIndex;
        if (index is >= 0 and <= (int)SearchSortOrder.Title)
        {
            SetCurrentValue(SortOrderProperty, (SearchSortOrder)index);
        }
    }
}
