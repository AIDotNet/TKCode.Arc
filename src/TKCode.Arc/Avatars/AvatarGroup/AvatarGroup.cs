using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A team or set of contributors in a small space (Arc AvatarGroup): overlapping avatars with a 2px surface ring and
/// a 1px border ring, later people stacked over earlier ones, and a "+N" chip for everyone past
/// <see cref="MaxVisibleCount"/>. People who join or leave open or close their slot on the morph spring so the stack
/// slides (P19); the overflow count rolls the way it moved (P14). Pointing at the stack (fine pointers only) loosens
/// it from the center (P28) and lifts the person under the pointer with a crisp ring and their name while the
/// neighbours ease aside (P27). Only the visible people get controls, so long member lists stay cheap.
/// Items may be <see cref="AvatarGroupMember"/>, <see cref="Avatar"/> or plain names.
/// </summary>
[TemplatePart(PanelPart, typeof(AvatarGroupPanel))]
public class AvatarGroup : TemplatedControl
{
    public const string PanelPart = "PART_Panel";

    /// <summary>Bound members (<see cref="AvatarGroupMember"/>, <see cref="Avatar"/> or names); takes precedence over <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<AvatarGroup, IEnumerable?>(nameof(ItemsSource));

    /// <summary>People shown before the rest fold into the overflow count (Arc <c>max</c>, 4).</summary>
    public static readonly StyledProperty<int> MaxVisibleCountProperty =
        AvaloniaProperty.Register<AvatarGroup, int>(nameof(MaxVisibleCount), 4);

    /// <summary>Avatar size: Small 28, Medium 36, Large 48 (XLarge is treated as Large).</summary>
    public static readonly StyledProperty<AvatarSize> SizeProperty =
        AvaloniaProperty.Register<AvatarGroup, AvatarSize>(nameof(Size), AvatarSize.Medium);

    /// <summary>Accessible name of the group, also used for the overflow chip ("2 more team members").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AvatarGroup, string>(nameof(Label), "Team members");

    /// <summary>How many people are folded into the "+N" chip.</summary>
    public static readonly DirectProperty<AvatarGroup, int> OverflowCountProperty =
        AvaloniaProperty.RegisterDirect<AvatarGroup, int>(nameof(OverflowCount), o => o.OverflowCount);

    internal static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<AvatarGroup, double>(nameof(Diameter), 36);

    internal static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<AvatarGroup, double>(nameof(Overlap), 6);

    internal static readonly StyledProperty<double> ChipFontSizeProperty =
        AvaloniaProperty.Register<AvatarGroup, double>(nameof(ChipFontSize), 12);

