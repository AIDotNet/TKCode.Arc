using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// The thumbnails: a virtualized list whose containers are the pooled, self-drawn <see cref="PdfThumbnail"/>, so only the
/// thumbnails near the viewport exist and ask for bitmaps. Up, Down, Home and End move the focus; Tab enters and leaves.
/// </summary>
internal sealed class PdfThumbnailList : ItemsControl
{
    private PdfViewer? _viewer;

    static PdfThumbnailList()
    {
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<PdfThumbnailList>(KeyboardNavigationMode.Once);
    }

    public PdfViewer? Viewer
    {
        get => _viewer;
        set => _viewer = value;
    }

    /// <summary>The thumbnails that exist right now.</summary>
    public IEnumerable<PdfThumbnail> Realized => GetRealizedContainers().OfType<PdfThumbnail>();

    public void SetPages(int count)
    {
        ItemsSource = count > 0 ? new PdfPageIndexList(count) : null;
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<PdfThumbnail>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new PdfThumbnail();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is PdfThumbnail thumbnail && _viewer is { } viewer)
        {
            thumbnail.Prepare(viewer, index);
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        (container as PdfThumbnail)?.Clear();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not PdfThumbnail { PageIndex: >= 0 } focused)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Up => focused.PageIndex - 1,
            Key.Down => focused.PageIndex + 1,
            Key.Home => 0,
            Key.End => ItemCount - 1,
            _ => -1
        };
        if (target < 0 || target >= ItemCount)
        {
            return;
        }
        e.Handled = true;
        ScrollIntoView(target);
        if (ContainerFromIndex(target) is { } container)
        {
            KeyboardNavigation.SetTabOnceActiveElement(this, container);
            container.Focus(NavigationMethod.Directional);
        }
    }
}
