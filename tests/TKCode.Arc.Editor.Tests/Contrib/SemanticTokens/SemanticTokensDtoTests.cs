// Ported from vscode (src/vs/editor/test/common/services/semanticTokensDto.test.ts, commit b57d5c9e), MIT license: all
// 4 upstream tests. The "unusual backing array buffer" case slices a larger byte array at unaligned offsets.

using Shouldly;
using TKCode.Arc.Editor.Services;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SemanticTokensDtoTests
{
    private const uint Packed = (1u << 24) + (2 << 16) + (3 << 8) + 4;

    private static void AssertEqual(SemanticTokensDto actual, SemanticTokensDto expected)
    {
        switch (expected)
        {
            case FullSemanticTokensDto full:
                var a = actual.ShouldBeOfType<FullSemanticTokensDto>();
                a.Id.ShouldBe(full.Id);
                a.Data.ShouldBe(full.Data);
                break;
            case DeltaSemanticTokensDto delta:
                var d = actual.ShouldBeOfType<DeltaSemanticTokensDto>();
                d.Id.ShouldBe(delta.Id);
                d.Deltas.Count.ShouldBe(delta.Deltas.Count);
                for (var i = 0; i < delta.Deltas.Count; i++)
                {
                    d.Deltas[i].Start.ShouldBe(delta.Deltas[i].Start);
                    d.Deltas[i].DeleteCount.ShouldBe(delta.Deltas[i].DeleteCount);
                    (d.Deltas[i].Data ?? []).ShouldBe(delta.Deltas[i].Data ?? []);
                }

                break;
        }
    }

    private static void TestRoundTrip(SemanticTokensDto value) =>
        AssertEqual(SemanticTokensDtoCodec.Decode(SemanticTokensDtoCodec.Encode(value)), value);

    [Fact]
    public void Full_encoding() => TestRoundTrip(new FullSemanticTokensDto(12, [Packed]));

    [Fact]
    public void Delta_encoding() => TestRoundTrip(new DeltaSemanticTokensDto(12,
    [
        new SemanticTokensDtoDelta(0, 4, null),
        new SemanticTokensDtoDelta(15, 0, [Packed]),
        new SemanticTokensDtoDelta(27, 5, [Packed, 1, 2, 3, 4, 5, 6, 7, 8, 9]),
    ]));

    [Fact]
    public void Partial_array_buffer()
    {
        uint[] shared = [Packed, 1, 2, 3, 4, 5, Packed];
        TestRoundTrip(new DeltaSemanticTokensDto(12,
        [
            new SemanticTokensDtoDelta(0, 4, shared[..1]),
            new SemanticTokensDtoDelta(15, 0, shared[1..]),
        ]));
    }

    [Fact]
    public void Issue_94521_unusual_backing_array_buffer()
    {
        static ReadOnlySpan<byte> WrapAndSlice(byte[] buffer, int prefixLength, int suffixLength)
        {
            var wrapped = new byte[prefixLength + buffer.Length + suffixLength];
            buffer.CopyTo(wrapped, prefixLength);
            return wrapped.AsSpan(prefixLength, buffer.Length);
        }

        var dto = new FullSemanticTokensDto(5, [1, 2, 3, 4, 5]);
        var encoded = SemanticTokensDtoCodec.Encode(dto);

        // with misaligned prefix and misaligned suffix
        AssertEqual(SemanticTokensDtoCodec.Decode(WrapAndSlice(encoded, 1, 1)), dto);
        // with misaligned prefix and aligned suffix
        AssertEqual(SemanticTokensDtoCodec.Decode(WrapAndSlice(encoded, 1, 4)), dto);
        // with aligned prefix and misaligned suffix
        AssertEqual(SemanticTokensDtoCodec.Decode(WrapAndSlice(encoded, 4, 1)), dto);
        // with aligned prefix and aligned suffix
        AssertEqual(SemanticTokensDtoCodec.Decode(WrapAndSlice(encoded, 4, 4)), dto);
    }
}
