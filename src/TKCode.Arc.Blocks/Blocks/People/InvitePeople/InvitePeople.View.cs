using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class InvitePeople
{
    /// <summary>How long a paste summary replaces the hint (3.2 s).</summary>
    internal static readonly TimeSpan SummaryDuration = TimeSpan.FromMilliseconds(3200);

    private Border? _card;
    private InvitePeopleHeaderPanel? _header;
    private TextBlock? _title;
    private TextBlock? _description;
    private InvitePeopleTeamStack? _team;
    private Panel? _fieldHost;
    private InvitePeopleChipPanel? _chips;
    private InvitePeopleInput? _input;
    private ArcContentSwap? _hint;
    private InvitePeopleButton? _pasteSample;
    private SpringSizeDecorator? _errorsHost;
    private Border? _errors;
    private TextBlock? _errorsTitle;
    private InvitePeopleButton? _removeInvalid;
    private StackPanel? _errorLines;
    private TextBlock? _errorsHelp;
    private RollingDigits? _seatTotal;
    private TextBlock? _seatOf;
    private RollingDigits? _seatLimit;
    private TextBlock? _seatWord;
    private ArcContentSwap? _plan;
    private InvitePeopleSeatBar? _meter;
    private TextBlock? _legendMembers;
    private TextBlock? _legendPending;
    private TextBlock? _legendNew;
    private SpringSizeDecorator? _overHost;
    private Grid? _over;
    private RollingDigits? _overCount;
    private TextBlock? _overText;
    private InvitePeopleButton? _upgrade;
    private TextBlock? _removeToSend;
    private InvitePeopleButton? _reset;
    private InvitePeopleButton? _send;
    private RollingDigits? _sendCount;
    private TextBlock? _sendVerb;
    private ArcContentSwap? _sendNoun;
    private TextBlock? _pendingHeading;
    private RollingDigits? _pendingCount;
    private StackPanel? _pendingRows;
    private SpringSizeDecorator? _pendingEmptyHost;
    private TextBlock? _pendingEmpty;
    private InvitePeopleFlightLayer? _flights;
    private ProjectsAnnouncer? _announcer;
    private DispatcherTimer? _summaryTimer;
    private bool _isNarrow;

    /// <summary>The live region (tests read its history).</summary>
    internal ProjectsAnnouncer? Announcer => _announcer;

    /// <summary>The flight layer (tests read it).</summary>
    internal InvitePeopleFlightLayer? Flights => _flights;

    /// <summary>The text box of the field.</summary>
    internal InvitePeopleInput? Input => _input;

    /// <summary>The chip panel.</summary>
    internal InvitePeopleChipPanel? ChipPanel => _chips;

    /// <summary>The send button.</summary>
    internal InvitePeopleButton? SendButton => _send;

    /// <summary>The upgrade button.</summary>
    internal InvitePeopleButton? UpgradeButton => _upgrade;

    /// <summary>The seat meter.</summary>
    internal InvitePeopleSeatBar? Meter => _meter;

    /// <summary>The over-limit line (it shakes when Send is refused for seats).</summary>
    internal Control? OverLine => _over;

    /// <summary>The team avatars.</summary>
    internal InvitePeopleTeamStack? Team => _team;

    /// <summary>The header panel.</summary>
    internal InvitePeopleHeaderPanel? Header => _header;

    /// <summary>The hint under the field (its current text).</summary>
    internal string? HintText => (_hint?.Content as TextBlock)?.Text;

    /// <summary>The pending heading (focus goes there after a revoke).</summary>
    internal TextBlock? PendingHeading => _pendingHeading;

    /// <summary>True while the error panel is open.</summary>
    internal bool AreErrorsShown => _errorsHost?.IsExpanded == true;

    /// <summary>True while the over-limit line is open.</summary>
    internal bool IsOverLineShown => _overHost?.IsExpanded == true;

    /// <summary>The narrow layout (status under the name, no titles in suggestions).</summary>
    internal bool IsNarrow => _isNarrow;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        var scope = e.NameScope;
        _card = scope.Find<Border>(CardPart);
        _header = scope.Find<InvitePeopleHeaderPanel>("PART_Header");
        _title = scope.Find<TextBlock>("PART_Title");
        _description = scope.Find<TextBlock>("PART_Description");
        _team = scope.Find<InvitePeopleTeamStack>("PART_Team");
        _fieldHost = scope.Find<Panel>("PART_FieldHost");
        _chips = scope.Find<InvitePeopleChipPanel>("PART_Chips");
        _hint = scope.Find<ArcContentSwap>("PART_Hint");
        _pasteSample = scope.Find<InvitePeopleButton>("PART_PasteSample");
        _errorsHost = scope.Find<SpringSizeDecorator>("PART_ErrorsHost");
        _errors = scope.Find<Border>("PART_Errors");
        _errorsTitle = scope.Find<TextBlock>("PART_ErrorsTitle");
        _removeInvalid = scope.Find<InvitePeopleButton>("PART_RemoveInvalid");
        _errorLines = scope.Find<StackPanel>("PART_ErrorLines");
        _errorsHelp = scope.Find<TextBlock>("PART_ErrorsHelp");
        _seatTotal = scope.Find<RollingDigits>("PART_SeatTotal");
        _seatOf = scope.Find<TextBlock>("PART_SeatOf");
        _seatLimit = scope.Find<RollingDigits>("PART_SeatLimit");
        _seatWord = scope.Find<TextBlock>("PART_SeatWord");
        _plan = scope.Find<ArcContentSwap>("PART_Plan");
        _meter = scope.Find<InvitePeopleSeatBar>("PART_Meter");
        _legendMembers = scope.Find<TextBlock>("PART_LegendMembers");
        _legendPending = scope.Find<TextBlock>("PART_LegendPending");
        _legendNew = scope.Find<TextBlock>("PART_LegendNew");
        _overHost = scope.Find<SpringSizeDecorator>("PART_OverHost");
        _over = scope.Find<Grid>("PART_Over");
        _overCount = scope.Find<RollingDigits>("PART_OverCount");
        _overText = scope.Find<TextBlock>("PART_OverText");
        _upgrade = scope.Find<InvitePeopleButton>("PART_Upgrade");
        _removeToSend = scope.Find<TextBlock>("PART_RemoveToSend");
        _reset = scope.Find<InvitePeopleButton>("PART_Reset");
        _send = scope.Find<InvitePeopleButton>("PART_Send");
        _pendingHeading = scope.Find<TextBlock>("PART_PendingHeading");
        _pendingCount = scope.Find<RollingDigits>("PART_PendingCount");
        _pendingRows = scope.Find<StackPanel>("PART_PendingRows");
        _pendingEmptyHost = scope.Find<SpringSizeDecorator>("PART_PendingEmptyHost");
        _pendingEmpty = scope.Find<TextBlock>("PART_PendingEmpty");
        _flights = scope.Find<InvitePeopleFlightLayer>("PART_Flights");
        _announcer = scope.Find<ProjectsAnnouncer>("PART_Announcer");

        BuildSendContent();
        BuildPasteContent();
        BuildField();
        Wire();
        _pendingRowMap.Clear();
        _pendingRows?.Children.Clear();
        SyncPendingRows(animate: false);
        SyncTeam(animate: false);
        RebuildChips();
        UpdateTexts();
        UpdateSeats();
        UpdateErrors();
        UpdateBreakpoints();
    }

    private void Wire()
    {
        if (_pasteSample is not null)
        {
            _pasteSample.Click += OnPasteSampleClick;
        }
        if (_removeInvalid is not null)
        {
            _removeInvalid.Click += OnRemoveInvalidClick;
        }
        if (_upgrade is not null)
        {
            _upgrade.Click += OnUpgradeClick;
        }
        if (_reset is not null)
        {
            _reset.Click += OnResetClick;
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        if (_fieldHost is not null)
        {
            _fieldHost.PointerPressed += OnFieldHostPressed;
            _fieldHost.GotFocus += OnFieldFocusChanged;
            _fieldHost.LostFocus += OnFieldFocusChanged;
        }
    }

    private void Unwire()
    {
        if (_pasteSample is not null)
        {
            _pasteSample.Click -= OnPasteSampleClick;
        }
        if (_removeInvalid is not null)
        {
            _removeInvalid.Click -= OnRemoveInvalidClick;
        }
        if (_upgrade is not null)
        {
            _upgrade.Click -= OnUpgradeClick;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        if (_fieldHost is not null)
        {
            _fieldHost.PointerPressed -= OnFieldHostPressed;
            _fieldHost.GotFocus -= OnFieldFocusChanged;
            _fieldHost.LostFocus -= OnFieldFocusChanged;
        }
        UnwireInput();
    }

    private void OnFieldFocusChanged(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => PseudoClasses.Set(FieldFocusedPseudoClass, _fieldHost?.IsKeyboardFocusWithin == true));
    }

    private void BuildSendContent()
    {
        if (_send is null)
        {
            return;
        }
        _sendVerb = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _sendCount = new RollingDigits
        {
            UseGrouping = false,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _sendNoun = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            Margin = new Thickness(4, 0, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        var words = new StackPanel { Orientation = Orientation.Horizontal, Children = { _sendVerb, _sendCount, _sendNoun } };
        var width = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, CrossClipOverflow = 12, Child = words };
        _send.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { InvitePeopleText.Glyph(ArcIcons.Send, 16), width }
        };
    }

    private void BuildPasteContent()
    {
        if (_pasteSample is null)
        {
            return;
        }
        _pasteSample.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                InvitePeopleText.Glyph(ArcIcons.ClipboardPaste, 14),
                new TextBlock { VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }

    #region Lifetime and size

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        CaptureStartState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CloseSuggestions();
        CloseRoleMenu(returnFocus: false);
        CancelWork();
        _flights?.LandAll();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints();
    }

    private void UpdateBreakpoints()
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        var card = IsFramed ? Math.Min(width, _card?.MaxWidth is > 0 and < double.PositiveInfinity ? _card.MaxWidth : width) : width;
        var padding = compact ? 16 : 20;
        var narrow = card - 2 * padding < NarrowContentWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (narrow == _isNarrow)
        {
            return;
        }
        _isNarrow = narrow;
        foreach (var row in _pendingRowMap.Values)
        {
            row.SetNarrow(narrow);
        }
        if (_popup?.IsOpen == true)
        {
            RefreshSuggestions();
        }
    }

    #endregion

    #region Texts

    private void UpdateTexts()
    {
        var labels = Labels;
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
        if (_hint is not null && _summaryTimer is null)
        {
            _hint.Content = HintBlock(labels.Hint);
        }
        if (_pasteSample is not null)
        {
            _pasteSample.IsVisible = !string.IsNullOrEmpty(SampleText);
            if (_pasteSample.Content is StackPanel { Children: [_, TextBlock text] })
            {
                text.Text = labels.PasteSample;
            }
            AutomationProperties.SetName(_pasteSample, labels.PasteSample);
        }
        if (_removeInvalid is not null)
        {
            _removeInvalid.Content = labels.RemoveInvalid;
            AutomationProperties.SetName(_removeInvalid, labels.RemoveInvalid);
        }
        if (_errorsHelp is not null)
        {
            _errorsHelp.Text = labels.ErrorsHelp;
        }
        if (_seatOf is not null)
        {
            _seatOf.Text = labels.SeatsOf;
        }
        if (_seatWord is not null)
        {
            _seatWord.Text = labels.Seats;
        }
        if (_removeToSend is not null)
        {
            _removeToSend.Text = labels.RemoveToSend;
        }
        if (_reset is not null)
        {
            _reset.IsVisible = IsResetVisible;
            AutomationProperties.SetName(_reset, labels.ResetName);
        }
        if (_pendingHeading is not null)
        {
            _pendingHeading.Text = labels.Pending;
        }
        if (_pendingEmpty is not null)
        {
            _pendingEmpty.Text = labels.NoPending;
        }
        if (_chips is not null)
        {
            AutomationProperties.SetName(_chips, labels.ChipsName);
        }
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, labels.FieldName);
            AutomationProperties.SetHelpText(_input, labels.Hint);
        }
        UpdatePlaceholder();
        UpdateSeats();
        UpdateErrors();
        SyncTeam(animate: false);
    }

    private void ShowSummary(string summary)
    {
        if (_hint is null)
        {
            return;
        }
        _hint.Content = HintBlock(summary);
        _summaryTimer?.Stop();
        _summaryTimer = new DispatcherTimer { Interval = SummaryDuration };
        _summaryTimer.Tick += (_, _) => RestoreHint();
        _summaryTimer.Start();
    }

    /// <summary>Puts the hint back after a paste summary (the timer does it after <see cref="SummaryDuration"/>).</summary>
    internal void RestoreHint()
    {
        _summaryTimer?.Stop();
        _summaryTimer = null;
        if (_hint is not null)
        {
            _hint.Content = HintBlock(Labels.Hint);
        }
    }

    /// <summary>The hint's text: 12 on a 16.8 line, wrapping to a second line when the row is narrow.</summary>
    private static TextBlock HintBlock(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = 16.8,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void Announce(string? text) => _announcer?.Announce(text);

    #endregion

    #region Seats, send and errors

    /// <summary>The seats as they stand.</summary>
    internal InvitePeopleSeats Seats =>
        new(Members.Count, PendingInvites.Count, _entries.Count(e => e.IsValid), EffectivePlan?.SeatLimit ?? 0);

    private void UpdateSeats()
    {
        var seats = Seats;
        var plan = EffectivePlan;
        var labels = Labels;
        var over = plan is not null && seats.IsOver;
        SeatsUsed = seats.Total;
        IsOverLimit = over;
        PseudoClasses.Set(OverPseudoClass, over);
        if (_seatTotal is not null)
        {
            _seatTotal.Value = seats.Total;
        }
        if (_seatLimit is not null)
        {
            _seatLimit.Value = seats.Limit;
        }
        if (_plan is not null)
        {
            _plan.Content = plan?.Name is { Length: > 0 } name ? labels.PlanName(name) : null;
        }
        if (_meter is not null)
        {
            _meter.SetSeats(seats);
            AutomationProperties.SetName(_meter, labels.MeterName);
            AutomationProperties.SetHelpText(_meter, labels.MeterValueText(seats.Total, seats.Limit, over ? seats.Over : 0));
        }
        if (_legendMembers is not null)
        {
            _legendMembers.Text = labels.LegendMembers(seats.Members);
        }
        if (_legendPending is not null)
        {
            _legendPending.Text = labels.LegendPending(seats.Pending);
        }
        if (_legendNew is not null)
        {
            _legendNew.Text = labels.LegendNew(seats.New);
        }
        if (_overHost is not null)
        {
            _overHost.IsExpanded = over;
        }
        if (_over is not null)
        {
            _over.Opacity = over ? 1 : 0;
        }
        if (over)
        {
            if (_overCount is not null)
            {
                _overCount.Value = seats.Over;
            }
            if (_overText is not null)
            {
                _overText.Text = labels.OverLimit(seats.Over, plan?.Name ?? string.Empty);
            }
            var next = NextPlan;
            if (_upgrade is not null)
            {
                _upgrade.IsVisible = next is not null;
                if (next is not null)
                {
                    var text = labels.Upgrade(next.Name ?? string.Empty);
                    _upgrade.Content = text;
                    AutomationProperties.SetName(_upgrade, text);
                }
            }
            if (_removeToSend is not null)
            {
                _removeToSend.IsVisible = next is null;
            }
        }
        UpdateSend();
    }

    private void UpdateSend()
    {
        if (_send is null)
        {
            return;
        }
        var labels = Labels;
        var valid = _entries.Count(e => e.IsValid);
        var invalid = _entries.Count - valid;
        var over = IsOverLimit;
        _send.IsEnabled = _entries.Count > 0;
        _send.SetState(InvitePeopleButton.ReadyPseudoClass, valid > 0 && invalid == 0 && !over);
        if (_sendVerb is not null)
        {
            _sendVerb.Text = labels.SendVerb;
        }
        if (_sendCount is not null)
        {
            _sendCount.IsVisible = valid > 0;
            _sendCount.Value = valid;
        }
        if (_sendNoun is not null)
        {
            _sendNoun.Content = valid == 1 ? labels.InviteOne : labels.InviteOther;
        }
        AutomationProperties.SetName(_send, labels.SendName(valid));
        var plan = EffectivePlan?.Name ?? string.Empty;
        AutomationProperties.SetHelpText(_send, invalid > 0
            ? labels.ErrorsTitle(invalid)
            : over ? labels.MeterValueText(SeatsUsed, EffectivePlan?.SeatLimit ?? 0, Seats.Over) + ". " + labels.OverLimit(Seats.Over, plan)
            : null);
    }

    private void UpdateErrors()
    {
        var invalid = _entries.Where(e => !e.IsValid).ToList();
        var labels = Labels;
        if (_errorsHost is not null)
        {
            _errorsHost.IsExpanded = invalid.Count > 0;
        }
        if (_errors is not null)
        {
            _errors.Opacity = invalid.Count > 0 ? 1 : 0;
        }
        if (invalid.Count == 0)
        {
            return;
        }
        if (_errorsTitle is not null)
        {
            _errorsTitle.Text = labels.ErrorsTitle(invalid.Count);
        }
        if (_errorLines is not null)
        {
            _errorLines.Children.Clear();
            var domain = ExampleDomainText();
            foreach (var entry in invalid)
            {
                var line = InvitePeopleText.Create(13, ArcDesignTokenKind.TextSecondary, lineHeight: 18.2);
                line.Text = labels.Reason(entry, domain);
                _errorLines.Children.Add(line);
            }
        }
    }

    /// <summary>The example domain of the "incomplete domain" message.</summary>
    internal string ExampleDomainText()
    {
        if (!string.IsNullOrWhiteSpace(ExampleDomain))
        {
            return ExampleDomain!;
        }
        foreach (var person in EffectiveDirectory)
        {
            var at = person.Email?.IndexOf('@') ?? -1;
            if (at > 0 && at < person.Email!.Length - 1)
            {
                return person.Email[(at + 1)..];
            }
        }
        return "example.com";
    }

    #endregion
}
