using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
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
    Danger
}

/// <summary>
/// Arc button: a clear, responsive action with quiet secondary states. Presses scale on a snappy spring,
/// label changes crossfade with a short blur while the width springs to fit, and <see cref="IsLoading"/>
/// shows a spinner while keeping keyboard focus (clicks are swallowed instead of disabling the button).
/// </summary>
[PseudoClasses(LoadingPseudoClass, NarrowPseudoClass, WidePseudoClass, RaisedPseudoClass)]
[TemplatePart("PART_Layers", typeof(Panel))]
public class Button : AvaloniaButton
{
    public const string LoadingPseudoClass = ":loading";
    public const string NarrowPseudoClass = ":narrow";
    public const string WidePseudoClass = ":wide";

    /// <summary>The button sits on a raised surface (<see cref="ArcSurface.ElevationProperty"/>): ghost hover uses a stronger fill.</summary>
    public const string RaisedPseudoClass = ":raised";

    #region 公共属性定义

    /// <summary>Visual style: Primary (default), Secondary, Ghost or Danger.</summary>
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

    // Most buttons never load, so the spinner is built the first time IsLoading turns true, not with every template.
    private Panel? _content;
    private ArcSpinner? _spinner;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _content = e.NameScope.Find<Panel>("PART_Layers");
        _spinner = null;
        if (IsLoading)
        {
            EnsureSpinner();
        }
    }

    private void EnsureSpinner()
    {
        if (_content is null)
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
            _content.Children.Add(_spinner);
            var size = _spinner.GetResourceObservable(ButtonTokens.Key(ButtonTokenKind.SpinnerSize));
            _spinner.Bind(WidthProperty, size);
            _spinner.Bind(HeightProperty, size);
        }
        _spinner.IsVisible = IsLoading;
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
        if (change.Property == IsLoadingProperty)
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
