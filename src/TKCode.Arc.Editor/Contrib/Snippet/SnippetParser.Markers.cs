// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetParser.ts: Marker, Text, TransformableMarker,
// Placeholder, Choice, Transform, FormatString, Variable, VariableResolver, walk and TextmateSnippet, commit b57d5c9e),
// MIT license.
// Differences:
//  - VariableResolver is the interface IVariableResolver; a resolver that returns null leaves the variable unresolved
//    (upstream: undefined);
//  - Transform's regexp is a JsRegExp (the JavaScript regular expression on top of .NET, see JsRegex.cs); its resolve
//    replaces through the .NET expression: every match with the g flag, else the first, and a group that did not take
//    part in the match is empty, as upstream's `groups[index] || ''`; a match that times out leaves the value as it is;
//  - placeholder indexes are int: the fractional indexes of a nested snippet exist only inside OneSnippet.Merge, which
//    renumbers them at once (upstream assigns fractions to the index and renormalizes in the same call);
//  - Variable.Resolve sets the parent of the Text it adds (upstream leaves it unset);
//  - the word case transforms (pascalcase, camelcase, kebabcase, snakecase) use .NET's \p{L}, \p{Lu} and \p{Ll}, which
//    see UTF-16 code units (a letter outside the BMP is not a letter here), and upcase / downcase / capitalize use the
//    current culture like upstream's toLocale*Case, the other transforms the invariant one like toUpper/LowerCase.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.Snippet;

/// <summary>A node of a parsed snippet (upstream's Marker).</summary>
internal abstract class Marker
{
    private protected List<Marker> _children = [];

    /// <summary>The node this one is a child of; null for the snippet itself and for nodes not in a tree.</summary>
    public Marker? Parent { get; internal set; }

    /// <summary>The children, in order.</summary>
    public IReadOnlyList<Marker> Children => _children;

    /// <summary>Appends a child; a text after a text merges into it.</summary>
    public virtual Marker AppendChild(Marker child)
    {
        if (child is Text text && _children.Count > 0 && _children[^1] is Text last)
        {
            // this and previous child are text -> merge them
            last.Value += text.Value;
        }
        else
        {
            // normal adoption of child
            child.Parent = this;
            _children.Add(child);
        }

        return this;
    }

    /// <summary>Replaces <paramref name="child"/> (a child of its own parent) with <paramref name="others"/>.</summary>
    public virtual void Replace(Marker child, IReadOnlyList<Marker> others)
    {
        var parent = child.Parent!;
        var idx = parent._children.IndexOf(child);
        var newChildren = new List<Marker>(parent._children);
        newChildren.RemoveAt(idx);
        newChildren.InsertRange(idx, others);
        parent._children = newChildren;

        FixParent(others, parent);

        static void FixParent(IReadOnlyList<Marker> children, Marker parent)
        {
            foreach (var child in children)
            {
                child.Parent = parent;
                FixParent(child.Children, child);
            }
        }
    }

    /// <summary>The last node of the subtree, depth first.</summary>
    public Marker RightMostDescendant => _children.Count > 0 ? _children[^1].RightMostDescendant : this;

    /// <summary>The snippet this node belongs to.</summary>
    public TextmateSnippet? Snippet
    {
        get
        {
            Marker? candidate = this;
            while (true)
            {
                if (candidate is null)
                {
                    return null;
                }

                if (candidate is TextmateSnippet snippet)
                {
                    return snippet;
                }

                candidate = candidate.Parent;
            }
        }
    }

    /// <summary>The text the node inserts.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var child in _children)
        {
            builder.Append(child.ToString());
        }

        return builder.ToString();
    }

    /// <summary>The node as TextMate snippet syntax.</summary>
    public abstract string ToTextmateString();

    /// <summary>The length of the text the node itself inserts (not counting its children).</summary>
    public virtual int Len() => 0;

    /// <summary>A deep copy.</summary>
    public abstract Marker Clone();

    private protected void SetChildren(List<Marker> children) => _children = children;

    private protected List<Marker> CloneChildren()
    {
        var result = new List<Marker>(_children.Count);
        foreach (var child in _children)
        {
            result.Add(child.Clone());
        }

        return result;
    }
}

/// <summary>Literal text (upstream's Text).</summary>
internal sealed class Text : Marker
{
    private static readonly Regex s_escape = new(@"\$|}|\\", RegexOptions.CultureInvariant);

