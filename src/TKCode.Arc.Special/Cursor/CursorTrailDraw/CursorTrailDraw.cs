using System.Globalization;
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
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A whiteboard for quick sketches and live walkthroughs (Arc Cursor trail draw, design reference). Ink follows the
/// pointer smoothly and takes its width from pen pressure, or from speed for a mouse or finger, tapering at both ends;
/// the laser pointer leaves a trail that thins and fades on its own; and a rough circle, ellipse, box, square, triangle,
/// four sided shape or line snaps to a clean shape on a small spring when the pen lifts, with a chip that offers to keep
/// it as drawn.
/// </summary>
/// <remarks>
/// The board is always 1000 units wide (<see cref="CursorTrailDrawItem"/> coordinates) and 16:10, or 4:5 when narrower
/// than 560 px; ink widths stay in px. The first time the board is in view it plays a short sketch
/// (<see cref="PlaysIntro"/>); any press or key takes over at once. Keyboard: arrow keys move the cursor (Shift slowly),
/// Space or Enter puts the pen down and lifts it, Escape cancels a stroke, P and L pick the pen and the laser, S toggles
/// snapping, Ctrl or Cmd+Z undoes. Under reduced motion, or when <see cref="IsPaused"/>, the sketch is shown at once,
/// shapes snap without the spring and the laser shows only its dot; drawing works the same.
/// </remarks>
[TemplatePart(BoardPart, typeof(CursorTrailDrawBoard))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(CountPart, typeof(TextBlock))]
[TemplatePart(ChipPart, typeof(Border))]
[TemplatePart(ChipTextPart, typeof(TextBlock))]
[TemplatePart(KeepPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(PenPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(LaserPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(SwatchesPart, typeof(Panel))]
[TemplatePart(SnapPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(UndoPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(ClearPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, LaserPseudoClass)]
public class CursorTrailDraw : TemplatedControl
{
    public const string BoardPart = "PART_Board";
    public const string HintPart = "PART_Hint";
    public const string CountPart = "PART_Count";
    public const string ChipPart = "PART_Chip";
    public const string ChipTextPart = "PART_ChipText";
    public const string KeepPart = "PART_Keep";
    public const string PenPart = "PART_Pen";
    public const string LaserPart = "PART_Laser";
    public const string SwatchesPart = "PART_Swatches";
    public const string SnapPart = "PART_Snap";
    public const string UndoPart = "PART_Undo";
    public const string ClearPart = "PART_Clear";
    public const string StatusPart = "PART_Status";
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";
    public const string LaserPseudoClass = ":laser";

    /// <summary>Heading and accessible name of the board.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CursorTrailDraw, string>(nameof(Label), "Whiteboard");

    /// <summary>The tool in use; people switch it in the toolbar or with P and L.</summary>
    public static readonly StyledProperty<CursorTrailDrawTool> ToolProperty =
        AvaloniaProperty.Register<CursorTrailDraw, CursorTrailDrawTool>(nameof(Tool), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The <see cref="CursorTrailDrawInk.Id"/> of the ink in use; null picks the first ink.</summary>
    public static readonly StyledProperty<string?> SelectedInkIdProperty =
        AvaloniaProperty.Register<CursorTrailDraw, string?>(nameof(SelectedInkId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether rough shapes snap clean when the pen lifts; people switch it in the toolbar or with S.</summary>
    public static readonly StyledProperty<bool> IsSnapEnabledProperty =
        AvaloniaProperty.Register<CursorTrailDraw, bool>(nameof(IsSnapEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Pen width in px at half pressure; strokes thin with speed and taper at both ends.</summary>
    public static readonly StyledProperty<double> PenSizeProperty =
        AvaloniaProperty.Register<CursorTrailDraw, double>(nameof(PenSize), 4);

    /// <summary>How far the ink trails the pointer, from 0 (raw) to 1 (very smooth).</summary>
    public static readonly StyledProperty<double> SmoothingProperty =
        AvaloniaProperty.Register<CursorTrailDraw, double>(nameof(Smoothing), 0.5);

    /// <summary>How long the laser trail takes to fade.</summary>
    public static readonly StyledProperty<TimeSpan> LaserFadeProperty =
        AvaloniaProperty.Register<CursorTrailDraw, TimeSpan>(nameof(LaserFade), TimeSpan.FromMilliseconds(700));

    /// <summary>
    /// Whether the board plays a short sketch once, the first time it is in view, when it starts empty: a rough box and
    /// circle snap clean and get joined, then the laser points at them. Turn it off where the board holds real work.
    /// </summary>
    public static readonly StyledProperty<bool> PlaysIntroProperty =
        AvaloniaProperty.Register<CursorTrailDraw, bool>(nameof(PlaysIntro), true);

    /// <summary>Multiplies the speed of the snap spring, the laser fade and the intro.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<CursorTrailDraw, double>(nameof(Speed), 1);

    /// <summary>Stops all animation: shapes land at once and the laser shows only its dot. Drawing still works.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<CursorTrailDraw, bool>(nameof(IsPaused));

    /// <summary>Color of the accent ink, the laser and the picked tool; null uses the theme accent.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<CursorTrailDraw, IBrush?>(nameof(AccentBrush));

    /// <summary>The line shown on an empty board; an empty string hides it.</summary>
    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<CursorTrailDraw, string>(nameof(Hint), "Sketch a rough circle, box or line and it tidies itself up.");

    /// <summary>The items on the board, oldest first (read-only; use <see cref="SetItems"/> to load a sketch).</summary>
    public static readonly DirectProperty<CursorTrailDraw, IReadOnlyList<CursorTrailDrawItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<CursorTrailDraw, IReadOnlyList<CursorTrailDrawItem>>(nameof(Items), o => o.Items);

    /// <summary>Raised after a stroke lands or snaps, is kept as drawn, undone or cleared.</summary>
    public static readonly RoutedEvent<CursorTrailDrawItemsChangedEventArgs> ItemsChangedEvent =
        RoutedEvent.Register<CursorTrailDraw, CursorTrailDrawItemsChangedEventArgs>(nameof(ItemsChanged), RoutingStrategies.Bubble);

    private static readonly CursorTrailDrawInk[] s_defaultInks =
    [
        new("ink", "Ink"),
        new("accent", "Accent"),
        new("red", "Red"),
    ];

    private readonly AvaloniaList<CursorTrailDrawInk> _inks = new();
    private readonly List<CursorTrailDrawToolButton> _swatchButtons = new();
    private IReadOnlyList<CursorTrailDrawItem> _items = Array.Empty<CursorTrailDrawItem>();
    private CursorTrailDrawBoard? _board;
    private TextBlock? _hint;
    private TextBlock? _count;
    private Border? _chip;
    private TextBlock? _chipText;
    private Avalonia.Controls.Button? _keep;
    private CursorTrailDrawToolButton? _pen;
    private CursorTrailDrawToolButton? _laser;
    private Panel? _swatches;
    private CursorTrailDrawToolButton? _snap;
    private Avalonia.Controls.Button? _undo;
    private Avalonia.Controls.Button? _clear;
    private TextBlock? _status;
    private BoardItem? _chipItem;
    private DispatcherTimer? _chipTimer;
    private MotionValue<double>? _chipOpacity;
    private MotionValue<double>? _chipLift;
    private bool _chipShown;
    private bool _introDone;
    private int _announceCount;

    static CursorTrailDraw()
    {
        FocusableProperty.OverrideDefaultValue<CursorTrailDraw>(false);
    }

    public CursorTrailDraw()
    {
        _inks.CollectionChanged += (_, _) => RebuildSwatches();
        AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, (_, _) => StopIntro(), RoutingStrategies.Tunnel);
    }

    /// <summary>Raised after a stroke lands or snaps, is kept as drawn, undone or cleared.</summary>
    public event EventHandler<CursorTrailDrawItemsChangedEventArgs>? ItemsChanged
    {
        add => AddHandler(ItemsChangedEvent, value);
        remove => RemoveHandler(ItemsChangedEvent, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ToolProperty"/>
    public CursorTrailDrawTool Tool { get => GetValue(ToolProperty); set => SetValue(ToolProperty, value); }

    /// <inheritdoc cref="SelectedInkIdProperty"/>
    public string? SelectedInkId { get => GetValue(SelectedInkIdProperty); set => SetValue(SelectedInkIdProperty, value); }

    /// <inheritdoc cref="IsSnapEnabledProperty"/>
    public bool IsSnapEnabled { get => GetValue(IsSnapEnabledProperty); set => SetValue(IsSnapEnabledProperty, value); }

    /// <inheritdoc cref="PenSizeProperty"/>
    public double PenSize { get => GetValue(PenSizeProperty); set => SetValue(PenSizeProperty, value); }

    /// <inheritdoc cref="SmoothingProperty"/>
    public double Smoothing { get => GetValue(SmoothingProperty); set => SetValue(SmoothingProperty, value); }

    /// <inheritdoc cref="LaserFadeProperty"/>
    public TimeSpan LaserFade { get => GetValue(LaserFadeProperty); set => SetValue(LaserFadeProperty, value); }

    /// <inheritdoc cref="PlaysIntroProperty"/>
    public bool PlaysIntro { get => GetValue(PlaysIntroProperty); set => SetValue(PlaysIntroProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="HintProperty"/>
    public string Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <summary>
    /// The inks in the toolbar, in order; the first is picked first. Empty uses Ink (the foreground), Accent and Red.
    /// </summary>
    public AvaloniaList<CursorTrailDrawInk> Inks => _inks;

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<CursorTrailDrawItem> Items
    {
        get => _items;
        private set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    internal CursorTrailDrawBoard? Board => _board;

    internal CursorTrailDrawTexts Texts { get; } = new();

    internal IReadOnlyList<CursorTrailDrawInk> EffectiveInks => _inks.Count > 0 ? _inks : s_defaultInks;

    internal string CurrentInkId
    {
        get
        {
            var inks = EffectiveInks;
            if (SelectedInkId is { } id && inks.Any(i => i.Id == id))
            {
                return id;
            }
            return inks.FirstOrDefault(i => i.IsVisible && i.IsEnabled)?.Id ?? inks.FirstOrDefault()?.Id ?? "ink";
        }
    }

    internal IBrush? LaserBrush => AccentBrush ?? _board?.AccentInkBrush;

    /// <summary>The last message in the live region (without the repeat marker).</summary>
    internal string LastAnnouncement { get; private set; } = string.Empty;

    internal bool IsChipShown => _chipShown;

    /// <summary>Removes the last item.</summary>
    public void Undo()
    {
        if (_board is null)
        {
            return;
        }
        StopIntro();
        var last = _board.Items.LastOrDefault(i => !i.Removing);
        if (last is null)
        {
            Announce(Texts.NothingToUndo);
            return;
        }
        var removed = last.ToPublic();
        if (ReferenceEquals(last, _chipItem))
        {
            HideChip();
        }
        _board.RemoveLast(animate: true);
        Announce(string.Format(CultureInfo.InvariantCulture, Texts.Removed, Texts.ShapeName(removed.Shape)));
        Publish(CursorTrailDrawChange.Undone, removed);
    }

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        if (_board is null)
        {
            return;
        }
        StopIntro();
        HideChip();
        _board.RemoveAll(animate: true);
        Announce(Texts.Cleared);
        Publish(CursorTrailDrawChange.Cleared, null);
    }

    /// <summary>Turns the shape that snapped last back into the stroke as drawn (what the chip's button does).</summary>
    public void KeepAsDrawn()
    {
        if (_board is null || _chipItem is null)
        {
            return;
        }
        var item = _board.KeepAsDrawn(_chipItem);
        HideChip();
        if (item is not null)
        {
            Announce(Texts.Kept);
            Publish(CursorTrailDrawChange.KeptAsDrawn, item.ToPublic());
        }
    }

    /// <summary>Replaces the items on the board, for example with a saved sketch; does not raise <see cref="ItemsChanged"/>.</summary>
    public void SetItems(IEnumerable<CursorTrailDrawItem> items)
    {
        StopIntro();
        _introDone = true;
        HideChip();
        var list = items.ToList();
        if (_board is null)
        {
            _pendingItems = list;
            Items = list;
            UpdateBoardState();
            return;
        }
        _board.SetItems(list);
        Items = _board.Items.Select(i => i.ToPublic()).ToList();
        UpdateBoardState();
    }

    private List<CursorTrailDrawItem>? _pendingItems;

    /// <summary>Plays the intro sketch again on an empty board (or shows it at once without motion).</summary>
    public void PlayIntro()
    {
        if (_board is null)
        {
            return;
        }
        _introDone = true;
        if (_board.Items.Count > 0)
        {
            return;
        }
        if (Animates)
        {
            _board.StartIntro();
        }
        else
        {
            _board.PlaceIntroSketch();
        }
        UpdateBoardState();
    }

    internal bool Animates => !IsPaused && ArcMotion.IsFull(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _board = e.NameScope.Find<CursorTrailDrawBoard>(BoardPart);
        _hint = e.NameScope.Find<TextBlock>(HintPart);
        _count = e.NameScope.Find<TextBlock>(CountPart);
        _chip = e.NameScope.Find<Border>(ChipPart);
        _chipText = e.NameScope.Find<TextBlock>(ChipTextPart);
        _keep = e.NameScope.Find<Avalonia.Controls.Button>(KeepPart);
        _pen = e.NameScope.Find<CursorTrailDrawToolButton>(PenPart);
        _laser = e.NameScope.Find<CursorTrailDrawToolButton>(LaserPart);
        _swatches = e.NameScope.Find<Panel>(SwatchesPart);
        _snap = e.NameScope.Find<CursorTrailDrawToolButton>(SnapPart);
        _undo = e.NameScope.Find<Avalonia.Controls.Button>(UndoPart);
        _clear = e.NameScope.Find<Avalonia.Controls.Button>(ClearPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_board is not null)
        {
            _board.Owner = this;
            if (_pendingItems is not null)
            {
                _board.SetItems(_pendingItems);
                _pendingItems = null;
            }
        }
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        if (_chip is not null)
        {
            _chip.IsVisible = false;
            _chip.Opacity = 0;
        }
        Hook(_pen, OnPenClick);
        Hook(_laser, OnLaserClick);
        Hook(_snap, OnSnapClick);
        Hook(_undo, OnUndoClick);
        Hook(_clear, OnClearClick);
        Hook(_keep, OnKeepClick);
        RebuildSwatches();
        UpdateTexts();
        UpdateToolState();
        UpdateBoardState();
    }

    private void Hook(Avalonia.Controls.Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
            _hooked.Add((button, handler));
        }
    }

    private readonly List<(Avalonia.Controls.Button Button, EventHandler<RoutedEventArgs> Handler)> _hooked = new();

    private void Detach()
    {
        foreach (var (button, handler) in _hooked)
        {
            button.Click -= handler;
        }
        _hooked.Clear();
        if (_board is not null)
        {
            _board.Owner = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(BeginIntroIfWanted, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _chipTimer?.Stop();
    }

    private void BeginIntroIfWanted()
    {
        if (_introDone || !PlaysIntro || _board is null || _board.Items.Count > 0 || VisualRoot is null)
        {
            return;
        }
        _introDone = true;
        if (Animates)
        {
            // The board's loop only runs while it is in view, so the sketch starts the first time people can see it.
            _board.StartIntro();
        }
        else
        {
            _board.PlaceIntroSketch();
        }
        UpdateBoardState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ToolProperty)
        {
            UpdateToolState();
            _board?.Refresh();
        }
        else if (property == SelectedInkIdProperty || property == AccentBrushProperty)
        {
            UpdateToolState();
            _board?.InvalidateVisual();
        }
        else if (property == IsSnapEnabledProperty)
        {
            UpdateToolState();
        }
        else if (property == LabelProperty || property == HintProperty)
        {
            UpdateTexts();
            UpdateBoardState();
        }
        else if (property == IsPausedProperty || property == ArcMotion.LevelProperty)
        {
            if (!Animates)
            {
                StopIntroForMotion();
            }
            _board?.Refresh();
        }
        else if (property == BoundsProperty)
        {
            var width = change.GetNewValue<Rect>().Width - Padding.Left - Padding.Right;
            var token = CompactBreakpoints();
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < token.Compact);
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width < token.Narrow);
        }
    }

    private (double Compact, double Narrow) CompactBreakpoints() =>
        (Token(CursorTrailDrawTokenKind.CompactWidth, 560), Token(CursorTrailDrawTokenKind.NarrowWidth, 360));

    private double Token(CursorTrailDrawTokenKind kind, double fallback) =>
        this.TryFindResource(CursorTrailDrawTokens.Key(kind), out var value) && value is double d ? d : fallback;

    private void StopIntroForMotion()
    {
        if (_board is { IsIntroPlaying: true } board)
        {
            board.StopIntro();
            board.SetItems(Array.Empty<CursorTrailDrawItem>());
            board.PlaceIntroSketch();
        }
    }

    // --- board callbacks -------------------------------------------------------------------------------------------

    internal void StopIntro()
    {
        if (_board is { IsIntroPlaying: true } board)
        {
            board.StopIntro();
            UpdateBoardState();
        }
    }

    internal void OnStrokeStarted()
    {
        HideChip();
        UpdateBoardState();
    }

    internal void OnLanded(BoardItem item, bool fromIntro)
    {
        var snapped = item.Clean is not null;
        var published = item.ToPublic();
        if (snapped)
        {
            ShowChip(item);
            if (!fromIntro)
            {
                Announce(string.Format(CultureInfo.InvariantCulture, Texts.Snapped, Texts.ShapeName(item.Shape)));
            }
        }
        else if (!fromIntro)
        {
            Announce(Texts.Added);
        }
        Publish(snapped ? CursorTrailDrawChange.Snapped : CursorTrailDrawChange.Added, published);
    }

    internal void OnIntroPlaced()
    {
        Items = _board?.Items.Select(i => i.ToPublic()).ToList() ?? (IReadOnlyList<CursorTrailDrawItem>)Array.Empty<CursorTrailDrawItem>();
        UpdateBoardState();
    }

    internal void OnIntroFinished() => UpdateBoardState();

    internal string IntroInkId(int index)
    {
        var inks = EffectiveInks;
        if (index == 1)
        {
            return inks.FirstOrDefault(i => i.Id == "accent")?.Id ?? (inks.Count > 1 ? inks[1].Id : CurrentInkId);
        }
        return inks.Count > 0 ? inks[0].Id : CurrentInkId;
    }

    internal IBrush? BrushFor(string inkId)
    {
        var board = _board;
        var ink = EffectiveInks.FirstOrDefault(i => i.Id == inkId);
        if (ink?.Brush is { } brush)
        {
            return brush;
        }
        return inkId switch
        {
            "accent" => AccentBrush ?? board?.AccentInkBrush,
            "red" => board?.DangerInkBrush,
            _ => board?.InkBrush,
        };
    }

    internal void Announce(string message)
    {
        LastAnnouncement = message;
        if (_status is null)
        {
            return;
        }
        _announceCount++;
        // A zero width space makes a repeated message a change, so it is read again.
        _status.Text = _announceCount % 2 == 0 ? message + "​" : message;
    }

    private void Publish(CursorTrailDrawChange change, CursorTrailDrawItem? item)
    {
        var live = _board?.Items.Where(i => !i.Removing).Select(i => i.ToPublic()).ToList() ?? new List<CursorTrailDrawItem>();
        Items = live;
        UpdateBoardState();
        RaiseEvent(new CursorTrailDrawItemsChangedEventArgs(ItemsChangedEvent, this, change, item, live));
    }

    // --- toolbar --------------------------------------------------------------------------------------------------

    private void OnPenClick(object? sender, RoutedEventArgs e) => PickTool(CursorTrailDrawTool.Pen);

    private void OnLaserClick(object? sender, RoutedEventArgs e) => PickTool(CursorTrailDrawTool.Laser);

    private void OnSnapClick(object? sender, RoutedEventArgs e) => ToggleSnap();

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Undo();

    private void OnClearClick(object? sender, RoutedEventArgs e) => Clear();

    private void OnKeepClick(object? sender, RoutedEventArgs e) => KeepAsDrawn();

    private void PickTool(CursorTrailDrawTool tool)
    {
        StopIntro();
        SetCurrentValue(ToolProperty, tool);
        Announce(tool == CursorTrailDrawTool.Pen ? Texts.PenPicked : Texts.LaserPicked);
    }

    private void ToggleSnap()
    {
        SetCurrentValue(IsSnapEnabledProperty, !IsSnapEnabled);
        Announce(IsSnapEnabled ? Texts.SnapOn : Texts.SnapOff);
    }

    private void PickInk(CursorTrailDrawInk ink)
    {
        StopIntro();
        SetCurrentValue(SelectedInkIdProperty, ink.Id);
        Announce(string.Format(CultureInfo.InvariantCulture, Texts.InkPicked, ink.Label));
    }

    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is TextBox)
        {
            return;
        }
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            Undo();
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None && e.KeyModifiers != KeyModifiers.Shift)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.P:
                e.Handled = true;
                PickTool(CursorTrailDrawTool.Pen);
                break;
            case Key.L:
                e.Handled = true;
                PickTool(CursorTrailDrawTool.Laser);
                break;
            case Key.S:
                e.Handled = true;
                StopIntro();
                ToggleSnap();
                break;
        }
    }

    private void RebuildSwatches()
    {
        if (_swatches is null)
        {
            return;
        }
        foreach (var button in _swatchButtons)
        {
            button.Click -= OnSwatchClick;
        }
        _swatchButtons.Clear();
        _swatches.Children.Clear();
        foreach (var ink in EffectiveInks)
        {
            if (!ink.IsVisible)
            {
                continue;
            }
            var swatch = new CursorTrailDrawSwatch();
            var button = new CursorTrailDrawToolButton
            {
                IsToggle = true,
                Tag = ink,
                Content = swatch,
                IsEnabled = ink.IsEnabled,
            };
            button.Classes.Add("swatch");
            button.Click += OnSwatchClick;
            _swatchButtons.Add(button);
            _swatches.Children.Add(button);
        }
        UpdateToolState();
    }

    private void OnSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CursorTrailDrawToolButton { Tag: CursorTrailDrawInk ink })
        {
            PickInk(ink);
        }
    }

    private void UpdateToolState()
    {
        if (_pen is not null)
        {
            _pen.IsChecked = Tool == CursorTrailDrawTool.Pen;
        }
        if (_laser is not null)
        {
            _laser.IsChecked = Tool == CursorTrailDrawTool.Laser;
        }
        if (_snap is not null)
        {
            _snap.IsChecked = IsSnapEnabled;
        }
        PseudoClasses.Set(LaserPseudoClass, Tool == CursorTrailDrawTool.Laser);
        var current = CurrentInkId;
        foreach (var button in _swatchButtons)
        {
            if (button.Tag is not CursorTrailDrawInk ink)
            {
                continue;
            }
            var selected = ink.Id == current;
            button.IsChecked = selected;
            var name = string.Format(CultureInfo.InvariantCulture, Texts.InkName, ink.Label);
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            if (button.Content is CursorTrailDrawSwatch swatch)
            {
                swatch.IsSelected = selected;
                swatch.Fill = BrushFor(ink.Id);
            }
        }
    }

    private void UpdateTexts()
    {
        AutomationProperties.SetName(this, Label);
        if (_board is not null)
        {
            AutomationProperties.SetName(_board, string.Format(CultureInfo.InvariantCulture, Texts.CanvasName, Label));
        }
        if (_hint is not null)
        {
            _hint.Text = Hint;
        }
    }

    private void UpdateBoardState()
    {
        var board = _board;
        var live = board?.Items.Count(i => !i.Removing) ?? Items.Count;
        if (_count is not null)
        {
            _count.Text = string.Format(CultureInfo.InvariantCulture, live == 1 ? Texts.CountOne : Texts.CountMany, live);
        }
        if (_hint is not null)
        {
            var empty = live == 0 && board is not { IsIntroPlaying: true } && board is not { IsDrawing: true };
            _hint.Opacity = empty && !string.IsNullOrEmpty(Hint) ? 1 : 0;
        }
        _undo?.SetCurrentValue(IsEnabledProperty, live > 0);
        _clear?.SetCurrentValue(IsEnabledProperty, live > 0);
        if (board is not null)
        {
            var summary = Texts.Summary(board.Items.Where(i => !i.Removing).Select(i => i.Shape));
            AutomationProperties.SetHelpText(board, Texts.Help + " " + summary);
        }
    }

    // --- chip ---------------------------------------------------------------------------------------------------

    private void ShowChip(BoardItem item)
    {
        if (_chip is null)
        {
            return;
        }
        _chipItem = item;
        if (_chipText is not null)
        {
            _chipText.Text = string.Format(CultureInfo.InvariantCulture, Texts.ChipText, Texts.ShapeName(item.Shape));
        }
        _chipTimer ??= new DispatcherTimer { Interval = ChipLifetime() };
        _chipTimer.Tick -= OnChipTimer;
        _chipTimer.Tick += OnChipTimer;
        _chipTimer.Stop();
        _chipTimer.Interval = ChipLifetime();
        _chipTimer.Start();
        var wasShown = _chipShown;
        _chipShown = true;
        _chip.IsVisible = true;
        if (!Animates)
        {
            _chipOpacity?.Jump(1);
            _chipLift?.Jump(1);
            ApplyChip(1, 1);
            return;
        }
        EnsureChipMotion();
        if (!wasShown)
        {
            _chipOpacity!.Jump(0);
            _chipLift!.Jump(0);
        }
        // Arc: opacity over 300ms on the standard curve, lift and scale on a critically damped spring.
        _ = _chipOpacity!.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(300), ArcEasings.Standard));
        _ = _chipLift!.AnimateTo(1, MotionSpec.Spring(SpringSpec.FromPhysical(644, 50)));
    }

    private TimeSpan ChipLifetime() =>
        this.TryFindResource(CursorTrailDrawTokens.Key(CursorTrailDrawTokenKind.ChipLifetime), out var value) && value is TimeSpan t
            ? t
            : TimeSpan.FromMilliseconds(4200);

    private void OnChipTimer(object? sender, EventArgs e)
    {
        _chipTimer?.Stop();
        HideChip();
    }

    private async void HideChip()
    {
        _chipTimer?.Stop();
        _chipItem = null;
        if (!_chipShown || _chip is null)
        {
            return;
        }
        _chipShown = false;
        if (_keep is { IsKeyboardFocusWithin: true })
        {
            _board?.Focus(NavigationMethod.Tab);
        }
        if (!Animates || _chipOpacity is null || _chipLift is null)
        {
            ApplyChip(0, 1);
            _chip.IsVisible = false;
            return;
        }
        _chipLift.Jump(1);
        _exiting = true;
        var done = await _chipOpacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Exit));
        _exiting = false;
        if (done && !_chipShown)
        {
            _chip.IsVisible = false;
        }
    }

    private bool _exiting;

    private void EnsureChipMotion()
    {
        if (_chip is null || _chipOpacity is not null)
        {
            return;
        }
        _chipOpacity = new MotionValue<double>(_chip, 0, MotionVectors.Double);
        _chipLift = new MotionValue<double>(_chip, 0, MotionVectors.Double);
        _chipOpacity.Changed += _ => ApplyChip(_chipOpacity.Value, _chipLift.Value);
        _chipLift.Changed += _ => ApplyChip(_chipOpacity.Value, _chipLift.Value);
    }

    private void ApplyChip(double opacity, double lift)
    {
        if (_chip is null)
        {
            return;
        }
        _chip.Opacity = opacity;
        if (_exiting)
        {
            // Leaving: rises 6 px as it fades.
            _chip.RenderTransform = new MatrixTransform(Matrix.CreateTranslation(0, -6 * (1 - opacity)));
            return;
        }
        var scale = 0.96 + 0.04 * lift;
        _chip.RenderTransform = new MatrixTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, -8 * (1 - lift)));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CursorTrailDrawAutomationPeer(this);

    private sealed class CursorTrailDrawAutomationPeer(CursorTrailDraw owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "whiteboard";
    }
}

