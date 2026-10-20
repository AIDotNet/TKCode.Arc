using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="ActivityReactionBar.Toggled"/>: the emoji whose chip was clicked.</summary>
internal sealed class ActivityReactionToggledEventArgs(string emoji) : EventArgs
{
    public string Emoji { get; } = emoji;
}

/// <summary>
/// The reactions under a comment or message (Arc's <c>.reactions</c> flex-wrap row, 4px gaps): one
/// <see cref="ActivityReactionChip"/> per emoji, kept by emoji across updates so counts roll instead of chips being
/// rebuilt. New chips pop in (P26), removed chips pop out of the flow and shrink where they were, and the chips that
/// moved glide from where they were on the chip's spring (FLIP, P19, keeps velocity). Reduced fades only and moves
/// nothing; Off applies the end state.
/// </summary>
internal sealed class ActivityReactionBar : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ActivityReactionBar, double>(nameof(Gap), 4);

    public static readonly StyledProperty<ActivityReactionChipVariant> VariantProperty =
        AvaloniaProperty.Register<ActivityReactionBar, ActivityReactionChipVariant>(nameof(Variant));

    /// <summary>Rows hug the right edge (a chat bubble of the current user: <c>justify-content: flex-end</c>).</summary>
    public static readonly StyledProperty<bool> IsRightAlignedProperty =
        AvaloniaProperty.Register<ActivityReactionBar, bool>(nameof(IsRightAligned));

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public ActivityReactionChipVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public bool IsRightAligned { get => GetValue(IsRightAlignedProperty); set => SetValue(IsRightAlignedProperty, value); }

    /// <summary>The accessible name of a chip ("👍 3, including you").</summary>
    public Func<ActivityReaction, string>? NameFormatter { get; set; }

    /// <summary>A chip was clicked: the thread toggles the viewer's reaction in its data.</summary>
    public event EventHandler<ActivityReactionToggledEventArgs>? Toggled;

    private static readonly RelativePoint s_topLeft = new(0, 0, RelativeUnit.Absolute);

    private sealed class Slot
    {
        public Point Natural;
        public bool Placed;
        public MotionValue<Vector>? Offset;
        public readonly MatrixTransform Transform = new();
    }

    private readonly Dictionary<string, ActivityReactionChip> _chips = new(StringComparer.Ordinal);
    private readonly Dictionary<Control, Slot> _slots = new();
    private readonly List<ActivityReaction> _current = new();
    private bool _flipArmed;

    static ActivityReactionBar()
    {
        AffectsMeasure<ActivityReactionBar>(GapProperty, IsRightAlignedProperty);
    }

    public ActivityReactionBar()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>The reactions shown, in order.</summary>
    public IReadOnlyList<ActivityReaction> Reactions => _current;

    /// <summary>The live chip of <paramref name="emoji"/> (tests and focus), or null.</summary>
    public ActivityReactionChip? ChipOf(string emoji) =>
        _chips.TryGetValue(emoji, out var chip) && !chip.IsLeaving ? chip : null;

    /// <summary>The chips in the flow, in order.</summary>
    public IEnumerable<ActivityReactionChip> LiveChips =>
        Children.OfType<ActivityReactionChip>().Where(chip => !chip.IsLeaving);

    /// <summary>
    /// Shows <paramref name="reactions"/>: chips are kept by emoji (their counts roll), new ones enter and missing ones
    /// leave. With <paramref name="animate"/> false (first show, recycled rows) everything is placed at once.
    /// </summary>
    public void SetReactions(IReadOnlyList<ActivityReaction>? reactions, bool animate)
    {
        reactions ??= Array.Empty<ActivityReaction>();
        animate &= IsLoaded;
        _current.Clear();
        _current.AddRange(reactions);
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var changedStructure = false;
        var index = 0;
        foreach (var reaction in reactions)
        {
            if (!wanted.Add(reaction.Emoji))
            {
                continue;
            }
            if (!_chips.TryGetValue(reaction.Emoji, out var chip))
            {
                chip = CreateChip(reaction.Emoji);
                _chips[reaction.Emoji] = chip;
                Children.Insert(Math.Min(index, Children.Count), chip);
                Update(chip, reaction);
                chip.Enter(animate, fresh: true);
                changedStructure = true;
            }
            else
            {
                if (chip.IsLeaving)
                {
                    chip.Enter(animate, fresh: false);
                    changedStructure = true;
                }
                var at = Children.IndexOf(chip);
                if (at != index && index < Children.Count)
                {
                    Children.Move(at, index);
                    changedStructure = true;
                }
                Update(chip, reaction);
            }
            index++;
        }
        foreach (var (emoji, chip) in _chips.ToArray())
        {
            if (wanted.Contains(emoji) || chip.IsLeaving)
            {
                continue;
            }
            changedStructure = true;
            if (!animate)
            {
                Remove(emoji, chip);
                continue;
            }
            _ = LeaveAsync(emoji, chip);
        }
        if (changedStructure)
        {
            _flipArmed = animate;
            InvalidateMeasure();
        }
    }

    private async Task LeaveAsync(string emoji, ActivityReactionChip chip)
    {
        if (await chip.LeaveAsync(animate: true))
        {
            Remove(emoji, chip);
        }
    }

    private void Remove(string emoji, ActivityReactionChip chip)
    {
        if (_chips.TryGetValue(emoji, out var current) && ReferenceEquals(current, chip))
        {
            _chips.Remove(emoji);
        }
        Children.Remove(chip);
    }

    private ActivityReactionChip CreateChip(string emoji)
    {
        var chip = new ActivityReactionChip { Emoji = emoji, Variant = Variant };
        chip.Click += OnChipClick;
        return chip;
    }

    private void Update(ActivityReactionChip chip, ActivityReaction reaction)
    {
        chip.Count = reaction.Count;
        chip.IsChecked = reaction.Mine;
        chip.Variant = Variant;
        AutomationProperties.SetName(chip, NameFormatter?.Invoke(reaction) ?? DefaultName(reaction));
    }

    /// <summary>Arc's default: "👍 3, including you".</summary>
    public static string DefaultName(ActivityReaction reaction) =>
        $"{reaction.Emoji} {reaction.Count}{(reaction.Mine ? ", including you" : string.Empty)}";

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ActivityReactionChip { IsLeaving: false, Emoji: { } emoji })
        {
            e.Handled = true;
            Toggled?.Invoke(this, new ActivityReactionToggledEventArgs(emoji));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
        {
            foreach (var chip in _chips.Values)
            {
                chip.Variant = Variant;
            }
        }
    }

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        if (e.OldItems is null || e.Action == NotifyCollectionChangedAction.Move)
        {
            return;
        }
        foreach (var item in e.OldItems)
        {
            if (item is Control child && !Children.Contains(child) && _slots.Remove(child, out var slot))
            {
                slot.Offset?.Dispose();
                child.ClearValue(RenderTransformProperty);
                child.ClearValue(RenderTransformOriginProperty);
            }
        }
    }

    private static bool InFlow(Control child) => child.IsVisible && child is not ActivityReactionChip { IsLeaving: true };

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        return Flow(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Flow(finalSize.Width, arrange: true);
        _flipArmed = false;
        return finalSize;
    }

    /// <summary>Lays the in-flow chips out in wrapping rows; arranges when asked.</summary>
    private Size Flow(double width, bool arrange)
    {
        var limit = double.IsFinite(width) ? width : double.PositiveInfinity;
        var gap = Gap;
        double x = 0, y = 0, rowHeight = 0, widest = 0;
        var rowStart = 0;
        var children = Children;
        for (var i = 0; i <= children.Count; i++)
        {
            var child = i < children.Count ? children[i] : null;
            if (child is not null && !InFlow(child))
            {
                continue;
            }
            var desired = child?.DesiredSize ?? default;
            var wraps = child is not null && x > 0 && x + desired.Width > limit + 0.01;
            if (child is null || wraps)
            {
                var rowWidth = x > 0 ? x - gap : 0;
                if (arrange)
                {
                    var start = IsRightAligned && double.IsFinite(width) ? Math.Max(0, width - rowWidth) : 0;
                    ArrangeRow(rowStart, i, start, y, rowHeight, gap);
                }
                widest = Math.Max(widest, rowWidth);
                if (child is null)
                {
                    break;
                }
                y += rowHeight + gap;
                x = 0;
                rowHeight = 0;
                rowStart = i;
            }
            x += desired.Width + gap;
            rowHeight = Math.Max(rowHeight, desired.Height);
        }
        if (arrange)
        {
            foreach (var child in children)
            {
                if (!InFlow(child) && _slots.TryGetValue(child, out var slot))
                {
                    // A leaving chip stays where it was drawn while it shrinks away.
                    child.Arrange(new Rect(slot.Natural, child.DesiredSize));
                    UpdateTransform(child);
                }
            }
        }
        return new Size(widest, y + rowHeight);
    }

    private void ArrangeRow(int start, int end, double x, double y, double rowHeight, double gap)
    {
        var animate = _flipArmed && IsLoaded && ArcMotion.IsFull(this);
        for (var i = start; i < end; i++)
        {
            var child = Children[i];
            if (!InFlow(child))
            {
                continue;
            }
            var desired = child.DesiredSize;
            var natural = new Point(x, y + (rowHeight - desired.Height) / 2);
            child.Arrange(new Rect(natural, desired));
            var slot = SlotOf(child);
            if (slot.Placed && animate && slot.Natural != natural)
            {
                // FLIP: start where the chip was drawn and glide to its new place, keeping any velocity it had.
                slot.Offset ??= CreateOffset(child);
                var velocity = slot.Offset.Velocity;
                var from = slot.Natural + slot.Offset.Value - natural;
                slot.Offset.Jump(from);
                var spring = child is ActivityReactionChip chip ? chip.Spring : ArcMotionPresets.Snappy;
                _ = slot.Offset.AnimateTo(default, MotionSpec.Spring(spring), velocity);
            }
            slot.Natural = natural;
            slot.Placed = true;
            UpdateTransform(child);
            x += desired.Width + gap;
        }
    }

    private Slot SlotOf(Control child)
    {
        if (!_slots.TryGetValue(child, out var slot))
        {
            slot = new Slot();
            _slots[child] = slot;
        }
        return slot;
    }

    private MotionValue<Vector> CreateOffset(Control child)
    {
        var value = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        value.Changed += _ => UpdateTransform(child);
        return value;
    }

    /// <summary>Writes a chip's transform: presence scale about its center, then its FLIP offset.</summary>
    public void UpdateTransform(Control child)
    {
        var slot = SlotOf(child);
        var offset = slot.Offset?.Value ?? default;
        var scale = child is ActivityReactionChip chip ? chip.PresenceScale : 1;
        var size = child.Bounds.Size;
        var cx = size.Width / 2;
        var cy = size.Height / 2;
        slot.Transform.Matrix = Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateScale(scale, scale) *
                                Matrix.CreateTranslation(cx + offset.X, cy + offset.Y);
        if (!ReferenceEquals(child.RenderTransform, slot.Transform))
        {
            child.RenderTransformOrigin = s_topLeft;
            child.RenderTransform = slot.Transform;
        }
    }
}
