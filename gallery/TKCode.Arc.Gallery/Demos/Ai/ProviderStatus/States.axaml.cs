using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProviderStatusStatesDemo : UserControl
{
    private readonly bool _ready;

    public ProviderStatusStatesDemo()
    {
        InitializeComponent();
        Card.Providers = [];
        Card.RefreshHandler = _ => Task.FromResult<IReadOnlyList<ProviderStatusItem>?>(null);
        // SelectionChanged fires while the XAML loads; subscribe once the fields are set.
        State.SelectionChanged += (_, _) => Show();
        _ready = true;
        Show();
    }

    private void Show()
    {
        if (!_ready)
        {
            return;
        }
        var state = State.SelectedValue as string;
        Card.IsLoading = state == "loading";
        Card.ErrorText = state == "failed" ? "Couldn’t load provider status. Check your connection and try again." : null;
        Card.Providers = state == "healthy" ? ProviderStatusDemoData.Healthy() : [];
    }
}