    public Text(string value)
    {
        Value = value;
    }

    /// <summary>The text.</summary>
    public string Value { get; set; }

    /// <summary>Escapes <c>$</c>, <c>}</c> and <c>\</c> with a backslash.</summary>
    public static string Escape(string value) => s_escape.Replace(value, @"\$&");

    public override string ToString() => Value;

    public override string ToTextmateString() => Escape(Value);

    public override int Len() => Value.Length;

    public override Marker Clone() => new Text(Value);
}

/// <summary>A node that can carry a regular expression transform (upstream's TransformableMarker).</summary>
internal abstract class TransformableMarker : Marker
{
    /// <summary>The transform, if any.</summary>
    public Transform? Transform { get; set; }
}

/// <summary>A tab stop or placeholder: <c>$1</c>, <c>${1:default}</c>, <c>${1|a,b|}</c> (upstream's Placeholder).</summary>
internal sealed class Placeholder : TransformableMarker
{
    public Placeholder(int index)
    {
        Index = index;
    }

    /// <summary>The tab stop number; 0 is the final tab stop.</summary>
    public int Index { get; set; }

    /// <summary>Orders by index with the final tab stop last.</summary>
    public static int CompareByIndex(Placeholder a, Placeholder b)
    {
        if (a.Index == b.Index)
        {
            return 0;
        }
        else if (a.IsFinalTabstop)
        {
            return 1;
        }
        else if (b.IsFinalTabstop)
        {
            return -1;
        }
        else if (a.Index < b.Index)
        {
            return -1;
        }
        else if (a.Index > b.Index)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    /// <summary>Whether this is <c>$0</c>.</summary>
    public bool IsFinalTabstop => Index == 0;

    /// <summary>The choice of a <c>${1|a,b|}</c> placeholder.</summary>
    public Choice? Choice => _children.Count == 1 && _children[0] is Choice choice ? choice : null;

    public override string ToTextmateString()
    {
        var transformString = Transform?.ToTextmateString() ?? "";
        if (Children.Count == 0 && Transform is null)
        {
            return "$" + Index.ToString(CultureInfo.InvariantCulture);
        }
        else if (Children.Count == 0)
        {
            return "${" + Index.ToString(CultureInfo.InvariantCulture) + transformString + "}";
        }
        else if (Choice is { } choice)
        {
            return "${" + Index.ToString(CultureInfo.InvariantCulture) + "|" + choice.ToTextmateString() + "|" + transformString + "}";
        }
        else
        {
            var builder = new StringBuilder();
            foreach (var child in Children)
            {
                builder.Append(child.ToTextmateString());
            }

            return "${" + Index.ToString(CultureInfo.InvariantCulture) + ":" + builder + transformString + "}";
        }
    }

    public override Marker Clone()
    {
        var ret = new Placeholder(Index);
        if (Transform is not null)
        {
            ret.Transform = (Transform)Transform.Clone();
        }

        ret.SetChildren(CloneChildren());
        return ret;
    }
}

/// <summary>The options of a choice placeholder <c>${1|one,two|}</c> (upstream's Choice).</summary>
internal sealed class Choice : Marker
{
    private static readonly Regex s_escape = new(@"\||,|\\", RegexOptions.CultureInvariant);

    /// <summary>The options; the first is what the snippet inserts.</summary>
    public List<Text> Options { get; } = [];

    public override Marker AppendChild(Marker marker)
    {
        if (marker is Text text)
        {
            text.Parent = this;
            Options.Add(text);
        }

        return this;
    }

    public override string ToString() => Options[0].Value;

    public override string ToTextmateString() =>
        string.Join(",", Options.Select(option => s_escape.Replace(option.Value, @"\$&")));

    public override int Len() => Options[0].Len();

    public override Marker Clone()
    {
        var ret = new Choice();
        foreach (var option in Options)
        {
            ret.AppendChild(option);
        }

        return ret;
    }
}

/// <summary>A regular expression transform: <c>/regex/format/options</c> (upstream's Transform).</summary>
internal sealed class Transform : Marker
{
    /// <summary>The regular expression; its <c>g</c> flag replaces every match.</summary>
    public JsRegExp Regexp { get; set; } = new("");

