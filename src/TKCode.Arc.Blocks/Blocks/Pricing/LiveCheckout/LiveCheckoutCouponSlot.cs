using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the coupon slot shows.</summary>
internal enum LiveCheckoutCouponState
{
    /// <summary>The "Add coupon" button.</summary>
    Idle,

    /// <summary>The code field and Apply.</summary>
    Editing,

    /// <summary>The applied code as a chip with a remove button.</summary>
    Applied
}

/// <summary>
/// The summary's coupon slot (36 high): <c>Add coupon</c> → the code field with Apply → a chip with the code, its discount
/// and a remove button. As measured: the button fades out in ~170 ms while the field fades in and drops 4px on the ω 20
/// spring; the chip pops in (opacity, scale .92 → 1, blur 4 → 0) on the smooth spring and leaves the same way while the
/// button fades back in 120 ms; a refused code opens an error row under the field. Focus follows (our fix of the
/// reference, which drops it): into the field on open, to the chip's remove button when applied, back to the button on
/// Escape or removal. The block runs the check; the slot only reports <see cref="ApplyRequested"/> and
/// <see cref="RemoveRequested"/>.
/// </summary>
[TemplatePart(AddPart, typeof(LiveCheckoutButton))]
[TemplatePart(FormPart, typeof(Control))]
[TemplatePart(CodePart, typeof(Input))]
[TemplatePart(ApplyPart, typeof(LiveCheckoutButton))]
[TemplatePart(ChipPart, typeof(Control))]
[TemplatePart(RemovePart, typeof(LiveCheckoutButton))]
[TemplatePart(ErrorRowPart, typeof(LiveCheckoutCollapse))]
internal sealed class LiveCheckoutCouponSlot : TemplatedControl
{
    public const string AddPart = "PART_Add";
    public const string FormPart = "PART_Form";
    public const string CodePart = "PART_Code";
    public const string ApplyPart = "PART_Apply";
    public const string ChipPart = "PART_Chip";
    public const string RemovePart = "PART_Remove";
    public const string ErrorRowPart = "PART_ErrorRow";

