using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A scroll-driven story: steps of text on one side and a pinned stage on the other. As the page scrolls, the stage stays
/// in view and changes its picture to the step crossing the reading line, while the other steps dim. Place it in a page
/// that scrolls (any ancestor <c>ScrollViewer</c>); below 760 the steps stack in one column, each with its own picture.
/// <code>
/// &lt;arc:ScrollStory Title="From request to release"&gt;
///     &lt;arc:ScrollStoryStep Kicker="Capture" Title="Every request lands in one inbox" Scene="Capture" Body="..." /&gt;
/// &lt;/arc:ScrollStory&gt;
/// </code>
/// </summary>
[TemplatePart(LayoutPart, typeof(ScrollStoryLayout))]
[TemplatePart(StagePart, typeof(ScrollStorySceneView))]
[PseudoClasses(CompactPseudoClass, NoHeaderPseudoClass)]
public class ScrollStory : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string StagePart = "PART_Stage";
    public const string CompactPseudoClass = ":compact";
    public const string NoHeaderPseudoClass = ":no-header";

    /// <summary>The small line above the title; null leaves it out.</summary>
    public static readonly StyledProperty<string?> EyebrowProperty =
        AvaloniaProperty.Register<ScrollStory, string?>(nameof(Eyebrow), "How it works");

    /// <summary>The section's heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ScrollStory, string?>(nameof(Title), "From request to release, on one page");

    /// <summary>The paragraph under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ScrollStory, string?>(nameof(Description),
            "Scroll through the four steps a team takes every week. The picture follows along.");

    /// <summary>Whether the invented sample story shows while <see cref="Steps"/> is empty.</summary>
    public static readonly StyledProperty<bool> ShowsSampleStepsProperty =
        AvaloniaProperty.Register<ScrollStory, bool>(nameof(ShowsSampleSteps), true);

    /// <summary>Below this width of the block (760) the steps stack in one column, each with its own picture.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ScrollStory, double>(nameof(CompactWidth), 760);

    /// <summary>The index of the step on the stage (read-only).</summary>
    public static readonly DirectProperty<ScrollStory, int> ActiveIndexProperty =
        AvaloniaProperty.RegisterDirect<ScrollStory, int>(nameof(ActiveIndex), o => o.ActiveIndex);

    /// <summary>Raised when another step becomes the active one.</summary>
    public static readonly RoutedEvent<ScrollStoryStepChangedEventArgs> ActiveStepChangedEvent =
        RoutedEvent.Register<ScrollStory, ScrollStoryStepChangedEventArgs>(nameof(ActiveStepChanged), RoutingStrategies.Bubble);

    private static readonly ScrollStoryStep[] s_samples = ScrollStorySamples.Create();
    private readonly AvaloniaList<ScrollStoryStep> _steps = new();
    private readonly List<ScrollStoryStepView> _views = new();
    private ScrollStoryLayout? _layout;
    private ScrollStorySceneView? _stage;
    private Control? _stageHost;
    private int _activeIndex;

    public ScrollStory()
    {
        _steps.CollectionChanged += OnStepsChanged;
    }

    /// <summary>The steps, in reading order. While it is empty the block tells an invented story (see <see cref="ShowsSampleSteps"/>).</summary>
    [Content]
    public AvaloniaList<ScrollStoryStep> Steps => _steps;

    /// <summary>The steps shown: <see cref="Steps"/>, or the samples while that is empty.</summary>
    public IReadOnlyList<ScrollStoryStep> EffectiveSteps => _steps.Count > 0 || !ShowsSampleSteps ? _steps : s_samples;

    /// <inheritdoc cref="EyebrowProperty"/>
    public string? Eyebrow { get => GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ShowsSampleStepsProperty"/>
    public bool ShowsSampleSteps { get => GetValue(ShowsSampleStepsProperty); set => SetValue(ShowsSampleStepsProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="ActiveIndexProperty"/>
    public int ActiveIndex { get => _activeIndex; private set => SetAndRaise(ActiveIndexProperty, ref _activeIndex, value); }

    /// <inheritdoc cref="ActiveStepChangedEvent"/>
    public event EventHandler<ScrollStoryStepChangedEventArgs>? ActiveStepChanged
    {
        add => AddHandler(ActiveStepChangedEvent, value);
        remove => RemoveHandler(ActiveStepChangedEvent, value);
    }

    internal ScrollStoryLayout? Layout => _layout;

    internal ScrollStorySceneView? Stage => _stage;

    internal IReadOnlyList<ScrollStoryStepView> Views => _views;

    /// <summary>Scrolls step <paramref name="index"/> into view (the stage follows once it crosses the reading line).</summary>
    public void ScrollToStep(int index)
    {
        if (index >= 0 && index < _views.Count)
        {
            _views[index].BringIntoView();
            _layout?.SetActive(index);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_layout is not null)
        {
            _layout.ActiveChanged -= OnActiveChanged;
            RemoveStepViews(_layout);
        }
        _layout = e.NameScope.Find<ScrollStoryLayout>(LayoutPart);
        _stageHost = e.NameScope.Find<Control>("PART_StageHost");
        _stage = e.NameScope.Find<ScrollStorySceneView>(StagePart);
        if (_layout is not null)
        {
            _layout.ActiveChanged += OnActiveChanged;
            _layout.IsStacked = PseudoClasses.Contains(CompactPseudoClass);
        }
        RebuildSteps();
        UpdateHeader();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowsSampleStepsProperty)
        {
            RebuildSteps();
        }
        else if (change.Property == TitleProperty || change.Property == EyebrowProperty || change.Property == DescriptionProperty)
        {
            UpdateHeader();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            var compact = Bounds.Width > 0 && Bounds.Width < CompactWidth;
            if (compact != PseudoClasses.Contains(CompactPseudoClass))
            {
                PseudoClasses.Set(CompactPseudoClass, compact);
                ApplyStacked(compact);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not ScrollStoryStepView current)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Down => current.Index + 1,
            Key.Up => current.Index - 1,
            Key.Home => 0,
            Key.End => _views.Count - 1,
            _ => -2
        };
        if (target == -2)
        {
            return;
        }
        e.Handled = true;
        if (target >= 0 && target < _views.Count)
        {
            _views[target].Focus(NavigationMethod.Directional);
            _layout?.SetActive(target);
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildSteps();

    private void RebuildSteps()
    {
        _views.Clear();
        if (_layout is null)
        {
            return;
        }
        RemoveStepViews(_layout);
        var steps = EffectiveSteps;
        for (var i = 0; i < steps.Count; i++)
        {
            var view = new ScrollStoryStepView(steps[i], i) { IsStacked = PseudoClasses.Contains(CompactPseudoClass) };
            _views.Add(view);
            _layout.Children.Add(view);
        }
        _layout.SetActive(-1);
        _layout.SetActive(steps.Count > 0 ? 0 : -1);
        if (steps.Count > 0)
        {
            _stage?.Show(steps[0].Scene, steps[0].Image);
        }
    }

    private static void RemoveStepViews(ScrollStoryLayout layout)
    {
        foreach (var view in layout.Children.OfType<ScrollStoryStepView>().ToList())
        {
            layout.Children.Remove(view);
        }
    }

    private void ApplyStacked(bool stacked)
    {
        foreach (var view in _views)
        {
            view.IsStacked = stacked;
        }
        if (_layout is not null)
        {
            _layout.IsStacked = stacked;
        }
    }

    private void UpdateHeader()
        => PseudoClasses.Set(NoHeaderPseudoClass,
            string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Eyebrow) && string.IsNullOrEmpty(Description));

    private void OnActiveChanged(object? sender, EventArgs e)
    {
        var index = _layout?.ActiveIndex ?? -1;
        var steps = EffectiveSteps;
        if (index < 0 || index >= steps.Count)
        {
            return;
        }
        var changed = index != ActiveIndex;
        ActiveIndex = index;
        var step = steps[index];
        _stage?.Show(step.Scene, step.Image);
        if (_stageHost is not null)
        {
            AutomationProperties.SetName(_stageHost, step.Title);
        }
        if (changed)
        {
            RaiseEvent(new ScrollStoryStepChangedEventArgs(ActiveStepChangedEvent, index, step));
        }
    }
}
