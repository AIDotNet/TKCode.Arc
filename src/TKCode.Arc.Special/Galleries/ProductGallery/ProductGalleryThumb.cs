using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One thumbnail of a <see cref="ProductGallery"/>'s strip: a button showing a small copy of the picture, ringed in the
/// accent color while its picture is on the stage.
/// </summary>
[PseudoClasses(SelectedPseudoClass)]
internal sealed class ProductGalleryThumb : Button
{
    internal const string SelectedPseudoClass = ":selected";

    /// <summary>The thumbnail picture.</summary>
    public static readonly StyledProperty<ImageSource?> SourceProperty =
        AvaloniaProperty.Register<ProductGalleryThumb, ImageSource?>(nameof(Source));

    /// <summary>Whether this thumbnail's picture is on the stage.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ProductGalleryThumb, bool>(nameof(IsSelected));

    private LazyFocusRing? _focusRing;

    protected override Type StyleKeyOverride => typeof(ProductGalleryThumb);

    public ImageSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>The position of the thumbnail in the strip.</summary>
    internal int Index { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, ProductGalleryTokens.Key(ProductGalleryTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep15));
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
