// What this client tells a server it can do (ClientCapabilities), and what it reads from the server's answer
// (ServerCapabilities), specification 3.17. Written for this package.

using System.Buffers;
using System.Text.Json;

namespace TKCode.Arc.Editor.Lsp.Protocol;

/// <summary>The client capabilities this client sends in <c>initialize</c>.</summary>
internal static class LspClientCapabilities
{
    /// <summary>The capabilities as JSON (built once; a JsonElement of its own document is immutable).</summary>
    public static JsonElement Value { get; } = Build();

    private static JsonElement Build()
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();

            w.WriteStartObject("general");
            WriteStrings(w, "positionEncodings", "utf-16");
            w.WriteEndObject();

            w.WriteStartObject("window");
            w.WriteStartObject("showMessage");
            w.WriteStartObject("messageActionItem");
            w.WriteBoolean("additionalPropertiesSupport", false);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteBoolean("workDoneProgress", false);
            w.WriteEndObject();

            w.WriteStartObject("workspace");
            w.WriteBoolean("applyEdit", true);
            w.WriteStartObject("workspaceEdit");
            w.WriteBoolean("documentChanges", true);
            WriteStrings(w, "resourceOperations");
            w.WriteString("failureHandling", "abort");
            w.WriteBoolean("normalizesLineEndings", true);
            w.WriteEndObject();
            w.WriteBoolean("workspaceFolders", true);
            w.WriteBoolean("configuration", true);
            WriteRefresh(w, "semanticTokens");
            WriteRefresh(w, "inlayHint");
            WriteRefresh(w, "codeLens");
            WriteRefresh(w, "foldingRange");
            w.WriteStartObject("executeCommand");
            w.WriteBoolean("dynamicRegistration", false);
            w.WriteEndObject();
            w.WriteEndObject();

            w.WriteStartObject("textDocument");
            w.WriteStartObject("synchronization");
            w.WriteBoolean("dynamicRegistration", false);
            w.WriteBoolean("willSave", false);
            w.WriteBoolean("willSaveWaitUntil", false);
            w.WriteBoolean("didSave", false);
            w.WriteEndObject();

            w.WriteStartObject("publishDiagnostics");
            w.WriteBoolean("relatedInformation", true);
            WriteValueSet(w, "tagSupport", 1, 2);
            w.WriteBoolean("versionSupport", true);
            w.WriteBoolean("codeDescriptionSupport", true);
            w.WriteBoolean("dataSupport", true);
            w.WriteEndObject();

            w.WriteStartObject("completion");
            w.WriteStartObject("completionItem");
            w.WriteBoolean("snippetSupport", true);
            w.WriteBoolean("commitCharactersSupport", true);
            WriteStrings(w, "documentationFormat", "markdown", "plaintext");
            w.WriteBoolean("deprecatedSupport", true);
            w.WriteBoolean("preselectSupport", true);
            WriteValueSet(w, "tagSupport", 1);
            w.WriteBoolean("insertReplaceSupport", true);
            w.WriteStartObject("resolveSupport");
            WriteStrings(w, "properties", "documentation", "detail", "additionalTextEdits");
            w.WriteEndObject();
            w.WriteBoolean("labelDetailsSupport", true);
            w.WriteEndObject();
            WriteValueSet(w, "completionItemKind", Range(1, 25));
            w.WriteBoolean("contextSupport", true);
            w.WriteStartObject("completionList");
            WriteStrings(w, "itemDefaults", "commitCharacters", "editRange", "insertTextFormat", "data");
            w.WriteEndObject();
            w.WriteEndObject();

            w.WriteStartObject("hover");
            WriteStrings(w, "contentFormat", "markdown", "plaintext");
            w.WriteEndObject();

            w.WriteStartObject("signatureHelp");
            w.WriteStartObject("signatureInformation");
            WriteStrings(w, "documentationFormat", "markdown", "plaintext");
            w.WriteStartObject("parameterInformation");
            w.WriteBoolean("labelOffsetSupport", true);
            w.WriteEndObject();
            w.WriteBoolean("activeParameterSupport", true);
            w.WriteEndObject();
            w.WriteBoolean("contextSupport", true);
            w.WriteEndObject();

