using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The billing switch of <see cref="PricingCalculator"/> (Arc pricing-calculator, design reference, notes 3.1, 6.4 and 7):
/// a pill groove (foreground at 6%, 3px inset) with two options, the yearly one followed by a green note; a surface
/// thumb with a resting shadow and a 1px inner border glides under the chosen option, moving and resizing on the Settle
/// spring (Arc's FLIP measured ω 16.8, critically damped). Any arrow key chooses the other option and moves focus to it;
/// only the chosen option is a tab stop. Built in code for the calculator, so it has its own theme.
/// </summary>
[TemplatePart(PanelPart, typeof(PricingCalculatorBillingPanel))]
[TemplatePart(ThumbPart, typeof(GlidingIndicator))]
[TemplatePart(MonthlyPart, typeof(PricingCalculatorBillingOption))]
[TemplatePart(YearlyPart, typeof(PricingCalculatorBillingOption))]
internal sealed class PricingCalculatorBillingSwitch : TemplatedControl
{
    public const string PanelPart = "PART_Panel";
    public const string ThumbPart = "PART_Thumb";
    public const string MonthlyPart = "PART_Monthly";
    public const string YearlyPart = "PART_Yearly";

    /// <summary>Defines the <see cref="IsYearly"/> property.</summary>
    public static readonly StyledProperty<bool> IsYearlyProperty =
        AvaloniaProperty.Register<PricingCalculatorBillingSwitch, bool>(nameof(IsYearly), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="MonthlyText"/> property.</summary>
    public static readonly StyledProperty<string?> MonthlyTextProperty =
        AvaloniaProperty.Register<PricingCalculatorBillingSwitch, string?>(nameof(MonthlyText), "Monthly");

    /// <summary>Defines the <see cref="YearlyText"/> property.</summary>
    public static readonly StyledProperty<string?> YearlyTextProperty =
        AvaloniaProperty.Register<PricingCalculatorBillingSwitch, string?>(nameof(YearlyText), "Annual");

    /// <summary>Defines the <see cref="YearlyNote"/> property.</summary>
    public static readonly StyledProperty<string?> YearlyNoteProperty =
        AvaloniaProperty.Register<PricingCalculatorBillingSwitch, string?>(nameof(YearlyNote));

    /// <summary>Defines the <see cref="IsUniform"/> property.</summary>
    public static readonly StyledProperty<bool> IsUniformProperty =
        AvaloniaProperty.Register<PricingCalculatorBillingSwitch, bool>(nameof(IsUniform));

    private PricingCalculatorBillingPanel? _panel;
    private GlidingIndicator? _thumb;
    private PricingCalculatorBillingOption? _monthly;
    private PricingCalculatorBillingOption? _yearly;
    private Size _lastSize;
    private Rect _lastTarget;

    /// <summary>True: yearly billing is chosen.</summary>
    public bool IsYearly { get => GetValue(IsYearlyProperty); set => SetValue(IsYearlyProperty, value); }

    /// <summary>Text of the monthly option.</summary>
    public string? MonthlyText { get => GetValue(MonthlyTextProperty); set => SetValue(MonthlyTextProperty, value); }

    /// <summary>Text of the yearly option.</summary>
    public string? YearlyText { get => GetValue(YearlyTextProperty); set => SetValue(YearlyTextProperty, value); }

    /// <summary>The green note after the yearly option (empty hides it).</summary>
    public string? YearlyNote { get => GetValue(YearlyNoteProperty); set => SetValue(YearlyNoteProperty, value); }

    /// <summary>True: the options share the width equally (the compact layout stretches the switch).</summary>
    public bool IsUniform { get => GetValue(IsUniformProperty); set => SetValue(IsUniformProperty, value); }

    /// <summary>The thumb (tests read its rectangle).</summary>
    internal GlidingIndicator? Thumb => _thumb;

    internal PricingCalculatorBillingOption? MonthlyOption => _monthly;

    internal PricingCalculatorBillingOption? YearlyOption => _yearly;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.Arranged = null;
        }
        if (_monthly is not null)
        {
            _monthly.Click -= OnMonthlyClick;
        }
        if (_yearly is not null)
        {
            _yearly.Click -= OnYearlyClick;
        }
        _panel = e.NameScope.Find<PricingCalculatorBillingPanel>(PanelPart);
        _thumb = e.NameScope.Find<GlidingIndicator>(ThumbPart);
        _monthly = e.NameScope.Find<PricingCalculatorBillingOption>(MonthlyPart);
        _yearly = e.NameScope.Find<PricingCalculatorBillingOption>(YearlyPart);
        if (_panel is not null)
        {
            _panel.Arranged = OnPanelArranged;
            _panel.IsUniform = IsUniform;
        }
        if (_monthly is not null)
        {
            _monthly.Click += OnMonthlyClick;
        }
        if (_yearly is not null)
        {
            _yearly.Click += OnYearlyClick;
        }
        SyncOptions();
    }

    private void OnMonthlyClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsYearlyProperty, false);

    private void OnYearlyClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsYearlyProperty, true);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsYearlyProperty)
        {
            SyncOptions();
            _panel?.InvalidateArrange();
        }
        else if (change.Property == IsUniformProperty && _panel is not null)
        {
            _panel.IsUniform = IsUniform;
        }
    }

    private void SyncOptions()
    {
        if (_monthly is null || _yearly is null)
        {
            return;
        }
        _monthly.IsChecked = !IsYearly;
        _yearly.IsChecked = IsYearly;
        _monthly.IsTabStop = !IsYearly;
        _yearly.IsTabStop = IsYearly;
    }

    private void OnPanelArranged(Size size)
    {
        var option = IsYearly ? _yearly : _monthly;
        if (_thumb is null || option is null)
        {
            return;
        }
        var resized = size != _lastSize;
        _lastSize = size;
        var target = option.Bounds;
        if (_thumb.HasTarget && target == _lastTarget)
        {
            return;
        }
        _lastTarget = target;
        // A resize of the switch (layout change) snaps; a change of option glides on Settle.
        _thumb.MoveTo(target, ArcMotionPresets.Settle, animate: !resized);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            SetCurrentValue(IsYearlyProperty, !IsYearly);
            (IsYearly ? _yearly : _monthly)?.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }
}

