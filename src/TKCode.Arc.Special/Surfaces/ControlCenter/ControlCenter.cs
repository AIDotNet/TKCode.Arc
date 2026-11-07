using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A quick settings panel (Arc Control center, design reference): a 360 × 396 surface with a wide Focus tile, Notifications
/// and Presenting tiles, and two segmented meters for the alert volume and the text size. A tile toggles in place, with its
/// icon and state line trading places in a short blur; the chevron of the Focus and Notifications tiles grows the tile
/// into a detail that fills the panel (a shared-element morph over a light scrim; the tiles stay put and become inert).
/// The Focus detail picks a mode, sets the session length on a dial (one turn is two hours, 5-minute steps, resistance past
/// 5 and 120) and starts, updates or ends the session; the Notifications detail has a master switch, one switch per
/// channel and the people who are always let through. The meters follow a grab 1:1, jump on a tap, swell while held and
/// stretch at their ends.
/// <para>
/// Give it <see cref="FocusModes"/> (the first one is what a tap on the Focus tile starts), <see cref="Channels"/> and
/// optionally <see cref="People"/>; read or bind <see cref="Volume"/>, <see cref="TextSize"/>,
/// <see cref="AreNotificationsOn"/>, <see cref="IsPresenting"/> and <see cref="FocusSession"/>, or handle
/// <see cref="StateChanged"/>, which reports the whole state after every change (once per step while a meter is dragged).
/// The text size only scales the "Aa" sample: apply it to your app yourself. Edits in the Focus detail take effect only on
/// Start or Update session; closing the detail discards them. Sessions do not end on their own when their time is up.
/// </para>
/// </summary>
[TemplatePart(SlotPart, typeof(Border))]
public sealed partial class ControlCenter : TemplatedControl
{
    internal const string SlotPart = "PART_Slot";

    /// <summary>The alert volume, 0 to 100 (60). Two-way: the volume meter changes it.</summary>
    public static readonly StyledProperty<int> VolumeProperty =
        AvaloniaProperty.Register<ControlCenter, int>(nameof(Volume), 60, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => Math.Clamp(v, ControlCenterMath.VolumeMinimum, ControlCenterMath.VolumeMaximum));

