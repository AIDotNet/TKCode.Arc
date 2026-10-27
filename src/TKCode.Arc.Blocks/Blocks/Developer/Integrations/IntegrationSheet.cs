using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The body of the Integrations drawer (Arc design reference): the status block (the same pill as the row and a line
/// saying who connected it, waiting for consent, not yet linked, or why it failed), the permissions (a heading, a lead in
/// the future or present tense and one line per permission), and, once connected, the settings: the sync frequency menu
/// with its hint and the hold-to-disconnect button with its note. The settings open and close on a Smooth height spring
/// with a fade (240ms in, 160ms out). Text changes blur-swap.
/// </summary>
[TemplatePart(PillPart, typeof(IntegrationConnectPill))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(MetaPart, typeof(ArcContentSwap))]
[TemplatePart(LeadPart, typeof(ArcContentSwap))]
[TemplatePart(ScopesPart, typeof(Panel))]
[TemplatePart(SettingsPart, typeof(SpringSizeDecorator))]
[TemplatePart(SettingsBodyPart, typeof(Control))]
[TemplatePart(FrequencyPart, typeof(Select))]
[TemplatePart(HoldPart, typeof(HoldToConfirm))]
internal sealed class IntegrationSheet : TemplatedControl
{
    internal const string PillPart = "PART_Pill";
    internal const string AvatarPart = "PART_Avatar";
    internal const string MetaPart = "PART_Meta";
    internal const string LeadPart = "PART_Lead";
    internal const string ScopesPart = "PART_Scopes";
    internal const string SettingsPart = "PART_Settings";
    internal const string SettingsBodyPart = "PART_SettingsBody";
    internal const string FrequencyPart = "PART_Frequency";
    internal const string HoldPart = "PART_Hold";

    /// <summary>The permissions heading.</summary>
    public static readonly StyledProperty<string?> AccessTitleProperty =
        AvaloniaProperty.Register<IntegrationSheet, string?>(nameof(AccessTitle));

    /// <summary>The note under the disconnect button.</summary>
    public static readonly StyledProperty<string?> DisconnectNoteProperty =
        AvaloniaProperty.Register<IntegrationSheet, string?>(nameof(DisconnectNote));

    /// <summary>Whether the frequency menu is shown (there are options).</summary>
    public static readonly StyledProperty<bool> HasFrequencyProperty =
        AvaloniaProperty.Register<IntegrationSheet, bool>(nameof(HasFrequency));

    /// <summary>Whether the avatar shows (someone other than "you" is known).</summary>
    public static readonly StyledProperty<bool> HasPersonProperty =
        AvaloniaProperty.Register<IntegrationSheet, bool>(nameof(HasPerson));

    private IntegrationItem? _item;
    private IntegrationConnectPill? _pill;
    private Avatar? _avatar;
    private ArcContentSwap? _meta;
    private ArcContentSwap? _lead;
    private Panel? _scopes;
    private SpringSizeDecorator? _settings;
    private Control? _settingsBody;
    private Select? _frequency;
    private HoldToConfirm? _hold;
    private MotionValue<double>? _settingsFade;
    private string? _metaText;
    private string? _leadText;
    private bool _syncingFrequency;
    private IReadOnlyList<IntegrationSyncOption> _options = Array.Empty<IntegrationSyncOption>();

    /// <inheritdoc cref="AccessTitleProperty"/>
    public string? AccessTitle { get => GetValue(AccessTitleProperty); set => SetValue(AccessTitleProperty, value); }

    /// <inheritdoc cref="DisconnectNoteProperty"/>
    public string? DisconnectNote { get => GetValue(DisconnectNoteProperty); set => SetValue(DisconnectNoteProperty, value); }

    /// <inheritdoc cref="HasFrequencyProperty"/>
    public bool HasFrequency { get => GetValue(HasFrequencyProperty); set => SetValue(HasFrequencyProperty, value); }

    /// <inheritdoc cref="HasPersonProperty"/>
    public bool HasPerson { get => GetValue(HasPersonProperty); set => SetValue(HasPersonProperty, value); }

    /// <summary>The block that owns the drawer.</summary>
    internal Integrations? Owner { get; set; }

    /// <summary>The app shown.</summary>
    internal IntegrationItem? Item => _item;

    internal IntegrationConnectPill? Pill => _pill;

    internal Select? Frequency => _frequency;

    internal HoldToConfirm? Hold => _hold;

    internal SpringSizeDecorator? Settings => _settings;

    /// <summary>The status line now shown (tests).</summary>
    internal string? MetaText => _metaText;

    /// <summary>The permissions lead now shown (tests).</summary>
    internal string? LeadText => _leadText;

    /// <summary>Shows <paramref name="item"/> (null clears); switching apps does not animate.</summary>
    public void Show(IntegrationItem? item)
    {
        if (ReferenceEquals(item, _item))
        {
            Refresh(animate: false);
            return;
        }
        if (_item is not null)
        {
            _item.PropertyChanged -= OnItemChanged;
            _item.Scopes.CollectionChanged -= OnScopesChanged;
        }
        _item = item;
        if (_item is not null)
        {
            _item.PropertyChanged += OnItemChanged;
            _item.Scopes.CollectionChanged += OnScopesChanged;
        }
        _hold?.SetCurrentValue(HoldToConfirm.IsConfirmedProperty, false);
        _metaText = null;
        _leadText = null;
        RebuildScopes();
        Refresh(animate: false);
    }

    /// <summary>Re-reads the labels, the options and the item.</summary>
    public void Refresh(bool animate)
    {
        var owner = Owner;
        var labels = owner?.Labels ?? Integrations.DefaultLabels;
        _options = owner?.EffectiveSyncOptions ?? Array.Empty<IntegrationSyncOption>();
        AccessTitle = labels.AccessTitle;
        HasFrequency = _options.Count > 0;
        if (_item is not { } item)
        {
            return;
        }
        var app = item.Name;
        DisconnectNote = labels.DisconnectNote(app);
        if (_pill is not null)
        {
            _pill.Labels = labels;
            _pill.AppName = app;
            _pill.Status = item.Status;
        }
        if (_hold is not null)
        {
            _hold.Label = labels.HoldToDisconnect;
            _hold.ConfirmedLabel = labels.Disconnected;
        }
        UpdateMeta(item, labels, animate);
        SetSwap(_lead, ref _leadText, item.Status == IntegrationStatus.Connected
            ? labels.AccessLeadPresent(app)
            : labels.AccessLeadFuture(app), animate);
        UpdateFrequency(item, labels);
        UpdateSettings(item.Status == IntegrationStatus.Connected, animate);
    }

    /// <summary>Puts the disconnect button back (after the disconnect settled or failed).</summary>
    public void ResetHold() => _hold?.SetCurrentValue(HoldToConfirm.IsConfirmedProperty, false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
        }
        if (_frequency is not null)
        {
            _frequency.SelectionChanged -= OnFrequencyChanged;
        }
        if (_hold is not null)
        {
            _hold.Confirmed -= OnHoldConfirmed;
        }
        _pill = e.NameScope.Find<IntegrationConnectPill>(PillPart);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _meta = e.NameScope.Find<ArcContentSwap>(MetaPart);
        _lead = e.NameScope.Find<ArcContentSwap>(LeadPart);
        _scopes = e.NameScope.Find<Panel>(ScopesPart);
        _settings = e.NameScope.Find<SpringSizeDecorator>(SettingsPart);
        _settingsBody = e.NameScope.Find<Control>(SettingsBodyPart);
        _frequency = e.NameScope.Find<Select>(FrequencyPart);
        _hold = e.NameScope.Find<HoldToConfirm>(HoldPart);
        if (_pill is not null)
        {
            _pill.Click += OnPillClick;
        }
        if (_frequency is not null)
        {
            _frequency.SelectionChanged += OnFrequencyChanged;
        }
        if (_hold is not null)
        {
            _hold.Icon = IntegrationIcons.Unplug;
            _hold.Confirmed += OnHoldConfirmed;
        }
        _settingsFade = null;
        _metaText = null;
        _leadText = null;
        RebuildScopes();
        Refresh(animate: false);
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh(animate: true);

    private void OnScopesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RebuildScopes();

    private void RebuildScopes()
    {
        if (_scopes is null)
        {
            return;
        }
        _scopes.Children.Clear();
        if (_item is null)
        {
            return;
        }
        for (var i = 0; i < _item.Scopes.Count; i++)
        {
            _scopes.Children.Add(new IntegrationScopeRow { Scope = _item.Scopes[i], IsLast = i == _item.Scopes.Count - 1 });
        }
    }

    private void UpdateMeta(IntegrationItem item, IntegrationLabels labels, bool animate)
    {
        string text;
        var person = false;
        switch (item.Status)
        {
            case IntegrationStatus.Connecting:
                text = labels.Waiting(item.Name);
                break;
            case IntegrationStatus.Error:
                text = string.IsNullOrWhiteSpace(item.ErrorText) ? labels.ConnectFailed(item.Name) : item.ErrorText!;
                break;
            case IntegrationStatus.Connected when item.IsConnectedByCurrentUser || item.ConnectedBy is null && item.ConnectedAt is null:
                text = item.IsConnectedByCurrentUser ? labels.ConnectedByYou : labels.ConnectedUnknown;
                break;
            case IntegrationStatus.Connected when item.ConnectedBy is null:
                text = labels.ConnectedBy(string.Empty, FormatDate(item.ConnectedAt, labels)).Trim();
                break;
            case IntegrationStatus.Connected:
                text = labels.ConnectedBy(item.ConnectedBy!, FormatDate(item.ConnectedAt, labels));
                person = true;
                break;
            default:
                text = labels.NotConnected;
                break;
        }
        HasPerson = person;
        if (_avatar is not null)
        {
            _avatar.DisplayName = person ? item.ConnectedBy : null;
        }
        SetSwap(_meta, ref _metaText, text, animate);
    }

    private static string FormatDate(DateTimeOffset? date, IntegrationLabels labels)
    {
        if (date is not { } d)
        {
            return string.Empty;
        }
        try
        {
            return d.ToLocalTime().ToString(labels.DateFormat, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return d.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
        }
    }

    private static void SetSwap(ArcContentSwap? swap, ref string? current, string text, bool animate)
    {
        if (current == text)
        {
            return;
        }
        current = text;
        if (swap is null)
        {
            return;
        }
        var block = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        if (!animate)
        {
            var mode = swap.Mode;
            swap.Mode = ContentSwapMode.None;
            swap.Content = block;
            swap.Mode = mode;
            return;
        }
        swap.Content = block;
    }

    private void UpdateFrequency(IntegrationItem item, IntegrationLabels labels)
    {
        if (_frequency is null)
        {
            return;
        }
        _syncingFrequency = true;
        try
        {
            _frequency.Label = labels.FrequencyLabel;
            if (!ReferenceEquals(_frequency.ItemsSource, _options))
            {
                _frequency.ItemsSource = _options;
                _frequency.TextSelector = o => (o as IntegrationSyncOption)?.Label;
            }
            var index = IndexOf(item.SyncFrequency);
            if (index < 0 && _options.Count > 0)
            {
                index = 0;
            }
            _frequency.SelectedIndex = index;
            _frequency.Description = index >= 0 ? _options[index].Hint : null;
        }
        finally
        {
            _syncingFrequency = false;
        }
    }

    private int IndexOf(string? key)
    {
        for (var i = 0; i < _options.Count; i++)
        {
            if (_options[i].Key == key)
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateSettings(bool open, bool animate)
    {
        if (_settings is null || _settingsBody is null)
        {
            return;
        }
        if (_settings.IsExpanded == open && _settingsFade is not { IsAnimating: true })
        {
            _settingsBody.Opacity = open ? 1 : 0;
            _settingsBody.IsHitTestVisible = open;
            return;
        }
        _settingsFade ??= CreateFade(_settingsBody);
        _settingsBody.IsHitTestVisible = open;
        if (open)
        {
            // A previous disconnect left the hold button reading "Unlinked": put it back before the settings reopen.
            ResetHold();
        }
        if (!animate || VisualRoot is null)
        {
            _settings.IsExpanded = open;
            _settingsFade.Jump(open ? 1 : 0);
            return;
        }
        var full = ArcMotion.IsFull(this);
        _settings.IsExpanded = open;
        _ = _settingsFade.AnimateTo(open ? 1 : 0, open
            ? full ? DeveloperSprings.FadeIn() : DeveloperSprings.ReducedFade()
            : full ? DeveloperSprings.FadeOut() : DeveloperSprings.ReducedFade());
    }

    private static MotionValue<double> CreateFade(Control body)
    {
        var fade = new MotionValue<double>(body, body.Opacity, MotionVectors.Double);
        fade.Changed += v => body.Opacity = v;
        return fade;
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_item is { } item)
        {
            Owner?.ConnectFromUser(item, _pill);
        }
    }

    private void OnFrequencyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingFrequency || _item is not { } item || _frequency?.SelectedItem is not IntegrationSyncOption option)
        {
            return;
        }
        _frequency.Description = option.Hint;
        Owner?.ChangeFrequency(item, option);
    }

    private void OnHoldConfirmed(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_item is { } item)
        {
            Owner?.DisconnectFromHold(item);
        }
    }

    /// <summary>Focuses the first thing worth focusing after a connect from the drawer: the menu, else the disconnect button.</summary>
    internal bool FocusSettings(NavigationMethod method)
    {
        if (HasFrequency && _frequency?.Focus(method) == true)
        {
            return true;
        }
        return _hold?.Focus(method) == true;
    }
}

/// <summary>Lucide glyphs the Core subset does not have (Lucide, ISC).</summary>
internal static class IntegrationIcons
{
    /// <summary>Lucide "unplug".</summary>
    public static readonly Avalonia.Media.Geometry Unplug = ArcIcons.Parse(
        "M19 5l3-3 M2 22l3-3 M6.3 20.3a2.4 2.4 0 0 0 3.4 0L12 18l-6-6-2.3 2.3a2.4 2.4 0 0 0 0 3.4Z M7.5 13.5 10 11 " +
        "M10.5 16.5 13 14 M12 6l6 6 2.3-2.3a2.4 2.4 0 0 0 0-3.4l-2.6-2.6a2.4 2.4 0 0 0-3.4 0Z");
}
