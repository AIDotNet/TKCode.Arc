using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A ribbon command that stays on or off (bold, word wrap, snap to grid): a click flips <see cref="IsChecked"/>, then runs
/// the command like a <see cref="RibbonButton"/>. While it is on the button is filled with the accent and outlined.
/// </summary>
public class RibbonToggleButton : RibbonButton
{
    #region 公共属性定义

    /// <summary>Whether the command is on (two-way).</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<RibbonToggleButton, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Raised when <see cref="IsChecked"/> changes, by a click or in code.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> IsCheckedChangedEvent =
        RoutedEvent.Register<RibbonToggleButton, RoutedEventArgs>(nameof(IsCheckedChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <inheritdoc cref="IsCheckedChangedEvent"/>
    public event EventHandler<RoutedEventArgs>? IsCheckedChanged
    {
        add => AddHandler(IsCheckedChangedEvent, value);
        remove => RemoveHandler(IsCheckedChangedEvent, value);
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, change.GetNewValue<bool>());
            RaiseEvent(new RoutedEventArgs(IsCheckedChangedEvent, this));
        }
    }

    protected override void OnClick()
    {
        SetCurrentValue(IsCheckedProperty, !IsChecked);
        InvokeCommand(null);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RibbonToggleButtonAutomationPeer(this);

    private sealed class RibbonToggleButtonAutomationPeer : ButtonAutomationPeer, IToggleProvider
    {
        public RibbonToggleButtonAutomationPeer(RibbonToggleButton owner)
            : base(owner)
        {
        }

        private RibbonToggleButton Toggle => (RibbonToggleButton)Owner;

        ToggleState IToggleProvider.ToggleState => Toggle.IsChecked ? ToggleState.On : ToggleState.Off;

        void IToggleProvider.Toggle()
        {
            if (Toggle.IsEffectivelyEnabled)
            {
                Toggle.SetCurrentValue(IsCheckedProperty, !Toggle.IsChecked);
            }
        }
    }
}
