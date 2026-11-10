using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentBuilderWiredDemo : UserControl
{
    private int _saves;

    public AgentBuilderWiredDemo()
    {
        InitializeComponent();
        Builder.SaveHandler = async (_, cancellation) =>
        {
            await Task.Delay(700, cancellation);
            if (_saves++ == 0)
            {
                throw new InvalidOperationException("The server is busy. Try again.");
            }
        };
        Builder.PublishHandler = (_, cancellation) => Task.Delay(900, cancellation);
        Builder.IndexHandler = async (source, progress, cancellation) =>
        {
            for (var i = 1; i <= 10; i++)
            {
                await Task.Delay(200, cancellation);
                progress.Report(i / 10.0);
                if (i == 6 && source.Url?.AbsoluteUri.Contains("private", StringComparison.OrdinalIgnoreCase) == true)
                {
                    throw new InvalidOperationException("The page asks for a sign-in.");
                }
            }
            return 40;
        };
        Builder.ConfigChanged += (_, e) => Log.Text = $"Changed: {e.Field}. {(e.IsDirty ? "Unsaved." : "Matches the saved version.")}";
        Builder.Saved += (_, e) => Log.Text = $"Saved \"{e.Config.Name}\" with {e.Config.ToolIds.Count} tools on.";
        Builder.Published += (_, e) => Log.Text = $"Published \"{e.Config.Name}\".";
        Builder.SourceIndexed += (_, e) => Log.Text = $"{e.Item.Name}: {e.Item.Status}.";
    }
}
