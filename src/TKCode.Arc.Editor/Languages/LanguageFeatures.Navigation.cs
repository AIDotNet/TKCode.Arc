// Ported from vscode (src/vs/editor/common/languages.ts: DefinitionProvider, DeclarationProvider, ImplementationProvider,
// TypeDefinitionProvider, ReferenceProvider, DocumentHighlightProvider, LinkedEditingRangeProvider,
// DocumentSymbolProvider, SelectionRangeProvider, FoldingRangeProvider, LinkProvider and their types, commit b57d5c9e),
// MIT license. The general differences are listed in LanguageFeatures.Types.cs. Here also:
//  - Definition (Location | Location[] | LocationLink[]) is a list of LocationLink, which a Location converts to
//    implicitly (upstream's goto code normalizes to links the same way);
//  - LinkedEditingRanges.wordPattern is a .NET Regex;
//  - ILink.url (URI | string) is Url or UrlText.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Provides the definitions of the symbol at a position (upstream's DefinitionProvider).</summary>
internal interface IDefinitionProvider
{
    Task<IReadOnlyList<LocationLink>?> ProvideDefinition(TextModel model, TextPosition position, CancellationToken token);
}

/// <summary>Provides the declarations of the symbol at a position (upstream's DeclarationProvider).</summary>
internal interface IDeclarationProvider
{
    Task<IReadOnlyList<LocationLink>?> ProvideDeclaration(TextModel model, TextPosition position, CancellationToken token);
}

/// <summary>Provides the implementations of the symbol at a position (upstream's ImplementationProvider).</summary>
internal interface IImplementationProvider
{
    Task<IReadOnlyList<LocationLink>?> ProvideImplementation(TextModel model, TextPosition position, CancellationToken token);
}

/// <summary>Provides the type definitions of the symbol at a position (upstream's TypeDefinitionProvider).</summary>
internal interface ITypeDefinitionProvider
{
    Task<IReadOnlyList<LocationLink>?> ProvideTypeDefinition(TextModel model, TextPosition position, CancellationToken token);
}

/// <summary>The context of a references request.</summary>
internal sealed record ReferenceContext(bool IncludeDeclaration);

/// <summary>Provides the references of the symbol at a position (upstream's ReferenceProvider).</summary>
internal interface IReferenceProvider
{
    Task<IReadOnlyList<Location>?> ProvideReferences(TextModel model, TextPosition position, ReferenceContext context, CancellationToken token);
}

/// <summary>A document highlight kind.</summary>
internal enum DocumentHighlightKind
{
    /// <summary>A textual occurrence.</summary>
    Text,

    /// <summary>Read-access of a symbol, like reading a variable.</summary>
    Read,

    /// <summary>Write-access of a symbol, like writing to a variable.</summary>
    Write,
}

/// <summary>A range to highlight, such as all occurrences of a variable (upstream's DocumentHighlight).</summary>
internal sealed record DocumentHighlight(TextRange Range)
{
    public DocumentHighlightKind? Kind { get; init; }
}

/// <summary>Provides the occurrences of the symbol at a position (upstream's DocumentHighlightProvider).</summary>
internal interface IDocumentHighlightProvider
{
    Task<IReadOnlyList<DocumentHighlight>?> ProvideDocumentHighlights(TextModel model, TextPosition position, CancellationToken token);
}

/// <summary>Ranges that are edited together and the word pattern they keep to (upstream's LinkedEditingRanges).</summary>
internal sealed class LinkedEditingRanges(IReadOnlyList<TextRange> ranges)
{
    public IReadOnlyList<TextRange> Ranges { get; } = ranges;

    public Regex? WordPattern { get; init; }
}

/// <summary>Provides ranges that change together (upstream's LinkedEditingRangeProvider).</summary>
internal interface ILinkedEditingRangeProvider
{
    Task<LinkedEditingRanges?> ProvideLinkedEditingRanges(TextModel model, TextPosition position, CancellationToken token);
}

