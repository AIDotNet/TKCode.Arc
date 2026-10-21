using System.Collections;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Colors of the chips, resolved from ArcDesign tokens (selected tints mix the accent in OKLab).</summary>
internal readonly record struct ChipGroupColors(
    Color Fill,
    Color Border,
    Color BorderHover,
    Color SelectedFill,
    Color SelectedBorder,
    Color SelectedHoverFill,
    Color SelectedHoverBorder,
    Color Text,
    Color TextOn,
    Color Check,
    Color MoreFill,
    Color MoreText);

/// <summary>
/// Arc chip group: toggle chips for facets people switch often (topics, statuses, tags). Picking a chip morphs it:
/// a check grows in, the label slides over and the chip's edge follows on a spring while the neighbours glide to
/// their new places, even across lines. Long sets fold behind a "+N more" chip (selected chips always stay in view);
/// expanding staggers the revealed chips in and the group height springs. With <see cref="IsMultiple"/> false it is a
/// single choice that can still be cleared.
/// <para>Keyboard: one tab stop; arrows move between chips and wrap, Home / End jump to the ends; Enter or Space
/// toggles the focused chip or expands the folded ones.</para>
/// </summary>
[TemplatePart(PanelPart, typeof(ChipGroupPanel))]
public class ChipGroup : SelectingItemsControl
{
    public const string PanelPart = "PART_Panel";

    #region 公共属性定义

    /// <summary>Accessible name of the group, such as "Topics".</summary>
    public static readonly StyledProperty<string?> LabelProperty = Select.LabelProperty.AddOwner<ChipGroup>();

    /// <summary>Defines the <see cref="IsMultiple"/> property.</summary>
    public static readonly StyledProperty<bool> IsMultipleProperty =
        AvaloniaProperty.Register<ChipGroup, bool>(nameof(IsMultiple), true);

    /// <summary>Defines the <see cref="MaxVisibleItems"/> property.</summary>
    public static readonly StyledProperty<int> MaxVisibleItemsProperty =
        AvaloniaProperty.Register<ChipGroup, int>(nameof(MaxVisibleItems), int.MaxValue);

    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ChipGroup, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Label of a data item (AOT-safe alternative to <c>DisplayMemberBinding</c>).</summary>
    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty = Select.TextSelectorProperty.AddOwner<ChipGroup>();

    /// <summary>Value of a data item (AOT-safe alternative to <c>SelectedValueBinding</c>).</summary>
    public static readonly StyledProperty<Func<object?, object?>?> ValueSelectorProperty = Select.ValueSelectorProperty.AddOwner<ChipGroup>();

    /// <summary>Defines the <see cref="SelectedItems"/> property.</summary>
    public new static readonly DirectProperty<SelectingItemsControl, IList?> SelectedItemsProperty =
        SelectingItemsControl.SelectedItemsProperty;

    public static readonly DirectProperty<ChipGroup, IReadOnlyList<object?>> SelectedValuesProperty =
        AvaloniaProperty.RegisterDirect<ChipGroup, IReadOnlyList<object?>>(nameof(SelectedValues), o => o.SelectedValues);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Allow several chips at once (default). In single mode the selected chip can still be cleared.</summary>
    public bool IsMultiple { get => GetValue(IsMultipleProperty); set => SetValue(IsMultipleProperty, value); }

    /// <summary>Chips shown before the rest fold behind "+N more". Chips selected when it folds stay in view.</summary>
    public int MaxVisibleItems { get => GetValue(MaxVisibleItemsProperty); set => SetValue(MaxVisibleItemsProperty, value); }

    /// <summary>Whether the folded chips are shown (two-way).</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="Select.TextSelector"/>
    public Func<object?, string?>? TextSelector { get => GetValue(TextSelectorProperty); set => SetValue(TextSelectorProperty, value); }

    /// <inheritdoc cref="Select.ValueSelector"/>
    public Func<object?, object?>? ValueSelector { get => GetValue(ValueSelectorProperty); set => SetValue(ValueSelectorProperty, value); }

    /// <summary>The selected items (bind a list to set them from a view model).</summary>
    public new IList? SelectedItems
    {
        get => base.SelectedItems;
        set => base.SelectedItems = value;
    }

    /// <summary>The selection model.</summary>
    public new ISelectionModel Selection
    {
        get => base.Selection;
        set => base.Selection = value;
    }

    /// <summary>Values of the selected options, in option order (Arc keeps <c>value</c> in option order).</summary>
    public IReadOnlyList<object?> SelectedValues
    {
        get => _selectedValues;
        private set => SetAndRaise(SelectedValuesProperty, ref _selectedValues, value);
    }

