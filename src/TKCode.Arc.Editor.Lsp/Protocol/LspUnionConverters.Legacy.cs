// AOT-safe Utf8JsonReader converters for the protocol's C# 15 union types on .NET 8 and .NET 10 (where inbox
// System.Text.Json predates native union support). Classification peeks a stack copy of Utf8JsonReader, avoiding
// intermediate JsonDocument allocations. On .NET 11+ this file compiles to nothing.

#if !NET11_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKCode.Arc.Editor.Lsp.Protocol;

internal static class LspUnionReaderHelpers
{
    public static bool HasProperty(Utf8JsonReader peek, ReadOnlySpan<byte> propertyName)
    {
        if (peek.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }
        while (peek.Read() && peek.TokenType != JsonTokenType.EndObject)
        {
            if (peek.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }
            if (peek.ValueTextEquals(propertyName))
            {
                return true;
            }
            if (!peek.Read())
            {
                break;
            }
            peek.Skip();
        }
        return false;
    }

    public static JsonTokenType PropertyTokenType(Utf8JsonReader peek, ReadOnlySpan<byte> propertyName)
    {
        if (peek.TokenType != JsonTokenType.StartObject)
        {
            return JsonTokenType.None;
        }
        while (peek.Read() && peek.TokenType != JsonTokenType.EndObject)
        {
            if (peek.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }
            var match = peek.ValueTextEquals(propertyName);
            if (!peek.Read())
            {
                break;
            }
            if (match)
            {
                return peek.TokenType;
            }
            peek.Skip();
        }
        return JsonTokenType.None;
    }
}

