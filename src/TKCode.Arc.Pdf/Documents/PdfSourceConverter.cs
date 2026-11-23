using System.ComponentModel;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Converts what a binding gives <see cref="PdfViewer.Source"/> into a <see cref="PdfSource"/>: a path or an absolute address
/// (<see cref="string"/>, as <see cref="PdfSource.Parse"/>), a <see cref="Uri"/>, or the bytes of a document
/// (<see cref="byte"/>[] or <see cref="ReadOnlyMemory{T}"/> of bytes). An empty or blank string gives no source.
/// </summary>
public sealed class PdfSourceConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || sourceType == typeof(Uri) || sourceType == typeof(byte[]) ||
        sourceType == typeof(ReadOnlyMemory<byte>) || sourceType == typeof(Memory<byte>) ||
        base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value switch
        {
            string text => string.IsNullOrWhiteSpace(text) ? null : PdfSource.Parse(text),
            Uri uri => PdfSource.FromUri(uri),
            byte[] bytes => PdfSource.FromBytes(bytes),
            ReadOnlyMemory<byte> memory => PdfSource.FromBytes(memory),
            Memory<byte> memory => PdfSource.FromBytes(memory),
            _ => base.ConvertFrom(context, culture, value)
        };
}