/// <summary>The board's words: names, the live region's messages, the help and the summary (English defaults).</summary>
internal sealed class CursorTrailDrawTexts
{
    public string CanvasName { get; set; } = "{0} drawing area";
    public string CountOne { get; set; } = "{0} item";
    public string CountMany { get; set; } = "{0} items";
    public string InkName { get; set; } = "{0} color";
    public string ChipText { get; set; } = "Tidied into a {0}";
    public string Snapped { get; set; } = "Tidied into a {0}";
    public string Added { get; set; } = "Stroke drawn";
    public string Kept { get; set; } = "Kept the stroke as you drew it";
    public string Removed { get; set; } = "Took away the {0}";
    public string NothingToUndo { get; set; } = "Nothing left to undo";
    public string Cleared { get; set; } = "Board wiped clean";
    public string Cancelled { get; set; } = "Stroke dropped";
    public string PenPicked { get; set; } = "Pen in hand";
    public string LaserPicked { get; set; } = "Laser pointer in hand; its trail fades by itself";
    public string SnapOn { get; set; } = "Shapes tidy up when the pen lifts";
    public string SnapOff { get; set; } = "Shapes stay as drawn";
    public string InkPicked { get; set; } = "{0} color in use";
    public string PenDown { get; set; } = "Pen on the board. Arrow keys draw, Space lifts the pen";
    public string PenUp { get; set; } = "Pen lifted";

