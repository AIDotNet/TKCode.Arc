using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class SeatManager
{
    /// <summary>A crossfade slot (old out 100 ms, new in 160 ms rising 2px) whose content is rebuilt only when its kind changes.</summary>
    private sealed class Slot
    {
        public ArcContentSwap? Swap;
        public string? Key;
        public Control? Content;
    }

    private readonly Slot _noticeSlot = new();
    private readonly Slot _dueNoteSlot = new();
    private readonly Slot _fromNoteSlot = new();
    private readonly Slot _legendDeltaSlot = new();
    private readonly Slot _footerTextSlot = new();
    private readonly Slot _footerActionsSlot = new();
    private readonly List<SeatManagerCellKind> _kinds = new();
    private readonly Dictionary<SeatManagerInvite, SeatManagerInviteRow> _rows = new();

    private Border? _card;
    private TextBlock? _title;
    private TextBlock? _planName;
    private Border? _badge;
    private TextBlock? _subtitle;
    private SeatManagerAvatars? _avatars;
    private SeatManagerRollText? _used;
    private SeatManagerMeter? _meter;
    private SeatManagerRollText? _legendMembers;
    private SeatManagerRollText? _legendInvited;
    private SeatManagerRollText? _legendOpen;
    private TextBlock? _stepperTitle;
    private SeatManagerRollText? _purchasedText;
    private SeatManagerStepButton? _minus;
    private SeatManagerSpinValue? _spin;
    private SeatManagerStepButton? _plus;
    private TextBlock? _dueLabel;
    private SeatManagerRoll? _due;
    private TextBlock? _fromLabel;
    private SeatManagerRoll? _next;
    private TextBlock? _invitesTitle;
    private SeatManagerRoll? _inviteCount;
    private StackPanel? _inviteList;
    private Control? _invitesEmpty;
    private TextBlock? _invitesEmptyText;
    private ArcContentSwap? _footerActions;
    private SeatManagerAnnouncer? _announcer;

    private readonly Button _update = new() { Variant = ButtonVariant.Primary, Size = ControlSize.Small };
    private readonly Button _back = new() { Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
    private readonly Button _pay = new() { Variant = ButtonVariant.Primary, Size = ControlSize.Small };
    private StackPanel? _idleActions;
    private StackPanel? _confirmActions;
    private bool _isNarrow;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_minus is not null)
        {
            _minus.Stepped -= Decrement;
        }
        if (_plus is not null)
        {
            _plus.Stepped -= Increment;
        }
        if (_spin is not null)
        {
            _spin.Requested -= OnSpinRequested;
            _spin.ValueRequested -= JumpTo;
        }
        _inviteList?.Children.Clear();
        _idleActions?.Children.Clear();
        _confirmActions?.Children.Clear();
        base.OnApplyTemplate(e);

        var scope = e.NameScope;
        _card = scope.Find<Border>(CardPart);
        _title = scope.Find<TextBlock>(TitlePart);
        _planName = scope.Find<TextBlock>(PlanNamePart);
        _badge = scope.Find<Border>(BadgePart);
        _subtitle = scope.Find<TextBlock>(SubtitlePart);
        _avatars = scope.Find<SeatManagerAvatars>(AvatarsPart);
        _used = scope.Find<SeatManagerRollText>(UsedPart);
        _meter = scope.Find<SeatManagerMeter>(MeterPart);
        _legendMembers = scope.Find<SeatManagerRollText>(LegendMembersPart);
        _legendInvited = scope.Find<SeatManagerRollText>(LegendInvitedPart);
        _legendOpen = scope.Find<SeatManagerRollText>(LegendOpenPart);
        _legendDeltaSlot.Swap = scope.Find<ArcContentSwap>(LegendDeltaPart);
        _noticeSlot.Swap = scope.Find<ArcContentSwap>(NoticePart);
        _stepperTitle = scope.Find<TextBlock>(StepperTitlePart);
        _purchasedText = scope.Find<SeatManagerRollText>(PurchasedPart);
        _minus = scope.Find<SeatManagerStepButton>(MinusPart);
        _spin = scope.Find<SeatManagerSpinValue>(SpinPart);
        _plus = scope.Find<SeatManagerStepButton>(PlusPart);
        _dueLabel = scope.Find<TextBlock>(DueLabelPart);
        _dueNoteSlot.Swap = scope.Find<ArcContentSwap>(DueNotePart);
        _due = scope.Find<SeatManagerRoll>(DuePart);
        _fromLabel = scope.Find<TextBlock>(FromLabelPart);
        _fromNoteSlot.Swap = scope.Find<ArcContentSwap>(FromNotePart);
        _next = scope.Find<SeatManagerRoll>(NextPart);
        _invitesTitle = scope.Find<TextBlock>(InvitesTitlePart);
        _inviteCount = scope.Find<SeatManagerRoll>(InviteCountPart);
        _inviteList = scope.Find<StackPanel>(InviteListPart);
        _invitesEmpty = scope.Find<Control>(InvitesEmptyPart);
        _invitesEmptyText = scope.Find<TextBlock>(InvitesEmptyTextPart);
        _footerTextSlot.Swap = scope.Find<ArcContentSwap>(FooterTextPart);
        _footerActions = scope.Find<ArcContentSwap>(FooterActionsPart);
        _footerActionsSlot.Swap = _footerActions;
        _announcer = scope.Find<SeatManagerAnnouncer>(AnnouncerPart);
        foreach (var (name, kind) in new[] { (DotMembersPart, SeatManagerCellKind.Member), (DotInvitedPart, SeatManagerCellKind.Invited),
                     (DotOpenPart, SeatManagerCellKind.Open) })
        {
            if (scope.Find<SeatManagerLegendDot>(name) is { } dot)
            {
                dot.Kind = kind;
                dot.Meter = _meter;
            }
        }

        if (_minus is not null)
        {
            _minus.Glyph = ArcIcons.Minus;
            _minus.Delay = (t, c) => Delay(t, c);
            _minus.Stepped += Decrement;
        }
        if (_plus is not null)
        {
            _plus.Glyph = ArcIcons.Plus;
            _plus.Delay = (t, c) => Delay(t, c);
            _plus.Stepped += Increment;
        }
        if (_spin is not null)
        {
            _spin.Requested += OnSpinRequested;
            _spin.ValueRequested += JumpTo;
        }
        _idleActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _update } };
        _confirmActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _back, _pay } };
        ResetSlots();
        foreach (var row in _rows.Values)
        {
            _inviteList?.Children.Add(row);
        }
        SyncRows();
        Refresh();
    }

    private bool _buttonsWired;

    private void WireButtons()
    {
        if (_buttonsWired)
        {
            return;
        }
        _buttonsWired = true;
        _update.Click += (_, _) => Review();
        _back.Click += (_, _) => Back();
        _pay.Click += (_, _) => OnPayClicked();
        _pay.LabelSwapMode = ContentSwapMode.Label;
        _update.LabelSwapMode = ContentSwapMode.Label;
    }

    private void ResetSlots()
    {
        WireButtons();
        foreach (var slot in new[] { _noticeSlot, _dueNoteSlot, _fromNoteSlot, _legendDeltaSlot, _footerTextSlot, _footerActionsSlot })
        {
            slot.Key = null;
            slot.Content = null;
            if (slot.Swap is not null && slot != _footerActionsSlot)
            {
                slot.Swap.Content = null;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width comes from the parent, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            var narrow = availableSize.Width < NarrowWidth;
            if (narrow != _isNarrow)
            {
                _isNarrow = narrow;
                PseudoClasses.Set(NarrowPseudoClass, narrow);
            }
        }
        return base.MeasureOverride(availableSize);
    }

    #endregion

    #region View

    private string FormatMoney(decimal amount) => SettingsFormat.Money(amount, PlanOrEmpty.Currency, Labels.Culture);

    private string FormatDate(DateTime date) => SettingsFormat.ShortDate(date, Labels.Culture);

    private string? CardText()
    {
        var method = PaymentMethod;
        return method is null ? null : Labels.Card(method.Brand, method.Last4);
    }

    private string Count(int n) => n.ToString("N0", Labels.Culture);

    /// <summary>Writes the current state into every part.</summary>
    private void UpdateView()
    {
        if (_card is null)
        {
            return;
        }
        var labels = Labels;
        var plan = PlanOrEmpty;
        var quote = Quote;
        var members = MemberTotal;
        var invites = ActiveInvites;
        var used = members + invites;
        var purchased = Purchased;
        var open = SeatManagerMath.Open(purchased, used);
        var delta = quote.Delta;

        PseudoClasses.Set(AddingPseudoClass, delta > 0);
        PseudoClasses.Set(RemovingPseudoClass, delta < 0);
        PseudoClasses.Set(FullPseudoClass, delta == 0 && open == 0);

        // Header.
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_planName is not null)
        {
            _planName.Text = plan.Name;
        }
        if (_badge is not null)
        {
            _badge.IsVisible = !string.IsNullOrWhiteSpace(plan.Name);
        }
        if (_subtitle is not null)
        {
            _subtitle.Text = labels.Subtitle(Workspace ?? string.Empty,
                SettingsFormat.MoneyShort(plan.PricePerSeat, plan.Currency, labels.Culture), plan.Interval, FormatDate(quote.RenewsOn));
        }

        // Overview.
        _avatars?.Show(_members.Where(m => m is not null).ToList(), members, labels.MembersGroup(members));
        _used?.Show(labels.UsedTemplate, Count(used), Count(purchased));
        SeatManagerMath.FillCells(_kinds, members, invites, purchased, Target);
        if (_meter is not null)
        {
            _meter.Show(_kinds);
            AutomationProperties.SetName(_meter, labels.MeterName(used, purchased, members, invites, open,
                Math.Max(0, delta), Math.Max(0, -delta)));
        }
        _legendMembers?.Show(labels.LegendMembers(members), Count(members));
        _legendInvited?.Show(labels.LegendInvited(invites), Count(invites));
        _legendOpen?.Show(labels.LegendOpen(open), Count(open));
        UpdateLegendDelta(delta);
        UpdateNotice(delta, open);

        // Quote.
        if (_stepperTitle is not null)
        {
            _stepperTitle.Text = labels.StepperTitle;
        }
        _purchasedText?.Show(labels.PurchasedTemplate, Count(purchased));
        var locked = IsLocked;
        if (_minus is not null)
        {
            _minus.IsLocked = locked;
            _minus.IsSoftDisabled = !locked && Target <= Minimum;
            AutomationProperties.SetName(_minus, labels.RemoveSeat);
        }
        if (_plus is not null)
        {
            _plus.IsLocked = locked;
            _plus.IsSoftDisabled = !locked && Target >= Maximum;
            AutomationProperties.SetName(_plus, labels.AddSeatButton);
        }
        if (_spin is not null)
        {
            _spin.Text = Count(Target);
            _spin.Range = (Minimum, Maximum, Target);
            _spin.IsTabStop = !locked;
            AutomationProperties.SetName(_spin, labels.SpinName);
            AutomationProperties.SetHelpText(_spin, labels.SpinValueText(Target));
        }
        if (_dueLabel is not null)
        {
            _dueLabel.Text = labels.DueToday;
        }
        if (_fromLabel is not null)
        {
            _fromLabel.Text = labels.From(FormatDate(quote.RenewsOn));
        }
        if (_due is not null)
        {
            _due.Text = FormatMoney(quote.DueToday);
        }
        if (_next is not null)
        {
            _next.Text = FormatMoney(quote.NextTotal) + labels.PerInterval(plan.Interval);
        }
        UpdateNotes(quote);

        // Invites.
        if (_invitesTitle is not null)
        {
            _invitesTitle.Text = labels.InvitesTitle;
        }
        if (_inviteCount is not null)
        {
            _inviteCount.Text = Count(invites);
        }
        if (_invitesEmptyText is not null)
        {
            _invitesEmptyText.Text = labels.InvitesEmpty;
        }
        if (_invitesEmpty is not null)
        {
            _invitesEmpty.IsVisible = _rows.Count == 0;
        }

        // Footer.
        UpdateFooter(quote);
    }

    private static void Show(Slot slot, string key, Func<Control> build, Action<Control>? update = null)
    {
        if (slot.Swap is null)
        {
            return;
        }
        if (slot.Key == key && slot.Content is not null)
        {
            update?.Invoke(slot.Content);
            return;
        }
        var content = build();
        update?.Invoke(content);
        slot.Key = key;
        slot.Content = content;
        slot.Swap.Content = content;
    }

    private static void Clear(Slot slot)
    {
        if (slot.Swap is null || slot.Key is null)
        {
            return;
        }
        slot.Key = null;
        slot.Content = null;
        slot.Swap.Content = null;
    }

    private static TextBlock Copy(double size, ArcDesignTokenKind brush, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock
        {
            FontSize = size,
            FontWeight = weight,
            LineHeight = 20,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, brush);
        return block;
    }

    private ArcGlyph Glyph(Geometry data, ArcDesignTokenKind? token, AvaloniaProperty? brushProperty = null)
    {
        var glyph = new ArcGlyph { Data = data, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        if (token is { } kind)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, kind);
        }
        else if (brushProperty is StyledProperty<IBrush?> property)
        {
            glyph.Bind(ArcGlyph.ForegroundProperty, this.GetObservable(property));
        }
        return glyph;
    }

    private void UpdateLegendDelta(int delta)
    {
        if (delta == 0)
        {
            Clear(_legendDeltaSlot);
            return;
        }
        var kind = delta > 0 ? SeatManagerCellKind.Adding : SeatManagerCellKind.Removing;
        var labels = Labels;
        var template = delta > 0 ? labels.LegendNew(delta) : labels.LegendRemoved(-delta);
        Show(_legendDeltaSlot, kind.ToString(), () =>
        {
            var text = new SeatManagerRollText();
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            TextElement.SetFontSize(text, 13);
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { new SeatManagerLegendDot { Kind = kind, Meter = _meter }, text }
            };
        }, c => ((SeatManagerRollText)((StackPanel)c).Children[1]).Show(template, Count(Math.Abs(delta))));
    }

    private void UpdateNotice(int delta, int open)
    {
        var labels = Labels;
        SeatManagerNoticeKind kind;
        string text;
        if (_limitNotice is not null)
        {
            kind = SeatManagerNoticeKind.Info;
            text = _limitNotice;
        }
        else
        {
            kind = SeatManagerMath.PickNotice(delta, open);
            text = kind switch
            {
                SeatManagerNoticeKind.Adding => labels.NoticeAdding(delta),
                SeatManagerNoticeKind.Removing => labels.NoticeRemoving(-delta, FormatDate(Quote.RenewsOn)),
                SeatManagerNoticeKind.Open => labels.NoticeOpen(open),
                _ => labels.NoticeFull
            };
        }
        Show(_noticeSlot, kind.ToString(), () => BuildNotice(kind), c =>
        {
            var grid = (Grid)c;
            ((TextBlock)grid.Children[1]).Text = text;
            if (grid.Children.Count > 2)
            {
                var action = (SeatManagerTextButton)grid.Children[2];
                action.Content = labels.AddSeat;
                AutomationProperties.SetName(action, labels.AddSeat);
            }
        });
    }

    private Control BuildNotice(SeatManagerNoticeKind kind)
    {
        var glyph = kind switch
        {
            SeatManagerNoticeKind.Adding => Glyph(ArcIcons.CirclePlus, ArcDesignTokenKind.Accent),
            SeatManagerNoticeKind.Removing => Glyph(ArcIcons.CircleMinus, ArcDesignTokenKind.TextMuted),
            SeatManagerNoticeKind.Open => Glyph(ArcIcons.CircleCheck, null, SuccessBrushProperty),
            SeatManagerNoticeKind.Full => Glyph(ArcIcons.TriangleAlert, null, WarningBrushProperty),
            _ => Glyph(ArcIcons.Info, ArcDesignTokenKind.TextMuted)
        };
        var text = Copy(13, kind == SeatManagerNoticeKind.Full ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
        text.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(text, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { glyph, text } };
        if (kind == SeatManagerNoticeKind.Full)
        {
            var action = new SeatManagerTextButton { Margin = new Thickness(8, 0, 0, 0) };
            action.Click += (_, _) => OnAddSeatClicked();
            Grid.SetColumn(action, 2);
            grid.Children.Add(action);
        }
        return grid;
    }

    private void UpdateNotes(SeatManagerQuote quote)
    {
        var labels = Labels;
        string dueKey;
        string dueText;
        if (quote.Delta > 0)
        {
            dueKey = "prorated";
            dueText = labels.NoteProrated(quote.DaysLeft);
        }
        else if (quote.Delta < 0)
        {
            dueKey = "removals";
            dueText = labels.NoteRemovals(FormatDate(quote.RenewsOn));
        }
        else
        {
            dueKey = "none";
            dueText = labels.NoteNoChange;
        }
        Show(_dueNoteSlot, dueKey, () => Copy(13, ArcDesignTokenKind.TextMuted), c => ((TextBlock)c).Text = dueText);
        var was = quote.Delta == 0
            ? labels.NoteCurrent
            : labels.NoteWas(FormatMoney(quote.Current * PlanOrEmpty.PricePerSeat));
        Show(_fromNoteSlot, quote.Delta == 0 ? "current" : "was", () => Copy(13, ArcDesignTokenKind.TextMuted), c => ((TextBlock)c).Text = was);
    }

    private void UpdateFooter(SeatManagerQuote quote)
    {
        var labels = Labels;
        var phase = Phase;
        var card = CardText();
        switch (phase)
        {
            case SeatManagerPhase.Confirm:
            {
                var text = quote.Delta > 0
                    ? labels.ConfirmAdd(FormatMoney(quote.DueToday), card)
                    : labels.ConfirmRemove(-quote.Delta, FormatDate(quote.RenewsOn));
                var emphasis = quote.Delta > 0 ? FormatMoney(quote.DueToday) : null;
                Show(_footerTextSlot, "confirm", () => Copy(14, ArcDesignTokenKind.TextSecondary), c => Emphasize((TextBlock)c, text, emphasis));
                break;
            }
            case SeatManagerPhase.Saving:
            {
                var text = quote.Delta > 0 ? labels.Charging(card) : labels.Updating;
                Show(_footerTextSlot, "saving", () =>
                {
                    var shimmer = new TextShimmer { FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                    ArcTokens.Bind(shimmer, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                    return shimmer;
                }, c => ((TextShimmer)c).Text = text);
                break;
            }
            case SeatManagerPhase.Done:
            {
                var text = labels.Done(Purchased, ReceiptEmail);
                Show(_footerTextSlot, "done", () =>
                {
                    var t = Copy(14, ArcDesignTokenKind.TextSecondary);
                    t.Margin = new Thickness(8, 0, 0, 0);
                    Grid.SetColumn(t, 1);
                    return new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                        Children = { Glyph(ArcIcons.CircleCheck, null, SuccessBrushProperty), t }
                    };
                }, c => ((TextBlock)((Grid)c).Children[1]).Text = text);
                break;
            }
            case SeatManagerPhase.Error:
            {
                var text = ErrorMessage ?? labels.ErrorFallback;
                Show(_footerTextSlot, "error", () =>
                {
                    var t = Copy(14, ArcDesignTokenKind.Foreground);
                    t.Margin = new Thickness(8, 0, 0, 0);
                    Grid.SetColumn(t, 1);
                    return new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                        Children = { Glyph(ArcIcons.CircleAlert, ArcDesignTokenKind.Danger), t }
                    };
                }, c => ((TextBlock)((Grid)c).Children[1]).Text = text);
                break;
            }
            default:
                if (quote.Delta != 0)
                {
                    var bold = labels.DeltaSeats(quote.Delta);
                    var tail = quote.Delta > 0
                        ? labels.DeltaAddTail(FormatMoney(quote.DueToday))
                        : labels.DeltaRemoveTail(FormatDate(quote.RenewsOn));
                    Show(_footerTextSlot, "delta", () => Copy(14, ArcDesignTokenKind.TextSecondary),
                        c => Emphasize((TextBlock)c, bold + tail, bold));
                }
                else if (PaymentMethod is { } method)
                {
                    var spoken = method.ProcessorName is { Length: > 0 } processor
                        ? $"{labels.BilledVia(processor)} {card}"
                        : card;
                    Show(_footerTextSlot, "card", () =>
                    {
                        var mark = new SeatManagerProcessorMark { VerticalAlignment = VerticalAlignment.Center };
                        ArcTokens.Bind(mark, SeatManagerProcessorMark.FillProperty, ArcDesignTokenKind.Accent);
                        ArcTokens.Bind(mark, SeatManagerProcessorMark.InkProperty, ArcDesignTokenKind.AccentForeground);
                        var t = Copy(14, ArcDesignTokenKind.TextSecondary);
                        t.Margin = new Thickness(8, 0, 0, 0);
                        Grid.SetColumn(t, 1);
                        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { mark, t } };
                    }, c =>
                    {
                        var grid = (Grid)c;
                        ((SeatManagerProcessorMark)grid.Children[0]).Image = method.Mark;
                        ((TextBlock)grid.Children[1]).Text = card;
                        AutomationProperties.SetName(grid, spoken);
                    });
                }
                else
                {
                    Clear(_footerTextSlot);
                }
                break;
        }

        // Actions: Update seats at rest; Back + Pay (Confirm, Try again) while a change is confirmed.
        _update.Content = labels.UpdateSeats;
        _update.IsEnabled = quote.Delta != 0;
        _back.Content = labels.Back;
        _back.IsEnabled = phase != SeatManagerPhase.Saving;
        _pay.IsLoading = phase == SeatManagerPhase.Saving;
        _pay.Content = phase == SeatManagerPhase.Error
            ? labels.TryAgain
            : quote.Delta > 0 ? labels.Pay(FormatMoney(quote.DueToday)) : labels.Confirm;
        if (_footerActionsSlot.Swap is not null)
        {
            var confirming = phase is SeatManagerPhase.Confirm or SeatManagerPhase.Saving or SeatManagerPhase.Error;
            var panel = confirming ? _confirmActions : _idleActions;
            if (!ReferenceEquals(_footerActionsSlot.Swap.Content, panel))
            {
                _footerActionsSlot.Swap.Content = panel;
            }
        }
    }

    /// <summary>Sets <paramref name="text"/> with <paramref name="emphasis"/> (if found) in medium Foreground.</summary>
    private static void Emphasize(TextBlock block, string text, string? emphasis)
    {
        block.Inlines ??= new InlineCollection();
        block.Inlines.Clear();
        var at = string.IsNullOrEmpty(emphasis) ? -1 : text.IndexOf(emphasis, StringComparison.Ordinal);
        if (at < 0)
        {
            block.Inlines.Add(new Run(text));
            return;
        }
        if (at > 0)
        {
            block.Inlines.Add(new Run(text[..at]));
        }
        var strong = new Run(emphasis) { FontWeight = FontWeight.Medium };
        ArcTokens.Bind(strong, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        block.Inlines.Add(strong);
        var end = at + emphasis!.Length;
        if (end < text.Length)
        {
            block.Inlines.Add(new Run(text[end..]));
        }
    }

    #endregion

    #region Invite rows

    private SeatManagerInviteRow? RowOf(SeatManagerInvite invite) => _rows.TryGetValue(invite, out var row) ? row : null;

    /// <summary>Follows <see cref="Invites"/>: keeps the rows of invites that stay, builds (and opens) rows for new ones.</summary>
    private void SyncRows()
    {
        var wanted = _invites.Where(i => i is not null).Distinct().ToList();
        foreach (var gone in _rows.Keys.Where(i => !wanted.Contains(i)).ToList())
        {
            var row = _rows[gone];
            _rows.Remove(gone);
            _inviteList?.Children.Remove(row);
        }
        var animateNew = _inviteList is not null && IsLoaded;
        for (var index = 0; index < wanted.Count; index++)
        {
            var invite = wanted[index];
            if (!_rows.TryGetValue(invite, out var row))
            {
                row = new SeatManagerInviteRow(this, invite);
                row.ResendClicked += r => Resend(r.Invite);
                row.RevokeClicked += r => Revoke(r.Invite);
                row.UndoClicked += r => Undo(r.Invite);
                _rows[invite] = row;
                if (animateNew)
                {
                    row.Enter();
                }
            }
            row.ResendState = ResendStateOf(invite);
            row.IsRevoked = _pending.ContainsKey(invite);
            row.HasDivider = index > 0;
            if (_inviteList is not null)
            {
                var at = _inviteList.Children.IndexOf(row);
                if (at != index)
                {
                    if (at >= 0)
                    {
                        _inviteList.Children.RemoveAt(at);
                    }
                    _inviteList.Children.Insert(Math.Min(index, _inviteList.Children.Count), row);
                }
            }
        }
    }

    /// <summary>The rows shown, in order (tests).</summary>
    internal IReadOnlyList<SeatManagerInviteRow> Rows =>
        _inviteList?.Children.OfType<SeatManagerInviteRow>().ToList() ?? (IReadOnlyList<SeatManagerInviteRow>)_rows.Values.ToList();

    internal SeatManagerAnnouncer? Announcer => _announcer;
    internal Button UpdateButton => _update;
    internal Button BackButton => _back;
    internal Button PayButton => _pay;
    internal SeatManagerSpinValue? SpinPartControl => _spin;
    internal SeatManagerStepButton? MinusPartControl => _minus;
    internal SeatManagerStepButton? PlusPartControl => _plus;
    internal SeatManagerMeter? MeterPartControl => _meter;
    internal Control? FooterTextContent => _footerTextSlot.Content;
    internal Control? NoticeContent => _noticeSlot.Content;
    internal string? NoticeKey => _noticeSlot.Key;
    internal SeatManagerRoll? DueRoll => _due;
    internal SeatManagerRoll? NextRoll => _next;

    #endregion
}
