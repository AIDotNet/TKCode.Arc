// Ported from Arc's free newsletter-signup block (MIT, Copyright (c) 2026 Elia Kuratli): IssueStack and its variants.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The stack of issues: all of them share one cell, so the stack is as tall as the tallest issue and nothing shifts when
/// they trade places. The front issue sits flat; each one behind steps up by <see cref="Step"/> and shrinks by
/// <see cref="Shrink"/> so its top edge shows. When <see cref="IsDelivered"/> turns on, the upcoming issue, addressed to
/// <see cref="Recipient"/>, rises from below onto the front, the others step back and the third drops away behind; turning
/// it off puts everything back. Position and scale ride one no-overshoot spring (Smooth), opacity a 120 ms tween so two
/// issues never read through each other. Under reduced motion issues take their places at once and only fade (160 ms).
/// </summary>
/// <remarks>
/// Code-driven: the poses are retargeted while they move (velocity is kept) and issues are added and removed with a
/// presence animation, which a ControlTheme cannot express.
/// </remarks>
internal sealed class NewsletterIssueStack : Control
{
    public static readonly StyledProperty<NewsletterPublication?> PublicationProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, NewsletterPublication?>(nameof(Publication));

    public static readonly StyledProperty<bool> IsDeliveredProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, bool>(nameof(IsDelivered));

    public static readonly StyledProperty<string?> RecipientProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, string?>(nameof(Recipient));

    /// <summary>The tray's stack: as tall as the tray, with the issues cropped by a 64 px fade at the bottom.</summary>
    public static readonly StyledProperty<bool> FillHeightProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, bool>(nameof(FillHeight));

    public static readonly StyledProperty<bool> IsTrayProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, bool>(nameof(IsTray));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<bool> IsTinyProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, bool>(nameof(IsTiny));

    public static readonly StyledProperty<double> TopInsetProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, double>(nameof(TopInset), 24);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, double>(nameof(Step), 12);

    public static readonly StyledProperty<double> ShrinkProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, double>(nameof(Shrink), 0.045);

    public static readonly StyledProperty<string> IssueFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, string>(nameof(IssueFormat), "Issue {0}");

    public static readonly StyledProperty<string> MinutesFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, string>(nameof(MinutesFormat), "{0} min");

    public static readonly StyledProperty<string> RecipientFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, string>(nameof(RecipientFormat), "To {0}");

    /// <summary>Screen reader name of the front issue: {0} is the publication name, {1} the issue number.</summary>
    public static readonly StyledProperty<string> IssueNameFormatProperty =
        AvaloniaProperty.Register<NewsletterIssueStack, string>(nameof(IssueNameFormat), "{0}, issue {1}");

    private const double FadeFar = 3;

    private static readonly SpringSpec Settle = SpringSpec.FromVisual(0.4, 0);

    private readonly NewsletterIssueCard _sizer = new() { IsSizer = true, Opacity = 0, IsHitTestVisible = false };
    private readonly List<Slot> _slots = new();
    private NewsletterPublication? _observed;
    private bool _populated;

    static NewsletterIssueStack()
    {
        AffectsMeasure<NewsletterIssueStack>(FillHeightProperty, TopInsetProperty);
        AffectsArrange<NewsletterIssueStack>(StepProperty);
        FocusableProperty.OverrideDefaultValue<NewsletterIssueStack>(false);
    }

    public NewsletterIssueStack()
    {
        AutomationProperties.SetAccessibilityView(_sizer, AccessibilityView.Raw);
        LogicalChildren.Add(_sizer);
        VisualChildren.Add(_sizer);
    }

    public NewsletterPublication? Publication { get => GetValue(PublicationProperty); set => SetValue(PublicationProperty, value); }

    public bool IsDelivered { get => GetValue(IsDeliveredProperty); set => SetValue(IsDeliveredProperty, value); }

    public string? Recipient { get => GetValue(RecipientProperty); set => SetValue(RecipientProperty, value); }

    public bool FillHeight { get => GetValue(FillHeightProperty); set => SetValue(FillHeightProperty, value); }