            foreach (var name in new[] { "declaration", "definition", "typeDefinition", "implementation" })
            {
                w.WriteStartObject(name);
                w.WriteBoolean("linkSupport", true);
                w.WriteEndObject();
            }
            WriteEmpty(w, "references");
            WriteEmpty(w, "documentHighlight");

            w.WriteStartObject("documentSymbol");
            WriteValueSet(w, "symbolKind", Range(1, 26));
            w.WriteBoolean("hierarchicalDocumentSymbolSupport", true);
            WriteValueSet(w, "tagSupport", 1);
            w.WriteEndObject();

            w.WriteStartObject("codeAction");
            w.WriteStartObject("codeActionLiteralSupport");
            w.WriteStartObject("codeActionKind");
            WriteStrings(w, "valueSet", "", "quickfix", "refactor", "refactor.extract", "refactor.inline", "refactor.rewrite", "source", "source.organizeImports", "source.fixAll");
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteBoolean("isPreferredSupport", true);
            w.WriteBoolean("disabledSupport", true);
            w.WriteBoolean("dataSupport", true);
            w.WriteStartObject("resolveSupport");
            WriteStrings(w, "properties", "edit");
            w.WriteEndObject();
            w.WriteEndObject();

            WriteEmpty(w, "codeLens");
            WriteEmpty(w, "formatting");
            w.WriteStartObject("rangeFormatting");
            w.WriteBoolean("rangesSupport", true);
            w.WriteEndObject();
            WriteEmpty(w, "onTypeFormatting");

            w.WriteStartObject("rename");
            w.WriteBoolean("prepareSupport", true);
            w.WriteNumber("prepareSupportDefaultBehavior", 1);
            w.WriteEndObject();

            w.WriteStartObject("foldingRange");
            w.WriteBoolean("lineFoldingOnly", true);
            w.WriteStartObject("foldingRangeKind");
            WriteStrings(w, "valueSet", "comment", "imports", "region");
            w.WriteEndObject();
            w.WriteEndObject();

            WriteEmpty(w, "selectionRange");
            WriteEmpty(w, "linkedEditingRange");
            WriteEmpty(w, "colorProvider");

            w.WriteStartObject("semanticTokens");
            w.WriteStartObject("requests");
            w.WriteBoolean("range", true);
            w.WriteStartObject("full");
            w.WriteBoolean("delta", true);
            w.WriteEndObject();
            w.WriteEndObject();
            WriteStrings(w, "tokenTypes", LspSemanticTokenTypes.Types);
            WriteStrings(w, "tokenModifiers", LspSemanticTokenTypes.Modifiers);
            WriteStrings(w, "formats", "relative");
            w.WriteBoolean("overlappingTokenSupport", false);
            w.WriteBoolean("multilineTokenSupport", false);
            w.WriteEndObject();

            w.WriteStartObject("inlayHint");
            w.WriteStartObject("resolveSupport");
            WriteStrings(w, "properties", "tooltip", "textEdits", "label.tooltip", "label.location", "label.command");
            w.WriteEndObject();
            w.WriteEndObject();

            w.WriteEndObject(); // textDocument
            w.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static int[] Range(int first, int last) => Enumerable.Range(first, last - first + 1).ToArray();

