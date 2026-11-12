using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Visual weight of an Arc <see cref="Button"/>.</summary>
public enum ButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger,

    /// <summary>
    /// Just the content: no fill, no border, no padding and no minimum height, so the button is as large as its label or
    /// icon. Only the label color answers hover, press and disabled. <see cref="Button.Size"/> does not apply.
    /// </summary>
    Text
}

/// <summary>
/// Arc button: a clear, responsive action with quiet secondary states. Presses scale on a snappy spring,
/// label changes crossfade with a short blur while the width springs to fit, and <see cref="IsLoading"/>
/// shows a spinner while keeping keyboard focus (clicks are swallowed instead of disabling the button).
/// </summary>
[PseudoClasses(LoadingPseudoClass, NarrowPseudoClass, WidePseudoClass, RaisedPseudoClass, PrimaryPseudoClass, SecondaryPseudoClass,
    GhostPseudoClass, DangerPseudoClass, TextPseudoClass, SmallPseudoClass, LargePseudoClass, PressScalePseudoClass)]
public class Button : AvaloniaButton
{
    public const string LoadingPseudoClass = ":loading";
    public const string NarrowPseudoClass = ":narrow";
    public const string WidePseudoClass = ":wide";

    /// <summary><see cref="Variant"/> as a pseudo-class (the theme selects on these, not on the property).</summary>
    public const string PrimaryPseudoClass = ":primary";

    /// <inheritdoc cref="PrimaryPseudoClass"/>
    public const string SecondaryPseudoClass = ":secondary";

    /// <inheritdoc cref="PrimaryPseudoClass"/>
    public const string GhostPseudoClass = ":ghost";

    /// <inheritdoc cref="PrimaryPseudoClass"/>
    public const string DangerPseudoClass = ":danger";

    /// <inheritdoc cref="PrimaryPseudoClass"/>
    public const string TextPseudoClass = ":text";

    /// <summary><see cref="Size"/> Small or Large as a pseudo-class (Medium has none).</summary>
    public const string SmallPseudoClass = ":small";

    /// <inheritdoc cref="SmallPseudoClass"/>
    public const string LargePseudoClass = ":large";

    /// <summary>Set while <see cref="IsPressScaleEnabled"/> is true.</summary>
    public const string PressScalePseudoClass = ":press-scale";

    /// <summary>The button sits on a raised surface (<see cref="ArcSurface.ElevationProperty"/>): ghost hover uses a stronger fill.</summary>
    public const string RaisedPseudoClass = ":raised";

    #region 公共属性定义

    /// <summary>Visual style: Primary (default), Secondary, Ghost, Danger or Text (the content alone, without any chrome).</summary>
    public static readonly StyledProperty<ButtonVariant> VariantProperty =
        AvaloniaProperty.Register<Button, ButtonVariant>(nameof(Variant), ButtonVariant.Primary);

    /// <summary>Button height: Small, Medium (default) or Large.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<Button, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>Puts the button in its loading state while true.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<Button, bool>(nameof(IsLoading));

    /// <summary>
    /// Anchors of popups and menus must not scale on press (the popup measures the pressed size);
    /// they answer with color only. Defaults to true.
    /// </summary>
    public static readonly StyledProperty<bool> IsPressScaleEnabledProperty =
        AvaloniaProperty.Register<Button, bool>(nameof(IsPressScaleEnabled), true);

    /// <summary>
    /// How a new label replaces the old one: BlurSwap (default, a short blur), or Label for the quiet crossfade of Arc's
    /// AI blocks (100ms out, 160ms in rising 2px), or any other content swap mode.
    /// </summary>
    public static readonly StyledProperty<ContentSwapMode> LabelSwapModeProperty =
        AvaloniaProperty.Register<Button, ContentSwapMode>(nameof(LabelSwapMode), ContentSwapMode.BlurSwap);

    /// <summary>
    /// Spring the button's width follows when its label changes: Morph (default, a slight bounce), or Quick for a
    /// bounce-free 0.25s glide that pairs with the Label swap.
    /// </summary>
    public static readonly StyledProperty<ArcSpring> LabelSpringProperty =
        AvaloniaProperty.Register<Button, ArcSpring>(nameof(LabelSpring), ArcSpring.Morph);

    /// <inheritdoc cref="VariantProperty"/>
    public ButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool IsPressScaleEnabled
    {
        get => GetValue(IsPressScaleEnabledProperty);
        set => SetValue(IsPressScaleEnabledProperty, value);
    }

    /// <inheritdoc cref="LabelSwapModeProperty"/>
    public ContentSwapMode LabelSwapMode
    {
        get => GetValue(LabelSwapModeProperty);
        set => SetValue(LabelSwapModeProperty, value);
    }

    /// <inheritdoc cref="LabelSpringProperty"/>
    public ArcSpring LabelSpring
    {
        get => GetValue(LabelSpringProperty);
        set => SetValue(LabelSpringProperty, value);
    }

