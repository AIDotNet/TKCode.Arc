// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli): the geometry of
// command-palette.module.css.

using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CommandPaletteToken : AbstractControlDesignToken
{
    /// <summary>Search row: <c>min-height: 54px; padding: 8px 14px</c> (narrow: <c>8px 11px</c>).</summary>
    public double SearchRowMinHeight { get; set; }
    public Thickness SearchRowPadding { get; set; }
    public Thickness SearchRowPaddingNarrow { get; set; }

    /// <summary>The row's <c>gap: 10px</c> (narrow 8px), as the margin after the search icon and before each accessory.</summary>
    public Thickness SearchIconMargin { get; set; }
    public Thickness SearchIconMarginNarrow { get; set; }
    public Thickness SearchAccessoryMargin { get; set; }
    public Thickness SearchAccessoryMarginNarrow { get; set; }

    /// <summary>The 22px search icon box with an 18px glass at stroke 1.75.</summary>
    public double SearchIconBox { get; set; }
    public double SearchGlyphSize { get; set; }
    public double SearchGlyphStroke { get; set; }

    /// <summary>Text line heights (<c>--leading-body</c> 1.4 at 14px and 12px).</summary>
    public double InputLineHeight { get; set; }
    public double TitleLineHeight { get; set; }
    public double CaptionLineHeight { get; set; }

    /// <summary>Key caps (<c>.commandKey</c>, <c>.closeButton</c>): 7px corners.</summary>
    public CornerRadius KeyCapRadius { get; set; }

    /// <summary>The ⌘K cap: <c>min-width: 38px; height: 24px; padding: 0 6px</c>.</summary>
    public double CommandKeyMinWidth { get; set; }
    public double CommandKeyHeight { get; set; }
    public Thickness CommandKeyPadding { get; set; }

    /// <summary>Clear button: 24px square with a 15px X.</summary>
    public double ClearButtonSize { get; set; }
    public double ClearGlyphSize { get; set; }

    /// <summary>"Esc" close button: <c>min-width: 27px; height: 24px; padding: 0 6px</c>.</summary>
    public double CloseButtonMinWidth { get; set; }
    public double CloseButtonHeight { get; set; }
    public Thickness CloseButtonPadding { get; set; }

    /// <summary>Focus ring around the small buttons (2px ring, 3px outside the 7px corners).</summary>
    public Thickness KeyFocusRingMargin { get; set; }
    public CornerRadius KeyFocusRingRadius { get; set; }

    /// <summary>Results list: <c>max-height: 342px; padding: 6px 8px</c> (narrow: <c>6px</c>).</summary>
    public double ResultsMaxHeight { get; set; }
    public Thickness ResultsPadding { get; set; }
    public Thickness ResultsPaddingNarrow { get; set; }

    /// <summary>Group heading: <c>padding: 6px 8px 3px</c>; groups are 6px apart.</summary>
    public Thickness GroupHeadingPadding { get; set; }
    public double GroupGap { get; set; }

    /// <summary>Result row: <c>min-height: 34px; padding: 0 8px</c> (≤ 340: <c>0 6px</c>), 8px corners (also the highlight).</summary>
    public double RowMinHeight { get; set; }
    public Thickness RowPadding { get; set; }
    public Thickness RowPaddingNarrowest { get; set; }
    public CornerRadius RowRadius { get; set; }

    /// <summary>The row's <c>gap: 10px</c> (narrow 8px) after the icon, and before the shortcut.</summary>
    public Thickness RowIconMargin { get; set; }
    public Thickness RowIconMarginNarrow { get; set; }
    public Thickness RowShortcutMargin { get; set; }

    /// <summary>Space between a row's title and its description (<c>gap: 8px</c>).</summary>
    public double RowCopyGap { get; set; }

    /// <summary>Row icons: 16px, lucide's default stroke 2.</summary>
    public double ItemIconSize { get; set; }
    public double ItemIconStroke { get; set; }

    /// <summary>Row shortcut cap: <c>min-width: 18px; height: 18px; padding: 0 4px; border-radius: 5px; font-size: 11px</c>.</summary>
    public double ShortcutMinWidth { get; set; }
    public double ShortcutHeight { get; set; }
    public Thickness ShortcutPadding { get; set; }
    public CornerRadius ShortcutRadius { get; set; }
    public double ShortcutFontSize { get; set; }

    /// <summary>Empty state: <c>padding: 26px 12px 28px; gap: 4px</c>, a 32px icon box (5px below) with a 20px glass.</summary>
    public Thickness EmptyPadding { get; set; }
    public double EmptyGap { get; set; }
    public double EmptyIconBox { get; set; }
    public Thickness EmptyIconMargin { get; set; }
    public double EmptyGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SearchRowMinHeight = 54;
        SearchRowPadding = new Thickness(14, 8);
        SearchRowPaddingNarrow = new Thickness(11, 8);
        SearchIconMargin = new Thickness(0, 0, 10, 0);
        SearchIconMarginNarrow = new Thickness(0, 0, 8, 0);
        SearchAccessoryMargin = new Thickness(10, 0, 0, 0);
        SearchAccessoryMarginNarrow = new Thickness(8, 0, 0, 0);
        SearchIconBox = 22;
        SearchGlyphSize = 18;
        SearchGlyphStroke = 1.75;
        InputLineHeight = 14 * 1.4;
        TitleLineHeight = 14 * 1.4;
        CaptionLineHeight = 12 * 1.4;
        KeyCapRadius = new CornerRadius(7);
        CommandKeyMinWidth = 38;
        CommandKeyHeight = 24;
        CommandKeyPadding = new Thickness(6, 0);
        ClearButtonSize = 24;
        ClearGlyphSize = 15;
        CloseButtonMinWidth = 27;
        CloseButtonHeight = 24;
        CloseButtonPadding = new Thickness(6, 0);
        KeyFocusRingMargin = new Thickness(-3);
        KeyFocusRingRadius = new CornerRadius(10);
        ResultsMaxHeight = 342;
        ResultsPadding = new Thickness(8, 6);
        ResultsPaddingNarrow = new Thickness(6);
        GroupHeadingPadding = new Thickness(8, 6, 8, 3);
        GroupGap = 6;
        RowMinHeight = 34;
        RowPadding = new Thickness(8, 0);
        RowPaddingNarrowest = new Thickness(6, 0);
        RowRadius = new CornerRadius(8);
        RowIconMargin = new Thickness(0, 0, 10, 0);
        RowIconMarginNarrow = new Thickness(0, 0, 8, 0);
        RowShortcutMargin = new Thickness(10, 0, 0, 0);
        RowCopyGap = 8;
        ItemIconSize = 16;
        ItemIconStroke = 2;
        ShortcutMinWidth = 18;
        ShortcutHeight = 18;
        ShortcutPadding = new Thickness(4, 0);
        ShortcutRadius = new CornerRadius(5);
        ShortcutFontSize = 11;
        EmptyPadding = new Thickness(12, 26, 12, 28);
        EmptyGap = 4;
        EmptyIconBox = 32;
        EmptyIconMargin = new Thickness(0, 0, 0, 5);
        EmptyGlyphSize = 20;
    }
}
