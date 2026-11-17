using Avalonia;
using Avalonia.Media;
using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>Rendering options of <see cref="TerminalView"/> (05-rendering.md §6.2, §8, §9, §10.5).</summary>
public sealed partial class TerminalView
{
    /// <summary>Defines the <see cref="FontWeight"/> property.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<TerminalView, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    /// <summary>Defines the <see cref="BoldFontWeight"/> property.</summary>
    public static readonly StyledProperty<FontWeight> BoldFontWeightProperty =
        AvaloniaProperty.Register<TerminalView, FontWeight>(nameof(BoldFontWeight), FontWeight.Bold);

    /// <summary>Defines the <see cref="LineHeight"/> property.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(LineHeight), 1.0);

    /// <summary>Defines the <see cref="BoldIsBright"/> property.</summary>
    public static readonly StyledProperty<bool> BoldIsBrightProperty =
        AvaloniaProperty.Register<TerminalView, bool>(nameof(BoldIsBright));

    /// <summary>Defines the <see cref="TextBlink"/> property.</summary>
    public static readonly StyledProperty<bool> TextBlinkProperty =
        AvaloniaProperty.Register<TerminalView, bool>(nameof(TextBlink), true);

    /// <summary>Defines the <see cref="CustomGlyphs"/> property.</summary>
    public static readonly StyledProperty<bool> CustomGlyphsProperty =
        AvaloniaProperty.Register<TerminalView, bool>(nameof(CustomGlyphs), true);

    /// <summary>Defines the <see cref="EmojiFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily?> EmojiFontFamilyProperty =
        AvaloniaProperty.Register<TerminalView, FontFamily?>(nameof(EmojiFontFamily));

    /// <summary>Defines the <see cref="HoveredLink"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<CellSpan>?> HoveredLinkProperty =
        AvaloniaProperty.Register<TerminalView, IReadOnlyList<CellSpan>?>(nameof(HoveredLink));

    /// <summary>Defines the <see cref="LinkForeground"/> property.</summary>
    public static readonly StyledProperty<Color?> LinkForegroundProperty =
        AvaloniaProperty.Register<TerminalView, Color?>(nameof(LinkForeground));

    /// <summary>Defines the <see cref="CursorBlink"/> property.</summary>
    public static readonly StyledProperty<TerminalCursorBlink> CursorBlinkProperty =
        AvaloniaProperty.Register<TerminalView, TerminalCursorBlink>(nameof(CursorBlink), TerminalCursorBlink.Program);

    /// <summary>Defines the <see cref="CursorBlinkInterval"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> CursorBlinkIntervalProperty =
        AvaloniaProperty.Register<TerminalView, TimeSpan>(nameof(CursorBlinkInterval), TimeSpan.FromMilliseconds(600));

    /// <summary>Defines the <see cref="UnfocusedCursorStyle"/> property.</summary>
    public static readonly StyledProperty<TerminalUnfocusedCursorStyle> UnfocusedCursorStyleProperty =
        AvaloniaProperty.Register<TerminalView, TerminalUnfocusedCursorStyle>(nameof(UnfocusedCursorStyle), TerminalUnfocusedCursorStyle.Hollow);

