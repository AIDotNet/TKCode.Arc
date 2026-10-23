using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The toolbar of the memory list (52 high): the filter pills on the left and the search at the right end. Closed, the
/// search is a 32px round button 12px from the end; open, it covers the whole row 12px in from both ends, its width
/// following <see cref="AgentMemorySearchBox.Openness"/>. The first child is the filters, the second the search box.
/// </summary>
internal sealed class AgentMemoryToolbar : Panel
{
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<AgentMemoryToolbar>();

    /// <summary>Space between the open search box and the toolbar's ends, and above and below it (12, 10).</summary>
    public static readonly StyledProperty<Thickness> SearchMarginProperty =
        AvaloniaProperty.Register<AgentMemoryToolbar, Thickness>(nameof(SearchMargin), new Thickness(12, 10));

    static AgentMemoryToolbar()
    {
        AffectsMeasure<AgentMemoryToolbar>(PaddingProperty, SearchMarginProperty);
    }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public Thickness SearchMargin { get => GetValue(SearchMarginProperty); set => SetValue(SearchMarginProperty, value); }

    private Control? Filters => Children.Count > 0 ? Children[0] : null;

    private AgentMemorySearchBox? Search => Children.Count > 1 ? Children[1] as AgentMemorySearchBox : null;

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        foreach (var child in Children)
        {
            if (child is AgentMemorySearchBox search)
            {
                search.OpennessChanged -= OnOpennessChanged;
                search.OpennessChanged += OnOpennessChanged;
            }
        }
    }

    private void OnOpennessChanged(object? sender, EventArgs e) => InvalidateArrange();

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        Filters?.Measure(new Size(Math.Max(0, width - padding.Left - padding.Right), availableSize.Height));
        var margin = SearchMargin;
        Search?.Measure(new Size(Math.Max(0, width - margin.Left - margin.Right), Math.Max(0, availableSize.Height - margin.Top - margin.Bottom)));
        var height = double.IsInfinity(availableSize.Height) ? Math.Max(Filters?.DesiredSize.Height ?? 0, Search?.DesiredSize.Height ?? 0) : 0;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        if (Filters is { } filters)
        {
            var filtersHeight = Math.Min(filters.DesiredSize.Height, finalSize.Height);
            filters.Arrange(new Rect(padding.Left, (finalSize.Height - filtersHeight) / 2,
                Math.Max(0, finalSize.Width - padding.Left - padding.Right), filtersHeight));
        }
        if (Search is { } search)
        {
            var margin = SearchMargin;
            var height = Math.Max(0, finalSize.Height - margin.Top - margin.Bottom);
            var closed = search.ClosedWidth;
            var open = Math.Max(closed, finalSize.Width - margin.Left - margin.Right);
            var width = closed + (open - closed) * Math.Clamp(search.Openness, 0, 1);
            search.Arrange(new Rect(finalSize.Width - margin.Right - width, margin.Top, width, height));
        }
        return finalSize;
    }
}

/// <summary>
/// The search of the memory list: a round magnifier button that opens into a box over the filter pills (Arc's 240ms
/// width on the standard curve), with the input and a close button. While open the magnifier only leads the box (it
/// focuses the input, out of the tab order); the border is the focus cue (BorderStrong while focused, Border otherwise),
/// the input and the close button fade in 60ms after the box starts to open and out at once when it closes.
/// Escape in the input asks to close (<see cref="CloseRequested"/>), like the close button. The owner holds the state.
/// </summary>
[TemplatePart(TogglePart, typeof(AgentMemoryButton))]
[TemplatePart(InputPart, typeof(AgentMemoryInput))]
[TemplatePart(ClosePart, typeof(AgentMemoryButton))]
[PseudoClasses(OpenPseudoClass)]
internal sealed class AgentMemorySearchBox : TemplatedControl
{
    public const string TogglePart = "PART_Toggle";
    public const string InputPart = "PART_Input";
    public const string ClosePart = "PART_Close";
    public const string OpenPseudoClass = ":open";

    private static readonly TimeSpan s_contentDelay = TimeSpan.FromMilliseconds(60);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<string?> SearchNameProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, string?>(nameof(SearchName));

    public static readonly StyledProperty<string?> CloseNameProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, string?>(nameof(CloseName));

    /// <summary>How long the box takes to open or close (the token's 240ms).</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(240));

    /// <summary>Width of the closed box (the round button with its transparent border).</summary>
    public static readonly StyledProperty<double> ClosedWidthProperty =
        AvaloniaProperty.Register<AgentMemorySearchBox, double>(nameof(ClosedWidth), 32);

    private readonly MotionValue<double> _openness;
    private readonly MotionValue<double> _content;
    private AgentMemoryButton? _toggle;
    private AgentMemoryInput? _input;
    private AgentMemoryButton? _close;
    private int _generation;

