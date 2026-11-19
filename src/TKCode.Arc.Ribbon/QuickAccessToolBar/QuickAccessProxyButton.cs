using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The small copy of a pinned ribbon command on a <see cref="QuickAccessToolBar"/>: it shows the command's icon and label
/// (tooltip), follows its enabled and checked state, and runs the command itself when clicked.
/// </summary>
internal sealed class QuickAccessProxyButton : RibbonButton
{
    private readonly bool _hasDropDown;

    public QuickAccessProxyButton(RibbonButton target)
    {
        Target = target;
        _hasDropDown = target is RibbonDropDownButton and not RibbonSplitButton;
        SetSizeCap(RibbonItemSize.Small);
        Sync();
        target.PropertyChanged += OnTargetPropertyChanged;
        UpdatePseudoClasses();
    }

    public RibbonButton Target { get; }

    private protected override bool HasDropDown => _hasDropDown;

    public void Detach() => Target.PropertyChanged -= OnTargetPropertyChanged;

    private void OnTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IconProperty || e.Property == LabelProperty || e.Property == DescriptionProperty
            || e.Property == ShortcutProperty || e.Property == IsEffectivelyEnabledProperty || e.Property == IsVisibleProperty
            || e.Property == RibbonToggleButton.IsCheckedProperty || e.Property == RibbonDropDownButton.IsDropDownOpenProperty)
        {
            Sync();
        }
    }

    private void Sync()
    {
        Icon = Target.Icon;
        Label = Target.Label;
        Description = Target.Description;
        Shortcut = Target.Shortcut;
        IsEnabled = Target.IsEffectivelyEnabled;
        IsVisible = Target.IsVisible;
        PseudoClasses.Set(CheckedPseudoClass, Target is RibbonToggleButton { IsChecked: true });
        PseudoClasses.Set(OpenPseudoClass, Target is RibbonDropDownButton { IsDropDownOpen: true } && _hasDropDown);
    }

    protected override void OnClick() => Target.InvokeFromQuickAccess(this);
}