    /// <summary>Defines the <see cref="CursorBarWidth"/> property.</summary>
    public static readonly StyledProperty<double> CursorBarWidthProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(CursorBarWidth), 1.0);

    /// <summary>Defines the <see cref="CursorTextColor"/> property.</summary>
    public static readonly StyledProperty<Color?> CursorTextColorProperty =
        AvaloniaProperty.Register<TerminalView, Color?>(nameof(CursorTextColor));

    /// <summary>Defines the <see cref="MinimumContrastRatio"/> property.</summary>
    public static readonly StyledProperty<double> MinimumContrastRatioProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(MinimumContrastRatio), 1.0);

    /// <summary>The weight of normal text (xterm.js <c>fontWeight</c>). Default <see cref="FontWeight.Normal"/>.</summary>
    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    /// <summary>
    /// The weight of bold text (SGR 1; xterm.js <c>fontWeightBold</c>). Default <see cref="FontWeight.Bold"/>. A family
    /// without that weight gets a synthesized bold.
    /// </summary>
    public FontWeight BoldFontWeight
    {
        get => GetValue(BoldFontWeightProperty);
        set => SetValue(BoldFontWeightProperty, value);
    }

    /// <summary>The cell height as a multiple of the font's line height. Default 1.</summary>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Whether bold text whose foreground is one of the first eight palette colors uses the bright variant (8–15) instead
    /// (xterm.js <c>drawBoldTextInBrightColors</c>). Default <c>false</c>; RGB colors are never changed.
    /// </summary>
    public bool BoldIsBright
    {
        get => GetValue(BoldIsBrightProperty);
        set => SetValue(BoldIsBrightProperty, value);
    }

    /// <summary>
    /// Whether text with SGR 5 or 6 blinks (its glyphs, not its background, hide every half second). Default <c>true</c>.
    /// The timer runs only while such text is on screen and the view is visible; hosts that honor a reduced-motion
    /// setting turn this off.
    /// </summary>
    public bool TextBlink
    {
        get => GetValue(TextBlinkProperty);
        set => SetValue(TextBlinkProperty, value);
    }

    /// <summary>
    /// Whether the view draws box drawing (U+2500–257F), block elements (U+2580–259F), braille (U+2800–28FF) and Powerline
    /// separators (U+E0B0–E0BF, E0D2, E0D4) itself, so lines meet across cells whatever the font and the cell height
    /// (xterm.js <c>customGlyphs</c>). Default <c>true</c>; <c>false</c> takes them from the font.
    /// </summary>
    public bool CustomGlyphs
    {
        get => GetValue(CustomGlyphsProperty);
        set => SetValue(CustomGlyphsProperty, value);
    }

    /// <summary>
    /// The font for emoji presentation (emoji by default, or followed by VS16). <c>null</c> (the default) asks the platform
    /// for its color emoji font (Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji).
    /// </summary>
    public FontFamily? EmojiFontFamily
    {
        get => GetValue(EmojiFontFamilyProperty);
        set => SetValue(EmojiFontFamilyProperty, value);
    }

    /// <summary>
    /// Cells of the link under the pointer (viewport rows, <see cref="CellSpan.EndColumn"/> inclusive), drawn with a single
    /// underline, or a double one where the text is already underlined. <c>null</c> (the default) draws nothing.
    /// </summary>
    public IReadOnlyList<CellSpan>? HoveredLink
    {
        get => GetValue(HoveredLinkProperty);
        set => SetValue(HoveredLinkProperty, value);
    }

    /// <summary>The color of the <see cref="HoveredLink"/> underline; <c>null</c> (the default) uses each cell's foreground.</summary>
    public Color? LinkForeground
    {
        get => GetValue(LinkForegroundProperty);
        set => SetValue(LinkForegroundProperty, value);
    }

    /// <summary>
    /// When the cursor blinks. Default <see cref="TerminalCursorBlink.Program"/>: when the program asks. It only blinks while
    /// the view has focus; typing and new output show it again.
    /// </summary>
    public TerminalCursorBlink CursorBlink
    {
        get => GetValue(CursorBlinkProperty);
        set => SetValue(CursorBlinkProperty, value);
    }

    /// <summary>How long the blinking cursor stays shown, and then hidden. Default 600 ms.</summary>
    public TimeSpan CursorBlinkInterval
    {
        get => GetValue(CursorBlinkIntervalProperty);
        set => SetValue(CursorBlinkIntervalProperty, value);
    }

    /// <summary>
    /// How the cursor looks while the view does not have focus. Default <see cref="TerminalUnfocusedCursorStyle.Hollow"/>,
    /// as in xterm.js, VS Code and Ghostty.
    /// </summary>
    public TerminalUnfocusedCursorStyle UnfocusedCursorStyle
    {
        get => GetValue(UnfocusedCursorStyleProperty);
        set => SetValue(UnfocusedCursorStyleProperty, value);
    }

    /// <summary>The width of the bar cursor in DIP (xterm.js <c>cursorWidth</c>), drawn at least one device pixel wide. Default 1.</summary>
    public double CursorBarWidth
    {
        get => GetValue(CursorBarWidthProperty);
        set => SetValue(CursorBarWidthProperty, value);
    }

    /// <summary>The color of the character under a block cursor; <c>null</c> (the default) uses the default background.</summary>
    public Color? CursorTextColor
    {
        get => GetValue(CursorTextColorProperty);
        set => SetValue(CursorTextColorProperty, value);
    }

    /// <summary>
    /// The lowest WCAG contrast ratio between a cell's text and its background (xterm.js <c>minimumContrastRatio</c>); text
    /// below it is darkened or lightened until it reaches it. Default 1 (off); 4.5 is the WCAG AA level VS Code uses.
    /// Box drawing, block, braille and Powerline cells, emoji and invisible text are left alone.
    /// </summary>
    public double MinimumContrastRatio
    {
        get => GetValue(MinimumContrastRatioProperty);
        set => SetValue(MinimumContrastRatioProperty, value);
    }
}
