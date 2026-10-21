using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One setting: an optional icon, a header and description on the left, and the control that changes it
/// (<see cref="ContentControl.Content"/>) on the right. Below <see cref="StackWidth"/> the control moves under the text.
/// Rows in a <see cref="SettingsGroup"/> (or any panel) are separated by a hairline; the first row has none. When the
/// control is a toggle (Switch, Checkbox), clicking the header or description toggles it too, and the control takes
/// the header as its accessible name and the description as its help text unless it has its own.
/// </summary>
[TemplatePart(LabelPart, typeof(Control))]
[PseudoClasses(StackedPseudoClass, IconPseudoClass)]
public class SettingsRow : ContentControl
{
    public const string LabelPart = "PART_Label";
    public const string StackedPseudoClass = ":stacked";
    public const string IconPseudoClass = ":icon";

    /// <summary>The setting's name, shown in medium weight; also the control's accessible name.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsRow, string?>(nameof(Header));

    /// <summary>A sentence under the header that explains the setting; also the control's help text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsRow, string?>(nameof(Description));

    /// <summary>A 24×24 stroke icon (for example an <see cref="ArcIcons"/> member) shown in a tile before the header.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SettingsRow, Geometry?>(nameof(Icon));

    /// <summary>Below this width the control moves under the header and description (default 440).</summary>
    public static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<SettingsRow, double>(nameof(StackWidth), 440);

    /// <summary>Whether the control currently sits under the text (the row is narrower than <see cref="StackWidth"/>).</summary>
    public static readonly DirectProperty<SettingsRow, bool> IsStackedProperty =
        AvaloniaProperty.RegisterDirect<SettingsRow, bool>(nameof(IsStacked), o => o.IsStacked);

    private Control? _label;
    private Control? _namedControl;
    private string? _assignedName;
    private string? _assignedHelp;
    private bool _isStacked;
    private bool _labelPressed;

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="StackWidthProperty"/>
    public double StackWidth
    {
        get => GetValue(StackWidthProperty);
        set => SetValue(StackWidthProperty, value);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked
    {
        get => _isStacked;
        private set
        {
            if (SetAndRaise(IsStackedProperty, ref _isStacked, value))
            {
                PseudoClasses.Set(StackedPseudoClass, value);
            }
        }
    }

    protected override Type StyleKeyOverride => typeof(SettingsRow);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_label is not null)
        {
            _label.PointerPressed -= OnLabelPointerPressed;
            _label.PointerReleased -= OnLabelPointerReleased;
            _label.PointerCaptureLost -= OnLabelPointerCaptureLost;
        }
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<Control>(LabelPart);
        if (_label is not null)
        {
            _label.PointerPressed += OnLabelPointerPressed;
            _label.PointerReleased += OnLabelPointerReleased;
            _label.PointerCaptureLost += OnLabelPointerCaptureLost;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width is set by the parent, not by the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            IsStacked = availableSize.Width < StackWidth;
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            PseudoClasses.Set(IconPseudoClass, Icon is not null);
        }
        else if (change.Property == StackWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == ContentProperty)
        {
            ReleaseAccessibleText();
            ApplyAccessibleText();
        }
        else if (change.Property == HeaderProperty || change.Property == DescriptionProperty)
        {
            ApplyAccessibleText();
        }
    }

    /// <summary>Gives the control the header as its name and the description as its help text, unless it has its own.</summary>
    private void ApplyAccessibleText()
    {
        if (Content is not Control control)
        {
            return;
        }
        _namedControl = control;
        var name = AutomationProperties.GetName(control);
        if (string.IsNullOrEmpty(name) || name == _assignedName)
        {
            _assignedName = Header;
            AutomationProperties.SetName(control, Header);
        }
        var help = AutomationProperties.GetHelpText(control);
        if (string.IsNullOrEmpty(help) || help == _assignedHelp)
        {
            _assignedHelp = Description;
            AutomationProperties.SetHelpText(control, Description);
        }
    }

    /// <summary>Takes back the text this row gave a control that is no longer its content.</summary>
    private void ReleaseAccessibleText()
    {
        if (_namedControl is null)
        {
            return;
        }
        if (_assignedName is not null && AutomationProperties.GetName(_namedControl) == _assignedName)
        {
            _namedControl.ClearValue(AutomationProperties.NameProperty);
        }
        if (_assignedHelp is not null && AutomationProperties.GetHelpText(_namedControl) == _assignedHelp)
        {
            _namedControl.ClearValue(AutomationProperties.HelpTextProperty);
        }
        _namedControl = null;
        _assignedName = null;
        _assignedHelp = null;
    }

    private void OnLabelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Content is ToggleButton && e.GetCurrentPoint(_label).Properties.IsLeftButtonPressed)
        {
            _labelPressed = true;
            e.Pointer.Capture(_label);
            e.Handled = true;
        }
    }

    private void OnLabelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_labelPressed || _label is null)
        {
            return;
        }
        _labelPressed = false;
        e.Pointer.Capture(null);
        var inside = new Rect(_label.Bounds.Size).Contains(e.GetPosition(_label));
        if (inside && Content is ToggleButton toggle && toggle.IsEffectivelyEnabled)
        {
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, toggle.IsChecked != true);
            e.Handled = true;
        }
    }

    private void OnLabelPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _labelPressed = false;
}
