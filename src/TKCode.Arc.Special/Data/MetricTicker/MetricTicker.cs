using System.Collections.Specialized;
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
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A strip of live metrics. In <see cref="MetricTickerMode.Scroll"/> the metrics run past in one line like a stock
/// ticker, with faded edges; pointing at the strip or focusing in it holds the line, and a pause button stops it for good
/// (moving content needs a way to stop it). In <see cref="MetricTickerMode.Roll"/>, or under reduced motion, the metrics
/// sit in even columns. Either way each value rolls digit by digit when it updates, the change since the previous value
/// shows with an arrow in the success or danger color, and the cell flashes that tint once.
/// </summary>
[TemplatePart(StripPart, typeof(MetricTickerStrip))]
[TemplatePart(PauseButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(ScrollingPseudoClass, PausedPseudoClass, HeldPseudoClass)]
public class MetricTicker : TemplatedControl
{
    /// <summary>The window that shows the metrics.</summary>
    public const string StripPart = "PART_Strip";

    /// <summary>The pause / play button (scrolling only).</summary>
    public const string PauseButtonPart = "PART_PauseButton";

    /// <summary>Set while the metrics are laid out in a scrolling line.</summary>
    public const string ScrollingPseudoClass = ":scrolling";

    /// <summary>Set while <see cref="IsPaused"/> is true.</summary>
    public const string PausedPseudoClass = ":paused";

    /// <summary>Set while the pointer or focus holds the line.</summary>
    public const string HeldPseudoClass = ":held";

    /// <summary>The metrics from a view model; when set it replaces <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable<MetricTickerItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<MetricTicker, IEnumerable<MetricTickerItem>?>(nameof(ItemsSource));

    /// <summary>A scrolling line (default) or columns that stay put.</summary>
    public static readonly StyledProperty<MetricTickerMode> ModeProperty =
        AvaloniaProperty.Register<MetricTicker, MetricTickerMode>(nameof(Mode));

    /// <summary>How fast the line scrolls, in pixels per second (0 to 400, default 40).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<MetricTicker, double>(nameof(Speed), 40,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 400) : 40);

    /// <summary>Stops the scrolling line where it is; the pause button and the Space key on it toggle it. Values still update.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<MetricTicker, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Holds the line while the pointer is over it or focus is in it (default true).</summary>
    public static readonly StyledProperty<bool> PausesOnHoverProperty =
        AvaloniaProperty.Register<MetricTicker, bool>(nameof(PausesOnHover), true);

    /// <summary>Shows the pause button while the line scrolls (default true). Hide it only when the app offers its own way to stop the line.</summary>
    public static readonly StyledProperty<bool> IsPauseButtonVisibleProperty =
        AvaloniaProperty.Register<MetricTicker, bool>(nameof(IsPauseButtonVisible), true);

    /// <summary>The accessible name of the ticker (default "Live metrics").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MetricTicker, string>(nameof(Label), "Live metrics");

    /// <summary>The pause button's name and tooltip while the line moves.</summary>
    public static readonly StyledProperty<string> PauseLabelProperty =
        AvaloniaProperty.Register<MetricTicker, string>(nameof(PauseLabel), "Pause ticker");

    /// <summary>The pause button's name and tooltip while the line is paused.</summary>
    public static readonly StyledProperty<string> PlayLabelProperty =
        AvaloniaProperty.Register<MetricTicker, string>(nameof(PlayLabel), "Play ticker");

    private readonly List<MetricTickerItem> _current = new();
    private INotifyCollectionChanged? _observedSource;
    private MetricTickerStrip? _strip;
    private AvaloniaButton? _pause;
    private bool _held;
    private bool _attached;

    static MetricTicker()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<MetricTicker>(AutomationControlType.List);
    }

    /// <summary>Creates an empty ticker.</summary>
    public MetricTicker()
    {
        Items.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                Refresh();
            }
        };
        AutomationProperties.SetName(this, Label);
        UpdatePseudoClasses();
    }

    /// <summary>The metrics, in order (the XAML content); ignored while <see cref="ItemsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<MetricTickerItem> Items { get; } = new();

    /// <summary>The metrics from a view model; when set it replaces <see cref="Items"/>.</summary>
    public IEnumerable<MetricTickerItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>A scrolling line (default) or columns that stay put.</summary>
    public MetricTickerMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>How fast the line scrolls, in pixels per second (0 to 400, default 40).</summary>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <summary>Stops the scrolling line where it is; the pause button and the Space key on it toggle it. Values still update.</summary>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <summary>Holds the line while the pointer is over it or focus is in it (default true).</summary>
    public bool PausesOnHover { get => GetValue(PausesOnHoverProperty); set => SetValue(PausesOnHoverProperty, value); }

    /// <summary>Shows the pause button while the line scrolls (default true). Hide it only when the app offers its own way to stop the line.</summary>
    public bool IsPauseButtonVisible { get => GetValue(IsPauseButtonVisibleProperty); set => SetValue(IsPauseButtonVisibleProperty, value); }

    /// <summary>The accessible name of the ticker (default "Live metrics").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The pause button's name and tooltip while the line moves.</summary>
    public string PauseLabel { get => GetValue(PauseLabelProperty); set => SetValue(PauseLabelProperty, value); }

    /// <summary>The pause button's name and tooltip while the line is paused.</summary>
    public string PlayLabel { get => GetValue(PlayLabelProperty); set => SetValue(PlayLabelProperty, value); }

    /// <summary>The metrics shown now.</summary>
    internal IReadOnlyList<MetricTickerItem> CurrentItems => _current;

    /// <summary>True when the metrics run in one scrolling line: scroll mode at full motion.</summary>
    internal bool UsesScrollLayout => Mode == MetricTickerMode.Scroll && ArcMotion.IsFull(this);

    /// <summary>True when nothing stops the line: not paused, not held, a speed above zero.</summary>
    internal bool IsMovingAllowed => !IsPaused && !_held && Speed > 0;

    /// <summary>The window (tests).</summary>
    internal MetricTickerStrip? Strip => _strip;

    /// <summary>The words a screen reader hears for one metric ("Requests per second, 1,240, up 3.2%").</summary>
    internal static string Describe(MetricTickerItem item)
    {
        var text = string.Concat(item.Label ?? string.Empty, ", ", item.FormatValue(item.Value));
        if (item.FormatChange() is { } change)
        {
            var direction = item.Change > 0 ? "up" : item.Change < 0 ? "down" : "unchanged";
            text += string.Concat(", ", direction, " ", change.TrimStart('+', '−'));
        }
        return text;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_pause is not null)
        {
            _pause.Click -= OnPauseClick;
        }
        base.OnApplyTemplate(e);
        _strip = e.NameScope.Find<MetricTickerStrip>(StripPart);
        _pause = e.NameScope.Find<AvaloniaButton>(PauseButtonPart);
        if (_pause is not null)
        {
            _pause.Click += OnPauseClick;
        }
        _strip?.Attach(this);
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveSource(ItemsSource);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveSource(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource(_attached ? ItemsSource : null);
            Refresh();
        }
        else if (change.Property == ModeProperty || change.Property == ArcMotion.LevelProperty)
        {
            Refresh();
        }
        else if (change.Property == IsPausedProperty || change.Property == SpeedProperty)
        {
            UpdateState();
        }
        else if (change.Property == PauseLabelProperty || change.Property == PlayLabelProperty ||
                 change.Property == IsPauseButtonVisibleProperty)
        {
            UpdatePauseButton();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            UpdateHeld();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateHeld();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateHeld();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateHeld();
    }

    private void UpdateHeld()
    {
        var held = PausesOnHover && (IsPointerOver || IsKeyboardFocusWithin);
        if (held != _held)
        {
            _held = held;
            UpdateState();
        }
    }

    private void ObserveSource(IEnumerable<MetricTickerItem>? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        _current.Clear();
        foreach (var item in ItemsSource ?? Items)
        {
            if (item is not null)
            {
                _current.Add(item);
            }
        }
        _strip?.Refresh();
        UpdateState();
    }

    private void UpdateState()
    {
        _strip?.UpdateLoop();
        UpdatePauseButton();
        UpdatePseudoClasses();
    }

    private void UpdatePauseButton()
    {
        if (_pause is null)
        {
            return;
        }
        _pause.IsVisible = IsPauseButtonVisible && UsesScrollLayout;
        var name = IsPaused ? PlayLabel : PauseLabel;
        AutomationProperties.SetName(_pause, name);
        Tooltip.SetTip(_pause, name);
        if (_pause is ExplainerIconButton icon)
        {
            icon.Icon = IsPaused ? ArcIcons.Play : ArcIcons.Pause;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(ScrollingPseudoClass, UsesScrollLayout);
        PseudoClasses.Set(PausedPseudoClass, IsPaused);
        PseudoClasses.Set(HeldPseudoClass, _held);
    }

    private void OnPauseClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsPausedProperty, !IsPaused);
}
