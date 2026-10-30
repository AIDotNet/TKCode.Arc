using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class TrialBanner
{
    /// <summary>The parts of one face (the banner row or the card) that show the state.</summary>
    internal sealed class Face
    {
        public required string Prefix { get; init; }
        public Control? Root { get; set; }
        public ArcRing? Ring { get; set; }
        public ArcContentSwap? Mark { get; set; }
        public ArcContentSwap? Title { get; set; }
        public TextBlock? Heading { get; set; }
        public ArcContentSwap? Detail { get; set; }
        public ArcContentSwap? Lead { get; set; }
        public ArcContentSwap? Note { get; set; }
        public StackPanel? Features { get; set; }
        public Button? Primary { get; set; }
        public Button? Compare { get; set; }
        public Button? Snooze { get; set; }
        public Button? Dismiss { get; set; }
        public MenuSurface? Menu { get; set; }
        public string? MarkKey { get; set; }
        public string? TitleText { get; set; }
        public string? DetailText { get; set; }
        public string? LeadText { get; set; }
        public string? NoteText { get; set; }
        public string? MenuKey { get; set; }
        public string? FeaturesKey { get; set; }
    }

    /// <summary>The ring's spring: its arc moves to the new length in 0.24s with no bounce.</summary>
    private static readonly SpringSpec s_ringSpring = SpringSpec.FromVisual(0.24, 0);

    private readonly Face _banner = new() { Prefix = "PART_Banner" };
    private readonly Face _card = new() { Prefix = "PART_Card" };
    private TrialBannerStage? _stage;
    private TrialBannerRow? _row;
    private TrialBannerPill? _pill;
    private ExplainerAnnouncer? _live;
    private MotionValue<double>? _ringValue;
    private bool _featuresDirty = true;

    internal TrialBannerStage? Stage => _stage;

    internal TrialBannerPill? PillFace => _pill;

    internal Face BannerParts => _banner;

    internal Face CardParts => _card;

    internal ExplainerAnnouncer? LiveRegion => Source._live;

    /// <summary>The face on screen: the banner row, the card, the pill or nothing.</summary>
    internal Control? ActiveFace => _stage?.Active;

    /// <summary>The face whose actions are on screen (the banner or the card), or null when the pill or nothing shows.</summary>
    internal Face? ActiveParts =>
        ActiveFace is { } active ? ReferenceEquals(active, _banner.Root) ? _banner : ReferenceEquals(active, _card.Root) ? _card : null : null;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _stage = e.NameScope.Find<TrialBannerStage>(StagePart);
        _row = e.NameScope.Find<TrialBannerRow>(BannerRowPart);
        _pill = e.NameScope.Find<TrialBannerPill>(PillPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_stage is not null)
        {
            _stage.Surface = e.NameScope.Find<Border>(SurfacePart);
        }
        _banner.Root = e.NameScope.Find<Control>(BannerFacePart);
        _card.Root = e.NameScope.Find<Control>(CardFacePart);
        FindFace(_banner, e.NameScope);
        FindFace(_card, e.NameScope);
        if (_row is not null)
        {
            _row.CompactChanged += OnCompactChanged;
            OnCompactChanged(_row.IsCompact);
        }
        if (_pill is not null)
        {
            _pill.Click += OnPillClick;
        }
        _featuresDirty = true;
        UpdateVariantClasses();
        Refresh(instant: true);
    }

    private void FindFace(Face face, INameScope scope)
    {
        var p = face.Prefix;
        face.Ring = scope.Find<ArcRing>(p + "Ring");
        face.Mark = scope.Find<ArcContentSwap>(p + "Mark");
        face.Title = scope.Find<ArcContentSwap>(p + "Title");
        face.Heading = scope.Find<TextBlock>(p + "Heading");
        face.Detail = scope.Find<ArcContentSwap>(p + "Detail");
        face.Lead = scope.Find<ArcContentSwap>(p + "Lead");
        face.Note = scope.Find<ArcContentSwap>(p + "Note");
        face.Features = scope.Find<StackPanel>(p + "Features");
        face.Primary = scope.Find<Button>(p + "Primary");
        face.Compare = scope.Find<Button>(p + "Compare");
        face.Snooze = scope.Find<Button>(p + "Snooze");
        face.Dismiss = scope.Find<Button>(p + "Dismiss");
        face.MarkKey = face.TitleText = face.DetailText = face.LeadText = face.NoteText = face.MenuKey = face.FeaturesKey = null;
        if (face.Primary is not null)
        {
            face.Primary.Click += OnPrimaryClick;
        }
        if (face.Compare is not null)
        {
            face.Compare.Click += OnCompareClick;
        }
        if (face.Dismiss is not null)
        {
            face.Dismiss.Click += OnDismissClick;
        }
        if (face.Snooze is not null)
        {
            face.Menu = new MenuSurface
            {
                Variant = MenuSurfaceVariant.Dropdown,
                Side = MenuSide.Bottom,
                Alignment = MenuAlignment.End,
                AnchorGap = 6
            };
            face.Menu.ItemClicked += OnSnoozeItemClicked;
            DropdownMenu.SetMenu(face.Snooze, face.Menu);
        }
    }

    private void Unhook()
    {
        if (_row is not null)
        {
            _row.CompactChanged -= OnCompactChanged;
        }
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
        }
        foreach (var face in new[] { _banner, _card })
        {
            if (face.Primary is not null)
            {
                face.Primary.Click -= OnPrimaryClick;
            }
            if (face.Compare is not null)
            {
                face.Compare.Click -= OnCompareClick;
            }
            if (face.Dismiss is not null)
            {
                face.Dismiss.Click -= OnDismissClick;
            }
            if (face.Snooze is not null)
            {
                DropdownMenu.SetMenu(face.Snooze, null);
            }
            if (face.Menu is not null)
            {
                face.Menu.ItemClicked -= OnSnoozeItemClicked;
                face.Menu = null;
            }
        }
    }

    /// <summary>Under 600px the banner's second line may wrap to two lines, and both are reserved so nothing shifts.</summary>
    private void OnCompactChanged(bool compact)
    {
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_banner.Detail is not { } detail)
        {
            return;
        }
        TextBlock.SetTextWrapping(detail, compact ? TextWrapping.Wrap : TextWrapping.NoWrap);
        TextBlock.SetMaxLines(detail, compact ? 2 : 1);
        detail.MinHeight = compact ? 2 * TextBlock.GetLineHeight(detail) : 0;
    }

    private void UpdateVariantClasses()
    {
        var popover = _host is not null;
        var variant = popover ? TrialBannerVariant.Card : Variant;
        PseudoClasses.Set(BannerPseudoClass, variant == TrialBannerVariant.Banner);
        PseudoClasses.Set(CardPseudoClass, variant == TrialBannerVariant.Card);
        PseudoClasses.Set(PillPseudoClass, variant == TrialBannerVariant.Pill);
        PseudoClasses.Set(PopoverPseudoClass, popover);
    }

    /// <summary>The face to show: the card in a popover, the pill for the pill variant or a folded banner, else the variant's own.</summary>
    private Control? TargetFace()
    {
        if (_host is not null)
        {
            return _card.Root;
        }
        if (Variant == TrialBannerVariant.Pill)
        {
            return IsCollapsed && CollapseTo == TrialBannerCollapse.None ? null : _pill;
        }
        if (IsCollapsed)
        {
            return CollapseTo == TrialBannerCollapse.Pill ? _pill : null;
        }
        return Variant == TrialBannerVariant.Card ? _card.Root : _banner.Root;
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    internal void Refresh(bool instant)
    {
        var source = Source;
        var labels = source.Labels;
        var clock = source.TimeProvider;
        var (days, end, ended) = TrialBannerMath.Resolve(source.DaysLeft, source.EndsAt, clock);
        // Without days, an end date or an upgrade there is no trial to show: one neutral line, an empty ring, no actions.
        var empty = !source.HasTrial;
        var status = empty ? TrialStatus.Active : TrialBannerMath.StatusOf(days, ended, source.WarnAt, source.IsUpgraded);
        var statusChanged = status != Status;
        Status = status;
        PseudoClasses.Set(WarningPseudoClass, status == TrialStatus.Warning);
        PseudoClasses.Set(ExpiredPseudoClass, status == TrialStatus.Expired);
        PseudoClasses.Set(UpgradedPseudoClass, status == TrialStatus.Upgraded);
        var phase = source._phase;
        PseudoClasses.Set(BusyPseudoClass, phase == TrialBillingPhase.Busy);
        PseudoClasses.Set(FailedPseudoClass, phase == TrialBillingPhase.Failed);
        if (statusChanged)
        {
            _featuresDirty = true;
        }
        AutomationProperties.SetName(this, empty ? labels.EmptyTitle : labels.Region(source.Plan));

        if (_stage is null)
        {
            return;
        }
        var target = TargetFace();
        _stage.Show(target, animate: !instant);
        PseudoClasses.Set(FoldedPseudoClass, _host is null && Variant != TrialBannerVariant.Pill && IsCollapsed);

        var plan = source.Plan;
        var loss = source.LossSummary;
        var workspace = string.IsNullOrWhiteSpace(source.Workspace) ? labels.DefaultWorkspace : source.Workspace;
        var daysText = labels.Days(days);
        var date = labels.Date(end);
        var endDay = TrialBannerMath.EndDay(days, end, labels);
        var note = source._note;
        var message = phase switch
        {
            TrialBillingPhase.Busy => labels.Opening,
            TrialBillingPhase.Failed => labels.Failed,
            _ => note
        };

        // The banner row.
        var (title, detail) = status switch
        {
            _ when empty => (labels.EmptyTitle, string.Empty),
            TrialStatus.Warning => (labels.WarningTitle(plan, endDay), labels.WarningDetail(days, loss)),
            TrialStatus.Expired => (labels.ExpiredTitle(plan), labels.ExpiredDetail(workspace, loss)),
            TrialStatus.Upgraded => (labels.UpgradedTitle(plan), labels.UpgradedDetail(workspace, loss)),
            _ => (labels.ActiveTitle(daysText, plan), labels.ActiveDetail(loss, date)),
        };
        SwapText(_banner, _banner.Title, title, f => f.TitleText, (f, v) => f.TitleText = v);
        SwapText(_banner, _banner.Detail, message ?? detail, f => f.DetailText, (f, v) => f.DetailText = v);

        // The card.
        if (_card.Heading is not null)
        {
            _card.Heading.Text = empty ? labels.EmptyTitle : labels.Region(plan);
        }
        var cardDetail = status switch
        {
            _ when empty => string.Empty,
            TrialStatus.Warning => labels.CardWarning(Capitalize(endDay), date),
            TrialStatus.Expired => labels.CardExpired(date),
            TrialStatus.Upgraded => labels.CardUpgraded(plan),
            _ => labels.CardActive(daysText, date),
        };
        var lead = status switch
        {
            _ when empty => string.Empty,
            TrialStatus.Expired => labels.LostLead(workspace),
            TrialStatus.Upgraded => labels.KeptLead(workspace),
            _ => labels.KeepLead,
        };
        SwapText(_card, _card.Detail, cardDetail, f => f.DetailText, (f, v) => f.DetailText = v);
        SwapText(_card, _card.Lead, lead, f => f.LeadText, (f, v) => f.LeadText = v);
        SwapText(_card, _card.Note, message ?? string.Empty, f => f.NoteText, (f, v) => f.NoteText = v);
        if (_featuresDirty)
        {
            BuildFeatures(status);
        }

        foreach (var face in new[] { _banner, _card })
        {
            UpdateMark(face, status, days, empty);
            UpdateActions(face, source, status, phase, labels, empty);
        }

        // The pill.
        if (_pill is not null)
        {
            var pillLabel = status switch
            {
                _ when empty => labels.EmptyPill,
                TrialStatus.Warning => labels.PillWarning(endDay),
                TrialStatus.Expired => labels.PillExpired,
                TrialStatus.Upgraded => labels.PillUpgraded(plan),
                _ => labels.PillActive(daysText),
            };
            _pill.Label = pillLabel;
            _pill.Tone = status;
            _pill.Mark = status switch
            {
                TrialStatus.Expired => ArcIcons.Lock,
                TrialStatus.Upgraded => ArcIcons.Check,
                _ => null
            };
            AutomationProperties.SetName(_pill, (empty ? labels.EmptyTitle : labels.Region(plan) + ", " + pillLabel) + ". " + labels.OpenDetails);
        }

        UpdateRing(empty ? 0 : TrialBannerMath.RingValue(status, days, source.TrialDays), instant);
        _mirror?.Refresh(instant: false);
    }

    private static string Capitalize(string text) =>
        text.Length == 0 || char.IsUpper(text[0]) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Swaps a line of copy in place with one crossfade (old out 100ms, new in 160ms rising 2px); unchanged text is left alone.</summary>
    private static void SwapText(Face face, ArcContentSwap? swap, string text, Func<Face, string?> get, Action<Face, string?> set)
    {
        if (swap is null || get(face) == text)
        {
            return;
        }
        set(face, text);
        swap.Content = new TextBlock { Text = text };
    }

    /// <summary>The days in the ring, or the lock (expired) or check (upgraded) in their place.</summary>
    private void UpdateMark(Face face, TrialStatus status, int days, bool empty)
    {
        if (face.Mark is not { } mark)
        {
            return;
        }
        var key = status switch
        {
            _ when empty => "\u2013",
            TrialStatus.Expired => "lock",
            TrialStatus.Upgraded => "check",
            _ => days.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (face.MarkKey == key)
        {
            return;
        }
        face.MarkKey = key;
        if (status is TrialStatus.Expired or TrialStatus.Upgraded)
        {
            var glyph = new ArcGlyph
            {
                Data = status == TrialStatus.Expired ? ArcIcons.Lock : ArcIcons.Check,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            glyph.Bind(ArcGlyph.SizeProperty, glyph.GetResourceObservable(TrialBannerTokens.Key(
                ReferenceEquals(face, _card) ? TrialBannerTokenKind.CardGlyphSize : TrialBannerTokenKind.BannerGlyphSize)));
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty,
                status == TrialStatus.Expired ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Success);
            mark.Content = glyph;
        }
        else
        {
            mark.Content = new TextBlock
            {
                Text = key,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }

    private void UpdateActions(Face face, TrialBanner source, TrialStatus status, TrialBillingPhase phase, TrialBannerLabels labels,
        bool empty)
    {
        var upgraded = status == TrialStatus.Upgraded;
        if (face.Primary is { } primary)
        {
            primary.IsVisible = !empty;
            var text = upgraded ? labels.ManageBilling : phase == TrialBillingPhase.Failed ? labels.TryAgain : labels.AddPayment;
            if (!Equals(primary.Content, text))
            {
                primary.Content = text;
            }
            primary.Variant = upgraded ? ButtonVariant.Secondary : ButtonVariant.Primary;
            primary.IsLoading = phase == TrialBillingPhase.Busy;
            AutomationProperties.SetName(primary, text);
        }
        if (face.Compare is { } compare)
        {
            compare.Content = labels.ComparePlans;
            compare.IsVisible = !upgraded && !empty;
            AutomationProperties.SetName(compare, labels.ComparePlans);
        }
        if (face.Dismiss is { } dismiss)
        {
            dismiss.IsVisible = source.IsDismissible;
            AutomationProperties.SetName(dismiss, labels.Dismiss);
            ToolTip.SetTip(dismiss, labels.Dismiss);
        }
        if (face.Snooze is { } snooze)
        {
            var (days, _, _) = TrialBannerMath.Resolve(source.DaysLeft, source.EndsAt, source.TimeProvider);
            var options = TrialBannerMath.SnoozeOptions(status, days, source.WarnAt);
            snooze.IsVisible = !empty && source.IsSnoozable && options.Count > 0;
            AutomationProperties.SetName(snooze, labels.Snooze);
            ToolTip.SetTip(snooze, labels.Snooze);
            BuildMenu(face, options, days, source, labels);
        }
    }

    private void BuildMenu(Face face, IReadOnlyList<TrialSnoozeOption> options, int days, TrialBanner source, TrialBannerLabels labels)
    {
        if (face.Menu is not { } menu)
        {
            return;
        }
        var warning = labels.Date(TrialBannerMath.WarningStarts(days, source.WarnAt, source.TimeProvider));
        var key = string.Join(",", options) + "|" + warning + "|" + labels.GetHashCode();
        if (face.MenuKey == key)
        {
            return;
        }
        face.MenuKey = key;
        AutomationProperties.SetName(menu, labels.Snooze);
        menu.Items.Clear();
        foreach (var option in options)
        {
            menu.Items.Add(new MenuEntry
            {
                Header = option switch
                {
                    TrialSnoozeOption.ThreeDays => labels.SnoozeThreeDays,
                    TrialSnoozeOption.UntilWarning => labels.SnoozeUntilWarning(warning),
                    _ => labels.SnoozeTomorrow
                },
                Icon = new ArcGlyph { Data = option == TrialSnoozeOption.UntilWarning ? ArcIcons.Bell : ArcIcons.Clock, Size = 16 },
                Tag = option
            });
        }
    }

    /// <summary>The card's rows: a 14px mark, the feature and its muted detail. Once expired the marks turn to locks.</summary>
    private void BuildFeatures(TrialStatus status)
    {
        _featuresDirty = false;
        if (_card.Features is not { } panel)
        {
            return;
        }
        panel.Children.Clear();
        foreach (var feature in DisplayedFeatures)
        {
            var icon = new ArcGlyph
            {
                Data = status == TrialStatus.Expired ? ArcIcons.Lock : feature.Icon ?? ArcIcons.Check,
                VerticalAlignment = VerticalAlignment.Top
            };
            icon.Bind(ArcGlyph.SizeProperty, icon.GetResourceObservable(TrialBannerTokens.Key(TrialBannerTokenKind.FeatureIconSize)));
            icon.Bind(MarginProperty, icon.GetResourceObservable(TrialBannerTokens.Key(TrialBannerTokenKind.FeatureIconGap),
                gap => new Thickness(0, 2, gap is double d ? d : 8, 0)));
            ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty,
                status == TrialStatus.Upgraded ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextSecondary);
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var label = new Run(feature.Label ?? string.Empty);
            ArcTokens.Bind(label, TextElement.ForegroundProperty,
                status == TrialStatus.Expired ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
            text.Inlines!.Add(label);
            if (!string.IsNullOrEmpty(feature.Detail))
            {
                var detail = new Run("  " + feature.Detail);
                ArcTokens.Bind(detail, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                text.Inlines.Add(detail);
            }
            var row = new DockPanel();
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            row.Children.Add(text);
            panel.Children.Add(row);
        }
    }

    /// <summary>The ring arc springs to its new length with no bounce (0.24s); reduced motion and Off: at once.</summary>
    private void UpdateRing(double value, bool instant)
    {
        if (_ringValue is null)
        {
            _ringValue = new MotionValue<double>(this, value, MotionVectors.Double);
            _ringValue.Changed += ApplyRing;
            ApplyRing(value);
            return;
        }
        if (instant || !ArcMotion.IsFull(this))
        {
            _ringValue.Jump(value);
            ApplyRing(value);
            return;
        }
        if (!_ringValue.Target.Equals(value))
        {
            _ = _ringValue.AnimateTo(value, MotionSpec.Spring(s_ringSpring));
        }
    }

    private void ApplyRing(double value)
    {
        if (_banner.Ring is not null)
        {
            _banner.Ring.Value = value;
        }
        if (_card.Ring is not null)
        {
            _card.Ring.Value = value;
        }
        if (_pill is not null)
        {
            _pill.RingValue = value;
        }
    }
}