/// <summary>The options row of the billing switch: the thumb under everything, options side by side (or equal halves).</summary>
internal sealed class PricingCalculatorBillingPanel : Panel
{
    private bool _isUniform;

    /// <summary>True: the options share the width equally.</summary>
    public bool IsUniform
    {
        get => _isUniform;
        set
        {
            _isUniform = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Called after every arrange with the panel size (the switch moves its thumb).</summary>
    public Action<Size>? Arranged { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var widest = 0.0;
        var count = 0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            if (child is GlidingIndicator)
            {
                continue;
            }
            count++;
            width += child.DesiredSize.Width;
            widest = Math.Max(widest, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        if (_isUniform)
        {
            width = double.IsInfinity(availableSize.Width) ? widest * count : availableSize.Width;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var options = Children.Where(c => c is not GlidingIndicator).ToList();
        var x = 0.0;
        var share = options.Count == 0 ? 0 : finalSize.Width / options.Count;
        foreach (var child in options)
        {
            var width = _isUniform ? share : child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }
        foreach (var child in Children)
        {
            if (child is GlidingIndicator)
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        Arranged?.Invoke(finalSize);
        return finalSize;
    }
}

/// <summary>One option of the billing switch: its text and an optional green note; foreground while chosen.</summary>
internal sealed class PricingCalculatorBillingOption : RadioButton
{
    /// <summary>Defines the <see cref="Note"/> property.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<PricingCalculatorBillingOption, string?>(nameof(Note));

    protected override Type StyleKeyOverride => typeof(PricingCalculatorBillingOption);

    /// <summary>The note after the text (the yearly saving).</summary>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == NoteProperty)
        {
            var text = Content?.ToString();
            AutomationProperties.SetName(this, string.IsNullOrEmpty(Note) ? text : $"{text} {Note}");
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Arrows belong to the switch (it moves the choice); the radio button must not handle them.
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            return;
        }
        base.OnKeyDown(e);
    }
}
