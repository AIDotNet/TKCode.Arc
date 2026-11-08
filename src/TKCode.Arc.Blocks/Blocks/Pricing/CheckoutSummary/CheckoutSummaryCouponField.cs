using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The block's small buttons: Apply (class <c>apply</c>) and the chip's remove button (class <c>remove</c>).</summary>
internal sealed class CheckoutSummaryButton : AvaloniaButton
{
    public const string BusyPseudoClass = ":busy";

    protected override Type StyleKeyOverride => typeof(CheckoutSummaryButton);

    /// <summary>Sets <see cref="BusyPseudoClass"/>.</summary>
    public void SetBusy(bool busy) => PseudoClasses.Set(BusyPseudoClass, busy);
}

/// <summary>The promo code text box: no frame (the form draws it), placeholder, single line.</summary>
internal sealed class CheckoutSummaryCouponInput : TextBox
{
    public const string HasTextPseudoClass = ":has-text";

    protected override Type StyleKeyOverride => typeof(CheckoutSummaryCouponInput);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            PseudoClasses.Set(HasTextPseudoClass, !string.IsNullOrEmpty(Text));
        }
    }
}

/// <summary>
/// The promo code area of <see cref="CheckoutSummary"/> (a candidate for a shared Pricing helper): a form with a code input
/// and Apply that turns into an "applied" chip with a remove button. Applying a non-blank code spins Apply while
/// <see cref="Validator"/> runs; a refusal shakes the area, opens an error line and marks the input invalid (typing
/// clears it); an acceptance fades the form out while the chip grows in, and focus moves to the chip's remove button.
/// Removing fades the chip out, brings an empty form back and focuses the input. <see cref="IsLocked"/> disables all of it.
/// </summary>
[TemplatePart(ShakePart, typeof(Control))]
[TemplatePart(FormPart, typeof(Border))]
[TemplatePart(InputPart, typeof(CheckoutSummaryCouponInput))]
[TemplatePart(ApplyPart, typeof(CheckoutSummaryButton))]
[TemplatePart(ApplyTextPart, typeof(TextBlock))]
[TemplatePart(ApplySpinnerHostPart, typeof(Panel))]
[TemplatePart(ChipPart, typeof(Border))]
[TemplatePart(ChipCodePart, typeof(TextBlock))]
[TemplatePart(ChipAppliedPart, typeof(TextBlock))]
[TemplatePart(RemovePart, typeof(CheckoutSummaryButton))]
[TemplatePart(ErrorHostPart, typeof(CheckoutSummaryCollapse))]
[TemplatePart(ErrorPart, typeof(TextBlock))]
[PseudoClasses(InvalidPseudoClass, BusyPseudoClass, AppliedPseudoClass, LockedPseudoClass)]
internal sealed class CheckoutSummaryCouponField : TemplatedControl
{
    public const string ShakePart = "PART_Shake";
    public const string FormPart = "PART_Form";
    public const string InputPart = "PART_Input";
    public const string ApplyPart = "PART_Apply";
    public const string ApplyTextPart = "PART_ApplyText";
    public const string ApplySpinnerHostPart = "PART_ApplySpinnerHost";
    public const string ChipPart = "PART_Chip";
    public const string ChipCodePart = "PART_ChipCode";
    public const string ChipAppliedPart = "PART_ChipApplied";
    public const string RemovePart = "PART_Remove";
    public const string ErrorHostPart = "PART_ErrorHost";
    public const string ErrorPart = "PART_Error";

    public const string InvalidPseudoClass = ":invalid";
    public const string BusyPseudoClass = ":busy";
    public const string AppliedPseudoClass = ":applied";
    public const string LockedPseudoClass = ":locked";

    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<CheckoutSummaryCouponField, bool>(nameof(IsLocked));

    public static readonly StyledProperty<bool> UppercaseProperty =
        AvaloniaProperty.Register<CheckoutSummaryCouponField, bool>(nameof(Uppercase), true);

    public static readonly StyledProperty<CheckoutSummaryLabels> LabelsProperty =
        AvaloniaProperty.Register<CheckoutSummaryCouponField, CheckoutSummaryLabels>(nameof(Labels), new CheckoutSummaryLabels());

    public static readonly StyledProperty<double> ShakeAmplitudeProperty =
        AvaloniaProperty.Register<CheckoutSummaryCouponField, double>(nameof(ShakeAmplitude), 6);

