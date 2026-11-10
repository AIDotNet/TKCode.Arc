using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One step of a <see cref="ScrollStory"/>: the kicker, the title and the paragraph, dimmed until it is the active step.
/// In the stacked layout it also shows its own small picture, because there is no pinned stage.
/// </summary>
internal sealed class ScrollStoryStepView : TemplatedControl
{
    public static readonly StyledProperty<string?> KickerProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, string?>(nameof(Kicker));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, string?>(nameof(Title));

    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, string?>(nameof(Body));

    public static readonly StyledProperty<ScrollStoryScene> SceneProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, ScrollStoryScene>(nameof(Scene));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, IImage?>(nameof(Image));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ScrollStoryStepView, bool>(nameof(IsStacked));

    static ScrollStoryStepView()
    {
        FocusableProperty.OverrideDefaultValue<ScrollStoryStepView>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ScrollStoryStepView>(false);
    }

    public ScrollStoryStepView(ScrollStoryStep step, int index)
    {
        Step = step;
        Index = index;
        Kicker = step.Kicker ?? (index + 1).ToString("00", System.Globalization.CultureInfo.CurrentCulture);
        Title = step.Title;
        Body = step.Body;
        Scene = step.Scene;
        Image = step.Image;
        AutomationProperties.SetName(this, step.Title);
        AutomationProperties.SetHelpText(this, step.Body);
    }

    public ScrollStoryStep Step { get; }

    public int Index { get; }

    public string? Kicker { get => GetValue(KickerProperty); set => SetValue(KickerProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    public ScrollStoryScene Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":active", IsActive);
        }
        else if (change.Property == IsStackedProperty)
        {
            PseudoClasses.Set(":stacked", IsStacked);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Focus scrolls the step into view and makes it the active one.
        this.BringIntoView();
        (Parent as ScrollStoryLayout)?.Refresh();
    }
}

