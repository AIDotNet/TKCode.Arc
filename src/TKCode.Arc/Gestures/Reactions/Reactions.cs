using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Arc reactions: a bar of reaction pills with counts plus an add button that opens a compact picker, like Slack's
/// reactions on a comment, post or release note. One tap adds or removes the viewer's reaction: the count rolls (only
/// the digits that change, in the direction it moved), the icon gets a velocity kick that stacks on quick taps (P27),
/// the viewer's own reactions take the accent and a filled glyph. Pills scale in out of a blur and leave the bar when
/// their count reaches zero while the rest glide into place (P26, P19); a new row grows the bar on a spring (P6).
/// Hovering or focusing a pill shows its label and who reacted (<see cref="Tooltip"/>).
/// <code>
/// &lt;arc:Reactions Value="{Binding Reactions}"&gt;
///     &lt;arc:ReactionOption Id="like" Label="Like" Icon="{x:Static arc:ArcIcons.ThumbsUp}" /&gt;
///     &lt;arc:ReactionOption Id="love" Label="Love" Icon="{x:Static arc:ArcIcons.Heart}" /&gt;
/// &lt;/arc:Reactions&gt;
/// </code>
/// Keyboard: Enter / Space toggles a pill or opens the picker; in the picker every arrow moves and wraps, Home / End
/// jump, Enter picks, Escape closes and returns focus to the add button. Each change is announced politely.
/// </summary>
[TemplatePart(BarPart, typeof(ReactionsBar))]
[TemplatePart(AddButtonPart, typeof(Avalonia.Controls.Button))]
public class Reactions : TemplatedControl
{
    public const string BarPart = "PART_Bar";
    public const string AddButtonPart = "PART_AddButton";
    public const string LiveRegionPart = "PART_LiveRegion";

    #region 公共属性定义

    /// <summary>Every reaction the picker offers, in picker order (inline children or a bound list).</summary>
    public static readonly DirectProperty<Reactions, IList<ReactionOption>> OptionsProperty =
        AvaloniaProperty.RegisterDirect<Reactions, IList<ReactionOption>>(nameof(Options), o => o.Options, (o, v) => o.Options = v);

    /// <summary>The reactions shown, in bar order (two-way). A reaction whose count reaches zero leaves the bar.</summary>
    public static readonly StyledProperty<IReadOnlyList<Reaction>?> ValueProperty =
        AvaloniaProperty.Register<Reactions, IReadOnlyList<Reaction>?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Accessible name of the bar (default "Reactions").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Reactions, string>(nameof(Label), "Reactions");

    /// <summary>Name and tooltip of the add button (default "Add reaction").</summary>
    public static readonly StyledProperty<string> AddLabelProperty =
        AvaloniaProperty.Register<Reactions, string>(nameof(AddLabel), "Add reaction");

    /// <summary>Accessible name of the picker tray (default "Add a reaction").</summary>
    public static readonly StyledProperty<string> PickerLabelProperty =
        AvaloniaProperty.Register<Reactions, string>(nameof(PickerLabel), "Add a reaction");

    /// <summary>
    /// Builds a pill's tooltip from its option and reaction; null uses the label followed by who reacted
    /// ("Love · You, Ada and 2 others").
    /// </summary>
    public static readonly StyledProperty<Func<ReactionOption, Reaction, object?>?> TipSelectorProperty =
        AvaloniaProperty.Register<Reactions, Func<ReactionOption, Reaction, object?>?>(nameof(TipSelector));

    /// <summary>How long a pointer rests on a pill before its tooltip shows (the theme sets Arc's 0.45s).</summary>
    public static readonly StyledProperty<TimeSpan> TipDelayProperty =
        AvaloniaProperty.Register<Reactions, TimeSpan>(nameof(TipDelay), TimeSpan.FromMilliseconds(450));

