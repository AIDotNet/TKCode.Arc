using System.Collections.ObjectModel;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/overview: Northstar's home. It is not a standard demo page (no view switcher): a hero with the
/// Week / Month / Quarter range and "New project", the key metrics strip, the growth MetricExplorer, workload, the projects
/// table and its drawer, the team with its latest updates, and the activity year. Sections are 32 apart; their ids
/// (overview, workload, projects, team, activity) are where search results scroll to.
/// </summary>
public sealed partial class DemoOverviewPage : DemoPageView
{
    /// <summary>The main column's widths where uiarc.dev's container queries switch layouts.</summary>
    private const double SplitFrom = 940;
    private const double NarrowBelow = 860;
    private const double CompactBelow = 560;

    private readonly List<Action<double>> _onWidth = new();
    private readonly List<Action<double>> _onViewport = new();
    private readonly ObservableCollection<TimelineEvent> _events = new(DemoUi.WithInitials(InitialEvents()));
    private DateTimeOffset _lastStamp = new(DemoData.Now);
    private OverviewRange _range = OverviewData.Range("month");
    private SegmentedControl _rangeSwitch = null!;
    private Button _newProject = null!;

    public DemoOverviewPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        Column.Spacing = 32;
        AddRow(DemoRowLayout.Stack, "overview", BuildOverview());
        AddRow(DemoRowLayout.Stack, null, BuildInsight());
        AddRow(DemoRowLayout.Stack, "workload", BuildWorkload());
        AddRow(DemoRowLayout.Stack, "projects", BuildProjects());
        AddRow(DemoRowLayout.Stack, "team", BuildTeam());
        AddRow(DemoRowLayout.Stack, "activity", BuildActivity());
        ShowRange(_range);

