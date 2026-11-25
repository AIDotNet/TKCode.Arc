// C# 15 union types for the Language Server Protocol's untagged unions (specification 3.17).
// On .NET 11+, System.Text.Json serializes and deserializes these natively through the source generator (token-based
// classification for disjoint JSON kinds, and one shared zero-allocation Utf8JsonReader classifier factory for
// object-vs-object unions).
// On .NET 8 and .NET 10, the same C# 15 union declarations compile against the internal polyfill below and use the
// AOT-safe converters in LspUnionConverters.Legacy.cs.

#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    internal sealed class UnionAttribute : Attribute;

    internal interface IUnion
    {
        object? Value { get; }
    }
}
#endif

namespace TKCode.Arc.Editor.Lsp.Protocol
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // ---- distinct JSON token kinds (STJ 11 handles these without any classifier) ----

    /// <summary>string | MarkupContent (documentation, inlay hint tooltip).</summary>
#if !NET11_0_OR_GREATER
    [JsonConverter(typeof(LspStringOrMarkupConverter))]
#endif
    internal union LspStringOrMarkup(string, LspMarkupContent);

    /// <summary>integer | string (Diagnostic.code).</summary>
#if !NET11_0_OR_GREATER
    [JsonConverter(typeof(LspDiagnosticCodeConverter))]
#endif
    internal union LspDiagnosticCode(long, string);

    /// <summary>string | [uinteger, uinteger] (ParameterInformation.label).</summary>
#if !NET11_0_OR_GREATER
    [JsonConverter(typeof(LspParameterLabelConverter))]
#endif
    internal union LspParameterLabel(string, int[]);

    /// <summary>string | InlayHintLabelPart[] (InlayHint.label).</summary>
#if !NET11_0_OR_GREATER
    [JsonConverter(typeof(LspInlayHintLabelConverter))]
#endif
    internal union LspInlayHintLabel(string, LspInlayHintLabelPart[]);

    /// <summary>CompletionItem[] | CompletionList (textDocument/completion).</summary>
#if !NET11_0_OR_GREATER
    [JsonConverter(typeof(LspCompletionResultConverter))]
#endif
    internal union LspCompletionResult(LspCompletionItem[], LspCompletionList);

    // ---- object-vs-object unions (STJ 11 uses a single shared Utf8JsonReader classifier factory) ----

    /// <summary>string | MarkupContent | MarkedString { language, value } (one element of hover contents).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspHoverItemConverter))]
#endif
    internal union LspHoverItem(string, LspMarkupContent, LspMarkedStringObject);

    /// <summary>MarkupContent | MarkedString | MarkedString[] (Hover.contents).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspHoverContentsConverter))]
#endif
    internal union LspHoverContents(string, LspMarkupContent, LspMarkedStringObject, LspHoverItem[]);

    /// <summary>TextEdit | InsertReplaceEdit (CompletionItem.textEdit).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspCompletionTextEditConverter))]
#endif
    internal union LspCompletionTextEdit(LspTextEdit, LspInsertReplaceEdit);

    /// <summary>Range | { insert, replace } (CompletionItemDefaults.editRange).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspEditRangeConverter))]
#endif
    internal union LspEditRange(LspRange, LspInsertReplaceRange);

    /// <summary>TextDocumentEdit | CreateFile | RenameFile | DeleteFile (WorkspaceEdit.documentChanges[]).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspDocumentChangeConverter))]
#endif
    internal union LspDocumentChange(LspTextDocumentEdit, LspResourceOperation);

    /// <summary>Location | LocationLink (one element of definition / declaration / implementation / typeDefinition).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspLocationOrLinkConverter))]
#endif
    internal union LspLocationOrLink(LspLocation, LspLocationLink);

    /// <summary>Location | Location[] | LocationLink[] (navigation result).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspLocationResultConverter))]
#endif
    internal union LspLocationResult(LspLocation, LspLocationLink, LspLocationOrLink[]);

    /// <summary>DocumentSymbol | SymbolInformation (textDocument/documentSymbol[]).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspDocumentSymbolItemConverter))]
