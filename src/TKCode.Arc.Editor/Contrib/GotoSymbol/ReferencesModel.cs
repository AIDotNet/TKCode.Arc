// Ported from vscode (src/vs/editor/contrib/gotoSymbol/browser/referencesModel.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a FilePreview reads a TextModel it is given (and disposes it when it owns it); FileReferences.Resolve takes a
//    function that returns the model of a document (upstream: the text model resolver service);
//  - the ids of references come from a counter of the model (upstream: a process-wide id generator);
//  - URIs compare by their string, ordinally (upstream's extUri.compare with case-sensitive paths);
//  - the aria messages are kept, in English.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.GotoSymbol;

/// <summary>One result of a references or definitions request (upstream's OneReference).</summary>
internal sealed class OneReference
{
    private readonly Action<OneReference> _rangeCallback;
    private TextRange? _range;

    public OneReference(string id, bool isProviderFirst, FileReferences parent, LocationLink link, Action<OneReference> rangeCallback)
    {
        Id = id;
        IsProviderFirst = isProviderFirst;
        Parent = parent;
        Link = link;
        _rangeCallback = rangeCallback;
    }

    public string Id { get; }

    /// <summary>Whether this is the first result the providers returned (go to it first).</summary>
    public bool IsProviderFirst { get; }

    public FileReferences Parent { get; }

    public LocationLink Link { get; }

    public Uri Uri => Link.Uri;

    /// <summary>The range to reveal and select: the link's selection range, or its range.</summary>
    public TextRange Range
    {
        get => _range ?? Link.TargetSelectionRange ?? Link.Range;
        set
        {
            _range = value;
            _rangeCallback(this);
        }
    }

    public string AriaMessage
    {
        get
        {
            var range = Range;
            if (Parent.GetPreview(this)?.Preview(range) is not { } preview)
            {
                return $"in {ReferencesModel.Basename(Uri)} on line {range.StartLineNumber} at column {range.StartColumn}";
            }

            return $"{preview.Value} in {ReferencesModel.Basename(Uri)} on line {range.StartLineNumber} at column {range.StartColumn}";
        }
    }
}

/// <summary>The text around a reference, with the reference's part marked (upstream's preview result).</summary>
/// <param name="Value">The text: a few characters of the word before, the reference, the rest of its last line.</param>
/// <param name="HighlightStart">Where the reference starts in <paramref name="Value"/>.</param>
/// <param name="HighlightEnd">Where the reference ends in <paramref name="Value"/>.</param>
internal sealed record ReferencePreview(string Value, int HighlightStart, int HighlightEnd);

/// <summary>The text of one document, for the previews of its references (upstream's FilePreview).</summary>
internal sealed class FilePreview : IDisposable
{
    private readonly bool _ownsModel;

    public FilePreview(TextModel model, bool ownsModel)
    {
        Model = model;
        _ownsModel = ownsModel;
    }

    public TextModel Model { get; }

    public void Dispose()
    {
        if (_ownsModel)
        {
            Model.Dispose();
        }
    }

    /// <summary>The preview of <paramref name="range"/>: up to <paramref name="n"/> characters of the word before it, the range and the rest of its line.</summary>
    public ReferencePreview? Preview(TextRange range, int n = 8)
    {
        if (Model.IsDisposed())
        {
            return null;
        }

        var word = ModelWords.GetWordUntilPosition(Model, new TextPosition(range.StartLineNumber, range.StartColumn - n));
        var beforeRange = new TextRange(range.StartLineNumber, word.StartColumn, range.StartLineNumber, range.StartColumn);
        var afterRange = new TextRange(range.EndLineNumber, range.EndColumn, range.EndLineNumber, int.MaxValue);

        var before = Model.GetValueInRange(beforeRange).TrimStart();
        var inside = Model.GetValueInRange(range);
        var after = Model.GetValueInRange(afterRange).TrimEnd();

        return new ReferencePreview(before + inside + after, before.Length, before.Length + inside.Length);
    }
}

/// <summary>The references in one document (upstream's FileReferences).</summary>
internal sealed class FileReferences : IDisposable
{
    private readonly Dictionary<string, FilePreview> _previews = new(StringComparer.Ordinal);

    public FileReferences(ReferencesModel parent, Uri uri)
    {
        Parent = parent;
        Uri = uri;
    }

    public ReferencesModel Parent { get; }

    public Uri Uri { get; }

    public List<OneReference> Children { get; } = new();

    public void Dispose()
    {
        foreach (var preview in _previews.Values)
        {
            preview.Dispose();
        }

        _previews.Clear();
    }

    public FilePreview? GetPreview(OneReference child) => _previews.GetValueOrDefault(child.Uri.ToString());

    public string AriaMessage => Children.Count == 1
        ? $"1 symbol in {ReferencesModel.Basename(Uri)}, full path {Uri.LocalPath}"
        : $"{Children.Count} symbols in {ReferencesModel.Basename(Uri)}, full path {Uri.LocalPath}";

    /// <summary>Gets the text of the document for the previews; <paramref name="resolve"/> returns a model and whether the preview owns it.</summary>
    public async Task<FileReferences> Resolve(Func<Uri, Task<(TextModel Model, bool Owned)?>> resolve)
    {
        if (_previews.Count != 0)
        {
            return this;
        }

        foreach (var child in Children)
        {
            var key = child.Uri.ToString();
            if (_previews.ContainsKey(key))
            {
                continue;
            }

            try
            {
                if (await resolve(child.Uri).ConfigureAwait(true) is { } resolved)
                {
                    _previews[key] = new FilePreview(resolved.Model, resolved.Owned);
                }
            }
            catch (Exception error)
            {
                UnexpectedErrors.Report(error);
            }
        }

        return this;
    }
}

