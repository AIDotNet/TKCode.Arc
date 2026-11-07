using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

public sealed partial class ControlCenter
{
    private const double PanelWidth = 360;
    private const double PanelHeight = 396;
    private const double CompactViewport = 360;
    private const double CompactPanel = 312;
    private const double FlushViewport = 455;

    private Border? _slot;
    private Panel _root = null!;
    private ControlCenterSurface _surface = null!;
    private Grid _main = null!;
    private ControlCenterTileHost _focusHost = null!;
    private ControlCenterTileHost _notifHost = null!;
    private ControlCenterTileHost _presentHost = null!;
    private ControlCenterTile _focusTile = null!;
    private ControlCenterTile _notifTile = null!;
    private ControlCenterTile _presentTile = null!;
    private ControlCenterButton _focusChevron = null!;
    private ControlCenterButton _notifChevron = null!;
    private ControlCenterBadge _focusBadge = null!;
    private ControlCenterBadge _notifBadge = null!;
    private ControlCenterBadge _presentBadge = null!;
    private ControlCenterSwap _focusIcon = null!;
    private ControlCenterSwap _notifIcon = null!;
    private ControlCenterSwap _presentIcon = null!;
    private TextBlock _focusTitle = null!;
    private TextBlock _notifTitle = null!;
    private TextBlock _presentTitle = null!;
    private ControlCenterSwap _focusStatus = null!;
    private ControlCenterSwap _notifStatus = null!;
    private ControlCenterSwap _presentStatus = null!;
    private StackPanel _meters = null!;
    private ControlCenterMeter _volumeMeter = null!;
    private ControlCenterMeter _textMeter = null!;
    private ControlCenterSwap _volumeIcon = null!;
    private ArcGlyph _textIcon = null!;
    private TextBlock _volumeLabel = null!;
    private TextBlock _textLabel = null!;
    private TextBlock _volumeValue = null!;
    private TextBlock _textValue = null!;
    private TextBlock _textPreview = null!;
    private readonly MatrixTransform _previewTransform = new();
    private ControlCenterScrim _scrim = null!;
    private ControlCenterMorphFrame _frame = null!;
    private Grid _detail = null!;
    private ControlCenterBadge _detailBadge = null!;
    private ControlCenterSwap _detailIcon = null!;
    private TextBlock _detailTitle = null!;
    private ControlCenterButton _close = null!;
    private Panel _bodyHost = null!;
    private Grid _focusBody = null!;
    private ControlCenterModeBar _modeBar = null!;
    private ControlCenterSwap _modeDescription = null!;
    private ControlCenterDial _dial = null!;
    private Grid _actions = null!;
    private ControlCenterButton _start = null!;
    private ControlCenterButton _end = null!;
    private ControlCenterButton _update = null!;
    private Grid _notifBody = null!;
    private Switch _allSwitch = null!;
    private TextBlock _allLabel = null!;
    private TextBlock _allDescription = null!;
    private ControlCenterChannelList _channelList = null!;
    private Border _peopleRow = null!;
    private ControlCenterPeople _peopleFaces = null!;
    private TextBlock _peopleText = null!;
    private bool _compact;
    private bool _flush;
    private IDisposable? _viewportWatch;
    private int _shownVolumeIcon = -1;
    private int _shownVolumeText = -1;
    private int _shownTextText = -1;
    private bool _syncingSwitch;

    /// <summary>Parts the tests read.</summary>
    internal ControlCenterTile FocusTile => _focusTile;

    internal ControlCenterTile NotificationsTile => _notifTile;

    internal ControlCenterTile PresentingTile => _presentTile;

    internal ControlCenterTileHost FocusTileHost => _focusHost;

    internal ControlCenterTileHost NotificationsTileHost => _notifHost;

    internal ControlCenterButton FocusChevron => _focusChevron;

    internal ControlCenterButton NotificationsChevron => _notifChevron;

    internal ControlCenterSwap FocusStatus => _focusStatus;

    internal ControlCenterSwap NotificationsStatus => _notifStatus;

    internal ControlCenterSwap PresentingStatus => _presentStatus;

    internal ControlCenterSwap FocusIcon => _focusIcon;

    internal TextBlock FocusTitleText => _focusTitle;

    internal ControlCenterMeter VolumeMeter => _volumeMeter;

