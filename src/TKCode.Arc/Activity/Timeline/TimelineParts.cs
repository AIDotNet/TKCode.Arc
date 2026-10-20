using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A flattened row of a <see cref="Timeline"/>: a day label or an event, keyed for reuse across updates.</summary>
internal abstract class TimelineRowModel
{
    protected TimelineRowModel(string key) => Key = key;

    /// <summary>"day:yyyy-MM-dd" or "ev:{id}".</summary>
    public string Key { get; }

    /// <summary>Added after the first show: grows in once (Arc's <c>fresh</c>).</summary>
    public bool Fresh { get; set; }

    /// <summary>Stopwatch timestamp until which a newly realized fresh row still plays its entrance.</summary>
    public long EnterUntil { get; set; }

    /// <summary>Removed from the data and playing its exit; dropped from the rows when it finishes.</summary>
    public bool Leaving { get; set; }

    /// <summary>The day this row belongs to (in the timeline's time zone).</summary>
    public DateOnly Day { get; set; }
}

/// <summary>A day label row (Arc's sticky <c>.day</c> heading).</summary>
internal sealed class TimelineDayModel(DateOnly day) : TimelineRowModel($"day:{day:yyyy-MM-dd}")
{
    /// <summary>Updates on this day.</summary>
    public int Count { get; set; }

    /// <summary>The newest update on this day (formats the heading for older days).</summary>
    public DateTimeOffset Newest { get; set; }

    /// <summary>First day of the feed (its section starts the list).</summary>
    public bool IsFirst { get; set; }

    /// <summary>Index of this label in the rows.</summary>
    public int Index { get; set; }

    /// <summary>Index of the next day's label (or the row count after the last day).</summary>
    public int NextIndex { get; set; }
}

/// <summary>An event row.</summary>
internal sealed class TimelineEventModel(TimelineEvent item) : TimelineRowModel($"ev:{item.Id}")
{
    public TimelineEvent Item { get; set; } = item;

    /// <summary>Last update of its day: no connecting line, and the day's bottom gap follows it.</summary>
    public bool IsLastOfDay { get; set; }

    /// <summary>The marker and line already revealed (once per event, kept across container reuse).</summary>
    public bool Revealed { get; set; }
}

/// <summary>
/// The virtualized list behind a <see cref="Timeline"/>: a <see cref="VirtualizingStackPanel"/> over the flattened rows,
/// with one recycle pool for day labels and one for events. Containers are prepared by the owning timeline.
/// </summary>
internal sealed class TimelineList : ItemsControl
{
    private static readonly object s_dayKey = new();
    private static readonly object s_eventKey = new();

    protected override Type StyleKeyOverride => typeof(TimelineList);

    public Timeline? Owner { get; set; }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = item is TimelineDayModel ? s_dayKey : s_eventKey;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        ReferenceEquals(recycleKey, s_dayKey) ? new TimelineDayHeader() : new TimelineRow();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        // The base would set the row model as DataContext, which details (app controls) must not inherit.
        Owner?.PrepareRow(container, item as TimelineRowModel);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        Owner?.ClearRow(container);
    }

    /// <summary>
    /// The timeline owns the arrow keys (between expandable rows only). ItemsControl's own directional navigation is
    /// skipped: it would realize and scroll to rows that cannot take focus.
    /// </summary>
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        Owner?.HandleListKey(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelineListPeer(this);

    /// <summary>Arc's <c>&lt;ol&gt;</c> of updates inside the region.</summary>
    private sealed class TimelineListPeer(TimelineList owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>
/// Height that follows its child times a progress on a spring (Arc's <c>height: 0 → auto</c> with
/// <c>overflow: clip</c>): fresh rows and days grow in, details open and close. Clips only while it is not fully open,
/// keeping a small clip margin across the moving axis.
/// </summary>
internal sealed class TimelineGrowHost : Decorator
{
    private MotionValue<double>? _progress;
    private bool _open = true;
    private readonly RectangleGeometry _clip = new();

    /// <summary>Overflow kept visible left and right while clipping (Arc <c>overflow-clip-margin: 4px</c>).</summary>
    public double ClipMargin { get; set; } = 4;

    /// <summary>Whether the content is (or is going) open.</summary>
    public bool IsOpen => _open;

    /// <summary>0 closed, 1 open.</summary>
    public double Progress => _progress?.Value ?? (_open ? 1 : 0);

    /// <summary>True while the height is moving.</summary>
    public bool IsMoving => _progress?.IsAnimating == true;

    /// <summary>Raised when an animated close finished.</summary>
    public event EventHandler? Closed;

    /// <summary>Opens or closes; without <paramref name="spec"/> it jumps.</summary>
    public void Set(bool open, MotionSpec? spec, double? from = null)
    {
        _open = open;
        _progress ??= CreateValue(open ? 1 : 0);
        if (Child is { } child && open)
        {
            child.IsVisible = true;
        }
        if (from is { } start)
        {
            _progress.Jump(start);
        }
        if (spec is null)
        {
            _progress.Jump(open ? 1 : 0);
            Settle();
            InvalidateMeasure();
            return;
        }
        _ = Run(open, spec);
        InvalidateMeasure();
    }

    private async Task Run(bool open, MotionSpec spec)
    {
        var done = await _progress!.AnimateTo(open ? 1 : 0, spec);
        if (done && _open == open)
        {
            Settle();
            if (!open)
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void Settle()
    {
        if (Child is { } child)
        {
            child.IsVisible = _open || Progress > 0.001;
        }
        UpdateClip();
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        var progress = Math.Max(0, Progress);
        return new Size(desired.Width, progress >= 1 ? desired.Height : desired.Height * progress);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, child.DesiredSize.Height)));
        }
        UpdateClip(finalSize);
        return finalSize;
    }

    private void UpdateClip(Size? size = null)
    {
        var bounds = size ?? Bounds.Size;
        if (Progress >= 1 && !IsMoving)
        {
            Clip = null;
            return;
        }
        _clip.Rect = new Rect(-ClipMargin, 0, bounds.Width + 2 * ClipMargin, bounds.Height);
        Clip = _clip;
        InvalidateVisual();
    }
}

