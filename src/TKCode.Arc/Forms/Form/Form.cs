using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A light form scope: it lays out nothing itself (put a panel inside), but every <see cref="FormItem"/> in it joins it,
/// so it knows whether any of them has an error, shows them all when the form is submitted, waits for running
/// asynchronous checks, then scrolls to and focuses the first invalid item, or runs <see cref="SubmitCommand"/>. The rules
/// stay in the view model (<c>INotifyDataErrorInfo</c>, reported through the bindings' data validation); the form never
/// reads the view model, and talks to it only through <see cref="ValidateCommand"/>, <see cref="SubmitCommand"/>,
/// <see cref="ResetCommand"/> and the optional <see cref="ErrorSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// Label placement, label width, error trigger, error placement, required indicator and read-only are inherited attached
/// properties: set on the form they are its default, on a section panel they apply to that section, on an item to that
/// item. <c>arc:Form.Action="Submit"</c> (or <c>Reset</c>) on a button runs the form; with <c>IsDefault="True"</c> Enter
/// submits.
/// </para>
/// <para>
/// The form only sees the items that are mounted. Keep checking <c>HasErrors</c> in the view model's submit command, or
/// set <see cref="ErrorSource"/>, for fields on tabs that are not selected or rows that are virtualized away.
/// </para>
/// </remarks>
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(ReadOnlyPseudoClass, SubmittedPseudoClass)]
public class Form : ContentControl
{
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Set while <see cref="IsSubmitAttempted"/> is true.</summary>
    public const string SubmittedPseudoClass = ":submitted";

    #region 附加属性

    /// <summary>Where items put their label: Top (default) or Left. Inherited; set it on the form, a section or an item.</summary>
    public static readonly AttachedProperty<FormLabelPlacement> LabelPlacementProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, FormLabelPlacement>("LabelPlacement", inherits: true);

    /// <summary>
    /// Width of the label column when labels are on the left. NaN (default) sizes it to the widest label of the shared
    /// size scope, up to 240 px. Inherited.
    /// </summary>
    public static readonly AttachedProperty<double> LabelWidthProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, double>("LabelWidth", double.NaN, inherits: true);

    /// <summary>When items show their errors: Change, Blur (default) or Submit. It does not run any rule. Inherited.</summary>
    public static readonly AttachedProperty<FormErrorTrigger> ErrorTriggerProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, FormErrorTrigger>("ErrorTrigger", FormErrorTrigger.Blur, inherits: true);

    /// <summary>Where items show their error: Inline (default), Tooltip or None. Inherited.</summary>
    public static readonly AttachedProperty<FormErrorPlacement> ErrorPlacementProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, FormErrorPlacement>("ErrorPlacement", inherits: true);

    /// <summary>How items mark required entries: Asterisk (default), Optional or None. Inherited.</summary>
    public static readonly AttachedProperty<FormRequiredIndicator> RequiredIndicatorProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, FormRequiredIndicator>("RequiredIndicator", inherits: true);

    /// <summary>
    /// Makes the form, a section or an item read-only. Arc text fields become read-only and items inside skip validation
    /// and hide their required mark. Controls without a read-only mode (checkboxes, switches, selects, pickers, sliders)
    /// stay editable. Inherited.
    /// </summary>
    public static readonly AttachedProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, bool>("IsReadOnly", inherits: true);

    /// <summary>
    /// Set on a button: clicking it submits (<see cref="SubmitAsync"/>) or resets (<see cref="ResetFields"/>) the nearest
    /// form. Do not also give that button a Command. Not inherited.
    /// </summary>
    public static readonly AttachedProperty<FormAction> ActionProperty =
        AvaloniaProperty.RegisterAttached<Form, Control, FormAction>("Action");

    /// <summary>Gets <see cref="LabelPlacementProperty"/>.</summary>
    public static FormLabelPlacement GetLabelPlacement(Control element) => element.GetValue(LabelPlacementProperty);

    /// <summary>Sets <see cref="LabelPlacementProperty"/>.</summary>
    public static void SetLabelPlacement(Control element, FormLabelPlacement value) => element.SetValue(LabelPlacementProperty, value);

    /// <summary>Gets <see cref="LabelWidthProperty"/>.</summary>
    public static double GetLabelWidth(Control element) => element.GetValue(LabelWidthProperty);

