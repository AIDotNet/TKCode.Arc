using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One tile of a <see cref="LightboxGallery"/>'s grid: a button showing the picture cropped to the tile, which grows a
/// little under the pointer. Its face is what the lightbox flies out of and back into.
/// </summary>
internal sealed class LightboxGalleryTile : Button
{
    internal const string FacePart = "PART_Face";

    /// <summary>The picture of the tile.</summary>
    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<LightboxGalleryTile, ImageSource?>(nameof(Source));

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(LightboxGalleryTile);

    public ImageSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The position of the tile in the grid.</summary>
    internal int Index { get; set; }

    /// <summary>The rounded face holding the picture (the flight's source).</summary>
    internal Border? Face { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Face = e.NameScope.Find<Border>(FacePart);
        if (Face is not null)
        {
            SharedElement.SetKey(Face, LightboxGallery.FlightKey);
        }
        _focusRing ??= new LazyFocusRing(this, LightboxGalleryTokens.Key(LightboxGalleryTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep19));
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
