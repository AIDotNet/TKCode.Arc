using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A ribbon command with a default action and a menu of alternatives (Paste with Paste special, Export with its formats): the
/// icon part runs <see cref="Avalonia.Controls.Button.Command"/>, and the chevron part opens the menu. On a large button the
/// chevron part is the label and chevron under the icon; on a medium or small one it is the chevron at the end. A divider
/// between the parts shows while the pointer is over the button. Enter and Space run the action; ArrowDown and F4 open the
/// menu.
/// </summary>
[TemplatePart(MenuPart, typeof(Control))]
public class RibbonSplitButton : RibbonDropDownButton
{
    public const string MenuPart = "PART_MenuPart";

    private RibbonSplitButtonPart? _menuPart;
    private TextBlock? _label;

    public RibbonSplitButton()
    {
        PseudoClasses.Set(SplitPseudoClass, true);
    }

    /// <summary>The chevron part (tests).</summary>
    internal RibbonSplitButtonPart? MenuPartControl => _menuPart;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_menuPart is not null)
        {
            _menuPart.Invoked -= OnMenuPartInvoked;
        }
        base.OnApplyTemplate(e);
        _menuPart = e.NameScope.Find<RibbonSplitButtonPart>(MenuPart);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        if (_menuPart is not null)
        {
            _menuPart.Invoked += OnMenuPartInvoked;
        }
    }

    private void OnMenuPartInvoked(object? sender, RoutedEventArgs e) => ToggleMenu(this);

    protected override void OnClick() => InvokeCommand(null);

    internal override void InvokeFromQuickAccess(Control anchor) => InvokeCommand(null);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceMenuPart();
        return size;
    }

    // On a large button the menu part covers the label and chevron under the icon; its top follows the label, read after the
    // template is arranged. On a medium or small button the theme docks it to the end at a fixed width.
    private void PlaceMenuPart()
    {
        if (_menuPart is null)
        {
            return;
        }
        var margin = default(Thickness);
        if (EffectiveSize == RibbonItemSize.Large && _label is not null && _label.TranslatePoint(default, this) is { } labelTop)
        {
            margin = new Thickness(0, Math.Max(0, Math.Round(labelTop.Y) - 2), 0, 0);
        }
        if (_menuPart.Margin != margin)
        {
            _menuPart.Margin = margin;
        }
    }
}