/// <summary>
/// The two columns of a <see cref="ScrollStory"/>: the steps on the start side, each at least <see cref="StepMinHeight"/>
/// tall so the reader scrolls through them one at a time, and the stage (the first child) on the end side. The stage is
/// pinned: as the section scrolls through the viewport, a render transform keeps the stage centered in the visible part
/// of the section, clamped to the section's top and bottom, so no layout pass runs while scrolling. The active step is
/// the one that crosses the viewport's reading line (45% from the top). <see cref="IsStacked"/> puts everything in one
/// column and hides the stage.
/// </summary>
internal sealed class ScrollStoryLayout : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ScrollStoryLayout, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> StepMinHeightProperty =
        AvaloniaProperty.Register<ScrollStoryLayout, double>(nameof(StepMinHeight), 380);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<ScrollStoryLayout, double>(nameof(ColumnGap), 56);

    public static readonly StyledProperty<double> StackedGapProperty =
        AvaloniaProperty.Register<ScrollStoryLayout, double>(nameof(StackedGap), 40);

    public static readonly StyledProperty<double> StageAspectProperty =
        AvaloniaProperty.Register<ScrollStoryLayout, double>(nameof(StageAspect), 0.8);

    private readonly TranslateTransform _pin = new();
    private Rect _viewport;
    private Size _size;
    private double _stageHeight;
    private int _active = -1;

    static ScrollStoryLayout()
    {
        AffectsMeasure<ScrollStoryLayout>(IsStackedProperty, StepMinHeightProperty, ColumnGapProperty, StackedGapProperty, StageAspectProperty);
    }

    public ScrollStoryLayout()
    {
        // Subscribed before the first layout, so the first viewport is reported.
        EffectiveViewportChanged += (_, e) => UpdateViewport(e.EffectiveViewport);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double StepMinHeight { get => GetValue(StepMinHeightProperty); set => SetValue(StepMinHeightProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double StackedGap { get => GetValue(StackedGapProperty); set => SetValue(StackedGapProperty, value); }

    public double StageAspect { get => GetValue(StageAspectProperty); set => SetValue(StageAspectProperty, value); }

    /// <summary>The step that crosses the reading line; -1 before the first viewport.</summary>
    public int ActiveIndex => _active;

    /// <summary>How far the stage is pushed down to stay in view.</summary>
    public double PinOffset => _pin.Y;

    /// <summary>Raised when <see cref="ActiveIndex"/> changes.</summary>
    public event EventHandler? ActiveChanged;

    private Control? Stage => Children.Count > 0 ? Children[0] : null;

    private IEnumerable<ScrollStoryStepView> Steps => Children.OfType<ScrollStoryStepView>();

    /// <summary>Recomputes the pin and the active step for <paramref name="viewport"/> (in this panel's coordinates).</summary>
    internal void UpdateViewport(Rect viewport)
    {
        _viewport = viewport;
        if (Stage is { } stage)
        {
            stage.RenderTransform = _pin;
            var height = _stageHeight;
            var room = _size.Height - height;
            if (IsStacked || viewport.Height <= 0 || double.IsInfinity(viewport.Height) || room <= 0)
            {
                _pin.Y = 0;
            }
            else
            {
                var centered = viewport.Y + (Math.Min(viewport.Height, _size.Height) - height) / 2;
                _pin.Y = Math.Clamp(centered, 0, room);
            }
        }
        var line = viewport.Height > 0 && !double.IsInfinity(viewport.Height) ? viewport.Y + viewport.Height * 0.45 : 0;
        var active = -1;
        var best = double.MaxValue;
        var index = 0;
        foreach (var step in Steps)
        {
            var b = step.Bounds;
            var distance = line < b.Top ? b.Top - line : line > b.Bottom ? line - b.Bottom : 0;
            if (distance < best)
            {
                best = distance;
                active = index;
            }
            index++;
        }
        // A step the keyboard is on stays active while any of it is in view, so the stage follows Tab and the arrows even
        // when scrolling it into view leaves it short of the reading line.
        index = 0;
        foreach (var step in Steps)
        {
            if (step.IsFocused && step.Classes.Contains(":focus-visible") && (viewport.Height <= 0 || step.Bounds.Intersects(viewport)))
            {
                active = index;
                break;
            }
            index++;
        }
        SetActive(active);
    }

    /// <summary>Recomputes the pin and the active step for the last viewport (a step took keyboard focus).</summary>
    internal void Refresh() => UpdateViewport(_viewport);

    /// <summary>Makes <paramref name="index"/> the active step (focus does this before the scroll catches up).</summary>
    internal void SetActive(int index)
    {
        if (index == _active)
        {
            return;
        }
        _active = index;
        var i = 0;
        foreach (var step in Steps)
        {
            step.IsActive = i == index;
            i++;
        }
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
        if (IsStacked)
        {
            Stage?.Measure(default);
            var y = 0.0;
            var first = true;
            foreach (var step in Steps)
            {
                step.Measure(new Size(width, double.PositiveInfinity));
                y += step.DesiredSize.Height + (first ? 0 : StackedGap);
                first = false;
            }
            return new Size(width, y);
        }
        var column = Math.Max(0, (width - ColumnGap) / 2);
        var stageHeight = column * StageAspect;
        Stage?.Measure(new Size(column, stageHeight));
        var total = 0.0;
        foreach (var step in Steps)
        {
            step.Measure(new Size(column, double.PositiveInfinity));
            total += Math.Max(StepMinHeight, step.DesiredSize.Height);
        }
        return new Size(width, Math.Max(total, stageHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _size = finalSize;
        _stageHeight = IsStacked ? 0 : Math.Max(0, (finalSize.Width - ColumnGap) / 2) * StageAspect;
        if (IsStacked)
        {
            Stage?.Arrange(default);
            var y = 0.0;
            foreach (var step in Steps)
            {
                step.Arrange(new Rect(0, y, finalSize.Width, step.DesiredSize.Height));
                y += step.DesiredSize.Height + StackedGap;
            }
        }
        else
        {
            var column = Math.Max(0, (finalSize.Width - ColumnGap) / 2);
            var stageHeight = column * StageAspect;
            Stage?.Arrange(new Rect(column + ColumnGap, 0, column, stageHeight));
            var y = 0.0;
            foreach (var step in Steps)
            {
                var slot = Math.Max(StepMinHeight, step.DesiredSize.Height);
                // The text sits in the middle of its slot, so it crosses the reading line while the stage shows it.
                step.Arrange(new Rect(0, y + (slot - step.DesiredSize.Height) / 2, column, step.DesiredSize.Height));
                y += slot;
            }
        }
        UpdateViewport(_viewport);
        return finalSize;
    }
}
