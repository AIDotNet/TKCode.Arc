using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One photo of a <see cref="PhotoGrid"/>: the picture cropped into a rounded face, an accent ring while it is selected
/// and the shared keyboard focus ring. It is laid out at its final place; while the grid reflows, a render transform
/// carries it from where it was (the grid writes it every frame).
/// </summary>
internal sealed class PhotoGridTile : Panel
{
    private readonly Border _face;
    private readonly ArcImage _image;
    private readonly Border _ring;
    private readonly LazyFocusRing _focusRing;

    static PhotoGridTile()
    {
        FocusableProperty.OverrideDefaultValue<PhotoGridTile>(true);
        FocusAdornerProperty.OverrideDefaultValue<PhotoGridTile>(null);
    }

    public PhotoGridTile(PhotoGrid owner)
    {
        Owner = owner;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = Transform;
        _image = new ArcImage { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
        _image.PropertyChanged += OnImagePropertyChanged;
        _face = new Border { ClipToBounds = true, Child = _image }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(_face, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _ring = new Border { IsHitTestVisible = false, IsVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(_ring, Border.BorderBrushProperty, ArcDesignTokenKind.Accent);
        _ring.Bind(Border.BorderThicknessProperty, _ring.GetResourceObservable(PhotoGridTokens.Key(PhotoGridTokenKind.SelectedRingThickness)));
        Children.Add(_face);
        Children.Add(_ring);
        _focusRing = new LazyFocusRing(this, PhotoGridTokens.Key(PhotoGridTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep15));
        _focusRing.Attach(this);
    }

    internal PhotoGrid Owner { get; }

    internal PhotoGridPhoto? Photo { get; private set; }

    /// <summary>The transform the grid writes while the photo travels.</summary>
    internal MatrixTransform Transform { get; } = new();

    /// <summary>The place the photo is laid out at (the end of a reflow).</summary>
    internal Rect Target;

    /// <summary>Where a reflow started from.</summary>
    internal Rect From;

    /// <summary>Where the photo is drawn now.</summary>
    internal Rect Shown;

    /// <summary>True while the photo is entering (it fades in at its place) or leaving (it fades out where it is).</summary>
    internal bool IsEntering;

    internal bool IsLeaving;

    internal int Index { get; set; }

    /// <summary>The proportions of the loaded picture, or 0 before it has loaded.</summary>
    internal double LoadedRatio
    {
        get
        {
            var size = _image.CurrentImage?.Size ?? default;
            return size.Width > 0 && size.Height > 0 ? size.Width / size.Height : 0;
        }
    }

    internal bool IsSelected
    {
        get => _ring.IsVisible;
        set
        {
            _ring.IsVisible = value;
            AutomationProperties.SetHelpText(this, value ? "Selected" : null);
        }
    }

    internal void SetPhoto(PhotoGridPhoto photo)
    {
        Photo = photo;
        _image.Source = photo.Source;
    }

    private void OnImagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ArcImage.CurrentImageProperty && Photo is { AspectRatio: <= 0 })
        {
            Owner.OnRatioDiscovered();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing.Update();
        Owner.OnTileFocused(this);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing.Update();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !IsLeaving)
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && !IsLeaving && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Owner.Toggle(this);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PhotoGridTileAutomationPeer(this);

    private sealed class PhotoGridTileAutomationPeer(PhotoGridTile owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}
