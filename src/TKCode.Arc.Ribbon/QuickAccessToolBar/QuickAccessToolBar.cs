using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// A row of small commands that stays visible whichever tab is selected (Save, Undo, Redo, and the commands the user pins).
/// Place it in an <c>ArcWindow</c> title bar (<c>TitleBar.LeftContent</c>), above or below the <see cref="Ribbon"/>, and link
/// it with <see cref="Ribbon"/>.
/// <para>
/// <see cref="Items"/> are the toolbar's own commands (ribbon buttons, shown small). <see cref="Keys"/> lists the ribbon
/// commands pinned to it by their <see cref="RibbonButton.QuickAccessKey"/>: each shows as a small copy that runs the ribbon
/// command (opens its menu, flips its toggle) and follows its icon and enabled state. The user pins and unpins commands from
/// a command's context menu or from the customize button at the end of the toolbar; save <see cref="Keys"/> to keep the
/// choice (it is an observable list).
/// </para>
/// </summary>
[TemplatePart(ItemsPanelPart, typeof(Panel))]
[TemplatePart(CustomizeButtonPart, typeof(RibbonButton))]
public class QuickAccessToolBar : TemplatedControl
{
    public const string ItemsPanelPart = "PART_ItemsPanel";
    public const string CustomizeButtonPart = "PART_CustomizeButton";

    #region 公共属性定义

    /// <summary>The ribbon whose commands can be pinned here (and whose commands are offered in the customize menu).</summary>
    public static readonly StyledProperty<Ribbon?> RibbonProperty =
        AvaloniaProperty.Register<QuickAccessToolBar, Ribbon?>(nameof(Ribbon));

    /// <summary>Shows the button at the end of the toolbar that lists the ribbon's commands to pin or unpin (default true).</summary>
    public static readonly StyledProperty<bool> IsCustomizeButtonVisibleProperty =
        AvaloniaProperty.Register<QuickAccessToolBar, bool>(nameof(IsCustomizeButtonVisible), true);

    /// <inheritdoc cref="RibbonProperty"/>
    public Ribbon? Ribbon
    {
        get => GetValue(RibbonProperty);
        set => SetValue(RibbonProperty, value);
    }

    /// <inheritdoc cref="IsCustomizeButtonVisibleProperty"/>
    public bool IsCustomizeButtonVisible
    {
        get => GetValue(IsCustomizeButtonVisibleProperty);
        set => SetValue(IsCustomizeButtonVisibleProperty, value);
    }

    /// <summary>The toolbar's own commands, shown small before the pinned ones.</summary>
    [Content]
    public AvaloniaList<Control> Items { get; } = new();

    /// <summary>
    /// The <see cref="RibbonButton.QuickAccessKey"/>s of the ribbon commands pinned here, in order. A key whose command is not
    /// in the ribbon (yet) is kept and shows nothing.
    /// </summary>
    public AvaloniaList<string> Keys { get; } = new();

    #endregion

    private Panel? _panel;
    private RibbonButton? _customize;
    private MenuSurface? _customizeMenu;
    private MenuSurface? _contextMenu;
    private string? _contextKey;
    private readonly List<QuickAccessProxyButton> _proxies = new();

    public QuickAccessToolBar()
    {
        Items.CollectionChanged += OnItemsChanged;
        Keys.CollectionChanged += (_, _) => Rebuild();
        AddHandler(ContextRequestedEvent, OnContextRequested);
        AddHandler(KeyDownEvent, OnToolBarKeyDown);
    }

