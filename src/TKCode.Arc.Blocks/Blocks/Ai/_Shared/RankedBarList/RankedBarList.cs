using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Where a <see cref="RankedBarList"/> replay is.</summary>
internal enum RankedBarReplayPhase
{
    /// <summary>No replay: the rows show <see cref="RankedBarList.Items"/>.</summary>
    None,

    /// <summary>The old standings show; the hold starts once the list is in a window.</summary>
    Pending,

    /// <summary>The old standings show for <see cref="RankedBarList.ReplayHold"/>.</summary>
    Holding,

    /// <summary>The rows are moving to the new standings.</summary>
    Moving
}

/// <summary>
/// A ranked list of bars (the arena's standings): a fixed rank column and rows of mark, name, a 56 × 4 win-rate bar in the
/// item's series color and the value as a rolling whole percentage, one row per <see cref="RowHeight"/>, at most
/// <see cref="Limit"/> rows. Setting <see cref="Items"/> shows them at once. <see cref="Replay"/> shows the change since
/// an earlier list: the old order and values hold for <see cref="ReplayHold"/>, then every row slides from its old rank to
/// its new one on a critically damped spring (<see cref="ArcSpring.Settle"/>), the bars fill from the old value to the new
/// one, the numbers roll, rows that drop out fade away and new ones fade in growing from zero; the rank column never
/// moves. Reduced motion holds, then puts the rows in place at once and changes the bars over 120ms; Off shows the end
/// at once. Changing <see cref="Items"/> during a replay finishes the replay first.
/// Screen readers get a list of items named "Rank 1, Lumen 3 Pro, win rate 70%" (<see cref="RankedBarItem.AutomationName"/>,
/// or one built from <see cref="RankFormat"/> and <see cref="WinRateSuffix"/>); the rank column, marks and bars are not read.
/// </summary>
[TemplatePart(RanksPart, typeof(RankedBarRanks))]
[TemplatePart(RowsPart, typeof(RankedBarPanel))]
internal class RankedBarList : TemplatedControl
{
    public const string RanksPart = "PART_Ranks";
    public const string RowsPart = "PART_Rows";

    /// <summary>How long a replay shows the old standings before the rows move (Arc: about 325ms after opening).</summary>
    public static readonly TimeSpan ReplayHold = TimeSpan.FromMilliseconds(325);

    /// <summary>The bars' spring: critically damped, ω ≈ 15.5 (settles in about 500ms).</summary>
    public static readonly SpringSpec BarSpring = SpringSpec.FromVisual(0.338, 0);

    /// <summary>How long a leaving row fades out at full motion.</summary>
    public static readonly TimeSpan LeaveDuration = TimeSpan.FromMilliseconds(100);

    private static readonly Easing s_linear = new LinearEasing();

    #region Properties

    /// <summary>The rows, best first; setting the list shows it at once.</summary>
    public static readonly StyledProperty<IReadOnlyList<RankedBarItem>?> ItemsProperty =
        AvaloniaProperty.Register<RankedBarList, IReadOnlyList<RankedBarItem>?>(nameof(Items));

    /// <summary>The most rows shown (5); 0 or less shows every item.</summary>
    public static readonly StyledProperty<int> LimitProperty =
        AvaloniaProperty.Register<RankedBarList, int>(nameof(Limit), 5);

    /// <summary>The height of a row and of a rank (36).</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<RankedBarList, double>(nameof(RowHeight), 36);

    /// <summary>The rank part of a row's accessible name when the item has none ("Rank {0},"); null leaves it out.</summary>
    public static readonly StyledProperty<string?> RankFormatProperty =
        AvaloniaProperty.Register<RankedBarList, string?>(nameof(RankFormat));

    /// <summary>The words between the name and the percentage in a built accessible name (", win rate").</summary>
    public static readonly StyledProperty<string?> WinRateSuffixProperty =
        AvaloniaProperty.Register<RankedBarList, string?>(nameof(WinRateSuffix));

    /// <summary>Series colors from the theme (tones 1 to 4).</summary>
    internal static readonly StyledProperty<IBrush?> Series1Property = AvaloniaProperty.Register<RankedBarList, IBrush?>("Series1");

