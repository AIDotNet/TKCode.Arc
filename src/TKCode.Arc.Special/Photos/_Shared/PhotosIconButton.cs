using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The round icon button the Photos controls lay over pictures (previous, next, close, zoom): a raised disc with a
/// Lucide glyph, a press scale and the shared keyboard focus ring, built on first keyboard focus.
/// </summary>
internal sealed class PhotosIconButton : Button
{
    /// <summary>The glyph drawn in the middle.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<PhotosIconButton, Geometry?>(nameof(Glyph));

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(PhotosIconButton);

    public PhotosIconButton()
    {
    }

    public PhotosIconButton(Geometry glyph, string name)
    {
        Glyph = glyph;
        AutomationProperties.SetName(this, name);
    }

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, ProductGalleryTokens.Key(ProductGalleryTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}
