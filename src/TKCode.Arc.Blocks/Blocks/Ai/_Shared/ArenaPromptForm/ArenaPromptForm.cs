using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The model arena's prompt row (spec 3.4): a 48px field (radius 18, a 1px Border that turns BorderStrong under the pointer
/// and while the focus is inside, no focus ring) holding the single-line box and the Run / Stop button, and below it a row of
/// sample prompt chips that scrolls sideways edge to edge without a scrollbar (hidden when there are no prompts).
/// <c>Padding</c> (6 20 14) is the space around the row; the chips scroll out to the control's edges.
/// </summary>
/// <remarks>
/// <para>
/// The form only asks: Run, Enter in the box and Ctrl / ⌘ + Enter in the box, on Run or on a chip raise
/// <see cref="RunRequestedEvent"/> (the shortcut always runs the box's text, never a chip's); Stop and Escape in the box or on
/// the button while running raise <see cref="StopRequestedEvent"/>; a chip raises <see cref="PromptChosenEvent"/> (the owner
/// fills the box and runs). The owner sets <see cref="IsRunning"/> and <see cref="Text"/>.
/// </para>
/// <para>
/// Run is enabled while running (it is Stop then) or when <see cref="CanRun"/> is true and the box holds more than white
/// space; Enter and the shortcut follow the same rule. A chip is pressed while the box's text equals its prompt.
/// </para>
/// </remarks>
[TemplatePart(FieldPart, typeof(Border))]
[TemplatePart(BoxPart, typeof(ArenaPromptBox))]
[TemplatePart(RunPart, typeof(ArenaRunButton))]
[TemplatePart(ChipsScrollerPart, typeof(ScrollViewer))]
[TemplatePart(ChipsPart, typeof(StackPanel))]
[PseudoClasses(RunningPseudoClass, CompactPseudoClass)]
internal sealed class ArenaPromptForm : TemplatedControl
{
    public const string FieldPart = "PART_Field";
    public const string BoxPart = "PART_Box";
    public const string RunPart = "PART_Run";
    public const string ChipsScrollerPart = "PART_ChipsScroller";
    public const string ChipsPart = "PART_Chips";
    public const string RunningPseudoClass = ":running";
    public const string CompactPseudoClass = ":compact";

    /// <summary>Between the field and the chips (spec 3.4: margin-top 10).</summary>
    private const double ChipsGap = 10;

    /// <summary>The prompt in the box (two-way).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The box's placeholder.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string?>(nameof(Placeholder));

    /// <summary>The box's accessible name.</summary>
    public static readonly StyledProperty<string?> PromptNameProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string?>(nameof(PromptName));

    /// <summary>The Run label (also the button's name while idle).</summary>
    public static readonly StyledProperty<string?> RunTextProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string?>(nameof(RunText));

    /// <summary>The Stop label (also the button's name while running).</summary>
    public static readonly StyledProperty<string?> StopTextProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string?>(nameof(StopText));

    /// <summary>The key cap after Run ("⌘↵" or "Ctrl ↵"; <see cref="PromptKbd.RunShortcutText"/>); null or empty shows none.</summary>
    public static readonly StyledProperty<string?> ShortcutHintProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string?>(nameof(ShortcutHint));

    /// <summary>Whether a run is going: the button is Stop.</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ArenaPromptForm, bool>(nameof(IsRunning));

    /// <summary>Whether a run can start at all (two contestants and a runner); false disables Run (opacity 0.52).</summary>
    public static readonly StyledProperty<bool> CanRunProperty =
        AvaloniaProperty.Register<ArenaPromptForm, bool>(nameof(CanRun), true);

    /// <summary>The sample prompts shown as chips; null or empty hides the chip row.</summary>
    public static readonly StyledProperty<IEnumerable<ArenaPrompt>?> PromptsProperty =
        AvaloniaProperty.Register<ArenaPromptForm, IEnumerable<ArenaPrompt>?>(nameof(Prompts));

    /// <summary>The accessible name of the chip group.</summary>
    public static readonly StyledProperty<string?> PromptsNameProperty =
        AvaloniaProperty.Register<ArenaPromptForm, string?>(nameof(PromptsName));

    /// <summary>The compact layout (the arena below 480): the key cap is hidden.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ArenaPromptForm, bool>(nameof(IsCompact));

