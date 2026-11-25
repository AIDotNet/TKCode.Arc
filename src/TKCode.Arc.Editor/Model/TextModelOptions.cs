// Ported from vscode (src/vs/editor/common/model.ts and core/misc/textModelDefaults.ts, commit b57d5c9e), MIT license.
// Upstream's indentSize is `number | 'tabSize'`; here it is a nullable int and null means "the tab size".

namespace TKCode.Arc.Editor.Model;

/// <summary>Bracket pair colorization settings of a model.</summary>
internal readonly record struct BracketPairColorizationOptions(bool Enabled, bool IndependentColorPoolPerBracketType);

/// <summary>The defaults a model is created with.</summary>
internal static class EditorModelDefaults
{
    public const int TabSize = 4;
    public const int IndentSize = 4;
    public const bool InsertSpaces = true;
    public const bool DetectIndentation = true;
    public const bool TrimAutoWhitespace = true;
    public const bool LargeFileOptimizations = true;

    public static BracketPairColorizationOptions BracketPairColorizationOptions => new(true, false);
}

/// <summary>The options a model is created with (upstream's ITextModelCreationOptions).</summary>
internal sealed record TextModelCreationOptions
{
    public static TextModelCreationOptions Default { get; } = new();

    public int TabSize { get; init; } = EditorModelDefaults.TabSize;

    /// <summary>The indent size; null follows <see cref="TabSize"/>.</summary>
    public int? IndentSize { get; init; } = EditorModelDefaults.IndentSize;

    public bool InsertSpaces { get; init; } = EditorModelDefaults.InsertSpaces;

    /// <summary>Guess <see cref="TabSize"/> and <see cref="InsertSpaces"/> from the content.</summary>
    public bool DetectIndentation { get; init; } = EditorModelDefaults.DetectIndentation;

    public bool TrimAutoWhitespace { get; init; } = EditorModelDefaults.TrimAutoWhitespace;

    public DefaultEndOfLine DefaultEOL { get; init; } = DefaultEndOfLine.LF;

    /// <summary>The model backs a small input (no undo stack sharing, no large-file handling).</summary>
    public bool IsForSimpleWidget { get; init; }

    public bool LargeFileOptimizations { get; init; } = EditorModelDefaults.LargeFileOptimizations;

    public BracketPairColorizationOptions BracketPairColorizationOptions { get; init; } =
        EditorModelDefaults.BracketPairColorizationOptions;
}

/// <summary>A change to a model's options; unset members stay as they are (upstream's ITextModelUpdateOptions).</summary>
internal sealed record TextModelUpdateOptions
{
    public int? TabSize { get; init; }

    public int? IndentSize { get; init; }

    /// <summary>Make the indent size follow the tab size (upstream's <c>indentSize: 'tabSize'</c>).</summary>
    public bool IndentSizeIsTabSize { get; init; }

    public bool? InsertSpaces { get; init; }

    public bool? TrimAutoWhitespace { get; init; }

    public BracketPairColorizationOptions? BracketColorizationOptions { get; init; }
}

/// <summary>The options a model works with.</summary>
internal sealed class TextModelResolvedOptions : IEquatable<TextModelResolvedOptions>
{
    private readonly bool _indentSizeIsTabSize;

    public TextModelResolvedOptions(
        int tabSize,
        int? indentSize,
        bool insertSpaces,
        DefaultEndOfLine defaultEOL,
        bool trimAutoWhitespace,
        BracketPairColorizationOptions bracketPairColorizationOptions)
    {
        TabSize = Math.Max(1, tabSize);
        if (indentSize is { } size)
        {
            IndentSize = Math.Max(1, size);
            _indentSizeIsTabSize = false;
        }
        else
        {
            IndentSize = TabSize;
            _indentSizeIsTabSize = true;
        }

        InsertSpaces = insertSpaces;
        DefaultEOL = defaultEOL;
        TrimAutoWhitespace = trimAutoWhitespace;
        BracketPairColorizationOptions = bracketPairColorizationOptions;
    }

    public int TabSize { get; }

    public int IndentSize { get; }

    public bool InsertSpaces { get; }

    public DefaultEndOfLine DefaultEOL { get; }

    public bool TrimAutoWhitespace { get; }

    public BracketPairColorizationOptions BracketPairColorizationOptions { get; }

    /// <summary>The indent size as it was given: null when it follows the tab size.</summary>
    public int? OriginalIndentSize => _indentSizeIsTabSize ? null : IndentSize;

    public bool Equals(TextModelResolvedOptions? other) =>
        other is not null
        && TabSize == other.TabSize
        && _indentSizeIsTabSize == other._indentSizeIsTabSize
        && IndentSize == other.IndentSize
        && InsertSpaces == other.InsertSpaces
        && DefaultEOL == other.DefaultEOL
        && TrimAutoWhitespace == other.TrimAutoWhitespace
        && BracketPairColorizationOptions == other.BracketPairColorizationOptions;

    public override bool Equals(object? obj) => Equals(obj as TextModelResolvedOptions);

    public override int GetHashCode() =>
        HashCode.Combine(TabSize, _indentSizeIsTabSize, IndentSize, InsertSpaces, DefaultEOL, TrimAutoWhitespace, BracketPairColorizationOptions);

    public ModelOptionsChangedEvent CreateChangeEvent(TextModelResolvedOptions newOpts) =>
        new(
            TabSize != newOpts.TabSize,
            IndentSize != newOpts.IndentSize,
            InsertSpaces != newOpts.InsertSpaces,
            TrimAutoWhitespace != newOpts.TrimAutoWhitespace);
}