    internal ControlCenterMeter TextSizeMeter => _textMeter;

    internal TextBlock VolumeValueText => _volumeValue;

    internal TextBlock TextSizeValueText => _textValue;

    internal ControlCenterSwap VolumeIcon => _volumeIcon;

    internal TextBlock TextPreview => _textPreview;

    internal Grid Main => _main;

    internal ControlCenterSurface PanelSurface => _surface;

    internal ControlCenterScrim Scrim => _scrim;

    internal ControlCenterMorphFrame Frame => _frame;

    internal Grid DetailRoot => _detail;

    internal ControlCenterButton CloseButton => _close;

    internal TextBlock DetailTitle => _detailTitle;

    internal ControlCenterModeBar ModeBar => _modeBar;

    internal ControlCenterSwap ModeDescription => _modeDescription;

    internal ControlCenterDial Dial => _dial;

    internal ControlCenterButton StartButton => _start;

    internal ControlCenterButton EndButton => _end;

    internal ControlCenterButton UpdateButton => _update;

    internal Switch AllSwitch => _allSwitch;

    internal ControlCenterChannelList ChannelList => _channelList;

    internal Border PeopleRow => _peopleRow;

    internal TextBlock PeopleText => _peopleText;

    internal bool IsCompact => _compact;

    internal bool IsFlush => _flush;

    private static TextBlock Text(ArcDesignTokenKind size, ArcDesignTokenKind brush, FontWeight weight = FontWeight.Normal)
    {
        var text = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = weight,
            FontFeatures = [FontFeature.Parse("tnum")]
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, brush);
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(text, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        return text;
    }

