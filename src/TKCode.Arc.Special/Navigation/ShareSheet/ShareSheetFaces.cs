using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The panel face of a <see cref="ShareSheet"/>: the title and close button, the card with the link, its copy button and
/// the access level (with the access list under it when open), the channel buttons, the recipients row with its chips,
/// the recent people, an error line and the Send button. The static frame comes from the theme; the rows of items are
/// built here from the owner's lists, and every action goes back to the owner.
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ClosePart, typeof(ShareSheetIconButton))]
[TemplatePart(CardPart, typeof(Control))]
[TemplatePart(LinkRowPart, typeof(Control))]
[TemplatePart(LinkTextPart, typeof(TextBlock))]
[TemplatePart(CopyPart, typeof(ShareSheetCopyButton))]
[TemplatePart(AccessRowPart, typeof(ShareSheetAccessRow))]
[TemplatePart(AccessListPart, typeof(Panel))]
[TemplatePart(AccessListSlotPart, typeof(SpringSizeDecorator))]
[TemplatePart(ChannelsPart, typeof(Grid))]
[TemplatePart(DividerPart, typeof(Control))]
[TemplatePart(RecipientsPart, typeof(Control))]
[TemplatePart(RecipientsHeadingPart, typeof(TextBlock))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(ChipsRowPart, typeof(Panel))]
[TemplatePart(ChipsScrollerPart, typeof(ScrollViewer))]
[TemplatePart(ChipsPart, typeof(StackPanel))]
[TemplatePart(PeoplePart, typeof(Grid))]
[TemplatePart(ErrorPart, typeof(Control))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(SendPart, typeof(ShareSheetSendButton))]
[PseudoClasses(SheetPseudoClass, ListOpenPseudoClass)]
internal sealed class ShareSheetPanelFace : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string ClosePart = "PART_Close";
    internal const string CardPart = "PART_Card";
    internal const string LinkRowPart = "PART_LinkRow";
    internal const string LinkTextPart = "PART_LinkText";
    internal const string CopyPart = "PART_Copy";
    internal const string AccessRowPart = "PART_AccessRow";
    internal const string AccessListPart = "PART_AccessList";
    internal const string AccessListSlotPart = "PART_AccessListSlot";
    internal const string ListOpenPseudoClass = ":list-open";
    internal const string ChannelsPart = "PART_Channels";
    internal const string DividerPart = "PART_Divider";
    internal const string RecipientsPart = "PART_Recipients";
    internal const string RecipientsHeadingPart = "PART_RecipientsHeading";
    internal const string HintPart = "PART_Hint";
    internal const string ChipsRowPart = "PART_ChipsRow";
    internal const string ChipsScrollerPart = "PART_ChipsScroller";
    internal const string ChipsPart = "PART_Chips";
    internal const string PeoplePart = "PART_People";
    internal const string ErrorPart = "PART_Error";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string SendPart = "PART_Send";
    internal const string SheetPseudoClass = ":sheet";

    private static readonly Avalonia.Animation.Easings.Easing s_chipFillEasing = new Avalonia.Animation.Easings.SplineEasing(0.4, 0, 0.6, 1);

    private readonly ShareSheet _owner;
    private readonly Dictionary<Control, MotionValue<double>> _reflows = new();
    private readonly List<ShareSheetAccessOption> _options = new();
    private readonly List<ShareSheetChannelButton> _channelButtons = new();
    private readonly List<ShareSheetPersonButton> _personButtons = new();
    private readonly Dictionary<ShareSheetPerson, ShareSheetChip> _chips = new();
    private readonly Dictionary<ShareSheetPerson, Flight> _flights = new();
    private readonly TranslateTransform _shakeShift = new();
    private MotionValue<double>? _shake;
    private MotionValue<double>? _hintFade;
    private TextBlock? _title;
    private ShareSheetIconButton? _close;
    private Control? _card;
    private Control? _linkRow;
    private TextBlock? _linkText;
    private Control? _recipients;
    private TextBlock? _recipientsHeading;
    private TextBlock? _hint;
    private Panel? _chipsRow;
    private ScrollViewer? _chipsScroller;
    private StackPanel? _chipsHost;
    private Canvas? _ghosts;
    private Grid? _channelsHost;
    private Control? _divider;
    private Grid? _peopleHost;
    private Panel? _accessList;
    private SpringSizeDecorator? _accessListSlot;
    private Control? _error;
    private TextBlock? _errorText;

    public ShareSheetPanelFace(ShareSheet owner)
    {
        _owner = owner;
        Focusable = false;
    }

    public ShareSheetCopyButton? Copy { get; private set; }

    public ShareSheetAccessRow? AccessRow { get; private set; }

    public ShareSheetSendButton? Send { get; private set; }

    public ShareSheetIconButton? CloseButton => _close;

    public IReadOnlyList<ShareSheetAccessOption> Options => _options;

    public IReadOnlyList<ShareSheetChannelButton> ChannelButtons => _channelButtons;

    public IReadOnlyList<ShareSheetPersonButton> PersonButtons => _personButtons;

    public IReadOnlyCollection<ShareSheetChip> Chips => _chips.Values;

    /// <summary>The recipients row, which shakes when Send is pressed with nobody picked.</summary>
    internal Control? Recipients => _recipients;

    /// <summary>Its current sideways nudge (tests).</summary>
    internal double ShakeOffset => _shakeShift.X;

    /// <summary>Flights in the air (tests).</summary>
    internal int FlightCount => _flights.Count;

    public bool IsAccessListOpen => AccessRow?.IsExpanded ?? false;

    public string? ErrorMessage => _error is { IsVisible: true } ? _errorText?.Text : null;

    /// <summary>Lays the panel out for the bottom sheet (or for floating).</summary>
    public void SetSheet(bool sheet) => PseudoClasses.Set(SheetPseudoClass, sheet);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (Copy is not null)
        {
            Copy.Click -= OnCopyClick;
        }
        if (AccessRow is not null)
        {
            AccessRow.Click -= OnAccessRowClick;
        }
        if (Send is not null)
        {
            Send.Click -= OnSendClick;
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _close = e.NameScope.Find<ShareSheetIconButton>(ClosePart);
        _card = e.NameScope.Find<Control>(CardPart);
        _linkRow = e.NameScope.Find<Control>(LinkRowPart);
        _linkText = e.NameScope.Find<TextBlock>(LinkTextPart);
        Copy = e.NameScope.Find<ShareSheetCopyButton>(CopyPart);
        AccessRow = e.NameScope.Find<ShareSheetAccessRow>(AccessRowPart);
        _accessList = e.NameScope.Find<Panel>(AccessListPart);
        _accessListSlot = e.NameScope.Find<SpringSizeDecorator>(AccessListSlotPart);
        _channelsHost = e.NameScope.Find<Grid>(ChannelsPart);
        _divider = e.NameScope.Find<Control>(DividerPart);
        _recipients = e.NameScope.Find<Control>(RecipientsPart);
        _recipientsHeading = e.NameScope.Find<TextBlock>(RecipientsHeadingPart);
        _hint = e.NameScope.Find<TextBlock>(HintPart);
        _chipsRow = e.NameScope.Find<Panel>(ChipsRowPart);
        _chipsScroller = e.NameScope.Find<ScrollViewer>(ChipsScrollerPart);
        _chipsHost = e.NameScope.Find<StackPanel>(ChipsPart);
        _peopleHost = e.NameScope.Find<Grid>(PeoplePart);
        _error = e.NameScope.Find<Control>(ErrorPart);
        _errorText = e.NameScope.Find<TextBlock>(ErrorTextPart);
        Send = e.NameScope.Find<ShareSheetSendButton>(SendPart);
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (Copy is not null)
        {
            Copy.Click += OnCopyClick;
        }
        if (AccessRow is not null)
        {
            AccessRow.Click += OnAccessRowClick;
        }
        if (Send is not null)
        {
            Send.Click += OnSendClick;
        }
        if (_recipients is not null)
        {
            _recipients.RenderTransform = _shakeShift;
        }
        if (_chipsRow is not null)
        {
            _ghosts = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
            _chipsRow.Children.Add(_ghosts);
        }
        if (_accessList is not null)
        {
            AutomationProperties.SetAccessibilityView(_accessList, AccessibilityView.Content);
        }
        _chips.Clear();
        RefreshWords();
        RebuildAll();
    }

    /// <summary>Reads the owner's words into the fixed parts.</summary>
    public void RefreshWords()
    {
        var labels = _owner.Labels;
        if (_title is not null)
        {
            _title.Text = labels.Title(_owner.Title ?? string.Empty);
        }
        AutomationProperties.SetName(this, labels.Title(_owner.Title ?? string.Empty));
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
            Tooltip.SetTip(_close, labels.Close);
        }
        if (Copy is not null)
        {
            Copy.SetWords(labels.Copy, labels.Copied, labels.CopyRetry);
            AutomationProperties.SetName(Copy, labels.CopyLink);
        }
        if (_accessList is not null)
        {
            AutomationProperties.SetName(_accessList, labels.AccessList);
        }
        if (_channelsHost is not null)
        {
            AutomationProperties.SetName(_channelsHost, labels.Channels);
        }
        if (_recipientsHeading is not null)
        {
            _recipientsHeading.Text = labels.Recipients;
        }
        if (_hint is not null)
        {
            _hint.Text = labels.RecipientsHint;
        }
        if (_peopleHost is not null)
        {
            AutomationProperties.SetName(_peopleHost, labels.People);
        }
        foreach (var button in _channelButtons)
        {
            button.SetFailedWord(labels.ChannelFailed);
        }
        foreach (var chip in _chips.Values)
        {
            NameRemove(chip);
        }
        Send?.SetWords(labels.Send, labels.Sending, labels.SendTo);
        if (_errorText is not null && _error is { IsVisible: true })
        {
            _errorText.Text = labels.SendFailed;
        }
    }

    /// <summary>Rebuilds every row from the owner's lists and the current picks.</summary>
    public void RebuildAll()
    {
        RefreshLink();
        RebuildAccess();
        RebuildChannels();
        RebuildPeople();
        SyncChips(animate: false);
        UpdateSend(animate: false);
    }

    public void RefreshLink()
    {
        var link = _owner.Link;
        if (_linkText is not null)
        {
            _linkText.Text = Display(link);
            Tooltip.SetTip(_linkText, link);
        }
        if (_linkRow is not null)
        {
            _linkRow.IsVisible = !string.IsNullOrEmpty(link);
        }
        UpdateCard();
    }

    /// <summary>The link without its scheme, as the row shows it.</summary>
    internal static string Display(string? link)
    {
        if (string.IsNullOrEmpty(link))
        {
            return string.Empty;
        }
        var at = link.IndexOf("://", StringComparison.Ordinal);
        return at >= 0 ? link[(at + 3)..] : link;
    }

    private void UpdateCard()
    {
        if (_card is not null)
        {
            _card.IsVisible = !string.IsNullOrEmpty(_owner.Link) || _owner.AccessLevels.Count > 0;
        }
        if (AccessRow is not null)
        {
            AccessRow.IsVisible = _owner.AccessLevels.Count > 0;
            AccessRow.Classes.Set("first", string.IsNullOrEmpty(_owner.Link));
        }
    }

    public void RebuildAccess()
    {
        if (_accessList is null || AccessRow is null)
        {
            return;
        }
        foreach (var option in _options)
        {
            option.Click -= OnOptionClick;
            option.KeyDown -= OnOptionKeyDown;
        }
        _options.Clear();
        _accessList.Children.Clear();
        var chosen = _owner.ChosenAccess;
        foreach (var access in _owner.AccessLevels)
        {
            var option = new ShareSheetAccessOption(access) { IsSelected = ReferenceEquals(access, chosen), Focusable = IsAccessListOpen };
            option.IsTabStop = option.IsSelected;
            option.Click += OnOptionClick;
            option.KeyDown += OnOptionKeyDown;
            _options.Add(option);
            _accessList.Children.Add(option);
        }
        AccessRow.Access = chosen;
        UpdateCard();
    }

    /// <summary>Marks the chosen level in the row and the list.</summary>
    public void ShowChosen()
    {
        var chosen = _owner.ChosenAccess;
        if (AccessRow is not null)
        {
            AccessRow.Access = chosen;
        }
        foreach (var option in _options)
        {
            option.IsSelected = ReferenceEquals(option.Access, chosen);
            option.IsTabStop = option.IsSelected;
        }
    }

    public void RebuildChannels()
    {
        if (_channelsHost is null)
        {
            return;
        }
        foreach (var button in _channelButtons)
        {
            button.Click -= OnChannelClick;
        }
        _channelButtons.Clear();
        _channelsHost.Children.Clear();
        _channelsHost.ColumnDefinitions.Clear();
        var channels = _owner.Channels;
        for (var i = 0; i < channels.Count; i++)
        {
            _channelsHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var button = new ShareSheetChannelButton(channels[i]);
            button.SetFailedWord(_owner.Labels.ChannelFailed);
            button.Click += OnChannelClick;
            Grid.SetColumn(button, i);
            _channelButtons.Add(button);
            _channelsHost.Children.Add(button);
        }
        _channelsHost.IsVisible = channels.Count > 0;
        if (_divider is not null)
        {
            _divider.IsVisible = channels.Count > 0 && _owner.People.Count > 0;
        }
    }

    public void RebuildPeople()
    {
        if (_peopleHost is null)
        {
            return;
        }
        foreach (var button in _personButtons)
        {
            button.Click -= OnPersonClick;
        }
        _personButtons.Clear();
        _peopleHost.Children.Clear();
        _peopleHost.ColumnDefinitions.Clear();
        var people = _owner.People;
        for (var i = 0; i < people.Count; i++)
        {
            _peopleHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var button = new ShareSheetPersonButton(people[i]) { IsChecked = _owner.IsPicked(people[i]) };
            button.Click += OnPersonClick;
            Grid.SetColumn(button, i);
            _personButtons.Add(button);
            _peopleHost.Children.Add(button);
        }
        var any = people.Count > 0;
        _peopleHost.IsVisible = any;
        if (_recipients is not null)
        {
            _recipients.IsVisible = any;
        }
        if (_divider is not null)
        {
            _divider.IsVisible = any && _owner.Channels.Count > 0;
        }
    }

    #region Picks and chips

    /// <summary>Brings the chips and the toggles in line with the owner's picks.</summary>
    public void SyncChips(bool animate)
    {
        if (_chipsHost is null)
        {
            return;
        }
        var picked = _owner.SelectedPeople;
        foreach (var person in _chips.Keys.ToList())
        {
            if (!picked.Contains(person))
            {
                RemoveChip(person, animate);
            }
        }
        foreach (var person in picked)
        {
            if (!_chips.ContainsKey(person))
            {
                AddChip(person, animate);
            }
        }
        foreach (var button in _personButtons)
        {
            button.IsChecked = picked.Contains(button.Person);
        }
        UpdateHint(animate);
        UpdateSend(animate);
    }

    private void AddChip(ShareSheetPerson person, bool animate)
    {
        var chip = new ShareSheetChip(person);
        _chips[person] = chip;
        chip.Ready += OnChipReady;
        var full = animate && ArcMotion.IsFull(this);
        var fading = animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        chip.Tag = new ChipIntro(full, fading);
        _chipsHost!.Children.Add(chip);
        if (full)
        {
            Dispatcher.UIThread.Post(() => StartFlight(person, chip), DispatcherPriority.Loaded);
        }
    }

    private void OnChipReady(object? sender, EventArgs e)
    {
        if (sender is not ShareSheetChip chip)
        {
            return;
        }
        NameRemove(chip);
        if (chip.Remove is { } remove)
        {
            remove.Click -= OnRemoveClick;
            remove.Click += OnRemoveClick;
        }
        if (chip.Tag is ChipIntro intro)
        {
            chip.Tag = null;
            if (intro.Flies && chip.Avatar is { } avatar)
            {
                avatar.Opacity = 0;
            }
            if (intro.Fades && chip.Fill is { } fill)
            {
                fill.Opacity = 0;
                var fade = new MotionValue<double>(chip, 0, MotionVectors.Double);
                fade.Changed += v => fill.Opacity = v;
                // The chip's fill arrives once the avatar is well on its way.
                var delay = intro.Flies ? TimeSpan.FromMilliseconds(180) : TimeSpan.Zero;
                _ = fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), s_chipFillEasing, delay));
            }
        }
    }

    private void NameRemove(ShareSheetChip chip)
    {
        if (chip.Remove is { } remove)
        {
            var words = _owner.Labels.Remove(chip.Person.DisplayName);
            AutomationProperties.SetName(remove, words);
            Tooltip.SetTip(remove, words);
        }
    }

    private void RemoveChip(ShareSheetPerson person, bool animate)
    {
        if (_chipsHost is null || !_chips.Remove(person, out var chip))
        {
            return;
        }
        if (_flights.Remove(person, out var flight))
        {
            flight.Cancel();
        }
        var index = _chipsHost.Children.IndexOf(chip);
        var shift = chip.Bounds.Width + _chipsHost.Spacing;
        var bounds = chip.Bounds;
        _chipsHost.Children.Remove(chip);
        if (_reflows.Remove(chip, out var own))
        {
            own.Dispose();
            chip.RenderTransform = null;
        }
        if (!animate || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || _ghosts is null || index < 0)
        {
            return;
        }
        var full = ArcMotion.IsFull(this);
        // The leaving chip goes on a layer of its own at its last place, so the rest close the gap at once.
        if (_chipsHost.TranslatePoint(bounds.Position, _ghosts) is { } at)
        {
            Canvas.SetLeft(chip, at.X);
            Canvas.SetTop(chip, at.Y);
            chip.Width = bounds.Width;
            chip.Height = bounds.Height;
            chip.IsHitTestVisible = false;
            chip.RenderTransformOrigin = RelativePoint.Center;
            var scale = new ScaleTransform();
            chip.RenderTransform = scale;
            _ghosts.Children.Add(chip);
            var exit = new MotionValue<double>(chip, 0, MotionVectors.Double);
            exit.Changed += p =>
            {
                chip.Opacity = 1 - p;
                if (full)
                {
                    scale.ScaleX = scale.ScaleY = 1 - 0.2 * p;
                }
            };
            _ = RemoveGhost(chip, exit.AnimateTo(1, MotionSpec.Tween(ShareSheetMotion.FaceExit, ArcEasings.Standard)));
        }
        if (!full)
        {
            return;
        }
        for (var i = index; i < _chipsHost.Children.Count; i++)
        {
            if (_chipsHost.Children[i] is not Control follower)
            {
                continue;
            }
            // The chips after the gap start where they were and spring into their new places.
            if (!_reflows.TryGetValue(follower, out var reflow))
            {
                var move = new TranslateTransform();
                follower.RenderTransform = move;
                reflow = new MotionValue<double>(follower, 0, MotionVectors.Double);
                reflow.Changed += x => move.X = x;
                _reflows[follower] = reflow;
            }
            reflow.Jump(reflow.Value + shift);
            _ = reflow.AnimateTo(0, MotionSpec.Spring(ShareSheetMotion.Reflow));
        }
    }

    private async Task RemoveGhost(Control chip, Task<bool> exit)
    {
        await exit;
        _ghosts?.Children.Remove(chip);
    }

    private void UpdateHint(bool animate)
    {
        if (_hint is null)
        {
            return;
        }
        var show = _owner.SelectedPeople.Count == 0;
        _hintFade ??= CreateHintFade();
        var target = show ? 1.0 : 0.0;
        if (!animate)
        {
            _hintFade.Jump(target);
            return;
        }
        _ = _hintFade.AnimateTo(target, MotionSpec.Tween(TimeSpan.FromMilliseconds(show ? 160 : 80), ArcEasings.Standard));
    }

    private MotionValue<double> CreateHintFade()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_hint is not null)
            {
                _hint.Opacity = v;
                _hint.IsVisible = v > 0.001;
            }
        };
        return value;
    }

    private void StartFlight(ShareSheetPerson person, ShareSheetChip chip)
    {
        var source = _personButtons.FirstOrDefault(b => ReferenceEquals(b.Person, person))?.Avatar;
        var target = chip.Avatar;
        if (!_chips.TryGetValue(person, out var current) || !ReferenceEquals(current, chip)
            || source is null || target is null || ArcOverlayLayer.Find(this) is not { } layer)
        {
            ShowChipAvatar(chip);
            return;
        }
        // Keep the new chip in view first, so the avatar lands where the chip is.
        chip.BringIntoView();
        var from = source.TranslatePoint(default, layer);
        var to = target.TranslatePoint(default, layer);
        if (from is null || to is null || source.Bounds.Width <= 0 || target.Bounds.Width <= 0)
        {
            ShowChipAvatar(chip);
            return;
        }
        var flight = new Flight(this, layer, person, chip, from.Value, source.Bounds.Width, to.Value, target.Bounds.Width);
        _flights[person] = flight;
        _ = LandAsync(person, chip, flight);
    }

    private async Task LandAsync(ShareSheetPerson person, ShareSheetChip chip, Flight flight)
    {
        var landed = await flight.Run();
        if (_flights.TryGetValue(person, out var current) && ReferenceEquals(current, flight))
        {
            _flights.Remove(person);
        }
        flight.Dispose();
        if (landed || _chips.ContainsKey(person))
        {
            ShowChipAvatar(chip);
        }
    }

    private static void ShowChipAvatar(ShareSheetChip chip)
    {
        if (chip.Avatar is { } avatar)
        {
            avatar.Opacity = 1;
        }
    }

    /// <summary>Lands every flight at once (the panel is going away).</summary>
    public void LandFlights()
    {
        foreach (var flight in _flights.Values.ToList())
        {
            flight.Cancel();
        }
        _flights.Clear();
        foreach (var chip in _chips.Values)
        {
            ShowChipAvatar(chip);
        }
    }

    /// <summary>Nudges the recipients row sideways on a stiff spring (Send with nobody picked).</summary>
    public void Shake()
    {
        if (_recipients is null || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_shake is null)
        {
            _shake = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shake.Changed += x => _shakeShift.X = x;
        }
        _ = _shake.AnimateTo(0, MotionSpec.Spring(ShareSheetMotion.Shake), ShareSheetMotion.ShakeVelocity);
    }

    #endregion

    public void UpdateSend(bool animate)
    {
        if (Send is null)
        {
            return;
        }
        Send.SetState(_owner.SelectedPeople.Count, _owner.IsSending, animate);
    }

    public void SetError(string? message)
    {
        if (_error is null || _errorText is null)
        {
            return;
        }
        _errorText.Text = message;
        _error.IsVisible = !string.IsNullOrEmpty(message);
    }

    #region Access list

    public void SetAccessListOpen(bool open, bool focusList)
    {
        if (AccessRow is null || _accessList is null)
        {
            return;
        }
        AccessRow.IsExpanded = open;
        PseudoClasses.Set(ListOpenPseudoClass, open);
        if (_accessListSlot is not null)
        {
            _accessListSlot.IsExpanded = open;
        }
        // A folded list keeps its options out of the tab order and out of reach.
        _accessList.IsHitTestVisible = open;
        foreach (var option in _options)
        {
            option.Focusable = open;
        }
        if (open && focusList)
        {
            var target = _options.FirstOrDefault(o => o.IsSelected) ?? _options.FirstOrDefault();
            Dispatcher.UIThread.Post(() => target?.Focus(SpecialFocusModality.For(this)), DispatcherPriority.Loaded);
        }
    }

    private void OnAccessRowClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetAccessListOpen(!IsAccessListOpen, focusList: true);
    }

    private void OnOptionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not ShareSheetAccessOption option)
        {
            return;
        }
        _owner.ChooseAccess(option.Access);
        SetAccessListOpen(false, focusList: false);
        AccessRow?.Focus(SpecialFocusModality.For(this));
    }

    private void OnOptionKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ShareSheetAccessOption option || _options.Count == 0)
        {
            return;
        }
        var index = _options.IndexOf(option);
        var next = e.Key switch
        {
            Key.Down or Key.Right => (index + 1) % _options.Count,
            Key.Up or Key.Left => (index - 1 + _options.Count) % _options.Count,
            Key.Home => 0,
            Key.End => _options.Count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        for (var i = 0; i < _options.Count; i++)
        {
            _options[i].IsTabStop = i == next;
        }
        _options[next].Focus(NavigationMethod.Directional);
    }

    #endregion

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.CloseFromPanel();
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = _owner.CopyLinkAsync();
    }

    private void OnChannelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is ShareSheetChannelButton button)
        {
            _ = _owner.RunChannelAsync(button);
        }
    }

    private void OnPersonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is ShareSheetPersonButton button)
        {
            _owner.TogglePerson(button.Person);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Visual remove || remove.FindAncestorOfType<ShareSheetChip>() is not { } chip)
        {
            return;
        }
        var person = chip.Person;
        _owner.Unpick(person);
        // Focus goes back to the person in the row below; the chip it was on is gone.
        _personButtons.FirstOrDefault(b => ReferenceEquals(b.Person, person))?.Focus(SpecialFocusModality.For(this));
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = _owner.SendAsync();
    }

    /// <summary>How a chip makes its entrance once its template is applied.</summary>
    private sealed record ChipIntro(bool Flies, bool Fades);

    /// <summary>
    /// The flight of a picked person's avatar to its chip. It is drawn on the overlay layer, above the panel, so the
    /// chips row's scroll clip cannot cut it, and it scales between the avatar's size and the chip's on the way.
    /// </summary>
    private sealed class Flight : IDisposable
    {
        private readonly Panel _layer;
        private readonly ShareSheetAvatar _avatar;
        private readonly TranslateTransform _move = new();
        private readonly ScaleTransform _scale = new();
        private readonly MotionValue<double> _progress;
        private readonly Point _from;
        private readonly Point _to;
        private readonly double _fromScale;

        public Flight(Visual owner, Panel layer, ShareSheetPerson person, ShareSheetChip chip, Point from, double fromSize, Point to, double toSize)
        {
            _layer = layer;
            _from = from;
            _to = to;
            _fromScale = toSize > 0 ? fromSize / toSize : 1;
            _avatar = new ShareSheetAvatar
            {
                Person = person,
                Width = toSize,
                Height = toSize,
                IsHitTestVisible = false,
                ZIndex = int.MaxValue,
                RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative),
                RenderTransform = new TransformGroup { Children = { _scale, _move } }
            };
            Canvas.SetLeft(_avatar, 0);
            Canvas.SetTop(_avatar, 0);
            layer.Children.Add(_avatar);
            _progress = new MotionValue<double>(owner, 0, MotionVectors.Double);
            _progress.Changed += Apply;
            Apply(0);
        }

        public Task<bool> Run() => _progress.AnimateTo(1, MotionSpec.Spring(ShareSheetMotion.Flight));

        public void Cancel() => _progress.Jump(1);

        private void Apply(double p)
        {
            var scale = _fromScale + (1 - _fromScale) * p;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
            _move.X = _from.X + (_to.X - _from.X) * p;
            _move.Y = _from.Y + (_to.Y - _from.Y) * p;
        }

        public void Dispose()
        {
            _progress.Dispose();
            _layer.Children.Remove(_avatar);
        }
    }
}

