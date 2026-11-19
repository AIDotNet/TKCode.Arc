using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A ribbon command that opens a menu (Arc <see cref="MenuEntry"/> rows, with submenus) instead of running a command: a
/// chevron sits under the label of a large button and after the label of a medium or small one. The menu opens below the
/// button; choosing a row raises the row's own <c>Click</c> and <c>Command</c>, and <see cref="Ribbon.ItemInvoked"/> with the
/// row. Enter, Space, ArrowDown and F4 open the menu with the first row highlighted.
/// </summary>
public class RibbonDropDownButton : RibbonButton
{
    #region 公共属性定义

    /// <summary>Whether the menu is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<RibbonDropDownButton, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Data items shown as one menu row each; <see cref="ItemTemplate"/> renders them when set.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<RibbonDropDownButton, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Template that renders each <see cref="ItemsSource"/> item as a row.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<RibbonDropDownButton, IDataTemplate?>(nameof(ItemTemplate));

    /// <inheritdoc cref="IsDropDownOpenProperty"/>
    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Inline menu rows (<see cref="MenuEntry"/>, <see cref="MenuEntrySeparator"/>, <see cref="MenuEntryLabel"/>).</summary>
    [Content]
    public ItemCollection Items => Menu.Items;

    /// <summary>The menu surface (for advanced configuration such as <see cref="MenuSurface.Alignment"/>).</summary>
    public MenuSurface Menu { get; }

    #endregion

    private bool _keyboardActivation;
    private Control? _menuAnchor;

    public RibbonDropDownButton()
    {
        Menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Side = MenuSide.Bottom, Alignment = MenuAlignment.Start };
        Menu.ItemClicked += (_, e) => ReportInvoked(e.Entry);
        Menu.Opened += (_, _) => OnMenuOpenChanged(true);
        Menu.Closed += (_, _) => OnMenuOpenChanged(false);
        UpdatePseudoClasses();
    }

    private protected override bool HasDropDown => true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Menu.Bind(MenuSurface.AnchorGapProperty, this.GetResourceObservable(RibbonDropDownButtonTokens.Key(RibbonDropDownButtonTokenKind.MenuGap)));
        AutomationProperties.SetName(Menu, Label);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (ReferenceEquals(_menuAnchor ?? this, this))
        {
            Menu.Close(returnFocus: false);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            Menu.ItemsSource = change.GetNewValue<IEnumerable?>();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            Menu.ItemTemplate = change.GetNewValue<IDataTemplate?>();
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            if (open && !Menu.IsOpen)
            {
                OpenMenu(_menuAnchor ?? this, MenuOpenFocus.Surface);
            }
            else if (!open && Menu.IsOpen)
            {
                Menu.Close();
            }
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(Menu, change.GetNewValue<string?>());
        }
    }

    private void OnMenuOpenChanged(bool open)
    {
        PseudoClasses.Set(OpenPseudoClass, open);
        SetCurrentValue(IsDropDownOpenProperty, open);
        if (!open)
        {
            _menuAnchor = null;
        }
    }

    /// <summary>Opens the menu against <paramref name="anchor"/> (this button, or its copy on a quick access toolbar).</summary>
    internal void OpenMenu(Control anchor, MenuOpenFocus focus)
    {
        _menuAnchor = anchor;
        Menu.Open(anchor, focus);
    }

    /// <summary>Opens the menu, or closes it when it is open (clicks on the button or on its menu part).</summary>
    private protected void ToggleMenu(Control anchor)
    {
        var focus = _keyboardActivation ? MenuOpenFocus.First : MenuOpenFocus.Surface;
        _keyboardActivation = false;
        if (Menu.IsOpen)
        {
            Menu.Close(returnFocus: false);
        }
        else
        {
            OpenMenu(anchor, focus);
        }
    }

    protected override void OnClick() => ToggleMenu(this);

    internal override void InvokeFromQuickAccess(Control anchor) => ToggleMenu(anchor);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            case Key.F4:
                if (!Menu.IsOpen)
                {
                    e.Handled = true;
                    OpenMenu(this, MenuOpenFocus.First);
                    return;
                }
                break;
            case Key.Up:
                if (!Menu.IsOpen && e.KeyModifiers == KeyModifiers.Alt)
                {
                    e.Handled = true;
                    return;
                }
                break;
            case Key.Enter:
            case Key.Space:
                _keyboardActivation = true;
                break;
        }
        base.OnKeyDown(e);
    }
}
