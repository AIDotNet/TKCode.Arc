// Ported from vscode (src/vs/base/common/arraysFind.ts, commit b57d5c9e), MIT license.
// Only the helpers that the ported code calls (findFirstMax, findLastMax, findFirstMin); more come when their callers
// do. A Comparator<T> is a Func<T, T, int>; "undefined" for an empty list is default(T).

namespace TKCode.Arc.Editor;

internal static class ArraysFind
{
    /// <summary>Returns the first item that is equal to or greater than every other item.</summary>
    public static T? FindFirstMax<T>(IReadOnlyList<T> array, Func<T, T, int> comparator)
    {
        if (array.Count == 0)
        {
            return default;
        }

        var max = array[0];
        for (var i = 1; i < array.Count; i++)
        {
            var item = array[i];
            if (comparator(item, max) > 0)
            {
                max = item;
            }
        }
        return max;
    }

    /// <summary>Returns the last item that is equal to or greater than every other item.</summary>
    public static T? FindLastMax<T>(IReadOnlyList<T> array, Func<T, T, int> comparator)
    {
        if (array.Count == 0)
        {
            return default;
        }

        var max = array[0];
        for (var i = 1; i < array.Count; i++)
        {
            var item = array[i];
            if (comparator(item, max) >= 0)
            {
                max = item;
            }
        }
        return max;
    }

    /// <summary>Returns the first item that is equal to or less than every other item.</summary>
    public static T? FindFirstMin<T>(IReadOnlyList<T> array, Func<T, T, int> comparator) =>
        FindFirstMax(array, (a, b) => -comparator(a, b));
}
