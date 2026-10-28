using Shouldly;

namespace TKCode.Arc.Core.Tests.Audio;

public class WavFileTests
{
    [Fact]
    public void Encoded_File_Reports_Its_Length()
    {
        var wav = WavFile.Encode(new float[22050], sampleRate: 44100);
        wav.Length.ShouldBe(44 + 22050 * 2);
        WavFile.TryGetDuration(wav).ShouldBe(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public void Stereo_Length_Counts_Frames_Not_Samples()
    {
        var wav = WavFile.Encode(new float[8000 * 2], sampleRate: 8000, channels: 2);
        WavFile.TryGetDuration(wav).ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Samples_Are_Clamped_And_Scaled_To_16_Bit()
    {
        var wav = WavFile.Encode([2f, -2f, 0.5f], sampleRate: 8000);
        BitConverter.ToInt16(wav, 44).ShouldBe(short.MaxValue);
        BitConverter.ToInt16(wav, 46).ShouldBe((short)-short.MaxValue);
        BitConverter.ToInt16(wav, 48).ShouldBe((short)16384);
    }

    [Fact]
    public void Chunks_Before_The_Data_Are_Skipped()
    {
        var wav = WavFile.Encode(new float[8000], sampleRate: 8000);
        // Insert an odd-sized LIST chunk (padded to even) between fmt and data.
        byte[] list = [.. "LIST"u8, 3, 0, 0, 0, 1, 2, 3, 0];
        var withList = wav[..36].Concat(list).Concat(wav[36..]).ToArray();
        WavFile.TryGetDuration(withList).ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Truncated_Data_Counts_Only_What_Is_There()
    {
        var wav = WavFile.Encode(new float[8000], sampleRate: 8000);
        WavFile.TryGetDuration(wav.AsSpan(0, 44 + 8000)).ShouldBe(TimeSpan.FromSeconds(0.5));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    public void Other_Data_Has_No_Length(byte[] data) => WavFile.TryGetDuration(data).ShouldBeNull();
}
