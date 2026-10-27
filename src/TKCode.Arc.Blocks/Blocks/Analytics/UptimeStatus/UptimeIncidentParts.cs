using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An incident's header button (notes 3.5): the status mark, the title (14, 500) over the meta line (12, TextMuted, may wrap,
/// swapped with a short rise), the status at the right (a badge while it goes on, the word "Resolved" afterwards) and a
/// chevron that turns half way round when the timeline opens. A toggle button; its theme is in
/// <c>Themes/UptimeStatusTheme.axaml</c>.
/// </summary>
[TemplatePart(MarkPart, typeof(UptimeStatusMark))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(ArcContentSwap))]
[TemplatePart(BadgePart, typeof(UptimeBadge))]
[TemplatePart(ResolvedPart, typeof(TextBlock))]
internal sealed class UptimeIncidentHeader : ToggleButton
{
    internal const string MarkPart = "PART_Mark";
    internal const string TitlePart = "PART_Title";
    internal const string MetaPart = "PART_Meta";
    internal const string BadgePart = "PART_Badge";
    internal const string ResolvedPart = "PART_Resolved";

    private UptimeStatusMark? _mark;
    private TextBlock? _title;
    private ArcContentSwap? _meta;
    private UptimeBadge? _badge;
    private TextBlock? _resolved;
    private UptimeIncident? _incident;
    private string _metaText = string.Empty;
    private string _ongoing = string.Empty;
    private string _resolvedText = string.Empty;
    private bool _pending;

    protected override Type StyleKeyOverride => typeof(UptimeIncidentHeader);

    internal ArcContentSwap? Meta => _meta;

    internal UptimeBadge? Badge => _badge;

    internal TextBlock? ResolvedBlock => _resolved;

    internal string MetaText => _metaText;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<UptimeStatusMark>(MarkPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _meta = e.NameScope.Find<ArcContentSwap>(MetaPart);
        _badge = e.NameScope.Find<UptimeBadge>(BadgePart);
        _resolved = e.NameScope.Find<TextBlock>(ResolvedPart);
        if (_pending)
        {
            Apply(false);
        }
    }

    /// <summary>Shows an incident; the meta line swaps (short rise) when <paramref name="animate"/>.</summary>
    public void Show(UptimeIncident incident, string metaText, string ongoingText, string resolvedText, bool animate)
    {
        _incident = incident;
        _metaText = metaText;
        _ongoing = ongoingText;
        _resolvedText = resolvedText;
        _pending = true;
        Apply(animate);
    }

    private void Apply(bool animate)
    {
        if (_incident is null || _title is null)
        {
            return;
        }
        _pending = false;
        _mark!.Status = _incident.Severity;
        _title.Text = _incident.Title;
        if (_meta is not null)
        {
            if (!animate)
            {
                // The first content never animates; the mode is restored for the next change.
                var mode = _meta.Mode;
                _meta.Mode = ContentSwapMode.None;
                _meta.Content = _metaText;
                _meta.Mode = mode;
            }
            else
            {
                _meta.Content = _metaText;
            }
        }
        _badge!.Label = _ongoing;
        _badge.Icon = ArcIcons.Activity;
        _badge.Status = _incident.Severity == ServiceStatus.Outage ? ServiceStatus.Outage : ServiceStatus.Degraded;
        _badge.IsVisible = !_incident.IsResolved;
        _resolved!.Text = _resolvedText;
        _resolved.IsVisible = _incident.IsResolved;
    }
}

/// <summary>
/// One incident of the list: the header button over the timeline (notes 3.5 and 6.7). The timeline's height springs between 0
/// and its content (k 190, c 28) while its opacity fades over 240ms (Enter); several incidents can be open together.
/// </summary>
internal sealed class UptimeIncidentItem : StackPanel
{
    private readonly SpringSizeDecorator _slot;
    private readonly UptimeRule _rule;
    private readonly LiveLoop _loop;
    private AnalyticsTween _fade = new(0);
    private UptimeStatusLabels _labels = new();