        Column.SizeChanged += (_, e) => Column.Spacing = Notify(_onWidth, e.NewSize.Width) <= CompactBelow ? 24 : 32;
        Scroll.SizeChanged += (_, e) => Notify(_onViewport, e.NewSize.Width);
    }

    /// <summary>uiarc.dev's <c>.main</c>: 24 at the top, the gutter at the sides, 32 at the bottom.</summary>
    protected override Thickness PaddingFor(double width)
    {
        var gutter = Gutter(width);
        return new Thickness(gutter, 24, gutter, 32);
    }

    /// <summary>Scrolls to a section; a project intent also opens that project's drawer.</summary>
    public override void ApplyIntent(DemoIntent intent)
    {
        base.ApplyIntent(intent);
        if (intent.Project is { } id && _projects.FirstOrDefault(project => project.Id == id) is { } match)
        {
            OpenProject(match, null);
        }
    }

    private static double Notify(List<Action<double>> listeners, double width)
    {
        foreach (var listener in listeners)
        {
            listener(width);
        }
        return width;
    }

    /// <summary>Runs <paramref name="apply"/> now and whenever the main column's width changes.</summary>
    private void OnWidth(Action<double> apply) => _onWidth.Add(apply);

    #region Hero

    private Control BuildOverview()
    {
        var first = Host.Account.FirstName;
        var left = new StackPanel { Spacing = 4, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left };
        left.Children.Add(DemoUi.Text(DemoData.Now.ToString("dddd, MMMM d", OverviewData.En), ArcDesignTokenKind.TextMuted, 12));
        var title = DemoUi.Text(first.Length > 0 ? $"Good afternoon, {first}" : "Good afternoon", ArcDesignTokenKind.Foreground, 24,
                                FontWeight.Medium);
        title.LetterSpacing = -0.48;
        title.LineHeight = 30;
        left.Children.Add(title);
        var lede = DemoUi.Text("Three reviews are waiting on you, and Northwind's onboarding is six days behind plan.",
                               ArcDesignTokenKind.TextSecondary);
        lede.LineHeight = 19;
        left.Children.Add(lede);

        _rangeSwitch = Segmented("Time range", "month", ("week", "Week"), ("month", "Month"), ("quarter", "Quarter"));
        _rangeSwitch.SelectionChanged += (_, _) =>
        {
            if (_rangeSwitch.SelectedValue is string id && id != _range.Id)
            {
                ShowRange(OverviewData.Range(id));
            }
        };
        _newProject = DemoUi.Button("New project", ButtonVariant.Primary, ArcIcons.Plus);
        _newProject.Click += (_, _) => CreateKestrel();
        var actions = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_newProject, Dock.Right);
        _newProject.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(_newProject);
        actions.Children.Add(_rangeSwitch);

        var hero = new OverviewSpread { Align = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4) };
        hero.Children.Add(left);
        hero.Children.Add(actions);
        // Below 640px the actions take the full width; the range switch flexes and the button keeps its size.
        _onViewport.Add(width =>
        {
            var narrow = width <= 640;
            hero.StretchSecond = narrow;
            _rangeSwitch.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        });

        var section = new StackPanel { Spacing = 12 };
        section.Children.Add(hero);
        section.Children.Add(BuildMetrics());
        return section;
    }

    private void ShowRange(OverviewRange range)
    {
        _range = range;
        ShowMetricsRange(range);
        ShowHours();
    }

    /// <summary>
    /// "New project" adds the Kestrel website after a second of loading, with an Undo in the toast; a second press while it
    /// exists only points at the table.
    /// </summary>
    private void CreateKestrel()
    {
        if (_projects.Any(project => project.Id == OverviewData.Kestrel.Id))
        {
            Host.Toast(ToastKind.Info, "Kestrel website already exists", "Open it from the projects table.");
            return;
        }
        if (_newProject.IsLoading)
        {
            return;
        }
        _newProject.IsLoading = true;
        var toast = Host.Toast(new ToastOptions
        {
            Kind = ToastKind.Loading, Title = "Creating Kestrel website", Description = "Setting up the brief and the team."
        });
        DispatcherTimer.RunOnce(() =>
        {
            _newProject.IsLoading = false;
            _projects.Insert(0, OverviewData.Kestrel);
            _statusFilter.Selection.Clear();
            PushEvent(new TimelineEvent("e-kestrel", default, "created Kestrel website", "Emma Collins") { Meta = "Kestrel Coffee" });
            RefreshProjects();
            Host.UpdateToast(toast, new ToastUpdate
            {
                Kind = ToastKind.Success, Title = "Kestrel website created", Description = "Added to projects with Olivia Bennett.",
                Action = new ToastAction("Undo", () => UndoKestrel(toast))
            });
        }, TimeSpan.FromMilliseconds(1100));
    }

    private void UndoKestrel(string toast)
    {
        _projects.RemoveAll(project => project.Id == OverviewData.Kestrel.Id);
        if (_events.FirstOrDefault(item => item.Id == "e-kestrel") is { } created)
        {
            _events.Remove(created);
        }
        if (_drawerProject?.Id == OverviewData.Kestrel.Id)
        {
            _drawer.IsOpen = false;
        }
        RefreshProjects();
        Host.UpdateToast(toast, new ToastUpdate
        {
            Kind = ToastKind.Info, Title = "Kestrel website removed", Description = "Removed from projects.", RemoveAction = true
        });
    }

    /// <summary>Puts an event at the top of the latest updates, a minute after the last one (and moves the timeline's now).</summary>
    private void PushEvent(TimelineEvent item)
    {
        DemoUi.WithInitials(item);
        _lastStamp = _lastStamp.AddMinutes(1);
        item.At = _lastStamp;
        if (_events.FirstOrDefault(existing => existing.Id == item.Id) is { } old)
        {
            _events.Remove(old);
        }
        _timeline.Now = _lastStamp;
        _events.Insert(0, item);
    }

    #endregion

    #region Shared parts

    /// <summary>A section head: the h2 (15px medium) and a muted line on the left, actions on the right; it wraps.</summary>
    private static OverviewSpread SectionHead(string title, TextBlock subtitle, Control? actions = null)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var heading = DemoUi.Text(title, ArcDesignTokenKind.Foreground, 15, FontWeight.Medium);
        heading.LetterSpacing = -0.15;
        text.Children.Add(heading);
        text.Children.Add(subtitle);
        var head = new OverviewSpread { MinHeight = 32 };
        head.Children.Add(text);
        if (actions is not null)
        {
            head.Children.Add(actions);
        }
        return head;
    }

    private static TextBlock Subtitle(string text = "") => DemoUi.Text(text, ArcDesignTokenKind.TextMuted);

    /// <summary>A section: its head and its content 12 apart.</summary>
    private static StackPanel Section(params Control[] children)
    {
        var section = new StackPanel { Spacing = 12 };
        section.Children.AddRange(children);
        return section;
    }

    /// <summary>A segmented control; <paramref name="compact"/> is uiarc.dev's section-head size (26px buttons, 10px sides).</summary>
    private static SegmentedControl Segmented(string label, string selected, params (string Value, string Label)[] options) =>
        Segmented(label, selected, false, options);

    private static SegmentedControl Segmented(string label, string selected, bool compact, params (string Value, string Label)[] options)
    {
        var control = new SegmentedControl();
        Avalonia.Automation.AutomationProperties.SetName(control, label);
        foreach (var (value, text) in options)
        {
            control.Items.Add(new SegmentedOption { Value = value, Label = text });
        }
        control.SelectedValue = selected;
        if (compact)
        {
            // uiarc.dev also trims the buttons to 26px with 10px sides; SegmentedItem is internal, so only the type size follows.
            control.FontSize = 13;
        }
        return control;
    }

    /// <summary>A two-column grid that stacks its two cards (16 apart) below <paramref name="from"/>.</summary>
    private Grid Split(Control first, Control second, string columns, double from = SplitFrom, double gap = 16, bool alignTop = false)
    {
        var grid = new Grid { ColumnSpacing = gap, RowSpacing = 16 };
        grid.Children.Add(first);
        grid.Children.Add(second);
        if (alignTop)
        {
            first.VerticalAlignment = VerticalAlignment.Top;
            second.VerticalAlignment = VerticalAlignment.Top;
        }
        var wide = (bool?)null;
        OnWidth(width =>
        {
            var next = width >= from;
            if (wide == next)
            {
                return;
            }
            wide = next;
            grid.ColumnDefinitions = new ColumnDefinitions(next ? columns : "*");
            grid.RowDefinitions = new RowDefinitions(next ? "Auto" : "Auto,Auto");
            Grid.SetColumn(second, next ? 1 : 0);
            Grid.SetRow(second, next ? 0 : 1);
        });
        return grid;
    }

    /// <summary>A muted label (12px) over a 14px medium value: one cell of a facts list.</summary>
    private static OverviewFact Fact(string label, Control value) => new(label, value);

    private static TextBlock FactValue(string text) => DemoUi.Text(text, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);

    private static AnimatedCounter Counter(double value, string? suffix = null, string? prefix = null, double size = 14) => new()
    {
        Value = value, Suffix = suffix, Prefix = prefix, FontSize = size, FontWeight = FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Left
    };

    /// <summary>A small pill track (4px high) with a fill of <paramref name="fraction"/>.</summary>
    private static Border Pill(double width, double fraction, ArcDesignTokenKind fill)
    {
        var bar = new Border { Width = width * Math.Clamp(fraction, 0, 1), Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(bar, Border.BackgroundProperty, fill);
        var track = new Border { Width = width, Height = 4, CornerRadius = new CornerRadius(2), Child = bar, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(track, Border.BackgroundProperty, ArcDesignTokenKind.Border);
        return track;
    }

    private static TextBlock Swapped(string text, ArcDesignTokenKind color = ArcDesignTokenKind.TextSecondary, double size = DemoUi.BodySize,
                                     FontWeight weight = FontWeight.Normal) => DemoUi.Text(text, color, size, weight);

    /// <summary>uiarc.dev's "rise" text swap: the new text rises out of a soft blur.</summary>
    private static ArcContentSwap Rise(Control content) => new()
    {
        Mode = ContentSwapMode.BlurSwap, Content = content, HorizontalContentAlignment = HorizontalAlignment.Left
    };

    #endregion
}

/// <summary>
/// A flex row that wraps (uiarc.dev's section heads, the hero, the filters): the first child on the left, the second on
/// the right, 24 apart; when they do not both fit at their natural width the second goes under the first, 12 below.
/// </summary>
internal sealed class OverviewSpread : Panel
{
    public static readonly StyledProperty<bool> StretchSecondProperty =
        AvaloniaProperty.Register<OverviewSpread, bool>(nameof(StretchSecond));

    private bool _stacked;

    static OverviewSpread()
    {
        AffectsMeasure<OverviewSpread>(StretchSecondProperty);
    }

    public double Gap { get; init; } = 24;

    public double RowGap { get; init; } = 12;

    /// <summary>Where the two line up on one line (uiarc.dev's align-items).</summary>
    public VerticalAlignment Align { get; init; } = VerticalAlignment.Center;

    /// <summary>Gives the second child the full width once it wraps.</summary>
    public bool StretchSecond { get => GetValue(StretchSecondProperty); set => SetValue(StretchSecondProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var first = Children.Count > 0 ? Children[0] : null;
        var second = Children.Count > 1 ? Children[1] : null;
        if (first is null)
        {
            return default;
        }
        if (second is null)
        {
            first.Measure(availableSize);
            return first.DesiredSize;
        }
        second.Measure(new Size(width, double.PositiveInfinity));
        first.Measure(Size.Infinity);
        var natural = first.DesiredSize.Width;
        _stacked = !double.IsInfinity(width) && (StretchSecond || natural + Gap + second.DesiredSize.Width > width);
        if (!_stacked)
        {
            first.Measure(new Size(Math.Max(0, width - Gap - second.DesiredSize.Width), double.PositiveInfinity));
            var height = Math.Max(Math.Max(first.DesiredSize.Height, second.DesiredSize.Height), MinHeight);
            return new Size(double.IsInfinity(width) ? natural + Gap + second.DesiredSize.Width : width, height);
        }
        first.Measure(new Size(width, double.PositiveInfinity));
        if (StretchSecond)
        {
            second.Measure(new Size(width, double.PositiveInfinity));
        }
        return new Size(width, first.DesiredSize.Height + RowGap + second.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var first = Children.Count > 0 ? Children[0] : null;
        var second = Children.Count > 1 ? Children[1] : null;
        if (first is null)
        {
            return finalSize;
        }
        if (second is null)
        {
            first.Arrange(new Rect(finalSize));
            return finalSize;
        }
        if (_stacked)
        {
            first.Arrange(new Rect(0, 0, finalSize.Width, first.DesiredSize.Height));
            var secondWidth = StretchSecond ? finalSize.Width : Math.Min(finalSize.Width, second.DesiredSize.Width);
            second.Arrange(new Rect(0, first.DesiredSize.Height + RowGap, secondWidth, second.DesiredSize.Height));
            return finalSize;
        }
        double Top(Control child) => Align switch
        {
            VerticalAlignment.Top => 0,
            VerticalAlignment.Bottom => finalSize.Height - child.DesiredSize.Height,
            _ => (finalSize.Height - child.DesiredSize.Height) / 2
        };
        var right = second.DesiredSize.Width;
        first.Arrange(new Rect(0, Top(first), Math.Max(0, finalSize.Width - Gap - right), first.DesiredSize.Height));
        second.Arrange(new Rect(finalSize.Width - right, Top(second), right, second.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// One cell of a facts list (uiarc.dev's <c>dl</c>): a muted 12px label over its value, or beside it in
/// <see cref="IsRow"/> form (a list row with the value on the right, on a bottom rule).
/// </summary>
internal sealed class OverviewFact : Border
{
    private readonly Grid _grid = new();
    private readonly TextBlock _label;
    private readonly Control _value;

    public OverviewFact(string label, Control value)
    {
        _label = DemoUi.Text(label, ArcDesignTokenKind.TextMuted, 12);
        _value = value;
        _grid.Children.Add(_label);
        _grid.Children.Add(value);
        Child = _grid;
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        SetRow(false);
    }

    /// <summary>Label left and value right on one line, baselines aligned, over a subtle rule.</summary>
    public void SetRow(bool row)
    {
        _grid.RowDefinitions = new RowDefinitions(row ? "Auto" : "Auto,Auto");
        _grid.ColumnDefinitions = new ColumnDefinitions(row ? "*,Auto" : "*");
        _grid.RowSpacing = row ? 0 : 4;
        Grid.SetRow(_value, row ? 0 : 1);
        Grid.SetColumn(_value, row ? 1 : 0);
        _label.VerticalAlignment = row ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _value.VerticalAlignment = row ? VerticalAlignment.Bottom : VerticalAlignment.Top;
    }

    /// <summary>Lays <paramref name="facts"/> out in <paramref name="grid"/>: a row of columns, a list, or one column.</summary>
    public static void Layout(Grid grid, IReadOnlyList<OverviewFact> facts, OverviewFactsMode mode, double inset = 16)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions = new ColumnDefinitions(mode == OverviewFactsMode.Columns ? string.Join(",", facts.Select(_ => "*")) : "*");
        grid.RowDefinitions = new RowDefinitions(mode == OverviewFactsMode.Columns ? "Auto" : string.Join(",", facts.Select(_ => "Auto")));
        for (var i = 0; i < facts.Count; i++)
        {
            var fact = facts[i];
            fact.SetRow(mode == OverviewFactsMode.List);
            fact.BorderThickness = mode == OverviewFactsMode.List ? new Thickness(0, 0, 0, 1) : default;
            fact.Padding = mode switch
            {
                OverviewFactsMode.List => new Thickness(0, 12),
                OverviewFactsMode.Stack => new Thickness(0, i == 0 ? 0 : 16, 0, 0),
                _ => new Thickness(i == 0 ? 0 : inset, 12, 0, 0)
            };
            Grid.SetColumn(fact, mode == OverviewFactsMode.Columns ? i : 0);
            Grid.SetRow(fact, mode == OverviewFactsMode.Columns ? 0 : i);
            grid.Children.Add(fact);
        }
    }
}

internal enum OverviewFactsMode
{
    /// <summary>Side by side, the later ones padded on the left.</summary>
    Columns,

    /// <summary>One per line, label left and value right.</summary>
    List,

    /// <summary>One under another, label over value.</summary>
    Stack
}

/// <summary>
/// A row that enters and leaves by collapsing its height and opacity on a smooth spring, so the rows after it glide to
/// their new places (uiarc.dev's animated people list). Code-driven: the height is a measured value.
/// </summary>
internal sealed class OverviewCollapse : Decorator
{
    private static readonly SpringSpec s_smooth = SpringSpec.FromVisual(0.4, 0);
    private readonly MotionValue<double> _shown;

    public OverviewCollapse()
    {
        ClipToBounds = true;
        _shown = new MotionValue<double>(this, 1, MotionVectors.Double);
        _shown.Changed += _ => Apply();
    }

    public bool IsShown { get; private set; } = true;

    public void SetShown(bool shown)
    {
        if (shown == IsShown)
        {
            return;
        }
        IsShown = shown;
        IsHitTestVisible = shown;
        if (ArcMotion.IsFull(this))
        {
            _ = _shown.AnimateTo(shown ? 1 : 0, MotionSpec.Spring(s_smooth));
            return;
        }
        _shown.Jump(shown ? 1 : 0);
        Apply();
    }

    private double Fraction => Math.Clamp(_shown.Value, 0, 1);

    private void Apply()
    {
        Opacity = Fraction;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, size.Height * Fraction);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>Holds one token binding of a property, so a color can be switched (a delta that turns from good to bad).</summary>
internal sealed class OverviewTokenSlot(StyledElement target, AvaloniaProperty property)
{
    private IDisposable? _binding;

    public void Set(ArcDesignTokenKind kind)
    {
        _binding?.Dispose();
        _binding = ArcTokens.Bind(target, property, kind);
    }
}
