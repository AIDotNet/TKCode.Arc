// Ported from Arc's free command-palette block (MIT, Copyright (c) 2026 Elia Kuratli): the grouped result list
// of command-palette.tsx with its layout animation (AnimatePresence popLayout + layout="position").

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The result list of a <see cref="CommandPalette"/>: group headings and rows stacked top to bottom (groups
/// <see cref="GroupGap"/> apart).
/// <para>
/// The list is virtual. Every heading and command is an <see cref="Entry"/> with a position, but only the ones inside
/// the scroll viewport (plus a short margin) have a visual: a <see cref="Slot"/> taken from a small pool and handed
/// back when the entry scrolls out or is filtered away. Mounting, typing and moving the highlight therefore cost the
/// same for ten commands and for a thousand. Rows are assumed to share one height (the measured height of the rows
/// realized so far is the estimate for the others).
/// </para>
/// <para>
/// Filtering animates like Arc's <c>layout="position"</c> list (FLIP): a row that stays keeps its old on-screen
/// position as a render offset and springs (smooth) into its new slot, keeping its velocity when a new keystroke
/// retargets it; a row that leaves pops out of the flow at the place it was drawn and fades (it follows its group
/// heading if that stays); a new row fades in. Code-driven (<see cref="MotionValue{T}"/>) because the motion depends
/// on where each row was drawn before the list changed, which a ControlTheme transition cannot know. Every frame only
/// writes a translate and an opacity, so the list never relayouts while it glides. Entries outside the viewport do
/// not animate.
/// </para>
/// </summary>
internal sealed class CommandPaletteList : Panel
{
    public static readonly StyledProperty<double> GroupGapProperty =
        AvaloniaProperty.Register<CommandPaletteList, double>(nameof(GroupGap), 6);

    /// <summary>How far past the viewport entries stay realized, so a wheel step or an arrow key never shows a gap.</summary>
    private const double Overscan = 120;

    /// <summary>The realized range is rebuilt when the viewport comes this close to its edge.</summary>
    private const double Prefetch = 40;

    private const int PoolLimit = 40;

    private static readonly MotionSpec s_enter = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
    private static readonly MotionSpec s_leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
    private static readonly MotionSpec s_glide = MotionSpec.Spring(ArcSpring.Smooth);

    private readonly Dictionary<object, Entry> _rows = new();
    private readonly Dictionary<string, Entry> _headings = new(StringComparer.Ordinal);
    private List<Entry> _live = new();
    private List<Entry> _next = new();
    private readonly List<Slot> _realized = new();
    private readonly List<Slot> _exiting = new();
    private readonly Stack<Slot> _rowPool = new();
    private readonly Stack<Slot> _headingPool = new();
    private readonly HashSet<object> _claimed = new();
    private ScrollViewer? _scroll;
    private CommandPaletteItem? _activeItem;
    private int _generation;
    private int _rowCount;
    private bool _pending;
    private bool _glide;
    private bool _fadeIn;
    private bool _narrow;
    private bool _narrowest;
    private double _rowHeight = 34;
    private double _headingHeight = 26;
    private bool _estimatesChanged;
    private double _viewTop;
    private double _viewHeight = double.PositiveInfinity;
    private double _coverTop = double.NegativeInfinity;
    private double _coverBottom = double.PositiveInfinity;

    static CommandPaletteList()
    {
        AffectsMeasure<CommandPaletteList>(GroupGapProperty);
    }

    public double GroupGap { get => GetValue(GroupGapProperty); set => SetValue(GroupGapProperty, value); }

    /// <summary>Raised at the end of every arrange pass (row positions are final): the palette places its highlight.</summary>
    public event Action? Arranged;

    /// <summary>Raised when automation selects a row.</summary>
    public Action<CommandPaletteRow>? RowAutomationSelected { get; set; }

    /// <summary>Number of rows (not headings) in the list, realized or not.</summary>
    public int RowCount => _rowCount;

    /// <summary>Number of entries that have a visual right now (live and leaving).</summary>
    public int RealizedCount => _realized.Count + _exiting.Count;

    /// <summary>Entries that are fading out.</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>The live entries (headings and rows) in list order, realized or not.</summary>
    public IReadOnlyList<Entry> LiveEntries => _live;

    /// <summary>A heading or a command of the list, with its place. It has a <see cref="Slot"/> only while it is near the viewport.</summary>
    internal sealed class Entry
    {
        public Entry(object key, bool isHeading)
        {
            Key = key;
            IsHeading = isHeading;
        }

        public object Key { get; }

        public bool IsHeading { get; }

        public string Group { get; set; } = string.Empty;

        public CommandPaletteItem? Item { get; set; }

        /// <summary>Index among the commands (not headings) in list order; -1 for a heading.</summary>
        public int ItemIndex { get; set; } = -1;

        /// <summary>Layout top in list coordinates (NaN before the first measure).</summary>
        public double Y { get; set; } = double.NaN;

        /// <summary>Layout height: measured while realized, the estimate otherwise.</summary>
        public double Height { get; set; }

        public bool IsLive { get; set; }

        public int Mark { get; set; }

        public EntryState State { get; set; }

        public Slot? Slot { get; set; }

        public CommandPaletteRow? Row => Slot?.Visual as CommandPaletteRow;

        public Control? Visual => Slot?.Visual;

        /// <summary>The render offset the entry is drawn with (0 while it has no visual).</summary>
        public double TranslateY => Slot?.Translate.Y ?? 0;

        /// <summary>Where the entry is drawn right now (layout top plus its glide offset).</summary>
        public double VisualY => Y + (Slot is { IsExiting: false } slot ? slot.Offset.Value : 0);
    }

    internal enum EntryState
    {
        None,
        Entering,
        Staying,
        Revived
    }

    /// <summary>A realized visual with the motion values that animate it; recycled between entries.</summary>
    internal sealed class Slot
    {
        public Slot(Control visual, bool isHeading)
        {
            Visual = visual;
            IsHeading = isHeading;
            visual.RenderTransform = Translate;
            Offset = new MotionValue<double>(visual, 0, MotionVectors.Double);
            Fade = new MotionValue<double>(visual, 1, MotionVectors.Double);
            Fade.Changed += opacity => visual.Opacity = opacity;
            Offset.Changed += _ => ApplyTranslate(this);
        }

        public Control Visual { get; }

        public bool IsHeading { get; }

        public TranslateTransform Translate { get; } = new();

        /// <summary>The FLIP offset: drawn position minus layout position.</summary>
        public MotionValue<double> Offset { get; }

        public MotionValue<double> Fade { get; }

        public Entry? Entry { get; set; }

        public bool InTree { get; set; }

        public bool IsExiting { get; set; }

        public double PreviousVisualY { get; set; } = double.NaN;

        public double ExitY { get; set; }

        public Slot? Anchor { get; set; }

        public double AnchorStart { get; set; }

        public List<Slot>? Followers { get; set; }

        public int ExitToken { get; set; }
    }

    #region Update

    /// <summary>
    /// Shows <paramref name="groups"/>. <paramref name="glide"/>: staying rows spring from where they are drawn and
    /// leaving rows fade out in place; otherwise staying rows snap and leaving rows go at once (so nothing overlaps the
    /// new rows). <paramref name="fadeIn"/>: new rows fade in.
    /// </summary>
    public void Update(IReadOnlyList<CommandPaletteGroup> groups, bool glide, bool fadeIn)
    {
        var generation = ++_generation;
        foreach (var slot in _realized)
        {
            slot.PreviousVisualY = slot.Entry is { } entry && !double.IsNaN(entry.Y) ? entry.VisualY : double.NaN;
        }
        foreach (var slot in _exiting)
        {
            slot.PreviousVisualY = slot.ExitY + slot.Translate.Y;
        }
        _next.Clear();
        _claimed.Clear();
        var itemIndex = 0;
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
                row.Item = item;
                row.Group = group.Name;
                row.Mark = generation;
                row.ItemIndex = itemIndex++;
                _next.Add(row);
            }
        }
        _rowCount = itemIndex;
        foreach (var entry in _live)
        {
            if (entry.Mark != generation)
            {
                BeginExit(entry, glide, generation);
            }
        }
        foreach (var entry in _next)
        {
            if (entry.Slot is { IsExiting: true } leaving)
            {
                CancelExit(leaving);
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
            if (entry.Slot is { } slot)
            {
                SyncRow(slot, entry);
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
            entry.IsLive = false;
            entry.Slot = null;
        }
        foreach (var slot in _realized.ToArray())
        {
            Release(slot);
        }
        foreach (var slot in _exiting.ToArray())
        {
            Release(slot);
        }
        _rows.Clear();
        _headings.Clear();
        _live.Clear();
        _realized.Clear();
        _exiting.Clear();
        _rowCount = 0;
        _pending = false;
        InvalidateMeasure();
    }

    /// <summary>Marks the command whose row draws as active (the palette's highlight sits behind it).</summary>
    public void SetActiveItem(CommandPaletteItem? item)
    {
        if (ReferenceEquals(_activeItem, item))
        {
            return;
        }
        var previous = _activeItem;
        _activeItem = item;
        if (previous is not null && FindRow(previous)?.Row is { } before)
        {
            before.SetActive(false);
        }
        if (item is not null && FindRow(item)?.Row is { } after)
        {
            after.SetActive(true);
        }
    }

    /// <summary>The live entry showing <paramref name="item"/>, or null.</summary>
    public Entry? FindRow(CommandPaletteItem item)
    {
        if (_rows.TryGetValue(CommandPaletteFilter.KeyOf(item), out var entry) && entry.IsLive && ReferenceEquals(entry.Item, item))
        {
            return entry;
        }
        return _rows.TryGetValue(item, out entry) && entry.IsLive ? entry : null;
    }

    /// <summary>The live row drawn under <paramref name="point"/> (list coordinates), or null. Only realized rows can be under the pointer.</summary>
    public Entry? HitRow(Point point)
    {
        if (point.X < 0 || point.X > Bounds.Width)
        {
            return null;
        }
        foreach (var slot in _realized)
        {
            if (slot.IsHeading || slot.Entry is not { IsLive: true } entry || double.IsNaN(entry.Y))
            {
                continue;
            }
            var top = entry.VisualY;
            if (point.Y >= top && point.Y < top + slot.Visual.Bounds.Height)
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
        foreach (var slot in _realized)
        {
            (slot.Visual as CommandPaletteRow)?.SetWidthClass(narrow, narrowest);
        }
        foreach (var slot in _exiting)
        {
            (slot.Visual as CommandPaletteRow)?.SetWidthClass(narrow, narrowest);
        }
        foreach (var slot in _rowPool)
        {
            (slot.Visual as CommandPaletteRow)?.SetWidthClass(narrow, narrowest);
        }
    }

    private Entry GetHeading(string name)
    {
        if (!_headings.TryGetValue(name, out var entry))
        {
            entry = new Entry(name, isHeading: true) { Height = _headingHeight };
            _headings.Add(name, entry);
        }
        return entry;
    }

    private Entry GetRow(object key)
    {
        if (!_rows.TryGetValue(key, out var entry))
        {
            entry = new Entry(key, isHeading: false) { Height = _rowHeight };
            _rows.Add(key, entry);
        }
        return entry;
    }

    #endregion

    #region Slots

    private Slot Acquire(Entry entry)
    {
        var pool = entry.IsHeading ? _headingPool : _rowPool;
        Slot slot;
        if (pool.Count > 0)
        {
            slot = pool.Pop();
        }
        else if (entry.IsHeading)
        {
            slot = new Slot(new CommandPaletteGroupHeading(), isHeading: true);
        }
        else
        {
            var row = new CommandPaletteRow { AutomationSelected = r => RowAutomationSelected?.Invoke(r) };
            row.SetWidthClass(_narrow, _narrowest);
            slot = new Slot(row, isHeading: false);
        }
        slot.Entry = entry;
        entry.Slot = slot;
        if (entry.IsHeading)
        {
            var heading = (CommandPaletteGroupHeading)slot.Visual;
            if (heading.GroupName != entry.Group)
            {
                heading.SetGroup(entry.Group);
            }
        }
        else
        {
            SyncRow(slot, entry);
            ((CommandPaletteRow)slot.Visual).SetActive(ReferenceEquals(entry.Item, _activeItem));
        }
        _realized.Add(slot);
        if (!slot.InTree)
        {
            slot.InTree = true;
            Children.Add(slot.Visual);
        }
        return slot;
    }

    /// <summary>Shows the entry's command in its row (cheap when nothing changed).</summary>
    private void SyncRow(Slot slot, Entry entry)
    {
        if (entry.IsHeading)
        {
            return;
        }
        if (entry.Item is { } item && slot.Visual is CommandPaletteRow row)
        {
            row.SetItem(item);
            AutomationProperties.SetPositionInSet(row, entry.ItemIndex + 1);
            AutomationProperties.SetSizeOfSet(row, _rowCount);
        }
    }

    /// <summary>Takes a slot out of the tree and back into the pool (or drops it when the pool is full).</summary>
    private void Release(Slot slot)
    {
        slot.ExitToken++;
        Detach(slot);
        if (slot.Followers is { } followers)
        {
            foreach (var follower in followers)
            {
                follower.Anchor = null;
            }
            followers.Clear();
        }
        slot.IsExiting = false;
        _exiting.Remove(slot);
        _realized.Remove(slot);
        if (slot.Entry is { } entry)
        {
            entry.Slot = null;
            slot.Entry = null;
            if (!entry.IsLive)
            {
                // A leaving entry is forgotten once its exit is over (a revived one is live again and stays).
                if (entry.IsHeading)
                {
                    _headings.Remove((string)entry.Key);
                }
                else
                {
                    _rows.Remove(entry.Key);
                }
            }
        }
        slot.Offset.Jump(0);
        slot.Fade.Jump(1);
        slot.Translate.Y = 0;
        if (slot.Visual is CommandPaletteRow row)
        {
            row.SetActive(false);
        }
        if (slot.InTree)
        {
            slot.InTree = false;
            Children.Remove(slot.Visual);
        }
        var pool = slot.IsHeading ? _headingPool : _rowPool;
        if (pool.Count < PoolLimit)
        {
            (slot.Visual as CommandPaletteRow)?.ReleaseIcon();
            pool.Push(slot);
        }
        else
        {
            slot.Offset.Dispose();
            slot.Fade.Dispose();
            (slot.Visual as CommandPaletteRow)?.ReleaseIcon();
        }
    }

    private static void Detach(Slot slot)
    {
        slot.Anchor?.Followers?.Remove(slot);
        slot.Anchor = null;
    }

    private void BeginExit(Entry entry, bool glide, int generation)
    {
        entry.IsLive = false;
        entry.State = EntryState.None;
        var slot = entry.Slot;
        if (slot is null)
        {
            // Never drawn: nothing to animate, forget it.
            if (entry.IsHeading)
            {
                _headings.Remove((string)entry.Key);
            }
            else
            {
                _rows.Remove(entry.Key);
            }
            return;
        }
        if (!glide || double.IsNaN(entry.Y) || double.IsNaN(slot.PreviousVisualY) || !IsLoaded)
        {
            Release(slot);
            return;
        }
        _realized.Remove(slot);
        slot.ExitY = slot.PreviousVisualY;
        slot.IsExiting = true;
        if (!entry.IsHeading && _headings.TryGetValue(entry.Group, out var heading) && heading.Mark == generation && heading.Slot is { } anchor)
        {
            slot.Anchor = anchor;
            slot.AnchorStart = anchor.PreviousVisualY;
            (anchor.Followers ??= new()).Add(slot);
        }
        slot.Offset.Jump(0);
        _exiting.Add(slot);
        var token = ++slot.ExitToken;
        _ = FinishExit(slot, token, slot.Fade.AnimateTo(0, s_leave));
    }

    private async Task FinishExit(Slot slot, int token, Task<bool> fade)
    {
        await fade;
        if (slot.ExitToken == token && slot.IsExiting)
        {
            Release(slot);
            InvalidateArrange();
        }
    }

    private void CancelExit(Slot slot)
    {
        slot.ExitToken++;
        Detach(slot);
        slot.IsExiting = false;
        _exiting.Remove(slot);
        _realized.Add(slot);
        // Back in the flow: its layout offset continues from where it is drawn.
        slot.Offset.Jump(0);
    }

    private static void ApplyTranslate(Slot slot)
    {
        if (slot.IsExiting)
        {
            // A leaving row rides along with its group heading, like Arc's rows popped out inside a positioned group.
            slot.Translate.Y = slot.Anchor is { Entry: { } anchorEntry } anchor && !double.IsNaN(anchorEntry.Y)
                ? anchorEntry.Y + anchor.Offset.Value - slot.AnchorStart
                : 0;
        }
        else
        {
            slot.Translate.Y = slot.Offset.Value;
        }
        if (slot.Followers is { Count: > 0 } followers)
        {
            foreach (var follower in followers)
            {
                ApplyTranslate(follower);
            }
        }
    }

    #endregion

    #region Viewport

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroll = this.FindAncestorOfType<ScrollViewer>();
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }
        ReadViewport();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll = null;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        ReadViewport();
        // Rebuild the realized range only when the viewport gets close to its edge.
        if (_viewTop - _coverTop < Prefetch || _coverBottom - (_viewTop + _viewHeight) < Prefetch)
        {
            InvalidateMeasure();
        }
    }

    /// <summary>The scroll viewport in list coordinates; before the viewer has measured, its maximum height stands in.</summary>
    private void ReadViewport()
    {
        if (_scroll is null)
        {
            _viewTop = 0;
            _viewHeight = double.PositiveInfinity;
            return;
        }
        var origin = _scroll.Content is Visual content ? this.TranslatePoint(default, content)?.Y ?? 0 : 0;
        _viewTop = _scroll.Offset.Y - origin;
        var height = _scroll.Viewport.Height;
        if (height <= 0)
        {
            height = double.IsFinite(_scroll.MaxHeight) ? _scroll.MaxHeight : 400;
        }
        _viewHeight = height;
    }

    #endregion

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        ReadViewport();
        var child = new Size(availableSize.Width, double.PositiveInfinity);
        double extent = 0;
        // Positions come from the heights (measured where realized, estimated elsewhere); a measured height that differs
        // from the estimate moves everything below it, so a second pass settles the positions.
        for (var pass = 0; pass < 3; pass++)
        {
            _estimatesChanged = false;
            extent = Place();
            Realize(child);
            if (!_estimatesChanged)
            {
                break;
            }
        }
        double width = 0;
        foreach (var slot in _realized)
        {
            width = Math.Max(width, slot.Visual.DesiredSize.Width);
        }
        foreach (var slot in _exiting)
        {
            slot.Visual.Measure(child);
        }
        return new Size(width, extent);
    }

    /// <summary>Assigns every entry its top from the heights and returns the extent.</summary>
    private double Place()
    {
        var gap = GroupGap;
        double y = 0;
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
            entry.Y = y;
            if (entry.Slot is { IsExiting: false } slot && slot.Visual.IsMeasureValid)
            {
                entry.Height = slot.Visual.DesiredSize.Height;
            }
            else if (entry.Slot is null)
            {
                entry.Height = entry.IsHeading ? _headingHeight : _rowHeight;
            }
            y += entry.Height;
        }
        return y;
    }

    /// <summary>Gives the entries near the viewport a visual and takes the visuals of the others back.</summary>
    private void Realize(Size child)
    {
        var top = _viewTop - Overscan;
        var bottom = _viewTop + _viewHeight + Overscan;
        var coverTop = double.PositiveInfinity;
        var coverBottom = double.NegativeInfinity;
        var firstRealized = false;
        var lastRealized = false;
        for (var i = 0; i < _live.Count; i++)
        {
            var entry = _live[i];
            var near = entry.Y + entry.Height >= top && entry.Y <= bottom;
            if (near)
            {
                var slot = entry.Slot ?? Acquire(entry);
                slot.Visual.Measure(child);
                var height = slot.Visual.DesiredSize.Height;
                var estimate = entry.IsHeading ? _headingHeight : _rowHeight;
                if (Math.Abs(height - entry.Height) > 0.01)
                {
                    entry.Height = height;
                }
                if (Math.Abs(height - estimate) > 0.01)
                {
                    if (entry.IsHeading)
                    {
                        _headingHeight = height;
                    }
                    else
                    {
                        _rowHeight = height;
                    }
                    _estimatesChanged = true;
                }
                coverTop = Math.Min(coverTop, entry.Y);
                coverBottom = Math.Max(coverBottom, entry.Y + entry.Height);
                firstRealized |= i == 0;
                lastRealized |= i == _live.Count - 1;
            }
            else if (entry.Slot is { } slot)
            {
                Release(slot);
            }
        }
        _coverTop = firstRealized || double.IsInfinity(coverTop) ? double.NegativeInfinity : coverTop;
        _coverBottom = lastRealized || double.IsInfinity(coverBottom) ? double.PositiveInfinity : coverBottom;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var slot in _realized)
        {
            if (slot.Entry is { } entry)
            {
                slot.Visual.Arrange(new Rect(0, entry.Y, finalSize.Width, slot.Visual.DesiredSize.Height));
            }
        }
        foreach (var slot in _exiting)
        {
            slot.Visual.Arrange(new Rect(0, slot.ExitY, finalSize.Width, slot.Visual.DesiredSize.Height));
        }
        if (_pending)
        {
            _pending = false;
            ApplyPending();
        }
        foreach (var slot in _realized)
        {
            if (slot.Followers is { Count: > 0 })
            {
                ApplyTranslate(slot);
            }
        }
        Arranged?.Invoke();
        return finalSize;
    }

    private void ApplyPending()
    {
        foreach (var entry in _live)
        {
            var state = entry.State;
            entry.State = EntryState.None;
            if (entry.Slot is not { } slot || state == EntryState.None)
            {
                continue;
            }
            switch (state)
            {
                case EntryState.Entering:
                    slot.Offset.Jump(0);
                    if (_fadeIn && IsLoaded)
                    {
                        slot.Fade.Jump(0);
                        _ = slot.Fade.AnimateTo(1, s_enter);
                    }
                    else
                    {
                        slot.Fade.Jump(1);
                    }
                    break;
                case EntryState.Revived:
                    Glide(slot, entry);
                    _ = slot.Fade.AnimateTo(1, s_enter);
                    break;
                case EntryState.Staying:
                    Glide(slot, entry);
                    break;
            }
        }
    }

    private void Glide(Slot slot, Entry entry)
    {
        if (!_glide || double.IsNaN(slot.PreviousVisualY) || !IsLoaded)
        {
            slot.Offset.Jump(0);
            return;
        }
        var offset = slot.PreviousVisualY - entry.Y;
        if (Math.Abs(offset - slot.Offset.Value) < 0.01)
        {
            // Same slot as before: a glide that is still running carries on untouched.
            return;
        }
        if (Math.Abs(offset) < 0.5)
        {
            slot.Offset.Jump(0);
            return;
        }
        var velocity = slot.Offset.Velocity;
        slot.Offset.Jump(offset);
        _ = slot.Offset.AnimateTo(0, s_glide, velocity);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(CommandPaletteList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