    /// <inheritdoc cref="Series1Property"/>
    internal static readonly StyledProperty<IBrush?> Series2Property = AvaloniaProperty.Register<RankedBarList, IBrush?>("Series2");

    /// <inheritdoc cref="Series1Property"/>
    internal static readonly StyledProperty<IBrush?> Series3Property = AvaloniaProperty.Register<RankedBarList, IBrush?>("Series3");

    /// <inheritdoc cref="Series1Property"/>
    internal static readonly StyledProperty<IBrush?> Series4Property = AvaloniaProperty.Register<RankedBarList, IBrush?>("Series4");

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<RankedBarItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="LimitProperty"/>
    public int Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <inheritdoc cref="RankFormatProperty"/>
    public string? RankFormat { get => GetValue(RankFormatProperty); set => SetValue(RankFormatProperty, value); }

    /// <inheritdoc cref="WinRateSuffixProperty"/>
    public string? WinRateSuffix { get => GetValue(WinRateSuffixProperty); set => SetValue(WinRateSuffixProperty, value); }

    /// <inheritdoc cref="Series1Property"/>
    internal IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    /// <inheritdoc cref="Series1Property"/>
    internal IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    /// <inheritdoc cref="Series1Property"/>
    internal IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    /// <inheritdoc cref="Series1Property"/>
    internal IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    #endregion

    private readonly Dictionary<string, RankedBarRow> _byId = new(StringComparer.Ordinal);
    private readonly List<RankedBarRow> _leaving = new();
    private readonly MotionValue<double> _hold;
    private List<RankedBarRow> _shown = new();
    private RankedBarPanel? _panel;
    private RankedBarRanks? _ranks;
    private IReadOnlyList<RankedBarItem>? _replayFrom;
    private IReadOnlyList<RankedBarItem>? _replayTarget;
    private RankedBarReplayPhase _phase;
    private int _generation;

    static RankedBarList()
    {
        FocusableProperty.OverrideDefaultValue<RankedBarList>(false);
    }

    public RankedBarList()
    {
        // Code-driven: the hold is a step of the replay timeline (the reorder starts when it ends), frame-aligned with the
        // springs that follow; a ControlTheme has no timeline.
        _hold = new MotionValue<double>(this, 0, MotionVectors.Double);
        _hold.Changed += OnHoldChanged;
    }

    /// <summary>The shown rows, top first.</summary>
    internal IReadOnlyList<RankedBarRow> ShownRows => _shown;

    /// <summary>Rows that dropped out and are fading away.</summary>
    internal IReadOnlyList<RankedBarRow> LeavingRows => _leaving;

    /// <summary>Every row the panel holds (shown, entering and leaving).</summary>
    internal IEnumerable<RankedBarRow> AllRows => _panel?.Children.OfType<RankedBarRow>() ?? [];

    /// <summary>Where a replay is.</summary>
    internal RankedBarReplayPhase Phase => _phase;

    /// <summary>True from <see cref="Replay"/> until every row rests at its new rank.</summary>
    internal bool IsReplaying => _phase != RankedBarReplayPhase.None;

    /// <summary>The rank column (template part).</summary>
    internal RankedBarRanks? Ranks => _ranks;

    /// <summary>The rows' panel (template part).</summary>
    internal RankedBarPanel? RowsPanel => _panel;

    /// <summary>
    /// Shows the change from <paramref name="from"/> to the current <see cref="Items"/>: <paramref name="from"/> in its
    /// order and values first, then, after <see cref="ReplayHold"/>, the rows move to the current standings. An empty
    /// <paramref name="from"/> replays the current rows growing from zero (the first look after the first vote). Set
    /// <see cref="Items"/> before calling it. A list that is not in a window yet starts the hold when it is shown.
    /// </summary>
    public void Replay(IReadOnlyList<RankedBarItem> from)
    {
        ArgumentNullException.ThrowIfNull(from);
        FinishReplay();
        var target = Visible(Items);
        if (target.Count == 0)
        {
            ShowAtOnce();
            return;
        }
        _generation++;
        _replayTarget = target;
        _replayFrom = from.Count == 0 ? target.Select(item => item with { Value = 0 }).ToArray() : Visible(from);
        _phase = RankedBarReplayPhase.Pending;
        if (_panel is null)
        {
            // Built when the template arrives.
            return;
        }
        ShowStart();
        if (TopLevel.GetTopLevel(this) is not null)
        {
            StartHold();
        }
    }