    #endregion

    public Button()
    {
        UpdateVariantPseudoClasses();
        UpdateSizePseudoClasses();
        PseudoClasses.Set(PressScalePseudoClass, IsPressScaleEnabled);
    }

    private void UpdateVariantPseudoClasses()
    {
        var variant = Variant;
        PseudoClasses.Set(PrimaryPseudoClass, variant == ButtonVariant.Primary);
        PseudoClasses.Set(SecondaryPseudoClass, variant == ButtonVariant.Secondary);
        PseudoClasses.Set(GhostPseudoClass, variant == ButtonVariant.Ghost);
        PseudoClasses.Set(DangerPseudoClass, variant == ButtonVariant.Danger);
        PseudoClasses.Set(TextPseudoClass, variant == ButtonVariant.Text);
    }

    private void UpdateSizePseudoClasses()
    {
        var size = Size;
        PseudoClasses.Set(SmallPseudoClass, size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, size == ControlSize.Large);
    }

    // Most buttons never load and are never focused by keyboard, so the spinner and the focus ring are built the first
    // time they are needed, not with every template.
    private ArcSpinner? _spinner;
    private LazyFocusRing? _focusRing;
    private bool _templated;
    private object _ringMarginKey = ButtonTokens.Key(ButtonTokenKind.FocusRingMargin);
    private object _ringRadiusKey = ButtonTokens.Key(ButtonTokenKind.FocusRingRadius);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // The extras sit after the template root, so they draw above it; a new template starts them over.
        RemoveExtra(_spinner);
        _spinner = null;
        _templated = true;
        if (IsLoading)
        {
            EnsureSpinner();
        }
        _focusRing ??= new LazyFocusRing(this, _ringMarginKey, _ringRadiusKey);
        _focusRing.Attach(AddExtra, RemoveExtra);
    }

    /// <summary>
    /// Resource keys of the focus ring's margin and corner radius, for a host that reuses the button with its own theme
    /// (a split button half has a ring with two round corners). The ring is rebuilt with the new keys when it is next shown.
    /// </summary>
    internal void SetFocusRingKeys(object marginKey, object radiusKey)
    {
        if (Equals(_ringMarginKey, marginKey) && Equals(_ringRadiusKey, radiusKey))
        {
            return;
        }
        _ringMarginKey = marginKey;
        _ringRadiusKey = radiusKey;
        if (_focusRing is null)
        {
            return;
        }
        _focusRing.Attach(null, null);
        _focusRing = new LazyFocusRing(this, marginKey, radiusKey);
        if (_templated)
        {
            _focusRing.Attach(AddExtra, RemoveExtra);
        }
    }

    /// <summary>Hosts a code-built part (spinner, focus ring) over the template; it is laid out over the whole button.</summary>
    private void AddExtra(Control part)
    {
        LogicalChildren.Add(part);
        VisualChildren.Add(part);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // The template lays out only its root; the extras follow the button's box and never change its size.
        _spinner?.Measure(availableSize);
        _focusRing?.Ring?.Measure(availableSize);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        var box = new Rect(size);
        _spinner?.Arrange(box);
        _focusRing?.Ring?.Arrange(box);
        return size;
    }

    private void RemoveExtra(Control? part)
    {
        if (part is null)
        {
            return;
        }
        LogicalChildren.Remove(part);
        VisualChildren.Remove(part);
    }

    private void EnsureSpinner()
    {
        if (!_templated)
        {
            return;
        }
        if (_spinner is null)
        {
            _spinner = new ArcSpinner
            {
                Name = "PART_Spinner",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            AddExtra(_spinner);
            var size = _spinner.GetResourceObservable(ButtonTokens.Key(ButtonTokenKind.SpinnerSize));
            _spinner.Bind(WidthProperty, size);
            _spinner.Bind(HeightProperty, size);
        }
        _spinner.IsVisible = IsLoading;
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

    protected override void OnClick()
    {
        // Loading swallows activation but keeps focus (aria-busy + aria-disabled instead of disabled).
        if (IsLoading)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
        {
            UpdateVariantPseudoClasses();
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizePseudoClasses();
        }
        else if (change.Property == IsPressScaleEnabledProperty)
        {
            PseudoClasses.Set(PressScalePseudoClass, IsPressScaleEnabled);
        }
        else if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(LoadingPseudoClass, IsLoading);
            if (IsLoading || _spinner is not null)
            {
                EnsureSpinner();
            }
        }
        else if (change.Property == ArcSurface.ElevationProperty)
        {
            PseudoClasses.Set(RaisedPseudoClass, ArcSurface.GetElevation(this) == ArcSurfaceElevation.Raised);
        }
        else if (change.Property == BoundsProperty)
        {
            // Press depth follows the width: icon-sized buttons press deeper, wide buttons shallower.
            var width = Bounds.Width;
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width <= 48);
            PseudoClasses.Set(WidePseudoClass, width > 220);
        }
    }
}