    /// <summary>The text size in percent, 85 to 130 in steps of 5 (100). Two-way: the text-size meter changes it.</summary>
    public static readonly StyledProperty<int> TextSizeProperty =
        AvaloniaProperty.Register<ControlCenter, int>(nameof(TextSize), 100, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, v) => ControlCenterMath.Snap(v, ControlCenterMath.TextSizeMinimum,
                ControlCenterMath.TextSizeMaximum, ControlCenterMath.TextSizeStep));

    /// <summary>Whether notifications are on (true). Two-way: the Notifications tile and the master switch change it; the channels keep their own switches.</summary>
    public static readonly StyledProperty<bool> AreNotificationsOnProperty =
        AvaloniaProperty.Register<ControlCenter, bool>(nameof(AreNotificationsOn), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether presenting mode is on (false). Two-way: the Presenting tile changes it.</summary>
    public static readonly StyledProperty<bool> IsPresentingProperty =
        AvaloniaProperty.Register<ControlCenter, bool>(nameof(IsPresenting), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The running focus session, or null (no session). Two-way: the Focus tile and the Focus detail's buttons change it.</summary>
    public static readonly StyledProperty<ControlCenterFocusSession?> FocusSessionProperty =
        AvaloniaProperty.Register<ControlCenter, ControlCenterFocusSession?>(nameof(FocusSession), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which detail is open (None). Two-way: the chevrons, Close, Escape and a press on the scrim change it; set it to open one from code.</summary>
    public static readonly StyledProperty<ControlCenterDetail> ActiveDetailProperty =
        AvaloniaProperty.Register<ControlCenter, ControlCenterDetail>(nameof(ActiveDetail), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The words, time format and accessible names; replace to translate or reword.</summary>
    public static readonly StyledProperty<ControlCenterLabels> LabelsProperty =
        AvaloniaProperty.Register<ControlCenter, ControlCenterLabels>(nameof(Labels), new ControlCenterLabels());

    /// <summary>The clock for session end times (the system clock by default; tests pass a manual one).</summary>
    public static readonly StyledProperty<TimeProvider> TimeProviderProperty =
        AvaloniaProperty.Register<ControlCenter, TimeProvider>(nameof(TimeProvider), TimeProvider.System);

    /// <summary>The focus modes, in the order of the mode switch; the first one is what a tap on the Focus tile starts.</summary>
    public static readonly DirectProperty<ControlCenter, AvaloniaList<ControlCenterFocusMode>> FocusModesProperty =
        AvaloniaProperty.RegisterDirect<ControlCenter, AvaloniaList<ControlCenterFocusMode>>(nameof(FocusModes), o => o._focusModes);

    /// <summary>The notification channels, one switch each in the Notifications detail.</summary>
    public static readonly DirectProperty<ControlCenter, AvaloniaList<ControlCenterChannel>> ChannelsProperty =
        AvaloniaProperty.RegisterDirect<ControlCenter, AvaloniaList<ControlCenterChannel>>(nameof(Channels), o => o._channels);

    /// <summary>The people who are always let through, shown at the foot of the Notifications detail (none: the row is hidden).</summary>
    public static readonly DirectProperty<ControlCenter, AvaloniaList<ControlCenterPerson>> PeopleProperty =
        AvaloniaProperty.RegisterDirect<ControlCenter, AvaloniaList<ControlCenterPerson>>(nameof(People), o => o._people);

    /// <summary>Raised after every change with the whole state and what changed.</summary>
    public static readonly RoutedEvent<ControlCenterStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<ControlCenter, ControlCenterStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a detail starts to open.</summary>
    public static readonly RoutedEvent<ControlCenterDetailEventArgs> DetailOpenedEvent =
        RoutedEvent.Register<ControlCenter, ControlCenterDetailEventArgs>(nameof(DetailOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when a detail starts to close.</summary>
    public static readonly RoutedEvent<ControlCenterDetailEventArgs> DetailClosedEvent =
        RoutedEvent.Register<ControlCenter, ControlCenterDetailEventArgs>(nameof(DetailClosed), RoutingStrategies.Bubble);

    private readonly AvaloniaList<ControlCenterFocusMode> _focusModes = [];
    private readonly AvaloniaList<ControlCenterChannel> _channels = [];
    private readonly AvaloniaList<ControlCenterPerson> _people = [];
    private ControlCenterChangeReason? _focusReason;

    /// <summary>Creates a control center.</summary>
    public ControlCenter()
    {
        BuildParts();
        _focusModes.CollectionChanged += (_, _) => OnModesChanged();
        _channels.CollectionChanged += (_, e) => OnChannelsChanged(e);
        _people.CollectionChanged += (_, _) => OnPeopleChanged();
        UpdateTexts(false);
    }

    /// <inheritdoc cref="VolumeProperty"/>
    public int Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    /// <inheritdoc cref="TextSizeProperty"/>
    public int TextSize
    {
        get => GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    /// <inheritdoc cref="AreNotificationsOnProperty"/>
    public bool AreNotificationsOn
    {
        get => GetValue(AreNotificationsOnProperty);
        set => SetValue(AreNotificationsOnProperty, value);
    }

    /// <inheritdoc cref="IsPresentingProperty"/>
    public bool IsPresenting
    {
        get => GetValue(IsPresentingProperty);
        set => SetValue(IsPresentingProperty, value);
    }

    /// <inheritdoc cref="FocusSessionProperty"/>
    public ControlCenterFocusSession? FocusSession
    {
        get => GetValue(FocusSessionProperty);
        set => SetValue(FocusSessionProperty, value);
    }

    /// <inheritdoc cref="ActiveDetailProperty"/>
    public ControlCenterDetail ActiveDetail
    {
        get => GetValue(ActiveDetailProperty);
        set => SetValue(ActiveDetailProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public ControlCenterLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="TimeProviderProperty"/>
    public TimeProvider TimeProvider
    {
        get => GetValue(TimeProviderProperty);
        set => SetValue(TimeProviderProperty, value);
    }

    /// <inheritdoc cref="FocusModesProperty"/>
    public AvaloniaList<ControlCenterFocusMode> FocusModes => _focusModes;

    /// <inheritdoc cref="ChannelsProperty"/>
    public AvaloniaList<ControlCenterChannel> Channels => _channels;

    /// <inheritdoc cref="PeopleProperty"/>
    public AvaloniaList<ControlCenterPerson> People => _people;

    /// <inheritdoc cref="StateChangedEvent"/>
    public event EventHandler<ControlCenterStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <inheritdoc cref="DetailOpenedEvent"/>
    public event EventHandler<ControlCenterDetailEventArgs>? DetailOpened
    {
        add => AddHandler(DetailOpenedEvent, value);
        remove => RemoveHandler(DetailOpenedEvent, value);
    }

    /// <inheritdoc cref="DetailClosedEvent"/>
    public event EventHandler<ControlCenterDetailEventArgs>? DetailClosed
    {
        add => AddHandler(DetailClosedEvent, value);
        remove => RemoveHandler(DetailClosedEvent, value);
    }

    /// <summary>The whole state now: the session, notifications and each channel, presenting, volume and text size.</summary>
    public ControlCenterState GetState()
    {
        var channels = new Dictionary<string, bool>(_channels.Count, StringComparer.Ordinal);
        foreach (var channel in _channels)
        {
            channels[channel.Id] = channel.IsOn;
        }
        return new ControlCenterState(FocusSession, AreNotificationsOn, channels, IsPresenting, Volume, TextSize);
    }

    /// <summary>Opens a detail (Focus or Notifications) with the morph from its tile; None closes the open one.</summary>
    public void OpenDetail(ControlCenterDetail detail) => ActiveDetail = detail;

    /// <summary>Closes the open detail (its unsaved edits are discarded) and returns focus to its chevron if focus was inside.</summary>
    public void CloseDetail() => ActiveDetail = ControlCenterDetail.None;

    /// <summary>
    /// Starts (or replaces) a focus session in the mode with id <paramref name="modeId"/> (the first mode when null) for
    /// <paramref name="minutes"/> (the mode's default when null; rounded to 5 within 5 to 120), ending now plus the minutes.
    /// </summary>
    public void StartFocus(string? modeId = null, int? minutes = null)
    {
        var mode = FindMode(modeId) ?? (_focusModes.Count > 0 ? _focusModes[0] : null);
        if (mode is null)
        {
            return;
        }
        var length = ControlCenterMath.SnapMinutes(minutes ?? mode.DefaultMinutes);
        var until = ControlCenterMath.Until(TimeProvider.GetLocalNow(), length);
        _focusReason = FocusSession is null ? ControlCenterChangeReason.FocusStarted : ControlCenterChangeReason.FocusUpdated;
        SetCurrentValue(FocusSessionProperty, new ControlCenterFocusSession(mode.Id, length, until));
    }

    /// <summary>Ends the running focus session, if any.</summary>
    public void EndFocus()
    {
        _focusReason = ControlCenterChangeReason.FocusEnded;
        SetCurrentValue(FocusSessionProperty, null);
    }

    private ControlCenterFocusMode? FindMode(string? id)
    {
        if (id is null)
        {
            return null;
        }
        foreach (var mode in _focusModes)
        {
            if (string.Equals(mode.Id, id, StringComparison.Ordinal))
            {
                return mode;
            }
        }
        return null;
    }

    private void RaiseStateChanged(ControlCenterChangeReason reason) =>
        RaiseEvent(new ControlCenterStateChangedEventArgs(StateChangedEvent, GetState(), reason));

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlCenterPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_slot is not null)
        {
            _slot.Child = null;
        }
        _slot = e.NameScope.Find<Border>(SlotPart);
        if (_slot is not null)
        {
            _slot.Child = _root;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VolumeProperty)
        {
            _volumeMeter.Value = Volume;
            UpdateVolumeIcon(true);
            RaiseStateChanged(ControlCenterChangeReason.VolumeChanged);
        }
        else if (change.Property == TextSizeProperty)
        {
            _textMeter.Value = TextSize;
            RaiseStateChanged(ControlCenterChangeReason.TextSizeChanged);
        }
        else if (change.Property == AreNotificationsOnProperty)
        {
            _allSwitch.IsChecked = AreNotificationsOn;
            _channelList.IsDimmed = !AreNotificationsOn;
            UpdateNotificationsTile(true);
            RaiseStateChanged(ControlCenterChangeReason.NotificationsToggled);
        }
        else if (change.Property == IsPresentingProperty)
        {
            UpdatePresentingTile(true);
            RaiseStateChanged(ControlCenterChangeReason.PresentingToggled);
        }
        else if (change.Property == FocusSessionProperty)
        {
            var reason = _focusReason ?? (change.OldValue is null ? ControlCenterChangeReason.FocusStarted
                : change.NewValue is null ? ControlCenterChangeReason.FocusEnded : ControlCenterChangeReason.FocusUpdated);
            _focusReason = null;
            UpdateFocusTile(true);
            UpdateFocusActions();
            RaiseStateChanged(reason);
        }
        else if (change.Property == ActiveDetailProperty)
        {
            OnActiveDetailChanged(change.GetOldValue<ControlCenterDetail>(), ActiveDetail);
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateTexts(false);
            OnModesChanged();
            RebuildChannelRows();
            OnPeopleChanged();
        }
    }

    private sealed class ControlCenterPeer(ControlCenter owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : ((ControlCenter)Owner).Labels.PanelName;
    }
}
