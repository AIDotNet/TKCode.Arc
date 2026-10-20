namespace TKCode.Arc;

/// <summary>
/// Tree helpers of <see cref="CommentThread"/> (Arc's <c>mapTree</c>, <c>countTree</c>, <c>authorsOf</c>, <c>pathTo</c>,
/// <c>find</c>). Every change returns a new tree that shares the unchanged comments.
/// </summary>
internal static class CommentThreadTree
{
    /// <summary>Replaces the comment <paramref name="id"/> with <paramref name="map"/>'s result (null removes it).</summary>
    public static IReadOnlyList<ThreadComment> Map(IReadOnlyList<ThreadComment> list, string id, Func<ThreadComment, ThreadComment?> map)
    {
        var next = new List<ThreadComment>(list.Count);
        foreach (var comment in list)
        {
            if (comment.Id == id)
            {
                if (map(comment) is { } mapped)
                {
                    next.Add(mapped);
                }
                continue;
            }
            next.Add(comment.Replies.Count > 0 ? comment with { Replies = Map(comment.Replies, id, map) } : comment);
        }
        return next;
    }

    /// <summary>Comments that are not deleted, replies included.</summary>
    public static int Count(IReadOnlyList<ThreadComment> list)
    {
        var count = 0;
        foreach (var comment in list)
        {
            count += (comment.IsDeleted ? 0 : 1) + Count(comment.Replies);
        }
        return count;
    }

    /// <summary>Authors of comments that are not deleted, in the order they first appear (depth first).</summary>
    public static List<CommentAuthor> Authors(IReadOnlyList<ThreadComment> list, CommentAuthor? first = null)
    {
        var order = new List<string>();
        var map = new Dictionary<string, CommentAuthor>(StringComparer.Ordinal);
        if (first is not null)
        {
            order.Add(first.Id);
            map[first.Id] = first;
        }
        Collect(list, order, map);
        return order.Select(id => map[id]).ToList();
    }

    private static void Collect(IReadOnlyList<ThreadComment> list, List<string> order, Dictionary<string, CommentAuthor> map)
    {
        foreach (var comment in list)
        {
            if (!comment.IsDeleted)
            {
                if (!map.ContainsKey(comment.Author.Id))
                {
                    order.Add(comment.Author.Id);
                }
                // Like a JS Map: the latest author object for an id wins, the first position stays.
                map[comment.Author.Id] = comment.Author;
            }
            Collect(comment.Replies, order, map);
        }
    }

    /// <summary>Ids from the root to <paramref name="id"/>, or null when it is not in the tree.</summary>
    public static List<string>? PathTo(IReadOnlyList<ThreadComment> list, string id)
    {
        foreach (var comment in list)
        {
            if (comment.Id == id)
            {
                return [id];
            }
            if (PathTo(comment.Replies, id) is { } found)
            {
                found.Insert(0, comment.Id);
                return found;
            }
        }
        return null;
    }

    /// <summary>The comment <paramref name="id"/>, or null.</summary>
    public static ThreadComment? Find(IReadOnlyList<ThreadComment> list, string id)
    {
        foreach (var comment in list)
        {
            if (comment.Id == id)
            {
                return comment;
            }
            if (Find(comment.Replies, id) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static int s_serial;

    /// <summary>A fresh comment id (Arc: <c>c-{time base36}-{serial base36}</c>).</summary>
    public static string NewId()
    {
        var serial = Interlocked.Increment(ref s_serial) - 1;
        return $"c-{Base36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())}-{Base36(serial)}";
    }

    private static string Base36(long value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0)
        {
            return "0";
        }
        Span<char> buffer = stackalloc char[16];
        var at = buffer.Length;
        while (value > 0)
        {
            buffer[--at] = digits[(int)(value % 36)];
            value /= 36;
        }
        return new string(buffer[at..]);
    }
}
