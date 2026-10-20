using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaCheckBox = Avalonia.Controls.CheckBox;

namespace TKCode.Arc;

/// <summary>The three states of a <see cref="Checkbox"/> (Radix <c>CheckedState</c>).</summary>
public enum CheckboxState
{
    Unchecked,
    Checked,

    /// <summary>Some but not all children are checked; the check morphs into a dash.</summary>
    Indeterminate
}

/// <summary>Arguments of <see cref="Checkbox.CheckedStateChanged"/>.</summary>
public sealed class CheckboxStateChangedEventArgs : RoutedEventArgs
{
    public CheckboxStateChangedEventArgs(RoutedEvent routedEvent, CheckboxState oldState, CheckboxState newState)
        : base(routedEvent)
    {
        OldState = oldState;
        NewState = newState;
    }

    public CheckboxState OldState { get; }

    public CheckboxState NewState { get; }
}

/// <summary>
/// Arc checkbox: a binary choice with a precise, legible state. The fill grows in on a snappy spring, the check draws
/// itself, and the indeterminate state morphs the same three-point path into a dash and back. Only the 18px square
/// answers a press (it dips to 0.95); the 44px hit area and the label never move. Clicking an indeterminate box
/// checks it (Radix behavior). <see cref="ToggleButton.IsChecked"/> is the tri-state value (null = indeterminate);
/// <see cref="CheckedState"/> is the same value as an enum.
/// </summary>
[TemplatePart(MarkPart, typeof(Control))]
[PseudoClasses(InvalidPseudoClass)]
public class Checkbox : AvaloniaCheckBox
{
    public const string MarkPart = "PART_Mark";
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>Accessible name used when the checkbox has no label (Arc: "Checkbox").</summary>
    public const string FallbackName = "Checkbox";

    #region 公共属性定义

    /// <summary>Secondary text under the label; also the help text read by assistive technology.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Checkbox, string?>(nameof(Description));

    /// <summary>Marks the choice as invalid (the square takes the danger border).</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<Checkbox, bool>(nameof(IsInvalid));

    /// <summary>The field must be checked before a form is submitted (reported to assistive technology).</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<Checkbox, bool>(nameof(IsRequired));

    /// <summary>The tri-state value as an enum; kept in sync with <see cref="ToggleButton.IsChecked"/>.</summary>
    public static readonly StyledProperty<CheckboxState> CheckedStateProperty =
        AvaloniaProperty.Register<Checkbox, CheckboxState>(nameof(CheckedState), defaultBindingMode: BindingMode.TwoWay);

    public static readonly RoutedEvent<CheckboxStateChangedEventArgs> CheckedStateChangedEvent =
        RoutedEvent.Register<Checkbox, CheckboxStateChangedEventArgs>(nameof(CheckedStateChanged), RoutingStrategies.Bubble);

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsInvalid
    {
        get => GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    public CheckboxState CheckedState
    {
        get => GetValue(CheckedStateProperty);
        set => SetValue(CheckedStateProperty, value);
    }

    /// <summary>Raised when the state changes, with the old and new <see cref="CheckboxState"/>.</summary>
    public event EventHandler<CheckboxStateChangedEventArgs>? CheckedStateChanged
    {
        add => AddHandler(CheckedStateChangedEvent, value);
        remove => RemoveHandler(CheckedStateChangedEvent, value);
    }

    #endregion

    private CheckboxMark? _mark;
    private bool _syncing;
    private bool _usesFallbackName;

    protected override Type StyleKeyOverride => typeof(Checkbox);

    public Checkbox()
    {
        UpdateFallbackName();
    }

    /// <summary>Unchecked and indeterminate both become checked; checked becomes unchecked (Radix). With
    /// <see cref="ToggleButton.IsThreeState"/> the Avalonia cycle (unchecked, checked, indeterminate) is used instead.</summary>
    protected override void Toggle()
    {
        if (IsThreeState)
        {
            base.Toggle();
            return;
        }
        SetCurrentValue(IsCheckedProperty, IsChecked != true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // WAI-ARIA (and Radix): a checkbox does not activate on Enter; the key keeps bubbling (form submit).
        if (e.Key == Key.Enter)
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<CheckboxMark>(MarkPart);
        _mark?.Apply(ToState(IsChecked), animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            var oldState = ToState(change.GetOldValue<bool?>());
            var newState = ToState(change.GetNewValue<bool?>());
            if (!_syncing)
            {
                _syncing = true;
                SetCurrentValue(CheckedStateProperty, newState);
                _syncing = false;
            }
            _mark?.Apply(newState, animate: true);
            if (oldState != newState)
            {
                RaiseEvent(new CheckboxStateChangedEventArgs(CheckedStateChangedEvent, oldState, newState));
            }
        }
        else if (change.Property == CheckedStateProperty)
        {
            if (!_syncing)
            {
                _syncing = true;
                SetCurrentValue(IsCheckedProperty, FromState(change.GetNewValue<CheckboxState>()));
                _syncing = false;
            }
        }
        else if (change.Property == DescriptionProperty)
        {
            AutomationProperties.SetHelpText(this, Description);
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == IsRequiredProperty)
        {
            AutomationProperties.SetIsRequiredForForm(this, IsRequired);
        }
        else if (change.Property == ContentProperty)
        {
            UpdateFallbackName();
        }
    }

    private void UpdateFallbackName()
    {
        var name = AutomationProperties.GetName(this);
        if (Content is null)
        {
            if (string.IsNullOrEmpty(name))
            {
                AutomationProperties.SetName(this, FallbackName);
                _usesFallbackName = true;
            }
        }
        else if (_usesFallbackName && name == FallbackName)
        {
            ClearValue(AutomationProperties.NameProperty);
            _usesFallbackName = false;
        }
    }

    internal static CheckboxState ToState(bool? value) => value switch
    {
        true => CheckboxState.Checked,
        false => CheckboxState.Unchecked,
        null => CheckboxState.Indeterminate
    };

    internal static bool? FromState(CheckboxState state) => state switch
    {
        CheckboxState.Checked => true,
        CheckboxState.Unchecked => false,
        _ => null
    };
}
