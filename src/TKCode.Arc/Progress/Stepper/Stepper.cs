using System.Collections;
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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Shows where someone is in a strict multi-step flow and which steps are done (Arc Stepper). Drive it with
/// <see cref="CurrentIndex"/> (the step count marks the whole flow complete). When progress jumps across several
/// steps the connectors fill one after another (80ms apart) from the previous current step, then the halo grows
/// around the new current step; numbers turn into drawn checks or alerts. With <see cref="IsNavigable"/> completed
/// steps can be chosen by click or keyboard (arrows between reachable steps, Home / End, Enter / Space).
/// Horizontal steppers narrower than 30rem fold into markers with the current step named underneath.
/// Steps come from <see cref="ItemsSource"/> or the inline <see cref="Steps"/>.
/// </summary>
[TemplatePart(PanelPart, typeof(StepperPanel))]
[PseudoClasses(VerticalPseudoClass, CompactPseudoClass, CaptionErrorPseudoClass)]
public class Stepper : TemplatedControl
{
    public const string PanelPart = "PART_Panel";
    public const string CaptionLabelPart = "PART_CaptionLabel";
    public const string CaptionDetailPart = "PART_CaptionDetail";
    public const string VerticalPseudoClass = ":vertical";
    public const string CompactPseudoClass = ":compact";
    public const string CaptionErrorPseudoClass = ":caption-error";

    /// <summary>Folds horizontal steppers narrower than this into markers (Arc: 30rem).</summary>
    public const double CompactBelowWidth = 480;

