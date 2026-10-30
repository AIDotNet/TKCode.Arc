using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Insert with a handler that takes a moment.</summary>
public partial class ReplyDraftInsertDemo : UserControl
{
    public ReplyDraftInsertDemo()
    {
        InitializeComponent();
        Draft.InsertHandler = InsertAsync;
    }

    private async Task InsertAsync(ReplyDraftEventArgs draft)
    {
        // Your composer would take the text here; the delay stands in for saving it.
        await Task.Delay(900);
        Composer.Text = draft.Draft;
    }
}
