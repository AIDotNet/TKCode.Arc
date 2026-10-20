using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of the money input (<c>money-input.module.css</c>) and its accent-independent derived colors.</summary>
[ControlDesignToken]
internal sealed class MoneyInputToken : AbstractControlDesignToken
{
    /// <summary>Shell height: <c>var(--money-input-height, 76px)</c>.</summary>
    public double ShellHeight { get; set; }

    /// <summary>Shell padding: <c>padding: 0 8px 0 18px</c>.</summary>
    public Thickness ShellPadding { get; set; }

    /// <summary>Gap between the amount and the currency switch: <c>gap: var(--space-2)</c>.</summary>
    public Thickness ShellGap { get; set; }

    /// <summary>Amount line box: <c>var(--text-3xl)</c> (36px) on a 1.2 line.</summary>
    public double AmountLineHeight { get; set; }

    /// <summary>Amount tracking: <c>letter-spacing: var(--tracking-display)</c> (-0.03em at 36px).</summary>
    public double AmountLetterSpacing { get; set; }

    /// <summary>Ghost digits: <c>color-mix(in oklab, var(--text-muted) 55%, transparent)</c>.</summary>
    public Color GhostDigitColor { get; set; }

    /// <summary>Currency switch: 36px pill, <c>padding: 0 8px 0 12px</c>, 4px gap, 16px chevron.</summary>
    public double CurrencyButtonHeight { get; set; }

    public Thickness CurrencyButtonPadding { get; set; }

    public double CurrencyButtonGap { get; set; }

    public double ChevronSize { get; set; }

    /// <summary>Single currency code: <c>.codeStatic { padding: 0 10px }</c>.</summary>
    public Thickness StaticCodePadding { get; set; }

    /// <summary>Currency menu: 256 wide, 6px padding, list max height 288, 8px below the shell.</summary>
    public double MenuWidth { get; set; }

    public Thickness MenuPadding { get; set; }

    public double MenuListMaxHeight { get; set; }

    public double MenuOffset { get; set; }

    /// <summary>Menu rows: min height 36, radius 14, <c>padding: 0 10px</c>, 10px gap, 2px between rows, 22px symbol, 16px check.</summary>
    public double OptionMinHeight { get; set; }

    public CornerRadius OptionRadius { get; set; }

    public Thickness OptionPadding { get; set; }

    public double OptionGap { get; set; }

    public double OptionSpacing { get; set; }

    public double OptionSymbolWidth { get; set; }

    public double CheckSize { get; set; }

    /// <summary>Gliding row highlight: <c>color-mix(in oklab, var(--foreground) 6.5%, transparent)</c>.</summary>
    public Color HighlightFill { get; set; }

    /// <summary>Quick add chips: 30px pill, <c>padding: 0 12px</c>, 6px apart, 0.45 opacity at the maximum.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipSpacing { get; set; }

    public double ChipMutedOpacity { get; set; }

    /// <summary>Toolbar: <c>padding-top: var(--space-3)</c>, <c>gap: var(--space-2) var(--space-3)</c>.</summary>
    public Thickness ToolbarPadding { get; set; }

    public double ToolbarRowGap { get; set; }

    public double ToolbarColumnGap { get; set; }

    /// <summary>Keyboard focus ring of the switch and chips: 2px, 2px outside.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ShellHeight = 76;
        ShellPadding = new Thickness(18, 0, 8, 0);
        ShellGap = new Thickness(8, 0, 0, 0);
        AmountLineHeight = 36 * 1.2;
        AmountLetterSpacing = -0.03 * 36;
        GhostDigitColor = Oklch.WithAlpha(n.TextMuted, 0.55);
        CurrencyButtonHeight = 36;
        CurrencyButtonPadding = new Thickness(12, 0, 8, 0);
        CurrencyButtonGap = 4;
        ChevronSize = 16;
        StaticCodePadding = new Thickness(10, 0);
        MenuWidth = 256;
        MenuPadding = new Thickness(6);
        MenuListMaxHeight = 288;
        MenuOffset = 8;
        OptionMinHeight = 36;
        OptionRadius = new CornerRadius(14);
        OptionPadding = new Thickness(10, 0);
        OptionGap = 10;
        OptionSpacing = 2;
        OptionSymbolWidth = 22;
        CheckSize = 16;
        HighlightFill = Oklch.WithAlpha(n.Foreground, 0.065);
        ChipHeight = 30;
        ChipPadding = new Thickness(12, 0);
        ChipSpacing = 6;
        ChipMutedOpacity = 0.45;
        ToolbarPadding = new Thickness(0, 12, 0, 0);
        ToolbarRowGap = 8;
        ToolbarColumnGap = 12;
        FocusRingMargin = new Thickness(-4);
    }
}