    /// <summary>Bound steps (<see cref="StepperStep"/> or any object, shown by its text); takes precedence over <see cref="Steps"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Stepper, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Index of the step in progress; the step count marks every step complete.</summary>
    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(CurrentIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Horizontal (default) or vertical.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Stepper, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>Lets people return to completed steps (Arc <c>onStepSelect</c>); otherwise the stepper is read-only.</summary>
    public static readonly StyledProperty<bool> IsNavigableProperty =
        AvaloniaProperty.Register<Stepper, bool>(nameof(IsNavigable));

    /// <summary>Which descriptions show (Arc <c>details</c>).</summary>
    public static readonly StyledProperty<StepperDetails> DetailsProperty =
        AvaloniaProperty.Register<Stepper, StepperDetails>(nameof(Details));

    /// <summary>Markers only; labels stay available to assistive technology.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<Stepper, bool>(nameof(IsCompact));

    /// <summary>Accessible name (Arc <c>label</c>, "Progress").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Stepper, string>(nameof(Label), "Progress");

    /// <summary>Announced, and shown in the compact caption, once every step is complete.</summary>
    public static readonly StyledProperty<string> CompleteLabelProperty =
        AvaloniaProperty.Register<Stepper, string>(nameof(CompleteLabel), "All steps complete");

    /// <summary>Raised when a completed step is chosen by click or keyboard.</summary>
    public static readonly RoutedEvent<StepperStepSelectedEventArgs> StepSelectedEvent =
        RoutedEvent.Register<Stepper, StepperStepSelectedEventArgs>(nameof(StepSelected), RoutingStrategies.Bubble);

    /// <summary>True when every step is complete.</summary>
    public static readonly DirectProperty<Stepper, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<Stepper, bool>(nameof(IsComplete), o => o.IsComplete);

    internal static readonly DirectProperty<Stepper, string?> CaptionLabelProperty =
        AvaloniaProperty.RegisterDirect<Stepper, string?>(nameof(CaptionLabel), o => o.CaptionLabel);

    internal static readonly DirectProperty<Stepper, string?> CaptionDetailProperty =
        AvaloniaProperty.RegisterDirect<Stepper, string?>(nameof(CaptionDetail), o => o.CaptionDetail);

    internal static readonly DirectProperty<Stepper, bool> IsCaptionVisibleProperty =
        AvaloniaProperty.RegisterDirect<Stepper, bool>(nameof(IsCaptionVisible), o => o.IsCaptionVisible);

    // Palette (set by the theme from ArcDesign tokens).
    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(SurfaceBrush));
    internal static readonly StyledProperty<IBrush?> BorderStrongBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(BorderStrongBrush));
    internal static readonly StyledProperty<IBrush?> TrackBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(TrackBrush));
    internal static readonly StyledProperty<IBrush?> MutedBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(MutedBrush));
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> AccentStrongBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(AccentStrongBrush));
    internal static readonly StyledProperty<IBrush?> AccentForegroundBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(AccentForegroundBrush));
    internal static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(AccentSubtleBrush));
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(DangerBrush));
    internal static readonly StyledProperty<IBrush?> ErrorDiscBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(ErrorDiscBrush));
    internal static readonly StyledProperty<IBrush?> ErrorDiscHoverBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(ErrorDiscHoverBrush));
    internal static readonly StyledProperty<IBrush?> ErrorRingBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(ErrorRingBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty = 
        AvaloniaProperty.Register<Stepper, IBrush?>(nameof(FocusRingBrush));

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public int CurrentIndex { get => GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool IsNavigable { get => GetValue(IsNavigableProperty); set => SetValue(IsNavigableProperty, value); }
    public StepperDetails Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string CompleteLabel { get => GetValue(CompleteLabelProperty); set => SetValue(CompleteLabelProperty, value); }
    internal IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    internal IBrush? BorderStrongBrush { get => GetValue(BorderStrongBrushProperty); set => SetValue(BorderStrongBrushProperty, value); }
    internal IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    internal IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? AccentStrongBrush { get => GetValue(AccentStrongBrushProperty); set => SetValue(AccentStrongBrushProperty, value); }
    internal IBrush? AccentForegroundBrush { get => GetValue(AccentForegroundBrushProperty); set => SetValue(AccentForegroundBrushProperty, value); }
    internal IBrush? AccentSubtleBrush { get => GetValue(AccentSubtleBrushProperty); set => SetValue(AccentSubtleBrushProperty, value); }
    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    internal IBrush? ErrorDiscBrush { get => GetValue(ErrorDiscBrushProperty); set => SetValue(ErrorDiscBrushProperty, value); }
    internal IBrush? ErrorDiscHoverBrush { get => GetValue(ErrorDiscHoverBrushProperty); set => SetValue(ErrorDiscHoverBrushProperty, value); }
    internal IBrush? ErrorRingBrush { get => GetValue(ErrorRingBrushProperty); set => SetValue(ErrorRingBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    /// <summary>Inline steps (<c>&lt;arc:StepperStep Label="…"/&gt;</c>); ignored while <see cref="ItemsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<StepperStep> Steps { get; } = new();

    /// <summary>Raised when a completed step is chosen. If no handler marks it handled, it becomes the current step.</summary>
    public event EventHandler<StepperStepSelectedEventArgs>? StepSelected
    {
        add => AddHandler(StepSelectedEvent, value);
        remove => RemoveHandler(StepSelectedEvent, value);
    }

    private bool _isComplete;

    /// <summary>True when <see cref="CurrentIndex"/> is at or past the step count.</summary>
    public bool IsComplete
    {
        get => _isComplete;
        private set => SetAndRaise(IsCompleteProperty, ref _isComplete, value);
    }

    private string? _captionLabel;

    internal string? CaptionLabel
    {
        get => _captionLabel;
        private set => SetAndRaise(CaptionLabelProperty, ref _captionLabel, value);
    }

    private string? _captionDetail;

    internal string? CaptionDetail
    {
        get => _captionDetail;
        private set => SetAndRaise(CaptionDetailProperty, ref _captionDetail, value);
    }

    private bool _isCaptionVisible;

    internal bool IsCaptionVisible
    {
        get => _isCaptionVisible;
        private set => SetAndRaise(IsCaptionVisibleProperty, ref _isCaptionVisible, value);
    }

    private readonly List<StepperItem> _items = new();
    private readonly List<StepperStep> _subscribed = new();
    private StepperPanel? _panel;
    private ProgressHeightFrame? _captionLabelFrame;
    private ProgressHeightFrame? _captionDetailFrame;
    private INotifyCollectionChanged? _observed;
    private int _travelFrom = -1;
    private int _travelTo = -1;
    private bool _narrow;

    internal StepperPalette? Palette { get; private set; }

    internal IReadOnlyList<StepperItem> Items => _items;

    static Stepper()
    {
        FocusableProperty.OverrideDefaultValue<Stepper>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Stepper>(AutomationControlType.Group);
        AutomationProperties.LiveSettingProperty.OverrideDefaultValue<Stepper>(AutomationLiveSetting.Polite);
    }

    public Stepper()
    {
        Steps.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                Rebuild();
            }
        };
        AutomationProperties.SetName(this, Label);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<StepperPanel>(PanelPart);
        _captionLabelFrame = e.NameScope.Find<ProgressSwapText>(CaptionLabelPart)?.Parent as ProgressHeightFrame;
        _captionDetailFrame = e.NameScope.Find<ProgressSwapText>(CaptionDetailPart)?.Parent as ProgressHeightFrame;
        BuildPalette();
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource();
            Rebuild();
        }
        else if (change.Property == CurrentIndexProperty || change.Property == DetailsProperty ||
                 change.Property == IsNavigableProperty || change.Property == CompleteLabelProperty)
        {
            Update(animate: true);
        }
        else if (change.Property == OrientationProperty || change.Property == IsCompactProperty)
        {
            Update(animate: false);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == BoundsProperty)
        {
            var narrow = Bounds.Width > 0 && Bounds.Width < CompactBelowWidth;
            if (narrow != _narrow)
            {
                _narrow = narrow;
                Update(animate: false);
            }
        }
        else if ((change.Property.OwnerType == typeof(Stepper) && change.Property.PropertyType == typeof(IBrush)) ||
                 change.Property == FontFamilyProperty || change.Property == ForegroundProperty)
        {
            BuildPalette();
            Update(animate: false);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    // User data is observed only while the stepper is in the tree, so a long-lived source never keeps it alive.
    private void ObserveSource()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
        }
        _observed = IsAttached ? ItemsSource as INotifyCollectionChanged : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnSourceChanged;
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    private void SubscribeSteps(IReadOnlyList<StepperStep> steps)
    {
        foreach (var step in _subscribed)
        {
            step.PropertyChanged -= OnStepChanged;
        }
        _subscribed.Clear();
        if (!IsAttached)
        {
            return;
        }
        foreach (var step in steps)
        {
            step.PropertyChanged += OnStepChanged;
            _subscribed.Add(step);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSource();
        var steps = CurrentSteps();
        if (!steps.SequenceEqual(_items.Select(i => i.Step)))
        {
            Rebuild();
        }
        else
        {
            SubscribeSteps(steps);
            Update(animate: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
            _observed = null;
        }
        SubscribeSteps(Array.Empty<StepperStep>());
    }

    private void BuildPalette()
    {
        var version = (Palette?.Version ?? 0) + 1;
        Palette = new StepperPalette
        {
            Version = version,
            Surface = SurfaceBrush ?? Brushes.White,
            BorderStrong = BorderStrongBrush ?? Brushes.Gray,
            Track = TrackBrush ?? Brushes.LightGray,
            TextMuted = MutedBrush ?? Brushes.Gray,
            Foreground = Foreground ?? Brushes.Black,
            Accent = AccentBrush ?? Brushes.Black,
            AccentStrong = AccentStrongBrush ?? AccentBrush ?? Brushes.Black,
            AccentForeground = AccentForegroundBrush ?? Brushes.White,
            AccentSubtle = AccentSubtleBrush ?? Brushes.LightGray,
            Danger = DangerBrush ?? Brushes.Red,
            ErrorDisc = ErrorDiscBrush ?? Brushes.MistyRose,
            ErrorDiscHover = ErrorDiscHoverBrush ?? ErrorDiscBrush ?? Brushes.MistyRose,
            ErrorRing = ErrorRingBrush ?? Brushes.MistyRose,
            FocusRing = FocusRingBrush ?? Brushes.Blue,
            FontFamily = FontFamily
        };
        foreach (var item in _items)
        {
            item.InvalidateVisual();
        }
    }

    private List<StepperStep> CurrentSteps()
    {
        var list = new List<StepperStep>();
        IEnumerable source = ItemsSource ?? Steps;
        foreach (var item in source)
        {
            if (item is StepperStep step)
            {
                list.Add(step);
            }
            else if (item is not null)
            {
                list.Add(new StepperStep(item.ToString() ?? string.Empty));
            }
        }
        return list;
    }

    private void Rebuild()
    {
        var steps = CurrentSteps();
        SubscribeSteps(steps);
        if (_panel is null)
        {
            Update(animate: false);
            return;
        }
        var existing = _items.ToDictionary(i => (object)i.Step);
        _items.Clear();
        foreach (var step in steps)
        {
            if (!existing.Remove(step, out var item))
            {
                item = new StepperItem(this, step);
            }
            _items.Add(item);
        }
        _panel.Children.Clear();
        foreach (var item in _items)
        {
            _panel.Children.Add(item);
        }
        Update(animate: false);
    }

    private void OnStepChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Update(animate: true);

    /// <summary>Delay of step <paramref name="index"/> when progress moved from <paramref name="from"/> to <paramref name="active"/>.</summary>
    internal static double DelayAt(int index, int from, int active, double gap)
    {
        if (active > from)
        {
            return index >= from && index < active ? (index - from) * gap : 0;
        }
        return index >= active && index < from ? (from - 1 - index) * gap : 0;
    }

    /// <summary>Status of step <paramref name="index"/> for the current index <paramref name="active"/>.</summary>
    internal static StepperStepStatus StatusOf(StepperStep step, int index, int active) =>
        !string.IsNullOrEmpty(step.Error) ? StepperStepStatus.Error
        : index < active ? StepperStepStatus.Complete
        : index == active ? StepperStepStatus.Current
        : StepperStepStatus.Upcoming;

    private void Update(bool animate)
    {
        var steps = _items.Count > 0 ? _items.Select(i => i.Step).ToList() : CurrentSteps();
        var count = steps.Count;
        var active = Math.Clamp(CurrentIndex, 0, count);
        IsComplete = active >= count;
        if (_travelTo != active)
        {
            _travelFrom = _travelTo < 0 ? active : _travelTo;
            _travelTo = active;
        }
        var from = animate ? _travelFrom : active;
        if (!animate)
        {
            _travelFrom = active;
        }
        var reduced = !ArcMotion.IsFull(this);
        var gap = ArcMotionPresets.StaggerLine.TotalSeconds;
        var ringDelay = Math.Max(0, Math.Abs(active - from) - 1) * gap + 0.12;
        var vertical = Orientation == Orientation.Vertical;
        var compact = IsCompact || (!vertical && _narrow);
        PseudoClasses.Set(VerticalPseudoClass, vertical);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_panel is not null)
        {
            _panel.Orientation = Orientation;
        }
        for (var i = 0; i < _items.Count; i++)
        {
            var step = steps[i];
            var status = StatusOf(step, i, active);
            var current = i == active;
            var kind = !string.IsNullOrEmpty(step.Error) ? StepperGlyphKind.Error
                : i < active ? StepperGlyphKind.Check
                : StepperGlyphKind.Number;
            var detail = step.Error ?? (Details == StepperDetails.All || current ? step.Description : null);
            var clickable = IsNavigable && i < active;
            var delay = reduced ? 0 : DelayAt(i, from, active, gap);
            _items[i].Configure(i, count, status, kind, current, clickable, i <= active, IsNavigable, vertical, compact, detail,
                delay, reduced ? 0 : ringDelay, i < active, animate);
        }

        var now = active < count ? steps[active] : null;
        var captionLabel = now?.Label ?? CompleteLabel;
        var captionDetail = now?.Error ?? now?.Description;
        if (!string.Equals(captionLabel, CaptionLabel, StringComparison.Ordinal))
        {
            _captionLabelFrame?.Arm();
        }
        if (!string.Equals(captionDetail, CaptionDetail, StringComparison.Ordinal))
        {
            _captionDetailFrame?.Arm();
        }
        CaptionLabel = captionLabel;
        CaptionDetail = captionDetail;
        IsCaptionVisible = compact && !vertical;
        PseudoClasses.Set(CaptionErrorPseudoClass, !string.IsNullOrEmpty(now?.Error));
        AutomationProperties.SetItemStatus(this, now is not null ? $"Step {active + 1} of {count}: {now.Label}" : CompleteLabel);
    }

    /// <summary>Chooses a completed step (as a click or Enter would).</summary>
    public void Select(int index)
    {
        if (!IsNavigable || index < 0 || index >= _items.Count || index >= CurrentIndex)
        {
            return;
        }
        var args = new StepperStepSelectedEventArgs(StepSelectedEvent, index, _items[index].Step);
        RaiseEvent(args);
        if (!args.Handled)
        {
            SetCurrentValue(CurrentIndexProperty, index);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsNavigable || e.Handled)
        {
            return;
        }
        var focused = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<StepperItem>().FirstOrDefault();
        if (focused is null)
        {
            return;
        }
        var reachable = _items.Where(i => i.IsReachable).ToList();
        var at = reachable.IndexOf(focused);
        if (at < 0)
        {
            return;
        }
        var rtl = Orientation == Orientation.Horizontal && FlowDirection == FlowDirection.RightToLeft;
        var back = e.Key == Key.Up || e.Key == (rtl ? Key.Right : Key.Left);
        var ahead = e.Key == Key.Down || e.Key == (rtl ? Key.Left : Key.Right);
        int? next = e.Key switch
        {
            Key.Home => 0,
            Key.End => reachable.Count - 1,
            _ when back => Math.Max(0, at - 1),
            _ when ahead => Math.Min(reachable.Count - 1, at + 1),
            _ => null
        };
        if (next is { } target)
        {
            e.Handled = true;
            reachable[target].Focus(NavigationMethod.Directional);
            return;
        }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && focused.IsClickable)
        {
            e.Handled = true;
            Select(focused.Index);
        }
    }
}