/// <summary>The results of a references or definitions request, grouped by document and sorted (upstream's ReferencesModel).</summary>
internal sealed class ReferencesModel : IDisposable
{
    private readonly IReadOnlyList<LocationLink> _links;
    private int _nextId;

    public ReferencesModel(IReadOnlyList<LocationLink> links, string title)
    {
        _links = links;
        Title = title;

        // grouping and sorting
        var providersFirst = links.Count > 0 ? links[0] : null;
        var sorted = links.Order(Comparer<LocationLink>.Create(CompareReferences)).ToList();

        FileReferences? current = null;
        foreach (var link in sorted)
        {
            if (current is null || !string.Equals(current.Uri.ToString(), link.Uri.ToString(), StringComparison.Ordinal))
            {
                // new group
                current = new FileReferences(this, link.Uri);
                Groups.Add(current);
            }

            // append, check for equality first
            if (current.Children.Count == 0 || CompareReferences(link, current.Children[^1].Link) != 0)
            {
                var oneRef = new OneReference(
                    (++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ReferenceEquals(providersFirst, link),
                    current,
                    link,
                    reference => DidChangeReferenceRange?.Invoke(reference));
                References.Add(oneRef);
                current.Children.Add(oneRef);
            }
        }
    }

    /// <summary>Raised when the range of a reference was moved (the decorations that track it followed an edit).</summary>
    public event Action<OneReference>? DidChangeReferenceRange;

    public List<FileReferences> Groups { get; } = new();

    public List<OneReference> References { get; } = new();

    public string Title { get; }

    public bool IsEmpty => Groups.Count == 0;

    public string AriaMessage
    {
        get
        {
            if (IsEmpty)
            {
                return "No results found";
            }

            if (References.Count == 1)
            {
                return $"Found 1 symbol in {References[0].Uri.LocalPath}";
            }

            if (Groups.Count == 1)
            {
                return $"Found {References.Count} symbols in {Groups[0].Uri.LocalPath}";
            }

            return $"Found {References.Count} symbols in {Groups.Count} files";
        }
    }

    public void Dispose()
    {
        foreach (var group in Groups)
        {
            group.Dispose();
        }

        Groups.Clear();
        DidChangeReferenceRange = null;
    }

    /// <summary>A new model of the same links (upstream's clone).</summary>
    public ReferencesModel Clone() => new(_links, Title);

    /// <summary>The next or previous reference, cycling within the document when it is the only one, else across documents.</summary>
    public OneReference NextOrPreviousReference(OneReference reference, bool next)
    {
        var parent = reference.Parent;
        var idx = parent.Children.IndexOf(reference);
        var childCount = parent.Children.Count;
        var groupCount = parent.Parent.Groups.Count;

        if (groupCount == 1 || (next && idx + 1 < childCount) || (!next && idx > 0))
        {
            // cycling within one file
            idx = next ? (idx + 1) % childCount : (idx + childCount - 1) % childCount;
            return parent.Children[idx];
        }

        idx = parent.Parent.Groups.IndexOf(parent);
        if (next)
        {
            idx = (idx + 1) % groupCount;
            return parent.Parent.Groups[idx].Children[0];
        }

        idx = (idx + groupCount - 1) % groupCount;
        var group = parent.Parent.Groups[idx];
        return group.Children[^1];
    }

    /// <summary>The reference nearest to a position: in the document whose URI shares the longest prefix, then the closest range.</summary>
    public OneReference? NearestReference(Uri resource, TextPosition position)
    {
        var resourceText = resource.ToString();
        var nearest = References
            .Select((reference, idx) => (
                Idx: idx,
                PrefixLen: CommonPrefixLength(reference.Uri.ToString(), resourceText),
                OffsetDist: (Math.Abs(reference.Range.StartLineNumber - position.LineNumber) * 100) + Math.Abs(reference.Range.StartColumn - position.Column)))
            .Order(Comparer<(int Idx, int PrefixLen, int OffsetDist)>.Create(static (a, b) =>
            {
                if (a.PrefixLen != b.PrefixLen)
                {
                    return b.PrefixLen.CompareTo(a.PrefixLen);
                }

                return a.OffsetDist.CompareTo(b.OffsetDist);
            }))
            .Select(static x => (int?)x.Idx)
            .FirstOrDefault();

        return nearest is { } index ? References[index] : null;
    }

    /// <summary>The reference whose range contains the position in the document, or null.</summary>
    public OneReference? ReferenceAt(Uri resource, TextPosition position)
    {
        var resourceText = resource.ToString();
        foreach (var reference in References)
        {
            if (reference.Uri.ToString() == resourceText && reference.Range.ContainsPosition(position))
            {
                return reference;
            }
        }

        return null;
    }

    /// <summary>The first result the providers returned, else the first reference.</summary>
    public OneReference? FirstReference()
    {
        foreach (var reference in References)
        {
            if (reference.IsProviderFirst)
            {
                return reference;
            }
        }

        return References.Count > 0 ? References[0] : null;
    }

    /// <summary>The last segment of a URI's path.</summary>
    internal static string Basename(Uri uri)
    {
        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        var slash = path.TrimEnd('/').LastIndexOf('/');
        return Uri.UnescapeDataString(slash >= 0 ? path.TrimEnd('/')[(slash + 1)..] : path);
    }

    private static int CompareReferences(LocationLink a, LocationLink b)
    {
        var byUri = string.CompareOrdinal(a.Uri.ToString(), b.Uri.ToString());
        return byUri != 0 ? Math.Sign(byUri) : TextRange.CompareRangesUsingStarts(a.Range, b.Range);
    }

    private static int CommonPrefixLength(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        for (var i = 0; i < length; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return length;
    }
}
