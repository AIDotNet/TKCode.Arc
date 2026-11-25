// Ported from vscode (src/vs/editor/common/services/semanticTokensDto.ts, commit b57d5c9e), MIT license.
// Upstream sends semantic tokens between processes in this compact form; here it serves a host that gets tokens from a
// language server in another process. Differences: the buffer is a byte[] (upstream's VSBuffer) written little endian
// with BinaryPrimitives, so there is no endianness flip and no alignment case; the DTOs are records.

using System.Buffers.Binary;

namespace TKCode.Arc.Editor.Services;

/// <summary>A full or delta semantic tokens result as sent between processes (upstream's ISemanticTokensDto).</summary>
internal union SemanticTokensDto(FullSemanticTokensDto, DeltaSemanticTokensDto)
{
    public uint Id => this switch
    {
        FullSemanticTokensDto full => full.Id,
        DeltaSemanticTokensDto delta => delta.Id,
        _ => 0,
    };
}

/// <summary>All the tokens (upstream's IFullSemanticTokensDto).</summary>
internal sealed class FullSemanticTokensDto(uint id, uint[] data)
{
    public uint Id { get; } = id;

    public uint[] Data { get; } = data;
}

/// <summary>One change of a delta (start, delete count, inserted data).</summary>
internal sealed class SemanticTokensDtoDelta(uint start, uint deleteCount, uint[]? data)
{
    public uint Start { get; } = start;

    public uint DeleteCount { get; } = deleteCount;

    public uint[]? Data { get; } = data;
}

/// <summary>Changes to the previous tokens (upstream's IDeltaSemanticTokensDto).</summary>
internal sealed class DeltaSemanticTokensDto(uint id, IReadOnlyList<SemanticTokensDtoDelta> deltas)
{
    public uint Id { get; } = id;

    public IReadOnlyList<SemanticTokensDtoDelta> Deltas { get; } = deltas;
}

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
        switch (semanticTokens)
        {
            case FullSemanticTokensDto full:
                dest[offset++] = Full;
                dest[offset++] = (uint)full.Data.Length;
                full.Data.CopyTo(dest, offset);
                offset += full.Data.Length;
                break;
            case DeltaSemanticTokensDto delta:
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
                break;
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
        switch (semanticTokens)
        {
            case FullSemanticTokensDto full:
                result += 1 + full.Data.Length; // data length, data
                break;
            case DeltaSemanticTokensDto delta:
                result += 1; // delta count
                result += 3 * delta.Deltas.Count; // start, deleteCount, data length
                foreach (var d in delta.Deltas)
                {
                    result += d.Data?.Length ?? 0;
                }
                break;
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