/// <summary>
/// The clickable part of an event row (Arc's <c>.trigger</c>): a button when the event has a detail, plain text
/// otherwise (not focusable, no hover). Exposes expand / collapse to assistive technology.
/// </summary>
internal sealed class TimelineTrigger : AvaloniaButton
{
    public const string InteractivePseudoClass = ":interactive";

    private bool _isInteractive;

    protected override Type StyleKeyOverride => typeof(TimelineTrigger);

    public TimelineRow? Row { get; set; }

    /// <summary>Whether the row can expand (it is a button only then).</summary>
    public bool IsInteractive
    {
        get => _isInteractive;
        set
        {
            _isInteractive = value;
            Focusable = value;
            IsTabStop = value;
            PseudoClasses.Set(InteractivePseudoClass, value);
        }
    }

    protected override void OnClick()
    {
        if (_isInteractive)
        {
            base.OnClick();
        }
    }

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
    {
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelineTriggerPeer(this);

    private sealed class TimelineTriggerPeer(TimelineTrigger owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private TimelineTrigger Trigger => (TimelineTrigger)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Trigger.IsInteractive ? AutomationControlType.Button : AutomationControlType.Text;

        public ExpandCollapseState ExpandCollapseState => !Trigger.IsInteractive
            ? ExpandCollapseState.LeafNode
            : Trigger.Row?.IsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => Trigger.Row?.RequestExpanded(true);

        public void Collapse() => Trigger.Row?.RequestExpanded(false);
    }
}

/// <summary>
/// A day label (Arc's <c>.day</c>): the day ("Today", "Yesterday", "Monday, October 19") and the number of updates,
/// which rolls when it changes. Used in the list and as the pinned overlay that stands in for <c>position: sticky</c>.
/// </summary>
internal sealed class TimelineDayHeader : TemplatedControl
{
    public const string GrowPart = "PART_Grow";
    public const string LabelPart = "PART_Label";
    public const string CountPart = "PART_Count";
    public const string CountWordPart = "PART_CountWord";

    protected override Type StyleKeyOverride => typeof(TimelineDayHeader);

    private TimelineGrowHost? _grow;
    private TextBlock? _label;
    private Primitives.RollingDigits? _count;
    private TextBlock? _countWord;
    private string _labelText = string.Empty;
    private int _countValue;
    private bool _pendingSilent = true;

    public TimelineDayModel? Model { get; set; }

    public Timeline? Owner { get; set; }

    private MotionValue<double>? _fade;
    private ArcMotionLevel? _pendingEnter;

    /// <summary>A new day opens from nothing and pushes the older days down (Arc: smooth spring, opacity standard).</summary>
    public void PlayEnter(ArcMotionLevel level)
    {
        if (_grow is null)
        {
            // A new container: the entrance starts once its template exists.
            _pendingEnter = level;
            return;
        }
        _pendingEnter = null;
        _fade ??= CreateFade();
        _fade.Jump(0);
        Opacity = 0;
        if (level == ArcMotionLevel.Full)
        {
            _grow.Set(true, MotionSpec.Spring(ArcSpring.Smooth), from: 0);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else
        {
            _grow.Set(true, null);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(TimelineMotion.ReducedFade, ArcEasings.Enter));
        }
    }

