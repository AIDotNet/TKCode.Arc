using System.Buffers.Binary;

namespace TKCode.Arc;

/// <summary>Reads the length of a RIFF WAVE file and writes 16-bit PCM WAVE files (for generated demo audio).</summary>
public static class WavFile
{
    /// <summary>The playing time of a PCM WAVE file, or null when <paramref name="data"/> is not one.</summary>
    public static TimeSpan? TryGetDuration(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return null;
        }
        var byteRate = 0;
        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var id = data.Slice(offset, 4);
            var size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4)), int.MaxValue);
            var body = offset + 8;
            if (id.SequenceEqual("fmt "u8) && body + 12 <= data.Length)
            {
                byteRate = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(body + 8, 4));
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (byteRate <= 0)
                {
                    return null;
                }
                var length = Math.Min(size, data.Length - body);
                return TimeSpan.FromSeconds((double)length / byteRate);
            }
            offset = body + size + (size & 1);
        }
        return null;
    }

    /// <summary>Encodes samples in -1..1 (interleaved when <paramref name="channels"/> is above 1) as a 16-bit PCM WAVE file.</summary>
    public static byte[] Encode(ReadOnlySpan<float> samples, int sampleRate, int channels = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        var dataLength = samples.Length * 2;
        var bytes = new byte[44 + dataLength];
        var span = bytes.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataLength);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataLength);
        for (var i = 0; i < samples.Length; i++)
        {
            var value = Math.Clamp(samples[i], -1f, 1f);
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + i * 2)..], (short)Math.Round(value * short.MaxValue));
        }
        return bytes;
    }
}
