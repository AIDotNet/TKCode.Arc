using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
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
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A guided form that asks for one thing at a time: a card with a progress list and a step count, one step of fields, and
/// Back and Continue buttons (Continue reads <see cref="CompleteLabel"/> on the last step). Submitting the last step shows a
/// success block in place of the fields and folds the buttons away. Steps slide 20 px in the direction of travel while the
/// card springs to the new step's height (both ways); done steps draw a check and the count rolls. Every step keeps its
/// fields alive while other steps are shown, so values survive Back and Continue. Enter in a single-line text field submits
/// the step. A step can block its submission with <see cref="MultiStepFormStep.ValidateAsync"/>, with
/// <see cref="StepValidating"/>, or by a field with a data validation error; <see cref="CompleteAsync"/> runs the final
/// submission with a spinner on the button.
/// </summary>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(ProgressListPart, typeof(Panel))]
[TemplatePart(ProgressScrollerPart, typeof(ScrollViewer))]
[TemplatePart(CountPrefixPart, typeof(TextBlock))]
[TemplatePart(CountDigitPart, typeof(MultiStepSwap))]
[TemplatePart(CountSuffixPart, typeof(TextBlock))]
[TemplatePart(ViewportPart, typeof(MultiStepViewport))]
[TemplatePart(ActionsPart, typeof(MultiStepCollapse))]
[TemplatePart(BackPart, typeof(MultiStepButton))]
[TemplatePart(NextPart, typeof(MultiStepButton))]
[TemplatePart(AnnouncerPart, typeof(AuthAnnouncer))]
[PseudoClasses(FirstPseudoClass, LastPseudoClass, CompletePseudoClass, NarrowPseudoClass, BusyPseudoClass, EmptyPseudoClass)]
public class MultiStepForm : TemplatedControl
{
    public const string CardPart = "PART_Card";
    public const string HeaderPart = "PART_Header";
    public const string ProgressListPart = "PART_ProgressList";
    public const string ProgressScrollerPart = "PART_ProgressScroller";
    public const string CountPrefixPart = "PART_CountPrefix";
    public const string CountDigitPart = "PART_CountDigit";
    public const string CountSuffixPart = "PART_CountSuffix";
    public const string ViewportPart = "PART_Viewport";
    public const string ActionsPart = "PART_Actions";
    public const string BackPart = "PART_Back";
    public const string NextPart = "PART_Next";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set on the first step (Back is disabled).</summary>
    public const string FirstPseudoClass = ":first";

    /// <summary>Set on the last step (the submit button reads <see cref="CompleteLabel"/>).</summary>
    public const string LastPseudoClass = ":last";

    /// <summary>Set while the success block is shown.</summary>
    public const string CompletePseudoClass = ":complete";

    /// <summary>Set while the card is at most 388 wide: the count stacks above the list and the paddings tighten.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while a step check or the final submission runs.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set while there are no steps: nothing is drawn (as in Arc).</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>The card is narrow at this width or less (Arc's 420 px breakpoint less two 16 px page gutters).</summary>
    internal const double NarrowWidth = 388;

    /// <summary>A step slides this far (px) in the direction of travel.</summary>
    internal const double SlideDistance = 20;

    /// <summary>The success block rises from this far below its place.</summary>
    internal const double SuccessRise = MultiStepSuccessView.RiseOffset;

    #region Properties

    /// <summary>Defines the <see cref="Steps"/> property.</summary>
    public static readonly DirectProperty<MultiStepForm, AvaloniaList<MultiStepFormStep>> StepsProperty =
        AvaloniaProperty.RegisterDirect<MultiStepForm, AvaloniaList<MultiStepFormStep>>(nameof(Steps), o => o.Steps);

    /// <summary>Defines the <see cref="StepsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<MultiStepFormStep>?> StepsSourceProperty =
        AvaloniaProperty.Register<MultiStepForm, IEnumerable<MultiStepFormStep>?>(nameof(StepsSource));

