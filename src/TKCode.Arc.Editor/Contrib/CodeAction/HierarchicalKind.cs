// Ported from vscode (src/vs/base/common/hierarchicalKind.ts, commit b57d5c9e), MIT license.
// Differences from upstream: a sealed record (equality by value); None and Empty are static read-only instances.

namespace TKCode.Arc.Editor.Contrib.CodeAction;

/// <summary>A dot-separated kind such as <c>refactor.extract.function</c> (upstream's HierarchicalKind).</summary>
internal sealed record HierarchicalKind(string Value)
{
    public const string Sep = ".";

    /// <summary>A kind that matches nothing.</summary>
    public static readonly HierarchicalKind None = new("@@none@@");

    /// <summary>The kind that contains every kind.</summary>
    public static readonly HierarchicalKind Empty = new(string.Empty);

    /// <summary>Whether <paramref name="other"/> is this kind or a sub-kind of it.</summary>
    public bool Contains(HierarchicalKind other) =>
        Equals(other) || Value.Length == 0 || other.Value.StartsWith(Value + Sep, StringComparison.Ordinal);

    /// <summary>Whether one kind contains the other.</summary>
    public bool Intersects(HierarchicalKind other) => Contains(other) || other.Contains(this);

    /// <summary>The sub-kind of this kind made of <paramref name="parts"/>.</summary>
    public HierarchicalKind Append(params ReadOnlySpan<string> parts)
    {
        if (parts.IsEmpty)
        {
            return this;
        }
        if (parts.Length == 1)
        {
            return new(Value.Length > 0 ? string.Concat(Value, Sep, parts[0]) : parts[0]);
        }
        if (parts.Length == 2)
        {
            return new(Value.Length > 0
                ? string.Concat(Value, Sep, parts[0], Sep, parts[1])
                : string.Concat(parts[0], Sep, parts[1]));
        }
        var sb = new System.Text.StringBuilder(Value.Length + parts.Length * 12);
        if (Value.Length > 0)
        {
            sb.Append(Value);
        }
        foreach (var part in parts)
        {
            if (sb.Length > 0)
            {
                sb.Append('.');
            }
            sb.Append(part);
        }
        return new(sb.ToString());
    }

    public override string ToString() => Value;
}