    /// <summary>Replaces the matches of <see cref="Regexp"/> in <paramref name="value"/> by the format.</summary>
    public string Resolve(string value)
    {
        var didMatch = false;
        string ret;
        try
        {
            var regex = Regexp.Regex;
            ret = regex.Replace(value, match =>
            {
                didMatch = true;
                var groups = new string?[match.Groups.Count];
                for (var i = 0; i < groups.Length; i++)
                {
                    groups[i] = match.Groups[i].Success ? match.Groups[i].Value : null;
                }

                return ReplaceCore(groups);
            }, Regexp.Global ? -1 : 1);
        }
        catch (RegexMatchTimeoutException)
        {
            return value;
        }

        // when the regex didn't match and when the transform has
        // else branches, then run those
        if (!didMatch && _children.Any(child => child is FormatString { ElseValue.Length: > 0 }))
        {
            ret = ReplaceCore([]);
        }

        return ret;
    }

    private string ReplaceCore(string?[] groups)
    {
        var ret = new StringBuilder();
        foreach (var marker in _children)
        {
            if (marker is FormatString formatString)
            {
                var value = formatString.Index < groups.Length ? groups[formatString.Index] ?? "" : "";
                value = formatString.Resolve(value);
                ret.Append(value);
            }
            else
            {
                ret.Append(marker.ToString());
            }
        }

        return ret.ToString();
    }

    public override string ToString() => "";

    public override string ToTextmateString()
    {
        var builder = new StringBuilder();
        foreach (var child in Children)
        {
            builder.Append(child.ToTextmateString());
        }

        return "/" + Regexp.Source + "/" + builder + "/" + (Regexp.IgnoreCase ? "i" : "") + (Regexp.Global ? "g" : "");
    }

