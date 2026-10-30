using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A settings card for bring-your-own-key AI products (Arc Provider keys, design reference). One row per provider with its
/// mark, the masked key (<c>sk-••••4f2a</c>), a status as icon plus label (Connected, Invalid, Not set), when the key was
/// last used, and four actions: reveal for five seconds with a countdown ring, copy, rotate, and remove with undo. Pressing
/// a status tests the stored key again. Add key and Rotate open an inline form under the row that checks the key's format
/// as you type, then shows Testing connection and Connected before the row takes the new key.
/// <para>
/// Bind <see cref="Keys"/> (two-way) and handle <see cref="KeySaved"/> and <see cref="KeyRemoved"/> to persist; set
/// <see cref="TestKey"/> to test keys on your server. Providers without a key read Not set. For a demo, set
/// <see cref="Simulation"/> instead of <see cref="TestKey"/> to simulate every test; with neither, a test fails with
/// <see cref="ProviderKeysLabels.NoKeyTest"/>, so no key reads Connected without a check.
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(ListPart, typeof(Panel))]
[TemplatePart(FooterPart, typeof(Control))]
[TemplatePart(FooterTextPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(NarrowPseudoClass, LoadingPseudoClass)]
public class ProviderKeys : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string ListPart = "PART_List";
    internal const string FooterPart = "PART_Footer";
    internal const string FooterTextPart = "PART_FooterText";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the card is narrower than 560px: rows and the form stack.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="IsLoading"/> is true.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>The card re-renders relative times this often.</summary>
    internal static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The keys, one per provider (two-way): the card writes the new list on every add, rotation, removal, undo and retest.
    /// Providers without an entry read Not set; null or an empty list is the empty state, every row Not set.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<ProviderKey>?> KeysProperty =
        AvaloniaProperty.Register<ProviderKeys, IReadOnlyList<ProviderKey>?>(nameof(Keys), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which providers get a row, in order. Null or an empty list shows no rows.</summary>
    public static readonly StyledProperty<IReadOnlyList<ProviderKeyProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<ProviderKeys, IReadOnlyList<ProviderKeyProvider>?>(nameof(Providers));

    /// <summary>
    /// Tests a key and returns whether the provider accepted it. The request carries the pasted key when adding or rotating,
    /// and no key when retesting a stored one (test it on your server by provider); its token is cancelled when the form
    /// closes. Null uses <see cref="Simulation"/>; with neither, every test fails with
    /// <see cref="ProviderKeysLabels.NoKeyTest"/>.
    /// </summary>
    public static readonly StyledProperty<Func<ProviderKeyTestRequest, Task<ProviderKeyTestResult>>?> TestKeyProperty =
        AvaloniaProperty.Register<ProviderKeys, Func<ProviderKeyTestRequest, Task<ProviderKeyTestResult>>?>(nameof(TestKey));

    /// <summary>
    /// Simulated key tests while <see cref="TestKey"/> is null, for demos: stored invalid keys fail again, everything else
    /// connects, and announcements say nothing was sent. Null (the default) runs no simulated test.
    /// </summary>
    public static readonly StyledProperty<ProviderKeysSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ProviderKeys, ProviderKeysSimulation?>(nameof(Simulation));

    /// <summary>How long a revealed key stays visible before it masks itself again.</summary>
    public static readonly StyledProperty<TimeSpan> RevealDurationProperty =
        AvaloniaProperty.Register<ProviderKeys, TimeSpan>(nameof(RevealDuration), TimeSpan.FromSeconds(5));

    /// <summary>How long Undo stays after a removal; <see cref="KeyRemoved"/> is raised when it closes.</summary>
    public static readonly StyledProperty<TimeSpan> UndoDurationProperty =
        AvaloniaProperty.Register<ProviderKeys, TimeSpan>(nameof(UndoDuration), TimeSpan.FromSeconds(5));

    /// <summary>Shows placeholder rows with the real row geometry while keys load.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProviderKeys, bool>(nameof(IsLoading));

    /// <summary>The card's title, also the name of the section.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ProviderKeys, string?>(nameof(Title), "Provider keys");

    /// <summary>A line under the title; null or empty hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ProviderKeys, string?>(nameof(Description));

    /// <summary>A quiet line under the list, an encryption note by default; null or empty hides it.</summary>
    public static readonly StyledProperty<string?> FooterProperty =
        AvaloniaProperty.Register<ProviderKeys, string?>(nameof(Footer),
            "Keys are encrypted at rest and only used for requests you make.");

    /// <summary>The card width under which each row stacks (status beside the name, the key on its own line) and the form stacks.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<ProviderKeys, double>(nameof(NarrowWidth), 560);

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ProviderKeysLabels> LabelsProperty =
        AvaloniaProperty.Register<ProviderKeys, ProviderKeysLabels>(nameof(Labels), ProviderKeysLabels.Default);

    /// <summary>Raised on every change to the keys: added, rotated, removed, restored with undo, or retested. Bubbles.</summary>
    public static readonly RoutedEvent<ProviderKeysChangedEventArgs> KeysChangedEvent =
        RoutedEvent.Register<ProviderKeys, ProviderKeysChangedEventArgs>(nameof(KeysChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a new key passed its test; persist it here (the row already shows it as connected). Bubbles.</summary>
    public static readonly RoutedEvent<ProviderKeySavedEventArgs> KeySavedEvent =
        RoutedEvent.Register<ProviderKeys, ProviderKeySavedEventArgs>(nameof(KeySaved), RoutingStrategies.Bubble);

    /// <summary>Raised when the undo window of a removal closes, so a removal only reaches your server once it sticks. Bubbles.</summary>
    public static readonly RoutedEvent<ProviderKeyEventArgs> KeyRemovedEvent =
        RoutedEvent.Register<ProviderKeys, ProviderKeyEventArgs>(nameof(KeyRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when a key was copied to the clipboard, for an audit log. Bubbles.</summary>
    public static readonly RoutedEvent<ProviderKeyEventArgs> KeyCopiedEvent =
        RoutedEvent.Register<ProviderKeys, ProviderKeyEventArgs>(nameof(KeyCopied), RoutingStrategies.Bubble);

    private readonly List<ProviderKeysRow> _rows = [];
    private TextBlock? _title;
    private TextBlock? _description;
    private Panel? _list;
    private Control? _footer;
    private TextBlock? _footerText;
    private ExplainerAnnouncer? _announcer;
    private DispatcherTimer? _clock;
    private bool _writing;
    private bool _narrow;

    /// <inheritdoc cref="KeysProperty"/>
    public IReadOnlyList<ProviderKey>? Keys { get => GetValue(KeysProperty); set => SetValue(KeysProperty, value); }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IReadOnlyList<ProviderKeyProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="TestKeyProperty"/>
    public Func<ProviderKeyTestRequest, Task<ProviderKeyTestResult>>? TestKey { get => GetValue(TestKeyProperty); set => SetValue(TestKeyProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ProviderKeysSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="RevealDurationProperty"/>
    public TimeSpan RevealDuration { get => GetValue(RevealDurationProperty); set => SetValue(RevealDurationProperty, value); }

    /// <inheritdoc cref="UndoDurationProperty"/>
    public TimeSpan UndoDuration { get => GetValue(UndoDurationProperty); set => SetValue(UndoDurationProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="FooterProperty"/>
    public string? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ProviderKeysLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="KeysChangedEvent"/>
    public event EventHandler<ProviderKeysChangedEventArgs>? KeysChanged
    {
        add => AddHandler(KeysChangedEvent, value);
        remove => RemoveHandler(KeysChangedEvent, value);
    }

    /// <inheritdoc cref="KeySavedEvent"/>
    public event EventHandler<ProviderKeySavedEventArgs>? KeySaved
    {
        add => AddHandler(KeySavedEvent, value);
        remove => RemoveHandler(KeySavedEvent, value);
    }

    /// <inheritdoc cref="KeyRemovedEvent"/>
    public event EventHandler<ProviderKeyEventArgs>? KeyRemoved
    {
        add => AddHandler(KeyRemovedEvent, value);
        remove => RemoveHandler(KeyRemovedEvent, value);
    }

    /// <inheritdoc cref="KeyCopiedEvent"/>
    public event EventHandler<ProviderKeyEventArgs>? KeyCopied
    {
        add => AddHandler(KeyCopiedEvent, value);
        remove => RemoveHandler(KeyCopiedEvent, value);
    }

    /// <summary>The clock relative times are measured against (tests replace it).</summary>
    internal Func<DateTimeOffset> Now { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>How the form waits on Connected before the row updates (tests skip it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = static (delay, token) => Task.Delay(delay, token);

    /// <summary>The rows on screen.</summary>
    internal IReadOnlyList<ProviderKeysRow> Rows => _rows;

    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>Whether the card is in its narrow layout.</summary>
    internal bool IsNarrow => _narrow;

    /// <summary>The providers on screen.</summary>
    internal IReadOnlyList<ProviderKeyProvider> EffectiveProviders => Providers ?? [];

    /// <summary>The keys on screen: <see cref="Keys"/>, or none.</summary>
    internal IReadOnlyList<ProviderKey> EffectiveKeys => Keys ?? [];

    /// <summary>The row of a provider, or null.</summary>
    internal ProviderKeysRow? RowOf(string provider) =>
        _rows.FirstOrDefault(r => string.Equals(r.Provider.Id, provider, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list?.Children.Clear();
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _list = e.NameScope.Find<Panel>(ListPart);
        _footer = e.NameScope.Find<Control>(FooterPart);
        _footerText = e.NameScope.Find<TextBlock>(FooterTextPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        RebuildRows();
        RefreshChrome();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeysProperty)
        {
            if (!_writing)
            {
                ApplyKeys();
            }
        }
        else if (change.Property == ProvidersProperty)
        {
            RebuildRows();
        }
        else if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(LoadingPseudoClass, IsLoading);
            foreach (var row in _rows)
            {
                row.SetPlaceholder(IsLoading);
            }
            if (!IsLoading)
            {
                ApplyKeys();
            }
        }
        else if (change.Property == LabelsProperty)
        {
            foreach (var row in _rows)
            {
                row.Refresh();
            }
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == FooterProperty)
        {
            RefreshChrome();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateNarrow(change.GetNewValue<Rect>().Width);
        }
        else if (change.Property == NarrowWidthProperty)
        {
            UpdateNarrow(Bounds.Width);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock ??= new DispatcherTimer(ClockInterval, DispatcherPriority.Background, (_, _) => RefreshTimes());
        _clock.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock?.Stop();
    }

    /// <summary>Redraws every relative time (the 30-second clock).</summary>
    internal void RefreshTimes()
    {
        foreach (var row in _rows)
        {
            row.RefreshTimes();
        }
    }

    private void RefreshChrome()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
        if (_footerText is not null)
        {
            _footerText.Text = Footer;
        }
        if (_footer is not null)
        {
            _footer.IsVisible = !string.IsNullOrEmpty(Footer);
        }
        AutomationProperties.SetName(this, Title);
    }

    private void UpdateNarrow(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var narrow = width < NarrowWidth;
        if (narrow == _narrow)
        {
            return;
        }
        _narrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        foreach (var row in _rows)
        {
            row.SetNarrow(narrow);
        }
    }

    private void RebuildRows()
    {
        if (_list is null)
        {
            return;
        }
        _list.Children.Clear();
        _rows.Clear();
        var providers = EffectiveProviders;
        for (var i = 0; i < providers.Count; i++)
        {
            var row = new ProviderKeysRow(this, providers[i]);
            row.SetFirst(i == 0);
            row.SetNarrow(_narrow);
            row.SetPlaceholder(IsLoading);
            _rows.Add(row);
            _list.Children.Add(row);
        }
        ApplyKeys();
    }

    /// <summary>Gives every row its key from the current list.</summary>
    private void ApplyKeys()
    {
        var keys = EffectiveKeys;
        foreach (var row in _rows)
        {
            row.SetKey(Find(keys, row.Provider.Id));
        }
    }

    private static ProviderKey? Find(IReadOnlyList<ProviderKey> keys, string provider)
    {
        foreach (var key in keys)
        {
            if (string.Equals(key.Provider, provider, StringComparison.OrdinalIgnoreCase) && key.Status != ProviderKeyStatus.Unset)
            {
                return key;
            }
        }
        return null;
    }

    /// <summary>Replaces (or removes, with null) a provider's key, writes the new list back and raises <see cref="KeysChanged"/>.</summary>
    internal void Replace(ProviderKeysRow row, ProviderKey? key, ProviderKeysChange change)
    {
        var id = row.Provider.Id;
        var next = new List<ProviderKey>();
        var placed = false;
        foreach (var existing in EffectiveKeys)
        {
            if (string.Equals(existing.Provider, id, StringComparison.OrdinalIgnoreCase))
            {
                if (key is not null && !placed)
                {
                    next.Add(key);
                    placed = true;
                }
                continue;
            }
            next.Add(existing);
        }
        if (key is not null && !placed)
        {
            next.Add(key);
        }
        if (key is not null && string.IsNullOrEmpty(key.Provider))
        {
            key.Provider = id;
        }
        _writing = true;
        try
        {
            SetCurrentValue(KeysProperty, next);
        }
        finally
        {
            _writing = false;
        }
        row.SetKey(key);
        RaiseEvent(new ProviderKeysChangedEventArgs(KeysChangedEvent, next, id, change));
    }

    /// <summary>
    /// Runs <see cref="TestKey"/>, or the simulation when there is none, or fails with the no-test label. Exceptions become
    /// a failed test.
    /// </summary>
    internal async Task<ProviderKeyTestResult> RunTestAsync(ProviderKeyProvider provider, ProviderKey? stored, string? pasted, CancellationToken token)
    {
        if (TestKey is { } test)
        {
            try
            {
                return await test(new ProviderKeyTestRequest(provider.Id, pasted, token));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ProviderKeyTestResult.Fail(ex.Message);
            }
        }
        if (Simulation is { } simulation)
        {
            return await simulation.TestAsync(stored, pasted, token);
        }
        return ProviderKeyTestResult.Fail(Labels.NoKeyTest);
    }

    /// <summary>Says <paramref name="text"/> in the polite live region; simulated test results add "Simulated".</summary>
    internal void Announce(string? text, bool simulated)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (simulated && TestKey is null && Simulation is not null)
        {
            text = text + ". " + Labels.Simulated;
        }
        _announcer?.Announce(text);
    }

    internal void OnCopied(ProviderKeysRow row)
    {
        RaiseEvent(new ProviderKeyEventArgs(KeyCopiedEvent, row.Provider.Id));
        Announce(Labels.CopiedAnnouncement(row.Provider.Name), simulated: false);
    }

    internal void OnSaved(ProviderKeysRow row, string key, ProviderKeySaveMode mode) =>
        RaiseEvent(new ProviderKeySavedEventArgs(KeySavedEvent, row.Provider.Id, key, mode));

    internal void OnRemoved(ProviderKeysRow row) =>
        RaiseEvent(new ProviderKeyEventArgs(KeyRemovedEvent, row.Provider.Id));
}
