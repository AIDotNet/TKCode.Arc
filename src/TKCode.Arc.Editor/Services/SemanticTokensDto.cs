// Ported from vscode (src/vs/editor/common/services/semanticTokensDto.ts, commit b57d5c9e), MIT license.
// Upstream sends semantic tokens between processes in this compact form; here it serves a host that gets tokens from a
// language server in another process. Differences: the buffer is a byte[] (upstream's VSBuffer) written little endian
// with BinaryPrimitives, so there is no endianness flip and no alignment case; the DTOs are records.

using System.Buffers.Binary;

namespace TKCode.Arc.Editor.Services;

/// <summary>A full or delta semantic tokens result as sent between processes (upstream's ISemanticTokensDto).</summary>
internal abstract record SemanticTokensDto(uint Id);

/// <summary>All the tokens (upstream's IFullSemanticTokensDto).</summary>
internal sealed record FullSemanticTokensDto(uint Id, uint[] Data) : SemanticTokensDto(Id);

/// <summary>One change of a delta (start, delete count, inserted data).</summary>
internal sealed record SemanticTokensDtoDelta(uint Start, uint DeleteCount, uint[]? Data);

/// <summary>Changes to the previous tokens (upstream's IDeltaSemanticTokensDto).</summary>
internal sealed record DeltaSemanticTokensDto(uint Id, IReadOnlyList<SemanticTokensDtoDelta> Deltas) : SemanticTokensDto(Id);

/// <summary>Encodes and decodes <see cref="SemanticTokensDto"/> (upstream's encodeSemanticTokensDto / decodeSemanticTokensDto).</summary>
internal static class SemanticTokensDtoCodec
{
    private const uint Full = 1;
    private const uint Delta = 2;

    public static byte[] Encode(SemanticTokensDto semanticTokens)
    {
        var dest = new uint[EncodeSize(semanticTokens)];
        var offset = 0;
        dest[offset++] = semanticTokens.Id;
        if (semanticTokens is FullSemanticTokensDto full)
        {
            dest[offset++] = Full;
            dest[offset++] = (uint)full.Data.Length;
            full.Data.CopyTo(dest, offset);
            offset += full.Data.Length;
        }
        else
        {
            var delta = (DeltaSemanticTokensDto)semanticTokens;
            dest[offset++] = Delta;
            dest[offset++] = (uint)delta.Deltas.Count;
            foreach (var d in delta.Deltas)
            {
                dest[offset++] = d.Start;
                dest[offset++] = d.DeleteCount;
                if (d.Data is { } data)
                {
                    dest[offset++] = (uint)data.Length;
                    data.CopyTo(dest, offset);
                    offset += data.Length;
                }
                else
                {
                    dest[offset++] = 0;
                }
            }
        }

        var bytes = new byte[dest.Length * 4];
        for (var i = 0; i < dest.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), dest[i]);
        }

        return bytes;
    }

    private static int EncodeSize(SemanticTokensDto semanticTokens)
    {
        var result = 2; // id, type
        if (semanticTokens is FullSemanticTokensDto full)
        {
            result += 1 + full.Data.Length; // data length, data
        }
        else
        {
            var delta = (DeltaSemanticTokensDto)semanticTokens;
            result += 1; // delta count
            result += 3 * delta.Deltas.Count; // start, deleteCount, data length
            foreach (var d in delta.Deltas)
            {
                result += d.Data?.Length ?? 0;
            }
        }

        return result;
    }

    public static SemanticTokensDto Decode(ReadOnlySpan<byte> buffer)
    {
        var src = new uint[buffer.Length / 4];
        for (var i = 0; i < src.Length; i++)
        {
            src[i] = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(i * 4));
        }

        var offset = 0;
        var id = src[offset++];
        var type = src[offset++];
        if (type == Full)
        {
            var length = (int)src[offset++];
            var data = src.AsSpan(offset, length).ToArray();
            return new FullSemanticTokensDto(id, data);
        }

        var deltaCount = (int)src[offset++];
        var deltas = new SemanticTokensDtoDelta[deltaCount];
        for (var i = 0; i < deltaCount; i++)
        {
            var start = src[offset++];
            var deleteCount = src[offset++];
            var length = (int)src[offset++];
            uint[]? data = null;
            if (length > 0)
            {
                data = src.AsSpan(offset, length).ToArray();
                offset += length;
            }

            deltas[i] = new SemanticTokensDtoDelta(start, deleteCount, data);
        }

        return new DeltaSemanticTokensDto(id, deltas);
    }
}
