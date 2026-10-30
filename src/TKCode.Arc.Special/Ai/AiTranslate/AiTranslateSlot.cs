using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A label of an <see cref="AiTranslate"/> card that reserves the width of its longest version (hidden copies of every
/// version share its one cell) and crossfades between versions inside that width, so nothing moves under the pointer.
/// The crossfade is <see cref="ArcContentSwap"/>'s (out 100ms, in 160ms; a short fade under reduced motion). The status
/// line's slot also carries the glyph that leads each version (the model's mark or the warning), so the glyph keeps to its
/// label whichever edge the version keeps to.
/// </summary>
internal sealed class AiTranslateSlot : Grid
{
    /// <summary>The leading glyph's size (0: versions have no glyph).</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<AiTranslateSlot, double>(nameof(GlyphSize));

    /// <summary>The space between the glyph and the text.</summary>
    public static readonly StyledProperty<double> GlyphGapProperty =
        AvaloniaProperty.Register<AiTranslateSlot, double>(nameof(GlyphGap), 6);

    private readonly ArcContentSwap _swap = new()
    {
        Mode = ContentSwapMode.Crossfade,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly List<TextBlock> _reserve = [];
    private string? _text;
    private Geometry? _glyph;
    private ArcDesignTokenKind _glyphColor;

    public AiTranslateSlot()
    {
        Children.Add(_swap);
    }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public double GlyphGap { get => GetValue(GlyphGapProperty); set => SetValue(GlyphGapProperty, value); }

    /// <summary>The version on screen.</summary>
    public string? Text
    {
        get => _text;
        set => Show(value, _glyph, _glyphColor);
    }

    /// <summary>The glyph leading the version on screen.</summary>
    public Geometry? Glyph => _glyph;

    /// <summary>The token color of that glyph.</summary>
    public ArcDesignTokenKind GlyphColor => _glyphColor;

    /// <summary>Which edge of the reserved width the version on screen keeps to (the status line keeps right on wide cards).</summary>
    public HorizontalAlignment ContentAlignment
    {
        get => _swap.HorizontalContentAlignment;
        set => _swap.HorizontalContentAlignment = value;
    }

    /// <summary>Shows a version led by <paramref name="glyph"/> in <paramref name="color"/>; the same version again does nothing.</summary>
    public void Show(string? text, Geometry? glyph, ArcDesignTokenKind color)
    {
        if (_text == text && ReferenceEquals(_glyph, glyph) && _glyphColor == color && _swap.Content is not null == text is not null)
        {
            return;
        }
        Build(text, glyph, color);
    }

    private void Build(string? text, Geometry? glyph, ArcDesignTokenKind color)
    {
        _text = text;
        _glyph = glyph;
        _glyphColor = color;
        if (text is null || glyph is null || GlyphSize <= 0)
        {
            _swap.Content = text;
            return;
        }
        var mark = new ArcGlyph { Data = glyph, Size = GlyphSize, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(mark, ArcGlyph.ForegroundProperty, color);
        AutomationProperties.SetAccessibilityView(mark, AccessibilityView.Raw);
        _swap.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = GlyphGap,
            Children = { mark, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    private double Lead => GlyphSize > 0 ? GlyphSize + GlyphGap : 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphSizeProperty || change.Property == GlyphGapProperty)
        {
            // The token values arrive after the parts are built: lay the glyph and the reserved widths out again.
            foreach (var block in _reserve)
            {
                block.Margin = new Thickness(Lead, 0, 0, 0);
            }
            if (_text is not null)
            {
                Build(_text, _glyph, _glyphColor);
            }
        }
    }

    /// <summary>The versions whose width the slot reserves.</summary>
    public void Reserve(IEnumerable<string> versions)
    {
        foreach (var block in _reserve)
        {
            Children.Remove(block);
        }
        _reserve.Clear();
        var lead = Lead;
        foreach (var version in versions.Distinct())
        {
            var block = new TextBlock
            {
                Text = version,
                Opacity = 0,
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(lead, 0, 0, 0),
            };
            AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
            _reserve.Add(block);
            Children.Insert(0, block);
        }
    }
}