internal sealed class LspStringOrMarkupConverter : JsonConverter<LspStringOrMarkup>
{
    public override LspStringOrMarkup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.StartObject => JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspMarkupContent)!,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for LspStringOrMarkup."),
        };

    public override void Write(Utf8JsonWriter writer, LspStringOrMarkup value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case LspMarkupContent markup:
                JsonSerializer.Serialize(writer, markup, LspJsonContext.Default.LspMarkupContent);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspDiagnosticCodeConverter : JsonConverter<LspDiagnosticCode>
{
    public override LspDiagnosticCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => reader.GetString()!,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for LspDiagnosticCode."),
        };

    public override void Write(Utf8JsonWriter writer, LspDiagnosticCode value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case long number:
                writer.WriteNumberValue(number);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspParameterLabelConverter : JsonConverter<LspParameterLabel>
{
    public override LspParameterLabel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.StartArray => JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.Int32Array)!,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for LspParameterLabel."),
        };

    public override void Write(Utf8JsonWriter writer, LspParameterLabel value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case int[] offsets:
                JsonSerializer.Serialize(writer, offsets, LspJsonContext.Default.Int32Array);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspInlayHintLabelConverter : JsonConverter<LspInlayHintLabel>
{
    public override LspInlayHintLabel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.StartArray => JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspInlayHintLabelPartArray)!,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for LspInlayHintLabel."),
        };

    public override void Write(Utf8JsonWriter writer, LspInlayHintLabel value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case LspInlayHintLabelPart[] parts:
                JsonSerializer.Serialize(writer, parts, LspJsonContext.Default.LspInlayHintLabelPartArray);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspCompletionResultConverter : JsonConverter<LspCompletionResult>
{
    public override LspCompletionResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.StartArray => JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspCompletionItemArray)!,
            JsonTokenType.StartObject => JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspCompletionList)!,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for LspCompletionResult."),
        };

    public override void Write(Utf8JsonWriter writer, LspCompletionResult value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspCompletionItem[] items:
                JsonSerializer.Serialize(writer, items, LspJsonContext.Default.LspCompletionItemArray);
                break;
            case LspCompletionList list:
                JsonSerializer.Serialize(writer, list, LspJsonContext.Default.LspCompletionList);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspHoverItemConverter : JsonConverter<LspHoverItem>
{
    public override LspHoverItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString()!;
        }
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return LspUnionReaderHelpers.HasProperty(reader, "kind"u8)
                ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspMarkupContent)!
                : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspMarkedStringObject)!;
        }
        throw new JsonException($"Unexpected token {reader.TokenType} for LspHoverItem.");
    }

    public override void Write(Utf8JsonWriter writer, LspHoverItem value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case LspMarkupContent markup:
                JsonSerializer.Serialize(writer, markup, LspJsonContext.Default.LspMarkupContent);
                break;
            case LspMarkedStringObject marked:
                JsonSerializer.Serialize(writer, marked, LspJsonContext.Default.LspMarkedStringObject);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspHoverContentsConverter : JsonConverter<LspHoverContents>
{
    public override LspHoverContents Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.StartArray => JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspHoverItemArray)!,
            JsonTokenType.StartObject => LspUnionReaderHelpers.HasProperty(reader, "kind"u8)
                ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspMarkupContent)!
                : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspMarkedStringObject)!,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for LspHoverContents."),
        };
    }

    public override void Write(Utf8JsonWriter writer, LspHoverContents value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case LspMarkupContent markup:
                JsonSerializer.Serialize(writer, markup, LspJsonContext.Default.LspMarkupContent);
                break;
            case LspMarkedStringObject marked:
                JsonSerializer.Serialize(writer, marked, LspJsonContext.Default.LspMarkedStringObject);
                break;
            case LspHoverItem[] items:
                JsonSerializer.Serialize(writer, items, LspJsonContext.Default.LspHoverItemArray);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspCompletionTextEditConverter : JsonConverter<LspCompletionTextEdit>
{
    public override LspCompletionTextEdit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.HasProperty(reader, "insert"u8)
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspInsertReplaceEdit)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspTextEdit)!;

    public override void Write(Utf8JsonWriter writer, LspCompletionTextEdit value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspTextEdit edit:
                JsonSerializer.Serialize(writer, edit, LspJsonContext.Default.LspTextEdit);
                break;
            case LspInsertReplaceEdit edit:
                JsonSerializer.Serialize(writer, edit, LspJsonContext.Default.LspInsertReplaceEdit);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspEditRangeConverter : JsonConverter<LspEditRange>
{
    public override LspEditRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.HasProperty(reader, "insert"u8)
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspInsertReplaceRange)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspRange)!;

    public override void Write(Utf8JsonWriter writer, LspEditRange value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspRange range:
                JsonSerializer.Serialize(writer, range, LspJsonContext.Default.LspRange);
                break;
            case LspInsertReplaceRange pair:
                JsonSerializer.Serialize(writer, pair, LspJsonContext.Default.LspInsertReplaceRange);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspDocumentChangeConverter : JsonConverter<LspDocumentChange>
{
    public override LspDocumentChange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.HasProperty(reader, "kind"u8)
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspResourceOperation)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspTextDocumentEdit)!;

    public override void Write(Utf8JsonWriter writer, LspDocumentChange value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspTextDocumentEdit edit:
                JsonSerializer.Serialize(writer, edit, LspJsonContext.Default.LspTextDocumentEdit);
                break;
            case LspResourceOperation operation:
                JsonSerializer.Serialize(writer, operation, LspJsonContext.Default.LspResourceOperation);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspLocationOrLinkConverter : JsonConverter<LspLocationOrLink>
{
    public override LspLocationOrLink Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.HasProperty(reader, "targetUri"u8)
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspLocationLink)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspLocation)!;

    public override void Write(Utf8JsonWriter writer, LspLocationOrLink value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspLocation location:
                JsonSerializer.Serialize(writer, location, LspJsonContext.Default.LspLocation);
                break;
            case LspLocationLink link:
                JsonSerializer.Serialize(writer, link, LspJsonContext.Default.LspLocationLink);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspLocationResultConverter : JsonConverter<LspLocationResult>
{
    public override LspLocationResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspLocationOrLinkArray)!;
        }
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return LspUnionReaderHelpers.HasProperty(reader, "targetUri"u8)
                ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspLocationLink)!
                : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspLocation)!;
        }
        throw new JsonException($"Unexpected token {reader.TokenType} for LspLocationResult.");
    }

    public override void Write(Utf8JsonWriter writer, LspLocationResult value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspLocation location:
                JsonSerializer.Serialize(writer, location, LspJsonContext.Default.LspLocation);
                break;
            case LspLocationLink link:
                JsonSerializer.Serialize(writer, link, LspJsonContext.Default.LspLocationLink);
                break;
            case LspLocationOrLink[] items:
                JsonSerializer.Serialize(writer, items, LspJsonContext.Default.LspLocationOrLinkArray);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspDocumentSymbolItemConverter : JsonConverter<LspDocumentSymbolItem>
{
    public override LspDocumentSymbolItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.HasProperty(reader, "location"u8)
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspSymbolInformation)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspDocumentSymbol)!;

    public override void Write(Utf8JsonWriter writer, LspDocumentSymbolItem value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspDocumentSymbol symbol:
                JsonSerializer.Serialize(writer, symbol, LspJsonContext.Default.LspDocumentSymbol);
                break;
            case LspSymbolInformation info:
                JsonSerializer.Serialize(writer, info, LspJsonContext.Default.LspSymbolInformation);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspCodeActionOrCommandConverter : JsonConverter<LspCodeActionOrCommand>
{
    public override LspCodeActionOrCommand Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.PropertyTokenType(reader, "command"u8) == JsonTokenType.String
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspCommand)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspCodeAction)!;

    public override void Write(Utf8JsonWriter writer, LspCodeActionOrCommand value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspCodeAction action:
                JsonSerializer.Serialize(writer, action, LspJsonContext.Default.LspCodeAction);
                break;
            case LspCommand command:
                JsonSerializer.Serialize(writer, command, LspJsonContext.Default.LspCommand);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspPrepareRenameResultConverter : JsonConverter<LspPrepareRenameResult>
{
    public override LspPrepareRenameResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (LspUnionReaderHelpers.HasProperty(reader, "defaultBehavior"u8))
        {
            return JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspPrepareRenameDefaultBehavior)!;
        }
        if (LspUnionReaderHelpers.HasProperty(reader, "range"u8))
        {
            return JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspPrepareRenamePlaceholder)!;
        }
        return JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspRange)!;
    }

    public override void Write(Utf8JsonWriter writer, LspPrepareRenameResult value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspRange range:
                JsonSerializer.Serialize(writer, range, LspJsonContext.Default.LspRange);
                break;
            case LspPrepareRenamePlaceholder placeholder:
                JsonSerializer.Serialize(writer, placeholder, LspJsonContext.Default.LspPrepareRenamePlaceholder);
                break;
            case LspPrepareRenameDefaultBehavior behavior:
                JsonSerializer.Serialize(writer, behavior, LspJsonContext.Default.LspPrepareRenameDefaultBehavior);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}

internal sealed class LspSemanticTokensResultConverter : JsonConverter<LspSemanticTokensResult>
{
    public override LspSemanticTokensResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        LspUnionReaderHelpers.HasProperty(reader, "edits"u8)
            ? JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspSemanticTokensDelta)!
            : JsonSerializer.Deserialize(ref reader, LspJsonContext.Default.LspSemanticTokens)!;

    public override void Write(Utf8JsonWriter writer, LspSemanticTokensResult value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case LspSemanticTokens tokens:
                JsonSerializer.Serialize(writer, tokens, LspJsonContext.Default.LspSemanticTokens);
                break;
            case LspSemanticTokensDelta delta:
                JsonSerializer.Serialize(writer, delta, LspJsonContext.Default.LspSemanticTokensDelta);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
#endif
