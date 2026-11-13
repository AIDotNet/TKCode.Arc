using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Event data of the <c>SelectionChanged</c> event of <see cref="CoverFlow"/>, <see cref="PhotoGrid"/> and
/// <see cref="ProductGallery"/>: the selected position before and after. The new item is the control's own
/// <c>SelectedItem</c> (or <c>SelectedImage</c>).
/// </summary>
public sealed class PhotosSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for a change from <paramref name="oldIndex"/> to <paramref name="newIndex"/>.</summary>
    public PhotosSelectionChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The selected index before; -1 when nothing was selected.</summary>
    public int OldIndex { get; }

    /// <summary>The selected index now; -1 when nothing is selected.</summary>
    public int NewIndex { get; }
}