    private static ArcGlyph Glyph(Geometry? data, double size) =>
        new() { Data = data, Size = size, StrokeThickness = 1.75, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    private static ControlCenterSwap IconSwap() =>
        new() { Kind = ControlCenterSwapKind.Pop, HorizontalContentAlignment = HorizontalAlignment.Center };

    private static ControlCenterSwap StatusSwap() => new() { Kind = ControlCenterSwapKind.Roll, ClipToBounds = false };

    private static void ShowText(ControlCenterSwap swap, string text, bool animate, ArcDesignTokenKind size = ArcDesignTokenKind.TypeXS,
        HorizontalAlignment alignment = HorizontalAlignment.Left) =>
        swap.Show(text, () =>
        {
            var block = Text(size, ArcDesignTokenKind.TextSecondary);
            block.Text = text;
            block.HorizontalAlignment = alignment;
            block.TextAlignment = alignment == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Left;
            return block;
        }, animate);

    private static void ShowIcon(ControlCenterSwap swap, Geometry geometry, bool animate, double size = 20) =>
        swap.Show(geometry, () => Glyph(geometry, size), animate);

    private ControlCenterButton Chevron()
    {
        var button = new ControlCenterButton { Content = Glyph(ArcIcons.ChevronRight, 16) };
        button.Classes.Add("chevron");
        return button;
    }

    private void BuildParts()
    {
        // Tiles: the wide Focus tile (icon and text side by side, chevron at the right), the half Notifications and
        // Presenting tiles (icon on top, text at the bottom; Notifications has a chevron in its corner).
        _focusIcon = IconSwap();
        _focusBadge = new ControlCenterBadge { Content = _focusIcon };
        _focusTitle = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        _focusStatus = StatusSwap();
        var focusText = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { _focusTitle, _focusStatus } };
        Grid.SetColumn(focusText, 1);
        _focusTile = new ControlCenterTile
        {
            Content = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*"), ColumnSpacing = 12, Children = { _focusBadge, focusText } }
        };
        _focusTile.Click += (_, _) => OnFocusTileClicked();
        _focusChevron = Chevron();
        _focusChevron.HorizontalAlignment = HorizontalAlignment.Right;
        _focusChevron.VerticalAlignment = VerticalAlignment.Center;
        _focusChevron.Margin = new Thickness(0, 0, 14, 0);
        _focusChevron.Click += (_, _) => Open(ControlCenterDetail.Focus, _focusChevron.PressedByKeyboard);
        _focusHost = new ControlCenterTileHost(_focusTile, _focusChevron);
        Grid.SetColumnSpan(_focusHost, 2);

        _notifIcon = IconSwap();
        _notifBadge = new ControlCenterBadge { Content = _notifIcon, HorizontalAlignment = HorizontalAlignment.Left };
        _notifTitle = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        _notifStatus = StatusSwap();
        _notifTile = new ControlCenterTile { Content = HalfTileContent(_notifBadge, _notifTitle, _notifStatus) };
        _notifTile.Click += (_, _) => SetCurrentValue(AreNotificationsOnProperty, !AreNotificationsOn);
        _notifChevron = Chevron();
        _notifChevron.HorizontalAlignment = HorizontalAlignment.Right;
        _notifChevron.VerticalAlignment = VerticalAlignment.Top;
        _notifChevron.Margin = new Thickness(0, 8, 8, 0);
        _notifChevron.Click += (_, _) => Open(ControlCenterDetail.Notifications, _notifChevron.PressedByKeyboard);
        _notifHost = new ControlCenterTileHost(_notifTile, _notifChevron);
        Grid.SetRow(_notifHost, 1);

        _presentIcon = IconSwap();
        _presentBadge = new ControlCenterBadge { Content = _presentIcon, HorizontalAlignment = HorizontalAlignment.Left };
        _presentTitle = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        _presentStatus = StatusSwap();
        _presentTile = new ControlCenterTile { Content = HalfTileContent(_presentBadge, _presentTitle, _presentStatus) };
        _presentTile.Click += (_, _) => SetCurrentValue(IsPresentingProperty, !IsPresenting);
        _presentHost = new ControlCenterTileHost(_presentTile, null);
        Grid.SetRow(_presentHost, 1);
        Grid.SetColumn(_presentHost, 1);

        BuildMeters();
        Grid.SetRow(_meters, 2);
        Grid.SetColumnSpan(_meters, 2);

        _main = new Grid
        {
            RowDefinitions = new RowDefinitions("84,104,*"),
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowSpacing = 8,
            ColumnSpacing = 8,
            Children = { _focusHost, _notifHost, _presentHost, _meters }
        };

        _surface = new ControlCenterSurface();
        _scrim = new ControlCenterScrim { IsVisible = false, Opacity = 0 };
        _scrim.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Close(false);
        };
        BuildDetail();
        _root = new Panel
        {
            Height = PanelHeight,
            Children = { _surface, _main, _scrim, _frame }
        };
        BuildMotion();
        ApplyLayoutMode();
    }

    private static Control HalfTileContent(ControlCenterBadge badge, TextBlock title, ControlCenterSwap status)
    {
        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Bottom, Children = { title, status } };
        Grid.SetRow(text, 2);
        return new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = { badge, text } };
    }

    private void BuildMeters()
    {
        _volumeIcon = IconSwap();
        _volumeIcon.Width = 16;
        ArcTokens.Bind(_volumeIcon, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _volumeLabel = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _volumeValue = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary);
        _volumeMeter = new ControlCenterMeter
        {
            Minimum = ControlCenterMath.VolumeMinimum,
            Maximum = ControlCenterMath.VolumeMaximum,
            Value = Volume,
            Step = 1
        };
        _volumeMeter.SyncDisplayed();
        _volumeMeter.UserChanged += v => SetCurrentValue(VolumeProperty, v);
        _volumeMeter.DisplayedChanged += _ => UpdateMeterTexts();

        _textIcon = Glyph(ArcIcons.Type, 16);
        ArcTokens.Bind(_textIcon, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _textLabel = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _textValue = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary);
        _textPreview = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        _textPreview.HorizontalAlignment = HorizontalAlignment.Center;
        _textPreview.VerticalAlignment = VerticalAlignment.Center;
        _textPreview.TextTrimming = TextTrimming.None;
        _textPreview.RenderTransform = _previewTransform;
        _textPreview.RenderTransformOrigin = RelativePoint.Center;
        _textMeter = new ControlCenterMeter
        {
            Minimum = ControlCenterMath.TextSizeMinimum,
            Maximum = ControlCenterMath.TextSizeMaximum,
            Value = TextSize,
            Step = ControlCenterMath.TextSizeStep
        };
        _textMeter.SyncDisplayed();
        _textMeter.UserChanged += v => SetCurrentValue(TextSizeProperty, v);
        _textMeter.DisplayedChanged += _ => UpdateMeterTexts();

        _meters = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(6, 4, 6, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                MeterBlock(_volumeIcon, _volumeLabel, null, _volumeValue, _volumeMeter),
                MeterBlock(_textIcon, _textLabel, _textPreview, _textValue, _textMeter)
            }
        };
        UpdateVolumeIcon(false);
        UpdateMeterTexts();
    }

    private static Control MeterBlock(Control icon, TextBlock label, TextBlock? preview, TextBlock value, ControlCenterMeter meter)
    {
        label.VerticalAlignment = VerticalAlignment.Center;
        value.VerticalAlignment = VerticalAlignment.Center;
        value.TextTrimming = TextTrimming.None;
        icon.VerticalAlignment = VerticalAlignment.Center;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,Auto,Auto"), ColumnSpacing = 8, Height = 20 };
        head.Children.Add(icon);
        Grid.SetColumn(label, 1);
        head.Children.Add(label);
        if (preview is not null)
        {
            var slot = new Panel { Width = 26, Children = { preview } };
            Grid.SetColumn(slot, 2);
            head.Children.Add(slot);
        }
        Grid.SetColumn(value, 3);
        head.Children.Add(value);
        return new StackPanel { Spacing = 8, Children = { head, meter } };
    }

    private void BuildDetail()
    {
        _detailIcon = IconSwap();
        _detailBadge = new ControlCenterBadge { Content = _detailIcon, IsOn = true };
        _detailTitle = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        ArcTokens.Bind(_detailTitle, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        _detailTitle.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_detailTitle, 1);
        _close = new ControlCenterButton { Content = Glyph(ArcIcons.X, 16), VerticalAlignment = VerticalAlignment.Center };
        _close.Classes.Add("close");
        _close.Click += (_, _) => Close(_close.PressedByKeyboard);
        Grid.SetColumn(_close, 2);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("36,*,32"),
            ColumnSpacing = 12,
            Margin = new Thickness(14, 12, 12, 4),
            Children = { _detailBadge, _detailTitle, _close }
        };

        BuildFocusBody();
        BuildNotificationsBody();
        _bodyHost = new Panel { Margin = new Thickness(14, 8, 14, 14) };
        Grid.SetRow(_bodyHost, 1);
        _detail = new Grid { RowDefinitions = new RowDefinitions("52,*"), Children = { header, _bodyHost } };
        KeyboardNavigation.SetTabNavigation(_detail, KeyboardNavigationMode.Cycle);
        _frame = new ControlCenterMorphFrame { Child = _detail, Margin = new Thickness(8), IsVisible = false };
    }

    private void BuildFocusBody()
    {
        _modeBar = new ControlCenterModeBar();
        _modeBar.SelectionChanged += OnModePicked;
        _modeDescription = StatusSwap();
        _modeDescription.HorizontalAlignment = HorizontalAlignment.Stretch;
        _modeDescription.HorizontalContentAlignment = HorizontalAlignment.Center;
        Grid.SetRow(_modeDescription, 1);
        _dial = new ControlCenterDial { Width = 164, Height = 164, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _dial.UserChanged += v =>
        {
            _draftMinutes = v;
            UpdateDialTexts();
        };
        _dial.ShownNumberChanged += _ => UpdateDialTexts();
        Grid.SetRow(_dial, 2);
        _start = new ControlCenterButton { HorizontalAlignment = HorizontalAlignment.Stretch };
        _start.Classes.Add("primary");
        _start.Click += (_, _) => CommitFocus(_start.PressedByKeyboard);
        Grid.SetColumnSpan(_start, 2);
        _end = new ControlCenterButton { HorizontalAlignment = HorizontalAlignment.Stretch };
        _end.Classes.Add("secondary");
        _end.Click += (_, _) =>
        {
            var keyboard = _end.PressedByKeyboard;
            EndFocus();
            Close(keyboard);
        };
        _update = new ControlCenterButton { HorizontalAlignment = HorizontalAlignment.Stretch };
        _update.Classes.Add("primary");
        _update.Click += (_, _) => CommitFocus(_update.PressedByKeyboard);
        Grid.SetColumn(_update, 1);
        _actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 8,
            Height = 38,
            Children = { _start, _end, _update }
        };
        Grid.SetRow(_actions, 3);
        _focusBody = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = 10,
            Children = { _modeBar, _modeDescription, _dial, _actions }
        };
        UpdateFocusActions();
    }

    private Grid SwitchRow(TextBlock label, TextBlock? description, Switch toggle)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { label } };
        if (description is not null)
        {
            text.Children.Add(description);
        }
        toggle.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(toggle, 1);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(0, 7),
            Children = { text, toggle }
        };
    }

    private void BuildNotificationsBody()
    {
        _allLabel = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
        _allDescription = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary);
        _allSwitch = new Switch { IsChecked = AreNotificationsOn };
        _allSwitch.IsCheckedChanged += (_, _) =>
        {
            if (!_syncingSwitch)
            {
                SetCurrentValue(AreNotificationsOnProperty, _allSwitch.IsChecked == true);
            }
        };
        var master = SwitchRow(_allLabel, _allDescription, _allSwitch);
        _channelList = new ControlCenterChannelList();
        var channels = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 4, 0, 0),
            Child = _channelList
        };
        ArcTokens.Bind(channels, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        var scroller = new ScrollViewer
        {
            Content = channels,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroller, 1);
        _peopleFaces = new ControlCenterPeople { VerticalAlignment = VerticalAlignment.Center };
        _peopleText = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary);
        _peopleText.TextWrapping = TextWrapping.Wrap;
        _peopleText.TextTrimming = TextTrimming.CharacterEllipsis;
        _peopleText.MaxLines = 2;
        _peopleText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_peopleText, 1);
        _peopleRow = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 10, 0, 0),
            IsVisible = false,
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Children = { _peopleFaces, _peopleText } }
        };
        ArcTokens.Bind(_peopleRow, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        Grid.SetRow(_peopleRow, 2);
        _notifBody = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = { master, scroller, _peopleRow } };
    }

    private void RebuildChannelRows()
    {
        _channelList.Children.Clear();
        foreach (var channel in _channels)
        {
            var label = Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground);
            label.Text = channel.Label;
            TextBlock? description = null;
            if (!string.IsNullOrEmpty(channel.Description))
            {
                description = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary);
                description.Text = channel.Description;
            }
            var toggle = new Switch { IsChecked = channel.IsOn };
            AutomationProperties.SetName(toggle, channel.Label);
            var bound = channel;
            toggle.IsCheckedChanged += (_, _) => bound.IsOn = toggle.IsChecked == true;
            toggle.Tag = channel;
            _channelList.Children.Add(SwitchRow(label, description, toggle));
        }
        _channelList.IsDimmed = !AreNotificationsOn;
    }

    private void OnChannelsChanged(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ControlCenterChannel channel in e.OldItems)
            {
                channel.PropertyChanged -= OnChannelPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (ControlCenterChannel channel in e.NewItems)
            {
                channel.PropertyChanged += OnChannelPropertyChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Reset gives no old items: detach from everything we may have seen and attach to what is there now.
            foreach (var row in _channelList.Children)
            {
                if (row is Grid { Children: [_, Switch { Tag: ControlCenterChannel old }] })
                {
                    old.PropertyChanged -= OnChannelPropertyChanged;
                }
            }
            foreach (var channel in _channels)
            {
                channel.PropertyChanged -= OnChannelPropertyChanged;
                channel.PropertyChanged += OnChannelPropertyChanged;
            }
        }
        RebuildChannelRows();
        UpdateNotificationsTile(false);
    }

    private void OnChannelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ControlCenterChannel.IsOnProperty || sender is not ControlCenterChannel channel)
        {
            return;
        }
        foreach (var row in _channelList.Children)
        {
            if (row is Grid { Children: [_, Switch toggle] } && ReferenceEquals(toggle.Tag, channel) && toggle.IsChecked != channel.IsOn)
            {
                toggle.IsChecked = channel.IsOn;
            }
        }
        UpdateNotificationsTile(true);
        RaiseStateChanged(ControlCenterChangeReason.ChannelToggled);
    }

    private void OnPeopleChanged()
    {
        _peopleFaces.People = _people.ToArray();
        _peopleRow.IsVisible = _people.Count > 0;
        var names = new string[_people.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = ControlCenterMath.FirstName(_people[i].Name);
        }
        _peopleText.Text = Labels.PeopleSentence(names);
    }

    private void OnModesChanged()
    {
        var labels = new string[_focusModes.Count];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = _focusModes[i].Label;
        }
        _modeBar.SetModes(labels);
        UpdateFocusTile(false);
        if (ActiveDetail == ControlCenterDetail.Focus)
        {
            LoadFocusDraft();
        }
    }

    /// <summary>Applies the labels to every text and accessible name (state lines without animation when <paramref name="animate"/> is false).</summary>
    private void UpdateTexts(bool animate)
    {
        var labels = Labels;
        AutomationProperties.SetName(_focusChevron, labels.FocusSettingsName);
        AutomationProperties.SetName(_notifChevron, labels.ChannelSettingsName);
        AutomationProperties.SetName(_close, labels.Close);
        AutomationProperties.SetName(_modeBar, labels.ModeGroupName);
        AutomationProperties.SetName(_dial, labels.SessionLengthName);
        AutomationProperties.SetName(_volumeMeter, labels.VolumeLabel);
        AutomationProperties.SetName(_textMeter, labels.TextSizeLabel);
        AutomationProperties.SetName(_allSwitch, labels.AllNotifications);
        _notifTitle.Text = labels.NotificationsTitle;
        _presentTitle.Text = labels.PresentingTitle;
        _volumeLabel.Text = labels.VolumeLabel;
        _textLabel.Text = labels.TextSizeLabel;
        _textPreview.Text = labels.TextPreview;
        _allLabel.Text = labels.AllNotifications;
        _allDescription.Text = labels.AllNotificationsDescription;
        _dial.UnitText = labels.MinutesUnit;
        _start.Content = labels.StartSession;
        _end.Content = labels.EndSession;
        _update.Content = labels.UpdateSession;
        _shownVolumeText = -1;
        _shownTextText = -1;
        UpdateMeterTexts();
        UpdateFocusTile(animate);
        UpdateNotificationsTile(animate);
        UpdatePresentingTile(animate);
        UpdateDetailHeader(false);
        UpdateDialTexts();
    }

    private ControlCenterFocusMode? SessionMode => FocusSession is { } session ? FindMode(session.ModeId) : null;

    private void UpdateFocusTile(bool animate)
    {
        var labels = Labels;
        var session = FocusSession;
        var mode = SessionMode;
        _focusTile.IsChecked = session is not null;
        _focusBadge.IsOn = session is not null;
        // The title changes at once (Arc does not blur it); the state line and the icon trade places.
        _focusTitle.Text = session is null ? labels.FocusTitle : mode?.Label ?? labels.FocusTitle;
        var status = session is null ? labels.Off : labels.Until(labels.FormatTime(session.Until));
        ShowText(_focusStatus, status, animate);
        ShowIcon(_focusIcon, session is null ? ControlCenterIcons.Headphones : mode?.Icon ?? ControlCenterIcons.Headphones, animate);
        AutomationProperties.SetName(_focusTile, $"{_focusTitle.Text}, {status}");
    }

    private void UpdateNotificationsTile(bool animate)
    {
        var labels = Labels;
        var on = AreNotificationsOn;
        _notifTile.IsChecked = on;
        _notifBadge.IsOn = on;
        var count = 0;
        foreach (var channel in _channels)
        {
            if (channel.IsOn)
            {
                count++;
            }
        }
        var status = on ? labels.ChannelCount(count, _channels.Count) : labels.NotificationsPaused;
        ShowText(_notifStatus, status, animate);
        ShowIcon(_notifIcon, on ? ArcIcons.Bell : ArcIcons.BellOff, animate);
        AutomationProperties.SetName(_notifTile, $"{labels.NotificationsTitle}, {status}");
        _syncingSwitch = true;
        try
        {
            _allSwitch.IsChecked = on;
        }
        finally
        {
            _syncingSwitch = false;
        }
        if (_shown == ControlCenterDetail.Notifications)
        {
            UpdateDetailHeader(animate);
        }
    }

    private void UpdatePresentingTile(bool animate)
    {
        var labels = Labels;
        var on = IsPresenting;
        _presentTile.IsChecked = on;
        _presentBadge.IsOn = on;
        var status = on ? labels.PresentingOn : labels.Off;
        ShowText(_presentStatus, status, animate);
        ShowIcon(_presentIcon, ControlCenterIcons.Presentation, false);
        AutomationProperties.SetName(_presentTile, $"{labels.PresentingTitle}, {status}");
    }

    private void UpdateVolumeIcon(bool animate)
    {
        var value = Volume;
        var kind = value <= 0 ? 0 : value < 50 ? 1 : 2;
        if (kind == _shownVolumeIcon)
        {
            return;
        }
        _shownVolumeIcon = kind;
        ShowIcon(_volumeIcon, ControlCenterIcons.Volume(value), animate, 16);
    }

    /// <summary>The meters' numbers count with the drawn level (whole steps), and "Aa" scales with the drawn text size.</summary>
    private void UpdateMeterTexts()
    {
        var labels = Labels;
        var volume = ControlCenterMath.Snap(_volumeMeter.DisplayedValue, ControlCenterMath.VolumeMinimum, ControlCenterMath.VolumeMaximum, 1);
        if (volume != _shownVolumeText)
        {
            _shownVolumeText = volume;
            _volumeValue.Text = labels.Percent(volume);
        }
        var size = ControlCenterMath.Snap(_textMeter.DisplayedValue, ControlCenterMath.TextSizeMinimum, ControlCenterMath.TextSizeMaximum,
            ControlCenterMath.TextSizeStep);
        if (size != _shownTextText)
        {
            _shownTextText = size;
            _textValue.Text = labels.Percent(size);
        }
        var scale = _textMeter.DisplayedValue / 100;
        _previewTransform.Matrix = Matrix.CreateScale(scale, scale);
        AutomationProperties.SetHelpText(_volumeMeter, labels.Percent(Volume));
        AutomationProperties.SetHelpText(_textMeter, labels.Percent(TextSize));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewportWatch = Primitives.ArcViewport.Observe(this, _ => ApplyLayoutMode());
        ApplyLayoutMode();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewportWatch?.Dispose();
        _viewportWatch = null;
        StopClock();
    }

    /// <summary>The panel is min(available, 360) wide and always 396 high.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? PanelWidth : Math.Min(PanelWidth, availableSize.Width);
        base.MeasureOverride(new Size(width, PanelHeight));
        return new Size(width, PanelHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(PanelWidth, finalSize.Width);
        base.ArrangeOverride(new Size(width, PanelHeight));
        return new Size(width, PanelHeight);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayoutMode();
    }

    /// <summary>
    /// The measured breakpoints: a viewport of 360 or less (or a panel squeezed to 312 or less) tightens the spacing and
    /// shrinks the dial to 150; a viewport of 455 or less lays the panel flush (no border, radius, shadow or fill) while
    /// the detail keeps its own surface. Arc measures both on the viewport, so this reads ArcViewport (the nearest viewport host, otherwise the window).
    /// </summary>
    private void ApplyLayoutMode()
    {
        var viewport = Primitives.ArcViewport.GetSize(this)?.Width ?? double.PositiveInfinity;
        var panel = Bounds.Width > 0 ? Math.Min(PanelWidth, Bounds.Width) : PanelWidth;
        var compact = viewport <= CompactViewport || panel <= CompactPanel;
        var flush = viewport <= FlushViewport;
        if (compact == _compact && flush == _flush && _main.Margin != default)
        {
            return;
        }
        _compact = compact;
        _flush = flush;
        _surface.IsFlush = flush;
        _scrim.IsFlush = flush;
        var pad = compact ? 6 : 10;
        var edge = flush ? 0 : 1;
        _main.Margin = new Thickness(pad + edge);
        // The detail sits 8 inside the border (360 → 342 × 378).
        _frame.Margin = new Thickness(8 + edge);
        _main.RowSpacing = compact ? 6 : 8;
        _main.ColumnSpacing = compact ? 6 : 8;
        _main.RowDefinitions[2].Height = GridLength.Star;
        _focusTile.Padding = compact ? new Thickness(10, 10, 56, 10) : new Thickness(14, 12, 56, 12);
        _notifTile.Padding = compact ? new Thickness(10) : new Thickness(14, 12);
        _presentTile.Padding = _notifTile.Padding;
        _focusChevron.Margin = new Thickness(0, 0, compact ? 10 : 14, 0);
        _dial.Width = compact ? 150 : 164;
        _dial.Height = _dial.Width;
    }
}
