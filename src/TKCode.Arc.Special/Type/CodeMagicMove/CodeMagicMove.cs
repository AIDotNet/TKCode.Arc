using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A code block that moves between versions of a snippet: words that stay glide to their new line and column, removed
/// words fade and lift out, and new code types in behind an accent caret while the block grows or shrinks to the new
/// line count. Step dots, the arrow keys and a sideways drag walk a tutorial or changelog through each change.
/// </summary>
/// <remarks>
/// The versions are <see cref="Steps"/>. Lines a step introduced carry a faint band with an accent hairline, and the
/// header counts the lines it added and removed. With <see cref="AutoPlay"/> the walkthrough moves on by itself every
/// <see cref="Interval"/>, holds while a mouse is over it, and stops for good once someone steps, drags or presses pause.
/// Every move is a function of its progress: a drag scrubs it under the finger and commits past a third (or on a flick),
/// stepping back mid-move reverses it, and a new step mid-move starts from where every word is. Under reduced motion,
/// or with motion off, steps change at once and autoplay stays off.
/// </remarks>
[TemplatePart(SurfacePart, typeof(CodeMagicMoveSurface))]
[TemplatePart(DotsPart, typeof(Panel))]
[TemplatePart(PreviousPart, typeof(AvaloniaButton))]
[TemplatePart(NextPart, typeof(AvaloniaButton))]
[TemplatePart(PlayPart, typeof(AvaloniaButton))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(SourcePart, typeof(TextBlock))]
[TemplatePart(AddedPart, typeof(TextBlock))]
[TemplatePart(RemovedPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass, FirstStepPseudoClass, PlayingPseudoClass, MovingPseudoClass, DraggingPseudoClass)]
public partial class CodeMagicMove : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string DotsPart = "PART_Dots";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string PlayPart = "PART_Play";
    public const string StatusPart = "PART_Status";
    public const string SourcePart = "PART_Source";
    public const string AddedPart = "PART_Added";
    public const string RemovedPart = "PART_Removed";
    public const string CompactPseudoClass = ":compact";
    public const string FirstStepPseudoClass = ":first-step";
    public const string PlayingPseudoClass = ":playing";
    public const string MovingPseudoClass = ":moving";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>The index of the step shown. Setting it moves to that step.</summary>
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<CodeMagicMove, int>(nameof(Value), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceValue);

    /// <summary>The language used to color the code, as in a <see cref="CodeBlock"/>; colors need <c>UseArcHighlighting()</c>.</summary>
    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<CodeMagicMove, string>(nameof(Language), "tsx");

    /// <summary>The file name in the header when a step has none of its own.</summary>
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<CodeMagicMove, string?>(nameof(FileName));

    /// <summary>Whether the walkthrough moves through the steps by itself, looping from the last back to the first.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<CodeMagicMove, bool>(nameof(AutoPlay), true);

    /// <summary>How long autoplay rests on a step before it moves on; the current dot fills meanwhile.</summary>
    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<CodeMagicMove, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(3.2));

    /// <summary>Multiplies the pace of every move. At 1 a typical move takes about a second, longer when there is much to type.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<CodeMagicMove, double>(nameof(Speed), 1, coerce: static (_, v) => double.IsFinite(v) && v > 0 ? v : 1);

    /// <summary>Holds autoplay. The pause button sets it, and so does stepping or dragging; play clears it.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<CodeMagicMove, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The accessible name of the walkthrough; the code area adds the step position to it.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CodeMagicMove, string>(nameof(Label), "Code walkthrough");

    /// <summary>The color of the caret, the changed line bands and the current step dot (the accent by default).</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<CodeMagicMove, IBrush?>(nameof(AccentBrush));

    /// <summary>The step shown (null without steps).</summary>
    public static readonly DirectProperty<CodeMagicMove, CodeMagicMoveStep?> CurrentStepProperty =
        AvaloniaProperty.RegisterDirect<CodeMagicMove, CodeMagicMoveStep?>(nameof(CurrentStep), o => o.CurrentStep);

    /// <summary>The file name in the header: the step's own, or <see cref="FileName"/>.</summary>
    public static readonly DirectProperty<CodeMagicMove, string?> CurrentFileNameProperty =
        AvaloniaProperty.RegisterDirect<CodeMagicMove, string?>(nameof(CurrentFileName), o => o.CurrentFileName);

    /// <summary>How many lines the step shown added to the one before it (blank lines not counted).</summary>
    public static readonly DirectProperty<CodeMagicMove, int> AddedLinesProperty =
        AvaloniaProperty.RegisterDirect<CodeMagicMove, int>(nameof(AddedLines), o => o.AddedLines);

    /// <summary>How many lines the step shown removed from the one before it.</summary>
    public static readonly DirectProperty<CodeMagicMove, int> RemovedLinesProperty =
        AvaloniaProperty.RegisterDirect<CodeMagicMove, int>(nameof(RemovedLines), o => o.RemovedLines);

    /// <summary>Whether a move is under way (or being dragged).</summary>
    public static readonly DirectProperty<CodeMagicMove, bool> IsMovingProperty =
        AvaloniaProperty.RegisterDirect<CodeMagicMove, bool>(nameof(IsMoving), o => o.IsMoving);

    /// <summary>Whether autoplay is running: <see cref="AutoPlay"/> on, not paused, and motion allowed.</summary>
    public static readonly DirectProperty<CodeMagicMove, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<CodeMagicMove, bool>(nameof(IsPlaying), o => o.IsPlaying);

    /// <summary>Raised when a move starts toward a different step, or when a drag commits to one.</summary>
    public static readonly RoutedEvent<CodeMagicMoveValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<CodeMagicMove, CodeMagicMoveValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    private const string KeyboardHintText = "Use the left and right arrow keys to move between steps.";

    private CodeMagicMoveSurface? _surface;
    private Panel? _dots;
    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private AvaloniaButton? _play;
    private TextBlock? _status;
    private TextBlock? _source;
    private TextBlock? _added;
    private TextBlock? _removed;
    private CodeMagicMoveStep? _currentStep;
    private string? _currentFileName;
    private int _addedLines;
    private int _removedLines;
    private bool _isMoving;
    private bool _isPlaying;
    private bool _stepsDirty = true;
    private CodeMagicMoveSource[] _sources = [];
    private CodeMagicMoveBand[][] _bands = [];
    private (int Added, int Removed)[] _counts = [];

    static CodeMagicMove()
    {
        FocusableProperty.OverrideDefaultValue<CodeMagicMove>(false);
        ClipToBoundsProperty.OverrideDefaultValue<CodeMagicMove>(false);
    }

    /// <summary>Creates an empty walkthrough; add <see cref="Steps"/>.</summary>
    public CodeMagicMove()
    {
        _loop = new LiveLoop(this, Step);
        Steps.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (CodeMagicMoveStep step in e.OldItems)
                {
                    step.Changed -= OnStepChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (CodeMagicMoveStep step in e.NewItems)
                {
                    step.Changed += OnStepChanged;
                }
            }
            StepsChanged();
        };
    }

    /// <summary>The versions of the snippet, in order.</summary>
    [Content]
    public AvaloniaList<CodeMagicMoveStep> Steps { get; } = [];

    /// <inheritdoc cref="ValueProperty"/>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="LanguageProperty"/>
    public string Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <inheritdoc cref="FileNameProperty"/>
    public string? FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    /// <inheritdoc cref="IntervalProperty"/>
    public TimeSpan Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <inheritdoc cref="CurrentStepProperty"/>
    public CodeMagicMoveStep? CurrentStep
    {
        get => _currentStep;
        private set => SetAndRaise(CurrentStepProperty, ref _currentStep, value);
    }

    /// <inheritdoc cref="CurrentFileNameProperty"/>
    public string? CurrentFileName
    {
        get => _currentFileName;
        private set => SetAndRaise(CurrentFileNameProperty, ref _currentFileName, value);
    }

    /// <inheritdoc cref="AddedLinesProperty"/>
    public int AddedLines
    {
        get => _addedLines;
        private set => SetAndRaise(AddedLinesProperty, ref _addedLines, value);
    }

    /// <inheritdoc cref="RemovedLinesProperty"/>
    public int RemovedLines
    {
        get => _removedLines;
        private set => SetAndRaise(RemovedLinesProperty, ref _removedLines, value);
    }

    /// <inheritdoc cref="IsMovingProperty"/>
    public bool IsMoving
    {
        get => _isMoving;
        private set
        {
            if (SetAndRaise(IsMovingProperty, ref _isMoving, value))
            {
                PseudoClasses.Set(MovingPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetAndRaise(IsPlayingProperty, ref _isPlaying, value))
            {
                PseudoClasses.Set(PlayingPseudoClass, value);
                UpdatePlayButton();
            }
        }
    }

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<CodeMagicMoveValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>Moves to the next step (nothing at the last one).</summary>
    public void Next() => GoTo(Value + 1);

    /// <summary>Moves to the previous step (nothing at the first one).</summary>
    public void Previous() => GoTo(Value - 1);

    /// <summary>Moves to step <paramref name="index"/> (clamped to the steps).</summary>
    public void GoTo(int index)
    {
        if (Steps.Count == 0)
        {
            return;
        }
        Value = Math.Clamp(index, 0, Steps.Count - 1);
    }

    // --- what the surface reads ----------------------------------------------------------------------------------------

    internal int MaxColumns { get; private set; }

    internal int MaxLineCount { get; private set; } = 1;

    internal CodeMagicMoveSource RestSource => Value >= 0 && Value < _sources.Length ? _sources[Value] : CodeMagicMoveSource.Empty;

    internal CodeMagicMoveBand[] RestBands => Value >= 0 && Value < _bands.Length ? _bands[Value] : [];

    internal double DisplayedLines => Plan?.Lines ?? RestSource.LineCount;

    internal string ViewportName => Steps.Count == 0
        ? Label
        : string.Format(CultureInfo.InvariantCulture, "{0}, step {1} of {2}", Label, Value + 1, Steps.Count);

    internal string KeyboardHint => KeyboardHintText;

    /// <summary>The version of each step as the surface draws it (tests).</summary>
    internal CodeMagicMoveSource SourceOf(int index)
    {
        EnsureSteps();
        return _sources[index];
    }

    /// <summary>The bands of each step (tests).</summary>
    internal CodeMagicMoveBand[] BandsOf(int index)
    {
        EnsureSteps();
        return _bands[index];
    }

    private static int CoerceValue(AvaloniaObject sender, int value)
    {
        var owner = (CodeMagicMove)sender;
        var count = owner.Steps.Count;
        return count == 0 ? 0 : Math.Clamp(value, 0, count - 1);
    }

    private void OnStepChanged(object? sender, EventArgs e) => StepsChanged();

    private void StepsChanged()
    {
        _stepsDirty = true;
        CancelMotion();
        CoerceValue(ValueProperty);
        EnsureSteps();
        UpdateCurrent(announce: false);
        RebuildDots();
        _surface?.InvalidateAllWords();
        _surface?.InvalidateMeasure();
        StartHighlighting();
        UpdateLoop();
    }

    private void EnsureSteps()
    {
        if (!_stepsDirty)
        {
            return;
        }
        _stepsDirty = false;
        var count = Steps.Count;
        _sources = new CodeMagicMoveSource[count];
        _bands = new CodeMagicMoveBand[count][];
        _counts = new (int, int)[count];
        var columns = 0;
        var lines = 1;
        for (var i = 0; i < count; i++)
        {
            _sources[i] = CodeMagicMoveSource.Parse(Steps[i].Code);
            columns = Math.Max(columns, _sources[i].MaxColumns);
            lines = Math.Max(lines, _sources[i].LineCount);
            if (i == 0)
            {
                _bands[i] = [];
                _counts[i] = (0, 0);
            }
            else
            {
                var match = CodeMagicMoveDiff.Match(_sources[i - 1], _sources[i]);
                _bands[i] = CodeMagicMoveDiff.Bands(_sources[i], match);
                _counts[i] = CodeMagicMoveDiff.LineCounts(_sources[i - 1], _sources[i]);
            }
        }
        MaxColumns = columns;
        MaxLineCount = lines;
    }

    private void UpdateCurrent(bool announce)
    {
        EnsureSteps();
        var index = Value;
        var step = index >= 0 && index < Steps.Count ? Steps[index] : null;
        CurrentStep = step;
        CurrentFileName = step?.FileName ?? FileName;
        var counts = index >= 0 && index < _counts.Length ? _counts[index] : (0, 0);
        AddedLines = counts.Item1;
        RemovedLines = counts.Item2;
        PseudoClasses.Set(FirstStepPseudoClass, index <= 0);
        if (_added is not null)
        {
            _added.Text = "+" + AddedLines.ToString(CultureInfo.InvariantCulture);
        }
        if (_removed is not null)
        {
            // U+2212 minus, as a diff header writes it.
            _removed.Text = "\u2212" + RemovedLines.ToString(CultureInfo.InvariantCulture);
        }
        if (_source is not null)
        {
            // The real code of the step for screen readers; the drawn layer is not read.
            _source.Text = index >= 0 && index < _sources.Length ? _sources[index].Text : string.Empty;
        }
        if (_previous is not null)
        {
            _previous.IsEnabled = index > 0;
        }
        if (_next is not null)
        {
            _next.IsEnabled = index < Steps.Count - 1;
        }
        if (_dots is not null)
        {
            for (var i = 0; i < _dots.Children.Count; i++)
            {
                if (_dots.Children[i] is CodeMagicMoveDot dot)
                {
                    dot.IsCurrent = i == index;
                }
            }
        }
        if (_surface is not null)
        {
            AutomationProperties.SetName(_surface, ViewportName);
        }
        if (announce && _status is not null && step is not null)
        {
            _status.Text = Announcement(index);
        }
        UpdateFill();
    }

    /// <summary>What the live region says for a step the person chose.</summary>
    internal string Announcement(int index)
    {
        EnsureSteps();
        var step = Steps[index];
        var text = string.Format(CultureInfo.InvariantCulture, "Step {0} of {1}: {2}.", index + 1, Steps.Count, step.Title);
        if (index > 0)
        {
            var (added, removed) = _counts[index];
            text += string.Format(CultureInfo.InvariantCulture, " {0} {1} added, {2} removed.", added, added == 1 ? "line" : "lines", removed);
        }
        return text;
    }

    private void RebuildDots()
    {
        if (_dots is null)
        {
            return;
        }
        foreach (var child in _dots.Children)
        {
            if (child is CodeMagicMoveDot old)
            {
                old.Click -= OnDotClick;
            }
        }
        _dots.Children.Clear();
        for (var i = 0; i < Steps.Count; i++)
        {
            var dot = new CodeMagicMoveDot { Tag = i, IsCurrent = i == Value };
            dot[!CodeMagicMoveDot.FillBrushProperty] = this[!AccentBrushProperty];
            AutomationProperties.SetName(dot, string.Format(CultureInfo.InvariantCulture, "Step {0}: {1}", i + 1, Steps[i].Title));
            dot.Click += OnDotClick;
            _dots.Children.Add(dot);
        }
        UpdateFill();
    }

    private void OnDotClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CodeMagicMoveDot { Tag: int index })
        {
            UserGoTo(index);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _surface = e.NameScope.Find<CodeMagicMoveSurface>(SurfacePart);
        _dots = e.NameScope.Find<Panel>(DotsPart);
        _previous = e.NameScope.Find<AvaloniaButton>(PreviousPart);
        _next = e.NameScope.Find<AvaloniaButton>(NextPart);
        _play = e.NameScope.Find<AvaloniaButton>(PlayPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _source = e.NameScope.Find<TextBlock>(SourcePart);
        _added = e.NameScope.Find<TextBlock>(AddedPart);
        _removed = e.NameScope.Find<TextBlock>(RemovedPart);
        if (_surface is not null)
        {
            _surface.Owner = this;
            AttachDrag(_surface);
            AutomationProperties.SetHelpText(_surface, KeyboardHintText);
        }
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_play is not null)
        {
            _play.Click += OnPlayClick;
        }
        RebuildDots();
        UpdateCurrent(announce: false);
        UpdatePlayButton();
        UpdateCompact(Bounds.Width);
    }

    private void DetachParts()
    {
        if (_surface is not null)
        {
            _surface.Owner = null;
            DetachDrag();
        }
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_play is not null)
        {
            _play.Click -= OnPlayClick;
        }
        if (_dots is not null)
        {
            foreach (var child in _dots.Children)
            {
                if (child is CodeMagicMoveDot dot)
                {
                    dot.Click -= OnDotClick;
                }
            }
            _dots.Children.Clear();
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => UserGoTo(Value - 1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => UserGoTo(Value + 1);

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (IsPlaying)
        {
            IsPaused = true;
            return;
        }
        IsPaused = false;
        if (!AutoPlay)
        {
            AutoPlay = true;
        }
        // Play picks up where the walkthrough is and moves on at once.
        if (IsPlaying && Plan is null && Steps.Count > 1)
        {
            Value = (Value + 1) % Steps.Count;
        }
    }

    private void UpdatePlayButton()
    {
        if (_play is null)
        {
            return;
        }
        AutomationProperties.SetName(_play, IsPlaying ? "Pause walkthrough" : "Play walkthrough");
        _play.Classes.Set("playing", IsPlaying);
    }

    /// <summary>A step the person chose: stops autoplay and is announced.</summary>
    internal void UserGoTo(int index)
    {
        if (Steps.Count == 0)
        {
            return;
        }
        var target = Math.Clamp(index, 0, Steps.Count - 1);
        IsPaused = true;
        if (target == Value && Plan is null)
        {
            return;
        }
        _announceNext = true;
        Value = target;
        _announceNext = false;
    }

    internal void HandleSurfaceKey(KeyEventArgs e)
    {
        if (e.Handled || Steps.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                UserGoTo(Value - 1);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                UserGoTo(Value + 1);
                e.Handled = true;
                break;
            case Key.Home:
                UserGoTo(0);
                e.Handled = true;
                break;
            case Key.End:
                UserGoTo(Steps.Count - 1);
                e.Handled = true;
                break;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var (oldValue, newValue) = change.GetOldAndNewValue<int>();
            OnValueChanged(oldValue, newValue);
            RaiseEvent(new CodeMagicMoveValueChangedEventArgs(ValueChangedEvent, oldValue, newValue));
        }
        else if (change.Property == FileNameProperty || change.Property == LabelProperty)
        {
            UpdateCurrent(announce: false);
        }
        else if (change.Property == LanguageProperty)
        {
            StartHighlighting();
        }
        else if (change.Property == BackgroundProperty)
        {
            var dark = Background is ISolidColorBrush solid && Oklch.FromColor(solid.Color).L < 0.5;
            if (dark != _isDark)
            {
                _isDark = dark;
                StartHighlighting();
            }
        }
        else if (change.Property == AutoPlayProperty || change.Property == IsPausedProperty)
        {
            _rest = 0;
            UpdateLoop();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        var limit = _surface?.CompactWidth ?? 520;
        PseudoClasses.Set(CompactPseudoClass, width > 0 && width < limit);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hovered = true;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartHighlighting();
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelMotion();
        ReleaseHighlighting();
    }
}