    internal static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(RingBrush));

    internal static readonly StyledProperty<IBrush?> RingHoverBrushProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(RingHoverBrush));

    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(SurfaceBrush));

    internal static readonly StyledProperty<IBrush?> ChipBackgroundProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(ChipBackground));

    internal static readonly StyledProperty<IBrush?> ChipForegroundProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(ChipForeground));

    internal static readonly StyledProperty<IBrush?> TipBackgroundProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(TipBackground));

    internal static readonly StyledProperty<IBrush?> TipForegroundProperty =
        AvaloniaProperty.Register<AvatarGroup, IBrush?>(nameof(TipForeground));

    internal static readonly StyledProperty<Thickness> TipPaddingProperty =
        AvaloniaProperty.Register<AvatarGroup, Thickness>(nameof(TipPadding), new Thickness(8, 4));

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public int MaxVisibleCount { get => GetValue(MaxVisibleCountProperty); set => SetValue(MaxVisibleCountProperty, value); }
    public AvatarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    internal double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }
    internal double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }
    internal double ChipFontSize { get => GetValue(ChipFontSizeProperty); set => SetValue(ChipFontSizeProperty, value); }
    internal IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    internal IBrush? RingHoverBrush { get => GetValue(RingHoverBrushProperty); set => SetValue(RingHoverBrushProperty, value); }
    internal IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    internal IBrush? ChipBackground { get => GetValue(ChipBackgroundProperty); set => SetValue(ChipBackgroundProperty, value); }
    internal IBrush? ChipForeground { get => GetValue(ChipForegroundProperty); set => SetValue(ChipForegroundProperty, value); }
    internal IBrush? TipBackground { get => GetValue(TipBackgroundProperty); set => SetValue(TipBackgroundProperty, value); }
    internal IBrush? TipForeground { get => GetValue(TipForegroundProperty); set => SetValue(TipForegroundProperty, value); }
    internal Thickness TipPadding { get => GetValue(TipPaddingProperty); set => SetValue(TipPaddingProperty, value); }

    /// <summary>Inline members (<c>&lt;arc:AvatarGroupMember DisplayName="…"/&gt;</c>); ignored while <see cref="ItemsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<object> Items { get; } = new();

    private int _overflowCount;

    /// <summary>How many people are folded into the "+N" chip.</summary>
    public int OverflowCount
    {
        get => _overflowCount;
        private set => SetAndRaise(OverflowCountProperty, ref _overflowCount, value);
    }

    private readonly record struct SlotKey(object Item, int Occurrence);

    private static readonly object OverflowKey = new();
    private readonly Dictionary<object, AvatarGroupSlot> _slots = new();
    private AvatarGroupPanel? _panel;
    private INotifyCollectionChanged? _observed;
    private bool _groupHover;
    private AvatarGroupSlot? _hovered;

    static AvatarGroup()
    {
        FocusableProperty.OverrideDefaultValue<AvatarGroup>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<AvatarGroup>(AutomationControlType.Group);
    }

    public AvatarGroup()
    {
        Items.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                Refresh();
            }
        };
        AutomationProperties.SetName(this, Label);
    }

    internal IReadOnlyList<AvatarGroupSlot> Slots => _panel?.Children.OfType<AvatarGroupSlot>().ToList() ?? (IReadOnlyList<AvatarGroupSlot>)Array.Empty<AvatarGroupSlot>();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.PointerMoved -= OnPanelPointerMoved;
            _panel.PointerExited -= OnPanelPointerExited;
            _panel.Children.Clear();
        }
        _slots.Clear();
        _panel = e.NameScope.Find<AvatarGroupPanel>(PanelPart);
        if (_panel is not null)
        {
            _panel.Owner = this;
            _panel.PointerMoved += OnPanelPointerMoved;
            _panel.PointerExited += OnPanelPointerExited;
        }
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource();
            Refresh();
        }
        else if (change.Property == MaxVisibleCountProperty)
        {
            Refresh();
        }
        else if (change.Property == SizeProperty || change.Property == SurfaceBrushProperty ||
                 change.Property == ChipBackgroundProperty || change.Property == ChipForegroundProperty ||
                 change.Property == ChipFontSizeProperty || change.Property == DiameterProperty ||
                 change.Property == TipBackgroundProperty || change.Property == TipForegroundProperty)
        {
            foreach (var slot in _slots.Values)
            {
                StyleSlot(slot);
            }
            _panel?.InvalidateMeasure();
        }
        else if (change.Property == RingBrushProperty || change.Property == RingHoverBrushProperty)
        {
            foreach (var slot in _slots.Values)
            {
                slot.InvalidateVisual();
            }
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
            UpdateChipName();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    // User data is observed only while the group is in the tree, so long-lived members never keep it alive.
    private void ObserveSource()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
        }
        _observed = TopLevel.GetTopLevel(this) is not null ? ItemsSource as INotifyCollectionChanged : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnSourceChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSource();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
            _observed = null;
        }
        // Member bindings end with their slots; the slots are rebuilt on the next attach.
        foreach (var slot in _slots.Values)
        {
            slot.DisposeBindings();
        }
        _panel?.Children.Clear();
        _slots.Clear();
        _hovered = null;
        _groupHover = false;
    }

    private List<object> CurrentItems()
    {
        var list = new List<object>();
        IEnumerable source = ItemsSource ?? Items;
        foreach (var item in source)
        {
            if (item is not null)
            {
                list.Add(item);
            }
        }
        return list;
    }

    /// <summary>Re-reads the members and opens, closes or reorders slots.</summary>
    private void Refresh()
    {
        if (_panel is null)
        {
            OverflowCount = Math.Max(0, CurrentItems().Count - Math.Max(0, MaxVisibleCount));
            return;
        }
        var items = CurrentItems();
        var visible = Math.Min(Math.Max(0, MaxVisibleCount), items.Count);
        var overflow = items.Count - visible;
        var previousOverflow = OverflowCount;
        OverflowCount = overflow;
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && level == ArcMotionLevel.Full;

        var wanted = new List<AvatarGroupSlot>(visible + 1);
        var opening = new List<AvatarGroupSlot>();
        var occurrences = new Dictionary<object, int>();
        var stale = new Dictionary<object, AvatarGroupSlot>(_slots);
        for (var i = 0; i < visible; i++)
        {
            var item = items[i];
            var occurrence = occurrences.GetValueOrDefault(item);
            occurrences[item] = occurrence + 1;
            var key = new SlotKey(item, occurrence);
            if (!stale.Remove(key, out var slot))
            {
                slot = CreateMemberSlot(key, item);
                _slots[key] = slot;
                slot.AnimatePresence(animate ? 0 : 1, false);
                opening.Add(slot);
            }
            wanted.Add(slot);
        }
        if (overflow > 0)
        {
            if (!stale.Remove(OverflowKey, out var chip))
            {
                chip = CreateOverflowSlot();
                _slots[OverflowKey] = chip;
                chip.SetCount(overflow, 1);
                chip.AnimatePresence(animate ? 0 : 1, false);
                opening.Add(chip);
            }
            else if (overflow != previousOverflow)
            {
                chip.SetCount(overflow, overflow < previousOverflow ? -1 : 1);
            }
            wanted.Add(chip);
        }
        UpdateChipName();

        // Leaving people keep their place in the stack while their slot closes.
        var order = new List<AvatarGroupSlot>(wanted);
        var children = _panel.Children;
        foreach (var (key, slot) in stale)
        {
            _slots.Remove(key);
            slot.IsLeaving = true;
            var oldIndex = children.IndexOf(slot);
            order.Insert(Math.Clamp(oldIndex, 0, order.Count), slot);
            if (!animate)
            {
                children.Remove(slot);
                order.Remove(slot);
                slot.DisposeBindings();
                continue;
            }
            _ = slot.AnimatePresence(0, true);
        }
        foreach (var child in children.OfType<AvatarGroupSlot>().ToList())
        {
            if (!order.Contains(child))
            {
                children.Remove(child);
            }
        }
        for (var i = 0; i < order.Count; i++)
        {
            var at = children.IndexOf(order[i]);
            if (at < 0)
            {
                children.Insert(i, order[i]);
            }
            else if (at != i)
            {
                children.Move(at, i);
            }
        }
        // Slots open once they are in the tree, so their spring runs on the frame clock.
        foreach (var slot in opening)
        {
            _ = slot.AnimatePresence(1, animate);
        }
        UpdateEmphasis();
        _panel.InvalidateMeasure();
    }

    /// <summary>Called by a leaving slot whose presence has landed at zero.</summary>
    internal void OnSlotClosed(AvatarGroupSlot slot)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!slot.IsLeaving || _panel is null)
            {
                return;
            }
            _panel.Children.Remove(slot);
            slot.DisposeBindings();
            if (ReferenceEquals(_hovered, slot))
            {
                _hovered = null;
            }
            UpdateEmphasis();
        });
    }

    private AvatarGroupSlot CreateMemberSlot(object key, object item)
    {
        var avatar = new Avatar();
        var bindings = new List<IDisposable>(3);
        switch (item)
        {
            case AvatarGroupMember member:
                bindings.Add(avatar.Bind(Avatar.DisplayNameProperty, member.GetObservable(AvatarGroupMember.DisplayNameProperty)));
                bindings.Add(avatar.Bind(Avatar.SourceProperty, member.GetObservable(AvatarGroupMember.SourceProperty)));
                bindings.Add(avatar.Bind(Avatar.StatusProperty, member.GetObservable(AvatarGroupMember.StatusProperty)));
                break;
            case Avatar source:
                avatar.DisplayName = source.DisplayName;
                avatar.Source = source.Source;
                avatar.Status = source.Status;
                break;
            default:
                avatar.DisplayName = Convert.ToString(item, CultureInfo.CurrentCulture);
                break;
        }
        var slot = new AvatarGroupSlot(this, key, avatar, isOverflow: false) { TipText = avatar.DisplayName };
        slot.Bindings.AddRange(bindings);
        AttachHover(slot);
        StyleSlot(slot);
        return slot;
    }

    private AvatarGroupSlot CreateOverflowSlot()
    {
        var chip = AvatarGroupSlot.CreateChip(out var count);
        var slot = new AvatarGroupSlot(this, OverflowKey, chip, isOverflow: true);
        slot.AttachCount(count);
        AttachHover(slot);
        StyleSlot(slot);
        return slot;
    }

    private void AttachHover(AvatarGroupSlot slot)
    {
        slot.PointerEntered += (_, e) => OnSlotPointer(slot, e);
    }

    private void StyleSlot(AvatarGroupSlot slot)
    {
        var d = Diameter;
        if (slot.Content is Avatar avatar)
        {
            avatar.Size = Size switch
            {
                AvatarSize.Small => AvatarSize.Small,
                AvatarSize.Medium => AvatarSize.Medium,
                _ => AvatarSize.Large
            };
            // The stack draws a 2px surface border inside the avatar's own size; the 1px ring is drawn by the slot.
            avatar.BorderThickness = new Thickness(2);
            avatar.BorderBrush = SurfaceBrush;
            slot.TipText = avatar.DisplayName;
        }
        else if (slot.Content is Border chip)
        {
            chip.Width = d;
            chip.Height = d;
            chip.CornerRadius = new CornerRadius(d / 2);
            chip.BorderThickness = new Thickness(2);
            chip.BorderBrush = SurfaceBrush;
            chip.Background = ChipBackground;
            TextElement.SetForeground(chip, ChipForeground);
            TextElement.SetFontSize(chip, ChipFontSize);
            TextElement.SetFontWeight(chip, FontWeight);
            TextElement.SetFontFamily(chip, FontFamily);
        }
        slot.InvalidateMeasure();
    }

    internal void StyleTip(Border tip, TextBlock text)
    {
        tip.Background = TipBackground;
        tip.Padding = TipPadding;
        tip.CornerRadius = new CornerRadius(9999);
        text.Foreground = TipForeground;
        text.FontFamily = FontFamily;
        text.FontSize = FontSize;
        text.FontWeight = FontWeight;
        text.LineHeight = FontSize * 1.4;
    }

    private void UpdateChipName()
    {
        if (_slots.TryGetValue(OverflowKey, out var chip))
        {
            var label = (Label ?? string.Empty).ToLower(CultureInfo.CurrentCulture);
            AutomationProperties.SetName(chip.Content, $"{OverflowCount} more {label}");
        }
    }

    private static bool IsFinePointer(PointerEventArgs e) => e.Pointer.Type != PointerType.Touch;

    private void OnSlotPointer(AvatarGroupSlot slot, PointerEventArgs e)
    {
        if (!IsFinePointer(e) || slot.IsLeaving)
        {
            return;
        }
        _groupHover = true;
        if (!ReferenceEquals(_hovered, slot))
        {
            _hovered = slot;
            UpdateEmphasis();
        }
    }

    private void OnPanelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsFinePointer(e))
        {
            return;
        }
        var slot = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<AvatarGroupSlot>().FirstOrDefault();
        if (!_groupHover || (slot is not null && !ReferenceEquals(slot, _hovered) && !slot.IsLeaving))
        {
            _groupHover = true;
            if (slot is { IsLeaving: false })
            {
                _hovered = slot;
            }
            UpdateEmphasis();
        }
    }

    private void OnPanelPointerExited(object? sender, PointerEventArgs e)
    {
        _groupHover = false;
        _hovered = null;
        UpdateEmphasis();
    }

    /// <summary>Applies the hover fan (P28), lift and neighbour shift (P27) to every slot.</summary>
    private void UpdateEmphasis()
    {
        if (_panel is null)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var full = level == ArcMotionLevel.Full;
        var live = _panel.Children.OfType<AvatarGroupSlot>().Where(s => !s.IsLeaving).ToList();
        var hoveredIndex = _hovered is null ? -1 : live.IndexOf(_hovered);
        for (var i = 0; i < live.Count; i++)
        {
            var slot = live[i];
            var target = ComputeOffset(i, live.Count, _groupHover, hoveredIndex);
            slot.AnimateOffset(target, full);
            var emphasized = i == hoveredIndex;
            slot.ZIndex = emphasized ? 1 : 0;
            slot.SetEmphasis(emphasized, level);
        }
    }

    /// <summary>
    /// Hover offset of slot <paramref name="index"/>: the stack loosens 4px per step from its center, the hovered
    /// person rises 2px and the people after / before it move 3px right / left.
    /// </summary>
    internal static Vector ComputeOffset(int index, int count, bool groupHover, int hoveredIndex)
    {
        if (!groupHover)
        {
            return default;
        }
        var fan = (index - (count - 1) / 2.0) * 4;
        var aside = hoveredIndex < 0 ? 0 : index > hoveredIndex ? 3 : index < hoveredIndex ? -3 : 0;
        var rise = index == hoveredIndex ? -2 : 0;
        return new Vector(fan + aside, rise);
    }
}

/// <summary>Lays the slots of an <see cref="AvatarGroup"/> out with a 6px overlap; slot widths follow their presence.</summary>
internal sealed class AvatarGroupPanel : Panel
{
    public AvatarGroup? Owner { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var d = Owner?.Diameter ?? 36;
        var overlap = Owner?.Overlap ?? 6;
        var width = overlap;
        var any = false;
        foreach (var child in Children)
        {
            child.Measure(new Size(d, d));
            if (child is AvatarGroupSlot slot)
            {
                width += (d - overlap) * Math.Max(0, slot.Presence);
                any = true;
            }
        }
        return new Size(any ? width : 0, d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var d = Owner?.Diameter ?? 36;
        var overlap = Owner?.Overlap ?? 6;
        var x = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, d, d));
            if (child is AvatarGroupSlot slot)
            {
                x += (d - overlap) * Math.Max(0, slot.Presence);
            }
        }
        return finalSize;
    }
}
