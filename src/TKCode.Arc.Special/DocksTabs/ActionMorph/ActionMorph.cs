using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A corner button for quick entries: it expands into a short list of actions, a chosen action reshapes the same surface
/// into a small form, and a successful submit shrinks it back to the button, which then shows a check mark. Width,
/// height and corner radius follow a spring from face to face; a deeper face enters from the right, Escape climbs one
/// level, and every action holds on to its draft for as long as the control exists.
/// </summary>
/// <remarks>
/// The morph does not place itself: put it at a corner of a panel (for example <c>HorizontalAlignment="Right"</c>,
/// <c>VerticalAlignment="Bottom"</c>, a 16 px margin), last among its siblings so the surface draws over them. It takes
/// only the button's 56 × 56 in the layout; the surface opens upward and away from the <see cref="Corner"/> side, over
/// what is around it. <see cref="SubmitHandler"/> receives each submission; while its task runs the form shows a
/// spinner, and when it throws the form stays up, keeps the draft and shows <see cref="ErrorMessage"/>.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(SurfacePart, typeof(ActionMorphSurface))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(OpenPseudoClass, SuccessPseudoClass, HoverPseudoClass, PressedPseudoClass, StartPseudoClass)]
public class ActionMorph : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string SurfacePart = "PART_Surface";
    internal const string StatusPart = "PART_Status";
    internal const string OpenPseudoClass = ":open";
    internal const string SuccessPseudoClass = ":success";
    internal const string HoverPseudoClass = ":hover";
    internal const string PressedPseudoClass = ":pressed";
    internal const string StartPseudoClass = ":start";

    /// <summary>The button's accessible name and the menu's title.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(Label), "Create");

    /// <summary>The bottom corner that holds the button; the menu and forms open upward and toward the other side.</summary>
    public static readonly StyledProperty<ActionMorphCorner> CornerProperty =
        AvaloniaProperty.Register<ActionMorph, ActionMorphCorner>(nameof(Corner));

    /// <summary>How long the check stays on the button after a successful submit before the plus returns.</summary>
    public static readonly StyledProperty<TimeSpan> ResetAfterProperty =
        AvaloniaProperty.Register<ActionMorph, TimeSpan>(nameof(ResetAfter), TimeSpan.FromMilliseconds(1600));

    /// <summary>
    /// Saves a submission (for example to your server). While its task runs the form shows a spinner; when it throws, the
    /// form stays up, keeps the draft and shows <see cref="ErrorMessage"/>. Going back or closing cancels its token. Null
    /// accepts every submission at once.
    /// </summary>
    public static readonly StyledProperty<Func<ActionMorphSubmission, CancellationToken, Task>?> SubmitHandlerProperty =
        AvaloniaProperty.Register<ActionMorph, Func<ActionMorphSubmission, CancellationToken, Task>?>(nameof(SubmitHandler));

    /// <summary>Whether the menu or a form is open (two-way). Setting it opens the menu or folds back into the button.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ActionMorph, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Accessible name of the close buttons.</summary>
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(CloseLabel), "Close");

    /// <summary>Accessible name of a form's back button.</summary>
    public static readonly StyledProperty<string> BackLabelProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(BackLabel), "Back");

    /// <summary>The text beside the spinner while a submission is saved.</summary>
    public static readonly StyledProperty<string> BusyLabelProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(BusyLabel), "Saving");

    /// <summary>Shown under the field when <see cref="SubmitHandler"/> throws.</summary>
    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(ErrorMessage), "Not saved. Try again.");

    /// <summary>Shown under an empty field on submit, for actions without their own <see cref="ActionMorphAction.EmptyHint"/>.</summary>
    public static readonly StyledProperty<string> EmptyHintProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(EmptyHint), "Write something first");

    /// <summary>What the surface shows.</summary>
    public static readonly DirectProperty<ActionMorph, ActionMorphView> ViewProperty =
        AvaloniaProperty.RegisterDirect<ActionMorph, ActionMorphView>(nameof(View), o => o.View);

    /// <summary>The action whose form is open, or null.</summary>
    public static readonly DirectProperty<ActionMorph, ActionMorphAction?> CurrentActionProperty =
        AvaloniaProperty.RegisterDirect<ActionMorph, ActionMorphAction?>(nameof(CurrentAction), o => o.CurrentAction);

    /// <summary>Whether <see cref="SubmitHandler"/> is running.</summary>
    public static readonly DirectProperty<ActionMorph, bool> IsSubmittingProperty =
        AvaloniaProperty.RegisterDirect<ActionMorph, bool>(nameof(IsSubmitting), o => o.IsSubmitting);

    /// <summary>Raised when the menu opens from the button. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ExpandedEvent =
        RoutedEvent.Register<ActionMorph, RoutedEventArgs>(nameof(Expanded), RoutingStrategies.Bubble);

    /// <summary>Raised when the surface folds back into the button. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CollapsedEvent =
        RoutedEvent.Register<ActionMorph, RoutedEventArgs>(nameof(Collapsed), RoutingStrategies.Bubble);

    /// <summary>Raised after a submission was accepted (after <see cref="SubmitHandler"/> finished). Bubbles.</summary>
    public static readonly RoutedEvent<ActionMorphSubmittedEventArgs> SubmittedEvent =
        RoutedEvent.Register<ActionMorph, ActionMorphSubmittedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    private readonly ActionMorphTrigger _trigger;
    private readonly Dictionary<ActionMorphAction, ActionMorphComposer> _composers = new();
    private Panel? _root;
    private ActionMorphSurface? _surface;
    private TextBlock? _status;
    private ActionMorphMenu? _menu;
    private LazyFocusRing? _focusRing;
    private TopLevel? _pressRoot;
    private DispatcherTimer? _resetTimer;
    private CancellationTokenSource? _submitCts;
    private ActionMorphView _view;
    private ActionMorphAction? _currentAction;
    private bool _isSubmitting;
    private bool _success;
    private string? _successLabel;
    private bool _syncingExpanded;
    private int _focusRequest;

    static ActionMorph()
    {
        FocusableProperty.OverrideDefaultValue<ActionMorph>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ActionMorph>(false);
        SpecialFocusModality.Watch();
    }

    public ActionMorph()
    {
        Actions.CollectionChanged += (_, _) => OnActionsChanged();
        _trigger = new ActionMorphTrigger(this);
        _trigger.Click += OnTriggerClick;
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        UpdateTriggerName();
    }

    /// <summary>The actions, one menu row each, in order.</summary>
    [Content]
    public AvaloniaList<ActionMorphAction> Actions { get; } = new();

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="CornerProperty"/>
    public ActionMorphCorner Corner { get => GetValue(CornerProperty); set => SetValue(CornerProperty, value); }

    /// <inheritdoc cref="ResetAfterProperty"/>
    public TimeSpan ResetAfter { get => GetValue(ResetAfterProperty); set => SetValue(ResetAfterProperty, value); }

    /// <inheritdoc cref="SubmitHandlerProperty"/>
    public Func<ActionMorphSubmission, CancellationToken, Task>? SubmitHandler
    {
        get => GetValue(SubmitHandlerProperty);
        set => SetValue(SubmitHandlerProperty, value);
    }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="CloseLabelProperty"/>
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    /// <inheritdoc cref="BackLabelProperty"/>
    public string BackLabel { get => GetValue(BackLabelProperty); set => SetValue(BackLabelProperty, value); }

    /// <inheritdoc cref="BusyLabelProperty"/>
    public string BusyLabel { get => GetValue(BusyLabelProperty); set => SetValue(BusyLabelProperty, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <inheritdoc cref="EmptyHintProperty"/>
    public string EmptyHint { get => GetValue(EmptyHintProperty); set => SetValue(EmptyHintProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public ActionMorphView View
    {
        get => _view;
        private set => SetAndRaise(ViewProperty, ref _view, value);
    }

    /// <inheritdoc cref="CurrentActionProperty"/>
    public ActionMorphAction? CurrentAction
    {
        get => _currentAction;
        private set => SetAndRaise(CurrentActionProperty, ref _currentAction, value);
    }

    /// <inheritdoc cref="IsSubmittingProperty"/>
    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set => SetAndRaise(IsSubmittingProperty, ref _isSubmitting, value);
    }

    /// <inheritdoc cref="ExpandedEvent"/>
    public event EventHandler<RoutedEventArgs>? Expanded
    {
        add => AddHandler(ExpandedEvent, value);
        remove => RemoveHandler(ExpandedEvent, value);
    }

    /// <inheritdoc cref="CollapsedEvent"/>
    public event EventHandler<RoutedEventArgs>? Collapsed
    {
        add => AddHandler(CollapsedEvent, value);
        remove => RemoveHandler(CollapsedEvent, value);
    }

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<ActionMorphSubmittedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    internal ActionMorphTrigger Trigger => _trigger;

    internal ActionMorphMenu? Menu => _menu;

    internal ActionMorphSurface? Surface => _surface;

    internal TextBlock? Status => _status;

    internal bool IsSuccess => _success;

    internal ActionMorphComposer? ComposerOf(ActionMorphAction action) => _composers.GetValueOrDefault(action);

    /// <summary>Expands the button into the menu and focuses the menu's first action.</summary>
    public void Expand()
    {
        if (_view == ActionMorphView.Button)
        {
            GoTo(ActionMorphView.Menu, null, ActionMorphFaceEntry.Scale, ActionMorphSurface.GrowSpring);
        }
    }

    /// <summary>Folds the menu or form back into the button (the draft stays); focus inside returns to the button.</summary>
    public void Collapse()
    {
        if (_view != ActionMorphView.Button)
        {
            CancelSubmit();
            GoTo(ActionMorphView.Button, null, ActionMorphFaceEntry.Scale, ActionMorphSurface.FoldSpring);
        }
    }

    /// <summary>Opens the form of <paramref name="action"/> (one of <see cref="Actions"/>); focus moves to its field.</summary>
    public void OpenComposer(ActionMorphAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!Actions.Contains(action) || (_view == ActionMorphView.Composer && ReferenceEquals(action, _currentAction)))
        {
            return;
        }
        CancelSubmit();
        var entry = _view == ActionMorphView.Button ? ActionMorphFaceEntry.Scale : ActionMorphFaceEntry.Forward;
        GoTo(ActionMorphView.Composer, action, entry, ActionMorphSurface.GrowSpring);
    }

    /// <summary>Goes back one level: from a form to the menu (focus on its action), from the menu to the button.</summary>
    public void Back()
    {
        if (_view == ActionMorphView.Composer)
        {
            CancelSubmit();
            GoTo(ActionMorphView.Menu, null, ActionMorphFaceEntry.Back, ActionMorphSurface.FoldSpring);
        }
        else if (_view == ActionMorphView.Menu)
        {
            Collapse();
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Panel>(RootPart);
        _surface = e.NameScope.Find<ActionMorphSurface>(SurfacePart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        _focusRing = new LazyFocusRing(_trigger, ActionMorphTokens.Key(ActionMorphTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(_root);
        if (_surface is not null)
        {
            _surface.IsStart = Corner == ActionMorphCorner.Start;
            _surface.Show(FaceFor(_view, _currentAction), ActionMorphFaceEntry.Instant, ActionMorphSurface.GrowSpring,
                _view == ActionMorphView.Button);
        }
        PseudoClasses.Set(StartPseudoClass, Corner == ActionMorphCorner.Start);
        PseudoClasses.Set(OpenPseudoClass, _view != ActionMorphView.Button);
        PseudoClasses.Set(SuccessPseudoClass, _success);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            if (_syncingExpanded)
            {
                return;
            }
            if (change.GetNewValue<bool>())
            {
                Expand();
            }
            else
            {
                Collapse();
            }
        }
        else if (change.Property == LabelProperty)
        {
            UpdateTriggerName();
            if (_menu is not null)
            {
                _menu.Title = Label;
                AutomationProperties.SetName(_menu, Label);
            }
        }
        else if (change.Property == CornerProperty)
        {
            var start = Corner == ActionMorphCorner.Start;
            PseudoClasses.Set(StartPseudoClass, start);
            if (_surface is not null)
            {
                _surface.IsStart = start;
            }
        }
        else if (change.Property == CloseLabelProperty || change.Property == BackLabelProperty || change.Property == BusyLabelProperty)
        {
            if (_menu is not null)
            {
                _menu.CloseLabel = CloseLabel;
            }
            foreach (var composer in _composers.Values)
            {
                ApplyWords(composer);
            }
        }
        else if (change.Property == ResetAfterProperty && _resetTimer is { IsEnabled: true } timer)
        {
            timer.Stop();
            timer.Interval = Positive(ResetAfter);
            timer.Start();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_view != ActionMorphView.Button)
        {
            WatchOutsidePresses(true);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchOutsidePresses(false);
        CancelSubmit();
        if (_success)
        {
            EndSuccess(animate: false);
        }
        _surface?.FinishExits();
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Expand();
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (_view == ActionMorphView.Composer)
            {
                e.Handled = true;
                Back();
            }
            else if (_view == ActionMorphView.Menu)
            {
                e.Handled = true;
                Collapse();
            }
            return;
        }
        if (_view == ActionMorphView.Menu
            && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0
            && ShortcutOf(e) is { } symbol
            && Actions.FirstOrDefault(a => a.Matches(symbol)) is { } action)
        {
            e.Handled = true;
            OpenComposer(action);
        }
    }

    private static string? ShortcutOf(KeyEventArgs e)
    {
        if (e.KeySymbol is { Length: 1 } symbol && char.IsLetterOrDigit(symbol[0]))
        {
            return symbol;
        }
        return e.Key is >= Key.A and <= Key.Z ? ((char)('a' + (e.Key - Key.A))).ToString() : null;
    }

    private void GoTo(ActionMorphView view, ActionMorphAction? action, ActionMorphFaceEntry entry, SpringSpec spring)
    {
        var wasExpanded = _view != ActionMorphView.Button;
        var fromAction = _currentAction;
        if (view != ActionMorphView.Button && _success)
        {
            EndSuccess(animate: false);
        }
        var keyboard = SpecialFocusModality.LastWasPointer(this) != true;
        var focusInside = FocusIsInside();
        var face = FaceFor(view, action);
        if (face is ActionMorphComposer { NeedsReset: true } fresh)
        {
            fresh.ResetDraft();
        }
        FitToViewport(face);
        View = view;
        CurrentAction = view == ActionMorphView.Composer ? action : null;
        var expanded = view != ActionMorphView.Button;
        PseudoClasses.Set(OpenPseudoClass, expanded);
        _surface?.Show(face, entry, spring, view == ActionMorphView.Button);
        if (view == ActionMorphView.Button)
        {
            // Give focus back before the old face leaves, or it would fall to the window without a ring.
            _focusRequest++;
            if (focusInside)
            {
                _trigger.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
            }
        }
        else
        {
            FocusSoon(view == ActionMorphView.Menu ? (Control?)_menu?.RowOf(fromAction) : null);
        }
        WatchOutsidePresses(expanded);
        if (expanded != wasExpanded)
        {
            _syncingExpanded = true;
            try
            {
                SetCurrentValue(IsExpandedProperty, expanded);
            }
            finally
            {
                _syncingExpanded = false;
            }
            RaiseEvent(new RoutedEventArgs(expanded ? ExpandedEvent : CollapsedEvent));
        }
    }

    private Control FaceFor(ActionMorphView view, ActionMorphAction? action) => view switch
    {
        ActionMorphView.Menu => EnsureMenu(),
        ActionMorphView.Composer when action is not null => ComposerFor(action),
        _ => _trigger
    };

    private ActionMorphMenu EnsureMenu()
    {
        if (_menu is null)
        {
            _menu = new ActionMorphMenu(this) { Title = Label, CloseLabel = CloseLabel };
            AutomationProperties.SetName(_menu, Label);
            _menu.SetActions(Actions);
        }
        return _menu;
    }

    private ActionMorphComposer ComposerFor(ActionMorphAction action)
    {
        if (!_composers.TryGetValue(action, out var composer))
        {
            composer = new ActionMorphComposer(this, action);
            ApplyWords(composer);
            _composers.Add(action, composer);
        }
        return composer;
    }

    private void ApplyWords(ActionMorphComposer composer)
    {
        composer.BackLabel = BackLabel;
        composer.CloseLabel = CloseLabel;
        composer.BusyLabel = BusyLabel;
    }

    private void OnActionsChanged()
    {
        _menu?.SetActions(Actions);
        foreach (var stale in _composers.Keys.Where(a => !Actions.Contains(a)).ToList())
        {
            var composer = _composers[stale];
            _composers.Remove(stale);
            _surface?.Forget(composer);
        }
        if (_view == ActionMorphView.Composer && _currentAction is { } current && !Actions.Contains(current))
        {
            Back();
        }
    }

    /// <summary>Caps the menu and forms at the viewport width less the margins, so they fit narrow phones.</summary>
    private void FitToViewport(Control face)
    {
        if (face is ActionMorphTrigger)
        {
            return;
        }
        var margin = this.TryFindResource(ActionMorphTokens.Key(ActionMorphTokenKind.ViewportMargin), out var value) && value is double m
            ? m
            : 16;
        face.MaxWidth = ArcViewport.GetSize(this) is { Width: > 0 } viewport
            ? Math.Max(0, viewport.Width - 2 * margin)
            : double.PositiveInfinity;
    }

    private bool FocusIsInside()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }
        return ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused)
               || (focused is ILogical logical && this.IsLogicalAncestorOf(logical));
    }

    /// <summary>Moves focus into the new face once it is laid out: the given row, else the first row or the field.</summary>
    private void FocusSoon(Control? preferred)
    {
        var request = ++_focusRequest;
        var method = SpecialFocusModality.For(this);
        Dispatcher.UIThread.Post(() =>
        {
            if (request != _focusRequest)
            {
                return;
            }
            switch (_view)
            {
                case ActionMorphView.Menu when _menu is not null:
                    var row = preferred as ActionMorphMenuItem ?? (_menu.Rows.Count > 0 ? _menu.Rows[0] : null);
                    if (row is not null)
                    {
                        row.Focus(method);
                        _menu.SetActive(row, animate: false);
                    }
                    else
                    {
                        _menu.CloseButton?.Focus(method);
                    }
                    break;
                case ActionMorphView.Composer when _currentAction is { } action && ComposerOf(action) is { } composer:
                    composer.FocusField(method);
                    break;
            }
        }, DispatcherPriority.Loaded);
    }

    internal async void SubmitFrom(ActionMorphComposer composer)
    {
        if (_isSubmitting || _view != ActionMorphView.Composer || !ReferenceEquals(composer.Action, _currentAction))
        {
            return;
        }
        var text = composer.Text.Trim();
        if (text.Length == 0)
        {
            composer.ShowError(composer.Action.EmptyHint ?? EmptyHint);
            return;
        }
        var submission = new ActionMorphSubmission(composer.Action, text, composer.Action.Choices.Count > 0 ? composer.Choice : null);
        if (SubmitHandler is { } handler)
        {
            var cts = new CancellationTokenSource();
            _submitCts = cts;
            SetSubmitting(composer, true);
            try
            {
                await handler(submission, cts.Token);
            }
            catch (OperationCanceledException)
            {
                FinishSubmit(composer, cts);
                return;
            }
            catch (Exception)
            {
                if (FinishSubmit(composer, cts))
                {
                    composer.ShowError(ErrorMessage);
                }
                return;
            }
            if (!FinishSubmit(composer, cts))
            {
                return;
            }
        }
        Succeed(composer, submission);
    }

    /// <summary>Ends a run of the handler; false when it was cancelled (the person went back or closed the form).</summary>
    private bool FinishSubmit(ActionMorphComposer composer, CancellationTokenSource cts)
    {
        var current = ReferenceEquals(_submitCts, cts) && !cts.IsCancellationRequested;
        if (ReferenceEquals(_submitCts, cts))
        {
            _submitCts = null;
            SetSubmitting(composer, false);
        }
        cts.Dispose();
        return current;
    }

    private void SetSubmitting(ActionMorphComposer composer, bool busy)
    {
        IsSubmitting = busy;
        composer.IsBusy = busy;
        if (composer.Submit is { } submit)
        {
            submit.IsBusy = busy;
        }
    }

    private void CancelSubmit()
    {
        if (_submitCts is not { } cts)
        {
            return;
        }
        _submitCts = null;
        cts.Cancel();
        if (_currentAction is { } action && ComposerOf(action) is { } composer)
        {
            SetSubmitting(composer, false);
        }
        IsSubmitting = false;
    }

    private void Succeed(ActionMorphComposer composer, ActionMorphSubmission submission)
    {
        composer.NeedsReset = true;
        RaiseEvent(new ActionMorphSubmittedEventArgs(SubmittedEvent, submission));
        if (_view != ActionMorphView.Composer || !ReferenceEquals(composer.Action, _currentAction))
        {
            return;
        }
        GoTo(ActionMorphView.Button, null, ActionMorphFaceEntry.Scale, ActionMorphSurface.FoldSpring);
        _success = true;
        _successLabel = submission.Action.EffectiveSuccessLabel;
        PseudoClasses.Set(SuccessPseudoClass, true);
        _trigger.SetSuccess(true, animate: true);
        UpdateTriggerName();
        Announce(_successLabel);
        _resetTimer ??= CreateResetTimer();
        _resetTimer.Stop();
        _resetTimer.Interval = Positive(ResetAfter);
        _resetTimer.Start();
    }

    private DispatcherTimer CreateResetTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            EndSuccess(animate: true);
        };
        return timer;
    }

    /// <summary>Returns the button from the check to the plus (the reset after a success).</summary>
    internal void EndSuccess(bool animate)
    {
        _resetTimer?.Stop();
        if (!_success)
        {
            return;
        }
        _success = false;
        PseudoClasses.Set(SuccessPseudoClass, false);
        _trigger.SetSuccess(false, animate);
        UpdateTriggerName();
    }

    private static TimeSpan Positive(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.FromMilliseconds(1);

    private void UpdateTriggerName() =>
        AutomationProperties.SetName(_trigger, _success && _successLabel is { Length: > 0 } words ? $"{Label}. {words}" : Label);

    private void Announce(string message)
    {
        if (_status is null)
        {
            return;
        }
        // The same words twice in a row are still announced: clear first.
        _status.Text = string.Empty;
        _status.Text = message;
    }

    internal void SetTriggerHover(bool hover) => PseudoClasses.Set(HoverPseudoClass, hover);

    internal void SetTriggerPressed(bool pressed) => PseudoClasses.Set(PressedPseudoClass, pressed);

    internal void UpdateFocusRing() => _focusRing?.Update();

    private void WatchOutsidePresses(bool watch)
    {
        if (_pressRoot is not null)
        {
            _pressRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            _pressRoot = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } root)
        {
            _pressRoot = root;
            root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_view == ActionMorphView.Button || e.Source is not Visual source)
        {
            return;
        }
        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)
            || (source is ILogical logical && this.IsLogicalAncestorOf(logical)))
        {
            return;
        }
        Collapse();
    }
}