/// <summary>A button on the sent confirmation: Undo (quiet) or Done (filled, class <c>primary</c>).</summary>
internal sealed class ShareSheetActionButton : ShareSheetPressable
{
    protected override Type StyleKeyOverride => typeof(ShareSheetActionButton);
}

/// <summary>
/// The sent confirmation: the avatars of the people it went to with a success badge, who it went to, the access level
/// it carries, and Undo / Done.
/// </summary>
[TemplatePart(AvatarsPart, typeof(Canvas))]
[TemplatePart(BadgePart, typeof(Control))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DetailPart, typeof(TextBlock))]
[TemplatePart(UndoPart, typeof(ShareSheetActionButton))]
[TemplatePart(DonePart, typeof(ShareSheetActionButton))]
internal sealed class ShareSheetSentFace : TemplatedControl
{
    internal const string AvatarsPart = "PART_Avatars";
    internal const string BadgePart = "PART_Badge";
    internal const string TitlePart = "PART_Title";
    internal const string DetailPart = "PART_Detail";
    internal const string UndoPart = "PART_Undo";
    internal const string DonePart = "PART_Done";

    /// <summary>At most this many avatars are stacked on the confirmation.</summary>
    internal const int MaxAvatars = 3;

    private readonly ShareSheet _owner;
    private Canvas? _avatars;
    private Control? _badge;
    private TextBlock? _title;
    private TextBlock? _detail;
    private ShareSheetSubmission? _submission;