    public override Marker Clone()
    {
        var ret = new Transform
        {
            Regexp = new JsRegExp(Regexp.Source, (Regexp.IgnoreCase ? "i" : "") + (Regexp.Global ? "g" : "")),
        };
        ret.SetChildren(CloneChildren());
        return ret;
    }
}

/// <summary>A group reference in a transform's format: <c>$1</c>, <c>${1:/upcase}</c>, <c>${1:+if}</c>, ... (upstream's FormatString).</summary>
internal sealed class FormatString : Marker
{
    private static readonly Regex s_words = new(@"[\p{L}0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex s_trimUnderscores = new(@"^_+|_+$", RegexOptions.CultureInvariant);
    private static readonly Regex s_whitespaceOrUnderscores = new(@"[\s_]+", RegexOptions.CultureInvariant);
    private static readonly Regex s_separators = new(@"[\s_-]+", RegexOptions.CultureInvariant);
    private static readonly Regex s_kebabWords = new(
        @"\p{Lu}{2,}(?=\p{Lu}\p{Ll}+[0-9]*|[\s_-]|$)|\p{Lu}?\p{Ll}+[0-9]*|\p{Lu}(?=\p{Lu}\p{Ll})|\p{Lu}(?=[\s_-]|$)|[0-9]+",
        RegexOptions.CultureInvariant);
    private static readonly Regex s_lowerUpper = new(@"(\p{Ll})(\p{Lu})", RegexOptions.CultureInvariant);
    private static readonly Regex s_spacesOrDashes = new(@"[\s\-]+", RegexOptions.CultureInvariant);

    public FormatString(int index, string? shorthandName = null, string? ifValue = null, string? elseValue = null)
    {
        Index = index;
        ShorthandName = shorthandName;
        IfValue = ifValue;
        ElseValue = elseValue;
    }

    /// <summary>The group number.</summary>
    public int Index { get; }

    /// <summary><c>upcase</c>, <c>downcase</c>, <c>capitalize</c>, <c>pascalcase</c>, <c>camelcase</c>, <c>kebabcase</c>, <c>snakecase</c>.</summary>
    public string? ShorthandName { get; }

    /// <summary>The text inserted when the group matched.</summary>
    public string? IfValue { get; }

    /// <summary>The text inserted when the group did not match.</summary>
    public string? ElseValue { get; }

    /// <summary>The text for a group value.</summary>
    public string Resolve(string? value)
    {
        var empty = string.IsNullOrEmpty(value);
        switch (ShorthandName)
        {
            case "upcase":
                return empty ? "" : value!.ToUpper(CultureInfo.CurrentCulture);
            case "downcase":
                return empty ? "" : value!.ToLower(CultureInfo.CurrentCulture);
            case "capitalize":
                return empty ? "" : value![..1].ToUpper(CultureInfo.CurrentCulture) + value[1..];
            case "pascalcase":
                return empty ? "" : ToPascalCase(value!);
            case "camelcase":
                return empty ? "" : ToCamelCase(value!);
            case "kebabcase":
                return empty ? "" : ToKebabCase(value!);
            case "snakecase":
                return empty ? "" : ToSnakeCase(value!);
        }

        if (!empty && IfValue is not null)
        {
            return IfValue;
        }
        else if (empty && ElseValue is not null)
        {
            return ElseValue;
        }
        else
        {
            return value ?? "";
        }
    }

    // Note: word-based case transforms rely on uppercase/lowercase distinctions.
    // For scripts without case, transforms are effectively no-ops.
    private static string ToKebabCase(string value)
    {
        if (!s_words.IsMatch(value))
        {
            return value;
        }

        var cleaned = s_trimUnderscores.Replace(value.Trim(), "");

        var match2 = s_kebabWords.Matches(cleaned);
        if (match2.Count == 0)
        {
            return string.Join("-", s_separators.Split(cleaned)
                .Where(word => word.Length > 0)
                .Select(word => word.ToLowerInvariant()));
        }

        return string.Join("-", match2.Select(x => x.Value.ToLowerInvariant()));
    }

    private static string ToPascalCase(string value)
    {
        var match = s_words.Matches(value);
        if (match.Count == 0)
        {
            return value;
        }

        return string.Concat(match.Select(word => word.Value[..1].ToUpperInvariant() + word.Value[1..]));
    }

    private static string ToCamelCase(string value)
    {
        var match = s_words.Matches(value);
        if (match.Count == 0)
        {
            return value;
        }

        return string.Concat(match.Select((word, index) => index == 0
            ? word.Value[..1].ToLowerInvariant() + word.Value[1..]
            : word.Value[..1].ToUpperInvariant() + word.Value[1..]));
    }

    private static string ToSnakeCase(string value) =>
        s_spacesOrDashes.Replace(s_lowerUpper.Replace(value, "$1_$2"), "_").ToLowerInvariant();

    public override string ToTextmateString()
    {
        var value = new StringBuilder("${");
        value.Append(Index.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(ShorthandName))
        {
            value.Append(":/").Append(ShorthandName);
        }
        else if (!string.IsNullOrEmpty(IfValue) && !string.IsNullOrEmpty(ElseValue))
        {
            value.Append(":?").Append(IfValue).Append(':').Append(ElseValue);
        }
        else if (!string.IsNullOrEmpty(IfValue))
        {
            value.Append(":+").Append(IfValue);
        }
        else if (!string.IsNullOrEmpty(ElseValue))
        {
            value.Append(":-").Append(ElseValue);
        }

        value.Append('}');
        return value.ToString();
    }

    public override Marker Clone() => new FormatString(Index, ShorthandName, IfValue, ElseValue);
}

/// <summary>A variable: <c>$TM_SELECTED_TEXT</c>, <c>${CLIPBOARD:default}</c> (upstream's Variable).</summary>
internal sealed class Variable : TransformableMarker
{
    public Variable(string name)
    {
        Name = name;
    }

    /// <summary>The variable's name.</summary>
    public string Name { get; set; }

    /// <summary>
    /// Asks <paramref name="resolver"/> for the value (and runs the transform on it); true when the variable now has a
    /// value.
    /// </summary>
    public bool Resolve(IVariableResolver resolver)
    {
        var value = resolver.Resolve(this);
        if (Transform is not null)
        {
            value = Transform.Resolve(value ?? "");
        }

        if (value is not null)
        {
            SetChildren([new Text(value) { Parent = this }]);
            return true;
        }

        return false;
    }

    public override string ToTextmateString()
    {
        var transformString = Transform?.ToTextmateString() ?? "";
        if (Children.Count == 0)
        {
            return "${" + Name + transformString + "}";
        }
        else
        {
            var builder = new StringBuilder();
            foreach (var child in Children)
            {
                builder.Append(child.ToTextmateString());
            }

            return "${" + Name + ":" + builder + transformString + "}";
        }
    }