    private static void WriteStrings(Utf8JsonWriter w, string name, params string[] values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }
        w.WriteEndArray();
    }

    private static void WriteValueSet(Utf8JsonWriter w, string name, params int[] values)
    {
        w.WriteStartObject(name);
        w.WriteStartArray("valueSet");
        foreach (var value in values)
        {
            w.WriteNumberValue(value);
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteRefresh(Utf8JsonWriter w, string name)
    {
        w.WriteStartObject(name);
        w.WriteBoolean("refreshSupport", true);
        w.WriteEndObject();
    }

    private static void WriteEmpty(Utf8JsonWriter w, string name)
    {
        w.WriteStartObject(name);
        w.WriteBoolean("dynamicRegistration", false);
        w.WriteEndObject();
    }
}

/// <summary>The semantic token types and modifiers the specification predefines.</summary>
internal static class LspSemanticTokenTypes
{
    public static readonly string[] Types =
    [
        "namespace", "type", "class", "enum", "interface", "struct", "typeParameter", "parameter", "variable", "property",
        "enumMember", "event", "function", "method", "macro", "keyword", "modifier", "comment", "string", "number", "regexp",
        "operator", "decorator",
    ];

    public static readonly string[] Modifiers =
    [
        "declaration", "definition", "readonly", "static", "deprecated", "abstract", "async", "modification", "documentation",
        "defaultLibrary",
    ];
}

/// <summary>How the server wants documents synchronized (TextDocumentSyncKind).</summary>
internal enum LspTextDocumentSyncKind
{
    None = 0,
    Full = 1,
    Incremental = 2,
}

/// <summary>The server capabilities this client acts on, read from the <c>initialize</c> result.</summary>
internal sealed class LspServerCapabilities
{
    public LspServerCapabilities(JsonElement capabilities)
    {
        Raw = capabilities.ValueKind == JsonValueKind.Object ? capabilities : default;
        var raw = Raw;

        // textDocumentSync: TextDocumentSyncKind | TextDocumentSyncOptions
        if (TryGet(raw, "textDocumentSync", out var sync))
        {
            if (sync.ValueKind == JsonValueKind.Number && sync.TryGetInt32(out var kind))
            {
                SyncKind = (LspTextDocumentSyncKind)kind;
                OpenClose = SyncKind != LspTextDocumentSyncKind.None;
            }
            else if (sync.ValueKind == JsonValueKind.Object)
            {
                OpenClose = Bool(sync, "openClose");
                SyncKind = TryGet(sync, "change", out var change) && change.TryGetInt32(out var changeKind)
                    ? (LspTextDocumentSyncKind)changeKind
                    : LspTextDocumentSyncKind.None;
            }
        }

        if (Provider(raw, "completionProvider", out var completion))
        {
            Completion = true;
            CompletionTriggerCharacters = Strings(completion, "triggerCharacters");
            CompletionResolve = Bool(completion, "resolveProvider");
        }
        Hover = Provider(raw, "hoverProvider", out _);
        if (Provider(raw, "signatureHelpProvider", out var signatureHelp))
        {
            SignatureHelp = true;
            SignatureHelpTriggerCharacters = Strings(signatureHelp, "triggerCharacters");
            SignatureHelpRetriggerCharacters = Strings(signatureHelp, "retriggerCharacters");
        }
        Declaration = Provider(raw, "declarationProvider", out _);
        Definition = Provider(raw, "definitionProvider", out _);
        TypeDefinition = Provider(raw, "typeDefinitionProvider", out _);
        Implementation = Provider(raw, "implementationProvider", out _);
        References = Provider(raw, "referencesProvider", out _);
        DocumentHighlight = Provider(raw, "documentHighlightProvider", out _);
        DocumentSymbol = Provider(raw, "documentSymbolProvider", out _);
        if (Provider(raw, "codeActionProvider", out var codeAction))
        {
            CodeAction = true;
            CodeActionKinds = Strings(codeAction, "codeActionKinds");
            CodeActionResolve = Bool(codeAction, "resolveProvider");
        }
        if (Provider(raw, "codeLensProvider", out var codeLens))
        {
            CodeLens = true;
            CodeLensResolve = Bool(codeLens, "resolveProvider");
        }
        DocumentFormatting = Provider(raw, "documentFormattingProvider", out _);
        if (Provider(raw, "documentRangeFormattingProvider", out var rangeFormatting))
        {
            DocumentRangeFormatting = true;
            DocumentRangesFormatting = Bool(rangeFormatting, "rangesSupport");
        }
        if (TryGet(raw, "documentOnTypeFormattingProvider", out var onType) && onType.ValueKind == JsonValueKind.Object
            && TryGet(onType, "firstTriggerCharacter", out var first) && first.ValueKind == JsonValueKind.String)
        {
            var characters = new List<string> { first.GetString()! };
            characters.AddRange(Strings(onType, "moreTriggerCharacter") ?? []);
            OnTypeFormattingTriggerCharacters = characters;
        }
        if (Provider(raw, "renameProvider", out var rename))
        {
            Rename = true;
            RenamePrepare = Bool(rename, "prepareProvider");
        }
        FoldingRange = Provider(raw, "foldingRangeProvider", out _);
        SelectionRange = Provider(raw, "selectionRangeProvider", out _);
        LinkedEditingRange = Provider(raw, "linkedEditingRangeProvider", out _);
        Color = Provider(raw, "colorProvider", out _);
        if (Provider(raw, "inlayHintProvider", out var inlayHint))
        {
            InlayHint = true;
            InlayHintResolve = Bool(inlayHint, "resolveProvider");
        }
        if (TryGet(raw, "semanticTokensProvider", out var semantic) && semantic.ValueKind == JsonValueKind.Object
            && TryGet(semantic, "legend", out var legend) && legend.ValueKind == JsonValueKind.Object)
        {
            SemanticTokensLegend = (Strings(legend, "tokenTypes") ?? [], Strings(legend, "tokenModifiers") ?? []);
            SemanticTokensRange = Provider(semantic, "range", out _);
            if (Provider(semantic, "full", out var full))
            {
                SemanticTokensFull = true;
                SemanticTokensDelta = Bool(full, "delta");
            }
        }
        if (TryGet(raw, "executeCommandProvider", out var executeCommand) && executeCommand.ValueKind == JsonValueKind.Object)
        {
            ExecuteCommands = Strings(executeCommand, "commands") ?? [];
        }
        if (TryGet(raw, "positionEncoding", out var encoding) && encoding.ValueKind == JsonValueKind.String)
        {
            PositionEncoding = encoding.GetString()!;
        }
    }

    public JsonElement Raw { get; }

    public string PositionEncoding { get; } = "utf-16";

    public bool OpenClose { get; }

    public LspTextDocumentSyncKind SyncKind { get; }

    public bool Completion { get; }

    public IReadOnlyList<string>? CompletionTriggerCharacters { get; }

    public bool CompletionResolve { get; }

    public bool Hover { get; }

    public bool SignatureHelp { get; }

    public IReadOnlyList<string>? SignatureHelpTriggerCharacters { get; }

    public IReadOnlyList<string>? SignatureHelpRetriggerCharacters { get; }

    public bool Declaration { get; }

    public bool Definition { get; }

    public bool TypeDefinition { get; }

    public bool Implementation { get; }

    public bool References { get; }

    public bool DocumentHighlight { get; }

    public bool DocumentSymbol { get; }

    public bool CodeAction { get; }

    public IReadOnlyList<string>? CodeActionKinds { get; }

    public bool CodeActionResolve { get; }

    public bool CodeLens { get; }

    public bool CodeLensResolve { get; }

    public bool DocumentFormatting { get; }

    public bool DocumentRangeFormatting { get; }

    public bool DocumentRangesFormatting { get; }

    public IReadOnlyList<string>? OnTypeFormattingTriggerCharacters { get; }

    public bool Rename { get; }

    public bool RenamePrepare { get; }

    public bool FoldingRange { get; }

    public bool SelectionRange { get; }

    public bool LinkedEditingRange { get; }

    public bool Color { get; }

    public bool InlayHint { get; }

    public bool InlayHintResolve { get; }

    public (IReadOnlyList<string> TokenTypes, IReadOnlyList<string> TokenModifiers)? SemanticTokensLegend { get; }

    public bool SemanticTokensFull { get; }

    public bool SemanticTokensDelta { get; }

    public bool SemanticTokensRange { get; }

    public IReadOnlyList<string> ExecuteCommands { get; } = [];

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }
        value = default;
        return false;
    }

    // boolean | XxxOptions | XxxRegistrationOptions: true or an object announces the feature.
    private static bool Provider(JsonElement element, string name, out JsonElement options)
    {
        options = default;
        if (!TryGet(element, name, out var value))
        {
            return false;
        }
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.Object:
                options = value;
                return true;
            default:
                return false;
        }
    }

    private static bool Bool(JsonElement element, string name) => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string>? Strings(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString()!);
            }
        }
        return list;
    }
}
