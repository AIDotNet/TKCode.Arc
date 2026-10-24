using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    private const int MaxBlocks = 40;

    private sealed class Block
    {
        public required object[] Items { get; init; }

        public required object[] Keys { get; init; }

        public required object?[]?[] Values { get; init; }

        public int Version { get; init; }
    }

    private readonly Dictionary<int, Block> _blocks = new();
    private readonly Dictionary<int, CancellationTokenSource> _pendingBlocks = new();
    private readonly Dictionary<object, object> _checkedItems = new();
    private int _dataSourceCount = -1;
    private int _queryVersion;
    private int _blockVersion;
    private IReadOnlyDictionary<string, decimal?>? _dataSourceTotals;
    private int _dataSourceTotalsVersion;

    private int BlockSize => Math.Max(1, DataSource?.BlockSize ?? 100);

    private void OnDataSourceChanged()
    {
        ResetBlocks();
        _checkedItems.Clear();
        if (DataSource is null)
        {
            _store.SetSource(ItemsSource, RowKeySelector);
        }
        else
        {
            _store.SetSource(null, RowKeySelector);
        }
        _undo.Clear();
        UpdateUndoState();
        InvalidateView();
        UpdatePseudoClasses();
        UpdateToolbarState();
    }

    /// <summary>Drops every block and request; the next view asks for rows again (sort, filters or search changed).</summary>
    internal void ResetBlocks()
    {
        CancelBlockRequests();
        _blocks.Clear();
        _queryVersion++;
        _dataSourceCount = -1;
        _dataSourceTotals = null;
        _dataSourceTotalsVersion++;
        InvalidateView();
    }

    private void CancelBlockRequests()
    {
        foreach (var pending in _pendingBlocks.Values)
        {
            pending.Cancel();
        }
        _pendingBlocks.Clear();
    }

    private DataGridView BuildDataSourceView() => DataGridView.Empty;

    /// <summary>Asks the data source for the blocks covering display rows <paramref name="first"/>..<paramref name="last"/>.</summary>
    internal void RequestRows(int first, int last)
    {
        if (DataSource is not { } source)
        {
            return;
        }
        var size = BlockSize;
        var blocks = new List<int>();
        if (_dataSourceCount < 0)
        {
            blocks.Add(0);
        }
        else
        {
            for (var b = Math.Max(0, first) / size; b <= Math.Max(first, last) / size && b * size < _dataSourceCount; b++)
            {
                blocks.Add(b);
            }
        }
        foreach (var block in blocks)
        {
            if (_blocks.ContainsKey(block) || _pendingBlocks.ContainsKey(block))
            {
                continue;
            }
            _ = LoadBlockAsync(source, block, size);
        }
    }

    private async Task LoadBlockAsync(IDataGridDataSource source, int block, int size)
    {
        var query = _queryVersion;
        var request = new DataGridRowsRequest(block * size, (block + 1) * size, SortDescriptions.ToArray(),
            new Dictionary<string, DataGridFilter>(Filters), (SearchText ?? string.Empty).Trim());
        // Every way out of this method leaves the source out of _pendingBlocks (done, or dropped by CancelBlockRequests),
        // so nothing can cancel it after it is disposed.
        using var cancellation = new CancellationTokenSource();
        _pendingBlocks[block] = cancellation;
        DataGridRowsResult result;
        try
        {
            result = await source.GetRowsAsync(request, cancellation.Token);
        }
        catch (Exception)
        {
            if (query == _queryVersion && !cancellation.IsCancellationRequested)
            {
                _pendingBlocks.Remove(block);
                if (_dataSourceCount < 0)
                {
                    _dataSourceCount = 0;
                }
                InvalidateView();
                Say(DataGridText.Get(DataGridLangResourceKind.RowsLoadFailed));
            }
            return;
        }
        if (query != _queryVersion || cancellation.IsCancellationRequested || !ReferenceEquals(source, DataSource))
        {
            return;
        }
        _pendingBlocks.Remove(block);
        var items = result.Rows.Where(r => r is not null).ToArray();
        var keySelector = RowKeySelector;
        _blocks[block] = new Block
        {
            Items = items,
            Keys = items.Select(i => keySelector?.Invoke(i) ?? (i is DataGridRow row ? row.Key : i)).ToArray(),
            Values = new object?[]?[_store.Accessors.Length],
            Version = ++_blockVersion
        };
        while (_blocks.Count > MaxBlocks)
        {
            var farthest = _blocks.Keys.MaxBy(k => Math.Abs(k - block));
            if (farthest == block)
            {
                break;
            }
            _blocks.Remove(farthest);
        }
        var countChanged = _dataSourceCount != Math.Max(0, result.RowCount);
        _dataSourceCount = Math.Max(0, result.RowCount);
        if (result.Totals is not null)
        {
            _dataSourceTotals = result.Totals;
            _dataSourceTotalsVersion++;
        }
        RememberCheckedItems(block);
        if (countChanged)
        {
            InvalidateView();
        }
        Viewport.InvalidateLayers();
        Viewport.UpdateTotals();
        UpdateStats();
        Viewport.UpdateEmptyState();
        UpdatePseudoClasses();
    }

    private Block? BlockOf(int row, out int offset)
    {
        var size = BlockSize;
        offset = row % size;
        return row >= 0 && _blocks.TryGetValue(row / size, out var block) && offset < block.Items.Length ? block : null;
    }

    private object? DataSourceItem(int row) => BlockOf(row, out var offset) is { } block ? block.Items[offset] : null;

    private object? DataSourceKey(int row) => BlockOf(row, out var offset) is { } block ? block.Keys[offset] : null;

    private int DataSourceVersion(int row) => BlockOf(row, out _) is { } block ? block.Version : 0;

    private object? DataSourceValue(int row, int accessor)
    {
        if (BlockOf(row, out var offset) is not { } block || accessor < 0 || accessor >= block.Values.Length)
        {
            return null;
        }
        var values = block.Values[accessor];
        if (values is null)
        {
            var access = _store.Accessors[accessor];
            values = new object?[block.Items.Length];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = access.Read(block.Items[i]);
            }
            access.Release();
            block.Values[accessor] = values;
        }
        return values[offset];
    }

    private void RememberCheckedItems(int? onlyBlock = null)
    {
        foreach (var (index, block) in _blocks)
        {
            if (onlyBlock is { } only && index != only)
            {
                continue;
            }
            for (var i = 0; i < block.Keys.Length; i++)
            {
                if (_checked.Contains(block.Keys[i]))
                {
                    _checkedItems[block.Keys[i]] = block.Items[i];
                }
            }
        }
        foreach (var key in _checkedItems.Keys.Where(k => !_checked.Contains(k)).ToList())
        {
            _checkedItems.Remove(key);
        }
    }

    private IReadOnlyList<object> CheckedDataSourceItems()
    {
        RememberCheckedItems();
        return CheckedRowKeys.Select(k => _checkedItems.TryGetValue(k, out var item) ? item : null).OfType<object>().ToArray();
    }
}