    #endregion

    #region 内部属性定义

    internal static readonly StyledProperty<IBrush?> ChipSurfaceProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipSurface");

    internal static readonly StyledProperty<IBrush?> ChipSurfaceMutedProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipSurfaceMuted");

    internal static readonly StyledProperty<IBrush?> ChipBorderProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipBorder");

    internal static readonly StyledProperty<IBrush?> ChipBorderStrongProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipBorderStrong");

    internal static readonly StyledProperty<IBrush?> ChipAccentProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipAccent");

    internal static readonly StyledProperty<IBrush?> ChipAccentStrongProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipAccentStrong");

    internal static readonly StyledProperty<IBrush?> ChipTextProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipText");

    internal static readonly StyledProperty<IBrush?> ChipTextOnProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipTextOn");

    internal static readonly StyledProperty<IBrush?> ChipTextMutedProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>("ChipTextMuted");

    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ChipGroup, IBrush?>(nameof(FocusRingBrush));

    internal static readonly StyledProperty<double> ChipHeightProperty =
        AvaloniaProperty.Register<ChipGroup, double>(nameof(ChipHeight), 36);

    internal static readonly StyledProperty<double> ChipPaddingProperty =
        AvaloniaProperty.Register<ChipGroup, double>(nameof(ChipPadding), 14);

    internal static readonly StyledProperty<FontWeight> ChipFontWeightProperty =
        AvaloniaProperty.Register<ChipGroup, FontWeight>(nameof(ChipFontWeight), FontWeight.Medium);