    public AgentMemorySearchBox()
    {
        // Code-driven: the open width is the toolbar's measured width, and the input fades on its own clock and delay.
        _openness = new MotionValue<double>(this, 0, MotionVectors.Double);
        _openness.Changed += _ => OpennessChanged?.Invoke(this, EventArgs.Empty);
        _content = new MotionValue<double>(this, 0, MotionVectors.Double);
        _content.Changed += ApplyContentOpacity;
    }

    static AgentMemorySearchBox()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemorySearchBox>(false);
    }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string? SearchName { get => GetValue(SearchNameProperty); set => SetValue(SearchNameProperty, value); }
    public string? CloseName { get => GetValue(CloseNameProperty); set => SetValue(CloseNameProperty, value); }
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public double ClosedWidth { get => GetValue(ClosedWidthProperty); set => SetValue(ClosedWidthProperty, value); }

    /// <summary>0 closed, 1 open; the toolbar sizes the box from it.</summary>
    public double Openness => _openness.Value;

    /// <summary>True while the box opens or closes.</summary>
    internal bool IsMoving => _openness.IsAnimating || _content.IsAnimating;

    public AgentMemoryButton? ToggleButton => _toggle;

    public AgentMemoryInput? Input => _input;

    public AgentMemoryButton? CloseButton => _close;

    /// <summary>The magnifier was pressed while closed.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>The close button was pressed, or Escape in the input.</summary>
    public event EventHandler<RoutedEventArgs>? CloseRequested;

    /// <summary><see cref="Openness"/> changed.</summary>
    public event EventHandler? OpennessChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        _input?.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        base.OnApplyTemplate(e);
        _toggle = e.NameScope.Find<AgentMemoryButton>(TogglePart);
        _input = e.NameScope.Find<AgentMemoryInput>(InputPart);
        _close = e.NameScope.Find<AgentMemoryButton>(ClosePart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        _input?.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        ApplyState(animate: false);
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            ApplyState(animate: true);
        }
        else if (change.Property == SearchNameProperty || change.Property == CloseNameProperty)
        {
            UpdateAutomation();
        }
    }

    /// <summary>Moves the box to the open or closed state: the width on a 240ms tween (Reduced and Off jump).</summary>
    private void ApplyState(bool animate)
    {
        var open = IsOpen;
        PseudoClasses.Set(OpenPseudoClass, open);
        var generation = ++_generation;
        if (_toggle is not null)
        {
            // Open, the magnifier only leads the box: Tab goes from the input to the close button.
            _toggle.IsTabStop = !open;
            _toggle.Expanded = open;
        }
        var level = animate && IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (open)
        {
            SetContentVisible(true);
        }
        if (level == ArcMotionLevel.Off)
        {
            _openness.Jump(open ? 1 : 0);
            _content.Jump(open ? 1 : 0);
            SetContentVisible(open);
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            // No spatial motion: the box jumps open or shut, the input fades in 120ms.
            _openness.Jump(open ? 1 : 0);
            _ = FinishWhenDone(open, _content.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)),
                Task.FromResult(true), generation);
            return;
        }
        var width = _openness.AnimateTo(open ? 1 : 0, MotionSpec.Tween(Duration, ArcEasings.Standard));
        var fade = _content.AnimateTo(open ? 1 : 0,
            MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, open ? s_contentDelay : TimeSpan.Zero));
        _ = FinishWhenDone(open, fade, width, generation);
    }

    private async Task FinishWhenDone(bool open, Task<bool> fade, Task<bool> width, int generation)
    {
        await fade;
        await width;
        // Closed: the input and the close button leave the tab order once the box has closed.
        if (!open && generation == _generation)
        {
            SetContentVisible(false);
        }
    }

    private void SetContentVisible(bool visible)
    {
        if (_input is not null)
        {
            _input.IsVisible = visible;
        }
        if (_close is not null)
        {
            _close.IsVisible = visible;
        }
    }

    private void ApplyContentOpacity(double value)
    {
        var opacity = Math.Clamp(value, 0, 1);
        if (_input is not null)
        {
            _input.Opacity = opacity;
        }
        if (_close is not null)
        {
            _close.Opacity = opacity;
        }
    }

    private void UpdateAutomation()
    {
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, SearchName);
        }
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, SearchName);
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, CloseName);
        }
    }

    /// <summary>Focuses the input with the caret at the end; false when the box is not open or not on screen.</summary>
    public bool FocusInput(NavigationMethod method = NavigationMethod.Tab)
    {
        if (_input is null || !IsOpen)
        {
            return false;
        }
        _input.IsVisible = true;
        if (!_input.Focus(method))
        {
            return false;
        }
        var end = _input.Text?.Length ?? 0;
        _input.CaretIndex = end;
        return true;
    }

    /// <summary>Focuses the magnifier (after the search closed).</summary>
    public bool FocusToggle(NavigationMethod method) => _toggle?.Focus(method) == true;

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsOpen)
        {
            FocusInput(NavigationMethod.Pointer);
            return;
        }
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseRequested?.Invoke(this, e);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled || _input is { IsComposing: true })
        {
            return;
        }
        e.Handled = true;
        CloseRequested?.Invoke(this, e);
    }
}
