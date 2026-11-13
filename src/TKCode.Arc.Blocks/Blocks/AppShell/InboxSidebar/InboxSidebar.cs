using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The navigation of a mail app, built on <see cref="Sidebar"/>: a rail toggle and Compose on top, the folders with their
/// unread counts, the labels with colored dots under a heading that folds (with a "New label" row), and the storage
/// meter and the signed-in account at the bottom. It collapses to a 64px icon rail where the folder icons, the label
/// dots, Compose's pencil and the avatar stay and counts become dots; the width springs. Counts above 999 read "999+".
/// Without <see cref="Folders"/> and <see cref="MailLabels"/> it shows an invented mailbox.
/// </summary>
[TemplatePart(SidebarPart, typeof(Sidebar))]
[TemplatePart(ComposeButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ToggleButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(CollapsedPseudoClass)]
public class InboxSidebar : TemplatedControl
{
    public const string SidebarPart = "PART_Sidebar";
    public const string ComposeButtonPart = "PART_ComposeButton";
    public const string ToggleButtonPart = "PART_ToggleButton";

    /// <summary>Set while the sidebar is the icon rail.</summary>
    public const string CollapsedPseudoClass = ":collapsed";

    internal const string NewLabelKey = "__inbox-sidebar-new-label";

    #region Properties

    /// <summary>The key of the open folder or label (two-way); bind it to your route.</summary>
    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<InboxSidebar, string?>(nameof(SelectedKey), "inbox", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the sidebar is the icon rail (two-way).</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<InboxSidebar, bool>(nameof(IsCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How the width change reaches the page beside it: Overlay (default) or Push.</summary>
    public static readonly StyledProperty<SidebarResizeMode> ResizeModeProperty =
        AvaloniaProperty.Register<InboxSidebar, SidebarResizeMode>(nameof(ResizeMode));

    /// <summary>The mailbox name beside the rail toggle.</summary>
    public static readonly StyledProperty<string> MailboxNameProperty =
        AvaloniaProperty.Register<InboxSidebar, string>(nameof(MailboxName), "Mail");

    /// <summary>The signed-in person's name in the footer (also the avatar's initials).</summary>
    public static readonly StyledProperty<string?> AccountNameProperty =
        AvaloniaProperty.Register<InboxSidebar, string?>(nameof(AccountName), "Iris Vantongeren");

    /// <summary>The signed-in address in the footer.</summary>
    public static readonly StyledProperty<string?> AccountEmailProperty =
        AvaloniaProperty.Register<InboxSidebar, string?>(nameof(AccountEmail), "iris@pinecove.example");

    /// <summary>Storage used, in gigabytes.</summary>
    public static readonly StyledProperty<double> StorageUsedProperty =
        AvaloniaProperty.Register<InboxSidebar, double>(nameof(StorageUsed), 3.2);

    /// <summary>Storage available, in gigabytes; 0 hides the meter.</summary>
    public static readonly StyledProperty<double> StorageTotalProperty =
        AvaloniaProperty.Register<InboxSidebar, double>(nameof(StorageTotal), 15);

    /// <summary>Runs when Compose is pressed.</summary>
    public static readonly StyledProperty<ICommand?> ComposeCommandProperty =
        AvaloniaProperty.Register<InboxSidebar, ICommand?>(nameof(ComposeCommand));

    /// <summary>Whether the labels end with a "New label" row.</summary>
    public static readonly StyledProperty<bool> CanCreateLabelsProperty =
        AvaloniaProperty.Register<InboxSidebar, bool>(nameof(CanCreateLabels), true);

    /// <summary>The built-in text; replace the whole object to localize the sidebar.</summary>
    public static readonly StyledProperty<InboxSidebarLabels> LabelsProperty =
        AvaloniaProperty.Register<InboxSidebar, InboxSidebarLabels>(nameof(Labels), new InboxSidebarLabels());

    /// <inheritdoc cref="SelectedKeyProperty"/>
    public string? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }

    /// <inheritdoc cref="IsCollapsedProperty"/>
    public bool IsCollapsed { get => GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }

    /// <inheritdoc cref="ResizeModeProperty"/>
    public SidebarResizeMode ResizeMode { get => GetValue(ResizeModeProperty); set => SetValue(ResizeModeProperty, value); }

    /// <inheritdoc cref="MailboxNameProperty"/>
    public string MailboxName { get => GetValue(MailboxNameProperty); set => SetValue(MailboxNameProperty, value); }

    /// <inheritdoc cref="AccountNameProperty"/>
    public string? AccountName { get => GetValue(AccountNameProperty); set => SetValue(AccountNameProperty, value); }

    /// <inheritdoc cref="AccountEmailProperty"/>
    public string? AccountEmail { get => GetValue(AccountEmailProperty); set => SetValue(AccountEmailProperty, value); }

    /// <inheritdoc cref="StorageUsedProperty"/>
    public double StorageUsed { get => GetValue(StorageUsedProperty); set => SetValue(StorageUsedProperty, value); }

    /// <inheritdoc cref="StorageTotalProperty"/>
    public double StorageTotal { get => GetValue(StorageTotalProperty); set => SetValue(StorageTotalProperty, value); }

    /// <inheritdoc cref="ComposeCommandProperty"/>
    public ICommand? ComposeCommand { get => GetValue(ComposeCommandProperty); set => SetValue(ComposeCommandProperty, value); }

    /// <inheritdoc cref="CanCreateLabelsProperty"/>
    public bool CanCreateLabels { get => GetValue(CanCreateLabelsProperty); set => SetValue(CanCreateLabelsProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public InboxSidebarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The folders, in order. Empty shows <see cref="SampleFolders"/>.</summary>
    public AvaloniaList<InboxSidebarFolder> Folders { get; } = new();

    /// <summary>The mail labels, in order. Empty shows <see cref="SampleLabels"/>.</summary>
    public AvaloniaList<InboxSidebarLabel> MailLabels { get; } = new();

    /// <summary>The folders on screen: <see cref="Folders"/>, or the examples while it is empty.</summary>
    public IReadOnlyList<InboxSidebarFolder> ShownFolders => Folders.Count > 0 ? Folders : _sampleFolders;

    /// <summary>The labels on screen: <see cref="MailLabels"/>, or the examples while it is empty.</summary>
    public IReadOnlyList<InboxSidebarLabel> ShownLabels => MailLabels.Count > 0 ? MailLabels : _sampleLabels;

    #endregion

    #region Events

    /// <summary>Raised when Compose is pressed (after <see cref="ComposeCommand"/>).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ComposeRequestedEvent =
        RoutedEvent.Register<InboxSidebar, RoutedEventArgs>(nameof(ComposeRequested), RoutingStrategies.Bubble);

    /// <summary>Raised by the "New label" row: ask for a name and add an <see cref="InboxSidebarLabel"/>.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CreateLabelRequestedEvent =
        RoutedEvent.Register<InboxSidebar, RoutedEventArgs>(nameof(CreateLabelRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a folder or label is chosen (it becomes <see cref="SelectedKey"/>), also when it already was open.</summary>
    public static readonly RoutedEvent<InboxSidebarItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<InboxSidebar, InboxSidebarItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when <see cref="SelectedKey"/> changes, whether a folder or label was chosen or the key was set in code or
    /// through a binding. Choosing the folder that is already open raises <see cref="ItemInvoked"/> only.
    /// </summary>
    public static readonly RoutedEvent<InboxSidebarSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<InboxSidebar, InboxSidebarSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ComposeRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ComposeRequested
    {
        add => AddHandler(ComposeRequestedEvent, value);
        remove => RemoveHandler(ComposeRequestedEvent, value);
    }

    /// <inheritdoc cref="CreateLabelRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CreateLabelRequested
    {
        add => AddHandler(CreateLabelRequestedEvent, value);
        remove => RemoveHandler(CreateLabelRequestedEvent, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<InboxSidebarItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<InboxSidebarSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

    /// <summary>An invented mailbox: Inbox, Starred, Snoozed, Sent, Drafts, Spam and Trash.</summary>
    public static IReadOnlyList<InboxSidebarFolder> SampleFolders() =>
    [
        new("inbox", "Inbox", ArcIcons.Inbox, 12),
        new("starred", "Starred", ArcIcons.Star),
        new("snoozed", "Snoozed", ArcIcons.Clock, 2),
        new("sent", "Sent", ArcIcons.Send),
        new("drafts", "Drafts", ArcIcons.FileText, 3, isCountUnread: false),
        new("spam", "Spam", ArcIcons.Ban, 1048),
        new("trash", "Trash", ArcIcons.Trash2)
    ];

    /// <summary>Invented labels with palette colors.</summary>
    public static IReadOnlyList<InboxSidebarLabel> SampleLabels() =>
    [
        new("labels/clients", "Clients", InboxSidebarLabelTone.Series1, 4),
        new("labels/invoices", "Invoices", InboxSidebarLabelTone.Success, 1),
        new("labels/travel", "Travel", InboxSidebarLabelTone.Warning),
        new("labels/hiring", "Hiring", InboxSidebarLabelTone.Series3, 2),
        new("labels/receipts", "Receipts", InboxSidebarLabelTone.Neutral)
    ];

    private readonly IReadOnlyList<InboxSidebarFolder> _sampleFolders = SampleFolders();
    private readonly IReadOnlyList<InboxSidebarLabel> _sampleLabels = SampleLabels();
    private readonly List<AvaloniaObject> _watched = new();
    private Sidebar? _sidebar;
    private AvaloniaButton? _compose;
    private AvaloniaButton? _toggle;
    private TextBlock? _composeText;
    private TextBlock? _mailboxName;
    private TextBlock? _storageText;
    private Progress? _storageMeter;
    private Control? _storage;
    private Avatar? _avatar;
    private TextBlock? _accountName;
    private TextBlock? _accountEmail;
    private SidebarSection? _labelSection;
    private bool _rebuildPosted;
    private bool _syncing;

    static InboxSidebar()
    {
        FocusableProperty.OverrideDefaultValue<InboxSidebar>(false);
    }

    public InboxSidebar()
    {
        Folders.CollectionChanged += OnItemsChanged;
        MailLabels.CollectionChanged += OnItemsChanged;
    }

    /// <summary>"999+" above 999, otherwise the number; null for 0.</summary>
    internal static string? FormatCount(int count, System.Globalization.CultureInfo culture)
        => count <= 0 ? null : count > 999 ? "999+" : count.ToString("N0", culture);

    /// <summary>Collapses the sidebar to the icon rail, or opens it from the rail.</summary>
    public void ToggleCollapsed() => SetCurrentValue(IsCollapsedProperty, !IsCollapsed);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_sidebar is not null)
        {
            _sidebar.ItemInvoked -= OnSidebarItemInvoked;
            _sidebar.PropertyChanged -= OnSidebarPropertyChanged;
        }
        if (_compose is not null)
        {
            _compose.Click -= OnComposeClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        base.OnApplyTemplate(e);
        _sidebar = e.NameScope.Find<Sidebar>(SidebarPart);
        _compose = e.NameScope.Find<AvaloniaButton>(ComposeButtonPart);
        _toggle = e.NameScope.Find<AvaloniaButton>(ToggleButtonPart);
        _composeText = e.NameScope.Find<TextBlock>("PART_ComposeText");
        _mailboxName = e.NameScope.Find<TextBlock>("PART_MailboxName");
        _storageText = e.NameScope.Find<TextBlock>("PART_StorageText");
        _storageMeter = e.NameScope.Find<Progress>("PART_StorageMeter");
        _storage = e.NameScope.Find<Control>("PART_Storage");
        _avatar = e.NameScope.Find<Avatar>("PART_Avatar");
        _accountName = e.NameScope.Find<TextBlock>("PART_AccountName");
        _accountEmail = e.NameScope.Find<TextBlock>("PART_AccountEmail");
        if (_sidebar is not null)
        {
            _sidebar.ItemInvoked += OnSidebarItemInvoked;
            _sidebar.PropertyChanged += OnSidebarPropertyChanged;
        }
        if (_compose is not null)
        {
            _compose.Click += OnComposeClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        UpdateTexts();
        UpdateFooter();
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedKeyProperty)
        {
            if (!_syncing && _sidebar is not null)
            {
                _syncing = true;
                _sidebar.SelectedKey = SelectedKey;
                _syncing = false;
            }
            var newKey = change.GetNewValue<string?>();
            RaiseEvent(new InboxSidebarSelectionChangedEventArgs(
                SelectionChangedEvent,
                change.GetOldValue<string?>(),
                newKey,
                ShownFolders.FirstOrDefault(f => newKey is not null && f.Key == newKey),
                ShownLabels.FirstOrDefault(l => newKey is not null && l.Key == newKey)));
        }
        else if (change.Property == IsCollapsedProperty)
        {
            PseudoClasses.Set(CollapsedPseudoClass, IsCollapsed);
            UpdateTexts();
        }
        else if (change.Property == LabelsProperty || change.Property == CanCreateLabelsProperty)
        {
            UpdateTexts();
            UpdateFooter();
            Rebuild();
        }
        else if (change.Property == MailboxNameProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == AccountNameProperty || change.Property == AccountEmailProperty
                 || change.Property == StorageUsedProperty || change.Property == StorageTotalProperty)
        {
            UpdateFooter();
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_rebuildPosted)
        {
            return;
        }
        _rebuildPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPosted = false;
            Rebuild();
        }, DispatcherPriority.Send);
    }

    #endregion

    #region Content

    private void Rebuild()
    {
        foreach (var watched in _watched)
        {
            watched.PropertyChanged -= OnModelPropertyChanged;
        }
        _watched.Clear();
        if (_sidebar is null)
        {
            return;
        }
        var labels = Labels;
        var expanded = _labelSection?.IsExpanded ?? true;
        _sidebar.Sections.Clear();

        var folders = new SidebarSection();
        foreach (var folder in ShownFolders)
        {
            var item = new SidebarItem { Tag = folder };
            Apply(item, folder);
            folders.Items.Add(item);
            folder.PropertyChanged += OnModelPropertyChanged;
            _watched.Add(folder);
        }
        _sidebar.Sections.Add(folders);

        _labelSection = new SidebarSection { Header = labels.LabelsHeading, IsCollapsible = true, IsExpanded = expanded };
        foreach (var label in ShownLabels)
        {
            var item = new SidebarItem { Tag = label };
            Apply(item, label);
            _labelSection.Items.Add(item);
            label.PropertyChanged += OnModelPropertyChanged;
            _watched.Add(label);
        }
        if (CanCreateLabels)
        {
            _labelSection.Items.Add(new SidebarItem
            {
                Key = NewLabelKey,
                Label = labels.NewLabel,
                Icon = ArcIcons.Plus,
                IsAction = true
            });
        }
        _sidebar.Sections.Add(_labelSection);
        _syncing = true;
        _sidebar.SelectedKey = SelectedKey;
        _syncing = false;
    }

    private void Apply(SidebarItem item, InboxSidebarFolder folder)
    {
        item.Key = folder.Key;
        item.Label = folder.Title;
        item.Icon = folder.Icon;
        item.Badge = FormatCount(folder.Count, Labels.Culture);
        item.IsUnread = folder.IsCountUnread && folder.Count > 0;
        item.IsEnabled = folder.IsEnabled;
        item.IsVisible = folder.IsVisible;
    }

    private void Apply(SidebarItem item, InboxSidebarLabel label)
    {
        item.Key = label.Key;
        item.Label = label.Title;
        item.Badge = FormatCount(label.Count, Labels.Culture);
        item.IsEnabled = label.IsEnabled;
        item.IsVisible = label.IsVisible;
        if (item.Icon is not Ellipse dot)
        {
            dot = new Ellipse();
            dot.Bind(WidthProperty, dot.GetResourceObservable(InboxSidebarTokens.Key(InboxSidebarTokenKind.LabelDot)));
            dot.Bind(HeightProperty, dot.GetResourceObservable(InboxSidebarTokens.Key(InboxSidebarTokenKind.LabelDot)));
            item.Icon = dot;
        }
        if (label.Brush is { } brush)
        {
            dot.Fill = brush;
        }
        else
        {
            ArcTokens.Bind(dot, Shape.FillProperty, label.Tone switch
            {
                InboxSidebarLabelTone.Series1 => ArcDesignTokenKind.Series1,
                InboxSidebarLabelTone.Series2 => ArcDesignTokenKind.Series2,
                InboxSidebarLabelTone.Series3 => ArcDesignTokenKind.Series3,
                InboxSidebarLabelTone.Series4 => ArcDesignTokenKind.Series4,
                InboxSidebarLabelTone.Success => ArcDesignTokenKind.Success,
                InboxSidebarLabelTone.Warning => ArcDesignTokenKind.Warning,
                InboxSidebarLabelTone.Danger => ArcDesignTokenKind.Danger,
                InboxSidebarLabelTone.Neutral => ArcDesignTokenKind.TextMuted,
                _ => ArcDesignTokenKind.Accent
            });
        }
    }

    /// <summary>The sidebar item that shows <paramref name="model"/> (a folder or a label), or null.</summary>
    internal SidebarItem? ItemOf(AvaloniaObject model)
        => _sidebar?.Sections.SelectMany(s => s.Items).FirstOrDefault(i => ReferenceEquals(i.Tag, model));

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_composeText is not null)
        {
            _composeText.Text = labels.Compose;
        }
        if (_compose is not null)
        {
            AutomationProperties.SetName(_compose, labels.Compose);
            Tooltip.SetTip(_compose, IsCollapsed ? labels.Compose : null);
        }
        if (_toggle is not null)
        {
            var text = IsCollapsed ? labels.Expand : labels.Collapse;
            AutomationProperties.SetName(_toggle, text);
            Tooltip.SetTip(_toggle, text);
        }
        if (_mailboxName is not null)
        {
            _mailboxName.Text = MailboxName;
        }
        if (_sidebar is not null)
        {
            _sidebar.Label = labels.Navigation;
            _sidebar.UnreadText = labels.Unread;
        }
    }

    private void UpdateFooter()
    {
        var labels = Labels;
        var hasStorage = StorageTotal > 0;
        if (_storage is not null)
        {
            _storage.IsVisible = hasStorage;
        }
        if (hasStorage)
        {
            var culture = labels.Culture;
            var used = Math.Clamp(StorageUsed, 0, StorageTotal);
            if (_storageText is not null)
            {
                _storageText.Text = labels.Storage(used.ToString("0.#", culture), StorageTotal.ToString("0.#", culture));
            }
            if (_storageMeter is not null)
            {
                _storageMeter.Value = used / StorageTotal * 100;
                _storageMeter.Label = labels.StorageName;
            }
        }
        if (_avatar is not null)
        {
            _avatar.DisplayName = AccountName;
        }
        if (_accountName is not null)
        {
            _accountName.Text = AccountName;
        }
        if (_accountEmail is not null)
        {
            _accountEmail.Text = AccountEmail;
        }
    }

    #endregion

    #region Handlers

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not AvaloniaObject model || ItemOf(model) is not { } item)
        {
            return;
        }
        if (model is InboxSidebarFolder folder)
        {
            Apply(item, folder);
        }
        else if (model is InboxSidebarLabel label)
        {
            Apply(item, label);
        }
    }

    private void OnSidebarItemInvoked(object? sender, SidebarItemInvokedEventArgs e)
    {
        if (e.Item.Key == NewLabelKey)
        {
            RaiseEvent(new RoutedEventArgs(CreateLabelRequestedEvent));
            return;
        }
        RaiseEvent(new InboxSidebarItemEventArgs(ItemInvokedEvent, e.Item.Key, e.Item.Tag as InboxSidebarFolder, e.Item.Tag as InboxSidebarLabel));
    }

    private void OnSidebarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_sidebar is null || _syncing || e.Property != Sidebar.SelectedKeyProperty)
        {
            return;
        }
        if (_sidebar.SelectedKey is { } key && key != NewLabelKey && key != SelectedKey)
        {
            _syncing = true;
            SetCurrentValue(SelectedKeyProperty, key);
            _syncing = false;
        }
    }

    private void OnComposeClick(object? sender, RoutedEventArgs e)
    {
        if (ComposeCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
        RaiseEvent(new RoutedEventArgs(ComposeRequestedEvent));
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => ToggleCollapsed();

    #endregion
}
