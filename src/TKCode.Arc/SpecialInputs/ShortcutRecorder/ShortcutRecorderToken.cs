using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ShortcutRecorderToken : AbstractControlDesignToken
{
    /// <summary>The recorder fills the row: <c>padding: 0 6px 0 9px</c>.</summary>
    public Thickness RecorderPadding { get; set; }

    /// <summary>The frame content corner (<c>--radius-control</c> inside the 1px border), for the recording tint.</summary>
    public CornerRadius TintRadius { get; set; }

    /// <summary>Recording tint: <c>color-mix(in oklab, var(--accent) 4%, var(--surface))</c> as accent opacity over the surface.</summary>
    public double TintOpacity { get; set; }

    /// <summary>Key cap (Arc ShortcutKbd): min 26×26, <c>padding: 0 7px 1px</c>, 1px edge with a 2px bottom, radius 8.</summary>
    public double KeyCapHeight { get; set; }
    public double KeyCapMinWidth { get; set; }
    public Thickness KeyCapPadding { get; set; }
    public CornerRadius KeyCapRadius { get; set; }
    public double KeyCapBottomEdge { get; set; }

    /// <summary>Caps sit 4px apart (<c>.row { gap: 4px }</c>).</summary>
    public double KeyCapGap { get; set; }

    /// <summary>Warning tone of a pending conflict: <c>color-mix(warning 55%, border-strong)</c> edge on <c>color-mix(warning 12%, surface-raised)</c>.</summary>
    public Color KeyCapWarningEdge { get; set; }
    public Color KeyCapWarningFill { get; set; }

    /// <summary>The recording light: 7px, <c>margin: 0 6px 0 3px</c>.</summary>
    public double RecordingDotDiameter { get; set; }
    public Thickness RecordingDotMargin { get; set; }

    /// <summary>The placeholder sits 3px further in (<c>padding-left: 3px</c>).</summary>
    public double PlaceholderInset { get; set; }

    /// <summary>Reset and clear: 32px squares, <c>calc(var(--radius-control) - 5px)</c>, 16px glyphs.</summary>
    public double ActionButtonSize { get; set; }
    public CornerRadius ActionButtonRadius { get; set; }
    public double ActionGlyphSize { get; set; }

    /// <summary>The action slot: <c>gap: 2px; padding-right: 4px</c>.</summary>
    public double ActionsSpacing { get; set; }
    public Thickness ActionsMargin { get; set; }

    /// <summary>Message row: <c>padding-top: var(--space-2)</c>, 12px copy on a 1.4 line, <c>gap: 2px var(--space-3)</c>.</summary>
    public Thickness MessagePadding { get; set; }
    public double MessageLineHeight { get; set; }
    public double MessageColumnGap { get; set; }
    public double MessageRowGap { get; set; }

    /// <summary>The 6px warning dot before a conflict message (<c>margin: 0 7px 1px 1px</c>).</summary>
    public double WarningDotDiameter { get; set; }
    public Thickness WarningDotMargin { get; set; }

    /// <summary>Links: <c>text-underline-offset: 3px</c>.</summary>
    public double LinkUnderlineOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        RecorderPadding = new Thickness(9, 0, 6, 0);
        TintRadius = new CornerRadius(17);
        TintOpacity = 0.04;
        KeyCapHeight = 26;
        KeyCapMinWidth = 26;
        KeyCapPadding = new Thickness(7, 0, 7, 1);
        KeyCapRadius = new CornerRadius(8);
        KeyCapBottomEdge = 2;
        KeyCapGap = 4;
        KeyCapWarningEdge = Oklch.MixOklab(n.BorderStrong, n.Warning, 0.55);
        KeyCapWarningFill = Oklch.MixOklab(n.SurfaceRaised, n.Warning, 0.12);
        RecordingDotDiameter = 7;
        RecordingDotMargin = new Thickness(3, 0, 6, 0);
        PlaceholderInset = 3;
        ActionButtonSize = 32;
        ActionButtonRadius = new CornerRadius(13);
        ActionGlyphSize = 16;
        ActionsSpacing = 2;
        ActionsMargin = new Thickness(0, 0, 4, 0);
        MessagePadding = new Thickness(0, 8, 0, 0);
        MessageLineHeight = 12 * 1.4;
        MessageColumnGap = 12;
        MessageRowGap = 2;
        WarningDotDiameter = 6;
        WarningDotMargin = new Thickness(1, 0, 7, 1);
        LinkUnderlineOffset = 3;
    }
}
