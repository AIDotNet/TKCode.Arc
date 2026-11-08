namespace TKCode.Arc;

/// <summary>
/// The readings in ascending time, in parallel arrays: appending is amortized O(1) and allocates only when it has to grow,
/// and readings are found by binary search. Dropping old readings moves a start offset, so a reading keeps a stable
/// sequence number (its index plus <see cref="Dropped"/>).
/// </summary>
internal sealed class RealtimeStreamBuffer
{
    private long[] _ticks = new long[256];
    private double[] _values = new double[256];
    private string?[] _tags = new string?[256];
    private int _start;
    private int _end;

    public int Count => _end - _start;

    /// <summary>How many readings have been dropped from the front since the last <see cref="Clear"/>.</summary>
    public long Dropped { get; private set; }

    public long TicksAt(int index) => _ticks[_start + index];

    public double ValueAt(int index) => _values[_start + index];

    public string? TagAt(int index) => _tags[_start + index];

    public long FirstTicks => _ticks[_start];

    public long LastTicks => _ticks[_end - 1];

    /// <summary>Adds a reading. Returns false for a value that is not finite or a time older than the newest reading.</summary>
    public bool Add(long ticks, double value, string? tag)
    {
        if (!double.IsFinite(value) || (Count > 0 && ticks < LastTicks))
        {
            return false;
        }
        if (_end == _ticks.Length)
        {
            MakeRoom();
        }
        _ticks[_end] = ticks;
        _values[_end] = value;
        _tags[_end] = tag;
        _end++;
        return true;
    }

    /// <summary>Index of the first reading at or after <paramref name="ticks"/> (<see cref="Count"/> when there is none).</summary>
    public int LowerBound(long ticks)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (_ticks[_start + middle] < ticks)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    /// <summary>Index of the first reading after <paramref name="ticks"/> (<see cref="Count"/> when there is none).</summary>
    public int UpperBound(long ticks)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (_ticks[_start + middle] <= ticks)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    /// <summary>Drops the first <paramref name="count"/> readings.</summary>
    public void DropFirst(int count)
    {
        count = Math.Clamp(count, 0, Count);
        Array.Clear(_tags, _start, count);
        _start += count;
        Dropped += count;
    }

    public void Clear()
    {
        Array.Clear(_tags, _start, Count);
        _start = 0;
        _end = 0;
        Dropped = 0;
    }

    private void MakeRoom()
    {
        var count = Count;
        // Less than half in use: moving is enough. Otherwise grow, so a steady feed settles on one capacity.
        if (count > _ticks.Length / 2)
        {
            var capacity = _ticks.Length * 2;
            var ticks = new long[capacity];
            var values = new double[capacity];
            var tags = new string?[capacity];
            Array.Copy(_ticks, _start, ticks, 0, count);
            Array.Copy(_values, _start, values, 0, count);
            Array.Copy(_tags, _start, tags, 0, count);
            _ticks = ticks;
            _values = values;
            _tags = tags;
        }
        else
        {
            Array.Copy(_ticks, _start, _ticks, 0, count);
            Array.Copy(_values, _start, _values, 0, count);
            Array.Copy(_tags, _start, _tags, 0, count);
            Array.Clear(_tags, count, _tags.Length - count);
        }
        _start = 0;
        _end = count;
    }
}
