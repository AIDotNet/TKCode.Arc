using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>The large button a collapsed <see cref="RibbonGroup"/> shows: the group's icon and name over a chevron.</summary>
internal sealed class RibbonGroupCollapsedButton : RibbonButton
{
    private bool _keyboardActivation;

    public RibbonGroupCollapsedButton()
    {
        SetCurrentValue(IconProperty, ArcIcons.Ellipsis);
        UpdatePseudoClasses();
    }

    public RibbonGroup? Owner { get; set; }

    private protected override bool HasDropDown => true;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty && change.NewValue is null)
        {
            SetCurrentValue(IconProperty, ArcIcons.Ellipsis);
        }
    }

    public void SetOpen(bool open) => PseudoClasses.Set(OpenPseudoClass, open);

    protected override void OnClick()
    {
        var keyboard = _keyboardActivation;
        _keyboardActivation = false;
        Owner?.ToggleDropDown(keyboard);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            _keyboardActivation = true;
        }
        else if (e.Key is Key.Down or Key.F4 && Owner is { IsDropDownOpen: false } owner)
        {
            e.Handled = true;
            owner.ToggleDropDown(keyboard: true);
            return;
        }
        base.OnKeyDown(e);
    }
}
