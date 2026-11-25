// Ported from vscode (src/vs/editor/common/model.ts: FindMatch, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>A search result: the range that matched and, when captures were requested, the capture group values.</summary>
internal sealed class FindMatch : IEquatable<FindMatch>
{
    internal FindMatch(TextRange range, string?[]? matches)
    {
        Range = range;
        Matches = matches;
    }

    /// <summary>The range of the match.</summary>
    internal TextRange Range { get; }

    /// <summary>
    /// The full match followed by its capture groups, or null when captures were not requested. A group that did not
    /// participate is null, like JavaScript's <c>undefined</c>.
    /// </summary>
    internal string?[]? Matches { get; }

    /// <inheritdoc />
    public bool Equals(FindMatch? other)
    {
        if (other is null || !Range.Equals(other.Range))
        {
            return false;
        }

        if (Matches is null || other.Matches is null)
        {
            return Matches is null && other.Matches is null;
        }

        if (Matches.Length != other.Matches.Length)
        {
            return false;
        }

        for (var i = 0; i < Matches.Length; i++)
        {
            if (!string.Equals(Matches[i], other.Matches[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FindMatch other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Range, Matches?.Length ?? 0);

    /// <inheritdoc />
    public override string ToString() => Matches is null ? Range.ToString() : $"{Range} {string.Join(", ", Matches)}";
}