    public static readonly StyledProperty<double> ChipEnterScaleProperty =
        AvaloniaProperty.Register<CheckoutSummaryCouponField, double>(nameof(ChipEnterScale), 0.97);

    public static readonly StyledProperty<double> FormEnterScaleProperty =
        AvaloniaProperty.Register<CheckoutSummaryCouponField, double>(nameof(FormEnterScale), 0.98);

    private readonly TranslateTransform _shakeMove = new();
    private readonly ScaleTransform _formScale = new();
    private readonly ScaleTransform _chipScale = new();
    private Control? _shake;
    private Border? _form;
    private CheckoutSummaryCouponInput? _input;
    private CheckoutSummaryButton? _apply;
    private TextBlock? _applyText;
    private Panel? _spinnerHost;
    private Border? _chip;
    private TextBlock? _chipCode;
    private TextBlock? _chipApplied;
    private CheckoutSummaryButton? _remove;
    private CheckoutSummaryCollapse? _errorHost;
    private TextBlock? _error;
    private MotionValue<double>? _shakeProgress;
    private MotionValue<double>? _formOpacity;
    private MotionValue<double>? _formScaleValue;
    private MotionValue<double>? _chipOpacity;
    private MotionValue<double>? _chipScaleValue;
    private CancellationTokenSource? _check;
    private CheckoutSummaryCouponResult? _applied;
    private string? _errorMessage;
    private bool _busy;
    private bool _keyboard;
    private bool _uppercasing;
    private int _swapVersion;

    /// <summary>Checks a code (already trimmed); null leaves Apply doing nothing.</summary>
    public Func<string, CancellationToken, Task<CheckoutSummaryCouponResult>>? Validator { get; set; }

    /// <summary>True disables the input, Apply and the remove button (while paying and after).</summary>
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    /// <summary>True turns typed letters into capitals as they are typed.</summary>
    public bool Uppercase { get => GetValue(UppercaseProperty); set => SetValue(UppercaseProperty, value); }

    /// <summary>Text of the field (placeholder, Apply, chip, error fallback).</summary>
    public CheckoutSummaryLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Travel of the refusal shake in px.</summary>
    public double ShakeAmplitude { get => GetValue(ShakeAmplitudeProperty); set => SetValue(ShakeAmplitudeProperty, value); }

    /// <summary>Scale the chip grows from.</summary>
    public double ChipEnterScale { get => GetValue(ChipEnterScaleProperty); set => SetValue(ChipEnterScaleProperty, value); }

    /// <summary>Scale the form grows from when it comes back.</summary>
    public double FormEnterScale { get => GetValue(FormEnterScaleProperty); set => SetValue(FormEnterScaleProperty, value); }

    /// <summary>The applied code, or null.</summary>
    public CheckoutSummaryCouponResult? Applied => _applied;

    /// <summary>True while a code is being checked.</summary>
    public bool IsBusy => _busy;

    /// <summary>The refusal shown under the field, or null.</summary>
    public string? ErrorMessage => _errorMessage;

    /// <summary>The text in the input.</summary>
    public string Text => _input?.Text ?? string.Empty;

    /// <summary>Raised when a code was accepted (the chip is showing).</summary>
    public event Action<string, CheckoutSummaryCouponResult>? CouponApplied;

    /// <summary>Raised when a code was refused: (typed code, result).</summary>
    public event Action<string, CheckoutSummaryCouponResult>? CouponRejected;

    /// <summary>Raised when the applied code was removed.</summary>
    public event Action<string>? CouponRemoved;

