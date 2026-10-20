using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>The toolbar's compact search field (its look comes from an inline theme in the FilterToolbar template).</summary>
internal sealed class FilterToolbarSearchBox : TextBox
{
    public FilterToolbarSearchBox()
    {
        FocusAdorner = null;
    }

    protected override Type StyleKeyOverride => typeof(FilterToolbarSearchBox);
}
