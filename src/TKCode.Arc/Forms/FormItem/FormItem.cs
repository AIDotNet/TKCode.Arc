using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// One labelled entry of a <see cref="Form"/>: the label (with a required or optional mark), the editor, a description
/// and the error. It collects the data-validation errors the bindings of its editors report (Avalonia
/// <see cref="DataValidationErrors"/>, so the rules stay in the view model, for example <c>INotifyDataErrorInfo</c>) and
/// shows the first one when <see cref="Form.ErrorTriggerProperty"/> says so. It joins the nearest <see cref="Form"/> when
/// it enters the logical tree and leaves it when removed, so items in an <c>ItemsControl</c> template need no
/// registration.
/// </summary>
/// <remarks>
/// <para>
/// Order of the error shown: <see cref="Field.ErrorMessage"/> set on the item (for example a server error), then an
/// explicit <c>ErrorMessage</c> of an Arc field inside it, then the binding errors. The first two show at once; the
/// binding errors follow the error trigger. Arc fields inside the item leave their label and error rows to the item and
/// turn invalid only while the item shows its errors.
/// </para>
/// <para>
/// The label names <see cref="Target"/> (or the first focusable editor) for assistive technology and focuses it when
/// clicked; the help text holds the description and the error only once it is shown.
/// </para>
/// </remarks>
[TemplatePart(LayoutPart, typeof(Grid))]
[TemplatePart(LabelRowPart, typeof(Control))]
[TemplatePart(BodyPart, typeof(Panel))]
[TemplatePart(AsteriskPart, typeof(TextBlock))]
[TemplatePart(OptionalPart, typeof(TextBlock))]
[PseudoClasses(RequiredPseudoClass, ValidatingPseudoClass, ReadOnlyPseudoClass, LabelLeftPseudoClass)]
public class FormItem : Field
{
    public const string LayoutPart = "PART_Layout";
    public const string LabelRowPart = "PART_LabelRow";
    public const string BodyPart = "PART_Body";
    public const string AsteriskPart = "PART_Asterisk";
    public const string OptionalPart = "PART_Optional";

    /// <summary>Set while <see cref="IsRequired"/> is true.</summary>
    public const string RequiredPseudoClass = ":required";

    /// <summary>Set while <see cref="IsValidating"/> is true.</summary>
    public const string ValidatingPseudoClass = ":validating";

    /// <summary>Set while the effective <see cref="Form.IsReadOnlyProperty"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Set while <see cref="Form.LabelPlacementProperty"/> is <see cref="FormLabelPlacement.Left"/> (Top has none).</summary>
    public const string LabelLeftPseudoClass = ":label-left";

    /// <summary>The shared size group of automatic left label columns (<c>Grid.IsSharedSizeScope</c> starts a new one).</summary>
    public const string LabelSizeGroup = "ArcFormLabel";

    #region 公共属性定义

    /// <summary>
    /// Marks the item as required: an asterisk after the label (see <see cref="Form.RequiredIndicatorProperty"/>) and
    /// <c>IsRequiredForForm</c> on the editor. It adds no rule; the view model decides what is valid.
    /// </summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<FormItem, bool>(nameof(IsRequired));

    /// <summary>
    /// The editor the label names and focuses. Null uses the content when it can be focused, otherwise its first
    /// focusable, enabled descendant.
    /// </summary>
    public static readonly StyledProperty<Control?> TargetProperty =
        AvaloniaProperty.Register<FormItem, Control?>(nameof(Target));

    /// <summary>
    /// False takes the item out of validation: it shows no error, is not counted and does not block a submit. Combined
    /// with <see cref="Form.IsValidationEnabled"/>.
    /// </summary>
    public static readonly StyledProperty<bool> IsValidationEnabledProperty =
        AvaloniaProperty.Register<FormItem, bool>(nameof(IsValidationEnabled), true);

    /// <summary>
    /// True while the view model checks the value asynchronously (bind it to the view model's flag): the item shows
    /// "Checking…" instead of its binding errors, and a submit waits until it is false again.
    /// </summary>
    public static readonly StyledProperty<bool> IsValidatingProperty =
        AvaloniaProperty.Register<FormItem, bool>(nameof(IsValidating));

