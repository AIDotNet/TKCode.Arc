using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvatarUploadFailuresDemo : UserControl
{
    public AvatarUploadFailuresDemo()
    {
        InitializeComponent();
        Upload.UploadHandler = UploadAsync;
    }

    // Stands in for sending request.Bytes to your server; the thrown message is shown as it is, with a Retry button.
    private async Task UploadAsync(AvatarUploadRequest request, IProgress<double> progress, CancellationToken cancel)
    {
        for (var step = 1; step <= 10; step++)
        {
            await Task.Delay(90, cancel);
            progress.Report(step / 10.0);
            if (step == 6 && Fail.IsChecked == true)
            {
                // Once: Retry then goes through.
                Fail.IsChecked = false;
                throw new InvalidOperationException("The server refused the photo. Nothing was saved, so try again.");
            }
        }
    }

    // A 12 MB file of zeros that exists only as a stream: the field refuses it by its length, before reading a byte.
    private void OnTooLarge(object? sender, RoutedEventArgs e) => _ = Upload.LoadAsync(new EmptyFile(12L * 1024 * 1024), "holiday.png");

    // A PDF named like a document: refused by its first bytes, however it is named.
    private void OnWrongType(object? sender, RoutedEventArgs e) =>
        _ = Upload.LoadAsync(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n")), "statement.pdf");

    private sealed class EmptyFile(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get => _position; set => _position = value; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - _position);
            Array.Clear(buffer, offset, n);
            _position += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _position + offset, _ => length + offset };
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
