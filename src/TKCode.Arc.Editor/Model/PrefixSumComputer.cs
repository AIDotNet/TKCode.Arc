// Ported from vscode (src/vs/editor/common/model/prefixSumComputer.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - PrefixSumComputer keeps upstream's uint arrays (Uint32Array) and takes and returns int; toUint32 clamps a negative
//    index or value to 0 as upstream's does, and the sums wrap around at 2^32 as a Uint32Array does;
//  - the valid index is a plain field (upstream keeps it in an Int32Array of length 1);
//  - ConstantTimePrefixSumComputer keeps its values, sums and index in lists; an index past the end of the list reads
//    as upstream's `undefined` does.

namespace TKCode.Arc.Editor.Model;

/// <summary>The prefix sums of a list of values, recomputed lazily from the first changed value.</summary>
internal sealed class PrefixSumComputer
{
    /// <summary>values[i] is the value at index i.</summary>
    private uint[] _values;

    /// <summary>prefixSum[i] = SUM(heights[j]), 0 &lt;= j &lt;= i.</summary>
    private uint[] _prefixSum;

    /// <summary>prefixSum[i], 0 &lt;= i &lt;= prefixSumValidIndex can be trusted.</summary>
    private int _prefixSumValidIndex;

    public PrefixSumComputer(uint[] values)
    {
        _values = values;
        _prefixSum = new uint[values.Length];
        _prefixSumValidIndex = -1;
    }

    public int GetCount() => _values.Length;

    public bool InsertValues(int insertIndex, uint[] insertValues)
    {
        insertIndex = ToUint32(insertIndex);
        var oldValues = _values;
        var oldPrefixSum = _prefixSum;
        var insertValuesLen = insertValues.Length;

        if (insertValuesLen == 0)
        {
            return false;
        }

        // A typed array's subarray clamps its bounds.
        insertIndex = Math.Min(insertIndex, oldValues.Length);
        _values = new uint[oldValues.Length + insertValuesLen];
        Array.Copy(oldValues, 0, _values, 0, insertIndex);
        Array.Copy(oldValues, insertIndex, _values, insertIndex + insertValuesLen, oldValues.Length - insertIndex);
        Array.Copy(insertValues, 0, _values, insertIndex, insertValuesLen);

        if (insertIndex - 1 < _prefixSumValidIndex)
        {
            _prefixSumValidIndex = insertIndex - 1;
        }

        _prefixSum = new uint[_values.Length];
        if (_prefixSumValidIndex >= 0)
        {
            Array.Copy(oldPrefixSum, 0, _prefixSum, 0, _prefixSumValidIndex + 1);
        }

        return true;
    }

    public bool SetValue(int index, int value)
    {
        index = ToUint32(index);
        var uintValue = (uint)ToUint32(value);

        if (_values[index] == uintValue)
        {
            return false;
        }

        _values[index] = uintValue;
        if (index - 1 < _prefixSumValidIndex)
        {
            _prefixSumValidIndex = index - 1;
        }

        return true;
    }

    public bool RemoveValues(int startIndex, int count)
    {
        startIndex = ToUint32(startIndex);
        count = ToUint32(count);

        var oldValues = _values;
        var oldPrefixSum = _prefixSum;

        if (startIndex >= oldValues.Length)
        {
            return false;
        }

        var maxCount = oldValues.Length - startIndex;
        if (count >= maxCount)
        {
            count = maxCount;
        }

        if (count == 0)
        {
            return false;
        }

        _values = new uint[oldValues.Length - count];
        Array.Copy(oldValues, 0, _values, 0, startIndex);
        Array.Copy(oldValues, startIndex + count, _values, startIndex, oldValues.Length - startIndex - count);

        _prefixSum = new uint[_values.Length];
        if (startIndex - 1 < _prefixSumValidIndex)
        {
            _prefixSumValidIndex = startIndex - 1;
        }

        if (_prefixSumValidIndex >= 0)
        {
            Array.Copy(oldPrefixSum, 0, _prefixSum, 0, _prefixSumValidIndex + 1);
        }

        return true;
    }

    public long GetTotalSum()
    {
        if (_values.Length == 0)
        {
            return 0;
        }

        return GetPrefixSumCore(_values.Length - 1);
    }

    /// <summary>Returns the sum of the first <c>index + 1</c> many items: <c>SUM(0 &lt;= j &lt;= index, values[j])</c>.</summary>
    public long GetPrefixSum(int index)
    {
        if (index < 0)
        {
            return 0;
        }

        index = ToUint32(index);
        return GetPrefixSumCore(index);
    }

    private uint GetPrefixSumCore(int index)
    {
        if (index <= _prefixSumValidIndex)
        {
            return _prefixSum[index];
        }

        var startIndex = _prefixSumValidIndex + 1;
        if (startIndex == 0)
        {
            _prefixSum[0] = _values[0];
            startIndex++;
        }

        if (index >= _values.Length)
        {
            index = _values.Length - 1;
        }

        for (var i = startIndex; i <= index; i++)
        {
            _prefixSum[i] = unchecked(_prefixSum[i - 1] + _values[i]);
        }

        _prefixSumValidIndex = Math.Max(_prefixSumValidIndex, index);
        return _prefixSum[index];
    }