    /// <summary>Shows the label at rest (a reused or initial header).</summary>
    public void ShowAtRest()
    {
        _pendingEnter = null;
        _grow?.Set(true, null);
        _fade?.Jump(1);
        Opacity = 1;
    }

    /// <summary>The day's last update was removed: the label closes (Arc: exit duration, standard ease).</summary>
    public void PlayExit(ArcMotionLevel level)
    {
        if (_grow is null)
        {
            return;
        }
        _fade ??= CreateFade();
        var spec = MotionSpec.Tween(level == ArcMotionLevel.Full ? ArcMotionPresets.Exit : ArcMotionPresets.Instant, ArcEasings.Standard);
        _grow.Set(false, spec);
        _ = _fade.AnimateTo(0, spec);
    }

    private MotionValue<double> CreateFade()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    private void OnGrowClosed(object? sender, EventArgs e)
    {
        if (Model is { Leaving: true } model)
        {
            Owner?.RemoveLeaving(model);
        }
    }

    /// <summary>The pinned copy above the list (decorative: the list's own label is the heading).</summary>
    public bool IsOverlay { get; set; }

    public string LabelText => _labelText;

    public int CountValue => _countValue;

    public TimelineGrowHost? Grow => _grow;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_grow is not null)
        {
            _grow.Closed -= OnGrowClosed;
        }
        _grow = e.NameScope.Find<TimelineGrowHost>(GrowPart);
        if (_grow is not null)
        {
            _grow.Closed += OnGrowClosed;
        }
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        _count = e.NameScope.Find<Primitives.RollingDigits>(CountPart);
        _countWord = e.NameScope.Find<TextBlock>(CountWordPart);
        Apply(silent: true);
        if (_pendingEnter is { } level)
        {
            PlayEnter(level);
        }
    }

    /// <summary>Shows a label and count; <paramref name="silent"/> skips the count roll (a reused or new header).</summary>
    public void Set(string label, int count, bool silent, int headingLevel)
    {
        _labelText = label;
        _countValue = count;
        if (IsOverlay)
        {
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        }
        else
        {
            AutomationProperties.SetHeadingLevel(this, headingLevel);
            AutomationProperties.SetName(this, $"{label}, {count} {(count == 1 ? "update" : "updates")}");
        }
        Apply(silent || _pendingSilent);
    }

    private void Apply(bool silent)
    {
        if (_label is null)
        {
            _pendingSilent = true;
            return;
        }
        _pendingSilent = false;
        _label.Text = _labelText;
        _countWord!.Text = _countValue == 1 ? "update" : "updates";
        if (_count is { } digits && !digits.Value.Equals(_countValue))
        {
            if (silent)
            {
                TimelineMotion.Silently(digits, () => digits.Value = _countValue);
            }
            else
            {
                digits.Value = _countValue;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelineDayHeaderPeer(this);

    private sealed class TimelineDayHeaderPeer(TimelineDayHeader owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Header;

        protected override bool IsContentElementCore() => !((TimelineDayHeader)Owner).IsOverlay;

        protected override bool IsControlElementCore() => !((TimelineDayHeader)Owner).IsOverlay;
    }
}

/// <summary>Motion helpers of the timeline.</summary>
internal static class TimelineMotion
{
    /// <summary>Marker pop (Arc: morph with visualDuration .36, bounce .32).</summary>
    public static readonly SpringSpec MarkerPop = SpringSpec.FromVisual(0.36, 0.32);

    /// <summary>Line draw (Arc: smooth with visualDuration .34).</summary>
    public static readonly SpringSpec LineDraw = SpringSpec.FromVisual(0.34, 0);

    /// <summary>Detail close (Arc: smooth with visualDuration .28).</summary>
    public static readonly SpringSpec DetailClose = SpringSpec.FromVisual(0.28, 0);

    /// <summary>Row exit height (Arc: smooth with visualDuration .3).</summary>
    public static readonly SpringSpec RowExit = SpringSpec.FromVisual(0.3, 0);

    /// <summary>Reduced motion's short fade (.15s).</summary>
    public static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Runs <paramref name="change"/> with motion off for <paramref name="control"/> (a reused row shows its new item at
    /// once instead of rolling or blurring from the previous one).
    /// </summary>
    public static void Silently(Control control, Action change)
    {
        var had = control.IsSet(ArcMotion.LevelProperty);
        var previous = control.GetValue(ArcMotion.LevelProperty);
        control.SetValue(ArcMotion.LevelProperty, ArcMotionLevel.Off);
        try
        {
            change();
        }
        finally
        {
            if (had)
            {
                control.SetValue(ArcMotion.LevelProperty, previous);
            }
            else
            {
                control.ClearValue(ArcMotion.LevelProperty);
            }
        }
    }
}
