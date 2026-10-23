using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiComposerAttachmentsDemo : UserControl
{
    public AiComposerAttachmentsDemo()
    {
        InitializeComponent();
        Composer.Placeholder = "Attach a file, then ask about it";

        // Upload somewhere: report progress, honor the token (the person cancelled), throw to refuse the file.
        Composer.UploadHandler = async (attachment, progress, cancellation) =>
        {
            for (var step = 1; step <= 10; step++)
            {
                await Task.Delay(180, cancellation);
                progress.Report(step / 10d);
                if (step == 6 && attachment.Name.Contains("fail", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Upload refused");
                }
            }
        };
    }
}