    /// <summary>Sets <see cref="LabelWidthProperty"/>.</summary>
    public static void SetLabelWidth(Control element, double value) => element.SetValue(LabelWidthProperty, value);

    /// <summary>Gets <see cref="ErrorTriggerProperty"/>.</summary>
    public static FormErrorTrigger GetErrorTrigger(Control element) => element.GetValue(ErrorTriggerProperty);

    /// <summary>Sets <see cref="ErrorTriggerProperty"/>.</summary>
    public static void SetErrorTrigger(Control element, FormErrorTrigger value) => element.SetValue(ErrorTriggerProperty, value);

    /// <summary>Gets <see cref="ErrorPlacementProperty"/>.</summary>
    public static FormErrorPlacement GetErrorPlacement(Control element) => element.GetValue(ErrorPlacementProperty);

    /// <summary>Sets <see cref="ErrorPlacementProperty"/>.</summary>
    public static void SetErrorPlacement(Control element, FormErrorPlacement value) => element.SetValue(ErrorPlacementProperty, value);

    /// <summary>Gets <see cref="RequiredIndicatorProperty"/>.</summary>
    public static FormRequiredIndicator GetRequiredIndicator(Control element) => element.GetValue(RequiredIndicatorProperty);

    /// <summary>Sets <see cref="RequiredIndicatorProperty"/>.</summary>
    public static void SetRequiredIndicator(Control element, FormRequiredIndicator value) => element.SetValue(RequiredIndicatorProperty, value);

    /// <summary>Gets <see cref="IsReadOnlyProperty"/>.</summary>
    public static bool GetIsReadOnly(Control element) => element.GetValue(IsReadOnlyProperty);

    /// <summary>Sets <see cref="IsReadOnlyProperty"/>.</summary>
    public static void SetIsReadOnly(Control element, bool value) => element.SetValue(IsReadOnlyProperty, value);

    /// <summary>Gets <see cref="ActionProperty"/>.</summary>
    public static FormAction GetAction(Control element) => element.GetValue(ActionProperty);

    /// <summary>Sets <see cref="ActionProperty"/>.</summary>
    public static void SetAction(Control element, FormAction value) => element.SetValue(ActionProperty, value);

    #endregion

    #region 公共属性定义

    /// <summary>False turns validation off: items show no error and are not counted, and a submit is not blocked.</summary>
    public static readonly StyledProperty<bool> IsValidationEnabledProperty =
        AvaloniaProperty.Register<Form, bool>(nameof(IsValidationEnabled), true);

    /// <summary>
    /// Run by <see cref="Validate"/> and by a submit so the view model checks every rule (for example
    /// <c>ValidateAllProperties()</c>), including fields nobody has touched yet.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ValidateCommandProperty =
        AvaloniaProperty.Register<Form, ICommand?>(nameof(ValidateCommand));

    /// <summary>Run with <see cref="SubmitCommandParameter"/> when a submit finds no error.</summary>
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<Form, ICommand?>(nameof(SubmitCommand));

    /// <summary>Parameter passed to <see cref="SubmitCommand"/>.</summary>
    public static readonly StyledProperty<object?> SubmitCommandParameterProperty =
        AvaloniaProperty.Register<Form, object?>(nameof(SubmitCommandParameter));

    /// <summary>Run by <see cref="ResetFields"/> so the view model restores the values and clears its errors.</summary>
    public static readonly StyledProperty<ICommand?> ResetCommandProperty =
        AvaloniaProperty.Register<Form, ICommand?>(nameof(ResetCommand));

    /// <summary>
    /// Optional: the view model's <see cref="INotifyDataErrorInfo"/>. Its <c>HasErrors</c> joins <see cref="HasErrors"/>, so
    /// fields that are not mounted still block a submit, and its entity-level errors (<c>GetErrors(null)</c>) are in
    /// <see cref="FormErrors"/>.
    /// </summary>
    public static readonly StyledProperty<INotifyDataErrorInfo?> ErrorSourceProperty =
        AvaloniaProperty.Register<Form, INotifyDataErrorInfo?>(nameof(ErrorSource));

    /// <summary>Whether any item that takes part in validation has an error (shown or not), or <see cref="ErrorSource"/> has errors.</summary>
    public static readonly DirectProperty<Form, bool> HasErrorsProperty =
        AvaloniaProperty.RegisterDirect<Form, bool>(nameof(HasErrors), o => o.HasErrors);

