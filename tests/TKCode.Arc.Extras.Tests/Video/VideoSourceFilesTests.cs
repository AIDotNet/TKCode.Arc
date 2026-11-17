using Shouldly;

namespace TKCode.Arc.Tests.Video;

/// <summary>
/// <see cref="VideoSourceFiles"/>: bytes and streams become a temporary file named by their content hash, written without
/// holding the whole stream in memory (design/components/video/completion-plan.md A9).
/// </summary>
public class VideoSourceFilesTests
{
    [Fact]
    public async Task Bytes_And_A_Stream_With_The_Same_Content_Share_One_File()
    {
        var data = Content(3_000_001, seed: 1);
        var fromBytes = await VideoSourceFiles.MaterializeAsync(VideoSource.FromBytes(data), Token);
        var fromStream = await VideoSourceFiles.MaterializeAsync(VideoSource.FromStream(() => new MemoryStream(data)), Token);

        fromStream.ShouldBe(fromBytes);
        fromBytes.IsFile.ShouldBeTrue();
        Path.GetExtension(fromBytes.LocalPath).ShouldBe(".mp4");
        File.ReadAllBytes(fromBytes.LocalPath).ShouldBe(data);
        NoPartialFiles(fromBytes);
    }

    [Fact]
    public async Task Different_Content_Gets_A_Different_File()
    {
        var a = await VideoSourceFiles.MaterializeAsync(VideoSource.FromBytes(Content(1000, seed: 2), "video/webm"), Token);
        var b = await VideoSourceFiles.MaterializeAsync(VideoSource.FromBytes(Content(1000, seed: 3), "video/webm"), Token);
        a.ShouldNotBe(b);
        Path.GetExtension(a.LocalPath).ShouldBe(".webm");
    }

    [Fact]
    public async Task Concurrent_Materializations_Of_One_Stream_Agree()
    {
        var data = Content(2_000_000, seed: 4);
        var source = VideoSource.FromStream(() => new MemoryStream(data));
        var uris = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => VideoSourceFiles.MaterializeAsync(source, Token), Token)));

        uris.Distinct().Count().ShouldBe(1);
        File.ReadAllBytes(uris[0].LocalPath).ShouldBe(data);
        NoPartialFiles(uris[0]);
    }

    [Fact]
    public async Task A_Large_Stream_Is_Not_Held_In_Memory()
    {
        const long length = 128L * 1024 * 1024;
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var uri = await VideoSourceFiles.MaterializeAsync(VideoSource.FromStream(() => new PatternStream(length, seed: 5)), Token);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        new FileInfo(uri.LocalPath).Length.ShouldBe(length);
        // Copying into memory first allocates at least the stream's length; the streamed copy needs a buffer or two (the bound
        // leaves room for other tests allocating at the same time).
        allocated.ShouldBeLessThan(length / 3);
        NoPartialFiles(uri);
        File.Delete(uri.LocalPath);
    }

    [Fact]
    public async Task A_Cancelled_Copy_Leaves_No_Partial_File()
    {
        using var cancel = new CancellationTokenSource();
        var probe = await VideoSourceFiles.MaterializeAsync(VideoSource.FromBytes(Content(10, seed: 6)), Token);
        var source = VideoSource.FromStream(() => new PatternStream(64L * 1024 * 1024, seed: 7, afterBytes: 1024 * 1024, onRead: cancel.Cancel));

        await Should.ThrowAsync<OperationCanceledException>(() => VideoSourceFiles.MaterializeAsync(source, cancel.Token));
        NoPartialFiles(probe);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Content(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static void NoPartialFiles(Uri materialized) =>
        Directory.GetFiles(Path.GetDirectoryName(materialized.LocalPath)!, "*.part").ShouldBeEmpty();

    /// <summary>A read-only, non-seekable stream of <paramref name="length"/> pseudo-random bytes that are never all in memory.</summary>
    private sealed class PatternStream(long length, int seed, long afterBytes = long.MaxValue, Action? onRead = null) : Stream
    {
        private readonly Random _random = new(seed);
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_position >= afterBytes)
            {
                onRead?.Invoke();
            }
            var n = (int)Math.Min(buffer.Length, length - _position);
            _random.NextBytes(buffer[..n]);
            _position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