#endif
    internal union LspDocumentSymbolItem(LspDocumentSymbol, LspSymbolInformation);

    /// <summary>Range | { range, placeholder } | { defaultBehavior } (textDocument/prepareRename).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspPrepareRenameResultConverter))]
#endif
    internal union LspPrepareRenameResult(LspRange, LspPrepareRenamePlaceholder, LspPrepareRenameDefaultBehavior);

    /// <summary>SemanticTokens | SemanticTokensDelta (textDocument/semanticTokens/full/delta).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspSemanticTokensResultConverter))]
#endif
    internal union LspSemanticTokensResult(LspSemanticTokens, LspSemanticTokensDelta);

    /// <summary>CodeAction | Command (textDocument/codeAction[]).</summary>
#if NET11_0_OR_GREATER
    [JsonUnion(TypeClassifier = typeof(LspUnionClassifier))]
#else
    [JsonConverter(typeof(LspCodeActionOrCommandConverter))]
#endif
    internal union LspCodeActionOrCommand(LspCodeAction, LspCommand);

#if NET11_0_OR_GREATER
    /// <summary>
    /// Single zero-allocation <see cref="Utf8JsonReader"/> classifier factory shared by all object-vs-object LSP union
    /// types. <see cref="CreateJsonClassifier"/> dispatches once per union type when the contract metadata is built.
    /// </summary>
    internal sealed class LspUnionClassifier : JsonTypeClassifierFactory
    {
        public override bool CanClassify(JsonTypeClassifierContext context) =>
            context.DeclaringType == typeof(LspHoverItem)
            || context.DeclaringType == typeof(LspHoverContents)
            || context.DeclaringType == typeof(LspCompletionTextEdit)
            || context.DeclaringType == typeof(LspEditRange)
            || context.DeclaringType == typeof(LspDocumentChange)
            || context.DeclaringType == typeof(LspLocationOrLink)
            || context.DeclaringType == typeof(LspLocationResult)
            || context.DeclaringType == typeof(LspDocumentSymbolItem)
            || context.DeclaringType == typeof(LspPrepareRenameResult)
            || context.DeclaringType == typeof(LspSemanticTokensResult)
            || context.DeclaringType == typeof(LspCodeActionOrCommand);

        public override JsonTypeClassifier CreateJsonClassifier(JsonTypeClassifierContext context, JsonSerializerOptions options)
        {
            var type = context.DeclaringType;
            if (type == typeof(LspHoverItem))
            {
                return ClassifyHoverItem;
            }
            if (type == typeof(LspHoverContents))
            {
                return ClassifyHoverContents;
            }
            if (type == typeof(LspCompletionTextEdit))
            {
                return ClassifyCompletionTextEdit;
            }
            if (type == typeof(LspEditRange))
            {
                return ClassifyEditRange;
            }
            if (type == typeof(LspDocumentChange))
            {
                return ClassifyDocumentChange;
            }
            if (type == typeof(LspLocationOrLink))
            {
                return ClassifyLocationOrLink;
            }
            if (type == typeof(LspLocationResult))
            {
                return ClassifyLocationResult;
            }
            if (type == typeof(LspDocumentSymbolItem))
            {
                return ClassifyDocumentSymbolItem;
            }
            if (type == typeof(LspPrepareRenameResult))
            {
                return ClassifyPrepareRename;
            }
            if (type == typeof(LspSemanticTokensResult))
            {
                return ClassifySemanticTokens;
            }
            if (type == typeof(LspCodeActionOrCommand))
            {
                return ClassifyCodeAction;
            }
            throw new NotSupportedException($"Unsupported union type: {type}");
        }

        private static bool HasProperty(ref Utf8JsonReader reader, ReadOnlySpan<byte> propertyName)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }
                if (reader.ValueTextEquals(propertyName))
                {
                    return true;
                }
                if (!reader.Read())
                {
                    break;
                }
                reader.Skip();
            }
            return false;
        }

        private static Type? ClassifyHoverItem(ref Utf8JsonReader reader) => reader.TokenType switch
        {
            JsonTokenType.String => typeof(string),
            JsonTokenType.StartObject => HasProperty(ref reader, "kind"u8) ? typeof(LspMarkupContent) : typeof(LspMarkedStringObject),
            _ => null,
        };

        private static Type? ClassifyHoverContents(ref Utf8JsonReader reader) => reader.TokenType switch
        {
            JsonTokenType.String => typeof(string),
            JsonTokenType.StartArray => typeof(LspHoverItem[]),
            JsonTokenType.StartObject => HasProperty(ref reader, "kind"u8) ? typeof(LspMarkupContent) : typeof(LspMarkedStringObject),
            _ => null,
        };

        private static Type? ClassifyCompletionTextEdit(ref Utf8JsonReader reader) =>
            reader.TokenType == JsonTokenType.StartObject
                ? (HasProperty(ref reader, "insert"u8) ? typeof(LspInsertReplaceEdit) : typeof(LspTextEdit))
                : null;

        private static Type? ClassifyEditRange(ref Utf8JsonReader reader) =>
            reader.TokenType == JsonTokenType.StartObject
                ? (HasProperty(ref reader, "insert"u8) ? typeof(LspInsertReplaceRange) : typeof(LspRange))
                : null;

        private static Type? ClassifyDocumentChange(ref Utf8JsonReader reader) =>
            reader.TokenType == JsonTokenType.StartObject
                ? (HasProperty(ref reader, "kind"u8) ? typeof(LspResourceOperation) : typeof(LspTextDocumentEdit))
                : null;

        private static Type? ClassifyLocationOrLink(ref Utf8JsonReader reader) =>
            reader.TokenType == JsonTokenType.StartObject
                ? (HasProperty(ref reader, "targetUri"u8) ? typeof(LspLocationLink) : typeof(LspLocation))
                : null;

        private static Type? ClassifyLocationResult(ref Utf8JsonReader reader) => reader.TokenType switch
        {
            JsonTokenType.StartArray => typeof(LspLocationOrLink[]),
            JsonTokenType.StartObject => HasProperty(ref reader, "targetUri"u8) ? typeof(LspLocationLink) : typeof(LspLocation),
            _ => null,
        };

        private static Type? ClassifyDocumentSymbolItem(ref Utf8JsonReader reader) =>
            reader.TokenType == JsonTokenType.StartObject
                ? (HasProperty(ref reader, "location"u8) ? typeof(LspSymbolInformation) : typeof(LspDocumentSymbol))
                : null;

        private static Type? ClassifyPrepareRename(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }
                if (reader.ValueTextEquals("defaultBehavior"u8))
                {
                    return typeof(LspPrepareRenameDefaultBehavior);
                }
                if (reader.ValueTextEquals("range"u8))
                {
                    return typeof(LspPrepareRenamePlaceholder);
                }
                if (reader.ValueTextEquals("start"u8))
                {
                    return typeof(LspRange);
                }
                if (!reader.Read())
                {
                    break;
                }
                reader.Skip();
            }
            return null;
        }

        private static Type? ClassifySemanticTokens(ref Utf8JsonReader reader) =>
            reader.TokenType == JsonTokenType.StartObject
                ? (HasProperty(ref reader, "edits"u8) ? typeof(LspSemanticTokensDelta) : typeof(LspSemanticTokens))
                : null;

        private static Type? ClassifyCodeAction(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }
                var isCommand = reader.ValueTextEquals("command"u8);
                if (!reader.Read())
                {
                    break;
                }
                if (isCommand)
                {
                    return reader.TokenType == JsonTokenType.String ? typeof(LspCommand) : typeof(LspCodeAction);
                }
                reader.Skip();
            }
            return typeof(LspCodeAction);
        }
    }
#endif
}
