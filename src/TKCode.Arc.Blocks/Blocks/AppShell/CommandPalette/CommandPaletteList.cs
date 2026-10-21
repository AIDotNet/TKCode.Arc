// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli): the grouped result list
// of command-palette.tsx with its layout animation (AnimatePresence popLayout + layout="position").

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The result list of a <see cref="CommandPalette"/>: group headings and rows stacked top to bottom (groups
/// <see cref="GroupGap"/> apart). Rows and headings are kept per command and per group and reused across filtering.
/// <para>
/// Filtering animates like Arc's <c>layout="position"</c> list (FLIP): a row that stays keeps its old on-screen
/// position as a render offset and springs (smooth) into its new slot, keeping its velocity when a new keystroke
/// retargets it; a row that leaves pops out of the flow at the place it was drawn and fades (it follows its group
/// heading if that stays); a new row fades in. Code-driven (<see cref="MotionValue{T}"/>) because the motion depends
/// on where each row was drawn before the list changed, which a ControlTheme transition cannot know. Every frame only
/// writes a translate and an opacity, so the list never relayouts while it glides.
/// </para>
/// </summary>
internal sealed class CommandPaletteList : Panel
{
    public static readonly StyledProperty<double> GroupGapProperty =
        AvaloniaProperty.Register<CommandPaletteList, double>(nameof(GroupGap), 6);

    private static readonly MotionSpec s_enter = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
    private static readonly MotionSpec s_leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
    private static readonly MotionSpec s_glide = MotionSpec.Spring(ArcSpring.Smooth);

    private readonly Dictionary<object, Entry> _rows = new();
    private readonly Dictionary<string, Entry> _headings = new(StringComparer.Ordinal);
    private List<Entry> _live = new();
    private List<Entry> _next = new();
    private readonly List<Entry> _exiting = new();
    private readonly HashSet<object> _claimed = new();
    private int _generation;
    private bool _pending;
    private bool _glide;
    private bool _fadeIn;
    private bool _narrow;
    private bool _narrowest;

    static CommandPaletteList()
    {
        AffectsMeasure<CommandPaletteList>(GroupGapProperty);
    }

    public double GroupGap { get => GetValue(GroupGapProperty); set => SetValue(GroupGapProperty, value); }

    /// <summary>Raised at the end of every arrange pass (row positions are final): the palette places its highlight.</summary>
    public event Action? Arranged;

    /// <summary>Raised when automation selects a row.</summary>
    public Action<CommandPaletteRow>? RowAutomationSelected { get; set; }

    /// <summary>Number of rows (not headings) in the list.</summary>
    public int RowCount
    {
        get
        {
            var count = 0;
            foreach (var entry in _live)
            {
                if (!entry.IsHeading)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>Entries that are fading out.</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>The live entries (headings and rows) in list order.</summary>
    public IReadOnlyList<Entry> LiveEntries => _live;

    internal sealed class Entry
    {
        public Entry(Control visual, object key, bool isHeading)
        {
            Visual = visual;
            Key = key;
            IsHeading = isHeading;
            visual.RenderTransform = Translate;
            Offset = new MotionValue<double>(visual, 0, MotionVectors.Double);
            Fade = new MotionValue<double>(visual, 1, MotionVectors.Double);
        }

        public Control Visual { get; }

        public object Key { get; }

        public bool IsHeading { get; }

        public CommandPaletteRow? Row => Visual as CommandPaletteRow;

        public TranslateTransform Translate { get; } = new();

        /// <summary>The FLIP offset: drawn position minus layout position.</summary>
        public MotionValue<double> Offset { get; }

        public MotionValue<double> Fade { get; }

        public string Group { get; set; } = string.Empty;

        /// <summary>Layout top in list coordinates (NaN before the first measure).</summary>
        public double Y { get; set; } = double.NaN;

        public double Height => Visual.DesiredSize.Height;

        public bool IsLive { get; set; }

        public bool IsExiting { get; set; }

        public bool InTree { get; set; }

        public int Mark { get; set; }

        public EntryState State { get; set; }

        public double PreviousVisualY { get; set; } = double.NaN;

        public double ExitY { get; set; }

        public Entry? Anchor { get; set; }

        public double AnchorStart { get; set; }

        public List<Entry>? Followers { get; set; }

        public int ExitToken { get; set; }

        /// <summary>Where the entry is drawn right now (layout top plus its offset).</summary>
        public double VisualY => IsExiting ? ExitY + Translate.Y : Y + Offset.Value;
    }

    internal enum EntryState
    {
        None,
        Entering,
        Staying,
        Revived
    }

    /// <summary>
    /// Shows <paramref name="groups"/>. <paramref name="glide"/>: staying rows spring from where they are drawn and
    /// leaving rows fade out in place; otherwise staying rows snap and leaving rows go at once (so nothing overlaps the
    /// new rows). <paramref name="fadeIn"/>: new rows fade in.
    /// </summary>
    public void Update(IReadOnlyList<CommandPaletteGroup> groups, bool glide, bool fadeIn)
    {
        var generation = ++_generation;
        foreach (var entry in _live)
        {
            entry.PreviousVisualY = double.IsNaN(entry.Y) ? double.NaN : entry.VisualY;
        }
        foreach (var entry in _exiting)
        {
            entry.PreviousVisualY = entry.VisualY;
        }
        _next.Clear();
        _claimed.Clear();
        foreach (var group in groups)
        {
            var heading = GetHeading(group.Name);
            heading.Group = group.Name;
            heading.Mark = generation;
            _next.Add(heading);
            foreach (var item in group.Items)
            {
                // A repeated id falls back to the item itself, so every command keeps a row of its own.
                var key = CommandPaletteFilter.KeyOf(item);
                if (!_claimed.Add(key))
                {
                    key = item;
                    _claimed.Add(key);
                }
                var row = GetRow(key);
                row.Row!.SetItem(item);
                row.Group = group.Name;
                row.Mark = generation;
                _next.Add(row);
            }
        }
        foreach (var entry in _live)
        {
            if (entry.Mark != generation)
            {
                BeginExit(entry, glide, generation);
            }
        }
        foreach (var entry in _next)
        {
            if (entry.IsExiting)
            {
                CancelExit(entry);
                entry.State = EntryState.Revived;
            }
            else if (!entry.IsLive)
            {
                entry.State = EntryState.Entering;
            }
            else
            {
                entry.State = EntryState.Staying;
            }
            entry.IsLive = true;
            if (!entry.InTree)
            {
                entry.InTree = true;
                Children.Add(entry.Visual);
            }
        }
        (_live, _next) = (_next, _live);
        _next.Clear();
        _pending = true;
        _glide = glide;
        _fadeIn = fadeIn;
        InvalidateMeasure();
    }

    /// <summary>Removes every row and heading at once (no exit).</summary>
    public void Clear()
    {
        foreach (var entry in _live)
        {
            Remove(entry);
        }
        foreach (var entry in _exiting.ToArray())
        {
            Remove(entry);
        }
        _live.Clear();
        _exiting.Clear();
        _pending = false;
        InvalidateMeasure();
    }

    /// <summary>Drops cached rows whose commands left the source (and headings of groups that are gone).</summary>
    public void Prune(HashSet<object> keepRows, HashSet<string> keepGroups)
    {
        List<object>? dropRows = null;
        foreach (var (key, entry) in _rows)
        {
            if (!entry.IsLive && !entry.IsExiting && !keepRows.Contains(key))
            {
                (dropRows ??= new()).Add(key);
            }
        }
        if (dropRows is not null)
        {
            foreach (var key in dropRows)
            {
                _rows[key].Row?.ReleaseIcon();
                _rows.Remove(key);
            }
        }
        List<string>? dropGroups = null;
        foreach (var (name, entry) in _headings)
        {
            if (!entry.IsLive && !entry.IsExiting && !keepGroups.Contains(name))
            {
                (dropGroups ??= new()).Add(name);
            }
        }
        if (dropGroups is not null)
        {
            foreach (var name in dropGroups)
            {
                _headings.Remove(name);
            }
        }
    }

    /// <summary>The live row showing <paramref name="item"/>, or null.</summary>
    public Entry? FindRow(CommandPaletteItem item)
    {
        if (_rows.TryGetValue(CommandPaletteFilter.KeyOf(item), out var entry) && entry.IsLive && ReferenceEquals(entry.Row?.Item, item))
        {
            return entry;
        }
        return _rows.TryGetValue(item, out entry) && entry.IsLive ? entry : null;
    }

    /// <summary>The live row drawn under <paramref name="point"/> (list coordinates), or null.</summary>
    public Entry? HitRow(Point point)
    {
        if (point.X < 0 || point.X > Bounds.Width)
        {
            return null;
        }
        foreach (var entry in _live)
        {
            if (entry.IsHeading || double.IsNaN(entry.Y))
            {
                continue;
            }
            var top = entry.VisualY;
            if (point.Y >= top && point.Y < top + entry.Visual.Bounds.Height)
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>Applies the narrow (≤ 480) and narrowest (≤ 340) row layouts.</summary>
    public void SetWidthClass(bool narrow, bool narrowest)
    {
        if (_narrow == narrow && _narrowest == narrowest)
        {
            return;
        }
        _narrow = narrow;
        _narrowest = narrowest;
        foreach (var entry in _rows.Values)
        {
            entry.Row?.SetWidthClass(narrow, narrowest);
        }
    }

    private Entry GetHeading(string name)
    {
        if (!_headings.TryGetValue(name, out var entry))
        {
            var heading = new CommandPaletteGroupHeading();
            heading.SetGroup(name);
            entry = Track(new Entry(heading, name, isHeading: true));
            _headings.Add(name, entry);
        }
        return entry;
    }

    private Entry GetRow(object key)
    {
        if (!_rows.TryGetValue(key, out var entry))
        {
            var row = new CommandPaletteRow { AutomationSelected = r => RowAutomationSelected?.Invoke(r) };
            row.SetWidthClass(_narrow, _narrowest);
            entry = Track(new Entry(row, key, isHeading: false));
            _rows.Add(key, entry);
        }
        return entry;
    }

    private Entry Track(Entry entry)
    {
        entry.Offset.Changed += _ => ApplyTranslate(entry);
        entry.Fade.Changed += opacity => entry.Visual.Opacity = opacity;
        return entry;
    }

    private static void ApplyTranslate(Entry entry)
    {
        if (entry.IsExiting)
        {
            // A leaving row rides along with its group heading, like Arc's rows popped out inside a positioned group.
            entry.Translate.Y = entry.Anchor is { } anchor && !double.IsNaN(anchor.Y)
                ? anchor.Y + anchor.Offset.Value - entry.AnchorStart
                : 0;
        }
        else
        {
            entry.Translate.Y = entry.Offset.Value;
        }
        if (entry.Followers is { Count: > 0 } followers)
        {
            foreach (var follower in followers)
            {
                ApplyTranslate(follower);
            }
        }
    }

    private void BeginExit(Entry entry, bool glide, int generation)
    {
        entry.IsLive = false;
        entry.State = EntryState.None;
        if (!glide || double.IsNaN(entry.Y) || !IsLoaded)
        {
            Remove(entry);
            return;
        }
        entry.ExitY = entry.PreviousVisualY;
        entry.IsExiting = true;
        if (!entry.IsHeading && _headings.TryGetValue(entry.Group, out var heading) && heading.Mark == generation)
        {
            entry.Anchor = heading;
            entry.AnchorStart = heading.PreviousVisualY;
            (heading.Followers ??= new()).Add(entry);
        }
        entry.Offset.Jump(0);
        _exiting.Add(entry);
        var token = ++entry.ExitToken;
        _ = FinishExit(entry, token, entry.Fade.AnimateTo(0, s_leave));
    }

    private async Task FinishExit(Entry entry, int token, Task<bool> fade)
    {
        await fade;
        if (entry.ExitToken == token && entry.IsExiting)
        {
            Remove(entry);
            InvalidateArrange();
        }
    }

    private void CancelExit(Entry entry)
    {
        entry.ExitToken++;
        Detach(entry);
        entry.IsExiting = false;
        _exiting.Remove(entry);
        // Back in the flow: its layout offset continues from where it is drawn.
        entry.Offset.Jump(0);
    }

    private static void Detach(Entry entry)
    {
        entry.Anchor?.Followers?.Remove(entry);
        entry.Anchor = null;
    }

    private void Remove(Entry entry)
    {
        entry.ExitToken++;
        Detach(entry);
        entry.IsExiting = false;
        entry.IsLive = false;
        entry.State = EntryState.None;
        _exiting.Remove(entry);
        entry.Offset.Jump(0);
        entry.Fade.Jump(1);
        entry.Translate.Y = 0;
        if (entry.InTree)
        {
            entry.InTree = false;
            Children.Remove(entry.Visual);
        }
        if (entry.Row is { } row)
        {
            row.SetActive(false);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = new Size(availableSize.Width, double.PositiveInfinity);
        var gap = GroupGap;
        double y = 0;
        double width = 0;
        var first = true;
        foreach (var entry in _live)
        {
            if (entry.IsHeading)
            {
                if (!first)
                {
                    y += gap;
                }
                first = false;
            }
            entry.Visual.Measure(child);
            entry.Y = y;
            y += entry.Visual.DesiredSize.Height;
            width = Math.Max(width, entry.Visual.DesiredSize.Width);
        }
        foreach (var entry in _exiting)
        {
            entry.Visual.Measure(child);
        }
        return new Size(width, y);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var entry in _live)
        {
            entry.Visual.Arrange(new Rect(0, entry.Y, finalSize.Width, entry.Visual.DesiredSize.Height));
        }
        foreach (var entry in _exiting)
        {
            entry.Visual.Arrange(new Rect(0, entry.ExitY, finalSize.Width, entry.Visual.DesiredSize.Height));
        }
        if (_pending)
        {
            _pending = false;
            ApplyPending();
        }
        foreach (var entry in _live)
        {
            if (entry.Followers is { Count: > 0 })
            {
                ApplyTranslate(entry);
            }
        }
        Arranged?.Invoke();
        return finalSize;
    }

    private void ApplyPending()
    {
        foreach (var entry in _live)
        {
            switch (entry.State)
            {
                case EntryState.Entering:
                    entry.Offset.Jump(0);
                    if (_fadeIn && IsLoaded)
                    {
                        entry.Fade.Jump(0);
                        _ = entry.Fade.AnimateTo(1, s_enter);
                    }
                    else
                    {
                        entry.Fade.Jump(1);
                    }
                    break;
                case EntryState.Revived:
                    Glide(entry);
                    _ = entry.Fade.AnimateTo(1, s_enter);
                    break;
                case EntryState.Staying:
                    Glide(entry);
                    break;
            }
            entry.State = EntryState.None;
        }
    }

    private void Glide(Entry entry)
    {
        if (!_glide || double.IsNaN(entry.PreviousVisualY) || !IsLoaded)
        {
            entry.Offset.Jump(0);
            return;
        }
        var offset = entry.PreviousVisualY - entry.Y;
        if (Math.Abs(offset - entry.Offset.Value) < 0.01)
        {
            // Same slot as before: a glide that is still running carries on untouched.
            return;
        }
        if (Math.Abs(offset) < 0.5)
        {
            entry.Offset.Jump(0);
            return;
        }
        var velocity = entry.Offset.Velocity;
        entry.Offset.Jump(offset);
        _ = entry.Offset.AnimateTo(0, s_glide, velocity);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(CommandPaletteList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
