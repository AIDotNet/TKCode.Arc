using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc cookie consent: a consent bar that morphs into a preferences panel and, once a choice is saved, folds into
/// a small floating button that reopens it. One surface springs between the three shapes. Optional categories start
/// off, refusing is as easy as accepting, and the choice is reported through <see cref="ConsentChanged"/> and can
/// be remembered (<see cref="StorageKey"/>, read back with <see cref="Read"/>).
/// <para>
/// The control is a layer: place it over the page (for example as the last child of the window's root grid, or of
/// a preview panel); it lets clicks through everywhere except on its surface, which hangs 16px from the bottom.
/// </para>
/// </summary>
[TemplatePart(SurfacePart, typeof(CookieConsentSurface))]
[TemplatePart(BarFacePart, typeof(Control))]
[TemplatePart(PanelFacePart, typeof(Control))]
[TemplatePart(ChipFacePart, typeof(Control))]
[PseudoClasses(BarStackedPseudoClass, BarCompactPseudoClass, PanelWidePseudoClass)]
public class CookieConsent : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string BarFacePart = "PART_BarFace";
    public const string PanelFacePart = "PART_PanelFace";
    public const string ChipFacePart = "PART_ChipFace";
    public const string BarStackedPseudoClass = ":bar-stacked";
    public const string BarCompactPseudoClass = ":bar-compact";
    public const string PanelWidePseudoClass = ":panel-wide";

    /// <summary>Bars narrower than this stack the message above the buttons.</summary>
    private const double StackBelow = 560;

    #region 公共属性定义

    /// <summary>Arc's four default categories (Strictly necessary is required).</summary>
    public static IReadOnlyList<CookieCategory> DefaultCategories { get; } =
    [
        new() { Id = "necessary", Label = "Strictly necessary", Description = "Sign-in, security, and your cart. The site can't work without these.", IsRequired = true },
        new() { Id = "preferences", Label = "Preferences", Description = "Remember your language, region, and display settings." },
        new() { Id = "analytics", Label = "Analytics", Description = "Count visits and see which pages help, only in aggregate." },
        new() { Id = "marketing", Label = "Marketing", Description = "Measure campaigns and show relevant ads on other sites." }
    ];

    /// <summary>Categories (XAML children); empty uses <see cref="DefaultCategories"/>.</summary>
    [Content]
    public AvaloniaList<CookieCategory> Categories { get; } = new();

    /// <summary>Categories from a binding; overrides <see cref="Categories"/> when set.</summary>
    public static readonly StyledProperty<IList<CookieCategory>?> ItemsSourceProperty =
        AvaloniaProperty.Register<CookieConsent, IList<CookieCategory>?>(nameof(ItemsSource));

    /// <summary>The saved choice (category id → allowed); null means the visitor has not decided. Two-way.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, bool>?> ConsentProperty =
        AvaloniaProperty.Register<CookieConsent, IReadOnlyDictionary<string, bool>?>(nameof(Consent), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Remembers the choice under this key (see <see cref="Read"/>).</summary>
    public static readonly StyledProperty<string?> StorageKeyProperty =
        AvaloniaProperty.Register<CookieConsent, string?>(nameof(StorageKey));

    /// <summary>Bump when categories or the policy change to ask again (default "1").</summary>
    public static readonly StyledProperty<string> VersionProperty =
        AvaloniaProperty.Register<CookieConsent, string>(nameof(Version), "1");

    /// <summary>Where the choice is stored; null uses <see cref="MessageStorage.Default"/>.</summary>
    public static readonly StyledProperty<IMessageStorage?> StorageProperty =
        AvaloniaProperty.Register<CookieConsent, IMessageStorage?>(nameof(Storage));

    /// <summary>Whether the preferences panel is open. Two-way.</summary>
    public static readonly StyledProperty<bool> IsPreferencesOpenProperty =
        AvaloniaProperty.Register<CookieConsent, bool>(nameof(IsPreferencesOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Link to the full policy, shown in the bar and the panel.</summary>
    public static readonly StyledProperty<CookieConsentPolicy?> PolicyProperty =
        AvaloniaProperty.Register<CookieConsent, CookieConsentPolicy?>(nameof(Policy));

    public static readonly StyledProperty<CookieConsentPosition> PositionProperty =
        AvaloniaProperty.Register<CookieConsent, CookieConsentPosition>(nameof(Position));

    /// <summary>Keep a small button after a choice so visitors can change their mind (default true).</summary>
    public static readonly StyledProperty<bool> IsReopenButtonVisibleProperty =
        AvaloniaProperty.Register<CookieConsent, bool>(nameof(IsReopenButtonVisible), true);

    /// <summary>Corner of the reopen button; null follows the bar (left when the bar is centered).</summary>
    public static readonly StyledProperty<CookieConsentSide?> ReopenSideProperty =
        AvaloniaProperty.Register<CookieConsent, CookieConsentSide?>(nameof(ReopenSide));

    /// <summary>Copy of the bar, panel and buttons (Arc's English defaults).</summary>
    public static readonly StyledProperty<CookieConsentLabels> LabelsProperty =
        AvaloniaProperty.Register<CookieConsent, CookieConsentLabels>(nameof(Labels), new CookieConsentLabels());

    public static readonly RoutedEvent<CookieConsentChangedEventArgs> ConsentChangedEvent =
        RoutedEvent.Register<CookieConsent, CookieConsentChangedEventArgs>(nameof(ConsentChanged), RoutingStrategies.Bubble);

    /// <summary>Categories from a binding; overrides <see cref="Categories"/> when set.</summary>
    public IList<CookieCategory>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The saved choice (category id → allowed); null means the visitor has not decided. Two-way.</summary>
    public IReadOnlyDictionary<string, bool>? Consent { get => GetValue(ConsentProperty); set => SetValue(ConsentProperty, value); }
    /// <summary>Remembers the choice under this key (see <see cref="Read"/>).</summary>
    public string? StorageKey { get => GetValue(StorageKeyProperty); set => SetValue(StorageKeyProperty, value); }
    /// <summary>Bump when categories or the policy change to ask again (default "1").</summary>
    public string Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }
    /// <summary>Where the choice is stored; null uses <see cref="MessageStorage.Default"/>.</summary>
    public IMessageStorage? Storage { get => GetValue(StorageProperty); set => SetValue(StorageProperty, value); }
    /// <summary>Whether the preferences panel is open. Two-way.</summary>
    public bool IsPreferencesOpen { get => GetValue(IsPreferencesOpenProperty); set => SetValue(IsPreferencesOpenProperty, value); }
    /// <summary>Link to the full policy, shown in the bar and the panel.</summary>
    public CookieConsentPolicy? Policy { get => GetValue(PolicyProperty); set => SetValue(PolicyProperty, value); }
    /// <summary>Where the bar sits (default bottom, centered).</summary>
    public CookieConsentPosition Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    /// <summary>Keep a small button after a choice so visitors can change their mind (default true).</summary>
    public bool IsReopenButtonVisible { get => GetValue(IsReopenButtonVisibleProperty); set => SetValue(IsReopenButtonVisibleProperty, value); }
    /// <summary>Corner of the reopen button; null follows the bar (left when the bar is centered).</summary>
    public CookieConsentSide? ReopenSide { get => GetValue(ReopenSideProperty); set => SetValue(ReopenSideProperty, value); }
    /// <summary>Copy of the bar, panel and buttons (Arc's English defaults).</summary>
    public CookieConsentLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Raised when a choice is made or reset.</summary>
    public event EventHandler<CookieConsentChangedEventArgs>? ConsentChanged
    {
        add => AddHandler(ConsentChangedEvent, value);
        remove => RemoveHandler(ConsentChangedEvent, value);
    }

    #endregion

    /// <summary>Reads a saved choice, or null when there is none or it was saved for another version.</summary>
    public static CookieConsentRecord? Read(string storageKey, string version = "1", IMessageStorage? storage = null)
    {
        var record = CookieConsentRecord.FromJson((storage ?? MessageStorage.Default).Read(storageKey));
        return record is not null && record.Version == version ? record : null;
    }

    private readonly Dictionary<string, bool> _draft = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Switch> _switches = new(StringComparer.Ordinal);
    private CookieConsentSurface? _surface;
    private Control? _barFace;
    private Control? _panelFace;
    private Control? _chipFace;
    private TextBlock? _message;
    private TextBlock? _title;
    private TextBlock? _description;
    private Grid? _barGrid;
    private Grid? _barActions;
    private Grid? _panelActions;
    private StackPanel? _rows;
    private ScrollViewer? _listScroll;
    private AvaloniaButton? _customize;
    private AvaloniaButton? _barReject;
    private AvaloniaButton? _barAccept;
    private AvaloniaButton? _panelReject;
    private AvaloniaButton? _panelAccept;
    private AvaloniaButton? _save;
    private AvaloniaButton? _close;
    private AvaloniaButton? _chip;
    private CookieConsentMode? _focusTarget;
    private bool _loaded;
    private IDisposable? _clientSizeSubscription;

    static CookieConsent()
    {
        FocusableProperty.OverrideDefaultValue<CookieConsent>(false);
        AffectsMeasure<CookieConsent>(PositionProperty);
    }

    public CookieConsent()
    {
        // Each instance gets its own labels, so changing one never changes another.
        SetCurrentValue(LabelsProperty, new CookieConsentLabels());
        Categories.CollectionChanged += (_, _) =>
        {
            BuildRows();
            InvalidateMeasure();
        };
    }

    private IReadOnlyList<CookieCategory> EffectiveCategories =>
        ItemsSource is { Count: > 0 } source ? source.ToArray() : Categories.Count > 0 ? Categories : DefaultCategories;

    /// <summary>The face currently shown.</summary>
    internal CookieConsentMode Mode => _surface?.Mode ?? CookieConsentMode.None;

    internal CookieConsentSurface? Surface => _surface;

    internal IReadOnlyDictionary<string, Switch> Switches => _switches;

    #region 命令式句柄

    /// <summary>Opens the preferences panel (for example from a "Cookie settings" link) and focuses its title.</summary>
    public void OpenPreferences() => OpenPanel(focus: true);

    /// <summary>Closes the preferences panel without saving.</summary>
    public void ClosePreferences() => ClosePanel(focus: false);

    /// <summary>Forgets the saved choice and asks again.</summary>
    public void Reset()
    {
        _focusTarget = null;
        SetCurrentValue(IsPreferencesOpenProperty, false);
        Commit(null, CookieConsentAction.Reset);
    }

    private void OpenPanel(bool focus)
    {
        SeedDraft();
        _focusTarget = focus ? CookieConsentMode.Panel : null;
        SetCurrentValue(IsPreferencesOpenProperty, true);
    }

    private void ClosePanel(bool focus)
    {
        _focusTarget = focus ? (Consent is not null ? CookieConsentMode.Chip : CookieConsentMode.Bar) : null;
        SetCurrentValue(IsPreferencesOpenProperty, false);
    }

    private void Choose(CookieConsentAction action)
    {
        var next = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var category in EffectiveCategories)
        {
            next[category.Id] = category.IsRequired || action switch
            {
                CookieConsentAction.AcceptAll => true,
                CookieConsentAction.Save => _draft.TryGetValue(category.Id, out var allowed) && allowed,
                _ => false
            };
        }
        // The choice folds the surface into its reopen button, which takes focus when it exists.
        _focusTarget = IsReopenButtonVisible ? CookieConsentMode.Chip : null;
        Commit(next, action);
        if (IsPreferencesOpen)
        {
            SetCurrentValue(IsPreferencesOpenProperty, false);
        }
    }

    private void Commit(IReadOnlyDictionary<string, bool>? next, CookieConsentAction action)
    {
        var updatedAt = DateTimeOffset.Now;
        SetCurrentValue(ConsentProperty, next);
        if (StorageKey is { Length: > 0 } key)
        {
            (Storage ?? MessageStorage.Default).Write(key, next is null ? null : new CookieConsentRecord(next, Version, updatedAt).ToJson());
        }
        RaiseEvent(new CookieConsentChangedEventArgs(ConsentChangedEvent, next, action, updatedAt));
    }

    private void SeedDraft()
    {
        _draft.Clear();
        var consent = Consent;
        foreach (var category in EffectiveCategories)
        {
            _draft[category.Id] = category.IsRequired ||
                                  (consent is not null && consent.TryGetValue(category.Id, out var allowed) ? allowed : category.IsEnabledByDefault);
        }
        foreach (var pair in _switches)
        {
            pair.Value.IsChecked = _draft.TryGetValue(pair.Key, out var on) && on;
        }
        UpdateSaveHelp();
    }

    #endregion

    #region 模板

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        var scope = e.NameScope;
        _surface = scope.Find<CookieConsentSurface>(SurfacePart);
        _barFace = scope.Find<Control>(BarFacePart);
        _panelFace = scope.Find<Control>(PanelFacePart);
        _chipFace = scope.Find<Control>(ChipFacePart);
        _message = scope.Find<TextBlock>("PART_Message");
        _title = scope.Find<TextBlock>("PART_Title");
        _description = scope.Find<TextBlock>("PART_Description");
        _barGrid = scope.Find<Grid>("PART_BarGrid");
        _barActions = scope.Find<Grid>("PART_BarActions");
        _panelActions = scope.Find<Grid>("PART_PanelActions");
        _rows = scope.Find<StackPanel>("PART_Rows");
        _listScroll = scope.Find<ScrollViewer>("PART_ListScroll");
        _customize = scope.Find<AvaloniaButton>("PART_Customize");
        _barReject = scope.Find<AvaloniaButton>("PART_BarReject");
        _barAccept = scope.Find<AvaloniaButton>("PART_BarAccept");
        _panelReject = scope.Find<AvaloniaButton>("PART_PanelReject");
        _panelAccept = scope.Find<AvaloniaButton>("PART_PanelAccept");
        _save = scope.Find<AvaloniaButton>("PART_Save");
        _close = scope.Find<AvaloniaButton>("PART_Close");
        _chip = scope.Find<AvaloniaButton>("PART_Chip");
        Wire();
        _surface?.SetFaces(_barFace, _panelFace, _chipFace);
        ApplyLabels();
        BuildRows();
        _barLayout = -1;
        _panelLayout = -1;
        UpdateMode();
    }

    private void Wire()
    {
        if (_customize is not null)
        {
            _customize.Click += OnCustomizeClick;
        }
        if (_barReject is not null)
        {
            _barReject.Click += OnRejectClick;
        }
        if (_barAccept is not null)
        {
            _barAccept.Click += OnAcceptClick;
        }
        if (_panelReject is not null)
        {
            _panelReject.Click += OnRejectClick;
        }
        if (_panelAccept is not null)
        {
            _panelAccept.Click += OnAcceptClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_chip is not null)
        {
            _chip.Click += OnChipClick;
        }
        if (_panelFace is not null)
        {
            _panelFace.KeyDown += OnPanelKeyDown;
        }
    }

    private void Unwire()
    {
        if (_customize is not null)
        {
            _customize.Click -= OnCustomizeClick;
        }
        if (_barReject is not null)
        {
            _barReject.Click -= OnRejectClick;
        }
        if (_barAccept is not null)
        {
            _barAccept.Click -= OnAcceptClick;
        }
        if (_panelReject is not null)
        {
            _panelReject.Click -= OnRejectClick;
        }
        if (_panelAccept is not null)
        {
            _panelAccept.Click -= OnAcceptClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_chip is not null)
        {
            _chip.Click -= OnChipClick;
        }
        if (_panelFace is not null)
        {
            _panelFace.KeyDown -= OnPanelKeyDown;
        }
    }

    private void OnCustomizeClick(object? sender, RoutedEventArgs e) => OpenPanel(focus: true);

    private void OnRejectClick(object? sender, RoutedEventArgs e) => Choose(CookieConsentAction.RejectAll);

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => Choose(CookieConsentAction.AcceptAll);

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Choose(CookieConsentAction.Save);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => ClosePanel(focus: true);

    private void OnChipClick(object? sender, RoutedEventArgs e) => OpenPanel(focus: true);

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }
        e.Handled = true;
        ClosePanel(focus: true);
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        SetText(_message, labels.Message);
        SetText(_description, labels.PreferencesDescription);
        if (_title is not null)
        {
            _title.Text = labels.PreferencesTitle;
        }
        SetContent(_customize, labels.Customize);
        SetContent(_barReject, labels.RejectAll);
        SetContent(_barAccept, labels.AcceptAll);
        SetContent(_panelReject, labels.RejectAll);
        SetContent(_panelAccept, labels.AcceptAll);
        SetContent(_save, labels.Save);
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
        }
        if (_chip is not null)
        {
            AutomationProperties.SetName(_chip, labels.Reopen);
        }
        if (_barFace is not null)
        {
            // Non-modal dialogs: the bar is described by its message, the panel named by its title.
            AutomationProperties.SetName(_barFace, labels.PreferencesTitle);
            AutomationProperties.SetHelpText(_barFace, labels.Message);
        }
        if (_panelFace is not null)
        {
            AutomationProperties.SetName(_panelFace, labels.PreferencesTitle);
            AutomationProperties.SetHelpText(_panelFace, labels.PreferencesDescription);
        }
    }

    private static void SetContent(AvaloniaButton? button, string text)
    {
        if (button is not null)
        {
            button.Content = text;
        }
    }

    /// <summary>Message text followed by the inline policy link.</summary>
    private void SetText(TextBlock? block, string text)
    {
        if (block is null)
        {
            return;
        }
        block.Inlines ??= new InlineCollection();
        block.Inlines.Clear();
        block.Inlines.Add(new Run(text));
        if (Policy is { } policy)
        {
            var link = new CookieConsentPolicyLink { Content = policy.Label };
            AutomationProperties.SetName(link, policy.Label);
            link.Click += (_, _) => InvokePolicy(policy);
            block.Inlines.Add(new Run(" "));
            block.Inlines.Add(new InlineUIContainer(link) { BaselineAlignment = BaselineAlignment.TextBottom });
        }
    }

    private void InvokePolicy(CookieConsentPolicy policy)
    {
        if (policy.Command is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
        if (policy.Href is { } href && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(href);
        }
    }

    private void BuildRows()
    {
        if (_rows is null)
        {
            return;
        }
        _rows.Children.Clear();
        _switches.Clear();
        var first = true;
        foreach (var category in EffectiveCategories)
        {
            _rows.Children.Add(CreateRow(category, first));
            first = false;
        }
        SeedDraft();
    }

    private Control CreateRow(CookieCategory category, bool first)
    {
        var label = new TextBlock { Text = category.Label, TextWrapping = TextWrapping.Wrap, LineHeight = 14 * 1.4 };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(label, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { label } };
        if (!string.IsNullOrEmpty(category.Description))
        {
            var hint = new TextBlock { Text = category.Description, TextWrapping = TextWrapping.Wrap, LineHeight = 12 * 1.4 };
            ArcTokens.Bind(hint, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(hint, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            text.Children.Add(hint);
        }
        Control trailing;
        if (category.IsRequired)
        {
            var always = new TextBlock { Text = Labels.AlwaysOn, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(always, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(always, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            trailing = always;
        }
        else
        {
            var toggle = new Switch { VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(toggle, category.Label);
            AutomationProperties.SetHelpText(toggle, category.Description ?? string.Empty);
            toggle.IsCheckedChanged += (_, _) =>
            {
                _draft[category.Id] = toggle.IsChecked == true;
                UpdateSaveHelp();
            };
            // Clicking the label toggles its switch, like <label for>.
            label.Cursor = new Cursor(StandardCursorType.Hand);
            label.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Left)
                {
                    toggle.IsChecked = toggle.IsChecked != true;
                }
            };
            _switches[category.Id] = toggle;
            trailing = toggle;
        }
        Grid.SetColumn(trailing, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16, Children = { text, trailing } };
        var row = new Border
        {
            Padding = new Thickness(6, 10),
            BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
            Child = grid
        };
        ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return row;
    }

    private void UpdateSaveHelp()
    {
        if (_save is null)
        {
            return;
        }
        var optional = 0;
        var allowed = 0;
        foreach (var category in EffectiveCategories)
        {
            if (category.IsRequired)
            {
                continue;
            }
            optional++;
            if (_draft.TryGetValue(category.Id, out var on) && on)
            {
                allowed++;
            }
        }
        AutomationProperties.SetHelpText(_save, $"{allowed} of {optional} optional categories allowed");
    }

    #endregion

    #region 状态与布局

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loaded)
        {
            // Read storage before the first face shows, so a visitor who decided never sees the bar flash.
            if (StorageKey is { Length: > 0 } key && Consent is null && Read(key, Version, Storage) is { } stored)
            {
                SetCurrentValue(ConsentProperty, stored.Value);
            }
            _loaded = true;
        }
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _clientSizeSubscription = topLevel.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new ClientSizeObserver(this));
        }
        UpdateMode();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clientSizeSubscription?.Dispose();
        _clientSizeSubscription = null;
    }

    private sealed class ClientSizeObserver : IObserver<Size>
    {
        private readonly CookieConsent _owner;

        public ClientSizeObserver(CookieConsent owner) => _owner = owner;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value)
        {
            if (_owner._listScroll is not null)
            {
                // The list scrolls past min(46vh, 300px).
                _owner._listScroll.MaxHeight = Math.Min(value.Height * 0.46, 300);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConsentProperty || change.Property == IsReopenButtonVisibleProperty ||
            change.Property == PositionProperty || change.Property == ReopenSideProperty)
        {
            UpdateMode();
        }
        else if (change.Property == IsPreferencesOpenProperty)
        {
            if (IsPreferencesOpen && _focusTarget != CookieConsentMode.Panel)
            {
                SeedDraft();
            }
            UpdateMode();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            BuildRows();
            InvalidateMeasure();
        }
        else if (change.Property == LabelsProperty || change.Property == PolicyProperty)
        {
            ApplyLabels();
            if (change.Property == LabelsProperty)
            {
                BuildRows();
            }
        }
    }

    private void UpdateMode()
    {
        if (_surface is null)
        {
            return;
        }
        var decided = Consent is not null;
        var mode = !_loaded ? CookieConsentMode.None
            : IsPreferencesOpen ? CookieConsentMode.Panel
            : !decided ? CookieConsentMode.Bar
            : IsReopenButtonVisible ? CookieConsentMode.Chip
            : CookieConsentMode.None;
        var align = Position switch
        {
            CookieConsentPosition.BottomLeft => CookieConsentAlign.Left,
            CookieConsentPosition.BottomRight => CookieConsentAlign.Right,
            _ => CookieConsentAlign.Center
        };
        var chip = ReopenSide switch
        {
            CookieConsentSide.Left => CookieConsentAlign.Left,
            CookieConsentSide.Right => CookieConsentAlign.Right,
            _ => align == CookieConsentAlign.Right ? CookieConsentAlign.Right : CookieConsentAlign.Left
        };
        // Reopened preferences grow out of the corner button, so the panel opens on its side once a choice exists.
        var panel = decided && IsReopenButtonVisible ? chip : align;
        _surface.SetMode(mode, align, panel, chip);
        if (_focusTarget is { } target && target == mode)
        {
            _focusTarget = null;
            Dispatcher.UIThread.Post(() => FocusFace(target), DispatcherPriority.Loaded);
        }
        else if (mode == CookieConsentMode.None)
        {
            _focusTarget = null;
        }
    }

    private void FocusFace(CookieConsentMode mode)
    {
        switch (mode)
        {
            case CookieConsentMode.Panel:
                _title?.Focus();
                break;
            case CookieConsentMode.Bar:
                _customize?.Focus();
                break;
            case CookieConsentMode.Chip:
                _chip?.Focus();
                break;
        }
    }

    private int _barLayout = -1;
    private int _panelLayout = -1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var layer = double.IsInfinity(availableSize.Width) ? CookieConsentSurface.BarMax : Math.Max(0, availableSize.Width - 32);
        var bar = Math.Min(layer, CookieConsentSurface.BarMax);
        var panel = Math.Min(layer, CookieConsentSurface.PanelMax);
        ApplyBarLayout(bar < StackBelow ? bar - 28 <= 300 ? 2 : 1 : 0);
        ApplyPanelLayout(panel - 36 >= 340 ? 1 : 0);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>0: one row; 1: message above three buttons; 2: refuse and accept share a row above Customize.</summary>
    private void ApplyBarLayout(int layout)
    {
        if (layout == _barLayout || _barGrid is null || _barActions is null || _customize is null || _barReject is null || _barAccept is null || _message is null)
        {
            return;
        }
        _barLayout = layout;
        PseudoClasses.Set(BarStackedPseudoClass, layout > 0);
        PseudoClasses.Set(BarCompactPseudoClass, layout == 2);
        if (layout == 0)
        {
            Place(_message, 0, 0, 1);
            Place(_barActions, 0, 1, 1);
            _barGrid.ColumnSpacing = 16;
            _barGrid.RowSpacing = 0;
            _barGrid.Margin = default;
            _barActions.ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto");
            Place(_customize, 0, 0, 1);
            Place(_barReject, 0, 1, 1);
            Place(_barAccept, 0, 2, 1);
        }
        else
        {
            Place(_message, 0, 0, 2);
            Place(_barActions, 1, 0, 2);
            _barGrid.ColumnSpacing = 0;
            _barGrid.RowSpacing = 12;
            _barGrid.Margin = new Thickness(0, 6, 8, 0);
            if (layout == 1)
            {
                _barActions.ColumnDefinitions = new ColumnDefinitions("Auto,*,*");
                Place(_customize, 0, 0, 1);
                Place(_barReject, 0, 1, 1);
                Place(_barAccept, 0, 2, 1);
            }
            else
            {
                _barActions.ColumnDefinitions = new ColumnDefinitions("*,*");
                Place(_barReject, 0, 0, 1);
                Place(_barAccept, 0, 1, 1);
                Place(_customize, 1, 0, 2);
            }
        }
        var stretch = layout > 0 ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        _customize.HorizontalAlignment = stretch;
        _barReject.HorizontalAlignment = stretch;
        _barAccept.HorizontalAlignment = stretch;
    }

    /// <summary>0: refuse and accept share a row above Save; 1: all three in one row.</summary>
    private void ApplyPanelLayout(int layout)
    {
        if (layout == _panelLayout || _panelActions is null || _panelReject is null || _panelAccept is null || _save is null)
        {
            return;
        }
        _panelLayout = layout;
        PseudoClasses.Set(PanelWidePseudoClass, layout == 1);
        if (layout == 1)
        {
            _panelActions.ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*");
            Place(_panelReject, 0, 0, 1);
            Place(_panelAccept, 0, 1, 1);
            Place(_save, 0, 2, 1);
        }
        else
        {
            _panelActions.ColumnDefinitions = new ColumnDefinitions("*,*");
            Place(_panelReject, 0, 0, 1);
            Place(_panelAccept, 0, 1, 1);
            Place(_save, 1, 0, 2);
        }
    }

    private static void Place(Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }

    #endregion
}

/// <summary>The inline policy link: underlined text whose underline darkens on hover.</summary>
internal sealed class CookieConsentPolicyLink : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(CookieConsentPolicyLink);
}