    public bool IsTray { get => GetValue(IsTrayProperty); set => SetValue(IsTrayProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public bool IsTiny { get => GetValue(IsTinyProperty); set => SetValue(IsTinyProperty, value); }

    public double TopInset { get => GetValue(TopInsetProperty); set => SetValue(TopInsetProperty, value); }

    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    public double Shrink { get => GetValue(ShrinkProperty); set => SetValue(ShrinkProperty, value); }

    public string IssueFormat { get => GetValue(IssueFormatProperty); set => SetValue(IssueFormatProperty, value); }

    public string MinutesFormat { get => GetValue(MinutesFormatProperty); set => SetValue(MinutesFormatProperty, value); }

    public string RecipientFormat { get => GetValue(RecipientFormatProperty); set => SetValue(RecipientFormatProperty, value); }

    public string IssueNameFormat { get => GetValue(IssueNameFormatProperty); set => SetValue(IssueNameFormatProperty, value); }

    /// <summary>The issues that belong on the stack, front first (the ones still dropping away are not listed).</summary>
    public IReadOnlyList<NewsletterIssueCard> Cards =>
        _slots.Where(s => !s.Exiting).OrderBy(s => s.Depth).Select(s => s.Card).ToList();

    /// <summary>The unseen copy that sizes the stack (tests read it).</summary>
    public NewsletterIssueCard Sizer => _sizer;

    /// <summary>Issues that are leaving and still on screen.</summary>
    public int ExitingCount => _slots.Count(s => s.Exiting);

    /// <summary>The pose (offset up in px, scale) a card is heading to, by issue number; null when it is not on the stack.</summary>
    public (double Y, double Scale, double Opacity)? TargetOf(int number)
    {
        var slot = _slots.FirstOrDefault(s => s.Number == number);
        return slot is null ? null : (slot.Pose.Target.X, slot.Pose.Target.Y, slot.Fade.Target);
    }

    /// <summary>The pose a card is drawn at right now, by issue number; null when it is not on the stack.</summary>
    public (double Y, double Scale, double Opacity)? CurrentOf(int number)
    {
        var slot = _slots.FirstOrDefault(s => s.Number == number);
        return slot is null ? null : (slot.Pose.Value.X, slot.Pose.Value.Y, slot.Fade.Value);
    }

    private sealed class Slot
    {
        public required NewsletterIssueCard Card { get; init; }
        public required int Number { get; init; }
        public required ScaleTransform Scale { get; init; }
        public required TranslateTransform Translate { get; init; }
        public required MotionValue<Vector> Pose { get; init; }
        public required MotionValue<double> Fade { get; init; }
        public int Depth { get; set; }
        public bool Upcoming { get; set; }
        public bool Exiting { get; set; }
        public int Version { get; set; }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Publication);
        Refresh(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        foreach (var slot in _slots.ToArray())
        {
            if (slot.Exiting)
            {
                RemoveSlot(slot);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PublicationProperty)
        {
            Observe(Publication);
            Rebuild();
        }
        else if (property == IsDeliveredProperty || property == RecipientProperty)
        {
            Refresh(animate: property == IsDeliveredProperty && _populated);
        }
        else if (property == IsTrayProperty || property == IsNarrowProperty || property == IsTinyProperty ||
                 property == IssueFormatProperty || property == MinutesFormatProperty || property == RecipientFormatProperty ||
                 property == IssueNameFormatProperty)
        {
            ApplyCardSettings();
        }
        else if (property == StepProperty || property == ShrinkProperty)
        {
            RetargetAll();
        }
        else if (property == FillHeightProperty)
        {
            UpdateMask();
        }
    }

    private void Observe(NewsletterPublication? publication)
    {
        if (ReferenceEquals(_observed, publication))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.Changed -= OnPublicationChanged;
        }
        _observed = publication;
        if (_observed is not null)
        {
            _observed.Changed += OnPublicationChanged;
        }
    }

    private void OnPublicationChanged(object? sender, EventArgs e) => Rebuild();

    /// <summary>Drops every issue and places the current ones at once (a new publication does not animate).</summary>
    private void Rebuild()
    {
        foreach (var slot in _slots.ToArray())
        {
            RemoveSlot(slot);
        }
        _populated = false;
        Refresh(animate: false);
    }

    private Vector Placed(int depth) => new(-depth * Step, 1 - depth * Shrink);

    private Vector Away(bool upcoming) => upcoming
        // The upcoming issue arrives from below, where the form is; older ones slide in from, and drop away to, behind.
        ? new Vector(56, 1)
        : new Vector(-FadeFar * Step, 1 - FadeFar * Shrink);

    private void ApplyCardSettings()
    {
        var all = _slots.Select(s => s.Card).Append(_sizer);
        foreach (var card in all)
        {
            card.IsTray = IsTray;
            card.IsNarrow = IsNarrow;
            card.IsTiny = IsTiny;
            card.IssueFormat = IssueFormat;
            card.MinutesFormat = MinutesFormat;
            card.RecipientFormat = RecipientFormat;
        }
        UpdateNames();
    }

    private void UpdateNames()
    {
        var name = Publication?.Name ?? string.Empty;
        foreach (var slot in _slots)
        {
            var front = !slot.Exiting && slot.Depth == 0;
            AutomationProperties.SetAccessibilityView(slot.Card, front ? AccessibilityView.Content : AccessibilityView.Raw);
            AutomationProperties.SetName(slot.Card, front && slot.Card.Issue is { } issue
                ? string.Format(CultureInfo.CurrentCulture, IssueNameFormat, name, issue.Number)
                : string.Empty);
        }
    }

    private List<(NewsletterIssue Issue, bool Upcoming)> Targets()
    {
        var list = new List<(NewsletterIssue, bool)>(3);
        var publication = Publication;
        if (publication is null)
        {
            return list;
        }
        if (IsDelivered && publication.Upcoming is { } upcoming)
        {
            list.Add((upcoming, true));
        }
        foreach (var issue in publication.Recent)
        {
            if (list.Count >= 3)
            {
                break;
            }
            if (list.Any(t => t.Item1.Number == issue.Number))
            {
                continue;
            }
            list.Add((issue, false));
        }
        return list;
    }

    private void Refresh(bool animate)
    {
        var publication = Publication;
        _sizer.PublicationName = publication?.Name;
        _sizer.Issue = publication?.Upcoming;
        // An unseen copy of the upcoming issue, addressed to somebody, so the stack is as tall as it will ever be.
        _sizer.Recipient = string.IsNullOrEmpty(Recipient) ? " " : Recipient;
        PseudoClassesSizer();
        if (publication is null)
        {
            foreach (var slot in _slots.ToArray())
            {
                RemoveSlot(slot);
            }
            return;
        }
        var targets = Targets();
        var shouldAnimate = animate && _populated;
        foreach (var slot in _slots.ToArray())
        {
            if (!slot.Exiting && !targets.Any(t => t.Issue.Number == slot.Number))
            {
                StartExit(slot, shouldAnimate);
            }
        }
        for (var depth = 0; depth < targets.Count; depth++)
        {
            var (issue, upcoming) = targets[depth];
            var slot = _slots.FirstOrDefault(s => s.Number == issue.Number);
            var fresh = slot is null;
            slot ??= CreateSlot(issue, upcoming, shouldAnimate);
            slot.Exiting = false;
            slot.Version++;
            slot.Depth = depth;
            slot.Upcoming = upcoming;
            slot.Card.Issue = issue;
            slot.Card.PublicationName = publication.Name;
            slot.Card.Recipient = upcoming ? Recipient : null;
            slot.Card.Depth = depth;
            slot.Card.ZIndex = 3 - depth;
            if (fresh && !shouldAnimate)
            {
                slot.Pose.Jump(Placed(depth));
                slot.Fade.Jump(1);
            }
            else
            {
                _ = slot.Pose.AnimateTo(Placed(depth), MotionSpec.Spring(Settle));
                _ = slot.Fade.AnimateTo(1, FadeSpec());
            }
        }
        _populated = true;
        UpdateNames();
        InvalidateArrange();
        InvalidateMeasure();
    }

    private void PseudoClassesSizer()
    {
        // The sizer reserves the recipient line, even with only a space in it.
        _sizer.IsTray = IsTray;
        _sizer.IsNarrow = IsNarrow;
        _sizer.IsTiny = IsTiny;
        _sizer.IssueFormat = IssueFormat;
        _sizer.MinutesFormat = MinutesFormat;
        _sizer.RecipientFormat = RecipientFormat;
    }

    private MotionSpec FadeSpec() => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced
        ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
        : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);

    private Slot CreateSlot(NewsletterIssue issue, bool upcoming, bool animate)
    {
        var card = new NewsletterIssueCard
        {
            IsTray = IsTray,
            IsNarrow = IsNarrow,
            IsTiny = IsTiny,
            IssueFormat = IssueFormat,
            MinutesFormat = MinutesFormat,
            RecipientFormat = RecipientFormat,
            RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative)
        };
        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform();
        card.RenderTransform = new TransformGroup { Children = { scale, translate } };
        var start = animate ? Away(upcoming) : new Vector(0, 1);
        var pose = new MotionValue<Vector>(this, start, MotionVectors.Vector);
        var fade = new MotionValue<double>(this, animate ? 0 : 1, MotionVectors.Double);
        var slot = new Slot { Card = card, Number = issue.Number, Scale = scale, Translate = translate, Pose = pose, Fade = fade, Upcoming = upcoming };
        pose.Changed += v =>
        {
            translate.Y = v.X;
            scale.ScaleX = v.Y;
            scale.ScaleY = v.Y;
        };
        fade.Changed += v => card.Opacity = v;
        translate.Y = start.X;
        scale.ScaleX = start.Y;
        scale.ScaleY = start.Y;
        card.Opacity = animate ? 0 : 1;
        _slots.Add(slot);
        LogicalChildren.Add(card);
        VisualChildren.Add(card);
        return slot;
    }