    /// <summary>Gap between the picker and the add button (the theme sets Arc's sideOffset 8).</summary>
    public static readonly StyledProperty<double> PickerOffsetProperty =
        AvaloniaProperty.Register<Reactions, double>(nameof(PickerOffset), 8);

    /// <summary>True while the picker is open.</summary>
    public static readonly DirectProperty<Reactions, bool> IsPickerOpenProperty =
        AvaloniaProperty.RegisterDirect<Reactions, bool>(nameof(IsPickerOpen), o => o.IsPickerOpen);

    private IList<ReactionOption> _options = new AvaloniaList<ReactionOption>();
    // Options may be a view model's list: it is observed only while the bar is shown, and re-read when it comes back.
    private INotifyCollectionChanged? _observedOptions;
    private bool _watching;
    private bool _isPickerOpen;

    [Content]
    public IList<ReactionOption> Options
    {
        get => _options;
        set
        {
            if (SetAndRaise(OptionsProperty, ref _options, value ?? new AvaloniaList<ReactionOption>()))
            {
                WatchOptions();
                OnOptionsChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    public IReadOnlyList<Reaction>? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string AddLabel { get => GetValue(AddLabelProperty); set => SetValue(AddLabelProperty, value); }
    public string PickerLabel { get => GetValue(PickerLabelProperty); set => SetValue(PickerLabelProperty, value); }
    public Func<ReactionOption, Reaction, object?>? TipSelector { get => GetValue(TipSelectorProperty); set => SetValue(TipSelectorProperty, value); }
    public TimeSpan TipDelay { get => GetValue(TipDelayProperty); set => SetValue(TipDelayProperty, value); }
    public double PickerOffset { get => GetValue(PickerOffsetProperty); set => SetValue(PickerOffsetProperty, value); }

    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        private set => SetAndRaise(IsPickerOpenProperty, ref _isPickerOpen, value);
    }

    #endregion

    /// <summary>Raised when the viewer adds or removes a reaction (Arc's onValueChange), after <see cref="Value"/> changed.</summary>
    public event EventHandler<ReactionsValueChangedEventArgs>? ValueChanged;

    private readonly Dictionary<string, ReactionsPill> _pills = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReactionsPill> _leaving = new(StringComparer.Ordinal);
    private readonly Stack<ReactionsPill> _pool = new();
    private readonly List<ReactionsPill> _order = new();
    private ReactionsBar? _bar;
    private ReactionsAddButton? _addButton;
    private ReactionsLiveRegion? _liveRegion;
    private Popover? _popover;
    private ReactionsPicker? _picker;
    private bool _surfaceStyled;

    static Reactions()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Reactions>(AutomationControlType.Group);
    }

    public Reactions()
    {
        AutomationProperties.SetName(this, Label);
    }

    /// <summary>The announcement of the last change ("Added Love. 4 reactions.").</summary>
    public string LastAnnouncement => _liveRegion?.Message ?? string.Empty;

    internal ReactionsBar? Bar => _bar;

    internal ReactionsAddButton? AddButton => _addButton;

    internal Popover? PickerPopover => _popover;

    internal ReactionsPicker? Picker => _picker;

    /// <summary>The pill showing <paramref name="id"/> (null when it is not in the bar).</summary>
    internal ReactionsPill? PillFor(string id) => _pills.TryGetValue(id, out var pill) ? pill : null;

    internal IReadOnlyList<ReactionsPill> Pills => _order;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_addButton is not null)
        {
            _addButton.Click -= OnAddClick;
        }
        if (_bar is not null)
        {
            foreach (var pill in _pills.Values.Concat(_leaving.Values))
            {
                _bar.Children.Remove(pill);
            }
        }
        _pills.Clear();
        _leaving.Clear();
        _order.Clear();
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<ReactionsBar>(BarPart);
        _addButton = e.NameScope.Find<ReactionsAddButton>(AddButtonPart);
        _liveRegion = e.NameScope.Find<ReactionsLiveRegion>(LiveRegionPart);
        if (_addButton is not null)
        {
            _addButton.Click += OnAddClick;
            UpdateAddButton();
        }
        Sync(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            Sync(animate: true);
            UpdatePicker();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == AddLabelProperty)
        {
            UpdateAddButton();
        }
        else if (change.Property == PickerLabelProperty && _popover is not null)
        {
            AutomationProperties.SetName(_popover, PickerLabel);
        }
        else if (change.Property == TipSelectorProperty || change.Property == TipDelayProperty)
        {
            Sync(animate: false);
        }
    }

    private void OnOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Sync(animate: false);
        UpdatePicker();
    }

    /// <summary>Listens to the current options while shown (and to nothing otherwise).</summary>
    private void WatchOptions()
    {
        if (_observedOptions is not null)
        {
            _observedOptions.CollectionChanged -= OnOptionsChanged;
        }
        _observedOptions = _watching ? _options as INotifyCollectionChanged : null;
        if (_observedOptions is not null)
        {
            _observedOptions.CollectionChanged += OnOptionsChanged;
        }
    }

    #endregion

    #region Pills

    private Dictionary<string, ReactionOption> OptionMap()
    {
        var map = new Dictionary<string, ReactionOption>(StringComparer.Ordinal);
        foreach (var option in _options)
        {
            map.TryAdd(option.Id, option);
        }
        return map;
    }

    /// <summary>The reactions the bar shows: a positive count and a known option, first entry per id, in value order.</summary>
    private List<Reaction> Shown(Dictionary<string, ReactionOption> options)
    {
        var shown = new List<Reaction>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reaction in Value ?? Array.Empty<Reaction>())
        {
            if (reaction is { Count: > 0 } && options.ContainsKey(reaction.Id) && seen.Add(reaction.Id))
            {
                shown.Add(reaction);
            }
        }
        return shown;
    }

    private ArcMotionLevel Level => ArcMotion.GetEffectiveLevel(this);

    private void Sync(bool animate)
    {
        if (_bar is null || _addButton is null)
        {
            return;
        }
        animate &= IsLoaded;
        var level = Level;
        var options = OptionMap();
        var shown = Shown(options);
        var structural = false;
        _order.Clear();
        var keep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reaction in shown)
        {
            keep.Add(reaction.Id);
            if (!_pills.TryGetValue(reaction.Id, out var pill))
            {
                var fresh = !_leaving.Remove(reaction.Id, out var revived);
                if (!fresh)
                {
                    pill = revived!;
                }
                else
                {
                    pill = _pool.Count > 0 ? _pool.Pop() : CreatePill();
                    pill.Id = reaction.Id;
                }
                _pills[reaction.Id] = pill;
                if (!_bar.Children.Contains(pill))
                {
                    _bar.Children.Insert(Math.Max(0, _bar.Children.Count - 1), pill);
                }
                pill.Enter(level, animate, fresh);
                structural = true;
            }
            UpdatePill(pill, options[reaction.Id], reaction);
            _order.Add(pill);
        }
        foreach (var (id, pill) in _pills.ToArray())
        {
            if (!keep.Contains(id))
            {
                _pills.Remove(id);
                _leaving[id] = pill;
                structural = true;
                _ = LeaveAsync(pill, level, animate);
            }
        }
        Reorder();
        if (structural && animate)
        {
            _bar.ArmLayoutMotion();
        }
    }

    private ReactionsPill CreatePill()
    {
        var pill = new ReactionsPill();
        pill.Click += OnPillClick;
        return pill;
    }

    private async Task LeaveAsync(ReactionsPill pill, ArcMotionLevel level, bool animate)
    {
        var gone = await pill.LeaveAsync(level, animate);
        if (!gone || !_leaving.TryGetValue(pill.Id, out var current) || !ReferenceEquals(current, pill))
        {
            return;
        }
        _leaving.Remove(pill.Id);
        _bar?.Children.Remove(pill);
        Tooltip.SetTip(pill, null);
        _pool.Push(pill);
    }

    /// <summary>Bar order: shown pills in value order, leaving pills (out of the flow), then the add button.</summary>
    private void Reorder()
    {
        if (_bar is null || _addButton is null)
        {
            return;
        }
        var children = _bar.Children;
        var index = 0;
        foreach (var pill in _order)
        {
            MoveTo(children, pill, index++);
        }
        foreach (var pill in _leaving.Values)
        {
            MoveTo(children, pill, index++);
        }
        MoveTo(children, _addButton, index);
    }

    private static void MoveTo(Controls children, Control child, int index)
    {
        var current = children.IndexOf(child);
        if (current >= 0 && current != index && index < children.Count)
        {
            children.Move(current, index);
        }
    }

    private void UpdatePill(ReactionsPill pill, ReactionOption option, Reaction reaction)
    {
        pill.Icon = option.Icon;
        pill.Count = reaction.Count;
        pill.IsChecked = reaction.Reacted;
        var noun = reaction.Count == 1 ? "reaction" : "reactions";
        AutomationProperties.SetName(pill, $"{option.Label}, {reaction.Count} {noun}{(reaction.Reacted ? ", including yours" : string.Empty)}");
        Tooltip.SetShowDelay(pill, TipDelay);
        Tooltip.SetTip(pill, TipSelector?.Invoke(option, reaction) ?? DefaultTip(option, reaction));
    }

    /// <summary>"Love" or, when names are known, "Love · You, Ada and 2 others".</summary>
    internal static string DefaultTip(ReactionOption option, Reaction reaction)
    {
        var names = new List<string>(4);
        if (reaction.Reacted)
        {
            names.Add("You");
        }
        if (reaction.ReactedBy is { } others)
        {
            foreach (var name in others)
            {
                if (names.Count == 3)
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
        }
        if (reaction.ReactedBy is not { Count: > 0 })
        {
            return option.Label;
        }
        var rest = Math.Max(0, reaction.Count - names.Count);
        string who;
        if (rest > 0)
        {
            who = $"{string.Join(", ", names)} and {rest} {(rest == 1 ? "other" : "others")}";
        }
        else if (names.Count > 1)
        {
            who = $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
        }
        else
        {
            who = names[0];
        }
        return $"{option.Label} · {who}";
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ReactionsPill { IsLeaving: false } pill)
        {
            e.Handled = true;
            Toggle(pill.Id);
        }
    }

    /// <summary>
    /// Adds or removes the viewer's reaction <paramref name="id"/> (one tap): the count changes by one, a reaction that
    /// reaches zero leaves, a new one joins the end with a count of one. Raises <see cref="ValueChanged"/> and announces it.
    /// </summary>
    public void Toggle(string id)
    {
        var options = OptionMap();
        if (!options.TryGetValue(id, out var option))
        {
            return;
        }
        var list = Value ?? Array.Empty<Reaction>();
        var shown = Shown(options);
        var current = shown.FirstOrDefault(r => r.Id == id);
        var add = current is not { Reacted: true };
        var count = Math.Max(0, (current?.Count ?? 0) + (add ? 1 : -1));
        List<Reaction> next;
        if (current is not null)
        {
            next = list.Select(r => r.Id == id ? r with { Count = count, Reacted = add } : r).Where(r => r.Count > 0).ToList();
        }
        else
        {
            next = list.Where(r => r.Id != id).ToList();
            next.Add(new Reaction(id, 1, true));
        }

        // A pill that leaves while focused hands focus to its neighbour, or to the add button.
        Control? refocus = null;
        var keyboard = false;
        if (count == 0 && _pills.TryGetValue(id, out var leaving) && leaving.IsFocused)
        {
            keyboard = leaving.Classes.Contains(":focus-visible");
            var index = shown.FindIndex(r => r.Id == id);
            var neighbour = index + 1 < shown.Count ? shown[index + 1].Id : index > 0 ? shown[index - 1].Id : null;
            refocus = neighbour is not null && _pills.TryGetValue(neighbour, out var pill) ? pill : _addButton;
        }

        SetCurrentValue(ValueProperty, next);
        if (add && _pills.TryGetValue(id, out var kicked))
        {
            kicked.Kick();
        }
        refocus?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        var noun = count == 1 ? "reaction" : "reactions";
        _liveRegion?.Announce(add
            ? $"Added {option.Label}. {count} {noun}."
            : count > 0 ? $"Removed {option.Label}. {count} {noun}." : $"Removed {option.Label}.");
        ValueChanged?.Invoke(this, new ReactionsValueChangedEventArgs(next, new ReactionChange(id, add)));
    }

    #endregion

    #region Picker

    private void UpdateAddButton()
    {
        if (_addButton is null)
        {
            return;
        }
        AutomationProperties.SetName(_addButton, AddLabel);
        Tooltip.SetShowDelay(_addButton, TipDelay);
        Tooltip.SetTip(_addButton, IsPickerOpen ? null : AddLabel);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsPickerOpen)
        {
            ClosePicker();
        }
        else
        {
            OpenPicker();
        }
    }

    /// <summary>Opens the picker above the add button.</summary>
    public void OpenPicker()
    {
        if (_addButton is null || TopLevel.GetTopLevel(_addButton) is null || IsPickerOpen)
        {
            return;
        }
        EnsurePicker();
        UpdatePicker();
        _picker!.Reset(Level);
        _popover!.Offset = PickerOffset;
        _popover.ShowAt(_addButton);
        if (_popover.Surface is { } surface && !_surfaceStyled)
        {
            // The tray is compact: Arc pads it 5px and lets the options set its width.
            _surfaceStyled = true;
            surface.Padding = _picker.SurfacePadding;
            surface.MinWidth = 0;
        }
        // The roving option takes focus (the first one on every open).
        _picker.ApplyTemplate();
        _picker.FocusActive(_addButton.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Unspecified);
    }

    /// <summary>Closes the picker.</summary>
    public void ClosePicker() => _popover?.Close();

    private void EnsurePicker()
    {
        if (_popover is not null)
        {
            return;
        }
        _picker = new ReactionsPicker();
        AutomationProperties.SetName(_picker, Label);
        _picker.Picked += OnPicked;
        _popover = new Popover
        {
            Content = _picker,
            Placement = PopoverPlacement.Top,
            Alignment = PopoverAlignment.Center
        };
        AutomationProperties.SetName(_popover, PickerLabel);
        _popover.Opened += (_, _) => SetPickerOpen(true);
        _popover.Closed += (_, _) => SetPickerOpen(false);
        LogicalChildren.Add(_popover);
    }

    private void SetPickerOpen(bool open)
    {
        IsPickerOpen = open;
        if (_addButton is not null)
        {
            _addButton.IsOpen = open;
        }
        UpdateAddButton();
    }

    private void UpdatePicker()
    {
        if (_picker is null)
        {
            return;
        }
        var reacted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reaction in Shown(OptionMap()))
        {
            if (reaction.Reacted)
            {
                reacted.Add(reaction.Id);
            }
        }
        _picker.SetOptions(_options as IReadOnlyList<ReactionOption> ?? _options.ToList(), reacted);
    }

    private void OnPicked(object? sender, string id)
    {
        var keyboard = _picker?.PickedWithKeyboard == true;
        ClosePicker();
        _addButton?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        Toggle(id);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchOptions();
        // The options may have changed while the bar was away.
        Sync(animate: false);
        UpdatePicker();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchOptions();
        if (IsPickerOpen)
        {
            ClosePicker();
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ReactionsAutomationPeer(this);

    private sealed class ReactionsAutomationPeer : ControlAutomationPeer
    {
        public ReactionsAutomationPeer(Reactions owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((Reactions)Owner).Label;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
