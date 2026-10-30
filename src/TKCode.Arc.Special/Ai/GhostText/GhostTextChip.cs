using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What the chip of a <see cref="GhostText"/> shows.</summary>
internal enum GhostTextChipState
{
    /// <summary>The model's mark and name.</summary>
    Idle,

    /// <summary>The mark and a shimmering "Thinking".</summary>
    Thinking,

    /// <summary>The mark, a Tab key cap and "to accept" (or "Tap to accept" on touch screens).</summary>
    Ready,

    /// <summary>The mark, a check and "Accepted".</summary>
    Accepted,

    /// <summary>The mark, an alert, "No suggestion" and "Retry".</summary>
    Error
}

/// <summary>
/// The small chip at the end of a <see cref="GhostText"/>: a pill button whose states crossfade in place (old out in
/// 100ms, new in 160ms) while its width follows on a 200ms tween anchored to its right edge. It never takes focus from the
/// field, except in the error state where Retry is its action and Tab reaches it.
/// </summary>
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
internal sealed class GhostTextChip : AvaloniaButton
{
    internal const string SwapPart = "PART_Swap";

    private ArcContentSwap? _swap;
    private object? _pending;

    static GhostTextChip()
    {
        FocusableProperty.OverrideDefaultValue<GhostTextChip>(false);
        IsTabStopProperty.OverrideDefaultValue<GhostTextChip>(false);
    }

    protected override Type StyleKeyOverride => typeof(GhostTextChip);

    /// <summary>The state on screen.</summary>
    internal GhostTextChipState State { get; private set; } = (GhostTextChipState)(-1);

    /// <summary>The Tab key cap of the ready state, when it shows.</summary>
    internal GhostTextKey? TabKey { get; private set; }

    /// <summary>The content on screen (tests).</summary>
    internal Control? Shown => _swap?.Content as Control ?? _pending as Control;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        if (_swap is not null && _pending is not null)
        {
            _swap.Content = _pending;
            _pending = null;
        }
    }

    /// <summary>Shows <paramref name="content"/> for <paramref name="state"/>, crossfading from what was there.</summary>
    internal void Show(GhostTextChipState state, Control content, GhostTextKey? tabKey)
    {
        State = state;
        TabKey = tabKey;
        var focusable = state == GhostTextChipState.Error;
        Focusable = focusable;
        IsTabStop = focusable;
        if (_swap is null)
        {
            _pending = content;
            return;
        }
        _swap.Content = content;
    }

    /// <summary>Builds a row of the chip: the 12px mark, then the given parts, 5px apart.</summary>
    internal static StackPanel Row(Geometry? mark, IBrush? markBrush, double markSize, double gap, params Control[] parts)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap, VerticalAlignment = VerticalAlignment.Center };
        var glyph = new ArcGlyph { Data = mark ?? ArcIcons.Sparkles, Size = markSize, VerticalAlignment = VerticalAlignment.Center };
        if (markBrush is not null)
        {
            glyph.Foreground = markBrush;
        }
        else
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        }
        row.Children.Add(glyph);
        foreach (var part in parts)
        {
            row.Children.Add(part);
        }
        return row;
    }

    /// <summary>A line of chip text in <paramref name="color"/>.</summary>
    internal static TextBlock Text(string text, ArcDesignTokenKind color, double fontSize, bool medium = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        if (medium)
        {
            ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return block;
    }

    /// <summary>A 12px glyph in <paramref name="color"/>.</summary>
    internal static ArcGlyph Glyph(Geometry data, ArcDesignTokenKind color, double size)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }
}

/// <summary>
/// A key cap (Tab, ⌘ →, Esc): a small rounded box with the key's name. <see cref="Press"/> presses it for 160ms (it scales
/// to 92% and darkens) when its key or the matching handle method fires; the press is a ControlTheme transition on the
/// <c>:struck</c> pseudo-class.
/// </summary>
[PseudoClasses(StruckPseudoClass)]
internal sealed class GhostTextKey : TemplatedControl
{
    internal const string StruckPseudoClass = ":struck";

    /// <summary>The key's name.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GhostTextKey, string?>(nameof(Text));

    private DispatcherTimer? _release;

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>True while the key shows pressed.</summary>
    internal bool IsStruck => PseudoClasses.Contains(StruckPseudoClass);

    /// <summary>Presses the cap for 160ms.</summary>
    internal void Press()
    {
        PseudoClasses.Set(StruckPseudoClass, true);
        _release ??= new DispatcherTimer(GhostTextMotion.KeyPress, DispatcherPriority.Normal, (_, _) =>
        {
            _release!.Stop();
            PseudoClasses.Set(StruckPseudoClass, false);
        });
        _release.Stop();
        _release.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _release?.Stop();
        PseudoClasses.Set(StruckPseudoClass, false);
    }
}
