using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TKCode.Arc;

public sealed partial class ExtractionOutput
{
    /// <summary>
    /// The output as JSON text: an object with one property per field in order; amounts and numbers are JSON numbers with
    /// the digits they were written with (<c>640.00</c>), dates ISO text, empty and invalid values <c>null</c>, a table an
    /// array of objects. Indented text uses two spaces and <c>\n</c> line breaks; text other than quotes, backslashes and
    /// control characters is written as it is.
    /// </summary>
    /// <param name="indented">Whether to write one value per line.</param>
    public string ToJson(bool indented = true) => ExtractionJson.Write(indented, writer =>
    {
        writer.WriteStartObject();
        foreach (var entry in Values)
        {
            writer.WritePropertyName(entry.Key);
            ExtractionJson.WriteValue(writer, entry.Value);
        }
        writer.WriteEndObject();
    });
}

/// <summary>The JSON writing shared by the typed output and the JSON schema (no reflection: values are matched by type).</summary>
internal static class ExtractionJson
{
    /// <summary>Runs <paramref name="write"/> on a writer and returns the text (line breaks <c>\n</c> on every platform).</summary>
    public static string Write(bool indented, Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = indented, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            write(writer);
        }
        var text = Encoding.UTF8.GetString(buffer.WrittenSpan);
        // Strings escape their own line breaks, so every CR LF left is one the writer put between values.
        return indented ? text.Replace("\r\n", "\n") : text;
    }

    /// <summary>Writes a value of the output: null, text, a number, a boolean, a date, an object (pairs) or an array.</summary>
    public static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case byte number:
                writer.WriteNumberValue(number);
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                if (double.IsFinite(number))
                {
                    writer.WriteNumberValue(number);
                }
                else
                {
                    writer.WriteNullValue();
                }
                break;
            case float number:
                if (float.IsFinite(number))
                {
                    writer.WriteNumberValue(number);
                }
                else
                {
                    writer.WriteNullValue();
                }
                break;
            case DateOnly date:
                writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case DateTime time:
                writer.WriteStringValue(time);
                break;
            case DateTimeOffset time:
                writer.WriteStringValue(time);
                break;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                writer.WriteStartObject();
                foreach (var pair in pairs)
                {
                    writer.WritePropertyName(pair.Key);
                    WriteValue(writer, pair.Value);
                }
                writer.WriteEndObject();
                break;
            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                    WriteValue(writer, entry.Value);
                }
                writer.WriteEndObject();
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items)
                {
                    WriteValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
