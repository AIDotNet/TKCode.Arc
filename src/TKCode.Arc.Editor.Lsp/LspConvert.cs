// Conversions between the Language Server Protocol's structures and the engine's: positions (LSP: 0-based line and
// UTF-16 character; the engine: 1-based line and UTF-16 column), ranges, URIs, markup, diagnostics, edits and
// workspace edits. Written for this package; the mapping follows the specification and the shapes of the engine's
// languages.ts port. The feature results are in LspConvert.Features.cs.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Lsp.Protocol;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc.Editor.Lsp;

internal static partial class LspConvert
{
    private static readonly LspJsonContext s_json = LspJsonContext.Default;

    /// <summary>JSON null (an immutable element of its own document).</summary>
    public static JsonElement NullElement { get; } = JsonDocument.Parse("null").RootElement.Clone();

    // ---- positions and ranges ----

    /// <summary>An LSP position (0-based) as an engine position (1-based); negative values clamp to the start.</summary>
    public static TextPosition ToPosition(LspPosition position) =>
        new(Math.Max(0, position.Line) + 1, Math.Max(0, position.Character) + 1);

    public static TextRange ToRange(LspRange range) => new(ToPosition(range.Start), ToPosition(range.End));

    public static LspPosition FromPosition(TextPosition position) => new(position.LineNumber - 1, position.Column - 1);

    public static LspRange FromRange(TextRange range) =>
        new(new LspPosition(range.StartLineNumber - 1, range.StartColumn - 1), new LspPosition(range.EndLineNumber - 1, range.EndColumn - 1));

    // ---- URIs ----

    /// <summary>The text a URI has in the protocol (percent-encoded, as <see cref="Uri.AbsoluteUri"/>).</summary>
    public static string FromUri(Uri uri) => uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.OriginalString;