/// <summary>What a document symbol is (upstream's SymbolKind).</summary>
internal enum SymbolKind
{
    File = 0,
    Module = 1,
    Namespace = 2,
    Package = 3,
    Class = 4,
    Method = 5,
    Property = 6,
    Field = 7,
    Constructor = 8,
    Enum = 9,
    Interface = 10,
    Function = 11,
    Variable = 12,
    Constant = 13,
    String = 14,
    Number = 15,
    Boolean = 16,
    Array = 17,
    Object = 18,
    Key = 19,
    Null = 20,
    EnumMember = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25,
}

internal enum SymbolTag
{
    Deprecated = 1,
}

/// <summary>A symbol of a document and its children (upstream's DocumentSymbol).</summary>
internal sealed record DocumentSymbol(string Name, string Detail, SymbolKind Kind, TextRange Range, TextRange SelectionRange)
{
    public IReadOnlyList<SymbolTag> Tags { get; init; } = [];

    public string? ContainerName { get; init; }

    public IReadOnlyList<DocumentSymbol>? Children { get; init; }
}

/// <summary>Provides the symbols of a document (upstream's DocumentSymbolProvider).</summary>
internal interface IDocumentSymbolProvider
{
    string? DisplayName => null;

    Task<IReadOnlyList<DocumentSymbol>?> ProvideDocumentSymbols(TextModel model, CancellationToken token);
}

/// <summary>A range smart selection grows to (upstream's SelectionRange).</summary>
internal sealed record SelectionRange(TextRange Range);

/// <summary>Provides the ranges smart selection grows through, for each position (upstream's SelectionRangeProvider).</summary>
internal interface ISelectionRangeProvider
{
    Task<IReadOnlyList<IReadOnlyList<SelectionRange>>?> ProvideSelectionRanges(TextModel model, IReadOnlyList<TextPosition> positions, CancellationToken token);
}

/// <summary>The context of a folding ranges request (upstream's FoldingContext: empty).</summary>
internal sealed record FoldingContext;

/// <summary>What a folding range is (upstream's FoldingRangeKind).</summary>
internal sealed record FoldingRangeKind(string Value)
{
    /// <summary>A comment.</summary>
    public static readonly FoldingRangeKind Comment = new("comment");

    /// <summary>Imports.</summary>
    public static readonly FoldingRangeKind Imports = new("imports");

    /// <summary>A region marked by #region / #endregion.</summary>
    public static readonly FoldingRangeKind Region = new("region");

    public static FoldingRangeKind FromValue(string value) => value switch
    {
        "comment" => Comment,
        "imports" => Imports,
        "region" => Region,
        _ => new FoldingRangeKind(value),
    };
}

/// <summary>Lines that fold together: <see cref="Start"/> stays visible (upstream's FoldingRange; 1-based lines).</summary>
internal sealed record FoldingRange(int Start, int End)
{
    public FoldingRangeKind? Kind { get; init; }
}

/// <summary>Provides folding ranges (upstream's FoldingRangeProvider).</summary>
internal interface IFoldingRangeProvider
{
    /// <summary>An optional id, to tell providers apart.</summary>
    string? Id => null;

    /// <summary>Raised when the ranges should be asked for again.</summary>
    event Action<IFoldingRangeProvider>? DidChange
    {
        add
        {
        }

        remove
        {
        }
    }

    Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token);
}

/// <summary>A link in a document (upstream's ILink).</summary>
internal sealed record Link(TextRange Range)
{
    public Uri? Url { get; init; }

    /// <summary>The target as text, when it is not a URI.</summary>
    public string? UrlText { get; init; }

    public string? Tooltip { get; init; }
}

/// <summary>The links of a document (upstream's ILinksList).</summary>
internal sealed class LinksList(IReadOnlyList<Link> links)
{
    public IReadOnlyList<Link> Links { get; } = links;

    public Action? Dispose { get; init; }
}

/// <summary>Provides the links of a document (upstream's LinkProvider).</summary>
internal interface ILinkProvider
{
    Task<LinksList?> ProvideLinks(TextModel model, CancellationToken token);

    /// <summary>Whether <see cref="ResolveLink"/> is implemented.</summary>
    bool SupportsResolveLink => false;

    Task<Link?> ResolveLink(Link link, CancellationToken token) => Task.FromResult<Link?>(null);
}