    public ShareSheetSentFace(ShareSheet owner)
    {
        _owner = owner;
        Focusable = false;
    }

    public ShareSheetActionButton? Undo { get; private set; }

    public ShareSheetActionButton? Done { get; private set; }

    public string? TitleText => _title?.Text;

    public void Show(ShareSheetSubmission submission)
    {
        _submission = submission;
        Refresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (Undo is not null)
        {
            Undo.Click -= OnUndoClick;
        }
        if (Done is not null)
        {
            Done.Click -= OnDoneClick;
        }
        _avatars = e.NameScope.Find<Canvas>(AvatarsPart);
        _badge = e.NameScope.Find<Control>(BadgePart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _detail = e.NameScope.Find<TextBlock>(DetailPart);
        Undo = e.NameScope.Find<ShareSheetActionButton>(UndoPart);
        Done = e.NameScope.Find<ShareSheetActionButton>(DonePart);
        if (Undo is not null)
        {
            Undo.Click += OnUndoClick;
        }
        if (Done is not null)
        {
            Done.Click += OnDoneClick;
        }
        Refresh();
    }

    public void Refresh()
    {
        var labels = _owner.Labels;
        if (Undo is not null)
        {
            Undo.Content = labels.Undo;
        }
        if (Done is not null)
        {
            Done.Content = labels.Done;
        }
        if (_submission is not { } submission)
        {
            return;
        }
        if (_title is not null)
        {
            _title.Text = labels.SentTitle(submission.People);
        }
        if (_detail is not null)
        {
            _detail.Text = labels.SentDetail(submission.Access);
            _detail.IsVisible = !string.IsNullOrEmpty(_detail.Text);
        }
        AutomationProperties.SetName(this, _title?.Text ?? string.Empty);
        BuildAvatars(submission.People);
    }

    private void BuildAvatars(IReadOnlyList<ShareSheetPerson> people)
    {
        if (_avatars is null)
        {
            return;
        }
        _avatars.Children.Clear();
        var size = Resource(ShareSheetTokenKind.SentAvatarSize, 44);
        var ring = Resource(ShareSheetTokenKind.SentRing, 3);
        var overlap = Resource(ShareSheetTokenKind.SentAvatarOverlap, 12);
        var count = Math.Min(people.Count, MaxAvatars);
        var step = size - overlap;
        var width = count == 0 ? size : size + step * (count - 1);
        _avatars.Width = width;
        _avatars.Height = size;
        for (var i = count - 1; i >= 0; i--)
        {
            // A ring in the panel's color separates overlapping avatars.
            var frame = new Border
            {
                Width = size + 2 * ring,
                Height = size + 2 * ring,
                Padding = new Thickness(ring),
                Child = new ShareSheetAvatar { Person = people[i] }
            }.BindCornerRadius(TKCode.Arc.DesignTokens.ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(frame, Border.BackgroundProperty, TKCode.Arc.DesignTokens.ArcDesignTokenKind.SurfaceRaised);
            Canvas.SetLeft(frame, i * step - ring);
            Canvas.SetTop(frame, -ring);
            _avatars.Children.Add(frame);
        }
        if (_badge is not null)
        {
            _badge.IsVisible = count > 0;
        }
    }

    private double Resource(ShareSheetTokenKind kind, double fallback) =>
        this.TryFindResource(ShareSheetTokens.Key(kind), out var value) && value is double d ? d : fallback;

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.UndoSend();
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.FinishSent();
    }
}
