using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How <see cref="PageViewer.Zoom"/> follows the viewport.</summary>
public enum PageFitMode
{
    /// <summary>The zoom stays where it is set.</summary>
    None,

    /// <summary>Pages fill the viewport width (the widest page when continuous, the current page in a single page view).</summary>
    Width,

    /// <summary>Pages fill the viewport height (the tallest page when continuous, the current page in a single page view).</summary>
    Height,

    /// <summary>A whole page fits inside the viewport.</summary>
    Page
}

/// <summary>How <see cref="PageViewer"/> arranges its pages.</summary>
public enum PageLayoutMode
{
    /// <summary>All pages in one scrolling column.</summary>
    Continuous,

    /// <summary>Only the current page; moving between pages swaps it.</summary>
    Single
}

/// <summary>What dragging over the pages does in <see cref="PageViewer"/>.</summary>
public enum PageInteractionMode
{
    /// <summary>Dragging goes to the page layers (such as text selection).</summary>
    Select,

    /// <summary>Dragging pans the view.</summary>
    Pan
}

/// <summary>Event data of <see cref="PageViewer.CurrentPageChanged"/>.</summary>
public sealed class PageChangedEventArgs : RoutedEventArgs
{
    internal PageChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The page that was current.</summary>
    public int OldIndex { get; }

    /// <summary>The page that is current now.</summary>
    public int NewIndex { get; }
}
