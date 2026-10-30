namespace TKCode.Arc;

/// <summary>Waits for the runner and the simulation; tests swap in a manual clock.</summary>
internal interface IEnrichColumnClock
{
    /// <summary>Completes after <paramref name="delay"/>, or is cancelled with <paramref name="token"/>.</summary>
    Task Delay(TimeSpan delay, CancellationToken token);
}

/// <summary>The wall clock.</summary>
internal sealed class EnrichColumnSystemClock : IEnrichColumnClock
{
    public static EnrichColumnSystemClock Instance { get; } = new();

    public Task Delay(TimeSpan delay, CancellationToken token) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, token);
}

/// <summary>Why a run settled.</summary>
internal enum EnrichColumnSettle
{
    Finished,
    Stopped
}

/// <summary>
/// The run engine of <see cref="EnrichColumn"/>: one queue of row ids, at most <see cref="Concurrency"/> workers that start
/// <see cref="Stagger"/> apart (so the column fills top to bottom), one cancellation source per running cell linked to the
/// run's own, and the cells. Workers read the rows, the column and the delegate through the owner's functions each time
/// they take a row, so editing mid-run never acts on stale data. Everything runs on the thread that calls it (the UI
/// thread): the awaits resume there, so no locks are needed.
/// </summary>
internal sealed class EnrichColumnRunner
{
    /// <summary>How far apart the workers start.</summary>
    public static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(140);

    private readonly Dictionary<string, EnrichColumnCell> _cells = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnrichColumnCell> _before = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> _running = new(StringComparer.Ordinal);
    private readonly Queue<string> _queue = new();
    private readonly Func<IReadOnlyList<EnrichColumnRow>> _rows;
    private readonly Func<EnrichColumnDefinition> _column;
    private readonly Func<EnrichColumnRow, EnrichColumnContext, Task<EnrichColumnResult>> _enrich;
    private readonly Func<string> _unknownError;
    private CancellationTokenSource? _run;
    private int _workers;
    private int _generation;

    public EnrichColumnRunner(Func<IReadOnlyList<EnrichColumnRow>> rows, Func<EnrichColumnDefinition> column,
        Func<EnrichColumnRow, EnrichColumnContext, Task<EnrichColumnResult>> enrich, Func<string> unknownError)
    {
        _rows = rows;
        _column = column;
        _enrich = enrich;
        _unknownError = unknownError;
    }

    /// <summary>Raised whenever a cell changes (queued, running, its activity, done, failed, or put back by a stop).</summary>
    public event Action<EnrichColumnRow, EnrichColumnCell>? CellChanged;

    /// <summary>Raised when a run starts with the rows it queued.</summary>
    public event Action<int>? Started;

    /// <summary>Raised when the last cell of a run settles, or a stop ends it.</summary>
    public event Action<EnrichColumnSettle>? Settled;

    /// <summary>How many cells run at once (at least 1).</summary>
    public int Concurrency { get; set; } = 2;

    /// <summary>The clock for the stagger between workers.</summary>
    public IEnrichColumnClock Clock { get; set; } = EnrichColumnSystemClock.Instance;

    /// <summary>True while any cell is queued or running.</summary>
    public bool IsRunning => _queue.Count > 0 || _running.Count > 0 || _workers > 0;

    /// <summary>How many workers are alive.</summary>
    internal int Workers => _workers;

    /// <summary>The cell of <paramref name="id"/> (idle when unknown).</summary>
    public EnrichColumnCell Get(string id) => _cells.TryGetValue(id, out var cell) ? cell : EnrichColumnCell.Idle;

    /// <summary>Every row's cell, keyed by row id, for the rows shown now.</summary>
    public IReadOnlyDictionary<string, EnrichColumnCell> Snapshot()
    {
        var map = new Dictionary<string, EnrichColumnCell>(StringComparer.Ordinal);
        foreach (var row in _rows())
        {
            map[row.Id] = Get(row.Id);
        }
        return map;
    }

    /// <summary>Takes cells the app already has (queued or running ones are treated as not run).</summary>
    public void Seed(IReadOnlyDictionary<string, EnrichColumnCell>? cells)
    {
        if (cells is null)
        {
            return;
        }
        foreach (var (id, cell) in cells)
        {
            _cells[id] = cell.Status is EnrichColumnCellStatus.Queued or EnrichColumnCellStatus.Running
                ? EnrichColumnCell.Idle
                : cell;
        }
    }

    /// <summary>Runs every row: any run in flight is cancelled quietly and the column starts over from the top.</summary>
    public void RunAll()
    {
        CancelAll(restore: false);
        var rows = _rows();
        foreach (var row in rows)
        {
            Enqueue(row);
        }
        if (_queue.Count == 0)
        {
            return;
        }
        Started?.Invoke(_queue.Count);
        StartWorkers();
    }