    /// <summary>Number of items with an error (items, not errors).</summary>
    public static readonly DirectProperty<Form, int> ErrorCountProperty =
        AvaloniaProperty.RegisterDirect<Form, int>(nameof(ErrorCount), o => o.ErrorCount);

    /// <summary>Whether any item is checking its value asynchronously (<see cref="FormItem.IsValidating"/>).</summary>
    public static readonly DirectProperty<Form, bool> IsValidatingProperty =
        AvaloniaProperty.RegisterDirect<Form, bool>(nameof(IsValidating), o => o.IsValidating);

    /// <summary>Whether the form was submitted or validated since the last <see cref="ClearValidation"/> or <see cref="ResetFields"/>.</summary>
    public static readonly DirectProperty<Form, bool> IsSubmitAttemptedProperty =
        AvaloniaProperty.RegisterDirect<Form, bool>(nameof(IsSubmitAttempted), o => o.IsSubmitAttempted);

    /// <summary>The entity-level errors of <see cref="ErrorSource"/>, for the app to show (for example in an Alert).</summary>
    public static readonly DirectProperty<Form, IReadOnlyList<object>> FormErrorsProperty =
        AvaloniaProperty.RegisterDirect<Form, IReadOnlyList<object>>(nameof(FormErrors), o => o.FormErrors);

    /// <inheritdoc cref="IsValidationEnabledProperty"/>
    public bool IsValidationEnabled { get => GetValue(IsValidationEnabledProperty); set => SetValue(IsValidationEnabledProperty, value); }

    /// <inheritdoc cref="ValidateCommandProperty"/>
    public ICommand? ValidateCommand { get => GetValue(ValidateCommandProperty); set => SetValue(ValidateCommandProperty, value); }

