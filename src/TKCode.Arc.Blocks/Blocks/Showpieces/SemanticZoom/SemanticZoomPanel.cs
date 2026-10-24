using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Easing curves of the map's interface measured in CSS terms.</summary>
internal static class SemanticZoomMotion
{
    /// <summary>CSS <c>ease-out</c>.</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>The 580 ms CSS spring (digits, bars, entering avatars).</summary>
    public static MotionSpec Spring(TimeSpan? duration = null) =>
        MotionSpec.Tween(duration ?? ArcMotionPresets.CssSpring, ArcEasings.CssSpring);
}

/// <summary>
/// The key numbers of one node of a <see cref="SemanticZoom"/> map: its kind and name, open and blocked tasks and the next
/// due date, progress as a large percentage with a bar, and the people involved as an avatar stack with a closing note.
/// Numbers roll digit by digit when <see cref="Scope"/> or its totals change. <see cref="SemanticZoom"/> shows one beside
/// or under its map; use it on its own by binding <see cref="Scope"/>.
/// </summary>
[TemplatePart(LayoutPart, typeof(SemanticZoomPanelGrid))]
[TemplatePart(ScopePart, typeof(ArcContentSwap))]
[TemplatePart(OpenPart, typeof(ShowpieceNumber))]
[TemplatePart(BlockedPart, typeof(ShowpieceNumber))]
[TemplatePart(DueMonthPart, typeof(TextBlock))]
[TemplatePart(DueDayPart, typeof(ShowpieceNumber))]
[TemplatePart(ProgressPart, typeof(ShowpieceNumber))]
[TemplatePart(BarPart, typeof(SemanticZoomBar))]
[TemplatePart(PeoplePart, typeof(SemanticZoomPeoplePanel))]
[TemplatePart(PeopleCountPart, typeof(ShowpieceNumber))]
[TemplatePart(FacesPart, typeof(StackPanel))]
[TemplatePart(NotePart, typeof(TextBlock))]
[PseudoClasses(StackPseudoClass)]
public class SemanticZoomPanel : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string ScopePart = "PART_Scope";
    public const string OpenPart = "PART_Open";
    public const string BlockedPart = "PART_Blocked";
    public const string DueMonthPart = "PART_DueMonth";
    public const string DueDayPart = "PART_DueDay";
    public const string ProgressPart = "PART_Progress";
    public const string BarPart = "PART_Bar";
    public const string PeoplePart = "PART_People";
    public const string PeopleCountPart = "PART_PeopleCount";
    public const string FacesPart = "PART_Faces";
    public const string NotePart = "PART_Note";

    /// <summary>Set while the panel stacks its blocks in one column.</summary>
    public const string StackPseudoClass = ":stack";

    /// <summary>Most avatars before a "+N" chip.</summary>
    public const int MaxFaces = 6;

    /// <summary>Avatar circle size (28) and its surface ring (2).</summary>
    internal const double FaceSize = 28;

    internal const double FaceRing = 2;

    /// <summary>The node whose numbers are shown (the organization, a team, a project or a task).</summary>
    public static readonly StyledProperty<SemanticZoomNode?> ScopeProperty =
        AvaloniaProperty.Register<SemanticZoomPanel, SemanticZoomNode?>(nameof(Scope));

    /// <summary>Two columns (under a wide map) or one (beside a map, or in narrow containers). Default two.</summary>
    public static readonly StyledProperty<SemanticZoomPanelLayout> LayoutProperty =
        AvaloniaProperty.Register<SemanticZoomPanel, SemanticZoomPanelLayout>(nameof(Layout));

    /// <summary>Labels and notes (English defaults).</summary>
    public static readonly StyledProperty<SemanticZoomTexts?> TextsProperty =
        AvaloniaProperty.Register<SemanticZoomPanel, SemanticZoomTexts?>(nameof(Texts));

    /// <summary>Formats the next due date and the percentage; null uses <see cref="SemanticZoomFormatter.Default"/>.</summary>
    public static readonly StyledProperty<ISemanticZoomFormatter?> FormatterProperty =
        AvaloniaProperty.Register<SemanticZoomPanel, ISemanticZoomFormatter?>(nameof(Formatter));

    private SemanticZoomPanelGrid? _layout;
    private ArcContentSwap? _scope;
    private ShowpieceNumber? _open;
    private ShowpieceNumber? _blocked;
    private TextBlock? _dueMonth;
    private ShowpieceNumber? _dueDay;
    private ShowpieceNumber? _progress;
    private SemanticZoomBar? _bar;
    private SemanticZoomPeoplePanel? _people;
    private ShowpieceNumber? _peopleCount;
    private StackPanel? _faces;
    private TextBlock? _note;
    private TextBlock? _progressLabel;
    private TextBlock? _openLabel;
    private TextBlock? _blockedLabel;
    private TextBlock? _dueLabel;
    private TextBlock? _peopleLabel;
    private SemanticZoomNode? _observed;
    private bool _attached;
    private SemanticZoomNode? _shownScope;
    private readonly List<SemanticZoomAvatar> _faceSlots = new();

    static SemanticZoomPanel()
    {
        FocusableProperty.OverrideDefaultValue<SemanticZoomPanel>(false);
    }

    /// <inheritdoc cref="ScopeProperty"/>
    public SemanticZoomNode? Scope { get => GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }

    /// <inheritdoc cref="LayoutProperty"/>
    public SemanticZoomPanelLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="TextsProperty"/>
    public SemanticZoomTexts? Texts { get => GetValue(TextsProperty); set => SetValue(TextsProperty, value); }

    /// <inheritdoc cref="FormatterProperty"/>
    public ISemanticZoomFormatter? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    private SemanticZoomTexts TextsOrDefault => Texts ?? SemanticZoomTexts.Default;

    private ISemanticZoomFormatter FormatterOrDefault => Formatter ?? SemanticZoomFormatter.Default;

    /// <summary>The note line (tests).</summary>
    internal string? NoteText => _note?.Text;

    /// <summary>The shown people (tests).</summary>
    internal IReadOnlyList<SemanticZoomAvatar> Faces => _faceSlots;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _layout = scope.Find<SemanticZoomPanelGrid>(LayoutPart);
        _scope = scope.Find<ArcContentSwap>(ScopePart);
        _open = scope.Find<ShowpieceNumber>(OpenPart);
        _blocked = scope.Find<ShowpieceNumber>(BlockedPart);
        _dueMonth = scope.Find<TextBlock>(DueMonthPart);
        _dueDay = scope.Find<ShowpieceNumber>(DueDayPart);
        _progress = scope.Find<ShowpieceNumber>(ProgressPart);
        _bar = scope.Find<SemanticZoomBar>(BarPart);
        _people = scope.Find<SemanticZoomPeoplePanel>(PeoplePart);
        _peopleCount = scope.Find<ShowpieceNumber>(PeopleCountPart);
        _faces = scope.Find<StackPanel>(FacesPart);
        _note = scope.Find<TextBlock>(NotePart);
        _progressLabel = scope.Find<TextBlock>("PART_ProgressLabel");
        _openLabel = scope.Find<TextBlock>("PART_OpenLabel");
        _blockedLabel = scope.Find<TextBlock>("PART_BlockedLabel");
        _dueLabel = scope.Find<TextBlock>("PART_DueLabel");
        _peopleLabel = scope.Find<TextBlock>("PART_PeopleLabel");
        _faceSlots.Clear();
        _shownScope = null;
        ApplyLayout();
        ApplyLabels();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScopeProperty)
        {
            Observe(_attached ? change.GetNewValue<SemanticZoomNode?>() : null);
            Refresh();
        }
        else if (change.Property == LayoutProperty)
        {
            ApplyLayout();
        }
        else if (change.Property == TextsProperty || change.Property == FormatterProperty)
        {
            ApplyLabels();
            _shownScope = null;
            Refresh();
        }
    }

    private void ApplyLayout()
    {
        var stack = Layout == SemanticZoomPanelLayout.Stack;
        PseudoClasses.Set(StackPseudoClass, stack);
        if (_layout is not null)
        {
            _layout.IsStack = stack;
        }
        if (_people is not null)
        {
            _people.IsStack = stack;
        }
    }

    private void ApplyLabels()
    {
        var texts = TextsOrDefault;
        AutomationProperties.SetName(this, texts.PanelLabel);
        SetText(_openLabel, texts.OpenTasksLabel);
        SetText(_blockedLabel, texts.BlockedLabel);
        SetText(_dueLabel, texts.NextDueLabel);
        SetText(_peopleLabel, texts.PeopleLabel);
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        // The scope belongs to the organization, which outlives the panel: watch it only while attached.
        Observe(Scope);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Observe(null);
    }

    private void Observe(SemanticZoomNode? node)
    {
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnScopeValueChanged;
        }
        _observed = node;
        if (_observed is not null)
        {
            _observed.PropertyChanged += OnScopeValueChanged;
        }
    }

    private void OnScopeValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SemanticZoomNode.OpenTasksProperty || e.Property == SemanticZoomNode.BlockedTasksProperty ||
            e.Property == SemanticZoomNode.NextDueProperty || e.Property == SemanticZoomNode.ProgressProperty ||
            e.Property == SemanticZoomNode.PeopleProperty || e.Property == SemanticZoomNode.NeedAttentionProperty ||
            e.Property == SemanticZoomNode.TitleProperty || e.Property == SemanticZoomTask.StatusProperty)
        {
            Refresh();
        }
    }

    /// <summary>Shows the scope's numbers (rolling from the previous ones).</summary>
    private void Refresh()
    {
        var node = Scope;
        var texts = TextsOrDefault;
        var formatter = FormatterOrDefault;
        if (_scope is not null && !ReferenceEquals(_shownScope, node))
        {
            _shownScope = node;
            _scope.Content = node is null ? null : ScopeLabel(node, texts);
        }
        else if (_scope?.Content is StackPanel { Children: [_, TextBlock name] } && node is not null && name.Text != node.Title)
        {
            name.Text = node.Title;
        }
        if (node is null)
        {
            return;
        }
        if (_open is not null)
        {
            _open.Value = node.OpenTasks;
        }
        if (_blocked is not null)
        {
            _blocked.Value = node.BlockedTasks;
        }
        if (_dueMonth is not null && _dueDay is not null)
        {
            if (node.NextDue is not { } due)
            {
                _dueMonth.Text = texts.NoneText;
                _dueDay.IsVisible = false;
            }
            else if (formatter is SemanticZoomFormatter standard)
            {
                _dueMonth.Text = due.ToString("MMM", standard.Culture);
                _dueDay.IsVisible = true;
                _dueDay.Value = due.Day;
            }
            else
            {
                _dueMonth.Text = formatter.FormatDate(due);
                _dueDay.IsVisible = false;
            }
        }
        if (_progressLabel is not null)
        {
            _progressLabel.Text = node is SemanticZoomTask ? texts.ChecklistProgressLabel : texts.ProgressLabel;
        }
        if (_progress is not null)
        {
            _progress.Value = Math.Round(node.Progress * 100) / 100;
        }
        _bar?.SetValue(node.Progress);
        var people = node.People;
        if (_peopleCount is not null)
        {
            _peopleCount.Value = people.Count;
        }
        UpdateFaces(people, texts);
        if (_note is not null)
        {
            _note.Text = node switch
            {
                SemanticZoomProject project => SemanticZoomTexts.Format(texts.ProjectNoteFormat, project.Lead?.Name ?? texts.NobodyText),
                SemanticZoomTask task => SemanticZoomTexts.Format(texts.TaskNoteFormat, task.Assignee?.Name ?? texts.NobodyText),
                _ => SemanticZoomTexts.Format(texts.ProjectsNoteFormat, node.ProjectCount, node.NeedAttention)
            };
        }
    }

    private Control ScopeLabel(SemanticZoomNode node, SemanticZoomTexts texts)
    {
        var kind = new TextBlock { Text = texts.KindName(node.Kind), LineHeight = 16.8, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(kind, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(kind, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var name = new TextBlock { Text = node.Title, LineHeight = 22.4, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(name, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeBase);
        ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        return new StackPanel { Spacing = 1, Children = { kind, name } };
    }

    private void UpdateFaces(IReadOnlyList<SemanticZoomPerson> people, SemanticZoomTexts texts)
    {
        if (_faces is null)
        {
            return;
        }
        var shown = Math.Min(MaxFaces, people.Count);
        var more = people.Count - shown;
        var slots = shown + (more > 0 ? 1 : 0);
        var animate = ArcMotion.IsFull(this);
        while (_faceSlots.Count < slots)
        {
            var slot = new SemanticZoomAvatar { Width = FaceSize, Height = FaceSize, Ring = FaceRing };
            _faceSlots.Add(slot);
            _faces.Children.Add(slot);
        }
        while (_faceSlots.Count > slots)
        {
            var slot = _faceSlots[^1];
            _faceSlots.RemoveAt(_faceSlots.Count - 1);
            _faces.Children.Remove(slot);
        }
        var names = new List<string>(people.Count);
        for (var i = 0; i < slots; i++)
        {
            var slot = _faceSlots[i];
            var person = i < shown ? people[i] : null;
            var label = i < shown ? null : SemanticZoomTexts.Format(texts.MorePeopleFormat, more);
            if (!ReferenceEquals(slot.Person, person) || slot.Label != label)
            {
                slot.Person = person;
                slot.Label = label;
                if (animate)
                {
                    FadeIn(slot);
                }
            }
            if (person is not null)
            {
                names.Add(person.Name);
            }
        }
        for (var i = shown; i < people.Count; i++)
        {
            names.Add(people[i].Name);
        }
        AutomationProperties.SetName(_faces, string.Join(", ", names));
    }

    private static void FadeIn(Control slot)
    {
        var fade = new MotionValue<double>(slot, 0, MotionVectors.Double);
        fade.Changed += v => slot.Opacity = v;
        slot.Opacity = 0;
        _ = fade.AnimateTo(1, SemanticZoomMotion.Spring(TimeSpan.FromMilliseconds(500)));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SemanticZoomGroupAutomationPeer(this);
}

/// <summary>
/// The panel's four blocks: two columns (scope | numbers, progress, people across, at most 768 px wide, columns 5 : 6 and
/// 32 apart, rows 20 apart) or one column (scope, progress, numbers, people).
/// </summary>
internal sealed class SemanticZoomPanelGrid : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<SemanticZoomPanelGrid, double>(nameof(ColumnGap), 32);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SemanticZoomPanelGrid, double>(nameof(RowGap), 20);

    public static readonly StyledProperty<double> MaxGridWidthProperty =
        AvaloniaProperty.Register<SemanticZoomPanelGrid, double>(nameof(MaxGridWidth), 768);

    private bool _stack;

    static SemanticZoomPanelGrid()
    {
        AffectsMeasure<SemanticZoomPanelGrid>(ColumnGapProperty, RowGapProperty, MaxGridWidthProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public double MaxGridWidth { get => GetValue(MaxGridWidthProperty); set => SetValue(MaxGridWidthProperty, value); }

    public bool IsStack
    {
        get => _stack;
        set
        {
            if (_stack != value)
            {
                _stack = value;
                InvalidateMeasure();
            }
        }
    }

    // Children: 0 scope, 1 numbers, 2 progress, 3 people.
    private Control? Child(int i) => i < Children.Count ? Children[i] : null;

    private (double Left, double Right) Columns(double width)
    {
        var grid = Math.Min(width, MaxGridWidth);
        var usable = Math.Max(0, grid - ColumnGap);
        return (usable * 5 / 11, usable * 6 / 11);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        if (_stack)
        {
            double height = 0;
            int[] order = [0, 2, 1, 3];
            var count = 0;
            foreach (var i in order)
            {
                if (Child(i) is not { } child)
                {
                    continue;
                }
                child.Measure(new Size(width, double.PositiveInfinity));
                height += child.DesiredSize.Height + (count++ > 0 ? RowGap : 0);
            }
            return new Size(width, height);
        }
        var (left, right) = Columns(width);
        var grid = Math.Min(width, MaxGridWidth);
        Child(0)?.Measure(new Size(left, double.PositiveInfinity));
        Child(1)?.Measure(new Size(right, double.PositiveInfinity));
        Child(2)?.Measure(new Size(left, double.PositiveInfinity));
        Child(3)?.Measure(new Size(grid, double.PositiveInfinity));
        var row1 = Math.Max(Child(0)?.DesiredSize.Height ?? 0, Child(1)?.DesiredSize.Height ?? 0);
        var row2 = Child(2)?.DesiredSize.Height ?? 0;
        var row3 = Child(3)?.DesiredSize.Height ?? 0;
        return new Size(width, row1 + RowGap + row2 + RowGap + row3);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (_stack)
        {
            double y = 0;
            int[] order = [0, 2, 1, 3];
            var count = 0;
            foreach (var i in order)
            {
                if (Child(i) is not { } child)
                {
                    continue;
                }
                if (count++ > 0)
                {
                    y += RowGap;
                }
                child.Arrange(new Rect(0, y, width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
            return finalSize;
        }
        var (left, right) = Columns(width);
        var grid = Math.Min(width, MaxGridWidth);
        var row1 = Math.Max(Child(0)?.DesiredSize.Height ?? 0, Child(1)?.DesiredSize.Height ?? 0);
        Child(0)?.Arrange(new Rect(0, 0, left, row1));
        Child(1)?.Arrange(new Rect(left + ColumnGap, 0, right, row1));
        var y2 = row1 + RowGap;
        var row2 = Child(2)?.DesiredSize.Height ?? 0;
        Child(2)?.Arrange(new Rect(0, y2, left, row2));
        var y3 = y2 + row2 + RowGap;
        Child(3)?.Arrange(new Rect(0, y3, grid, Child(3)?.DesiredSize.Height ?? 0));
        return finalSize;
    }
}

/// <summary>
/// The people block: header ("People" and the count), the avatar stack and the note. In two columns the header sits left
/// of the stack (156 px, 16 apart, centered on one row) and the note below; in one column all three stack.
/// </summary>
internal sealed class SemanticZoomPeoplePanel : Panel
{
    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<SemanticZoomPeoplePanel, double>(nameof(LabelWidth), 156);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<SemanticZoomPeoplePanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<SemanticZoomPeoplePanel, double>(nameof(RowGap), 8);

    private bool _stack;

    static SemanticZoomPeoplePanel()
    {
        AffectsMeasure<SemanticZoomPeoplePanel>(LabelWidthProperty, ColumnGapProperty, RowGapProperty);
    }

    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool IsStack
    {
        get => _stack;
        set
        {
            if (_stack != value)
            {
                _stack = value;
                InvalidateMeasure();
            }
        }
    }

    // Children: 0 header, 1 faces, 2 note.
    private Control? Child(int i) => i < Children.Count ? Children[i] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 400;
        if (_stack)
        {
            Child(0)?.Measure(new Size(width, double.PositiveInfinity));
            Child(1)?.Measure(new Size(width, double.PositiveInfinity));
            Child(2)?.Measure(new Size(width, double.PositiveInfinity));
            var h = (Child(0)?.DesiredSize.Height ?? 0) + RowGap + (Child(1)?.DesiredSize.Height ?? 0) + RowGap +
                    (Child(2)?.DesiredSize.Height ?? 0);
            return new Size(width, h);
        }
        var label = Math.Min(LabelWidth, width);
        Child(0)?.Measure(new Size(label, double.PositiveInfinity));
        Child(1)?.Measure(new Size(Math.Max(0, width - label - ColumnGap), double.PositiveInfinity));
        Child(2)?.Measure(new Size(width, double.PositiveInfinity));
        var row = Math.Max(Child(0)?.DesiredSize.Height ?? 0, Child(1)?.DesiredSize.Height ?? 0);
        return new Size(width, row + RowGap + (Child(2)?.DesiredSize.Height ?? 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (_stack)
        {
            double y = 0;
            for (var i = 0; i < 3; i++)
            {
                if (Child(i) is not { } child)
                {
                    continue;
                }
                child.Arrange(new Rect(0, y, width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + RowGap;
            }
            return finalSize;
        }
        var label = Math.Min(LabelWidth, width);
        var row = Math.Max(Child(0)?.DesiredSize.Height ?? 0, Child(1)?.DesiredSize.Height ?? 0);
        if (Child(0) is { } header)
        {
            header.Arrange(new Rect(0, (row - header.DesiredSize.Height) / 2, label, header.DesiredSize.Height));
        }
        if (Child(1) is { } faces)
        {
            faces.Arrange(new Rect(label + ColumnGap, (row - faces.DesiredSize.Height) / 2,
                Math.Max(0, width - label - ColumnGap), faces.DesiredSize.Height));
        }
        Child(2)?.Arrange(new Rect(0, row + RowGap, width, Child(2)!.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>A 4 px progress bar whose fill width follows its value on the 580 ms CSS spring.</summary>
/// <remarks>
/// Code-driven motion: the fill is self-drawn (its width is a fraction of the bar), so a ControlTheme transition has no
/// property to animate; a <see cref="MotionValue{T}"/> drives the fraction.
/// </remarks>
internal sealed class SemanticZoomBar : Control
{
    private MotionValue<double>? _value;
    private double _shown;
    private bool _colorsDirty = true;
    private IBrush? _track;
    private IBrush? _fill;

    public SemanticZoomBar()
    {
        IsHitTestVisible = false;
        ResourcesChanged += (_, _) => MarkColors();
        ActualThemeVariantChanged += (_, _) => MarkColors();
    }

    /// <summary>The drawn fraction (tests).</summary>
    public double Shown => _shown;

    private void MarkColors()
    {
        _colorsDirty = true;
        InvalidateVisual();
    }

    /// <summary>Moves the fill to <paramref name="fraction"/> (animated at full motion).</summary>
    public void SetValue(double fraction)
    {
        fraction = Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1);
        if (!ArcMotion.IsFull(this))
        {
            _value?.Jump(fraction);
            _shown = fraction;
            InvalidateVisual();
            return;
        }
        if (_value is null)
        {
            _value = new MotionValue<double>(this, _shown, MotionVectors.Double);
            _value.Changed += v =>
            {
                _shown = v;
                InvalidateVisual();
            };
        }
        _ = _value.AnimateTo(fraction, SemanticZoomMotion.Spring());
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, 4);

    public override void Render(DrawingContext context)
    {
        if (_colorsDirty)
        {
            _colorsDirty = false;
            _track = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.BorderSubtle));
            _fill = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Accent));
        }
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        context.DrawRectangle(_track, null, new RoundedRect(bounds, radius));
        var width = bounds.Width * Math.Clamp(_shown, 0, 1);
        if (width > 0)
        {
            context.DrawRectangle(_fill, null, new RoundedRect(bounds.WithWidth(width), radius));
        }
    }
}
