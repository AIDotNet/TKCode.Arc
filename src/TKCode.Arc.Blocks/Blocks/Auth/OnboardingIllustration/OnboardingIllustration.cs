using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
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
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A compact first-run card: a line drawing on top whose shapes morph from scene to scene (an empty box, files dropping
/// in, a team around a board, a badge), and below it the step's title and sentence, a row of step dots, Back and a main
/// button. The main button says Next, then Get started on the last step, then Replay intro once the onboarding is
/// complete; Skip in the drawing's corner jumps to the last step. The Left and Right arrow keys step back and forth
/// anywhere in the card. With no <see cref="Steps"/> the card shows four sample steps, one per scene.
/// </summary>
/// <remarks>
/// The block creates nothing: <see cref="GetStarted"/> (and <see cref="GetStartedCommand"/>) tell the app the person is
/// done; <see cref="StepChanged"/> reports every move with its reason. Unlike the web original, focus never drops to
/// the page: when Skip disappears while it has focus, focus moves to the main button.
/// </remarks>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(StagePart, typeof(Control))]
[TemplatePart(ArtPart, typeof(Control))]
[TemplatePart(SkipPart, typeof(AvaloniaButton))]
[TemplatePart(CopyPart, typeof(Control))]
[TemplatePart(DotsPart, typeof(Panel))]
[TemplatePart(BackPart, typeof(AvaloniaButton))]
[TemplatePart(BackTextPart, typeof(TextBlock))]
[TemplatePart(PrimaryPart, typeof(AvaloniaButton))]
[TemplatePart(PrimaryLabelPart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(FirstPseudoClass, LastPseudoClass, CompletedPseudoClass, CompactPseudoClass, UnframedPseudoClass)]
public class OnboardingIllustration : TemplatedControl
{
    public const string CardPart = "PART_Card";
    public const string StagePart = "PART_Stage";
    public const string ArtPart = "PART_Art";
    public const string SkipPart = "PART_Skip";
    public const string CopyPart = "PART_Copy";
    public const string DotsPart = "PART_Dots";
    public const string BackPart = "PART_Back";
    public const string BackTextPart = "PART_BackText";
    public const string PrimaryPart = "PART_Primary";
    public const string PrimaryLabelPart = "PART_PrimaryLabel";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set on the first step: Back is unavailable.</summary>
    public const string FirstPseudoClass = ":first";

    /// <summary>Set on the last step: the main button says Get started and Skip is gone.</summary>
    public const string LastPseudoClass = ":last";

    /// <summary>Set once the onboarding is complete.</summary>
    public const string CompletedPseudoClass = ":completed";

    /// <summary>Set while the control is narrower than <see cref="CompactWidth"/>: tighter padding, Back shows only its arrow.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while <see cref="IsFramed"/> is false: no border, corner radius or shadow.</summary>
    public const string UnframedPseudoClass = ":unframed";

    /// <summary>Below this width (the card's own width; Arc's 440px viewport less its margins) the layout is compact.</summary>
    public const double CompactWidth = 410;

    /// <summary>Skip fades in and out on CSS ease-out in 160ms.</summary>
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    #region Public properties and events

    /// <summary>The steps, in order (each with its scene and text). Empty (the default) shows four sample steps.</summary>
    public static readonly DirectProperty<OnboardingIllustration, AvaloniaList<OnboardingStep>> StepsProperty =
        AvaloniaProperty.RegisterDirect<OnboardingIllustration, AvaloniaList<OnboardingStep>>(nameof(Steps), o => o.Steps);

    /// <summary>Steps from a view model; when set it wins over <see cref="Steps"/>, and changes to an observable collection show at once.</summary>
    public static readonly StyledProperty<IEnumerable<OnboardingStep>?> StepsSourceProperty =
        AvaloniaProperty.Register<OnboardingIllustration, IEnumerable<OnboardingStep>?>(nameof(StepsSource));

    /// <summary>The step shown, 0-based (two way). Values outside the steps are clamped.</summary>
    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<OnboardingIllustration, int>(nameof(CurrentIndex), 0, defaultBindingMode: BindingMode.TwoWay,
            coerce: (o, value) => ((OnboardingIllustration)o).CoerceIndex(value));

    /// <summary>
    /// True once Get started was pressed (two way): the card shows <see cref="CompletedTitle"/> and
    /// <see cref="CompletedDescription"/>, Back is unavailable and the main button replays the steps. Setting it moves to
    /// the last step; a step dot or a new <see cref="CurrentIndex"/> leaves it.
    /// </summary>
    public static readonly StyledProperty<bool> IsCompletedProperty =
        AvaloniaProperty.Register<OnboardingIllustration, bool>(nameof(IsCompleted), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows Skip in the drawing's corner on every step but the last (default true).</summary>
    public static readonly StyledProperty<bool> IsSkipVisibleProperty =
        AvaloniaProperty.Register<OnboardingIllustration, bool>(nameof(IsSkipVisible), true);

    /// <summary>
    /// Get started enters the completed state (default true). False: Get started only raises <see cref="GetStarted"/>
    /// and the app moves on (closes the dialog, opens the workspace).
    /// </summary>
    public static readonly StyledProperty<bool> IsCompletionVisibleProperty =
        AvaloniaProperty.Register<OnboardingIllustration, bool>(nameof(IsCompletionVisible), true);

    /// <summary>Draws the card's border, rounded corners and shadow (default true). False for a card that fills a sheet or a phone screen edge to edge.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<OnboardingIllustration, bool>(nameof(IsFramed), true);

    /// <summary>The title of the completed state.</summary>
    public static readonly StyledProperty<string> CompletedTitleProperty =
        AvaloniaProperty.Register<OnboardingIllustration, string>(nameof(CompletedTitle), "Your space is open");

    /// <summary>The sentence of the completed state.</summary>
    public static readonly StyledProperty<string> CompletedDescriptionProperty =
        AvaloniaProperty.Register<OnboardingIllustration, string>(nameof(CompletedDescription), "Replay the tour to walk through each step again.");

    /// <summary>The accessible name of the card's region, such as "Northdesk onboarding".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<OnboardingIllustration, string>(nameof(Label), "Onboarding");

    /// <summary>The button labels, the dots' names and the announced status texts.</summary>
    public static readonly StyledProperty<OnboardingIllustrationLabels> LabelsProperty =
        AvaloniaProperty.Register<OnboardingIllustration, OnboardingIllustrationLabels>(nameof(Labels), new OnboardingIllustrationLabels());

    /// <summary>Runs when Get started is pressed, after <see cref="GetStarted"/>; its parameter is the event arguments.</summary>
    public static readonly StyledProperty<ICommand?> GetStartedCommandProperty =
        AvaloniaProperty.Register<OnboardingIllustration, ICommand?>(nameof(GetStartedCommand));

    /// <summary>Runs when Skip is pressed, after <see cref="Skipped"/>; its parameter is the event arguments.</summary>
    public static readonly StyledProperty<ICommand?> SkipCommandProperty =
        AvaloniaProperty.Register<OnboardingIllustration, ICommand?>(nameof(SkipCommand));

    /// <summary>The step changed (Next, Back, a dot, an arrow key, Skip, Replay or code), with the old and new index and the reason.</summary>
    public static readonly RoutedEvent<OnboardingStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<OnboardingIllustration, OnboardingStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Get started was pressed on the last step: start the app's own flow here.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> GetStartedEvent =
        RoutedEvent.Register<OnboardingIllustration, RoutedEventArgs>(nameof(GetStarted), RoutingStrategies.Bubble);

    /// <summary>Skip was pressed (or <see cref="Skip"/> called); the card is now on the last step.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SkippedEvent =
        RoutedEvent.Register<OnboardingIllustration, RoutedEventArgs>(nameof(Skipped), RoutingStrategies.Bubble);

    /// <summary>Replay intro was pressed (or <see cref="Replay"/> called); the card is back on the first step.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ReplayedEvent =
        RoutedEvent.Register<OnboardingIllustration, RoutedEventArgs>(nameof(Replayed), RoutingStrategies.Bubble);

    private readonly AvaloniaList<OnboardingStep> _steps = new();

    /// <inheritdoc cref="StepsProperty"/>
    [Content]
    public AvaloniaList<OnboardingStep> Steps => _steps;

    /// <inheritdoc cref="StepsSourceProperty"/>
    public IEnumerable<OnboardingStep>? StepsSource { get => GetValue(StepsSourceProperty); set => SetValue(StepsSourceProperty, value); }

    /// <inheritdoc cref="CurrentIndexProperty"/>
    public int CurrentIndex { get => GetValue(CurrentIndexProperty); set => SetValue(CurrentIndexProperty, value); }

    /// <inheritdoc cref="IsCompletedProperty"/>
    public bool IsCompleted { get => GetValue(IsCompletedProperty); set => SetValue(IsCompletedProperty, value); }

    /// <inheritdoc cref="IsSkipVisibleProperty"/>
    public bool IsSkipVisible { get => GetValue(IsSkipVisibleProperty); set => SetValue(IsSkipVisibleProperty, value); }

    /// <inheritdoc cref="IsCompletionVisibleProperty"/>
    public bool IsCompletionVisible { get => GetValue(IsCompletionVisibleProperty); set => SetValue(IsCompletionVisibleProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="CompletedTitleProperty"/>
    public string CompletedTitle { get => GetValue(CompletedTitleProperty); set => SetValue(CompletedTitleProperty, value); }

    /// <inheritdoc cref="CompletedDescriptionProperty"/>
    public string CompletedDescription { get => GetValue(CompletedDescriptionProperty); set => SetValue(CompletedDescriptionProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public OnboardingIllustrationLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="GetStartedCommandProperty"/>
    public ICommand? GetStartedCommand { get => GetValue(GetStartedCommandProperty); set => SetValue(GetStartedCommandProperty, value); }

    /// <inheritdoc cref="SkipCommandProperty"/>
    public ICommand? SkipCommand { get => GetValue(SkipCommandProperty); set => SetValue(SkipCommandProperty, value); }

    /// <inheritdoc cref="StepChangedEvent"/>
    public event EventHandler<OnboardingStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <inheritdoc cref="GetStartedEvent"/>
    public event EventHandler<RoutedEventArgs>? GetStarted
    {
        add => AddHandler(GetStartedEvent, value);
        remove => RemoveHandler(GetStartedEvent, value);
    }

    /// <inheritdoc cref="SkippedEvent"/>
    public event EventHandler<RoutedEventArgs>? Skipped
    {
        add => AddHandler(SkippedEvent, value);
        remove => RemoveHandler(SkippedEvent, value);
    }

    /// <inheritdoc cref="ReplayedEvent"/>
    public event EventHandler<RoutedEventArgs>? Replayed
    {
        add => AddHandler(ReplayedEvent, value);
        remove => RemoveHandler(ReplayedEvent, value);
    }

    #endregion

    private readonly OnboardingStep[] _samples = OnboardingIllustrationSamples.Steps();
    private readonly List<OnboardingStep> _watched = new();
    private INotifyCollectionChanged? _watchedSource;

    private Border? _card;
    private Control? _stage;
    private OnboardingArt? _art;
    private AvaloniaButton? _skip;
    private OnboardingSwap? _copy;
    private OnboardingDotRow? _dots;
    private OnboardingButton? _back;
    private TextBlock? _backText;
    private AvaloniaButton? _primary;
    private OnboardingSwap? _primaryLabel;
    private AuthAnnouncer? _announcer;

    private MotionValue<double>? _skipFade;
    private bool _skipShown = true;
    private OnboardingStepChangeReason? _pendingReason;
    private bool _completing;
    private int _defer;
    private int _shownIndex;
    private object? _shownCopy;
    private bool _ready;
    private bool _attached;
    private bool _compact;

    static OnboardingIllustration()
    {
        FocusableProperty.OverrideDefaultValue<OnboardingIllustration>(false);
    }

    public OnboardingIllustration()
    {
        _steps.CollectionChanged += (_, _) => OnStepsChanged();
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
    }

    /// <summary>The steps shown: <see cref="StepsSource"/>, else <see cref="Steps"/>, else the samples.</summary>
    private IReadOnlyList<OnboardingStep> EffectiveSteps
    {
        get
        {
            if (StepsSource is { } source)
            {
                var list = source as IReadOnlyList<OnboardingStep> ?? source.ToList();
                if (list.Count > 0)
                {
                    return list;
                }
            }
            return _steps.Count > 0 ? _steps : _samples;
        }
    }

    private int LastIndex => EffectiveSteps.Count - 1;

    #region Test hooks

    internal OnboardingArt? Art => _art;

    internal OnboardingSwap? CopySwap => _copy;

    internal OnboardingSwap? PrimaryLabelSwap => _primaryLabel;

    internal OnboardingDotRow? DotRow => _dots;

    internal OnboardingButton? BackButton => _back;

    internal AvaloniaButton? PrimaryButton => _primary;

    internal AvaloniaButton? SkipButton => _skip;

    internal string? Announcement => _announcer?.Announcement;

    /// <summary>The copy now on screen: (title, description).</summary>
    internal (string? Title, string? Description) DisplayedCopy => _copy?.CurrentContent is OnboardingCopy copy ? (copy.Title, copy.Description) : (null, null);

    /// <summary>The main button's label now on screen.</summary>
    internal (OnboardingPrimaryKind Kind, string? Text) DisplayedPrimary =>
        _primaryLabel?.CurrentContent is OnboardingPrimaryLabel label ? (label.Kind, label.Text) : (OnboardingPrimaryKind.Next, null);

    #endregion

    #region Public methods

    /// <summary>Goes to the next step; false on the last step or once complete.</summary>
    public bool Next() => !IsCompleted && CurrentIndex < LastIndex && Move(CurrentIndex + 1, OnboardingStepChangeReason.Next);

    /// <summary>Goes back one step; false on the first step or once complete.</summary>
    public bool Back() => !IsCompleted && CurrentIndex > 0 && Move(CurrentIndex - 1, OnboardingStepChangeReason.Back);

    /// <summary>Shows the step at <paramref name="index"/> (clamped), leaving the completed state; false when nothing changed.</summary>
    public bool GoTo(int index) => GoToStep(index, OnboardingStepChangeReason.Programmatic);

    /// <summary>Jumps to the last step, raises <see cref="Skipped"/> and runs <see cref="SkipCommand"/>; false on the last step or once complete.</summary>
    public bool Skip()
    {
        if (IsCompleted || CurrentIndex >= LastIndex)
        {
            return false;
        }
        Move(LastIndex, OnboardingStepChangeReason.Skip);
        var args = new RoutedEventArgs(SkippedEvent, this);
        RaiseEvent(args);
        if (SkipCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        return true;
    }

    /// <summary>Enters the completed state on the last step (as Get started does, without raising <see cref="GetStarted"/>); false when already complete.</summary>
    public bool Complete()
    {
        if (IsCompleted)
        {
            return false;
        }
        SetCurrentValue(IsCompletedProperty, true);
        return true;
    }

    /// <summary>Leaves the completed state and goes back to the first step, raising <see cref="Replayed"/>; false when not complete.</summary>
    public bool Replay()
    {
        if (!IsCompleted)
        {
            return false;
        }
        _defer++;
        try
        {
            SetCurrentValue(IsCompletedProperty, false);
            if (CurrentIndex != 0)
            {
                Move(0, OnboardingStepChangeReason.Replay);
            }
        }
        finally
        {
            EndDefer();
        }
        RaiseEvent(new RoutedEventArgs(ReplayedEvent, this));
        return true;
    }

    #endregion

    #region State

    private int CoerceIndex(int value)
    {
        var count = EffectiveSteps.Count;
        return Math.Clamp(value, 0, Math.Max(0, count - 1));
    }

    private bool GoToStep(int index, OnboardingStepChangeReason reason)
    {
        index = CoerceIndex(index);
        if (!IsCompleted)
        {
            return index != CurrentIndex && Move(index, reason);
        }
        // A dot (or code) leaves the completed state, even for the step already shown.
        _defer++;
        try
        {
            SetCurrentValue(IsCompletedProperty, false);
            if (index != CurrentIndex)
            {
                Move(index, reason);
            }
        }
        finally
        {
            EndDefer();
        }
        return true;
    }

    private bool Move(int index, OnboardingStepChangeReason reason)
    {
        _pendingReason = reason;
        try
        {
            SetCurrentValue(CurrentIndexProperty, index);
        }
        finally
        {
            _pendingReason = null;
        }
        return true;
    }

    private void EndDefer()
    {
        _defer--;
        if (_defer == 0)
        {
            UpdateView();
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _ready = true;
        ClampIndex();
        _shownIndex = CurrentIndex;
        UpdateView();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentIndexProperty)
        {
            OnIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == IsCompletedProperty)
        {
            OnCompletedChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == StepsSourceProperty)
        {
            WatchSource();
            OnStepsChanged();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(UnframedPseudoClass, !IsFramed);
        }
        else if (change.Property == LabelsProperty || change.Property == CompletedTitleProperty ||
                 change.Property == CompletedDescriptionProperty || change.Property == IsSkipVisibleProperty)
        {
            UpdateView();
        }
    }

    private void OnIndexChanged(int oldIndex, int newIndex)
    {
        var reason = _pendingReason ?? OnboardingStepChangeReason.Programmatic;
        if (!_ready)
        {
            return;
        }
        _defer++;
        try
        {
            // A new step from anywhere but Complete() itself leaves the completed state.
            if (IsCompleted && !_completing)
            {
                SetCurrentValue(IsCompletedProperty, false);
            }
        }
        finally
        {
            EndDefer();
        }
        RaiseEvent(new OnboardingStepChangedEventArgs(StepChangedEvent, oldIndex, newIndex, reason));
    }

    private void OnCompletedChanged(bool completed)
    {
        PseudoClasses.Set(CompletedPseudoClass, completed);
        if (!_ready)
        {
            return;
        }
        _defer++;
        try
        {
            if (completed && CurrentIndex != LastIndex)
            {
                // The completed state belongs to the last step (its dot is the current one, its drawing stays).
                _completing = true;
                try
                {
                    Move(LastIndex, OnboardingStepChangeReason.Programmatic);
                }
                finally
                {
                    _completing = false;
                }
            }
        }
        finally
        {
            EndDefer();
        }
    }

    private void OnStepsChanged()
    {
        if (!_ready)
        {
            return;
        }
        Watch();
        _defer++;
        try
        {
            ClampIndex();
        }
        finally
        {
            EndDefer();
        }
    }

    /// <summary>Clamps <see cref="CurrentIndex"/> to the steps and keeps the clamped value (more steps later do not jump ahead).</summary>
    private void ClampIndex()
    {
        CoerceValue(CurrentIndexProperty);
        SetCurrentValue(CurrentIndexProperty, CurrentIndex);
    }

    /// <summary>Follows an observable <see cref="StepsSource"/> while the block is in a window (so a long-lived source never holds it).</summary>
    private void WatchSource()
    {
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged -= OnSourceChanged;
            _watchedSource = null;
        }
        if (_attached && StepsSource is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnSourceChanged;
            _watchedSource = observable;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnStepsChanged();

    /// <summary>Follows text and scene edits of the steps shown, while the block is in a window.</summary>
    private void Watch()
    {
        foreach (var step in _watched)
        {
            step.PropertyChanged -= OnStepPropertyChanged;
        }
        _watched.Clear();
        if (!_attached)
        {
            return;
        }
        foreach (var step in EffectiveSteps)
        {
            step.PropertyChanged += OnStepPropertyChanged;
            _watched.Add(step);
        }
    }

    private void OnStepPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => UpdateView();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        WatchSource();
        Watch();
        if (_ready)
        {
            // Back in a window: what changed in the steps while it was away shows now.
            ClampIndex();
            UpdateView();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        WatchSource();
        Watch();
    }

    #endregion

    #region View

    private sealed record CopyKey(bool Completed, string? Title, string? Description);

    private sealed record PrimaryKey(OnboardingPrimaryKind Kind, string Text);

    /// <summary>Shows the current step (or the completed state): drawing, copy, buttons, dots and Skip.</summary>
    private void UpdateView()
    {
        if (!_ready || _defer > 0)
        {
            return;
        }
        var steps = EffectiveSteps;
        var index = Math.Clamp(CurrentIndex, 0, steps.Count - 1);
        var last = steps.Count - 1;
        var completed = IsCompleted;
        var step = steps[index];
        var labels = Labels;

        PseudoClasses.Set(FirstPseudoClass, index == 0);
        PseudoClasses.Set(LastPseudoClass, index == last);
        PseudoClasses.Set(CompletedPseudoClass, completed);

        var forward = index >= _shownIndex;
        _shownIndex = index;
        _art?.Show(step.Scene, forward, step.SceneDescription);

        var copy = completed ? new CopyKey(true, CompletedTitle, CompletedDescription) : new CopyKey(false, step.Title, step.Description);
        var compact = _compact;
        _copy?.Show(copy, () => new OnboardingCopy { Title = copy.Title, Description = copy.Description, IsCompact = compact });
        if (_shownCopy is not null && !Equals(_shownCopy, copy))
        {
            var status = completed ? labels.CompletedStatus : labels.StepStatus(index + 1, steps.Count);
            _announcer?.Announce(Sentences(copy.Title, copy.Description, status));
        }
        _shownCopy = copy;

        var kind = completed ? OnboardingPrimaryKind.Replay : index == last ? OnboardingPrimaryKind.GetStarted : OnboardingPrimaryKind.Next;
        var text = kind switch
        {
            OnboardingPrimaryKind.Replay => labels.ReplayIntro,
            OnboardingPrimaryKind.GetStarted => labels.GetStarted,
            _ => labels.Next
        };
        var primary = new PrimaryKey(kind, text);
        _primaryLabel?.Show(primary, () => new OnboardingPrimaryLabel { Kind = primary.Kind, Text = primary.Text });
        if (_primary is not null)
        {
            AutomationProperties.SetName(_primary, text);
        }

        if (_back is not null)
        {
            _back.IsInert = completed || index == 0;
            if (_backText is not null)
            {
                _backText.Text = labels.Back;
            }
            AutomationProperties.SetName(_back, labels.Back);
        }
        if (_skip is not null)
        {
            _skip.Content = labels.Skip;
            AutomationProperties.SetName(_skip, labels.Skip);
        }
        SetSkipShown(IsSkipVisible && !completed && index < last);

        if (_dots is not null)
        {
            AutomationProperties.SetName(_dots, labels.StepsGroupName);
            _dots.SetCount(steps.Count);
            for (var i = 0; i < steps.Count; i++)
            {
                var dot = _dots.Dots[i];
                var state = completed
                    ? i < last ? OnboardingDotState.Complete : OnboardingDotState.Current
                    : i < index ? OnboardingDotState.Complete : i == index ? OnboardingDotState.Current : OnboardingDotState.Upcoming;
                dot.State = state;
                AutomationProperties.SetName(dot, labels.DotName(i + 1, steps[i].Title));
                AutomationProperties.SetItemStatus(dot, state == OnboardingDotState.Current ? labels.CurrentDotStatus : string.Empty);
                AutomationProperties.SetPositionInSet(dot, i + 1);
                AutomationProperties.SetSizeOfSet(dot, steps.Count);
            }
        }
    }

    /// <summary>Joins the title, the sentence and the status into what the live region reads.</summary>
    private static string Sentences(params string?[] parts)
    {
        var text = string.Empty;
        foreach (var part in parts)
        {
            var piece = part?.Trim();
            if (string.IsNullOrEmpty(piece))
            {
                continue;
            }
            if (text.Length > 0)
            {
                text += text[^1] is '.' or '!' or '?' or '…' or ':' ? " " : ". ";
            }
            text += piece;
        }
        return text;
    }

    /// <summary>Skip fades in and out (160ms ease-out, also under reduced motion) and leaves the Tab order once gone.</summary>
    private void SetSkipShown(bool shown)
    {
        if (_skip is not { } skip || shown == _skipShown)
        {
            return;
        }
        _skipShown = shown;
        if (!shown && skip.IsFocused)
        {
            // Arc lets focus fall to the page here; keep it in the card on the main button.
            var keyboard = skip.Classes.Contains(":focus-visible");
            _primary?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        skip.IsHitTestVisible = shown;
        skip.Focusable = shown;
        _skipFade ??= CreateSkipFade(skip);
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _skipFade.Jump(shown ? 1 : 0);
            skip.Opacity = shown ? 1 : 0;
            skip.IsVisible = shown;
            return;
        }
        if (shown)
        {
            skip.IsVisible = true;
        }
        _ = FadeSkip(skip, _skipFade, shown);
    }

    private static MotionValue<double> CreateSkipFade(AvaloniaButton skip)
    {
        var fade = new MotionValue<double>(skip, skip.IsVisible ? 1 : 0, MotionVectors.Double);
        fade.Changed += v => skip.Opacity = Math.Clamp(v, 0, 1);
        return fade;
    }

    private async Task FadeSkip(AvaloniaButton skip, MotionValue<double> fade, bool shown)
    {
        var done = await fade.AnimateTo(shown ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, s_easeOut));
        if (done && !shown && !_skipShown)
        {
            skip.IsVisible = false;
        }
    }

    #endregion

    #region Template and input

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stage is not null)
        {
            _stage.PointerMoved -= OnStagePointerMoved;
            _stage.PointerExited -= OnStagePointerExited;
        }
        if (_skip is not null)
        {
            _skip.Click -= OnSkipClick;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_primary is not null)
        {
            _primary.Click -= OnPrimaryClick;
        }
        if (_dots is not null)
        {
            _dots.Invoked = null;
        }
        _card = e.NameScope.Find<Border>(CardPart);
        _stage = e.NameScope.Find<Control>(StagePart);
        _art = e.NameScope.Find<OnboardingArt>(ArtPart);
        _skip = e.NameScope.Find<AvaloniaButton>(SkipPart);
        _copy = e.NameScope.Find<OnboardingSwap>(CopyPart);
        _dots = e.NameScope.Find<OnboardingDotRow>(DotsPart);
        _back = e.NameScope.Find<OnboardingButton>(BackPart);
        _backText = e.NameScope.Find<TextBlock>(BackTextPart);
        _primary = e.NameScope.Find<AvaloniaButton>(PrimaryPart);
        _primaryLabel = e.NameScope.Find<OnboardingSwap>(PrimaryLabelPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        if (_stage is not null)
        {
            _stage.PointerMoved += OnStagePointerMoved;
            _stage.PointerExited += OnStagePointerExited;
        }
        if (_skip is not null)
        {
            _skip.Click += OnSkipClick;
            _skipFade = null;
            _skipShown = _skip.IsVisible;
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_primary is not null)
        {
            _primary.Click += OnPrimaryClick;
        }
        if (_dots is not null)
        {
            _dots.Invoked = OnDotInvoked;
        }
        _shownCopy = null;
        ApplyDensity(Bounds.Width);
        UpdateView();
    }

    private void OnSkipClick(object? sender, RoutedEventArgs e) => Skip();

    private void OnBackClick(object? sender, RoutedEventArgs e) => Back();

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (IsCompleted)
        {
            Replay();
            return;
        }
        if (CurrentIndex < LastIndex)
        {
            Next();
            return;
        }
        var args = new RoutedEventArgs(GetStartedEvent, this);
        RaiseEvent(args);
        if (GetStartedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        if (IsCompletionVisible)
        {
            Complete();
        }
    }

    private void OnDotInvoked(int index) => GoToStep(index, OnboardingStepChangeReason.Dot);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Left or Key.Right))
        {
            return;
        }
        // The arrows step anywhere in the card and never move focus; they do nothing at the ends, nor once complete
        // (Right never presses Get started).
        e.Handled = true;
        if (IsCompleted)
        {
            return;
        }
        if (e.Key == Key.Left && CurrentIndex > 0)
        {
            Move(CurrentIndex - 1, OnboardingStepChangeReason.Keyboard);
        }
        else if (e.Key == Key.Right && CurrentIndex < LastIndex)
        {
            Move(CurrentIndex + 1, OnboardingStepChangeReason.Keyboard);
        }
    }

    private void OnStagePointerMoved(object? sender, PointerEventArgs e)
    {
        // Parallax follows a mouse only (Arc's (hover: hover) and (pointer: fine)).
        if (_stage is not { } stage || _art is null || e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        var size = stage.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var p = e.GetPosition(stage);
        _art.PointAt((p.X - size.Width / 2) / (size.Width / 2), (p.Y - size.Height / 2) / (size.Height / 2));
    }

    private void OnStagePointerExited(object? sender, PointerEventArgs e) => _art?.ReleasePointer();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            ApplyDensity(e.NewSize.Width);
        }
    }

    /// <summary>
    /// The card is min(440, the space there is) wide, like Arc's: a host that leaves the width open (a centered stack)
    /// still gets the full card, not one shrunk to its drawing.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var max = _card is { MaxWidth: > 0 and < double.PositiveInfinity } card ? card.MaxWidth : 440;
        var open = double.IsInfinity(availableSize.Width);
        var size = base.MeasureOverride(open ? availableSize.WithWidth(max) : availableSize);
        var width = open ? max : Math.Min(max, availableSize.Width);
        return new Size(Math.Max(size.Width, width), size.Height);
    }

    /// <summary>The compact layout follows the block's own width (Arc's viewport query, mapped to the card).</summary>
    private void ApplyDensity(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width < CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        _copy?.ForEachContent(c =>
        {
            if (c is OnboardingCopy copy)
            {
                copy.IsCompact = compact;
            }
        });
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RegionAutomationPeer(this);

    private sealed class RegionAutomationPeer(OnboardingIllustration owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    #endregion
}
