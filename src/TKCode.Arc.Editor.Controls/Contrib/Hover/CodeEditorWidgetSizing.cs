// How large the floating widgets of the hover track may be: content widgets cannot overflow the editor here (see
// CodeEditorWidgets.cs), so they are kept as wide as the text area.

namespace TKCode.Arc;

/// <summary>Size limits of the editor's floating widgets.</summary>
internal static class CodeEditorWidgetSizing
{
    // room for the overlay scroll bar at the right
    private const double RightMargin = 12;

    /// <summary>The widest a floating widget can be: the text area less the scroll bar.</summary>
    public static double AvailableWidth(CodeEditor editor)
    {
        var width = editor.GetLayoutSize().Width - (editor.View?.GutterWidth ?? 0) - RightMargin;
        return width > 0 ? width : double.PositiveInfinity;
    }
}
