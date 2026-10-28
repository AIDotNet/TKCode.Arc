namespace TKCode.Arc;

/// <summary>How a parent node of a <see cref="Cascader"/> opens its child column.</summary>
public enum CascaderExpandTrigger
{
    /// <summary>The child column opens when the parent is clicked, tapped or entered with the keyboard.</summary>
    Click,

    /// <summary>The child column also opens while the pointer rests on the parent.</summary>
    Hover,
}

/// <summary>Data of <see cref="Cascader.SelectionChanged"/>.</summary>
public sealed class CascaderSelectionChangedEventArgs : EventArgs
{
    public CascaderSelectionChangedEventArgs(IReadOnlyList<object> oldPath, IReadOnlyList<object> newPath)
    {
        OldPath = oldPath;
        NewPath = newPath;
    }

    /// <summary>The nodes from the root to the previously selected node; empty when nothing was selected.</summary>
    public IReadOnlyList<object> OldPath { get; }

    /// <summary>The nodes from the root to the newly selected node; empty when the selection was cleared.</summary>
    public IReadOnlyList<object> NewPath { get; }
}