    public UptimeIncidentItem(UptimeIncident incident)
    {
        Incident = incident;
        Header = new UptimeIncidentHeader();
        Timeline = new UptimeTimeline();
        _slot = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Smooth,
            IsExpanded = incident.IsExpanded,
            CrossClipOverflow = 0,
            Child = Timeline
        };
        _fade.Jump(incident.IsExpanded ? 1 : 0);
        _slot.Opacity = incident.IsExpanded ? 1 : 0;
        _rule = new UptimeRule { IsVisible = false };
        Children.Add(_rule);
        Children.Add(Header);
        Children.Add(_slot);
        Header.IsChecked = incident.IsExpanded;
        Header.IsCheckedChanged += (_, _) => Incident.IsExpanded = Header.IsChecked == true;
        _loop = new LiveLoop(this, dt =>
        {
            var running = _fade.Advance(dt);
            _slot.Opacity = Math.Clamp(_fade.Value, 0, 1);
            return running;
        });
    }

    /// <summary>The incident.</summary>
    public UptimeIncident Incident { get; }

    /// <summary>A 1px rule above the item (every incident but the first).</summary>
    public bool HasRule
    {
        get => _rule.IsVisible;
        set => _rule.IsVisible = value;
    }

    /// <summary>The header button.</summary>
    public UptimeIncidentHeader Header { get; }

    /// <summary>The timeline.</summary>
    public UptimeTimeline Timeline { get; }

    /// <summary>The height decorator around the timeline (tests).</summary>
    internal SpringSizeDecorator Slot => _slot;

    /// <summary>Opens or closes the timeline.</summary>
    public void SetExpanded(bool expanded)
    {
        if (Header.IsChecked != expanded)
        {
            Header.IsChecked = expanded;
        }
        _slot.IsExpanded = expanded;
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
        {
            _fade.Jump(expanded ? 1 : 0);
            _slot.Opacity = expanded ? 1 : 0;
        }
        else
        {
            // Opacity runs on the 240ms enter curve in both directions (Reduced keeps it; only the height jumps).
            _fade.Start(expanded ? 1 : 0, ArcMotionPresets.Standard, AnalyticsEasings.Enter);
            _loop.Start();
        }
        AutomationProperties.SetName(Header, string.Format(AnalyticsFormat.DefaultCulture, _labels.IncidentButtonFormat,
            Incident.Title, expanded ? _labels.ExpandedText : _labels.CollapsedText));
    }

    /// <summary>Updates texts and the timeline from the incident.</summary>
    public void Refresh(UptimeStatusLabels labels, string meta, Func<DateTimeOffset, string> time, bool animate)
    {
        _labels = labels;
        Header.Show(Incident, meta, labels.OngoingText, labels.ResolvedText, animate);
        Timeline.SetIncident(Incident, labels, time, animate);
        AutomationProperties.SetName(Header, string.Format(AnalyticsFormat.DefaultCulture, labels.IncidentButtonFormat,
            Incident.Title, Incident.IsExpanded ? labels.ExpandedText : labels.CollapsedText));
        AutomationProperties.SetHelpText(Header, meta);
    }
}

/// <summary>
/// The incident list (notes 3.5 and 6.6): newest first, the second item on with a 1px BorderSubtle rule above it. An incident
/// that arrives grows from zero height on a spring (k 190, c 28 ≈ Smooth, about 700ms) while it fades in over 200ms; one that
/// leaves fades out in 130ms while its height springs to zero and is then removed.
/// </summary>
/// <remarks>Why code-driven: arrival and departure are per-item height and opacity changes owned by the list, not by a style.</remarks>
internal sealed class UptimeIncidentList : Panel
{
    private readonly List<Slot> _slots = [];
    private readonly LiveLoop _loop;

    public UptimeIncidentList()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <summary>The items in order (leaving ones included until removed).</summary>
    public IReadOnlyList<UptimeIncidentItem> Items => _slots.Select(s => s.Item).ToArray();

    /// <summary>The items that stay (not leaving).</summary>
    public IReadOnlyList<UptimeIncidentItem> LiveItems => _slots.Where(s => !s.Leaving).Select(s => s.Item).ToArray();

    /// <summary>The finder of an item for an incident.</summary>
    public UptimeIncidentItem? Find(UptimeIncident incident) =>
        _slots.FirstOrDefault(s => !s.Leaving && ReferenceEquals(s.Item.Incident, incident))?.Item;

    /// <summary>Opacity of an item's slot (tests).</summary>
    internal double OpacityOf(UptimeIncidentItem item) => _slots.First(s => ReferenceEquals(s.Item, item)).Fade.Value;