    private async void StartExit(Slot slot, bool animate)
    {
        slot.Exiting = true;
        var version = ++slot.Version;
        AutomationProperties.SetAccessibilityView(slot.Card, AccessibilityView.Raw);
        slot.Card.ZIndex = 0;
        if (!animate)
        {
            RemoveSlot(slot);
            return;
        }
        _ = slot.Pose.AnimateTo(Away(slot.Upcoming), MotionSpec.Spring(Settle));
        var reached = await slot.Fade.AnimateTo(0, FadeSpec());
        if (reached && slot.Exiting && slot.Version == version && _slots.Contains(slot))
        {
            RemoveSlot(slot);
        }
    }

    private void RemoveSlot(Slot slot)
    {
        slot.Pose.Dispose();
        slot.Fade.Dispose();
        _slots.Remove(slot);
        LogicalChildren.Remove(slot.Card);
        VisualChildren.Remove(slot.Card);
        InvalidateMeasure();
    }

    private void RetargetAll()
    {
        foreach (var slot in _slots)
        {
            if (!slot.Exiting)
            {
                _ = slot.Pose.AnimateTo(Placed(slot.Depth), MotionSpec.Spring(Settle));
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 440 : availableSize.Width;
        var cell = new Size(width, double.PositiveInfinity);
        _sizer.Measure(cell);
        var tallest = _sizer.DesiredSize.Height;
        foreach (var slot in _slots)
        {
            slot.Card.Measure(cell);
            tallest = Math.Max(tallest, slot.Card.DesiredSize.Height);
        }
        _cardHeight = tallest;
        var height = TopInset + tallest;
        if (FillHeight && !double.IsInfinity(availableSize.Height))
        {
            height = availableSize.Height;
        }
        return new Size(width, height);
    }

    private double _cardHeight;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(0, TopInset, finalSize.Width, _cardHeight);
        _sizer.Arrange(rect);
        foreach (var slot in _slots)
        {
            slot.Card.Arrange(rect);
        }
        return finalSize;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateMask();
    }

    private double _maskHeight;

    /// <summary>The tray's crop: the issues fade out over the last 64 px of the stack.</summary>
    private void UpdateMask()
    {
        if (!FillHeight || Bounds.Height <= 0)
        {
            OpacityMask = null;
            _maskHeight = 0;
            return;
        }
        if (Math.Abs(_maskHeight - Bounds.Height) < 0.01 && OpacityMask is not null)
        {
            return;
        }
        _maskHeight = Bounds.Height;
        var height = Bounds.Height;
        // Absolute coordinates: the issues overflow the stack below, and a relative gradient would stretch over that overflow.
        // A new immutable brush each time the height changes: editing the stops of a live brush does not reach the compositor.
        OpacityMask = new ImmutableLinearGradientBrush(
            new[]
            {
                new ImmutableGradientStop(0, Colors.Black),
                new ImmutableGradientStop(1, Colors.Transparent)
            },
            startPoint: new RelativePoint(0, Math.Max(0, height - 64), RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, height, RelativeUnit.Absolute));
    }
}