    /// <summary>Runs one row (a re-run or a retry). It joins a run in flight, or starts a run of its own.</summary>
    public void RunRow(string id)
    {
        var row = Find(id);
        if (row is null)
        {
            return;
        }
        var status = Get(id).Status;
        if (status is EnrichColumnCellStatus.Queued or EnrichColumnCellStatus.Running)
        {
            return;
        }
        var fresh = !IsRunning;
        Enqueue(row);
        if (fresh)
        {
            Started?.Invoke(1);
        }
        StartWorkers();
    }

    /// <summary>Stops everything: requests are cancelled and every queued or running cell goes back to what it was.</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }
        CancelAll(restore: true);
        Settled?.Invoke(EnrichColumnSettle.Stopped);
    }

    /// <summary>Forgets every cell (rows replaced); runs in flight are cancelled without a settle.</summary>
    public void Reset()
    {
        CancelAll(restore: false);
        _cells.Clear();
        _before.Clear();
    }

    private void CancelAll(bool restore)
    {
        _generation++;
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
        _workers = 0;
        foreach (var cts in _running.Values)
        {
            cts.Cancel();
        }
        _running.Clear();
        _queue.Clear();
        if (!restore)
        {
            _before.Clear();
            return;
        }
        foreach (var row in _rows())
        {
            var cell = Get(row.Id);
            if (cell.Status is EnrichColumnCellStatus.Queued or EnrichColumnCellStatus.Running)
            {
                Set(row, _before.TryGetValue(row.Id, out var before) ? before : EnrichColumnCell.Idle);
            }
        }
        _before.Clear();
    }

    private void Enqueue(EnrichColumnRow row)
    {
        var current = Get(row.Id);
        _before[row.Id] = current;
        _queue.Enqueue(row.Id);
        Set(row, new EnrichColumnCell
        {
            Status = EnrichColumnCellStatus.Queued,
            Attempt = current.Attempt,
        });
    }

    private void StartWorkers()
    {
        _run ??= new CancellationTokenSource();
        var limit = Math.Max(1, Concurrency);
        var wanted = Math.Min(limit, _queue.Count + _workers);
        var started = 0;
        while (_workers < wanted)
        {
            _workers++;
            _ = Work(Stagger * started, _run.Token, _generation);
            started++;
        }
    }

    private async Task Work(TimeSpan delay, CancellationToken token, int generation)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Clock.Delay(delay, token);
            }
            while (!token.IsCancellationRequested && _queue.Count > 0)
            {
                var id = _queue.Dequeue();
                // The latest rows: a row removed since it was queued is skipped.
                var row = Find(id);
                if (row is null)
                {
                    _before.Remove(id);
                    continue;
                }
                await RunOne(row, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        if (generation != _generation)
        {
            return;
        }
        _workers--;
        if (_workers == 0 && _running.Count == 0 && _queue.Count == 0)
        {
            _run?.Dispose();
            _run = null;
            _before.Clear();
            Settled?.Invoke(EnrichColumnSettle.Finished);
        }
    }

    private async Task RunOne(EnrichColumnRow row, CancellationToken token)
    {
        var attempt = Get(row.Id).Attempt + 1;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _running[row.Id] = cts;
        Set(row, new EnrichColumnCell { Status = EnrichColumnCellStatus.Running, Attempt = attempt });
        // The latest column, read when the cell starts.
        var context = new EnrichColumnContext(row, _column(), attempt, cts.Token, text =>
        {
            if (!cts.IsCancellationRequested && Get(row.Id) is { Status: EnrichColumnCellStatus.Running } running &&
                running.Activity != text)
            {
                Set(row, new EnrichColumnCell { Status = EnrichColumnCellStatus.Running, Attempt = attempt, Activity = text });
            }
        });
        EnrichColumnCell? settled = null;
        try
        {
            var result = await _enrich(row, context);
            if (!cts.IsCancellationRequested)
            {
                settled = EnrichColumnCell.Done(result?.Value ?? string.Empty, result?.Source, attempt);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                var message = ex is AggregateException { InnerException: { } inner } ? inner.Message : ex.Message;
                settled = EnrichColumnCell.Failed(string.IsNullOrWhiteSpace(message) ? _unknownError() : message, attempt);
            }
        }
        if (_running.TryGetValue(row.Id, out var mine) && ReferenceEquals(mine, cts))
        {
            _running.Remove(row.Id);
        }
        if (settled is not null)
        {
            _before.Remove(row.Id);
            Set(row, settled);
        }
    }

    private EnrichColumnRow? Find(string id)
    {
        foreach (var row in _rows())
        {
            if (string.Equals(row.Id, id, StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }

    private void Set(EnrichColumnRow row, EnrichColumnCell cell)
    {
        _cells[row.Id] = cell;
        CellChanged?.Invoke(row, cell);
    }
}