    /// <summary>
    /// Template of the error, inline and in the tooltip. Its data is the first error object (usually the string the view
    /// model reported); without a template the error is its text.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> ErrorTemplateProperty =
        AvaloniaProperty.Register<FormItem, IDataTemplate?>(nameof(ErrorTemplate));

    /// <summary>Every binding error collected from the editors in the item, in document order, shown or not.</summary>
    public static readonly DirectProperty<FormItem, IReadOnlyList<object>> ErrorsProperty =
        AvaloniaProperty.RegisterDirect<FormItem, IReadOnlyList<object>>(nameof(Errors), o => o.Errors);

    /// <summary>Whether the item takes part in validation and has an error (collected or explicit), shown or not.</summary>
    public static readonly DirectProperty<FormItem, bool> HasErrorsProperty =
        AvaloniaProperty.RegisterDirect<FormItem, bool>(nameof(HasErrors), o => o.HasErrors);

    /// <summary>The error text currently shown, or null while none is shown.</summary>
    public static readonly DirectProperty<FormItem, string?> EffectiveErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<FormItem, string?>(nameof(EffectiveErrorMessage), o => o.EffectiveErrorMessage);

    /// <inheritdoc cref="IsRequiredProperty"/>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <inheritdoc cref="TargetProperty"/>
    public Control? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <inheritdoc cref="IsValidationEnabledProperty"/>
    public bool IsValidationEnabled { get => GetValue(IsValidationEnabledProperty); set => SetValue(IsValidationEnabledProperty, value); }

    /// <inheritdoc cref="IsValidatingProperty"/>
    public bool IsValidating { get => GetValue(IsValidatingProperty); set => SetValue(IsValidatingProperty, value); }

    /// <inheritdoc cref="ErrorTemplateProperty"/>
    public IDataTemplate? ErrorTemplate { get => GetValue(ErrorTemplateProperty); set => SetValue(ErrorTemplateProperty, value); }

    /// <inheritdoc cref="ErrorsProperty"/>
    public IReadOnlyList<object> Errors
    {
        get => _errorsView;
        private set => SetAndRaise(ErrorsProperty, ref _errorsView, value);
    }

    /// <inheritdoc cref="HasErrorsProperty"/>
    public bool HasErrors
    {
        get => _hasErrors;
        private set => SetAndRaise(HasErrorsProperty, ref _hasErrors, value);
    }

    /// <inheritdoc cref="EffectiveErrorMessageProperty"/>
    public string? EffectiveErrorMessage
    {
        get => _effectiveError;
        private set => SetAndRaise(EffectiveErrorMessageProperty, ref _effectiveError, value);
    }

    #endregion

    private static readonly IReadOnlyList<object> s_noErrors = Array.Empty<object>();

    private IReadOnlyList<object> _errorsView = s_noErrors;
    private bool _hasErrors;
    private string? _effectiveError;
    private object? _shownError;
    private string? _fieldError;
    private bool _shown;
    private bool _openOnNextChange;
    private bool _focusWithin;
    private bool _tooltipHeld;
    private bool _announcementsSuppressed;
    private Form? _form;
    private Control? _automationTarget;
    private string? _appliedName;

    private Grid? _layout;
    private Control? _labelRow;
    private Panel? _body;
    private TextBlock? _asterisk;
    private TextBlock? _optional;
    private ContentPresenter? _contentPresenter;
    private FieldMessage? _errorRow;
    private FieldMessage? _statusRow;
    private IDisposable? _statusText;
    private ContentPresenter? _errorPresenter;
    private ContentPresenter? _tipContent;
    private TextBlock? _announcer;
    private IDisposable? _labelMaxWidth;

    static FormItem()
    {
        IsInvalidProperty.OverrideMetadata<FormItem>(new StyledPropertyMetadata<bool>(coerce: CoerceItemInvalid));
        // One class handler for the whole app: an editor's errors changed, so the nearest item collects again.
        DataValidationErrors.ErrorsProperty.Changed.AddClassHandler<Control>((c, _) => NotifyContainingItem(c));
        Field.ErrorMessageProperty.Changed.AddClassHandler<Control>((c, _) => NotifyContainingItem(c));
        TagInput.ErrorMessageProperty.Changed.AddClassHandler<TagInput>((c, _) => NotifyContainingItem(c));
    }