    /// <summary>Defines the <see cref="CurrentIndex"/> property.</summary>
    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<MultiStepForm, int>(nameof(CurrentIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsComplete"/> property.</summary>
    public static readonly StyledProperty<bool> IsCompleteProperty =
        AvaloniaProperty.Register<MultiStepForm, bool>(nameof(IsComplete), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsBusy"/> property.</summary>
    public static readonly DirectProperty<MultiStepForm, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<MultiStepForm, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>Defines the <see cref="StepCount"/> property.</summary>
    public static readonly DirectProperty<MultiStepForm, int> StepCountProperty =
        AvaloniaProperty.RegisterDirect<MultiStepForm, int>(nameof(StepCount), o => o.StepCount);

    /// <summary>Defines the <see cref="NextLabel"/> property.</summary>
    public static readonly StyledProperty<string> NextLabelProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(NextLabel), "Continue");

    /// <summary>Defines the <see cref="CompleteLabel"/> property.</summary>
    public static readonly StyledProperty<string> CompleteLabelProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(CompleteLabel), "Finish");

    /// <summary>Defines the <see cref="BackLabel"/> property.</summary>
    public static readonly StyledProperty<string> BackLabelProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(BackLabel), "Back");

    /// <summary>Defines the <see cref="SuccessTitle"/> property.</summary>
    public static readonly StyledProperty<string> SuccessTitleProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(SuccessTitle), "Thank you");

    /// <summary>Defines the <see cref="SuccessDescription"/> property.</summary>
    public static readonly StyledProperty<string> SuccessDescriptionProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(SuccessDescription), "We received everything we need.");

    /// <summary>Defines the <see cref="CountFormat"/> property.</summary>
    public static readonly StyledProperty<string> CountFormatProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(CountFormat), "Step {0} of {1}");

    /// <summary>Defines the <see cref="ProgressNameFormat"/> property.</summary>
    public static readonly StyledProperty<string> ProgressNameFormatProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(ProgressNameFormat), "Progress, step {0} of {1}");

    /// <summary>Defines the <see cref="AnnouncementFormat"/> property.</summary>
    public static readonly StyledProperty<string> AnnouncementFormatProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(AnnouncementFormat), "Step {0} of {1}");

    /// <summary>Defines the <see cref="FormName"/> property.</summary>
    public static readonly StyledProperty<string> FormNameProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(FormName), "Multi-step form");

    /// <summary>Defines the <see cref="CurrentStepText"/> property.</summary>
    public static readonly StyledProperty<string> CurrentStepTextProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(CurrentStepText), "Current step");

    /// <summary>Defines the <see cref="CompletedStepText"/> property.</summary>
    public static readonly StyledProperty<string> CompletedStepTextProperty =
        AvaloniaProperty.Register<MultiStepForm, string>(nameof(CompletedStepText), "Completed");

    /// <summary>Defines the <see cref="CompleteCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CompleteCommandProperty =
        AvaloniaProperty.Register<MultiStepForm, ICommand?>(nameof(CompleteCommand));

    /// <summary>
    /// The steps in order (write them as children in XAML). <see cref="StepsSource"/> wins when it is set. With no steps the
    /// form draws nothing.
    /// </summary>
    [Content]
    public AvaloniaList<MultiStepFormStep> Steps { get; }

    /// <summary>A collection of steps you own; it replaces <see cref="Steps"/> while set, and collection changes are followed.</summary>
    public IEnumerable<MultiStepFormStep>? StepsSource
    {
        get => GetValue(StepsSourceProperty);
        set => SetValue(StepsSourceProperty, value);
    }

    /// <summary>
    /// The zero-based step shown (two-way). It starts at 0; a value outside the steps is clamped once there are steps (Arc's
    /// <c>initialStep</c>). Setting it moves the form there with the motion of the direction of travel.
    /// </summary>
    public int CurrentIndex
    {
        get => GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    /// <summary>
    /// True once the last step was submitted: the success block replaces the fields and the buttons fold away (two-way).
    /// Set it back to false to show the steps again (see <see cref="Restart"/>).
    /// </summary>
    public bool IsComplete
    {
        get => GetValue(IsCompleteProperty);
        set => SetValue(IsCompleteProperty, value);
    }

    /// <summary>True while a step check (<see cref="MultiStepFormStep.ValidateAsync"/>) or <see cref="CompleteAsync"/> runs.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetAndRaise(IsBusyProperty, ref _isBusy, value);
    }

    /// <summary>The number of steps in use.</summary>
    public int StepCount
    {
        get => _stepCount;
        private set => SetAndRaise(StepCountProperty, ref _stepCount, value);
    }

    /// <summary>The submit button's label on every step but the last.</summary>
    public string NextLabel
    {
        get => GetValue(NextLabelProperty);
        set => SetValue(NextLabelProperty, value);
    }

    /// <summary>The submit button's label on the last step.</summary>
    public string CompleteLabel
    {
        get => GetValue(CompleteLabelProperty);
        set => SetValue(CompleteLabelProperty, value);
    }

    /// <summary>The back button's label.</summary>
    public string BackLabel
    {
        get => GetValue(BackLabelProperty);
        set => SetValue(BackLabelProperty, value);
    }

    /// <summary>The success block's title.</summary>
    public string SuccessTitle
    {
        get => GetValue(SuccessTitleProperty);
        set => SetValue(SuccessTitleProperty, value);
    }

    /// <summary>The line under the success title.</summary>
    public string SuccessDescription
    {
        get => GetValue(SuccessDescriptionProperty);
        set => SetValue(SuccessDescriptionProperty, value);
    }

    /// <summary>The visible count: {0} is the one-based step (the part that rolls), {1} the number of steps.</summary>
    public string CountFormat
    {
        get => GetValue(CountFormatProperty);
        set => SetValue(CountFormatProperty, value);
    }

    /// <summary>The progress list's accessible name: {0} the one-based step, {1} the number of steps.</summary>
    public string ProgressNameFormat
    {
        get => GetValue(ProgressNameFormatProperty);
        set => SetValue(ProgressNameFormatProperty, value);
    }

    /// <summary>What screen readers hear after a step change: {0} the one-based step, {1} the number of steps.</summary>
    public string AnnouncementFormat
    {
        get => GetValue(AnnouncementFormatProperty);
        set => SetValue(AnnouncementFormatProperty, value);
    }

    /// <summary>The card's accessible name.</summary>
    public string FormName
    {
        get => GetValue(FormNameProperty);
        set => SetValue(FormNameProperty, value);
    }

    /// <summary>Spoken status of the current entry in the progress list.</summary>
    public string CurrentStepText
    {
        get => GetValue(CurrentStepTextProperty);
        set => SetValue(CurrentStepTextProperty, value);
    }

    /// <summary>Spoken status of a done entry in the progress list.</summary>
    public string CompletedStepText
    {
        get => GetValue(CompletedStepTextProperty);
        set => SetValue(CompletedStepTextProperty, value);
    }

    /// <summary>Runs after <see cref="Completed"/> with the event arguments as its parameter.</summary>
    public ICommand? CompleteCommand
    {
        get => GetValue(CompleteCommandProperty);
        set => SetValue(CompleteCommandProperty, value);
    }

    /// <summary>
    /// Optional work for the final submission (Arc: <c>onComplete</c>). While it runs the submit button shows a spinner and
    /// Back is disabled; when it finishes the success block appears. If it throws, the form stays on the last step and raises
    /// <see cref="CompleteFailed"/>. Null completes at once.
    /// </summary>
    public Func<CancellationToken, Task>? CompleteAsync { get; set; }

    #endregion

    #region Events

    /// <summary>Defines the <see cref="StepChanged"/> event.</summary>
    public static readonly RoutedEvent<MultiStepFormStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<MultiStepForm, MultiStepFormStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="StepValidating"/> event.</summary>
    public static readonly RoutedEvent<MultiStepFormStepValidatingEventArgs> StepValidatingEvent =
        RoutedEvent.Register<MultiStepForm, MultiStepFormStepValidatingEventArgs>(nameof(StepValidating), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Completed"/> event.</summary>
    public static readonly RoutedEvent<MultiStepFormCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<MultiStepForm, MultiStepFormCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="CompleteFailed"/> event.</summary>
    public static readonly RoutedEvent<MultiStepFormCompleteFailedEventArgs> CompleteFailedEvent =
        RoutedEvent.Register<MultiStepForm, MultiStepFormCompleteFailedEventArgs>(nameof(CompleteFailed), RoutingStrategies.Bubble);

    /// <summary>Raised after the shown step changed (Continue, Back, <see cref="CurrentIndex"/> or <see cref="Restart"/>).</summary>
    public event EventHandler<MultiStepFormStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <summary>Raised when a step is submitted, before its <see cref="MultiStepFormStep.ValidateAsync"/>; cancel it to stay.</summary>
    public event EventHandler<MultiStepFormStepValidatingEventArgs>? StepValidating
    {
        add => AddHandler(StepValidatingEvent, value);
        remove => RemoveHandler(StepValidatingEvent, value);
    }

    /// <summary>Raised once the last step was submitted and the success block is shown (Arc: <c>onComplete</c>).</summary>
    public event EventHandler<MultiStepFormCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <summary>Raised when <see cref="CompleteAsync"/> threw; the form stays on the last step.</summary>
    public event EventHandler<MultiStepFormCompleteFailedEventArgs>? CompleteFailed
    {
        add => AddHandler(CompleteFailedEvent, value);
        remove => RemoveHandler(CompleteFailedEvent, value);
    }

    #endregion

    private readonly List<MultiStepFormStep> _steps = new();
    private readonly Dictionary<MultiStepFormStep, MultiStepStepView> _views = new();
    private readonly List<MultiStepProgressItem> _items = new();
    private INotifyCollectionChanged? _observedSource;
    private bool _listening;
    private bool _isBusy;
    private int _stepCount;

    private Border? _card;
    private Control? _header;
    private Panel? _progressList;
    private ScrollViewer? _progressScroller;
    private TextBlock? _countPrefix;
    private MultiStepSwap? _countDigit;
    private TextBlock? _countSuffix;
    private MultiStepViewport? _viewport;
    private MultiStepCollapse? _actions;
    private MultiStepButton? _back;
    private MultiStepButton? _next;
    private AuthAnnouncer? _announcer;
    private MultiStepSuccessView? _success;

    private Control? _currentLayer;
    private int _shownIndex = -1;
    private bool _shownComplete;
    private int _batch;
    private bool _syncing;
    private CancellationTokenSource? _operation;

    static MultiStepForm()
    {
        FocusableProperty.OverrideDefaultValue<MultiStepForm>(false);
    }

    public MultiStepForm()
    {
        Steps = new AvaloniaList<MultiStepFormStep>();
        Steps.CollectionChanged += OnStepsChanged;
    }

    #region Public methods

    /// <summary>
    /// Submits the current step, as Continue does: runs the checks (data validation errors in the step's fields,
    /// <see cref="StepValidating"/>, <see cref="MultiStepFormStep.ValidateAsync"/>) and moves to the next step, or on the last
    /// step runs <see cref="CompleteAsync"/> and shows the success block. Returns true when the form moved on.
    /// </summary>
    public async Task<bool> NextAsync()
    {
        if (_steps.Count == 0 || IsComplete || IsBusy)
        {
            return false;
        }
        var index = EffectiveIndex;
        var step = _steps[index];
        if (FindInvalidField(step) is { } invalid)
        {
            invalid.Focus(NavigationMethod.Unspecified);
            return false;
        }
        var validating = new MultiStepFormStepValidatingEventArgs(StepValidatingEvent, step, index);
        RaiseEvent(validating);
        if (validating.Cancel)
        {
            return false;
        }
        var operation = BeginOperation();
        var token = operation.Token;
        try
        {
            if (step.ValidateAsync is { } validate)
            {
                bool valid;
                try
                {
                    var check = validate(token);
                    if (!check.IsCompleted)
                    {
                        SetBusy(true);
                    }
                    valid = await check;
                }
                catch (Exception)
                {
                    // A failing check keeps the person on the step, like a check that says no.
                    valid = false;
                }
                if (token.IsCancellationRequested || !valid || !IsCurrentOperation(operation)
                    || EffectiveIndex != index || index >= _steps.Count || !ReferenceEquals(_steps[index], step))
                {
                    return false;
                }
            }
            if (index < _steps.Count - 1)
            {
                EndOperation(operation);
                SetCurrentValue(CurrentIndexProperty, index + 1);
                return true;
            }
            if (CompleteAsync is { } complete)
            {
                try
                {
                    var work = complete(token);
                    if (!work.IsCompleted)
                    {
                        SetBusy(true);
                    }
                    await work;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return false;
                }
                catch (Exception exception)
                {
                    if (IsCurrentOperation(operation))
                    {
                        EndOperation(operation);
                        RaiseEvent(new MultiStepFormCompleteFailedEventArgs(CompleteFailedEvent, exception));
                    }
                    return false;
                }
                if (token.IsCancellationRequested || !IsCurrentOperation(operation))
                {
                    return false;
                }
            }
            EndOperation(operation);
            SetCurrentValue(IsCompleteProperty, true);
            var completed = new MultiStepFormCompletedEventArgs(CompletedEvent, _steps.ToArray());
            RaiseEvent(completed);
            if (CompleteCommand is { } command && command.CanExecute(completed))
            {
                command.Execute(completed);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (IsCurrentOperation(operation))
            {
                EndOperation(operation);
            }
        }
    }

    /// <summary>Goes back one step (as Back does); cancels a running check. Does nothing on the first step or after completion.</summary>
    public void Back()
    {
        if (_steps.Count == 0 || IsComplete)
        {
            return;
        }
        CancelOperation();
        var index = EffectiveIndex;
        if (index > 0)
        {
            SetCurrentValue(CurrentIndexProperty, index - 1);
        }
    }

    /// <summary>Shows the first step again with the success block cleared (Arc has no restart; the motion runs backward).</summary>
    public void Restart()
    {
        CancelOperation();
        _batch++;
        try
        {
            SetCurrentValue(IsCompleteProperty, false);
            SetCurrentValue(CurrentIndexProperty, 0);
        }
        finally
        {
            _batch--;
        }
        Sync(CanAnimate);
    }

    #endregion

    /// <summary>The step shown, clamped to the steps (0 with none).</summary>
    internal int EffectiveIndex => _steps.Count == 0 ? 0 : Math.Clamp(CurrentIndex, 0, _steps.Count - 1);

    /// <summary>The steps in use (tests read them).</summary>
    internal IReadOnlyList<MultiStepFormStep> EffectiveSteps => _steps;

    /// <summary>The view of a step, when it was shown once (tests read it).</summary>
    internal MultiStepStepView? ViewOf(MultiStepFormStep step) => _views.TryGetValue(step, out var view) ? view : null;

    /// <summary>The success block, once created (tests read it).</summary>
    internal MultiStepSuccessView? SuccessView => _success;

    /// <summary>The progress entries (tests read them).</summary>
    internal IReadOnlyList<MultiStepProgressItem> ProgressItems => _items;

    private bool CanAnimate => IsLoaded && IsEffectivelyVisible && ArcMotion.IsFull(this);

    #region Template and layout

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        foreach (var view in _views.Values)
        {
            ReleaseView(view);
        }
        _viewport?.Children.Clear();
        _progressList?.Children.Clear();
        _views.Clear();
        _items.Clear();
        _success = null;
        _currentLayer = null;
        _shownIndex = -1;
        _shownComplete = false;

        _card = e.NameScope.Find<Border>(CardPart);
        _header = e.NameScope.Find<Control>(HeaderPart);
        _progressList = e.NameScope.Find<Panel>(ProgressListPart);
        _progressScroller = e.NameScope.Find<ScrollViewer>(ProgressScrollerPart);
        _countPrefix = e.NameScope.Find<TextBlock>(CountPrefixPart);
        _countDigit = e.NameScope.Find<MultiStepSwap>(CountDigitPart);
        _countSuffix = e.NameScope.Find<TextBlock>(CountSuffixPart);
        _viewport = e.NameScope.Find<MultiStepViewport>(ViewportPart);
        _actions = e.NameScope.Find<MultiStepCollapse>(ActionsPart);
        _back = e.NameScope.Find<MultiStepButton>(BackPart);
        _next = e.NameScope.Find<MultiStepButton>(NextPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_card is not null)
        {
            AutomationProperties.SetLandmarkType(_card, AutomationLandmarkType.Form);
        }
        if (_header is not null)
        {
            AutomationProperties.SetLandmarkType(_header, AutomationLandmarkType.Navigation);
        }
        RebuildItems();
        Sync(false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Arc: width 540px, max-width 100%. The card takes the smaller of the two even where the width is unbounded.
        var cardWidth = _card is { } card && !double.IsInfinity(card.MaxWidth) && !double.IsNaN(card.MaxWidth) ? card.MaxWidth : 540;
        var width = Math.Min(availableSize.Width, cardWidth);
        PseudoClasses.Set(NarrowPseudoClass, width <= NarrowWidth);
        var desired = base.MeasureOverride(availableSize.WithWidth(width));
        return new Size(Math.Max(desired.Width, Math.Min(width, availableSize.Width)), desired.Height);
    }

    #endregion

    #region Steps

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepsSourceProperty)
        {
            if (_observedSource is not null && _listening)
            {
                _observedSource.CollectionChanged -= OnStepsChanged;
            }
            _observedSource = StepsSource as INotifyCollectionChanged;
            if (_observedSource is not null && _listening)
            {
                _observedSource.CollectionChanged += OnStepsChanged;
            }
            OnStepsChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        else if (change.Property == CurrentIndexProperty || change.Property == IsCompleteProperty)
        {
            // A pending check or submission belongs to the step that was shown: any step change (or a restart) cancels it.
            if (change.Property == CurrentIndexProperty || !IsComplete)
            {
                CancelOperation();
            }
            if (_batch == 0 && !_syncing)
            {
                Sync(CanAnimate);
            }
        }
        else if (change.Property == NextLabelProperty || change.Property == CompleteLabelProperty
                 || change.Property == BackLabelProperty || change.Property == CountFormatProperty
                 || change.Property == ProgressNameFormatProperty || change.Property == CurrentStepTextProperty
                 || change.Property == CompletedStepTextProperty || change.Property == SuccessTitleProperty
                 || change.Property == SuccessDescriptionProperty)
        {
            UpdateChrome(EffectiveIndex, IsComplete && _steps.Count > 0, 0);
        }
        else if (change.Property == FormNameProperty && _card is not null)
        {
            AutomationProperties.SetName(_card, FormName);
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (StepsSource is not null && ReferenceEquals(sender, Steps))
        {
            return;
        }
        ListenToSteps(false);
        _steps.Clear();
        IEnumerable source = StepsSource ?? (IEnumerable)Steps;
        foreach (var item in source)
        {
            if (item is MultiStepFormStep step && !_steps.Contains(step))
            {
                _steps.Add(step);
            }
        }
        ListenToSteps(_listening);
        StepCount = _steps.Count;
        // Views of steps that are gone leave the tree.
        foreach (var gone in _views.Keys.Where(s => !_steps.Contains(s)).ToArray())
        {
            var view = _views[gone];
            ReleaseView(view);
            _viewport?.Children.Remove(view);
            _views.Remove(gone);
            if (ReferenceEquals(_currentLayer, view))
            {
                _currentLayer = null;
                _shownIndex = -1;
            }
        }
        RebuildItems();
        Sync(false);
    }

    /// <summary>
    /// Follows the steps' own changes while the form is in a window only: steps that outlive the form (a view model's
    /// collection) never keep it alive.
    /// </summary>
    private void ListenToSteps(bool on)
    {
        foreach (var step in _steps)
        {
            step.PropertyChanged -= OnStepPropertyChanged;
            if (on)
            {
                step.PropertyChanged += OnStepPropertyChanged;
            }
        }
    }

    /// <summary>Lets go of a step's fields, so the same control can be shown by another view or form.</summary>
    private static void ReleaseView(MultiStepStepView view)
    {
        view.ContentTemplate = null;
        view.Content = null;
    }

    private void OnStepPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not MultiStepFormStep step)
        {
            return;
        }
        if (_views.TryGetValue(step, out var view))
        {
            view.Sync();
        }
        var index = _steps.IndexOf(step);
        if (index >= 0 && index < _items.Count)
        {
            _items[index].Title = step.Title;
        }
    }

    private void RebuildItems()
    {
        if (_progressList is null)
        {
            return;
        }
        _progressList.Children.Clear();
        _items.Clear();
        for (var i = 0; i < _steps.Count; i++)
        {
            var item = new MultiStepProgressItem
            {
                Number = i + 1,
                Title = _steps[i].Title,
                CurrentText = CurrentStepText,
                DoneText = CompletedStepText
            };
            _items.Add(item);
            _progressList.Children.Add(item);
        }
    }

    private MultiStepStepView ViewFor(MultiStepFormStep step)
    {
        if (!_views.TryGetValue(step, out var view))
        {
            view = new MultiStepStepView(step) { IsVisible = false };
            _views[step] = view;
            _viewport?.Children.Add(view);
        }
        return view;
    }

    private MultiStepSuccessView SuccessFor()
    {
        if (_success is null)
        {
            _success = new MultiStepSuccessView { IsVisible = false };
            _viewport?.Children.Add(_success);
        }
        _success.Title = SuccessTitle;
        _success.Description = SuccessDescription;
        return _success;
    }

    #endregion

    #region State to screen

    private void Sync(bool animate)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        try
        {
            // Arc clamps initialStep into range. XAML sets the index before it adds the steps one by one, so the index waits
            // until the form is initialized (and for steps to exist) before it is clamped.
            if (IsInitialized && _steps.Count > 0 && (CurrentIndex < 0 || CurrentIndex >= _steps.Count))
            {
                SetCurrentValue(CurrentIndexProperty, EffectiveIndex);
            }
            var count = _steps.Count;
            PseudoClasses.Set(EmptyPseudoClass, count == 0);
            if (count == 0)
            {
                _currentLayer?.SetValue(IsVisibleProperty, false);
                _currentLayer = null;
                if (_viewport is not null)
                {
                    _viewport.Current = null;
                }
                _shownIndex = -1;
                _shownComplete = false;
                PseudoClasses.Set(FirstPseudoClass, false);
                PseudoClasses.Set(LastPseudoClass, false);
                PseudoClasses.Set(CompletePseudoClass, false);
                return;
            }
            var index = EffectiveIndex;
            var complete = IsComplete;
            var first = _shownIndex < 0 || _currentLayer is null;
            int direction;
            if (complete && !_shownComplete)
            {
                direction = 1;
            }
            else if (!complete && _shownComplete)
            {
                direction = -1;
            }
            else
            {
                direction = index >= _shownIndex ? 1 : -1;
            }
            animate = animate && !first;
            var focusWithin = !first && IsKeyboardFocusWithin;
            var oldIndex = _shownIndex;
            var wasComplete = _shownComplete;
            if (_viewport is not null)
            {
                // The layer first: the focus moves to the new heading before Back may turn disabled.
                ShowLayer(complete ? SuccessFor() : ViewFor(_steps[index]), direction, animate, focusWithin);
            }
            UpdateChrome(index, complete, animate ? direction : 0);
            if (complete)
            {
                _actions?.Collapse(animate);
            }
            else if (_actions is { IsExpanded: false })
            {
                _actions.Expand(animate);
            }
            _shownIndex = index;
            _shownComplete = complete;
            if (first)
            {
                return;
            }
            if (complete && !wasComplete)
            {
                // The success block speaks for itself (Arc: role=status); the count stays as it was.
                _announcer?.Announce(Join(SuccessTitle, SuccessDescription));
            }
            else if (!complete && (oldIndex != index || wasComplete))
            {
                _announcer?.Announce(Format(AnnouncementFormat, index + 1, count));
            }
            if (oldIndex != index)
            {
                RaiseEvent(new MultiStepFormStepChangedEventArgs(StepChangedEvent, oldIndex, index));
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Progress entries, count, button labels and states, pseudo-classes and names.</summary>
    private void UpdateChrome(int index, bool complete, int direction)
    {
        var count = _steps.Count;
        if (count == 0)
        {
            return;
        }
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            item.CurrentText = CurrentStepText;
            item.DoneText = CompletedStepText;
            item.IsDone = complete || i < index;
            item.IsCurrent = i == index;
            item.UpdateStatus();
        }
        if (_countDigit is not null)
        {
            if (direction != 0)
            {
                _countDigit.Direction = direction;
            }
            _countDigit.Text = (index + 1).ToString(CultureInfo.CurrentCulture);
        }
        var format = CountFormat ?? string.Empty;
        var split = format.IndexOf("{0}", StringComparison.Ordinal);
        var prefix = split < 0 ? format : format[..split];
        var suffix = split < 0 ? string.Empty : format[(split + 3)..];
        if (_countPrefix is not null)
        {
            _countPrefix.Text = Format(prefix, index + 1, count);
        }
        if (_countSuffix is not null)
        {
            _countSuffix.Text = Format(suffix, index + 1, count);
        }
        if (_countDigit is not null)
        {
            _countDigit.IsVisible = split >= 0;
        }
        var progressName = Format(ProgressNameFormat, index + 1, count);
        if (_header is not null)
        {
            AutomationProperties.SetName(_header, progressName);
        }
        if (_progressList is not null)
        {
            AutomationProperties.SetName(_progressList, progressName);
        }
        if (_card is not null)
        {
            AutomationProperties.SetName(_card, FormName);
        }
        if (_next is not null)
        {
            _next.Label = index >= count - 1 ? CompleteLabel : NextLabel;
            _next.IsLoading = IsBusy;
        }
        if (_back is not null)
        {
            _back.Label = BackLabel;
            _back.IsEnabled = index > 0 && !complete && !IsBusy;
        }
        if (_success is not null)
        {
            _success.Title = SuccessTitle;
            _success.Description = SuccessDescription;
        }
        PseudoClasses.Set(FirstPseudoClass, index == 0);
        PseudoClasses.Set(LastPseudoClass, index == count - 1);
        PseudoClasses.Set(CompletePseudoClass, complete);
        if (index < _items.Count)
        {
            // Long lists scroll; keep the current entry in view.
            _items[index].BringIntoView();
        }
    }

    private void ShowLayer(Control layer, int direction, bool animate, bool focusWithin)
    {
        var previous = _currentLayer;
        _currentLayer = layer;
        if (_viewport is not null)
        {
            _viewport.Current = layer;
        }
        var success = layer as MultiStepSuccessView;
        if (!ReferenceEquals(previous, layer))
        {
            // Outgoing: slides against the direction of travel (the success block only fades).
            if (previous is not null)
            {
                var motion = MotionOf(previous);
                if (animate)
                {
                    motion?.Exit(previous is MultiStepSuccessView ? 0 : -SlideDistance * direction);
                }
                else
                {
                    motion?.Hide();
                }
            }
            var incoming = MotionOf(layer);
            if (animate)
            {
                if (success is not null)
                {
                    incoming?.Enter(0, SuccessRise);
                }
                else
                {
                    incoming?.Enter(SlideDistance * direction, 0);
                }
            }
            else
            {
                incoming?.Show();
            }
            success?.PlayEntrance(animate && previous is not null);
        }
        else if (!layer.IsVisible)
        {
            MotionOf(layer)?.Show();
        }
        if (focusWithin)
        {
            // Same frame as the change (Arc): the new heading takes the focus before Back may disable.
            if (layer is TemplatedControl templated)
            {
                templated.ApplyTemplate();
            }
            var target = success is not null ? success.TitleBlock : (layer as MultiStepStepView)?.Legend;
            target?.Focus(NavigationMethod.Unspecified);
        }
    }

    private static MultiStepLayerMotion? MotionOf(Control layer) => layer switch
    {
        MultiStepStepView view => view.LayerMotion,
        MultiStepSuccessView success => success.LayerMotion,
        _ => null
    };

    private static string Format(string? format, int current, int count)
    {
        if (string.IsNullOrEmpty(format))
        {
            return string.Empty;
        }
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, current, count);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static string Join(string? title, string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return title ?? string.Empty;
        }
        return string.IsNullOrEmpty(title) ? description : title + ". " + description;
    }

    #endregion

    #region Input

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Back();
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = NextAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Arc's native form submit: Enter in a single-line text field of the current step. Fields that use Enter
        // themselves (an open combobox list, a multi-line box) handle it first or are skipped.
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source)
        {
            return;
        }
        var textBox = source as TextBox ?? source.FindAncestorOfType<TextBox>();
        if (textBox is null || textBox.AcceptsReturn || _currentLayer is not MultiStepStepView view || !view.IsVisualAncestorOf(textBox))
        {
            return;
        }
        e.Handled = true;
        _ = NextAsync();
    }

    /// <summary>The first visible field of the step that reports a data validation error (Arc: native constraints).</summary>
    private Control? FindInvalidField(MultiStepFormStep step)
    {
        if (!_views.TryGetValue(step, out var view))
        {
            return null;
        }
        foreach (var visual in view.GetVisualDescendants())
        {
            if (visual is Control control && control.IsEffectivelyVisible && DataValidationErrors.GetHasErrors(control))
            {
                return control;
            }
        }
        return null;
    }

    #endregion

    #region Operations

    private CancellationTokenSource BeginOperation()
    {
        CancelOperation();
        _operation = new CancellationTokenSource();
        return _operation;
    }

    private bool IsCurrentOperation(CancellationTokenSource operation) => ReferenceEquals(_operation, operation);

    private void EndOperation(CancellationTokenSource operation)
    {
        if (ReferenceEquals(_operation, operation))
        {
            _operation = null;
            operation.Dispose();
            SetBusy(false);
        }
    }

    private void CancelOperation()
    {
        if (_operation is { } operation)
        {
            _operation = null;
            operation.Cancel();
            operation.Dispose();
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        if (IsBusy == busy)
        {
            return;
        }
        IsBusy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_steps.Count > 0)
        {
            var complete = IsComplete;
            if (_next is not null)
            {
                _next.IsLoading = busy;
            }
            if (_back is not null)
            {
                _back.IsEnabled = EffectiveIndex > 0 && !complete && !busy;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _listening = true;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnStepsChanged;
        }
        // Catch up with whatever changed while the form was away.
        OnStepsChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelOperation();
        _listening = false;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnStepsChanged;
        }
        ListenToSteps(false);
    }

    #endregion
}
