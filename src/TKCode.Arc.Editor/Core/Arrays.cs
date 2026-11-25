// Ported from vscode (src/vs/base/common/arrays.ts, commit b57d5c9e), MIT license.
// Only the helpers that the ported code calls (arrayInsert, binarySearch, binarySearch2, compareBy); more come when their
// callers do. A Comparator<T> is a Func<T, T, int>.

namespace TKCode.Arc.Editor;

internal static class Arrays
{
    /// <summary>Returns a new list with <paramref name="insertArr"/> inserted into <paramref name="target"/> at <paramref name="insertIndex"/>.</summary>
    public static List<T> ArrayInsert<T>(IReadOnlyList<T> target, int insertIndex, IReadOnlyList<T> insertArr)
    {
        // Array.prototype.slice clamps its bounds.
        insertIndex = Math.Max(0, Math.Min(insertIndex, target.Count));
        var result = new List<T>(target.Count + insertArr.Count);
        for (var i = 0; i < insertIndex; i++)
        {
            result.Add(target[i]);
        }

        for (var i = 0; i < insertArr.Count; i++)
        {
            result.Add(insertArr[i]);
        }

        for (var i = insertIndex; i < target.Count; i++)
        {
            result.Add(target[i]);
        }

        return result;
    }

    /// <summary>
    /// Searches <paramref name="array"/>, sorted by <paramref name="comparator"/>, for <paramref name="key"/>.
    /// Returns the index of an equal element, or <c>-(n + 1)</c> (that is <c>~n</c>) where n is the index at which the
    /// key would be inserted to keep the order.
    /// </summary>
    public static int BinarySearch<T>(IReadOnlyList<T> array, T key, Func<T, T, int> comparator) =>
        BinarySearch2(array.Count, i => comparator(array[i], key));

    /// <summary>
    /// A binary search over a sorted collection of <paramref name="length"/> elements that need not be an array.
    /// <paramref name="compareToKey"/> takes an index and returns zero when the element there equals the key, a negative
    /// number when the element comes before the key and a positive number when it comes after. Returns the index of an
    /// equal element, or <c>-(n + 1)</c> where n is the index at which the key would be inserted to keep the order.
    /// </summary>
    public static int BinarySearch2(int length, Func<int, int> compareToKey)
    {
        int low = 0,
            high = length - 1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var comp = compareToKey(mid);
            if (comp < 0)
            {
                low = mid + 1;
            }
            else if (comp > 0)
            {
                high = mid - 1;
            }
            else
            {
                return mid;
            }
        }
        return -(low + 1);
    }

    /// <summary>A comparator that compares items by what <paramref name="selector"/> returns for them.</summary>
    public static Func<TItem, TItem, int> CompareBy<TItem, TCompareBy>(Func<TItem, TCompareBy> selector, Func<TCompareBy, TCompareBy, int> comparator) =>
        (a, b) => comparator(selector(a), selector(b));
}
