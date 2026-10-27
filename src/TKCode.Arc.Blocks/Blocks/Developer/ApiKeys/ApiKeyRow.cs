using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One key in an <see cref="ApiKeys"/> list: a whole-row button (name, environment badge and mask; scopes; dates; chevron)
/// and the details it opens (creator, last request, request count, and the hold-to-revoke line). Four columns at 700 and
/// wider; below, the scopes and the creation date move into the details; below 540 the details stack.
/// </summary>
[TemplatePart(TriggerPart, typeof(ApiKeyRowTrigger))]
[TemplatePart(DetailHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(RevokePart, typeof(HoldToConfirm))]
[PseudoClasses(FirstPseudoClass, ExpandedPseudoClass, RevokedPseudoClass, StalePseudoClass, FreshPseudoClass,
    NarrowPseudoClass, CompactPseudoClass)]
internal sealed class ApiKeyRow : TemplatedControl
{
    internal const string TriggerPart = "PART_Trigger";
    internal const string ColumnsPart = "PART_Columns";
    internal const string NamePart = "PART_Name";
    internal const string NameStrikePart = "PART_NameStrike";
    internal const string BadgePart = "PART_Badge";
    internal const string MaskPart = "PART_Mask";
    internal const string MaskStrikePart = "PART_MaskStrike";
    internal const string ScopesPart = "PART_Scopes";
    internal const string DatesPart = "PART_Dates";
    internal const string StaleIconPart = "PART_StaleIcon";
    internal const string UsedPart = "PART_Used";
    internal const string CreatedPart = "PART_Created";
    internal const string ChevronPart = "PART_Chevron";
    internal const string DetailHostPart = "PART_DetailHost";
    internal const string DetailPart = "PART_Detail";
    internal const string FactsPart = "PART_Facts";
    internal const string CreatedByFactPart = "PART_CreatedByFact";
    internal const string CreatedByLabelPart = "PART_CreatedByLabel";
    internal const string AvatarPart = "PART_Avatar";
    internal const string CreatedByPart = "PART_CreatedBy";
    internal const string LastRequestFactPart = "PART_LastRequestFact";
    internal const string LastRequestLabelPart = "PART_LastRequestLabel";
    internal const string LastRequestPart = "PART_LastRequest";
    internal const string RequestsFactPart = "PART_RequestsFact";
    internal const string RequestsLabelPart = "PART_RequestsLabel";
    internal const string RequestsPart = "PART_Requests";
    internal const string CreatedFactPart = "PART_CreatedFact";
    internal const string CreatedLabelPart = "PART_CreatedLabel";
    internal const string CreatedDatePart = "PART_CreatedDate";
    internal const string ScopesFactPart = "PART_ScopesFact";
    internal const string ScopesLabelPart = "PART_ScopesLabel";
    internal const string DetailScopesPart = "PART_DetailScopes";
    internal const string RevokeLinePart = "PART_RevokeLine";
    internal const string NotePart = "PART_Note";
    internal const string NoteIconPart = "PART_NoteIcon";
    internal const string NoteTextPart = "PART_NoteText";
    internal const string RevokePart = "PART_Revoke";

    internal const string FirstPseudoClass = ":first";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string RevokedPseudoClass = ":revoked";
    internal const string StalePseudoClass = ":stale";
    internal const string FreshPseudoClass = ":fresh";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";

    private ApiKeyRowTrigger? _trigger;
    private Grid? _columns;
    private TextBlock? _name;
    private ApiKeyStrike? _nameStrike;
    private Badge? _badge;
    private TextBlock? _mask;
    private ApiKeyStrike? _maskStrike;
    private WrapPanel? _scopes;
    private Control? _dates;
    private Control? _staleIcon;
    private TextBlock? _used;
    private TextBlock? _created;
    private Control? _chevron;
    private SpringSizeDecorator? _detailHost;
    private Control? _detail;
    private Grid? _facts;
    private Control? _createdByFact;
    private TextBlock? _createdByLabel;
    private Avatar? _avatar;
    private TextBlock? _createdBy;
    private Control? _lastRequestFact;
    private TextBlock? _lastRequestLabel;
    private TextBlock? _lastRequest;
    private Control? _requestsFact;
    private TextBlock? _requestsLabel;
    private TextBlock? _requests;
    private Control? _createdFact;
    private TextBlock? _createdLabel;
    private TextBlock? _createdDate;
    private Control? _scopesFact;
    private TextBlock? _scopesLabel;
    private WrapPanel? _detailScopes;
    private Grid? _revokeLine;
    private Control? _note;
    private Control? _noteIcon;
    private TextBlock? _noteText;
    private HoldToConfirm? _revoke;

    private MotionValue<double>? _detailFade;
    private ApiKeyItem? _item;
    private bool _expanded;
    private bool _narrow;
    private bool _compact;

    public ApiKeyRow(ApiKeys owner, ApiKeyItem item)
    {
        Owner = owner;
        Item = item;
    }

    /// <summary>The block that owns the row.</summary>
    public ApiKeys Owner { get; }

    /// <summary>The key the row shows.</summary>
    public ApiKeyItem Item
    {
        get => _item!;
        private set => _item = value;
    }

    /// <summary>The whole-row button.</summary>
    public ApiKeyRowTrigger? Trigger => _trigger;

    /// <summary>The hold-to-revoke button in the details.</summary>
    public HoldToConfirm? RevokeButton => _revoke;

    /// <summary>Whether the details are open.</summary>
    public bool IsExpanded => _expanded;

    /// <summary>Whether the details are shown (open, or still folding away).</summary>
    public bool IsDetailShown => _detail?.IsVisible == true;

    /// <summary>The name and mask strikes (tests).</summary>
    internal ApiKeyStrike? NameStrike => _nameStrike;

    internal ApiKeyStrike? MaskStrike => _maskStrike;

    protected override AutomationPeer OnCreateAutomationPeer() => new ApiKeyRowPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        if (_revoke is not null)
        {
            _revoke.Confirmed -= OnRevokeConfirmed;
        }
        T? Find<T>(string name) where T : class => e.NameScope.Find<T>(name);
        _trigger = Find<ApiKeyRowTrigger>(TriggerPart);
        _columns = Find<Grid>(ColumnsPart);
        _name = Find<TextBlock>(NamePart);
        _nameStrike = Find<ApiKeyStrike>(NameStrikePart);
        _badge = Find<Badge>(BadgePart);
        _mask = Find<TextBlock>(MaskPart);
        _maskStrike = Find<ApiKeyStrike>(MaskStrikePart);
        _scopes = Find<WrapPanel>(ScopesPart);
        _dates = Find<Control>(DatesPart);
        _staleIcon = Find<Control>(StaleIconPart);
        _used = Find<TextBlock>(UsedPart);
        _created = Find<TextBlock>(CreatedPart);
        _chevron = Find<Control>(ChevronPart);
        _detailHost = Find<SpringSizeDecorator>(DetailHostPart);
        _detail = Find<Control>(DetailPart);
        _facts = Find<Grid>(FactsPart);
        _createdByFact = Find<Control>(CreatedByFactPart);
        _createdByLabel = Find<TextBlock>(CreatedByLabelPart);
        _avatar = Find<Avatar>(AvatarPart);
        _createdBy = Find<TextBlock>(CreatedByPart);
        _lastRequestFact = Find<Control>(LastRequestFactPart);
        _lastRequestLabel = Find<TextBlock>(LastRequestLabelPart);
        _lastRequest = Find<TextBlock>(LastRequestPart);
        _requestsFact = Find<Control>(RequestsFactPart);
        _requestsLabel = Find<TextBlock>(RequestsLabelPart);
        _requests = Find<TextBlock>(RequestsPart);
        _createdFact = Find<Control>(CreatedFactPart);
        _createdLabel = Find<TextBlock>(CreatedLabelPart);
        _createdDate = Find<TextBlock>(CreatedDatePart);
        _scopesFact = Find<Control>(ScopesFactPart);
        _scopesLabel = Find<TextBlock>(ScopesLabelPart);
        _detailScopes = Find<WrapPanel>(DetailScopesPart);
        _revokeLine = Find<Grid>(RevokeLinePart);
        _note = Find<Control>(NotePart);
        _noteIcon = Find<Control>(NoteIconPart);
        _noteText = Find<TextBlock>(NoteTextPart);
        _revoke = Find<HoldToConfirm>(RevokePart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
        }
        if (_revoke is not null)
        {
            _revoke.Confirmed += OnRevokeConfirmed;
        }
        _detailFade = null;
        Refresh();
        ApplyLayout();
        ApplyExpanded(animate: false);
        ApplyRevoked(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Item.PropertyChanged += OnItemChanged;
        Item.ScopesChanged += OnScopesChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Item.PropertyChanged -= OnItemChanged;
        Item.ScopesChanged -= OnScopesChanged;
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ApiKeyItem.IsRevokedProperty)
        {
            ApplyRevoked(animate: true);
        }
        else if (e.Property == ApiKeyItem.IsFreshProperty)
        {
            PseudoClasses.Set(FreshPseudoClass, Item.IsFresh);
        }
        else
        {
            Refresh();
        }
    }

    private void OnScopesChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Marks the row as the first in the list (no line above it).</summary>
    public void SetFirst(bool first) => PseudoClasses.Set(FirstPseudoClass, first);

    /// <summary>Applies the block's breakpoints.</summary>
    public void SetLayout(bool narrow, bool compact)
    {
        if (_narrow == narrow && _compact == compact)
        {
            return;
        }
        _narrow = narrow;
        _compact = compact;
        ApplyLayout();
    }

    /// <summary>Opens or closes the details.</summary>
    public void SetExpanded(bool expanded, bool animate)
    {
        if (_expanded == expanded)
        {
            return;
        }
        _expanded = expanded;
        ApplyExpanded(animate);
    }

    /// <summary>Puts the hold button back (a cancelled or failed revoke).</summary>
    public void ResetHold()
    {
        if (_revoke is not null)
        {
            _revoke.CancelHold();
            _revoke.IsConfirmed = false;
        }
    }

    /// <summary>Rewrites every text from the item, the owner's labels and its clock.</summary>
    public void Refresh()
    {
        if (_trigger is null || _item is null)
        {
            return;
        }
        var labels = Owner.Labels;
        var item = Item;
        var now = Owner.Now;
        var stale = Owner.IsStale(item, now);
        PseudoClasses.Set(StalePseudoClass, stale);
        PseudoClasses.Set(FreshPseudoClass, item.IsFresh);

        if (_name is not null)
        {
            _name.Text = item.Name;
        }
        if (_badge is not null)
        {
            _badge.Content = item.Environment == ApiKeyEnvironment.Live ? labels.Live : labels.Test;
            _badge.Tone = item.Environment == ApiKeyEnvironment.Live ? BadgeTone.Success : BadgeTone.Neutral;
        }
        if (_mask is not null)
        {
            _mask.Text = item.MaskedText;
        }
        if (_scopes is not null)
        {
            ApiKeyChips.Fill(_scopes, item.Scopes);
        }
        if (_detailScopes is not null)
        {
            ApiKeyChips.Fill(_detailScopes, item.Scopes);
        }
        var used = item.LastUsedText
                   ?? (item.LastUsedAt is { } at ? labels.Used(labels.Ago(Clamp(now - at))) : labels.NeverUsed);
        if (_used is not null)
        {
            _used.Text = used;
        }
        if (_staleIcon is not null)
        {
            _staleIcon.IsVisible = stale;
        }
        var date = labels.Date(item.CreatedAt);
        if (_created is not null)
        {
            _created.Text = labels.Created(date);
        }
        if (_createdDate is not null)
        {
            _createdDate.Text = date;
        }
        if (_createdByLabel is not null)
        {
            _createdByLabel.Text = labels.CreatedByLabel;
        }
        var creator = string.IsNullOrWhiteSpace(item.CreatedBy) ? labels.UnknownPerson : item.CreatedBy!;
        if (_createdBy is not null)
        {
            _createdBy.Text = creator;
        }
        if (_avatar is not null)
        {
            _avatar.DisplayName = creator;
            _avatar.IsVisible = !string.IsNullOrWhiteSpace(item.CreatedBy);
        }
        if (_lastRequestLabel is not null)
        {
            _lastRequestLabel.Text = labels.LastRequestLabel;
        }
        if (_lastRequest is not null)
        {
            _lastRequest.Text = item.LastRequestText
                                ?? (item.LastUsedAt is { } last ? labels.Ago(Clamp(now - last)) : labels.NoRequests);
        }
        if (_requestsLabel is not null)
        {
            _requestsLabel.Text = labels.RequestsLabel;
        }
        if (_requests is not null)
        {
            _requests.Text = DeveloperText.Thousands(item.RequestsLast30Days);
        }
        if (_createdLabel is not null)
        {
            _createdLabel.Text = labels.CreatedLabel;
        }
        if (_scopesLabel is not null)
        {
            _scopesLabel.Text = labels.ScopesLabel;
        }
        if (_noteIcon is not null)
        {
            _noteIcon.IsVisible = stale;
        }
        if (_noteText is not null)
        {
            _noteText.Text = stale && item.LastUsedAt is { } idle
                ? labels.StaleNote((int)Math.Floor(Clamp(now - idle).TotalDays))
                : labels.RevokeNote;
        }
        if (_revoke is not null)
        {
            _revoke.Label = labels.HoldToRevoke;
            _revoke.ConfirmedLabel = labels.Revoked;
            _revoke.HoldDuration = Owner.RevokeHoldDuration;
            AutomationProperties.SetName(_revoke, $"{labels.HoldToRevoke}: {item.Name}");
        }

        // The row reads as one button: name, environment, the last four, scopes and dates.
        var parts = new List<string> { item.Name, _badge?.Content as string ?? string.Empty, labels.KeyEnding(item.LastFour) };
        if (item.Scopes.Count > 0)
        {
            parts.Add($"{labels.ScopesPrefix}: {string.Join(", ", item.Scopes)}");
        }
        parts.Add(stale ? $"{labels.Idle}, {used}" : used);
        parts.Add(labels.Created(date));
        AutomationProperties.SetName(_trigger, string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p))));
        if (_detail is not null)
        {
            AutomationProperties.SetName(_detail, labels.DetailsName(item.Name));
        }
        AutomationProperties.SetAutomationId(this, item.Key);
    }

    private static TimeSpan Clamp(TimeSpan span) => span < TimeSpan.Zero ? TimeSpan.Zero : span;

    private void ApplyLayout()
    {
        PseudoClasses.Set(NarrowPseudoClass, _narrow);
        PseudoClasses.Set(CompactPseudoClass, _compact);
        if (_columns is not null && _scopes is not null && _dates is not null && _chevron is not null)
        {
            if (_narrow)
            {
                // Name | used (right-aligned) | chevron; the scopes and the creation date move into the details.
                _columns.ColumnDefinitions = new ColumnDefinitions("*,Auto,16");
                _scopes.IsVisible = false;
                Grid.SetColumn(_dates, 1);
                Grid.SetColumn(_chevron, 2);
            }
            else
            {
                var dates = new ColumnDefinition(new GridLength(0.6, GridUnitType.Star)) { MinWidth = Owner.DatesMinWidth };
                _columns.ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(new GridLength(1.25, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1.15, GridUnitType.Star)),
                    dates,
                    new ColumnDefinition(new GridLength(16)),
                };
                _scopes.IsVisible = true;
                Grid.SetColumn(_dates, 2);
                Grid.SetColumn(_chevron, 3);
            }
        }
        if (_created is not null)
        {
            _created.IsVisible = !_narrow;
        }
        if (_dates is not null)
        {
            _dates.HorizontalAlignment = _narrow ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        }
        if (_used?.Parent is StackPanel usedLine)
        {
            usedLine.HorizontalAlignment = _narrow ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        ArrangeFacts();
        if (_revokeLine is not null && _note is not null && _revoke is not null)
        {
            // Compact: the note above, the button under it at the start.
            _revokeLine.ColumnDefinitions = _compact ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
            _revokeLine.RowDefinitions = _compact ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetColumn(_revoke, _compact ? 0 : 1);
            Grid.SetRow(_revoke, _compact ? 1 : 0);
            _revoke.HorizontalAlignment = _compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }
    }

    private void ArrangeFacts()
    {
        if (_facts is null || _createdByFact is null || _lastRequestFact is null || _requestsFact is null
            || _createdFact is null || _scopesFact is null)
        {
            return;
        }
        Control[] facts = [_createdByFact, _lastRequestFact, _requestsFact, _createdFact, _scopesFact];
        _createdFact.IsVisible = _narrow;
        _scopesFact.IsVisible = _narrow;
        int columns = _compact ? 1 : _narrow ? 2 : 3;
        _facts.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)));
        var visible = facts.Where(f => f.IsVisible).ToList();
        var rows = 0;
        for (var i = 0; i < visible.Count; i++)
        {
            var fact = visible[i];
            // The scope chips take a whole line of their own.
            var span = ReferenceEquals(fact, _scopesFact) ? columns : 1;
            var column = ReferenceEquals(fact, _scopesFact) ? 0 : i % columns;
            var row = ReferenceEquals(fact, _scopesFact) ? (i + columns - 1) / columns : i / columns;
            Grid.SetColumn(fact, column);
            Grid.SetRow(fact, row);
            Grid.SetColumnSpan(fact, span);
            rows = Math.Max(rows, row + 1);
        }
        var definitions = new RowDefinitions();
        for (var i = 0; i < rows; i++)
        {
            definitions.Add(new RowDefinition(GridLength.Auto));
        }
        _facts.RowDefinitions = definitions;
    }

    private void ApplyExpanded(bool animate)
    {
        PseudoClasses.Set(ExpandedPseudoClass, _expanded);
        if (_trigger is not null)
        {
            _trigger.IsExpanded = _expanded;
        }
        if (_detailHost is null || _detail is null)
        {
            return;
        }
        animate &= IsLoaded;
        var level = animate ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        _detailFade ??= CreateDetailFade(_detail);
        if (_expanded)
        {
            _detail.IsVisible = true;
            _detailHost.IsExpanded = true;
            if (level == ArcMotionLevel.Off)
            {
                _detailFade.Jump(1);
            }
            else
            {
                // Arc: the details fade in over 240ms after 50ms while their height opens on Smooth.
                _ = _detailFade.AnimateTo(1, level == ArcMotionLevel.Full
                    ? MotionSpec.Tween(DeveloperSprings.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(50))
                    : DeveloperSprings.ReducedFade());
            }
        }
        else
        {
            _detailHost.IsExpanded = false;
            if (_detail.IsKeyboardFocusWithin && _trigger is not null)
            {
                // A button that folds away must not keep the focus.
                _trigger.Focus(NavigationMethodFor());
            }
            if (level == ArcMotionLevel.Off)
            {
                _detailFade.Jump(0);
            }
            else
            {
                _ = _detailFade.AnimateTo(0, level == ArcMotionLevel.Full
                    ? MotionSpec.Tween(DeveloperSprings.Fast, ArcEasings.Exit)
                    : DeveloperSprings.ReducedFade());
            }
        }
    }

    private Avalonia.Input.NavigationMethod NavigationMethodFor() => ApiKeyFocus.Method(Owner.LastInputWasKeyboard);

    private MotionValue<double> CreateDetailFade(Control detail)
    {
        // The details' opacity runs on its own timing (in 240ms after 50ms, out 160ms) while the decorator springs the height.
        var value = new MotionValue<double>(this, _expanded ? 1 : 0, MotionVectors.Double);
        detail.Opacity = value.Value;
        detail.IsVisible = _expanded;
        value.Changed += v =>
        {
            detail.Opacity = Math.Clamp(v, 0, 1);
            if (!_expanded && v <= 0 && value.Target <= 0)
            {
                // Out of the tab order once it has faded: its hold button must not take the focus while folded.
                detail.IsVisible = false;
            }
        };
        return value;
    }

    private void ApplyRevoked(bool animate)
    {
        var revoked = _item?.IsRevoked == true;
        PseudoClasses.Set(RevokedPseudoClass, revoked);
        if (!animate || !IsLoaded)
        {
            _nameStrike?.Set(revoked, TimeSpan.Zero);
            _maskStrike?.Set(revoked, TimeSpan.Zero);
            return;
        }
        _nameStrike?.Set(revoked, TimeSpan.Zero);
        _maskStrike?.Set(revoked, DeveloperSprings.StrikeStagger);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Item.IsRevoked)
        {
            return;
        }
        Owner.ToggleExpanded(Item);
    }

    private void OnRevokeConfirmed(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Owner.OnRevokeConfirmed(this);
    }

    private sealed class ApiKeyRowPeer(ApiKeyRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore() => ((ApiKeyRow)Owner).Item.Name;
    }
}
