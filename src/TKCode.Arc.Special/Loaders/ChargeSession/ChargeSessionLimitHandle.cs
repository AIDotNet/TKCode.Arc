using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// The charge limit marker of <see cref="ChargeSession"/> (Arc Charge session, design reference): a focusable slider whose
/// 64 × 44 touch target holds a 48 × 26 pill with the limit and a 1.5px stem down to the pack's top edge. It is placed over
/// the scene by <see cref="ChargeSessionHero"/>; the drags happen on the scene and the curve, so the marker itself only
/// takes keys: arrows ±1, Shift+arrows and Page Up / Down ±10, Home the minimum, End 100, each jumping at once. While a drag
/// holds it (<c>:held</c>) the pill lifts to 1.08 and inverts on the CSS spring; keyboard focus draws a square 2px ring
/// around the touch target and tints the pill.
/// </summary>
[TemplatePart("PART_Stem", typeof(Control))]
[TemplatePart("PART_Pill", typeof(Control))]
[PseudoClasses(HeldPseudoClass)]
internal sealed class ChargeSessionLimitHandle : RangeBase
{
    public const string HeldPseudoClass = ":held";

    /// <summary>The pill's text ("80%").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ChargeSessionLimitHandle, string?>(nameof(Text));

    /// <summary>How far the stem reaches below the marker's bottom edge, to the pack's top.</summary>
    public static readonly StyledProperty<double> StemOverhangProperty =
        AvaloniaProperty.Register<ChargeSessionLimitHandle, double>(nameof(StemOverhang));

    private bool _isHeld;
    private Control? _stem;
    private Control? _pill;

    static ChargeSessionLimitHandle()
    {
        FocusableProperty.OverrideDefaultValue<ChargeSessionLimitHandle>(true);
        IsTabStopProperty.OverrideDefaultValue<ChargeSessionLimitHandle>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ChargeSessionLimitHandle>(false);
        SmallChangeProperty.OverrideDefaultValue<ChargeSessionLimitHandle>(1);
        LargeChangeProperty.OverrideDefaultValue<ChargeSessionLimitHandle>(10);
        MaximumProperty.OverrideDefaultValue<ChargeSessionLimitHandle>(100);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public double StemOverhang { get => GetValue(StemOverhangProperty); set => SetValue(StemOverhangProperty, value); }

    /// <summary>Whether a drag holds the limit.</summary>
    public bool IsHeld
    {
        get => _isHeld;
        set
        {
            _isHeld = value;
            PseudoClasses.Set(HeldPseudoClass, value);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stem = e.NameScope.Find<Control>("PART_Stem");
        _pill = e.NameScope.Find<Control>("PART_Pill");
        UpdateStem();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StemOverhangProperty || change.Property == HeightProperty)
        {
            UpdateStem();
        }
    }

    private void UpdateStem()
    {
        if (_stem is null)
        {
            return;
        }
        // From the pill's bottom edge through the rest of the marker and on down to the pack's top edge.
        var pill = _pill is { } p && double.IsFinite(p.Height) ? p.Height : 26;
        var height = double.IsFinite(Height) ? Height : 44;
        _stem.Margin = new Thickness(0, pill, 0, 0);
        _stem.Height = Math.Max(0, height - pill + StemOverhang);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var step = shift ? LargeChange : SmallChange;
        double? next = e.Key switch
        {
            Key.Left or Key.Down => Value - step,
            Key.Right or Key.Up => Value + step,
            Key.PageDown => Value - LargeChange,
            Key.PageUp => Value + LargeChange,
            Key.Home => Minimum,
            Key.End => Maximum,
            _ => null,
        };
        if (next is { } value)
        {
            e.Handled = true;
            // The card watches Value (keys and assistive technology alike) and applies the rounded limit.
            SetCurrentValue(ValueProperty, Math.Clamp(Math.Round(value), Minimum, Maximum));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LimitPeer(this);

    private sealed class LimitPeer(ChargeSessionLimitHandle owner) : RangeBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => "Slider";
    }
}
