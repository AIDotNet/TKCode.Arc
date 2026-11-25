using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the find widget of <see cref="CodeEditor"/> that follows neither the theme nor the accent.</summary>
[ControlDesignToken]
internal sealed class CodeEditorFindWidgetToken : AbstractControlDesignToken
{
    /// <summary>The width of the widget when the editor has room for it (Monaco's FIND_WIDGET_INITIAL_WIDTH, 419).</summary>
    public double Width { get; set; }

    /// <summary>The space between the widget and the editor's top and right edges.</summary>
    public Thickness Margin { get; set; }

    /// <summary>The space inside the widget's frame.</summary>
    public Thickness Padding { get; set; }

    /// <summary>The square size of the toggles inside the inputs (match case, whole word, regex, preserve case).</summary>
    public double ToggleSize { get; set; }

    /// <summary>The room the find input keeps at its end for the three toggles over it.</summary>
    public double FindTogglesWidth { get; set; }

    /// <summary>The room the replace input keeps at its end for the preserve case toggle over it.</summary>
    public double ReplaceTogglesWidth { get; set; }

    /// <summary>The space between the toggles and the end of their input.</summary>
    public Thickness TogglesMargin { get; set; }

    /// <summary>The square size of the buttons (previous, next, find in selection, close, replace, replace all).</summary>
    public double ButtonSize { get; set; }

    /// <summary>The width of the button that shows and hides the replace row.</summary>
    public double ReplaceToggleWidth { get; set; }

    /// <summary>The size of the buttons' glyphs.</summary>
    public double GlyphSize { get; set; }

    /// <summary>The smallest width of the match count ("3 of 12"), so the buttons do not move while typing.</summary>
    public double MatchesCountMinWidth { get; set; }

    /// <summary>The opacity of a button that cannot be used now (no match to go to, nothing to replace).</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>The editor width below which the match count is hidden (Monaco's narrow find widget).</summary>
    public double NarrowEditorWidth { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Width = 419;
        Margin = new Thickness(0, 6, 8, 0);
        Padding = new Thickness(4);
        ToggleSize = 22;
        FindTogglesWidth = 62;
        ReplaceTogglesWidth = 18;
        TogglesMargin = new Thickness(0, 0, 6, 0);
        ButtonSize = 26;
        ReplaceToggleWidth = 18;
        GlyphSize = 15;
        MatchesCountMinWidth = 64;
        DisabledOpacity = 0.4;
        NarrowEditorWidth = 440;
    }
}
