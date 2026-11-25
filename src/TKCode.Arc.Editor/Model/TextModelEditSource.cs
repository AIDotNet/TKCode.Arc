// Ported from vscode (src/vs/editor/common/textModelEditSource.ts, commit b57d5c9e), MIT license.
// Upstream's metadata is an open record that feeds telemetry (chat, inline completion and extension ids).
// Here it is the source, a kind and a detail; the sources the editor itself produces are in EditSources.

namespace TKCode.Arc.Editor.Model;

/// <summary>Says what caused an edit: typing, paste, undo, a snippet, <c>SetValue</c>, ...</summary>
internal sealed class TextModelEditSource
{
    internal TextModelEditSource(string source, string? kind = null, string? detailedSource = null)
    {
        Source = source;
        Kind = kind;
        DetailedSource = detailedSource;
    }

    /// <summary>The kind of source: <c>cursor</c>, <c>setValue</c>, <c>applyEdits</c>, ...</summary>
    public string Source { get; }

    /// <summary>For <c>cursor</c>: <c>type</c>, <c>paste</c>, <c>cut</c>, ...; for <c>unknown</c>: its name.</summary>
    public string? Kind { get; }

    public string? DetailedSource { get; }

    public string GetSourceType() => Source switch
    {
        "cursor" => Kind ?? Source,
        "unknown" => string.IsNullOrEmpty(Kind) ? "unknown" : Kind,
        _ => Source,
    };

    public override string ToString() => Source;
}

/// <summary>The edit sources the editor produces.</summary>
internal static class EditSources
{
    private static readonly TextModelEditSource s_setValue = new("setValue");
    private static readonly TextModelEditSource s_eolChange = new("eolChange");
    private static readonly TextModelEditSource s_applyEdits = new("applyEdits");
    private static readonly TextModelEditSource s_snippet = new("snippet");
    private static readonly TextModelEditSource s_reloadFromDisk = new("reloadFromDisk");

    public static TextModelEditSource Unknown(string? name = null) => new("unknown", name);

    public static TextModelEditSource Cursor(string kind, string? detailedSource = null) => new("cursor", kind, detailedSource);

    public static TextModelEditSource SetValue() => s_setValue;

    public static TextModelEditSource EolChange() => s_eolChange;

    public static TextModelEditSource ApplyEdits() => s_applyEdits;

    public static TextModelEditSource Snippet() => s_snippet;

    public static TextModelEditSource ReloadFromDisk() => s_reloadFromDisk;

    /// <summary>Whether the user typed the edit.</summary>
    public static bool IsUserEdit(TextModelEditSource source) => source.Source == "cursor" && source.Kind == "type";
}
