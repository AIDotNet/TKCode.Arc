using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One legend entry of a <see cref="SplitBar"/>: a toggle button with the part's swatch, its name and its share. Pressing
/// it switches the part off or back on; pointing at it highlights the part in the bar. A part that is off shows a hollow
/// swatch, a struck-through name and the word Off. The entry for the empty rest is plain text with a hollow swatch.
/// </summary>
[TemplatePart("PART_Label", typeof(Control))]
[TemplatePart("PART_Swatch", typeof(SplitBarSwatch))]
[TemplatePart("PART_Name", typeof(TextBlock))]
[TemplatePart("PART_Share", typeof(RollingDigits))]
[TemplatePart("PART_Off", typeof(TextBlock))]
[PseudoClasses(":off", ":highlighted", ":remainder")]
internal sealed class SplitBarLegendItem : TemplatedControl
{
    private readonly SplitBar _owner;
    private readonly SplitBarSlot? _slot;
    private readonly TranslateTransform _shake = new();
    private Control? _label;
    private SplitBarSwatch? _swatch;
    private TextBlock? _name;
    private RollingDigits? _share;
    private TextBlock? _off;
    private string? _labelText;
    private double _shareValue;
    private double _value;
    private bool _isOff;
    private bool _shown;
    private bool _pressed;

    public SplitBarLegendItem(SplitBar owner, SplitBarSlot? slot)
    {
        _owner = owner;
        _slot = slot;
        Focusable = slot is not null;
        PseudoClasses.Set(":remainder", slot is null);
        if (slot is null)
        {
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        }
    }

    /// <summary>The part this entry stands for; null for the empty rest.</summary>
    public SplitBarSlot? Slot => _slot;

    /// <summary>The label shown (tests).</summary>
    public string? LabelText => _labelText;

    /// <summary>The share shown ("41.6%"), "Off" for a part that is off, null when values show instead (tests).</summary>
    public string? PercentText => _isOff ? "Off" : _owner.ShowsPercent ? SplitBar.FormatShare(_shareValue) : null;

    /// <summary>The value of the part (tests).</summary>
    public double ShownValue => _value;

    /// <summary>True while the part is off.</summary>
    public bool IsOff => _isOff;

    /// <summary>The horizontal shake of the swatch and name (px; tests).</summary>
    public double ShakeOffset => _shake.X;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<Control>("PART_Label");
        _swatch = e.NameScope.Find<SplitBarSwatch>("PART_Swatch");
        _name = e.NameScope.Find<TextBlock>("PART_Name");
        _share = e.NameScope.Find<RollingDigits>("PART_Share");
        _off = e.NameScope.Find<TextBlock>("PART_Off");
        if (_label is not null)
        {
            _label.RenderTransform = _shake;
        }
        if (_share is not null)
        {
            _share.WheelMotion = SplitBar.DigitMotion;
        }
        _shown = false;
        Apply(false);
    }

    public void Update(int colorIndex, bool animate)
    {
        if (_slot is null)
        {
            return;
        }
        _labelText = _slot.Segment.Label;
        _shareValue = _slot.BaseShare;
        _value = Math.Max(0, _slot.Segment.Value);
        _isOff = _slot.Segment.IsHidden;
        Apply(animate);
    }

    public void UpdateRemainder(string label, double value, double share, bool animate)
    {
        _labelText = label;
        _value = value;
        _shareValue = share;
        _isOff = false;
        Apply(animate);
    }

    private void Apply(bool animate)
    {
        PseudoClasses.Set(":off", _isOff);
        if (_swatch is not null)
        {
            _swatch.IsHollow = _isOff || _slot is null;
            _swatch.Brush = _slot is null ? null : _owner.Track?.CapBrush(_slot);
        }
        if (_name is not null)
        {
            _name.Text = _labelText;
            // The strike line is drawn in the strong border color, quieter than the muted name.
            _name.TextDecorations = _isOff ? Strike(_owner.Track?.Neutral2) : null;
        }
        if (_off is not null)
        {
            _off.IsVisible = _isOff;
        }
        if (_share is not null)
        {
            _share.IsVisible = !_isOff;
            if (!_isOff)
            {
                // The first figures show at once; later ones roll.
                _share.IsInstant = !_shown || !animate;
                if (_owner.ShowsPercent)
                {
                    _share.Decimals = 1;
                    _share.Prefix = null;
                    _share.Suffix = "%";
                    _share.Value = Math.Round(_shareValue * 1000) / 10;
                }
                else
                {
                    _share.Decimals = _owner.ValueDecimals;
                    _share.Prefix = _owner.ValuePrefix;
                    _share.Suffix = _owner.ValueSuffix;
                    _share.Value = _value;
                }
                _shown = true;
            }
        }
        if (_slot is not null)
        {
            AutomationProperties.SetName(this, _isOff
                ? $"{_labelText}, hidden"
                : $"{_labelText}, {SplitBar.FormatShare(_shareValue)}");
        }
    }

    private TextDecorationCollection? _strike;
    private IBrush? _strikeBrush;

    private TextDecorationCollection Strike(IBrush? brush)
    {
        if (_strike is null || !ReferenceEquals(brush, _strikeBrush))
        {
            _strikeBrush = brush;
            _strike = new TextDecorationCollection
            {
                new TextDecoration { Location = TextDecorationLocation.Strikethrough, Stroke = brush }
            };
        }
        return _strike;
    }

    public void SetHighlighted(bool highlighted) => PseudoClasses.Set(":highlighted", highlighted);

    public void SetShake(double offset) => _shake.X = offset;

    private int IndexOf() => _slot is null ? -1 : _owner.IndexOf(_slot);

    /// <summary>Switches the part (the last part that is on refuses).</summary>
    internal void Toggle()
    {
        var index = IndexOf();
        if (index >= 0)
        {
            _owner.Toggle(index);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _slot is not null && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            Toggle();
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_slot is not null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed)
        {
            _pressed = false;
            e.Handled = true;
            if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                Toggle();
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (_slot is null || e.Pointer.Type == PointerType.Touch || _slot.Segment.IsHidden)
        {
            return;
        }
        var index = IndexOf();
        if (index >= 0)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, index);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_slot is not null && _owner.HighlightedIndex >= 0 && _owner.HighlightedIndex == IndexOf())
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LegendItemPeer(this);

    private sealed class LegendItemPeer(SplitBarLegendItem owner) : ControlAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => owner.IsOff ? ToggleState.Off : ToggleState.On;

        public void Toggle() => owner.Toggle();

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => owner.Slot is not null;

        protected override bool IsControlElementCore() => owner.Slot is not null;
    }
}

/// <summary>Shared text settings of the split bar.</summary>
internal static class SplitBarText
{
    /// <summary>Tabular figures, so rolling and changing numbers do not shift their neighbours.</summary>
    public static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };
}