    /// <summary>Ends a replay now: the rows jump to the current standings.</summary>
    internal void FinishReplay()
    {
        switch (_phase)
        {
            case RankedBarReplayPhase.Pending:
            case RankedBarReplayPhase.Holding:
                _hold.Stop();
                if (_panel is not null && _replayTarget is not null)
                {
                    Flip(instant: true);
                }
                break;
            case RankedBarReplayPhase.Moving:
                foreach (var row in _shown)
                {
                    row.Finish();
                }
                break;
        }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            RemoveRow(_leaving[i]);
        }
        _replayFrom = null;
        _replayTarget = null;
        _phase = RankedBarReplayPhase.None;
    }

    #region Showing

    /// <summary>The first <see cref="Limit"/> items of <paramref name="items"/>, without repeated ids.</summary>
    private IReadOnlyList<RankedBarItem> Visible(IReadOnlyList<RankedBarItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }
        var limit = Limit > 0 ? Limit : int.MaxValue;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var visible = new List<RankedBarItem>(Math.Min(items.Count, limit));
        foreach (var item in items)
        {
            if (visible.Count >= limit)
            {
                break;
            }
            if (seen.Add(item.Id))
            {
                visible.Add(item);
            }
        }
        return visible;
    }

    /// <summary>Shows <see cref="Items"/> at once (no replay).</summary>
    private void ShowAtOnce()
    {
        _generation++;
        _hold.Stop();
        _phase = RankedBarReplayPhase.None;
        _replayFrom = null;
        _replayTarget = null;
        if (_panel is null)
        {
            return;
        }
        ShowRows(Visible(Items));
    }

    /// <summary>Shows a replay's starting point and readies the rows that will enter, invisible, at their new ranks.</summary>
    private void ShowStart()
    {
        if (_replayFrom is null || _replayTarget is null)
        {
            return;
        }
        ShowRows(_replayFrom);
        for (var i = 0; i < _replayTarget.Count; i++)
        {
            var item = _replayTarget[i];
            if (_byId.ContainsKey(item.Id))
            {
                continue;
            }
            // Made now so its number is on screen (and can roll) when the hold ends.
            var row = Add(item with { Value = 0 });
            row.State = RankedBarRowState.Entering;
            row.Slot = i;
            row.Rest(0, 0);
            row.JumpOpacity(0);
        }
    }

    /// <summary>Shows <paramref name="items"/> in order at once, reusing the rows of the same ids.</summary>
    private void ShowRows(IReadOnlyList<RankedBarItem> items)
    {
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            RemoveRow(_leaving[i]);
        }
        var keep = new HashSet<string>(items.Select(item => item.Id), StringComparer.Ordinal);
        foreach (var stale in _byId.Values.Where(row => !keep.Contains(row.Item.Id)).ToArray())
        {
            RemoveRow(stale);
        }
        var shown = new List<RankedBarRow>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!_byId.TryGetValue(item.Id, out var row))
            {
                row = Add(item);
            }
            row.Item = item;
            row.FillBrush = FillFor(item.Tone);
            row.State = RankedBarRowState.Shown;
            row.Slot = i;
            row.ZIndex = 0;
            row.Rest(item.Ratio, item.Percent);
            shown.Add(row);
        }
        _shown = shown;
        Relabel();
    }

    /// <summary>
    /// The reorder at the end of the hold: every row of the new standings starts from where it is drawn and springs to its
    /// new rank, its bar fills to the new value and its number rolls; rows that dropped out fade and are removed. With
    /// <paramref name="instant"/> (or Off) everything jumps.
    /// </summary>
    private void Flip(bool instant)
    {
        var target = _replayTarget;
        _replayTarget = null;
        _replayFrom = null;
        if (target is null || _panel is null)
        {
            _phase = RankedBarReplayPhase.None;
            return;
        }
        var level = instant ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(this);
        var full = level == ArcMotionLevel.Full;
        var reduced = level == ArcMotionLevel.Reduced;
        var move = instant ? MotionSpec.Instant : MotionSpec.Spring(ArcSpring.Settle);
        var fill = full ? MotionSpec.Spring(BarSpring)
            : reduced ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Instant;
        var enter = full ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : reduced ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Instant;
        var leave = full ? MotionSpec.Tween(LeaveDuration, ArcEasings.Standard)
            : reduced ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Instant;
        var height = RowHeight;
        var motions = new List<Task<bool>>();
        var targetIds = new HashSet<string>(target.Select(item => item.Id), StringComparer.Ordinal);
        foreach (var row in _shown.ToArray())
        {
            if (targetIds.Contains(row.Item.Id))
            {
                continue;
            }
            row.State = RankedBarRowState.Leaving;
            row.ZIndex = 0;
            _byId.Remove(row.Item.Id);
            _leaving.Add(row);
            var fade = row.FadeTo(0, leave);
            motions.Add(fade);
            _ = RemoveWhenFadedAsync(row, fade);
        }
        var shown = new List<RankedBarRow>(target.Count);
        for (var i = 0; i < target.Count; i++)
        {
            var item = target[i];
            if (!_byId.TryGetValue(item.Id, out var row))
            {
                row = Add(item with { Value = 0 });
                row.Rest(0, 0);
                row.JumpOpacity(0);
                row.State = RankedBarRowState.Entering;
            }
            var entering = row.State == RankedBarRowState.Entering;
            var drawnAt = row.Slot * height + row.Offset;
            row.Item = item;
            row.FillBrush = FillFor(item.Tone);
            row.State = RankedBarRowState.Shown;
            row.Slot = i;
            // Rows are opaque while they cross: climbers paint over the rows they pass (the climb is the news), and every
            // row of the new standings over the ones that are leaving.
            row.ZIndex = drawnAt > i * height + 0.01 ? 2 : 1;
            if (entering)
            {
                motions.Add(row.FadeTo(1, enter));
            }
            else if (Math.Abs(drawnAt - i * height) > 0.01)
            {
                motions.Add(row.MoveHome(drawnAt - i * height, move));
            }
            motions.Add(row.FillTo(item.Ratio, fill));
            row.RollTo(item.Percent, instant: !full && !reduced);
            shown.Add(row);
        }
        _shown = shown;
        Relabel();
        if (instant)
        {
            for (var i = _leaving.Count - 1; i >= 0; i--)
            {
                RemoveRow(_leaving[i]);
            }
            _phase = RankedBarReplayPhase.None;
            return;
        }
        _phase = RankedBarReplayPhase.Moving;
        _ = EndWhenSettledAsync(motions, _generation);
    }

    private void StartHold()
    {
        if (_phase != RankedBarReplayPhase.Pending)
        {
            return;
        }
        _phase = RankedBarReplayPhase.Holding;
        _hold.Jump(0);
        // Off jumps to the end at once (and so reorders right away); Reduced and Full hold.
        _ = _hold.AnimateTo(1, MotionSpec.Tween(ReplayHold, s_linear));
    }

    private void OnHoldChanged(double progress)
    {
        if (progress >= 1 && _phase == RankedBarReplayPhase.Holding)
        {
            Flip(instant: false);
        }
    }

    private async Task EndWhenSettledAsync(List<Task<bool>> motions, int generation)
    {
        // One at a time on the UI thread: Task.WhenAll would finish on a pool thread and come back late.
        foreach (var motion in motions)
        {
            await motion;
        }
        if (generation == _generation && _phase == RankedBarReplayPhase.Moving)
        {
            _phase = RankedBarReplayPhase.None;
        }
    }

    private async Task RemoveWhenFadedAsync(RankedBarRow row, Task<bool> fade)
    {
        await fade;
        if (row.State == RankedBarRowState.Leaving)
        {
            RemoveRow(row);
        }
    }

    private RankedBarRow Add(RankedBarItem item)
    {
        var row = new RankedBarRow(item) { Background = Background, FillBrush = FillFor(item.Tone) };
        _byId[item.Id] = row;
        _panel?.Children.Add(row);
        return row;
    }

    private void RemoveRow(RankedBarRow row)
    {
        _leaving.Remove(row);
        if (_byId.TryGetValue(row.Item.Id, out var known) && ReferenceEquals(known, row))
        {
            _byId.Remove(row.Item.Id);
        }
        _shown.Remove(row);
        _panel?.Children.Remove(row);
    }

    /// <summary>Updates the rank column and every shown row's accessible name, and lays the rows out again.</summary>
    private void Relabel()
    {
        if (_ranks is not null)
        {
            _ranks.Count = _shown.Count;
        }
        for (var i = 0; i < _shown.Count; i++)
        {
            AutomationProperties.SetName(_shown[i], AccessibleName(i + 1, _shown[i].Item));
        }
        _panel?.InvalidateMeasure();
        _panel?.InvalidateArrange();
    }

    /// <summary>The item's accessible name, or "Rank 1, Name, win rate 70%" built from the list's texts.</summary>
    internal string AccessibleName(int rank, RankedBarItem item)
    {
        if (!string.IsNullOrEmpty(item.AutomationName))
        {
            return item.AutomationName;
        }
        var percent = item.Percent.ToString(CultureInfo.CurrentCulture) + "%";
        var name = item.Name + WinRateSuffix;
        return string.IsNullOrEmpty(RankFormat)
            ? name + " " + percent
            : string.Format(CultureInfo.CurrentCulture, RankFormat, rank) + " " + name + " " + percent;
    }

    /// <summary>The bar fill of a tone (1 to 4) in the current theme.</summary>
    internal IBrush FillFor(int tone)
    {
        var brush = tone switch
        {
            1 => GetValue(Series1Property),
            2 => GetValue(Series2Property),
            3 => GetValue(Series3Property),
            _ => GetValue(Series4Property)
        };
        return brush ?? new ImmutableSolidColorBrush(PromptTone.SeriesColor(this, tone));
    }

    #endregion

    #region Lifetime

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == LimitProperty)
        {
            FinishReplay();
            ShowAtOnce();
        }
        else if (change.Property == RankFormatProperty || change.Property == WinRateSuffixProperty)
        {
            Relabel();
        }
        else if (change.Property == Series1Property || change.Property == Series2Property ||
                 change.Property == Series3Property || change.Property == Series4Property)
        {
            foreach (var row in AllRows)
            {
                row.FillBrush = FillFor(row.Item.Tone);
            }
        }
        else if (change.Property == BackgroundProperty)
        {
            foreach (var row in AllRows)
            {
                row.Background = Background;
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _byId.Clear();
        _leaving.Clear();
        _shown = new List<RankedBarRow>();
        _panel = e.NameScope.Find<RankedBarPanel>(RowsPart);
        _ranks = e.NameScope.Find<RankedBarRanks>(RanksPart);
        if (_phase == RankedBarReplayPhase.Pending)
        {
            ShowStart();
            if (TopLevel.GetTopLevel(this) is not null)
            {
                StartHold();
            }
        }
        else
        {
            ShowAtOnce();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_phase == RankedBarReplayPhase.Pending && _panel is not null)
        {
            StartHold();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A list that is closed mid-replay comes back showing the standings, not a stale frame.
        if (_phase is RankedBarReplayPhase.Holding or RankedBarReplayPhase.Moving)
        {
            FinishReplay();
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    /// <summary>A list whose items are the shown rows, top first; the rank column and leaving rows are not read.</summary>
    private sealed class ListPeer(RankedBarList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var list = (RankedBarList)Owner;
            var peers = new List<AutomationPeer>(list._shown.Count);
            foreach (var row in list._shown)
            {
                peers.Add(CreatePeerForElement(row));
            }
            return peers;
        }
    }
}