    public PrefixSumIndexOfResult GetIndexOf(double sum)
    {
        var flooredSum = (long)Math.Floor(sum);

        // Compute all sums (to get a fully valid prefixSum)
        GetTotalSum();

        var low = 0;
        var high = _values.Length - 1;
        var mid = 0;
        long midStop;
        long midStart = 0;

        while (low <= high)
        {
            mid = low + ((high - low) / 2);

            midStop = _prefixSum[mid];
            midStart = midStop - _values[mid];

            if (flooredSum < midStart)
            {
                high = mid - 1;
            }
            else if (flooredSum >= midStop)
            {
                low = mid + 1;
            }
            else
            {
                break;
            }
        }

        return new PrefixSumIndexOfResult(mid, (int)(flooredSum - midStart));
    }

    /// <summary>Upstream's toUint32 for the values used here: negative is 0.</summary>
    private static int ToUint32(int value) => value < 0 ? 0 : value;
}

/// <summary>
/// Prefix sums whose <see cref="GetIndexOf"/> has an amortized runtime complexity of O(1)
/// (<see cref="PrefixSumComputer.GetIndexOf"/> is just O(log n)).
/// </summary>
internal sealed class ConstantTimePrefixSumComputer
{
    private List<int> _values;
    private bool _isValid;
    private int _validEndIndex;

    /// <summary>_prefixSum[i] = SUM(values[j]), 0 &lt;= j &lt;= i.</summary>
    private readonly List<int> _prefixSum;

    /// <summary>_indexBySum[sum] = idx =&gt; _prefixSum[idx - 1] &lt;= sum &lt; _prefixSum[idx].</summary>
    private readonly List<int> _indexBySum;

    public ConstantTimePrefixSumComputer(IEnumerable<int> values)
    {
        _values = new List<int>(values);
        _isValid = false;
        _validEndIndex = -1;
        _prefixSum = new List<int>();
        _indexBySum = new List<int>();
    }

    /// <summary>Returns <c>SUM(0 &lt;= j &lt; values.length, values[j])</c>.</summary>
    public int GetTotalSum()
    {
        EnsureValid();
        return _indexBySum.Count;
    }

    /// <summary>Returns the sum of the first <paramref name="count"/> many items: <c>SUM(0 &lt;= j &lt; count, values[j])</c>.</summary>
    public int GetPrefixSum(int count)
    {
        EnsureValid();
        if (count == 0)
        {
            return 0;
        }

        return _prefixSum[count - 1];
    }

    /// <summary>Returns <c>result</c>, such that <c>getPrefixSum(result.index) + result.remainder = sum</c>.</summary>
    public PrefixSumIndexOfResult GetIndexOf(int sum)
    {
        EnsureValid();
        if (sum < 0 || sum >= _indexBySum.Count)
        {
            // sum does not have a direct entry in _indexBySum (e.g. sum >= getTotalSum() or the array is empty / all values are zero)
            var lastIdx = Math.Max(0, _values.Count - 1);
            var lastPrefixSum = lastIdx > 0 ? _prefixSum[lastIdx - 1] : 0;
            return new PrefixSumIndexOfResult(lastIdx, sum - lastPrefixSum);
        }

        var idx = _indexBySum[sum];
        var viewLinesAbove = idx > 0 ? _prefixSum[idx - 1] : 0;
        return new PrefixSumIndexOfResult(idx, sum - viewLinesAbove);
    }

    public void RemoveValues(int start, int deleteCount)
    {
        // Array.prototype.splice clamps the count.
        deleteCount = Math.Max(0, Math.Min(deleteCount, _values.Count - start));
        _values.RemoveRange(start, deleteCount);
        Invalidate(start);
    }

    public void InsertValues(int insertIndex, IReadOnlyList<int> insertArr)
    {
        _values = Arrays.ArrayInsert(_values, insertIndex, insertArr);
        Invalidate(insertIndex);
    }

    public void SetValue(int index, int value)
    {
        if (_values[index] == value)
        {
            // no change
            return;
        }

        _values[index] = value;
        Invalidate(index);
    }

    private void Invalidate(int index)
    {
        _isValid = false;
        _validEndIndex = Math.Min(_validEndIndex, index - 1);
    }

    private void EnsureValid()
    {
        if (_isValid)
        {
            return;
        }

        for (int i = _validEndIndex + 1, len = _values.Count; i < len; i++)
        {
            var value = _values[i];
            var sumAbove = i > 0 ? _prefixSum[i - 1] : 0;

            SetAt(_prefixSum, i, sumAbove + value);
            for (var j = 0; j < value; j++)
            {
                SetAt(_indexBySum, sumAbove + j, i);
            }
        }

        // trim things
        SetLength(_prefixSum, _values.Count);
        SetLength(_indexBySum, _values.Count > 0 ? _prefixSum[_values.Count - 1] : 0);

        // mark as valid
        _isValid = true;
        _validEndIndex = _values.Count - 1;
    }

    /// <summary>A JavaScript array write: past the end it grows the array.</summary>
    private static void SetAt(List<int> list, int index, int value)
    {
        while (list.Count < index)
        {
            list.Add(0);
        }

        if (index == list.Count)
        {
            list.Add(value);
        }
        else
        {
            list[index] = value;
        }
    }

    /// <summary>A JavaScript <c>array.length = length</c>.</summary>
    private static void SetLength(List<int> list, int length)
    {
        if (list.Count > length)
        {
            list.RemoveRange(length, list.Count - length);
        }

        while (list.Count < length)
        {
            list.Add(0);
        }
    }
}

/// <summary>The index of the value a sum falls in, and how far into that value it is.</summary>
internal readonly record struct PrefixSumIndexOfResult(int Index, int Remainder);