    /// <summary>A protocol URI; a text that is no absolute URI stays relative instead of failing.</summary>
    public static Uri ToUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var absolute) ? absolute : new Uri(uri, UriKind.RelativeOrAbsolute);

    /// <summary>
    /// The key two spellings of one document share: servers differ in what they percent-encode (a drive colon, a
    /// space), so the key is the unescaped text, with a file URI's drive letter in lower case.
    /// </summary>
    public static string UriKey(string uri)
    {
        var text = Uri.UnescapeDataString(uri);
        if (text.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) && text.Length > 9 && text[9] == ':' && char.IsAsciiLetter(text[8]))
        {
            text = "file:///" + char.ToLowerInvariant(text[8]) + text[9..];
        }
        return text;
    }

    public static string UriKey(Uri uri) => UriKey(FromUri(uri));

    // ---- markup ----

    [GeneratedRegex(@"[\\`*_{}\[\]()#+\-!~]")]
    private static partial Regex MarkdownSyntax();

    /// <summary>Plain text shown as Markdown: the syntax characters escaped (upstream's escapeMarkdownSyntaxTokens).</summary>
    public static string EscapeMarkdown(string text) => MarkdownSyntax().Replace(text, @"\$&");

    /// <summary>MarkupContent, MarkedString or a list of MarkedString (hover contents) as Markdown strings.</summary>
    public static IReadOnlyList<MarkdownString> ToMarkdownStrings(LspHoverContents contents)
    {
        var list = new List<MarkdownString>();
        switch (contents)
        {
            case LspHoverItem[] items:
                foreach (var item in items)
                {
                    if (ToMarkdownString(item) is { } markdown)
                    {
                        list.Add(markdown);
                    }
                }
                break;
            case string text:
                if (text.Length > 0)
                {
                    list.Add(new MarkdownString(text));
                }
                break;
            case LspMarkupContent markup:
                if (ToMarkdownString(markup) is { } content)
                {
                    list.Add(content);
                }
                break;
            case LspMarkedStringObject marked:
                list.Add(ToMarkdownString(marked));
                break;
        }
        return list;
    }

    private static MarkdownString? ToMarkdownString(LspHoverItem item) => item switch
    {
        string text => text.Length == 0 ? null : new MarkdownString(text),
        LspMarkupContent markup => ToMarkdownString(markup),
        LspMarkedStringObject marked => ToMarkdownString(marked),
        _ => null,
    };

    private static MarkdownString? ToMarkdownString(LspMarkupContent markup)
    {
        if (string.IsNullOrEmpty(markup.Value))
        {
            return null;
        }
        return markup.Kind == "markdown"
            ? new MarkdownString(markup.Value)
            : new MarkdownString(EscapeMarkdown(markup.Value));
    }

    private static MarkdownString ToMarkdownString(LspMarkedStringObject marked) =>
        new("```" + marked.Language + "\n" + marked.Value + "\n```");

    /// <summary>string | MarkupContent (documentation): plain text stays a string, Markdown becomes a MarkdownString.</summary>
    public static StringOrMarkdown? ToDocumentation(LspStringOrMarkup? value) => value switch
    {
        string text => text,
        LspMarkupContent markup => markup.Kind == "markdown" ? new MarkdownString(markup.Value) : markup.Value,
        _ => null,
    };

    /// <summary>The engine's documentation in the protocol (for an active signature help sent back).</summary>
    public static LspStringOrMarkup? FromDocumentation(StringOrMarkdown? value) => value switch
    {
        { } doc => doc switch
        {
            string plain => plain,
            MarkdownString markdown => new LspMarkupContent("markdown", markdown.Value),
            _ => null,
        },
        null => null,
    };

    // ---- commands, edits ----

    public static LanguageCommand? ToCommand(LspCommand? command)
    {
        if (command is null)
        {
            return null;
        }
        return new LanguageCommand(command.Command, command.Title)
        {
            Arguments = command.Arguments?.Select(static argument => (object?)argument).ToList(),
        };
    }

    public static LspCommand FromCommand(LanguageCommand command) =>
        new(command.Title, command.Id)
        {
            Arguments = command.Arguments?.Select(static argument => argument is JsonElement element ? element : ToElement(argument)).ToArray(),
        };

    private static JsonElement ToElement(object? value) => value switch
    {
        null => NullElement,
        string s => JsonSerializer.SerializeToElement(s, s_json.String),
        bool b => JsonSerializer.SerializeToElement(b, s_json.Boolean),
        int i => JsonSerializer.SerializeToElement(i, s_json.Int32),
        double d => JsonSerializer.SerializeToElement(d, s_json.Double),
        _ => JsonSerializer.SerializeToElement(value.ToString() ?? string.Empty, s_json.String),
    };

    public static Languages.TextEdit ToTextEdit(LspTextEdit edit) => new(ToRange(edit.Range), edit.NewText);

    public static IReadOnlyList<Languages.TextEdit> ToTextEdits(LspTextEdit[]? edits) =>
        edits is null ? [] : edits.Select(ToTextEdit).ToList();

    public static SingleEditOperation ToEditOperation(LspTextEdit edit) => new(ToRange(edit.Range), edit.NewText);

    // ---- diagnostics ----

    public static MarkerSeverity ToSeverity(int? severity) => severity switch
    {
        1 => MarkerSeverity.Error,
        2 => MarkerSeverity.Warning,
        3 => MarkerSeverity.Info,
        4 => MarkerSeverity.Hint,
        // The specification leaves a missing severity to the client; upstream's language client shows an error.
        _ => MarkerSeverity.Error,
    };

    public static MarkerData ToMarkerData(LspDiagnostic diagnostic)
    {
        var range = ToRange(diagnostic.Range);
        var code = diagnostic.Code switch
        {
            string text => text,
            long number => number.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        List<MarkerTag>? tags = null;
        if (diagnostic.Tags is { Length: > 0 })
        {
            tags = new List<MarkerTag>();
            foreach (var tag in diagnostic.Tags)
            {
                if (tag is 1 or 2)
                {
                    tags.Add((MarkerTag)tag);
                }
            }
        }
        return new MarkerData
        {
            Severity = ToSeverity(diagnostic.Severity),
            Message = diagnostic.Message,
            Source = diagnostic.Source,
            Code = code,
            CodeTarget = diagnostic.CodeDescription?.Href is { } href && Uri.TryCreate(href, UriKind.Absolute, out var target) ? target : null,
            StartLineNumber = range.StartLineNumber,
            StartColumn = range.StartColumn,
            EndLineNumber = range.EndLineNumber,
            EndColumn = range.EndColumn,
            Tags = tags,
            RelatedInformation = diagnostic.RelatedInformation?.Select(static info =>
            {
                var infoRange = ToRange(info.Location.Range);
                return new RelatedInformation(ToUri(info.Location.Uri), info.Message, infoRange.StartLineNumber, infoRange.StartColumn, infoRange.EndLineNumber, infoRange.EndColumn);
            }).ToList(),
        };
    }

    // ---- workspace edits ----

    /// <summary>
    /// A protocol workspace edit as the engine's. The document changes (TextDocumentEdit | CreateFile | RenameFile |
    /// DeleteFile) win over the changes map, as the specification says.
    /// </summary>
    public static WorkspaceEdit ToWorkspaceEdit(LspWorkspaceEdit? edit)
    {
        var entries = new List<WorkspaceEditEntry>();
        if (edit is null)
        {
            return new WorkspaceEdit(entries);
        }
        if (edit.DocumentChanges is { } documentChanges)
        {
            foreach (var change in documentChanges)
            {
                switch (change)
                {
                    case LspTextDocumentEdit documentEdit:
                        var resource = ToUri(documentEdit.TextDocument.Uri);
                        foreach (var textEdit in documentEdit.Edits)
                        {
                            entries.Add(new WorkspaceTextEdit(resource, ToTextEdit(textEdit)) { VersionId = documentEdit.TextDocument.Version });
                        }
                        break;
                    case LspResourceOperation operation:
                        var options = ToFileOptions(operation.Options);
                        switch (operation.Kind)
                        {
                            case "create":
                                entries.Add(new WorkspaceFileEdit(null, operation.Uri is { } createUri ? ToUri(createUri) : null) { Options = options });
                                break;
                            case "rename":
                                entries.Add(new WorkspaceFileEdit(
                                    operation.OldUri is { } oldUri ? ToUri(oldUri) : null,
                                    operation.NewUri is { } newUri ? ToUri(newUri) : null) { Options = options });
                                break;
                            case "delete":
                                entries.Add(new WorkspaceFileEdit(operation.Uri is { } deleteUri ? ToUri(deleteUri) : null, null) { Options = options });
                                break;
                        }
                        break;
                }
            }
            return new WorkspaceEdit(entries);
        }
        if (edit.Changes is not null)
        {
            foreach (var (uri, edits) in edit.Changes)
            {
                var resource = ToUri(uri);
                foreach (var textEdit in edits)
                {
                    entries.Add(new WorkspaceTextEdit(resource, ToTextEdit(textEdit)));
                }
            }
        }
        return new WorkspaceEdit(entries);
    }

    private static WorkspaceFileEditOptions? ToFileOptions(LspResourceOperationOptions? options) =>
        options is null
            ? null
            : new WorkspaceFileEditOptions
            {
                Overwrite = options.Overwrite,
                IgnoreIfExists = options.IgnoreIfExists,
                IgnoreIfNotExists = options.IgnoreIfNotExists,
                Recursive = options.Recursive,
            };

    // ---- JSON helpers ----

    public static bool GetBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>A result that is absent or JSON null.</summary>
    public static bool IsNull(JsonElement element) => element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;

    /// <summary>Text with the line breaks the model uses (servers may send \r\n or \n).</summary>
    public static string NormalizeEol(string text, string eol)
    {
        if (text.IndexOf('\r') < 0 && eol == "\n")
        {
            return text;
        }
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                builder.Append(eol);
            }
            else if (c == '\n')
            {
                builder.Append(eol);
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
}
