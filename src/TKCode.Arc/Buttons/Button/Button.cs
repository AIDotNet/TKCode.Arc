using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
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
[PseudoClasses(LoadingPseudoClass, NarrowPseudoClass, WidePseudoClass)]
public class Button : AvaloniaButton
{
    public const string LoadingPseudoClass = ":loading";
    public const string NarrowPseudoClass = ":narrow";
    public const string WidePseudoClass = ":wide";

    #region 公共属性定义

    public static readonly StyledProperty<ButtonVariant> VariantProperty =
        AvaloniaProperty.Register<Button, ButtonVariant>(nameof(Variant), ButtonVariant.Primary);

    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<Button, ControlSize>(nameof(Size), ControlSize.Medium);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<Button, bool>(nameof(IsLoading));

    /// <summary>
    /// Anchors of popups and menus must not scale on press (the popup measures the pressed size);
    /// they answer with color only. Defaults to true.
    /// </summary>
    public static readonly StyledProperty<bool> IsPressScaleEnabledProperty =
        AvaloniaProperty.Register<Button, bool>(nameof(IsPressScaleEnabled), true);

    public ButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ControlSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

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

    #endregion

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