    // The menu key on a pinned copy: the same menu as a right-click.
    private void OnToolBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && (e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift))
            && e.Source is QuickAccessProxyButton proxy)
        {
            e.Handled = OpenRemoveMenu(proxy, null);
        }
    }

    private ArcLanguageManager? _languages;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        ApplyText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => ApplyText();

    private void ApplyText()
    {
        AutomationProperties.SetName(this, RibbonText.Get(RibbonLangResourceKind.QuickAccessToolBar));
        if (_customize is not null)
        {
            var text = RibbonText.Get(RibbonLangResourceKind.CustomizeQuickAccess);
            _customize.Label = text;
            AutomationProperties.SetName(_customize, text);
        }
    }

    /// <summary>The copies of pinned ribbon commands, in order (tests).</summary>
    internal IReadOnlyList<QuickAccessProxyButton> Proxies => _proxies;

    /// <summary>The customize menu (null until it first opens).</summary>
    internal MenuSurface? CustomizeMenu => _customizeMenu;

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (Control item in e.OldItems)
            {
                if (item is RibbonButton button)
                {
                    button.SetSizeCap(null);
                }
            }
        }
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RibbonProperty)
        {
            if (change.GetOldValue<Ribbon?>() is { } old && ReferenceEquals(old.QuickAccess, this))
            {
                old.QuickAccess = null;
            }
            if (change.GetNewValue<Ribbon?>() is { } ribbon)
            {
                ribbon.QuickAccess = this;
            }
            Rebuild();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_customize is not null)
        {
            _customize.Click -= OnCustomizeClick;
        }
        _panel?.Children.Clear();
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<Panel>(ItemsPanelPart);
        _customize = e.NameScope.Find<RibbonButton>(CustomizeButtonPart);
        if (_customize is not null)
        {
            _customize.Click += OnCustomizeClick;
        }
        ApplyText();
        Rebuild();
    }

    /// <summary>Pins the command with <paramref name="key"/>, or unpins it when it is pinned.</summary>
    internal void TogglePinned(string key)
    {
        if (!Keys.Remove(key))
        {
            Keys.Add(key);
        }
    }

    /// <summary>Shows the toolbar's own commands and a copy of each pinned ribbon command.</summary>
    internal void Rebuild()
    {
        foreach (var proxy in _proxies)
        {
            proxy.Detach();
        }
        _proxies.Clear();
        if (_panel is null)
        {
            return;
        }
        _panel.Children.Clear();
        foreach (var item in Items)
        {
            if (item is RibbonButton button)
            {
                button.SetSizeCap(RibbonItemSize.Small);
            }
            _panel.Children.Add(item);
        }
        var ribbon = Ribbon;
        if (ribbon is null)
        {
            return;
        }
        foreach (var key in Keys)
        {
            if (ribbon.FindQuickAccessItem(key) is not { } target)
            {
                continue;
            }
            var proxy = new QuickAccessProxyButton(target);
            _proxies.Add(proxy);
            _panel.Children.Add(proxy);
        }
    }

    private void OnCustomizeClick(object? sender, RoutedEventArgs e)
    {
        if (_customize is null || Ribbon is not { } ribbon)
        {
            return;
        }
        _customizeMenu ??= CreateCustomizeMenu();
        _customizeMenu.Items.Clear();
        _customizeMenu.Items.Add(new MenuEntryLabel { Text = RibbonText.Get(RibbonLangResourceKind.CustomizeQuickAccess) });
        foreach (var command in ribbon.QuickAccessCandidates())
        {
            var key = command.QuickAccessKey!;
            _customizeMenu.Items.Add(new MenuEntry
            {
                Header = command.Label ?? key,
                ToggleType = MenuEntryToggleType.CheckBox,
                IsChecked = Keys.Contains(key),
                StaysOpenOnClick = true,
                Tag = key
            });
        }
        _customizeMenu.Toggle(_customize, MenuOpenFocus.First);
    }

    private MenuSurface CreateCustomizeMenu()
    {
        var menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Side = MenuSide.Bottom, Alignment = MenuAlignment.Start };
        menu.ItemClicked += (_, e) =>
        {
            if (e.Entry.Tag is string key)
            {
                TogglePinned(key);
                e.Entry.IsChecked = Keys.Contains(key);
            }
        };
        return menu;
    }

    // Right-click on a pinned copy offers to remove it.
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            return;
        }
        var proxy = source as QuickAccessProxyButton ?? Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<QuickAccessProxyButton>(source);
        if (proxy is null)
        {
            return;
        }
        e.Handled = OpenRemoveMenu(proxy, e.TryGetPosition(proxy, out var point) ? point : null);
    }

    private bool OpenRemoveMenu(QuickAccessProxyButton proxy, Point? point)
    {
        if (proxy.Target.QuickAccessKey is not { } key)
        {
            return false;
        }
        _contextKey = key;
        if (_contextMenu is null)
        {
            _contextMenu = new MenuSurface { Variant = MenuSurfaceVariant.Context };
            _contextMenu.Items.Add(new MenuEntry { Icon = ArcIcons.Trash2 });
            _contextMenu.ItemClicked += (_, _) =>
            {
                if (_contextKey is { } pinned)
                {
                    Keys.Remove(pinned);
                }
                _contextKey = null;
            };
        }
        ((MenuEntry)_contextMenu.Items[0]!).Header = RibbonText.Get(RibbonLangResourceKind.RemoveFromQuickAccess);
        if (point is { } at)
        {
            _contextMenu.OpenAt(proxy, at);
        }
        else
        {
            _contextMenu.Open(proxy, MenuOpenFocus.First);
        }
        return true;
    }
}
