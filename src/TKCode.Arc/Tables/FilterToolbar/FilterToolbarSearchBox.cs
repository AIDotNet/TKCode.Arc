using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The toolbar's compact search field (its look comes from an inline theme in the FilterToolbar template).</summary>
internal sealed class FilterToolbarSearchBox : TextBox
{
    // The focus ring is built on the first keyboard focus rather than declared hidden in every toolbar's template.
    private readonly TablesBFocusRing _focusRing = new(new Thickness(-3), FilterToolbarTokens.Key(FilterToolbarTokenKind.SearchFocusRingRadius));

    public FilterToolbarSearchBox()
    {
        FocusAdorner = null;
    }

    protected override Type StyleKeyOverride => typeof(FilterToolbarSearchBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Layers"));
        _focusRing.Update(Classes.Contains(":focus-visible"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing.Update(Classes.Contains(":focus-visible"));
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing.Update(Classes.Contains(":focus-visible"));
    }
}