    internal double ChipHeight { get => GetValue(ChipHeightProperty); set => SetValue(ChipHeightProperty, value); }
    internal double ChipPadding { get => GetValue(ChipPaddingProperty); set => SetValue(ChipPaddingProperty, value); }
    internal FontWeight ChipFontWeight { get => GetValue(ChipFontWeightProperty); set => SetValue(ChipFontWeightProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    #endregion

    private const int MoreKey = -1;
    private readonly SelectEntrySource _entries;
    private readonly Dictionary<int, ChipGroupChip> _chips = new();
    private readonly HashSet<int> _pinned = new();
    private IReadOnlyList<object?> _selectedValues = Array.Empty<object?>();
    private ChipGroupPanel? _panel;
    private ChipGroupChip? _more;
    private int _active = int.MinValue;
    private bool _colorsDirty = true;
    private ChipGroupColors _colors;
    private bool _syncPosted;

    static ChipGroup()
    {
        FocusableProperty.OverrideDefaultValue<ChipGroup>(false);
        SelectionModeProperty.OverrideDefaultValue<ChipGroup>(SelectionMode.Multiple | SelectionMode.Toggle);
    }

    public ChipGroup()
    {
        _entries = new SelectEntrySource(this, () => TextSelector, () => null);
        _entries.Changed += (_, _) => PostSync();
        SelectedValueBinding = SelectValueConverter.CreateBinding(() => ValueSelector);
        SelectionChanged += (_, _) => OnSelectionChangedInternal();
        ArcTokens.Bind(this, ChipSurfaceProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, ChipSurfaceMutedProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, ChipBorderProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, ChipBorderStrongProperty, ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(this, ChipAccentProperty, ArcDesignTokenKind.Accent);
        ArcTokens.Bind(this, ChipAccentStrongProperty, ArcDesignTokenKind.AccentStrong);
        ArcTokens.Bind(this, ChipTextProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(this, ChipTextOnProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, ChipTextMutedProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, FocusRingBrushProperty, ArcDesignTokenKind.FocusRing);
    }

    internal ChipGroupColors ChipColors
    {
        get
        {
            if (_colorsDirty)
            {
                _colorsDirty = false;
                _colors = ResolveColors();
            }
            return _colors;
        }
    }

    private ChipGroupColors ResolveColors()
    {
        static Color C(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        var surface = C(GetValue(ChipSurfaceProperty));
        var border = C(GetValue(ChipBorderProperty));
        var accent = C(GetValue(ChipAccentProperty));
        // Selected reads as a quiet tint of the accent plus the check, never color alone.
        return new ChipGroupColors(
            Fill: surface,
            Border: border,
            BorderHover: C(GetValue(ChipBorderStrongProperty)),
            SelectedFill: Oklch.MixOklab(surface, accent, 0.11),
            SelectedBorder: Oklch.MixOklab(border, accent, 0.42),
            SelectedHoverFill: Oklch.MixOklab(surface, accent, 0.16),
            SelectedHoverBorder: Oklch.MixOklab(border, accent, 0.60),
            Text: C(GetValue(ChipTextProperty)),
            TextOn: C(GetValue(ChipTextOnProperty)),
            Check: C(GetValue(ChipAccentStrongProperty)),
            MoreFill: C(GetValue(ChipSurfaceMutedProperty)),
            MoreText: C(GetValue(ChipTextMutedProperty)));
    }

    /// <summary>Toggles the option at <paramref name="index"/> like a click would.</summary>
    public void Toggle(int index)
    {
        if (index < 0 || index >= _entries.Count || !_entries.IsEnabledAt(index))
        {
            return;
        }
        var on = Selection.IsSelected(index);
        if (!IsMultiple)
        {
            using var _ = Selection.BatchUpdate();
            Selection.Clear();
            if (!on)
            {
                Selection.Select(index);
            }
            return;
        }
        if (on)
        {
            Selection.Deselect(index);
        }
        else
        {
            Selection.Select(index);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChipGroupAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _chips.Clear();
        _more = null;
        _panel = e.NameScope.Find<ChipGroupPanel>(PanelPart);
        _pinned.Clear();
        foreach (var index in Selection.SelectedIndexes)
        {
            _pinned.Add(index);
        }
        Sync(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsMultipleProperty)
        {
            SelectionMode = IsMultiple ? SelectionMode.Multiple | SelectionMode.Toggle : SelectionMode.Single | SelectionMode.Toggle;
            if (!IsMultiple && Selection.SelectedIndexes.Count > 1)
            {
                var keep = Selection.SelectedIndexes[0];
                using var _ = Selection.BatchUpdate();
                Selection.Clear();
                Selection.Select(keep);
            }
        }
        else if (change.Property == MaxVisibleItemsProperty)
        {
            PostSync();
        }
        else if (change.Property == IsExpandedProperty)
        {
            if (!change.GetNewValue<bool>())
            {
                // Chips selected when the overflow folds stay in view, so a selection never hides.
                _pinned.Clear();
                foreach (var index in Selection.SelectedIndexes)
                {
                    _pinned.Add(index);
                }
            }
            Sync(animate: true);
        }
        else if (change.Property == TextSelectorProperty || change.Property == DisplayMemberBindingProperty)
        {
            _entries.Invalidate();
        }
        else if (change.Property == ChipSurfaceProperty || change.Property == ChipSurfaceMutedProperty ||
                 change.Property == ChipBorderProperty || change.Property == ChipBorderStrongProperty ||
                 change.Property == ChipAccentProperty || change.Property == ChipAccentStrongProperty ||
                 change.Property == ChipTextProperty || change.Property == ChipTextOnProperty ||
                 change.Property == ChipTextMutedProperty || change.Property == FocusRingBrushProperty)
        {
            _colorsDirty = true;
            foreach (var chip in AllChips())
            {
                chip.InvalidateText();
            }
        }
        else if (change.Property == ChipHeightProperty || change.Property == ChipPaddingProperty ||
                 change.Property == ChipFontWeightProperty || change.Property == FontSizeProperty ||
                 change.Property == FontFamilyProperty)
        {
            foreach (var chip in AllChips())
            {
                chip.InvalidateText();
            }
        }
    }

    private IEnumerable<ChipGroupChip> AllChips() => _panel?.Children.OfType<ChipGroupChip>() ?? Enumerable.Empty<ChipGroupChip>();

    private void PostSync()
    {
        if (_syncPosted)
        {
            return;
        }
        _syncPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _syncPosted = false;
            Sync(animate: IsLoaded);
        }, DispatcherPriority.Background);
    }

    private void OnSelectionChangedInternal()
    {
        SelectedValues = Selection.SelectedIndexes.OrderBy(i => i)
                                  .Select(i => SelectEntrySource.ValueOf(ItemsView[i], ValueSelector)).ToArray();
        Sync(animate: true);
    }

    /// <summary>Builds the visible chip set: folded chips stay hidden unless pinned or selected.</summary>
    private void Sync(bool animate)
    {
        if (_panel is null)
        {
            return;
        }
        var count = _entries.Count;
        var max = Math.Max(0, MaxVisibleItems);
        var foldable = count > max;
        var expanded = IsExpanded;
        var visible = new List<int>();
        for (var i = 0; i < count; i++)
        {
            if (!foldable || expanded || i < max || _pinned.Contains(i) || Selection.IsSelected(i))
            {
                visible.Add(i);
            }
        }
        var hidden = count - visible.Count;
        var showMore = foldable && (expanded || hidden > 0);
        var visibleSet = new HashSet<int>(visible);

        foreach (var (index, chip) in _chips.ToList())
        {
            if (!visibleSet.Contains(index) || index >= count)
            {
                _chips.Remove(index);
                RemoveChip(chip, animate);
            }
        }

        var position = 0;
        foreach (var index in visible)
        {
            var entry = _entries[index];
            if (!_chips.TryGetValue(index, out var chip))
            {
                chip = new ChipGroupChip(this, index, entry.Text, isMore: false) { EntryIndex = index };
                chip.SetSelected(Selection.IsSelected(index), animate: false);
                _chips[index] = chip;
                InsertChip(chip, position);
                if (animate && expanded && index >= max)
                {
                    // Chips revealed by the overflow grow in one after another, capped at 0.3s.
                    var delay = TimeSpan.FromMilliseconds(Math.Min(Math.Max(0, index - max) * 35, 300));
                    chip.PlayEnter(delay);
                }
            }
            else
            {
                chip.SetText(entry.Text);
                chip.SetSelected(Selection.IsSelected(index), animate);
            }
            position++;
        }

        if (showMore)
        {
            if (_more is null)
            {
                _more = new ChipGroupChip(this, MoreKey, string.Empty, isMore: true);
                _panel.Children.Add(_more);
            }
            _more.IsExpanded = expanded;
            _more.SetText(expanded ? "Show less" : $"+{hidden} more");
        }
        else if (_more is not null)
        {
            var more = _more;
            _more = null;
            RemoveChip(more, animate: false);
        }
        UpdateTabStop();
        _panel.InvalidateMeasure();
    }

    private void InsertChip(ChipGroupChip chip, int flowPosition)
    {
        // Insert before the flow chip currently at this position (leaving chips keep their slots in Children).
        var children = _panel!.Children;
        var seen = 0;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is ChipGroupChip { IsLeaving: false } existing && !existing.IsMore)
            {
                if (seen == flowPosition)
                {
                    children.Insert(i, chip);
                    return;
                }
                seen++;
            }
            else if (children[i] is ChipGroupChip { IsMore: true })
            {
                children.Insert(i, chip);
                return;
            }
        }
        children.Add(chip);
    }

    private async void RemoveChip(ChipGroupChip chip, bool animate)
    {
        if (animate)
        {
            await chip.PlayExit();
        }
        _panel?.Children.Remove(chip);
        _panel?.InvalidateMeasure();
    }

    private IReadOnlyList<ChipGroupChip> Focusables() =>
        _panel?.Children.OfType<ChipGroupChip>().Where(c => !c.IsLeaving).ToList() ?? (IReadOnlyList<ChipGroupChip>)Array.Empty<ChipGroupChip>();

    /// <summary>One chip holds the tab stop: the last one focused, else the first selected, else the first.</summary>
    private void UpdateTabStop()
    {
        var chips = Focusables();
        var stop = chips.FirstOrDefault(c => KeyOf(c) == _active)
                   ?? chips.FirstOrDefault(c => !c.IsMore && Selection.IsSelected(c.EntryIndex))
                   ?? chips.FirstOrDefault();
        foreach (var chip in chips)
        {
            chip.IsTabStop = ReferenceEquals(chip, stop);
        }
    }

    private static int KeyOf(ChipGroupChip chip) => chip.IsMore ? MoreKey : chip.EntryIndex;

    internal void OnChipFocused(ChipGroupChip chip)
    {
        _active = KeyOf(chip);
        UpdateTabStop();
    }

    internal void OnChipInvoked(ChipGroupChip chip)
    {
        if (!IsEffectivelyEnabled || chip.IsLeaving)
        {
            return;
        }
        if (chip.IsMore)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
            return;
        }
        Toggle(chip.EntryIndex);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }
        var chips = Focusables();
        var current = chips.ToList().FindIndex(c => c.IsFocused);
        if (current < 0 || chips.Count == 0)
        {
            base.OnKeyDown(e);
            return;
        }
        var last = chips.Count - 1;
        int? target = e.Key switch
        {
            Key.Right or Key.Down => current == last ? 0 : current + 1,
            Key.Left or Key.Up => current == 0 ? last : current - 1,
            Key.Home => 0,
            Key.End => last,
            _ => null
        };
        if (target is { } next)
        {
            e.Handled = true;
            chips[next].Focus(NavigationMethod.Directional);
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>The group is a named Group; chips are pressed / unpressed buttons.</summary>
    private sealed class ChipGroupAutomationPeer : ControlAutomationPeer
    {
        private readonly ChipGroup _group;

        public ChipGroupAutomationPeer(ChipGroup group) : base(group) => _group = group;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? _group.Label : name;
        }
    }
}