    public string Help { get; set; } =
        "Draw with a pointer or the keyboard. Arrow keys move the cursor and Shift slows it; Space or Enter puts the pen "
        + "on the board and lifts it; Escape drops the stroke in progress. P takes the pen, L the laser pointer, S turns "
        + "shape tidying on or off, and Ctrl or Cmd with Z undoes.";

    public string ShapeName(CursorTrailDrawShape shape) => shape switch
    {
        CursorTrailDrawShape.Line => "line",
        CursorTrailDrawShape.Circle => "circle",
        CursorTrailDrawShape.Ellipse => "ellipse",
        CursorTrailDrawShape.Rectangle => "rectangle",
        CursorTrailDrawShape.Square => "square",
        CursorTrailDrawShape.Triangle => "triangle",
        CursorTrailDrawShape.Quad => "four sided shape",
        _ => "stroke",
    };

    private string Plural(CursorTrailDrawShape shape) => shape switch
    {
        CursorTrailDrawShape.Ellipse => "ellipses",
        CursorTrailDrawShape.Quad => "four sided shapes",
        _ => ShapeName(shape) + "s",
    };

    public string Summary(IEnumerable<CursorTrailDrawShape> shapes)
    {
        var groups = shapes.GroupBy(s => s).ToList();
        if (groups.Count == 0)
        {
            return "Nothing on the board yet.";
        }
        var parts = groups.Select(g => g.Count() == 1 ? "1 " + ShapeName(g.Key) : g.Count().ToString(CultureInfo.InvariantCulture) + " " + Plural(g.Key));
        return "Board contents: " + string.Join(", ", parts) + ".";
    }
}
