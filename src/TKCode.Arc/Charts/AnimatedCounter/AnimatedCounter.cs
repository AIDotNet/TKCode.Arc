using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A number that rolls digit by digit like an odometer: only changed columns turn (in the direction the
/// whole number moved), separators slide in and out, and the label crossfades with a short rise. With
/// <see cref="AnimateOnView"/> every digit rolls up from zero the first time 60% of the counter is visible.
/// </summary>
[TemplatePart(DigitsPart, typeof(RollingDigits))]
public class AnimatedCounter : TemplatedControl
{
    public const string DigitsPart = "PART_Digits";

    /// <summary>The number to display; changes animate the digits to the new value.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnimatedCounter, double>(nameof(Value));

    /// <summary>Accessible label announced before the value.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AnimatedCounter, string?>(nameof(Label));

    /// <summary>Text placed before the number, such as a currency sign.</summary>
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<AnimatedCounter, string?>(nameof(Prefix));

    /// <summary>Text placed after the number, such as a unit or percent sign.</summary>
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<AnimatedCounter, string?>(nameof(Suffix));

    /// <summary>Digits after the decimal point (default 0); negative values count as 0.</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<AnimatedCounter, int>(nameof(Decimals));

    /// <summary>Roll every digit up from zero the first time the counter scrolls into view.</summary>
    public static readonly StyledProperty<bool> AnimateOnViewProperty =
        AvaloniaProperty.Register<AnimatedCounter, bool>(nameof(AnimateOnView));

    /// <summary>Formatting culture; fixed (en-US) by default so every machine renders the same digits.</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<AnimatedCounter, string>(nameof(CultureName), "en-US");

    internal static readonly DirectProperty<AnimatedCounter, bool> IsArmedProperty =
        AvaloniaProperty.RegisterDirect<AnimatedCounter, bool>(nameof(IsArmed), o => o.IsArmed);

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <inheritdoc cref="PrefixProperty"/>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    /// <inheritdoc cref="SuffixProperty"/>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public bool AnimateOnView { get => GetValue(AnimateOnViewProperty); set => SetValue(AnimateOnViewProperty, value); }
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    private bool _isArmed;
    internal bool IsArmed
    {
        get => _isArmed;
        private set => SetAndRaise(IsArmedProperty, ref _isArmed, value);
    }

    private ViewportVisibility? _visibility;
    private bool _revealed;
    private RollingDigits? _digits;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _digits = e.NameScope.Find<RollingDigits>(DigitsPart);
        UpdateAutomationName();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Arm();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
    }

    private void Arm()
    {
        if (!AnimateOnView || _revealed || _visibility is not null)
        {
            IsArmed = false;
            return;
        }
        IsArmed = true;
        _visibility = new ViewportVisibility(this, 0.6, once: true);
        _visibility.VisibilityChanged += (_, visible) =>
        {
            if (visible)
            {
                _revealed = true;
                IsArmed = false;
            }
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AnimateOnViewProperty && TopLevel.GetTopLevel(this) is not null)
        {
            Arm();
        }
        if (change.Property == ValueProperty || change.Property == PrefixProperty || change.Property == SuffixProperty ||
            change.Property == DecimalsProperty || change.Property == LabelProperty || change.Property == CultureNameProperty)
        {
            UpdateAutomationName();
        }
    }

    private void UpdateAutomationName()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(CultureName);
        var text = $"{Prefix}{Value.ToString("N" + Math.Max(0, Decimals), culture)}{Suffix}";
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Label) ? text : $"{Label}: {text}");
    }
}