    /// <summary>Creates an item.</summary>
    public FormItem()
    {
        // Fields inside leave their label and error rows to the item from the start (no flash of their own rows).
        SetValue(FormFieldHost.StateProperty, FormFieldState.Hidden);
        AddHandler(GotFocusEvent, OnFocusEntered, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, OnFocusLeft, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The form this item has joined (the nearest <see cref="Form"/> ancestor), or null.</summary>
    internal Form? OwnerForm => _form;

    /// <summary>Whether errors of this item currently show (the display switch of the error trigger).</summary>
    internal bool IsRevealed => _shown;

    private FormErrorTrigger Trigger => GetValue(Form.ErrorTriggerProperty);

    private static bool CoerceItemInvalid(AvaloniaObject target, bool value)
        => value || ((FormItem)target)._shownError is not null;

    /// <summary>
    /// Takes part in validation: validation enabled on the item and its form, and the item enabled, visible and not
    /// read-only (the HTML constraint validation rules).
    /// </summary>
    internal bool IsParticipating =>
        IsValidationEnabled && (_form?.IsValidationEnabled ?? true) && IsEffectivelyEnabled && IsEffectivelyVisible &&
        !GetValue(Form.IsReadOnlyProperty);

    /// <summary>Hides the errors this item shows until its error trigger shows them again; the view model's errors stay.</summary>
    public void ClearValidation()
    {
        _shown = false;
        _openOnNextChange = Trigger == FormErrorTrigger.Change;
        Refresh();
    }

    /// <summary>Shows the item's errors now (submit or validate).</summary>
    internal void Reveal()
    {
        _openOnNextChange = false;
        if (!_shown)
        {
            _shown = true;
            Refresh();
        }
    }

    /// <summary>Turns the announcement of the error row off while the form announces a summary instead.</summary>
    internal void SetAnnouncementsSuppressed(bool suppressed)
    {
        _announcementsSuppressed = suppressed;
        var setting = suppressed ? AutomationLiveSetting.Off : AutomationLiveSetting.Assertive;
        if (_errorRow is not null)
        {
            AutomationProperties.SetLiveSetting(_errorRow, setting);
        }
        if (_announcer is not null)
        {
            AutomationProperties.SetLiveSetting(_announcer, setting);
        }
    }

    #region 收集错误

    private static void NotifyContainingItem(Control source)
    {
        if (source is FormItem)
        {
            return;
        }
        for (var node = ParentOf(source); node is not null; node = ParentOf(node))
        {
            if (node is FormItem item)
            {
                item.Collect();
                return;
            }
        }
    }

    /// <summary>
    /// The logical parent, else the visual parent. A popup's root continues at the popup (its logical parent), so editors'
    /// popups count as inside; any other top level ends the walk.
    /// </summary>
    private static StyledElement? ParentOf(StyledElement element)
    {
        if (element is TopLevel and not PopupRoot)
        {
            return null;
        }
        return element.Parent ?? (element as Visual)?.GetVisualParent() as StyledElement;
    }

    /// <summary>Whether <paramref name="element"/> is this item or inside it (logical tree first, so popups of its editors count).</summary>
    internal bool Contains(object? element)
    {
        for (var node = element as StyledElement; node is not null; node = ParentOf(node))
        {
            if (ReferenceEquals(node, this))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Collects the errors of the editors inside the item again (logical descendants, nested items excluded).</summary>
    internal void Collect()
    {
        var errors = new List<object>();
        string? fieldError = null;
        CollectFrom(this, errors, ref fieldError);
        var changed = !errors.SequenceEqual(_errorsView);
        if (changed)
        {
            Errors = errors.Count == 0 ? s_noErrors : errors.AsReadOnly();
            if (_openOnNextChange && Trigger == FormErrorTrigger.Change)
            {
                _openOnNextChange = false;
                _shown = true;
            }
        }
        _fieldError = fieldError;
        Refresh();
    }

    private static void CollectFrom(ILogical node, List<object> errors, ref string? fieldError)
    {
        foreach (var child in node.GetLogicalChildren())
        {
            if (child is FormItem)
            {
                continue;
            }
            if (child is Control control)
            {
                if (DataValidationErrors.GetErrors(control) is { } list)
                {
                    foreach (var error in list)
                    {
                        if (error is not null)
                        {
                            errors.Add(error);
                        }
                    }
                }
                fieldError ??= ExplicitErrorOf(control);
            }
            CollectFrom(child, errors, ref fieldError);
        }
    }

    private static string? ExplicitErrorOf(Control control)
    {
        var text = control switch
        {
            FieldTextBox field => field.ErrorMessage,
            Field field => field.ErrorMessage,
            TimePicker picker => picker.ErrorMessage,
            DateRangePicker picker => picker.ErrorMessage,
            ColorPicker picker => picker.ErrorMessage,
            TagInput tags => tags.ErrorMessage,
            _ => null
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Works out what the item shows from what it collected, then updates the presentation and its form.</summary>
    private void Refresh()
    {
        var participating = IsParticipating;
        var itemError = ErrorMessage;
        var hasItemError = !string.IsNullOrEmpty(itemError);
        var hasFieldError = _fieldError is not null;
        HasErrors = participating && (_errorsView.Count > 0 || hasItemError || hasFieldError);

        object? shown = null;
        if (participating)
        {
            if (hasItemError)
            {
                shown = itemError;
            }
            else if (hasFieldError)
            {
                shown = _fieldError;
            }
            else if (_shown && !IsValidating && _errorsView.Count > 0)
            {
                shown = _errorsView[0];
            }
        }
        _shownError = shown;
        SetValue(FormFieldHost.StateProperty, !participating ? FormFieldState.Hidden
            : hasItemError ? FormFieldState.Forced
            : _shown && !IsValidating ? FormFieldState.Shown
            : FormFieldState.Hidden);
        EffectiveErrorMessage = shown is null ? null : FormFieldHost.TextOf(shown) ?? string.Empty;
        CoerceValue(IsInvalidProperty);
        UpdateErrorPresentation();
        UpdateContentAutomation();
        _form?.OnItemChanged(this);
    }

    /// <summary>
    /// The control to focus when a submit fails: the first control in the item with an error that can take focus (or its
    /// first focusable descendant), else <see cref="Target"/>.
    /// </summary>
    internal InputElement? GetFocusTarget()
    {
        var withError = FindErrorControl(this);
        if (withError is not null && FirstFocusable(withError) is { } inner)
        {
            return inner;
        }
        return ResolveTarget() is { } target ? FirstFocusable(target) : null;
    }

    private static Control? FindErrorControl(ILogical node)
    {
        foreach (var child in node.GetLogicalChildren())
        {
            if (child is FormItem)
            {
                continue;
            }
            if (child is Control control && (DataValidationErrors.GetHasErrors(control) || ExplicitErrorOf(control) is not null))
            {
                return control;
            }
            if (FindErrorControl(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static InputElement? FirstFocusable(Control control)
        => control.Focusable && control.IsEffectivelyEnabled ? control
            : control.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyEnabled && c.IsEffectivelyVisible);

    #endregion

    #region 表单

    private void JoinForm()
    {
        Form? form = null;
        for (var node = ParentOf(this); node is not null; node = ParentOf(node))
        {
            if (node is Form found)
            {
                form = found;
                break;
            }
        }
        if (ReferenceEquals(form, _form))
        {
            return;
        }
        _form?.Remove(this);
        _form = form;
        _form?.Add(this);
    }

    private void LeaveForm()
    {
        var form = _form;
        _form = null;
        form?.Remove(this);
    }

    /// <summary>The form's validation switch changed: participation changes.</summary>
    internal void OnFormChanged() => Refresh();

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        JoinForm();
        Collect();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        LeaveForm();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Hosts that are only in the visual tree: look again now that the visual parents are known.
        if (_form is null)
        {
            JoinForm();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // The editors' templates exist now, so a focusable descendant can be found for the label.
        UpdateContentAutomation();
    }

    #endregion

    #region 焦点

    private void OnFocusEntered(object? sender, FocusChangedEventArgs e)
    {
        _focusWithin = true;
        if (Form.GetErrorPlacement(this) == FormErrorPlacement.Tooltip && _shownError is not null && _contentPresenter is not null)
        {
            // Moving between editors of the item closes the tooltip (its trigger lost focus); keep it open.
            _tooltipHeld = true;
            Tooltip.SetIsOpen(_contentPresenter, false);
            Tooltip.SetIsOpen(_contentPresenter, true);
        }
    }

    private void OnFocusLeft(object? sender, FocusChangedEventArgs e)
    {
        if (e.NewFocusedElement is { } next && Contains(next))
        {
            return;
        }
        _focusWithin = false;
        if (Trigger == FormErrorTrigger.Blur && !_shown)
        {
            _shown = true;
            _openOnNextChange = false;
            Refresh();
        }
        else
        {
            UpdateHeldTooltip();
        }
    }

    #endregion

    #region 模板与呈现

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _labelMaxWidth?.Dispose();
        _labelMaxWidth = null;
        if (_contentPresenter is not null)
        {
            Tooltip.SetTip(_contentPresenter, null);
        }
        _tooltipHeld = false;
        _statusText?.Dispose();
        _statusText = null;
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<Grid>(LayoutPart);
        _labelRow = e.NameScope.Find<Control>(LabelRowPart);
        _body = e.NameScope.Find<Panel>(BodyPart);
        _asterisk = e.NameScope.Find<TextBlock>(AsteriskPart);
        _optional = e.NameScope.Find<TextBlock>(OptionalPart);
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        _errorRow = e.NameScope.Find<FieldMessage>(ErrorPart);
        _statusRow = null;
        _errorPresenter = null;
        _announcer = null;
        UpdateLabelLayout();
        UpdateMarks();
        UpdateStatus();
        SetAnnouncementsSuppressed(_announcementsSuppressed);
        UpdateErrorPresentation();
    }

    private void UpdateLabelLayout()
    {
        var left = GetValue(Form.LabelPlacementProperty) == FormLabelPlacement.Left;
        PseudoClasses.Set(LabelLeftPseudoClass, left);
        if (_layout is null || _labelRow is null || _body is null)
        {
            return;
        }
        _layout.ColumnDefinitions.Clear();
        _layout.RowDefinitions.Clear();
        _labelMaxWidth?.Dispose();
        _labelMaxWidth = null;
        _labelRow.ClearValue(MaxWidthProperty);
        if (left)
        {
            var width = GetValue(Form.LabelWidthProperty);
            if (double.IsNaN(width) || width < 0)
            {
                // Auto: every left label of the shared size scope (the form, or a section that starts its own) takes the
                // widest width, capped by FormToken.AutoLabelMaxWidth; longer labels wrap.
                _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = LabelSizeGroup });
                _labelMaxWidth = _labelRow.Bind(MaxWidthProperty, _labelRow.GetResourceObservable(FormTokens.Key(FormTokenKind.AutoLabelMaxWidth)));
            }
            else
            {
                _layout.ColumnDefinitions.Add(new ColumnDefinition(width, GridUnitType.Pixel));
            }
            _layout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetRow(_labelRow, 0);
            Grid.SetColumn(_labelRow, 0);
            Grid.SetRow(_body, 0);
            Grid.SetColumn(_body, 1);
        }
        else
        {
            _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(_labelRow, 0);
            Grid.SetColumn(_labelRow, 0);
            Grid.SetRow(_body, 1);
            Grid.SetColumn(_body, 0);
        }
        UpdateLabelRowVisibility();
    }

    private bool IsLabelShown => IsLabelVisible && !string.IsNullOrEmpty(Label);

    private void UpdateLabelRowVisibility()
    {
        if (_labelRow is not null)
        {
            // A left column stays (empty) so the editor lines up with the others.
            _labelRow.IsVisible = IsLabelShown || GetValue(Form.LabelPlacementProperty) == FormLabelPlacement.Left;
        }
    }

    private void UpdateMarks()
    {
        var readOnly = GetValue(Form.IsReadOnlyProperty);
        var indicator = GetValue(Form.RequiredIndicatorProperty);
        var labelShown = IsLabelShown && !readOnly;
        if (_asterisk is not null)
        {
            _asterisk.IsVisible = labelShown && indicator == FormRequiredIndicator.Asterisk && IsRequired;
        }
        if (_optional is not null)
        {
            _optional.IsVisible = labelShown && indicator == FormRequiredIndicator.Optional && !IsRequired;
        }
    }

    private void UpdateStatus()
    {
        PseudoClasses.Set(ValidatingPseudoClass, IsValidating);
        if (IsValidating)
        {
            if (_statusRow is null && _body is not null)
            {
                // Built the first time a check runs: most items never validate asynchronously.
                _statusRow = new FieldMessage { Name = "PART_Status", Kind = FieldMessageKind.Description };
                AutomationProperties.SetLiveSetting(_statusRow, AutomationLiveSetting.Polite);
                var index = _errorRow is not null ? _body.Children.IndexOf(_errorRow) : -1;
                _body.Children.Insert(index < 0 ? _body.Children.Count : index, _statusRow);
            }
            if (_statusRow is not null && _statusText is null)
            {
                _statusText = _statusRow.Bind(FieldMessage.TextProperty,
                    _statusRow.GetResourceObservable(FormLangResources.Key(FormLangResourceKind.Validating)));
            }
        }
        else if (_statusRow is not null)
        {
            _statusText?.Dispose();
            _statusText = null;
            _statusRow.Text = null;
        }
    }

    private void UpdateErrorPresentation()
    {
        var placement = GetValue(Form.ErrorPlacementProperty);
        var text = EffectiveErrorMessage;
        var template = ErrorTemplate;
        var inline = placement == FormErrorPlacement.Inline;

        if (_errorRow is not null)
        {
            _errorRow.Text = inline && template is null ? text : null;
        }

        if (inline && template is not null && _shownError is not null)
        {
            if (_errorPresenter is null && _body is not null)
            {
                _errorPresenter = new ContentPresenter { Name = "PART_ErrorPresenter" };
                AutomationProperties.SetLiveSetting(_errorPresenter, AutomationLiveSetting.Assertive);
                var index = _errorRow is not null ? _body.Children.IndexOf(_errorRow) + 1 : _body.Children.Count;
                _body.Children.Insert(index, _errorPresenter);
            }
            if (_errorPresenter is not null)
            {
                // Content first, then the template (a mounted presenter rebuilds on each change).
                _errorPresenter.Content = _shownError;
                _errorPresenter.ContentTemplate = template;
                _errorPresenter.IsVisible = true;
            }
        }
        else if (_errorPresenter is not null)
        {
            _errorPresenter.ContentTemplate = null;
            _errorPresenter.Content = null;
            _errorPresenter.IsVisible = false;
        }

        if (_contentPresenter is not null)
        {
            object? tip = null;
            if (placement == FormErrorPlacement.Tooltip && _shownError is not null)
            {
                if (template is null)
                {
                    tip = text;
                }
                else
                {
                    _tipContent ??= new ContentPresenter();
                    _tipContent.ContentTemplate = null;
                    _tipContent.Content = _shownError;
                    _tipContent.ContentTemplate = template;
                    tip = _tipContent;
                }
                Tooltip.SetSide(_contentPresenter, TooltipSide.Bottom);
            }
            var hadTip = Tooltip.GetTip(_contentPresenter) is not null;
            if (!Equals(Tooltip.GetTip(_contentPresenter), tip))
            {
                Tooltip.SetTip(_contentPresenter, tip);
            }
            if (!hadTip && tip is not null)
            {
                _tooltipHeld = false;
            }
        }
        UpdateHeldTooltip();
        UpdateAnnouncer(placement, text);
    }

    private void UpdateHeldTooltip()
    {
        if (_contentPresenter is null)
        {
            return;
        }
        var hold = _focusWithin && _shownError is not null && GetValue(Form.ErrorPlacementProperty) == FormErrorPlacement.Tooltip;
        if (hold != _tooltipHeld)
        {
            _tooltipHeld = hold;
            Tooltip.SetIsOpen(_contentPresenter, hold);
        }
    }

    private void UpdateAnnouncer(FormErrorPlacement placement, string? text)
    {
        // Tooltip and None have no visible row to announce, so a live text that takes no space says the same.
        var announced = placement == FormErrorPlacement.Inline ? null : text;
        if (announced is null && _announcer is null)
        {
            return;
        }
        if (_announcer is null && _layout is not null)
        {
            _announcer = new TextBlock
            {
                Name = "PART_ErrorAnnouncer",
                Width = 1,
                Height = 1,
                Opacity = 0,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumnSpan(_announcer, 2);
            Grid.SetRowSpan(_announcer, 2);
            AutomationProperties.SetLiveSetting(_announcer,
                _announcementsSuppressed ? AutomationLiveSetting.Off : AutomationLiveSetting.Assertive);
            _layout.Children.Add(_announcer);
        }
        if (_announcer is not null)
        {
            _announcer.Text = announced;
        }
    }

    #endregion

    #region 无障碍

    /// <summary>
    /// The editor the label names: <see cref="Target"/>, else the content when it can be focused, else the content's first
    /// focusable, enabled descendant (or the content itself before its template exists).
    /// </summary>
    internal Control? ResolveTarget()
    {
        if (Target is { } target)
        {
            return target;
        }
        if (Content is not Control content)
        {
            return null;
        }
        if (content.Focusable)
        {
            return content;
        }
        return content.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyEnabled) ?? content;
    }

    protected override Control? GetLabelTarget() => ResolveTarget();

    // Only the error that is shown: an error the person cannot see is not read out either.
    protected override string? GetAutomationErrorText() => EffectiveErrorMessage;

    protected override void UpdateContentAutomation()
    {
        var target = ResolveTarget();
        if (!ReferenceEquals(target, _automationTarget) && _automationTarget is { } old)
        {
            FormFieldHost.SetItemHelpText(old, null);
            old.ClearValue(AutomationProperties.IsRequiredForFormProperty);
            if (ReferenceEquals(AutomationProperties.GetLabeledBy(old), LabelElement))
            {
                old.ClearValue(AutomationProperties.LabeledByProperty);
            }
            if (_appliedName is not null && AutomationProperties.GetName(old) == _appliedName)
            {
                old.ClearValue(AutomationProperties.NameProperty);
            }
            _appliedName = null;
        }
        _automationTarget = target;
        if (target is null)
        {
            return;
        }
        if (LabelElement is { } label)
        {
            AutomationProperties.SetLabeledBy(target, label);
        }
        var name = AutomationProperties.GetName(target);
        if (string.IsNullOrEmpty(name) || name == _appliedName)
        {
            _appliedName = Label;
            AutomationProperties.SetName(target, Label ?? string.Empty);
        }
        FormFieldHost.SetItemHelpText(target, FieldText.HelpText(Description, EffectiveErrorMessage));
        AutomationProperties.SetIsRequiredForForm(target, IsRequired);
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ErrorMessageProperty || property == IsValidationEnabledProperty || property == IsEffectivelyEnabledProperty ||
            property == IsVisibleProperty)
        {
            Refresh();
        }
        else if (property == IsValidatingProperty)
        {
            UpdateStatus();
            Refresh();
        }
        else if (property == Form.IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, change.GetNewValue<bool>());
            UpdateMarks();
            Refresh();
        }
        else if (property == Form.ErrorTriggerProperty)
        {
            if (change.GetNewValue<FormErrorTrigger>() == FormErrorTrigger.Change && !_openOnNextChange)
            {
                _shown = true;
            }
            Refresh();
        }
        else if (property == Form.ErrorPlacementProperty || property == ErrorTemplateProperty)
        {
            UpdateErrorPresentation();
        }
        else if (property == Form.LabelPlacementProperty || property == Form.LabelWidthProperty)
        {
            UpdateLabelLayout();
        }
        else if (property == LabelProperty || property == IsLabelVisibleProperty)
        {
            UpdateLabelRowVisibility();
            UpdateMarks();
        }
        else if (property == IsRequiredProperty)
        {
            PseudoClasses.Set(RequiredPseudoClass, IsRequired);
            UpdateMarks();
            UpdateContentAutomation();
        }
        else if (property == Form.RequiredIndicatorProperty)
        {
            UpdateMarks();
        }
        else if (property == TargetProperty || property == ContentProperty)
        {
            UpdateContentAutomation();
            if (property == ContentProperty)
            {
                Collect();
            }
        }
    }
}