    /// <summary>Checks <paramref name="code"/> as if typed and applied; returns true when it was accepted.</summary>
    public async Task<bool> ApplyAsync(string code)
    {
        var trimmed = code.Trim();
        if (trimmed.Length == 0 || _busy || IsLocked || _applied is not null || Validator is not { } validator)
        {
            return false;
        }
        if (_input is not null && _input.Text != code)
        {
            _input.Text = Uppercase ? code.ToUpperInvariant() : code;
        }
        _check?.Cancel();
        var cts = new CancellationTokenSource();
        _check = cts;
        SetBusy(true);
        CheckoutSummaryCouponResult result;
        try
        {
            result = await validator(trimmed, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            result = CheckoutSummaryCouponResult.Invalid(Labels.CouponFailed);
        }
        finally
        {
            if (ReferenceEquals(_check, cts))
            {
                _check = null;
                SetBusy(false);
            }
            cts.Dispose();
        }
        if (cts.IsCancellationRequested)
        {
            return false;
        }
        if (result.IsValid)
        {
            ShowApplied(result, animate: true, moveFocus: true);
            CouponApplied?.Invoke(trimmed, result);
            return true;
        }
        ShowError(string.IsNullOrWhiteSpace(result.Message) ? Labels.CouponFailed : result.Message);
        CouponRejected?.Invoke(trimmed, result);
        return false;
    }

    /// <summary>Removes the applied code: the chip fades out, an empty form comes back and (optionally) takes focus.</summary>
    public void Remove(bool focusInput = true)
    {
        if (_applied is not { } applied)
        {
            return;
        }
        _applied = null;
        if (_input is not null)
        {
            _input.Text = string.Empty;
        }
        PseudoClasses.Set(AppliedPseudoClass, false);
        SwapToForm(animate: true);
        if (focusInput)
        {
            _input?.Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        CouponRemoved?.Invoke(applied.Code ?? string.Empty);
    }

    /// <summary>Shows <paramref name="result"/> as applied without a check (or clears it when null), without motion.</summary>
    public void SetApplied(CheckoutSummaryCouponResult? result)
    {
        CancelCheck();
        ClearError(animate: false);
        if (result is { IsValid: true })
        {
            ShowApplied(result, animate: false, moveFocus: false);
        }
        else if (_applied is not null)
        {
            _applied = null;
            if (_input is not null)
            {
                _input.Text = string.Empty;
            }
            PseudoClasses.Set(AppliedPseudoClass, false);
            SwapToForm(animate: false);
        }
    }

    /// <summary>Stops a running check without an answer.</summary>
    public void CancelCheck()
    {
        if (_check is { } cts)
        {
            _check = null;
            cts.Cancel();
            SetBusy(false);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.TextChanged -= OnInputTextChanged;
            _input.KeyDown -= OnInputKeyDown;
        }
        if (_apply is not null)
        {
            _apply.Click -= OnApplyClick;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        _shake = e.NameScope.Find<Control>(ShakePart);
        _form = e.NameScope.Find<Border>(FormPart);
        _input = e.NameScope.Find<CheckoutSummaryCouponInput>(InputPart);
        _apply = e.NameScope.Find<CheckoutSummaryButton>(ApplyPart);
        _applyText = e.NameScope.Find<TextBlock>(ApplyTextPart);
        _spinnerHost = e.NameScope.Find<Panel>(ApplySpinnerHostPart);
        _chip = e.NameScope.Find<Border>(ChipPart);
        _chipCode = e.NameScope.Find<TextBlock>(ChipCodePart);
        _chipApplied = e.NameScope.Find<TextBlock>(ChipAppliedPart);
        _remove = e.NameScope.Find<CheckoutSummaryButton>(RemovePart);
        _errorHost = e.NameScope.Find<CheckoutSummaryCollapse>(ErrorHostPart);
        _error = e.NameScope.Find<TextBlock>(ErrorPart);
        if (_shake is not null)
        {
            _shake.RenderTransform = _shakeMove;
        }
        if (_form is not null)
        {
            _form.RenderTransform = _formScale;
            _form.IsVisible = _applied is null;
        }
        if (_chip is not null)
        {
            _chip.RenderTransform = _chipScale;
            _chip.IsVisible = _applied is not null;
        }
        if (_input is not null)
        {
            _input.TextChanged += OnInputTextChanged;
            _input.KeyDown += OnInputKeyDown;
        }
        if (_apply is not null)
        {
            _apply.Click += OnApplyClick;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        if (_errorHost is not null)
        {
            _errorHost.FadeWithSpring = true;
            _errorHost.SetOpen(_errorMessage is not null, animate: false);
        }
        UpdateTexts();
        UpdateLocked();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelsProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == IsLockedProperty)
        {
            PseudoClasses.Set(LockedPseudoClass, change.GetNewValue<bool>());
            UpdateLocked();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelCheck();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _keyboard = true;
        base.OnKeyDown(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _keyboard = false;
        base.OnPointerPressed(e);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        _keyboard = true;
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = ApplyAsync(_input?.Text ?? string.Empty);
        }
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => _ = ApplyAsync(_input?.Text ?? string.Empty);

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => Remove();

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_input is null || _uppercasing)
        {
            return;
        }
        var text = _input.Text ?? string.Empty;
        if (Uppercase)
        {
            var upper = text.ToUpperInvariant();
            if (!string.Equals(upper, text, StringComparison.Ordinal))
            {
                var caret = _input.CaretIndex;
                _uppercasing = true;
                _input.Text = upper;
                _input.CaretIndex = Math.Min(caret, upper.Length);
                _uppercasing = false;
            }
        }
        if (_errorMessage is not null)
        {
            ClearError(animate: true);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        _apply?.SetBusy(busy);
        if (_spinnerHost is not null)
        {
            if (busy && _spinnerHost.Children.Count == 0)
            {
                var spinner = new CheckoutSummarySpinner(16);
                AutomationProperties.SetName(spinner, Labels.CheckingCode);
                _spinnerHost.Children.Add(spinner);
            }
            else if (!busy)
            {
                _spinnerHost.Children.Clear();
            }
        }
        if (_applyText is not null)
        {
            _applyText.Opacity = busy ? 0 : 1;
        }
        if (_apply is not null)
        {
            AutomationProperties.SetName(_apply, busy ? Labels.CheckingCode : Labels.Apply);
        }
        UpdateLocked();
    }

    private void ShowError(string message)
    {
        _errorMessage = message;
        PseudoClasses.Set(InvalidPseudoClass, true);
        if (_error is not null)
        {
            _error.Text = message;
        }
        if (_input is not null)
        {
            AutomationProperties.SetHelpText(_input, message);
        }
        _errorHost?.SetOpen(true);
        Shake();
        if (_input is not null && !_input.IsKeyboardFocusWithin)
        {
            _input.Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void ClearError(bool animate)
    {
        if (_errorMessage is null)
        {
            return;
        }
        _errorMessage = null;
        PseudoClasses.Set(InvalidPseudoClass, false);
        if (_input is not null)
        {
            AutomationProperties.SetHelpText(_input, null);
        }
        _errorHost?.SetOpen(false, animate);
    }

    private void Shake()
    {
        if (_shake is null || !IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        // A one-off keyframe run triggered by the check's answer, which a ControlTheme state cannot express.
        _shakeProgress ??= CreateValue(0, t => _shakeMove.X = CheckoutSummaryMotion.ShakeAt(t, ShakeAmplitude));
        _shakeProgress.Jump(0);
        _ = _shakeProgress.AnimateTo(1, MotionSpec.Tween(CheckoutSummaryMotion.ShakeDuration, new Avalonia.Animation.Easings.LinearEasing()));
    }

    private void ShowApplied(CheckoutSummaryCouponResult result, bool animate, bool moveFocus)
    {
        _applied = result;
        ClearError(animate);
        PseudoClasses.Set(AppliedPseudoClass, true);
        UpdateTexts();
        var hadFocus = IsKeyboardFocusWithin || _apply?.IsFocused == true || FocusManagerFocusedIsNull();
        SwapToChip(animate);
        if (moveFocus && hadFocus && _remove is not null)
        {
            // The reference drops focus to the page when the form unmounts; the chip's remove button is the useful place.
            _remove.Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private bool FocusManagerFocusedIsNull()
        => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is null or TopLevel;

    private void SwapToChip(bool animate)
    {
        if (_form is null || _chip is null)
        {
            return;
        }
        var version = ++_swapVersion;
        var level = ArcMotion.GetEffectiveLevel(this);
        _chip.IsVisible = true;
        UpdateLocked();
        if (!animate || !IsLoaded || level == ArcMotionLevel.Off)
        {
            JumpForm(0, 1);
            JumpChip(1, 1);
            _form.IsVisible = false;
            return;
        }
        var full = level == ArcMotionLevel.Full;
        EnsureValues(formFrom: 1, chipFrom: 0);
        // Form: 160 ms ease-out fade, no travel (kept under reduced motion).
        _ = HideWhenDone(_form, _formOpacity!.AnimateTo(0, MotionSpec.Tween(CheckoutSummaryMotion.Fast, CheckoutSummaryMotion.EaseOut)), version);
        if (full)
        {
            // Chip: opacity and scale (0.97 → 1) on the smooth spring.
            if (!_chipOpacity!.IsAnimating && _chipOpacity.Value <= 0.001)
            {
                _chipScaleValue!.Jump(ChipEnterScale);
            }
            _ = _chipOpacity.AnimateTo(1, MotionSpec.Spring(CheckoutSummaryMotion.Smooth));
            _ = _chipScaleValue!.AnimateTo(1, MotionSpec.Spring(CheckoutSummaryMotion.Smooth));
        }
        else
        {
            JumpChip(1, 1);
        }
    }

    private void SwapToForm(bool animate)
    {
        if (_form is null || _chip is null)
        {
            return;
        }
        var version = ++_swapVersion;
        var level = ArcMotion.GetEffectiveLevel(this);
        _form.IsVisible = true;
        UpdateLocked();
        if (!animate || !IsLoaded || level == ArcMotionLevel.Off)
        {
            JumpChip(0, 1);
            JumpForm(1, 1);
            _chip.IsVisible = false;
            return;
        }
        var full = level == ArcMotionLevel.Full;
        EnsureValues(formFrom: 0, chipFrom: 1);
        _ = HideWhenDone(_chip, _chipOpacity!.AnimateTo(0, MotionSpec.Tween(CheckoutSummaryMotion.Fast, CheckoutSummaryMotion.EaseOut)), version);
        if (!_formOpacity!.IsAnimating && _formOpacity.Value <= 0.001)
        {
            _formScaleValue!.Jump(full ? FormEnterScale : 1);
        }
        var enter = MotionSpec.Tween(CheckoutSummaryMotion.Standard, ArcEasings.Enter);
        _ = _formOpacity.AnimateTo(1, enter);
        _ = _formScaleValue!.AnimateTo(1, full ? enter : MotionSpec.Instant);
    }

    private async Task HideWhenDone(Control target, Task<bool> fade, int version)
    {
        await fade;
        if (version == _swapVersion)
        {
            target.IsVisible = false;
        }
    }

    private void EnsureValues(double formFrom, double chipFrom)
    {
        _formOpacity ??= CreateValue(formFrom, v => SetOpacity(_form, v));
        _formScaleValue ??= CreateValue(1, v => _formScale.ScaleX = _formScale.ScaleY = v);
        _chipOpacity ??= CreateValue(chipFrom, v => SetOpacity(_chip, v));
        _chipScaleValue ??= CreateValue(1, v => _chipScale.ScaleX = _chipScale.ScaleY = v);
    }

    private void JumpForm(double opacity, double scale)
    {
        _formOpacity?.Jump(opacity);
        _formScaleValue?.Jump(scale);
        SetOpacity(_form, opacity);
        _formScale.ScaleX = _formScale.ScaleY = scale;
    }

    private void JumpChip(double opacity, double scale)
    {
        _chipOpacity?.Jump(opacity);
        _chipScaleValue?.Jump(scale);
        SetOpacity(_chip, opacity);
        _chipScale.ScaleX = _chipScale.ScaleY = scale;
    }

    private static void SetOpacity(Control? control, double value)
    {
        if (control is not null)
        {
            control.Opacity = Math.Clamp(value, 0, 1);
        }
    }

    private MotionValue<double> CreateValue(double initial, Action<double> changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_input is not null)
        {
            _input.PlaceholderText = labels.CouponPlaceholder;
            AutomationProperties.SetName(_input, labels.CouponFieldName);
        }
        if (_applyText is not null)
        {
            _applyText.Text = labels.Apply;
        }
        if (_apply is not null)
        {
            AutomationProperties.SetName(_apply, _busy ? labels.CheckingCode : labels.Apply);
        }
        if (_chipApplied is not null)
        {
            _chipApplied.Text = labels.Applied;
        }
        var code = _applied?.Code ?? string.Empty;
        if (_chipCode is not null)
        {
            _chipCode.Text = code;
        }
        if (_remove is not null)
        {
            AutomationProperties.SetName(_remove, labels.RemoveCode(code));
        }
    }

    private void UpdateLocked()
    {
        var locked = IsLocked;
        if (_input is not null)
        {
            _input.IsEnabled = !locked && _applied is null;
        }
        if (_apply is not null)
        {
            _apply.IsEnabled = !locked && !_busy && _applied is null;
        }
        if (_remove is not null)
        {
            _remove.IsEnabled = !locked && _applied is not null;
        }
    }
}