    /// <summary>Raised (bubbling) when the person asks to run the box's text.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RunRequestedEvent =
        RoutedEvent.Register<ArenaPromptForm, RoutedEventArgs>(nameof(RunRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person asks to stop the run.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<ArenaPromptForm, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person picks a sample prompt chip.</summary>
    public static readonly RoutedEvent<ArenaPromptChosenEventArgs> PromptChosenEvent =
        RoutedEvent.Register<ArenaPromptForm, ArenaPromptChosenEventArgs>(nameof(PromptChosen), RoutingStrategies.Bubble);

    private readonly List<ArenaPromptChip> _chips = new();
    private readonly List<IDisposable> _chipSubscriptions = new();
    private Border? _field;
    private ArenaPromptBox? _box;
    private ArenaRunButton? _run;
    private ScrollViewer? _chipsScroller;
    private StackPanel? _chipsPanel;
    private INotifyCollectionChanged? _observedPrompts;

    static ArenaPromptForm()
    {
        FocusableProperty.OverrideDefaultValue<ArenaPromptForm>(false);
    }

    public ArenaPromptForm()
    {
        // Before the box and the buttons: a button clicks on any Enter, and the shortcut must run the box's text instead.
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="PromptNameProperty"/>
    public string? PromptName { get => GetValue(PromptNameProperty); set => SetValue(PromptNameProperty, value); }

    /// <inheritdoc cref="RunTextProperty"/>
    public string? RunText { get => GetValue(RunTextProperty); set => SetValue(RunTextProperty, value); }

    /// <inheritdoc cref="StopTextProperty"/>
    public string? StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    /// <inheritdoc cref="ShortcutHintProperty"/>
    public string? ShortcutHint { get => GetValue(ShortcutHintProperty); set => SetValue(ShortcutHintProperty, value); }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    /// <inheritdoc cref="CanRunProperty"/>
    public bool CanRun { get => GetValue(CanRunProperty); set => SetValue(CanRunProperty, value); }

    /// <inheritdoc cref="PromptsProperty"/>
    public IEnumerable<ArenaPrompt>? Prompts { get => GetValue(PromptsProperty); set => SetValue(PromptsProperty, value); }

    /// <inheritdoc cref="PromptsNameProperty"/>
    public string? PromptsName { get => GetValue(PromptsNameProperty); set => SetValue(PromptsNameProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="RunRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RunRequested { add => AddHandler(RunRequestedEvent, value); remove => RemoveHandler(RunRequestedEvent, value); }

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StopRequested { add => AddHandler(StopRequestedEvent, value); remove => RemoveHandler(StopRequestedEvent, value); }

    /// <inheritdoc cref="PromptChosenEvent"/>
    public event EventHandler<ArenaPromptChosenEventArgs>? PromptChosen
    {
        add => AddHandler(PromptChosenEvent, value);
        remove => RemoveHandler(PromptChosenEvent, value);
    }

    /// <summary>The box (once the template is applied).</summary>
    internal ArenaPromptBox? Box => _box;

    /// <summary>The Run / Stop button (once the template is applied).</summary>
    internal ArenaRunButton? RunButton => _run;

    /// <summary>The field around the box and the button (once the template is applied).</summary>
    internal Border? Field => _field;

    /// <summary>The chips, in order.</summary>
    internal IReadOnlyList<ArenaPromptChip> Chips => _chips;

    /// <summary>The chip group (once the template is applied).</summary>
    internal StackPanel? ChipsPanel => _chipsPanel;

    /// <summary>The chip row's scroller (once the template is applied).</summary>
    internal ScrollViewer? ChipsScroller => _chipsScroller;

    /// <summary>Whether Run (or Enter, or the shortcut) would ask for a run now.</summary>
    internal bool CanRunNow => CanRun && !string.IsNullOrWhiteSpace(Text);

    /// <summary>Puts the keyboard focus in the box, the caret at the end.</summary>
    /// <param name="method">How the focus arrives (a keyboard method shows rings elsewhere; the box never draws one).</param>
    /// <returns>Whether the box has the focus.</returns>
    public bool FocusBox(NavigationMethod method = NavigationMethod.Tab)
    {
        ApplyTemplate();
        if (_box is null)
        {
            return false;
        }
        var focused = _box.Focus(method);
        _box.CaretIndex = _box.Text?.Length ?? 0;
        _box.ClearSelection();
        return focused;
    }

    /// <summary>Puts the keyboard focus on Run (Stop while running), with its ring by default.</summary>
    /// <param name="method">How the focus arrives; <see cref="NavigationMethod.Tab"/> shows the ring.</param>
    /// <returns>Whether the button has the focus (false while it is disabled).</returns>
    public bool FocusRun(NavigationMethod method = NavigationMethod.Tab)
    {
        ApplyTemplate();
        return _run is not null && _run.Focus(method);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_run is not null)
        {
            _run.Click -= OnRunClick;
        }
        _field = e.NameScope.Find<Border>(FieldPart);
        _box = e.NameScope.Find<ArenaPromptBox>(BoxPart);
        _run = e.NameScope.Find<ArenaRunButton>(RunPart);
        _chipsScroller = e.NameScope.Find<ScrollViewer>(ChipsScrollerPart);
        _chipsPanel = e.NameScope.Find<StackPanel>(ChipsPart);
        if (_run is not null)
        {
            _run.Click += OnRunClick;
        }
        RefreshNames();
        RefreshRun();
        RebuildChips();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            RefreshPressed();
            RefreshRun();
        }
        else if (change.Property == IsRunningProperty)
        {
            PseudoClasses.Set(RunningPseudoClass, change.GetNewValue<bool>());
            RefreshRun();
        }
        else if (change.Property == CanRunProperty)
        {
            RefreshRun();
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, change.GetNewValue<bool>());
            RefreshRun();
        }
        else if (change.Property == ShortcutHintProperty)
        {
            RefreshRun();
        }
        else if (change.Property == PromptNameProperty || change.Property == PromptsNameProperty)
        {
            RefreshNames();
        }
        else if (change.Property == PromptsProperty)
        {
            if (_observedPrompts is not null)
            {
                _observedPrompts.CollectionChanged -= OnPromptsChanged;
            }
            _observedPrompts = change.GetNewValue<IEnumerable<ArenaPrompt>?>() as INotifyCollectionChanged;
            if (_observedPrompts is not null)
            {
                _observedPrompts.CollectionChanged += OnPromptsChanged;
            }
            RebuildChips();
        }
        else if (change.Property == PaddingProperty)
        {
            ApplyPadding();
        }
    }

    private void OnPromptsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildChips();

    private void RefreshNames()
    {
        if (_box is not null)
        {
            AutomationProperties.SetName(_box, PromptName ?? string.Empty);
        }
        if (_chipsPanel is not null)
        {
            AutomationProperties.SetName(_chipsPanel, PromptsName ?? string.Empty);
        }
    }

    private void RefreshRun()
    {
        if (_run is null)
        {
            return;
        }
        _run.IsRunning = IsRunning;
        _run.ShortcutText = IsCompact ? null : ShortcutHint;
        // Stop must stay reachable whatever the box holds.
        _run.IsEnabled = IsRunning || CanRunNow;
    }

    private void RebuildChips()
    {
        foreach (var chip in _chips)
        {
            chip.Click -= OnChipClick;
        }
        foreach (var subscription in _chipSubscriptions)
        {
            subscription.Dispose();
        }
        _chipSubscriptions.Clear();
        _chips.Clear();
        if (_chipsPanel is null)
        {
            return;
        }
        _chipsPanel.Children.Clear();
        if (Prompts is { } prompts)
        {
            foreach (var prompt in prompts)
            {
                var chip = new ArenaPromptChip(prompt);
                _chipSubscriptions.Add(chip.Bind(ArenaPromptChip.LabelProperty, prompt.GetObservable(ArenaPrompt.LabelProperty)));
                _chipSubscriptions.Add(prompt.GetPropertyChangedObservable(ArenaPrompt.TextProperty)
                    .Subscribe(new AnonymousObserver<AvaloniaPropertyChangedEventArgs>(_ => RefreshPressed())));
                chip.Click += OnChipClick;
                _chips.Add(chip);
                _chipsPanel.Children.Add(chip);
            }
        }
        if (_chipsScroller is not null)
        {
            _chipsScroller.IsVisible = _chips.Count > 0;
        }
        RefreshPressed();
        ApplyPadding();
    }

    private void RefreshPressed()
    {
        var text = Text;
        foreach (var chip in _chips)
        {
            var prompt = chip.Prompt.Text;
            chip.IsChecked = !string.IsNullOrEmpty(prompt) && string.Equals(text, prompt, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The padding goes around the field, and below the chips; the chip row itself spans the whole width and indents its
    /// chips by the side padding, so they scroll out to the edges (Arc: margin −20, padding 20).
    /// </summary>
    private void ApplyPadding()
    {
        var padding = Padding;
        var chips = _chips.Count > 0;
        if (_field is not null)
        {
            _field.Margin = new Thickness(padding.Left, padding.Top, padding.Right, chips ? 0 : padding.Bottom);
        }
        if (_chipsScroller is not null)
        {
            _chipsScroller.Margin = new Thickness(0, ChipsGap, 0, padding.Bottom);
        }
        if (_chipsPanel is not null)
        {
            _chipsPanel.Margin = new Thickness(padding.Left, 0, padding.Right, 0);
        }
    }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsRunning)
        {
            RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        }
        else
        {
            RequestRun();
        }
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ArenaPromptChip chip)
        {
            e.Handled = true;
            RaiseEvent(new ArenaPromptChosenEventArgs(PromptChosenEvent, this, chip.Prompt));
        }
    }

    private void RequestRun()
    {
        if (CanRunNow)
        {
            RaiseEvent(new RoutedEventArgs(RunRequestedEvent, this));
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            return;
        }
        var inBox = Within(_box, source);
        var onRun = !inBox && Within(_run, source);
        var onChip = !inBox && !onRun && _chipsPanel is not null && _chipsPanel.IsVisualAncestorOf(source);
        if (!inBox && !onRun && !onChip)
        {
            return;
        }
        if (e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            // The shortcut runs the box's text wherever it is pressed in the row (on a chip too).
            e.Handled = true;
            RequestRun();
        }
        else if (inBox && e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            // One line: Enter runs, never a line break.
            e.Handled = true;
            RequestRun();
        }
        else if ((inBox || onRun) && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && IsRunning)
        {
            // Stops the run; the focus stays where it is.
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        }
    }

    private static bool Within(Visual? part, Visual source) =>
        part is not null && (ReferenceEquals(part, source) || part.IsVisualAncestorOf(source));
}
