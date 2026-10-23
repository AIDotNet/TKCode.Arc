using System.Text.Json;

namespace TKCode.Arc;

public sealed partial class ExtractionSchema
{
    /// <summary>The JSON Schema dialect written by <see cref="ToJsonSchema"/>.</summary>
    internal const string JsonSchemaDialect = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>The pattern an IBAN in the output matches (no spaces).</summary>
    internal const string IbanPattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$";

    /// <summary>The pattern a currency code in the output matches.</summary>
    internal const string CurrencyPattern = "^[A-Z]{3}$";

    /// <summary>
    /// The output format to send to the model, as JSON Schema (draft 2020-12) text: an object with every field required
    /// and no other properties; each field is its type or null (<c>"type": ["string", "null"]</c>), a date has
    /// <c>"format": "date"</c>, an amount <c>"multipleOf": 0.01</c>, a currency and an IBAN a pattern, a table is an array of
    /// objects with its columns. The title is <see cref="Title"/> (or <see cref="Name"/> without one) and descriptions come
    /// from <see cref="Description"/> and each field's.
    /// </summary>
    /// <param name="modelId">The model the schema is sent to; when given it is written as the schema's <c>$comment</c>.</param>
    public string ToJsonSchema(string? modelId = null) => ExtractionJson.Write(true, writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("$schema", JsonSchemaDialect);
        if (!string.IsNullOrEmpty(modelId))
        {
            writer.WriteString("$comment", modelId);
        }
        var title = string.IsNullOrEmpty(Title) ? Name : Title;
        if (!string.IsNullOrEmpty(title))
        {
            writer.WriteString("title", title);
        }
        if (!string.IsNullOrEmpty(Description))
        {
            writer.WriteString("description", Description);
        }
        writer.WriteString("type", "object");
        writer.WriteBoolean("additionalProperties", false);
        writer.WriteStartArray("required");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (seen.Add(field.Key))
            {
                writer.WriteStringValue(field.Key);
            }
        }
        writer.WriteEndArray();
        writer.WriteStartObject("properties");
        seen.Clear();
        foreach (var field in Fields)
        {
            // A repeated key would make the object invalid JSON Schema; the first field with it wins (as in the form).
            if (!seen.Add(field.Key))
            {
                continue;
            }
            writer.WritePropertyName(field.Key);
            if (field is ExtractionTableField table)
            {
                WriteTable(writer, table);
            }
            else
            {
                WriteScalar(writer, field.Type, field.Description);
            }
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    });

    private static void WriteTable(Utf8JsonWriter writer, ExtractionTableField table)
    {
        writer.WriteStartObject();
        WriteNullableType(writer, "array");
        if (!string.IsNullOrEmpty(table.Description))
        {
            writer.WriteString("description", table.Description);
        }
        writer.WriteStartObject("items");
        writer.WriteString("type", "object");
        writer.WriteBoolean("additionalProperties", false);
        writer.WriteStartArray("required");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in table.Columns)
        {
            if (seen.Add(column.Key))
            {
                writer.WriteStringValue(column.Key);
            }
        }
        writer.WriteEndArray();
        writer.WriteStartObject("properties");
        seen.Clear();
        foreach (var column in table.Columns)
        {
            if (seen.Add(column.Key))
            {
                writer.WritePropertyName(column.Key);
                WriteScalar(writer, column.Type, null);
            }
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteScalar(Utf8JsonWriter writer, ExtractionFieldType type, string? description)
    {
        writer.WriteStartObject();
        WriteNullableType(writer, type switch
        {
            ExtractionFieldType.Money or ExtractionFieldType.Number => "number",
            ExtractionFieldType.Integer => "integer",
            _ => "string"
        });
        switch (type)
        {
            case ExtractionFieldType.Date:
                writer.WriteString("format", "date");
                break;
            case ExtractionFieldType.Money:
                writer.WriteNumber("multipleOf", 0.01m);
                break;
            case ExtractionFieldType.Currency:
                writer.WriteString("pattern", CurrencyPattern);
                break;
            case ExtractionFieldType.Iban:
                writer.WriteString("pattern", IbanPattern);
                break;
        }
        if (!string.IsNullOrEmpty(description))
        {
            writer.WriteString("description", description);
        }
        writer.WriteEndObject();
    }

    private static void WriteNullableType(Utf8JsonWriter writer, string type)
    {
        writer.WriteStartArray("type");
        writer.WriteStringValue(type);
        writer.WriteStringValue("null");
        writer.WriteEndArray();
    }
}