    public override Marker Clone()
    {
        var ret = new Variable(Name);
        if (Transform is not null)
        {
            ret.Transform = (Transform)Transform.Clone();
        }

        ret.SetChildren(CloneChildren());
        return ret;
    }
}

/// <summary>Gives variables their values (upstream's VariableResolver).</summary>
internal interface IVariableResolver
{
    /// <summary>The value of <paramref name="variable"/>, or null when this resolver does not know it.</summary>
    string? Resolve(Variable variable);
}

/// <summary>The placeholders of a snippet in document order and the one with the highest index.</summary>
internal sealed record PlaceholderInfo(List<Placeholder> All, Placeholder? Last);

/// <summary>A parsed snippet: the root of the tree (upstream's TextmateSnippet).</summary>
internal sealed class TextmateSnippet : Marker
{
    private PlaceholderInfo? _placeholders;

    /// <summary>The placeholders (cached until the tree changes through this snippet).</summary>
    public PlaceholderInfo PlaceholderInfo
    {
        get
        {
            if (_placeholders is null)
            {
                // fill in placeholders
                var all = new List<Placeholder>();
                Placeholder? last = null;
                Walk(candidate =>
                {
                    if (candidate is Placeholder placeholder)
                    {
                        all.Add(placeholder);
                        last = last is null || last.Index < placeholder.Index ? placeholder : last;
                    }

                    return true;
                });
                _placeholders = new PlaceholderInfo(all, last);
            }

            return _placeholders;
        }
    }

    /// <summary>The placeholders in document order.</summary>
    public List<Placeholder> Placeholders => PlaceholderInfo.All;

    /// <summary>The offset of <paramref name="marker"/>'s text in the snippet's text, or -1 when it is not in the snippet.</summary>
    public int Offset(Marker marker)
    {
        var pos = 0;
        var found = false;
        Walk(candidate =>
        {
            if (ReferenceEquals(candidate, marker))
            {
                found = true;
                return false;
            }

            pos += candidate.Len();
            return true;
        });

        if (!found)
        {
            return -1;
        }

        return pos;
    }

    /// <summary>The length of the text of <paramref name="marker"/> and its descendants.</summary>
    public int FullLen(Marker marker)
    {
        var ret = 0;
        WalkMarkers([marker], m =>
        {
            ret += m.Len();
            return true;
        });
        return ret;
    }

    /// <summary>The placeholders that contain <paramref name="placeholder"/>, innermost first.</summary>
    public List<Placeholder> EnclosingPlaceholders(Placeholder placeholder)
    {
        var ret = new List<Placeholder>();
        var parent = placeholder.Parent;
        while (parent is not null)
        {
            if (parent is Placeholder enclosing)
            {
                ret.Add(enclosing);
            }

            parent = parent.Parent;
        }

        return ret;
    }

    /// <summary>Resolves every variable with <paramref name="resolver"/>.</summary>
    public TextmateSnippet ResolveVariables(IVariableResolver resolver)
    {
        Walk(candidate =>
        {
            if (candidate is Variable variable)
            {
                if (variable.Resolve(resolver))
                {
                    _placeholders = null;
                }
            }

            return true;
        });
        return this;
    }

    public override Marker AppendChild(Marker child)
    {
        _placeholders = null;
        return base.AppendChild(child);
    }

    public override void Replace(Marker child, IReadOnlyList<Marker> others)
    {
        _placeholders = null;
        base.Replace(child, others);
    }

    public override string ToTextmateString()
    {
        var builder = new StringBuilder();
        foreach (var child in Children)
        {
            builder.Append(child.ToTextmateString());
        }

        return builder.ToString();
    }

    public override Marker Clone()
    {
        var ret = new TextmateSnippet();
        ret.SetChildren(CloneChildren());
        return ret;
    }

    /// <summary>Visits the nodes depth first in document order until <paramref name="visitor"/> returns false.</summary>
    public void Walk(Func<Marker, bool> visitor) => WalkMarkers(Children, visitor);

    // upstream's walk(): a pre-order traversal that stops at the first false
    internal static void WalkMarkers(IReadOnlyList<Marker> markers, Func<Marker, bool> visitor)
    {
        var stack = new Stack<Marker>();
        for (var i = markers.Count - 1; i >= 0; i--)
        {
            stack.Push(markers[i]);
        }

        while (stack.Count > 0)
        {
            var marker = stack.Pop();
            var recurse = visitor(marker);
            if (!recurse)
            {
                break;
            }

            var children = marker.Children;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }
}