    /// <inheritdoc cref="SubmitCommandProperty"/>
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }

    /// <inheritdoc cref="SubmitCommandParameterProperty"/>
    public object? SubmitCommandParameter { get => GetValue(SubmitCommandParameterProperty); set => SetValue(SubmitCommandParameterProperty, value); }

    /// <inheritdoc cref="ResetCommandProperty"/>
    public ICommand? ResetCommand { get => GetValue(ResetCommandProperty); set => SetValue(ResetCommandProperty, value); }

    /// <inheritdoc cref="ErrorSourceProperty"/>
    public INotifyDataErrorInfo? ErrorSource { get => GetValue(ErrorSourceProperty); set => SetValue(ErrorSourceProperty, value); }

    /// <inheritdoc cref="LabelPlacementProperty"/>
    public FormLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <inheritdoc cref="LabelWidthProperty"/>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <inheritdoc cref="ErrorTriggerProperty"/>
    public FormErrorTrigger ErrorTrigger { get => GetValue(ErrorTriggerProperty); set => SetValue(ErrorTriggerProperty, value); }

    /// <inheritdoc cref="ErrorPlacementProperty"/>
    public FormErrorPlacement ErrorPlacement { get => GetValue(ErrorPlacementProperty); set => SetValue(ErrorPlacementProperty, value); }

    /// <inheritdoc cref="RequiredIndicatorProperty"/>
    public FormRequiredIndicator RequiredIndicator { get => GetValue(RequiredIndicatorProperty); set => SetValue(RequiredIndicatorProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="HasErrorsProperty"/>
    public bool HasErrors { get => _hasErrors; private set => SetAndRaise(HasErrorsProperty, ref _hasErrors, value); }

    /// <inheritdoc cref="ErrorCountProperty"/>
    public int ErrorCount { get => _errorCount; private set => SetAndRaise(ErrorCountProperty, ref _errorCount, value); }

    /// <inheritdoc cref="IsValidatingProperty"/>
    public bool IsValidating { get => _isValidating; private set => SetAndRaise(IsValidatingProperty, ref _isValidating, value); }

    /// <inheritdoc cref="IsSubmitAttemptedProperty"/>
    public bool IsSubmitAttempted { get => _isSubmitAttempted; private set => SetAndRaise(IsSubmitAttemptedProperty, ref _isSubmitAttempted, value); }

    /// <inheritdoc cref="FormErrorsProperty"/>
    public IReadOnlyList<object> FormErrors { get => _formErrors; private set => SetAndRaise(FormErrorsProperty, ref _formErrors, value); }

    #endregion

    #region 事件

    /// <summary>A submit found no error; raised before <see cref="SubmitCommand"/> runs.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SubmittedEvent =
        RoutedEvent.Register<Form, RoutedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    /// <summary>
    /// A submit found errors. The arguments list the invalid items in document order; set
    /// <see cref="FormSubmitFailedEventArgs.SuppressFocus"/> to move focus yourself.
    /// </summary>
    public static readonly RoutedEvent<FormSubmitFailedEventArgs> SubmitFailedEvent =
        RoutedEvent.Register<Form, FormSubmitFailedEventArgs>(nameof(SubmitFailed), RoutingStrategies.Bubble);

    /// <summary><see cref="ResetFields"/> has finished.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FieldsResetEvent =
        RoutedEvent.Register<Form, RoutedEventArgs>(nameof(FieldsReset), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<RoutedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    /// <inheritdoc cref="SubmitFailedEvent"/>
    public event EventHandler<FormSubmitFailedEventArgs>? SubmitFailed
    {
        add => AddHandler(SubmitFailedEvent, value);
        remove => RemoveHandler(SubmitFailedEvent, value);
    }

    /// <inheritdoc cref="FieldsResetEvent"/>
    public event EventHandler<RoutedEventArgs>? FieldsReset
    {
        add => AddHandler(FieldsResetEvent, value);
        remove => RemoveHandler(FieldsResetEvent, value);
    }

    #endregion

    private static readonly IReadOnlyList<object> s_noErrors = Array.Empty<object>();

    private readonly List<FormItem> _items = new();
    private readonly List<TaskCompletionSource<bool>> _validationWaiters = new();
    private bool _hasErrors;
    private int _errorCount;
    private bool _isValidating;
    private bool _isSubmitAttempted;
    private IReadOnlyList<object> _formErrors = s_noErrors;
    private Task<bool>? _submitTask;
    private INotifyDataErrorInfo? _subscribedSource;
    private TextBlock? _announcer;

    static Form()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<Form>(AutomationLandmarkType.Form);
    }

    /// <summary>Creates a form.</summary>
    public Form()
    {
        AddHandler(AvaloniaButton.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
    }

    /// <summary>The items that joined this form, in the order they joined.</summary>
    internal IReadOnlyList<FormItem> Items => _items;

    #region 方法

    /// <summary>
    /// Shows every item's errors, runs <see cref="ValidateCommand"/> and returns whether the form is valid. Returns false
    /// while an asynchronous check is still running; use <see cref="ValidateAsync"/> to wait for it.
    /// </summary>
    public bool Validate()
    {
        if (!IsValidationEnabled)
        {
            return true;
        }
        RevealAndValidate();
        if (HasErrors)
        {
            Announce(ErrorCount);
        }
        return !HasErrors && !IsValidating;
    }

    /// <summary>Like <see cref="Validate"/>, but waits for running asynchronous checks first.</summary>
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsValidationEnabled)
        {
            return true;
        }
        RevealAndValidate();
        if (!await WaitForValidationAsync(cancellationToken))
        {
            return false;
        }
        RefreshItems();
        if (HasErrors)
        {
            Announce(ErrorCount);
        }
        return !HasErrors;
    }

    /// <summary>
    /// Submits: shows every error, runs <see cref="ValidateCommand"/>, waits for running asynchronous checks, then either
    /// raises <see cref="SubmitFailed"/> and focuses the first invalid item, or raises <see cref="Submitted"/> and runs
    /// <see cref="SubmitCommand"/>. Returns true when the form was valid and the command (if any) ran. A second call while a
    /// submit is waiting returns the same task.
    /// </summary>
    public Task<bool> SubmitAsync()
    {
        if (_submitTask is { IsCompleted: false })
        {
            return _submitTask;
        }
        var task = SubmitCoreAsync();
        _submitTask = task.IsCompleted ? null : task;
        return task;
    }

    private async Task<bool> SubmitCoreAsync()
    {
        if (!IsValidationEnabled)
        {
            return RunSubmit();
        }
        RevealAndValidate();
        if (!await WaitForValidationAsync(CancellationToken.None))
        {
            return false;
        }
        RefreshItems();
        if (!HasErrors)
        {
            return RunSubmit();
        }
        var invalid = GetInvalidItems();
        var args = new FormSubmitFailedEventArgs(SubmitFailedEvent, invalid, FormErrors) { Source = this };
        RaiseEvent(args);
        if (!args.SuppressFocus && args.FirstInvalidItem is { } first)
        {
            FocusItem(first);
        }
        Announce(invalid.Count);
        return false;
    }

    private bool RunSubmit()
    {
        RaiseEvent(new RoutedEventArgs(SubmittedEvent, this));
        var command = SubmitCommand;
        if (command is null)
        {
            return true;
        }
        var parameter = SubmitCommandParameter;
        if (!command.CanExecute(parameter))
        {
            return false;
        }
        command.Execute(parameter);
        return true;
    }

    /// <summary>
    /// Runs <see cref="ResetCommand"/> so the view model restores its values, then <see cref="ClearValidation"/>, then raises
    /// <see cref="FieldsReset"/>. A submit that is waiting for asynchronous checks stops and returns false.
    /// </summary>
    public void ResetFields()
    {
        CancelValidationWaits();
        var command = ResetCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
        ClearValidation();
        RaiseEvent(new RoutedEventArgs(FieldsResetEvent, this));
    }

    /// <summary>
    /// Hides every error shown and forgets that the form was submitted or items were left, so errors show again only by
    /// their trigger. The view model's errors are not touched (<see cref="HasErrors"/> stays), and an explicit
    /// <see cref="Field.ErrorMessage"/> stays until the app clears it. The issue's <c>ClearValidate()</c>.
    /// </summary>
    public void ClearValidation()
    {
        foreach (var item in _items.ToArray())
        {
            item.ClearValidation();
        }
        IsSubmitAttempted = false;
        if (_announcer is not null)
        {
            _announcer.Text = null;
        }
    }

    /// <summary>
    /// Scrolls to and focuses the first invalid item in document order, for example after server errors arrived. Returns
    /// false when no mounted item has an error.
    /// </summary>
    public bool FocusFirstError()
    {
        RefreshItems();
        var invalid = GetInvalidItems();
        return invalid.Count > 0 && FocusItem(invalid[0]);
    }

    /// <summary>The mounted items that take part in validation and have an error, in document order.</summary>
    public IReadOnlyList<FormItem> GetInvalidItems()
    {
        var result = new List<FormItem>();
        foreach (var item in OrderedItems())
        {
            if (item.IsParticipating && item.HasErrors)
            {
                result.Add(item);
            }
        }
        return result;
    }

    #endregion

    #region 内部

    internal void Add(FormItem item)
    {
        if (!_items.Contains(item))
        {
            _items.Add(item);
        }
        Recompute();
    }

    internal void Remove(FormItem item)
    {
        if (_items.Remove(item))
        {
            Recompute();
        }
    }

    internal void OnItemChanged(FormItem item) => Recompute();

    private void RevealAndValidate()
    {
        IsSubmitAttempted = true;
        // One batch: the items do not announce their errors one by one, the form announces a summary instead.
        var items = _items.ToArray();
        foreach (var item in items)
        {
            item.SetAnnouncementsSuppressed(true);
            item.Reveal();
        }
        var command = ValidateCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
        RefreshItems();
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var item in items)
            {
                item.SetAnnouncementsSuppressed(false);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>Collects every item again (catches editors removed from an item and visibility changes of ancestors).</summary>
    private void RefreshItems()
    {
        foreach (var item in _items.ToArray())
        {
            item.Collect();
        }
        Recompute();
    }

    private void Recompute()
    {
        var enabled = IsValidationEnabled;
        var count = 0;
        var validating = false;
        foreach (var item in _items)
        {
            if (!item.IsParticipating)
            {
                continue;
            }
            if (item.HasErrors)
            {
                count++;
            }
            if (item.IsValidating)
            {
                validating = true;
            }
        }
        ErrorCount = enabled ? count : 0;
        IsValidating = validating;
        HasErrors = enabled && (count > 0 || (ErrorSource?.HasErrors ?? false));
        if (!validating && _validationWaiters.Count > 0)
        {
            CompleteValidationWaits(true);
        }
    }

    private Task<bool> WaitForValidationAsync(CancellationToken cancellationToken)
    {
        if (!IsValidating)
        {
            return Task.FromResult(true);
        }
        // No timeout: the view model clears its checking flag in a finally block. Reset and unloading stop the wait.
        var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _validationWaiters.Add(waiter);
        if (cancellationToken.CanBeCanceled)
        {
            var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(() =>
            {
                _validationWaiters.Remove(waiter);
                waiter.TrySetCanceled(cancellationToken);
            }));
            waiter.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
        }
        return waiter.Task;
    }

    private void CompleteValidationWaits(bool result)
    {
        var waiters = _validationWaiters.ToArray();
        _validationWaiters.Clear();
        foreach (var waiter in waiters)
        {
            waiter.TrySetResult(result);
        }
    }

    private void CancelValidationWaits()
    {
        if (_validationWaiters.Count > 0)
        {
            CompleteValidationWaits(false);
        }
    }

    /// <summary>The joined items in document order (a walk of the form's visual descendants), then any not in the visual tree.</summary>
    private List<FormItem> OrderedItems()
    {
        var ordered = new List<FormItem>(_items.Count);
        var seen = new HashSet<FormItem>();
        foreach (var visual in this.GetVisualDescendants())
        {
            if (visual is FormItem item && ReferenceEquals(item.OwnerForm, this) && seen.Add(item))
            {
                ordered.Add(item);
            }
        }
        foreach (var item in _items)
        {
            if (seen.Add(item))
            {
                ordered.Add(item);
            }
        }
        return ordered;
    }

    private bool FocusItem(FormItem item)
    {
        // The error rows that just opened move the items below them: lay out before scrolling.
        UpdateLayout();
        var margin = this.TryFindResource(FormTokens.Key(FormTokenKind.ErrorScrollMargin), out var value) && value is double d ? d : 24;
        var bounds = item.Bounds;
        item.BringIntoView(new Rect(0, -margin, bounds.Width, bounds.Height + margin));
        return item.GetFocusTarget() is { } target && target.Focus(NavigationMethod.Tab);
    }

    private void Announce(int invalidCount)
    {
        if (_announcer is null || invalidCount <= 0)
        {
            return;
        }
        var text = invalidCount == 1
            ? FormLangResources.Get(FormLangResourceKind.InvalidSummaryOne)
            : FormLangResources.Format(FormLangResourceKind.InvalidSummaryMany, invalidCount);
        // Clear first so the same summary twice in a row is announced again.
        _announcer.Text = null;
        _announcer.Text = text;
    }

    /// <summary>The summary the form announced last (for tests).</summary>
    internal string? AnnouncedSummary => _announcer?.Text;

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Control source)
        {
            return;
        }
        switch (GetAction(source))
        {
            case FormAction.Submit:
                e.Handled = true;
                _ = SubmitAsync();
                break;
            case FormAction.Reset:
                e.Handled = true;
                ResetFields();
                break;
        }
    }

    private void SubscribeErrorSource()
    {
        var source = ErrorSource;
        if (ReferenceEquals(source, _subscribedSource))
        {
            return;
        }
        if (_subscribedSource is not null)
        {
            _subscribedSource.ErrorsChanged -= OnSourceErrorsChanged;
        }
        _subscribedSource = source;
        if (source is not null)
        {
            source.ErrorsChanged += OnSourceErrorsChanged;
        }
        UpdateFormErrors();
    }

    private void UnsubscribeErrorSource()
    {
        if (_subscribedSource is not null)
        {
            _subscribedSource.ErrorsChanged -= OnSourceErrorsChanged;
            _subscribedSource = null;
        }
    }

    private void OnSourceErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateFormErrors);
            return;
        }
        UpdateFormErrors();
    }

    private void UpdateFormErrors()
    {
        var source = ErrorSource;
        var errors = new List<object>();
        if (source?.GetErrors(null) is IEnumerable all)
        {
            foreach (var error in all)
            {
                if (error is not null)
                {
                    errors.Add(error);
                }
            }
        }
        if (!errors.SequenceEqual(_formErrors))
        {
            FormErrors = errors.Count == 0 ? s_noErrors : errors.AsReadOnly();
        }
        Recompute();
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeErrorSource();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Unloading stops a submit that waits for checks, and lets the view model outlive the view.
        CancelValidationWaits();
        UnsubscribeErrorSource();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsValidationEnabledProperty)
        {
            foreach (var item in _items.ToArray())
            {
                item.OnFormChanged();
            }
            Recompute();
        }
        else if (property == ErrorSourceProperty)
        {
            if (TopLevel.GetTopLevel(this) is not null)
            {
                SubscribeErrorSource();
            }
            else
            {
                UpdateFormErrors();
            }
        }
        else if (property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, change.GetNewValue<bool>());
        }
        else if (property == IsSubmitAttemptedProperty)
        {
            PseudoClasses.Set(SubmittedPseudoClass, IsSubmitAttempted);
        }
    }
}