    /// <summary>
    /// Makes the list show exactly <paramref name="incidents"/> in order. New ones are created with <paramref name="create"/>
    /// and enter; missing ones leave.
    /// </summary>
    public void Sync(IReadOnlyList<UptimeIncident> incidents, Func<UptimeIncident, UptimeIncidentItem> create, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        animate &= this.IsAttachedToVisualTree() && level != ArcMotionLevel.Off;
        foreach (var slot in _slots.ToList())
        {
            if (!slot.Leaving && !incidents.Any(i => ReferenceEquals(i, slot.Item.Incident)))
            {
                Leave(slot, animate, level);
            }
        }
        var order = new List<Slot>();
        foreach (var incident in incidents)
        {
            var slot = _slots.FirstOrDefault(s => !s.Leaving && ReferenceEquals(s.Item.Incident, incident));
            if (slot is null)
            {
                slot = Enter(incident, create, animate, level);
            }
            order.Add(slot);
        }
        // Leaving slots keep their place relative to the live ones before them.
        var merged = new List<Slot>();
        var live = 0;
        foreach (var slot in _slots.ToList())
        {
            if (slot.Leaving)
            {
                merged.Add(slot);
            }
        }
        foreach (var slot in order)
        {
            merged.Insert(Math.Min(live, merged.Count), slot);
            live++;
        }
        _slots.Clear();
        _slots.AddRange(merged);
        var first = true;
        foreach (var slot in _slots.Where(s => !s.Leaving))
        {
            slot.Item.HasRule = !first;
            first = false;
        }
        InvalidateMeasure();
        _loop.Start();
    }

    private Slot Enter(UptimeIncident incident, Func<UptimeIncident, UptimeIncidentItem> create, bool animate, ArcMotionLevel level)
    {
        var item = create(incident);
        var decorator = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Smooth,
            IsExpanded = !animate || level != ArcMotionLevel.Full,
            CrossClipOverflow = 12,
            Child = item
        };
        var slot = new Slot(item, decorator);
        if (animate)
        {
            slot.Fade.Jump(0);
            slot.Fade.Start(1, ArcMotionPresets.Fast, AnalyticsEasings.Enter);
            decorator.Opacity = 0;
            if (level == ArcMotionLevel.Full)
            {
                // Collapsed first, then expanded once it is in the tree so the height springs from zero.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => decorator.IsExpanded = true, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
        _slots.Add(slot);
        Children.Add(decorator);
        return slot;
    }

    private void Leave(Slot slot, bool animate, ArcMotionLevel level)
    {
        slot.Leaving = true;
        if (!animate)
        {
            Remove(slot);
            return;
        }
        slot.Fade.Start(0, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
        slot.Decorator.IsHitTestVisible = false;
        if (level == ArcMotionLevel.Full)
        {
            slot.Decorator.IsExpanded = false;
            slot.Wait = 0.75;
        }
        else
        {
            slot.Wait = 0.16;
        }
    }

    private void Remove(Slot slot)
    {
        _slots.Remove(slot);
        Children.Remove(slot.Decorator);
        InvalidateMeasure();
    }

    private bool Advance(double dt)
    {
        var moving = false;
        foreach (var slot in _slots.ToList())
        {
            moving |= slot.Fade.Advance(dt);
            slot.Decorator.Opacity = Math.Clamp(slot.Fade.Value, 0, 1);
            if (slot.Leaving)
            {
                slot.Wait -= dt;
                if (slot.Wait <= 0 && !slot.Fade.IsRunning)
                {
                    Remove(slot);
                }
                else
                {
                    moving = true;
                }
            }
        }
        return moving;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var slot in _slots)
        {
            slot.Decorator.Measure(availableSize.WithHeight(double.PositiveInfinity));
            height += slot.Decorator.DesiredSize.Height;
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var slot in _slots)
        {
            var h = slot.Decorator.DesiredSize.Height;
            slot.Decorator.Arrange(new Rect(0, y, finalSize.Width, h));
            y += h;
        }
        return finalSize;
    }

    private sealed class Slot(UptimeIncidentItem item, SpringSizeDecorator decorator)
    {
        public readonly UptimeIncidentItem Item = item;
        public readonly SpringSizeDecorator Decorator = decorator;
        public AnalyticsTween Fade = new(1);
        public bool Leaving;
        public double Wait;
    }
}