    public static readonly StyledProperty<string?> AddLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(AddLabel));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string?> FieldLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(FieldLabel));

    public static readonly StyledProperty<string?> ApplyLabelProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(ApplyLabel));

    public static readonly StyledProperty<string?> ChipCodeProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(ChipCode));

    public static readonly StyledProperty<string?> ChipOffProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(ChipOff));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, string?>(nameof(ErrorText));

    public static readonly StyledProperty<bool> IsCheckingProperty =
        AvaloniaProperty.Register<LiveCheckoutCouponSlot, bool>(nameof(IsChecking));

    private LiveCheckoutButton? _add;
    private Control? _form;
    private Input? _code;
    private LiveCheckoutButton? _apply;
    private Control? _chip;
    private LiveCheckoutButton? _remove;
    private LiveCheckoutCollapse? _errorRow;
    private LiveCheckoutPresence? _addPresence;
    private LiveCheckoutPresence? _formPresence;
    private LiveCheckoutPresence? _chipPresence;
    private bool _uppercasing;

    public string? AddLabel { get => GetValue(AddLabelProperty); set => SetValue(AddLabelProperty, value); }
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string? FieldLabel { get => GetValue(FieldLabelProperty); set => SetValue(FieldLabelProperty, value); }
    public string? ApplyLabel { get => GetValue(ApplyLabelProperty); set => SetValue(ApplyLabelProperty, value); }
    public string? ChipCode { get => GetValue(ChipCodeProperty); set => SetValue(ChipCodeProperty, value); }
    public string? ChipOff { get => GetValue(ChipOffProperty); set => SetValue(ChipOffProperty, value); }
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public bool IsChecking { get => GetValue(IsCheckingProperty); set => SetValue(IsCheckingProperty, value); }

    /// <summary>What the slot shows.</summary>
    public LiveCheckoutCouponState State { get; private set; }

    /// <summary>Accessible name pattern of the remove button.</summary>
    public Func<string, string>? RemoveLabel { get; set; }

    /// <summary>How focus moves when the slot moves it (keyboard users get a ring, pointer users do not).</summary>
    public Func<NavigationMethod>? FocusMethod { get; set; }

    /// <summary>Raised with the typed code when Apply or Enter is pressed.</summary>
    public event EventHandler<string>? ApplyRequested;

    /// <summary>Raised when the chip's remove button is pressed.</summary>
    public event EventHandler? RemoveRequested;

    internal Input? CodeField => _code;
    internal LiveCheckoutButton? AddButton => _add;
    internal LiveCheckoutButton? ApplyButton => _apply;
    internal LiveCheckoutButton? RemoveButton => _remove;
    internal LiveCheckoutCollapse? ErrorRow => _errorRow;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _add = e.NameScope.Find<LiveCheckoutButton>(AddPart);
        _form = e.NameScope.Find<Control>(FormPart);
        _code = e.NameScope.Find<Input>(CodePart);
        _apply = e.NameScope.Find<LiveCheckoutButton>(ApplyPart);
        _chip = e.NameScope.Find<Control>(ChipPart);
        _remove = e.NameScope.Find<LiveCheckoutButton>(RemovePart);
        _errorRow = e.NameScope.Find<LiveCheckoutCollapse>(ErrorRowPart);
        if (_add is not null)
        {
            _add.Click += OnAdd;
            _addPresence = new LiveCheckoutPresence(_add, visible: State == LiveCheckoutCouponState.Idle);
        }
        if (_form is not null)
        {
            _formPresence = new LiveCheckoutPresence(_form, new Vector(0, -4), visible: State == LiveCheckoutCouponState.Editing);
        }
        if (_chip is not null)
        {
            _chipPresence = new LiveCheckoutPresence(_chip, scale: 0.92, blur: 4, visible: State == LiveCheckoutCouponState.Applied);
        }
        if (_apply is not null)
        {
            _apply.Click += OnApply;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemove;
        }
        if (_code is not null)
        {
            _code.KeyDown += OnCodeKeyDown;
            _code.TextChanged += OnCodeChanged;
        }
        _errorRow?.SetOpen(false, false);
        SyncApply();
    }

    private void Detach()
    {
        if (_add is not null)
        {
            _add.Click -= OnAdd;
        }
        if (_apply is not null)
        {
            _apply.Click -= OnApply;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemove;
        }
        if (_code is not null)
        {
            _code.KeyDown -= OnCodeKeyDown;
            _code.TextChanged -= OnCodeChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckingProperty)
        {
            SyncApply();
        }
        else if (change.Property == ChipCodeProperty && _remove is not null && ChipCode is { } code)
        {
            AutomationProperties.SetName(_remove, RemoveLabel?.Invoke(code) ?? code);
        }
    }

    private void SyncApply()
    {
        if (_apply is not null)
        {
            _apply.IsBusy = IsChecking;
            _apply.IsEnabled = IsChecking || !string.IsNullOrWhiteSpace(_code?.Text);
        }
        if (_code is not null)
        {
            _code.IsReadOnly = IsChecking;
        }
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => Open(true);

    private void OnApply(object? sender, RoutedEventArgs e) => RequestApply();

    private void OnRemove(object? sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this, EventArgs.Empty);

    private void OnCodeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RequestApply();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && !IsChecking)
        {
            Close(true, focusButton: true);
            e.Handled = true;
        }
    }

    private void OnCodeChanged(object? sender, TextChangedEventArgs e)
    {
        if (_code is null || _uppercasing)
        {
            return;
        }
        // The value itself becomes upper case (not just its display), as measured.
        var text = _code.Text ?? string.Empty;
        var upper = text.ToUpperInvariant();
        if (upper != text)
        {
            _uppercasing = true;
            var caret = _code.CaretIndex;
            _code.Text = upper;
            _code.CaretIndex = caret;
            _uppercasing = false;
        }
        SyncApply();
    }

    private void RequestApply()
    {
        var code = _code?.Text?.Trim() ?? string.Empty;
        if (IsChecking || code.Length == 0)
        {
            return;
        }
        ApplyRequested?.Invoke(this, code);
    }

    /// <summary>Opens the code field (focus goes into it).</summary>
    public void Open(bool animate)
    {
        if (State == LiveCheckoutCouponState.Editing)
        {
            return;
        }
        State = LiveCheckoutCouponState.Editing;
        var motion = animate && LiveCheckoutMotion.CanAnimate(this);
        _addPresence?.Hide(motion, MotionSpec.Tween(LiveCheckoutMotion.QuickFade, LiveCheckoutMotion.Standard));
        _chipPresence?.Jump(false);
        _formPresence?.Show(motion, MotionSpec.Spring(LiveCheckoutMotion.Roll));
        if (_code is not null)
        {
            _code.Text = string.Empty;
            _code.Focus(NavigationMethod.Pointer);
        }
        SetError(null, false);
        SyncApply();
    }

    /// <summary>Back to the "Add coupon" button.</summary>
    public void Close(bool animate, bool focusButton)
    {
        var wasChip = State == LiveCheckoutCouponState.Applied;
        State = LiveCheckoutCouponState.Idle;
        var motion = animate && LiveCheckoutMotion.CanAnimate(this);
        if (wasChip)
        {
            _chipPresence?.Hide(motion, MotionSpec.Spring(LiveCheckoutMotion.Smooth));
        }
        _formPresence?.Hide(motion, MotionSpec.Tween(LiveCheckoutMotion.ReturnFade, LiveCheckoutMotion.Standard));
        _addPresence?.Show(motion, MotionSpec.Tween(LiveCheckoutMotion.ReturnFade, LiveCheckoutMotion.Standard));
        SetError(null, motion);
        IsChecking = false;
        if (focusButton)
        {
            _add?.Focus(FocusMethod?.Invoke() ?? NavigationMethod.Tab);
        }
    }

    /// <summary>Shows the applied code as a chip (focus goes to its remove button).</summary>
    public void ShowApplied(string code, string off, bool animate, bool focus)
    {
        State = LiveCheckoutCouponState.Applied;
        ChipCode = code;
        ChipOff = off;
        var motion = animate && LiveCheckoutMotion.CanAnimate(this);
        _formPresence?.Hide(motion, MotionSpec.Tween(LiveCheckoutMotion.ReturnFade, LiveCheckoutMotion.Standard));
        _addPresence?.Jump(false);
        _chipPresence?.Show(motion, MotionSpec.Spring(LiveCheckoutMotion.Smooth));
        SetError(null, motion);
        if (focus)
        {
            _remove?.Focus(FocusMethod?.Invoke() ?? NavigationMethod.Tab);
        }
    }

    /// <summary>Shows or clears the error row under the field.</summary>
    public void SetError(string? message, bool animate)
    {
        if (!string.IsNullOrEmpty(message))
        {
            ErrorText = message;
        }
        if (_code is not null)
        {
            _code.IsInvalid = !string.IsNullOrEmpty(message);
        }
        _errorRow?.SetOpen(!string.IsNullOrEmpty(message), animate);
    }

    /// <summary>Back to the button at once (a reset).</summary>
    public void Reset()
    {
        State = LiveCheckoutCouponState.Idle;
        IsChecking = false;
        _addPresence?.Jump(true);
        _formPresence?.Jump(false);
        _chipPresence?.Jump(false);
        if (_code is not null)
        {
            _code.Text = string.Empty;
            _code.IsInvalid = false;
        }
        _errorRow?.SetOpen(false, false);
    }
}
