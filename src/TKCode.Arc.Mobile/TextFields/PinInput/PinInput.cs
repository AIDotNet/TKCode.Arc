using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Mobile.DesignTokens;

namespace TKCode.Arc.Mobile;

/// <summary>Event data of <see cref="PinInput.CodeChanged"/> and <see cref="PinInput.Completed"/>.</summary>
public sealed class PinCodeEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public PinCodeEventArgs(RoutedEvent routedEvent, string code)
        : base(routedEvent)
    {
        Code = code;
    }

    /// <summary>The current sanitized code string.</summary>
    public string Code { get; }
}

/// <summary>
/// Mobile OTP / PIN code input: a row of touch-sized character boxes backed by a single hidden <see cref="TextField"/>
/// so iOS and Android soft keyboards, SMS one-time-code auto-fill, full-code paste, IME and hardware keyboards work
/// seamlessly without jumping focus between boxes. Triggers <see cref="Haptics"/> cues on entry, completion and error.
/// </summary>
[TemplatePart("PART_Root", typeof(Control))]
[TemplatePart("PART_Input", typeof(TextField))]
[TemplatePart("PART_Cells", typeof(Grid))]
[PseudoClasses(":focused", ":error", ":complete")]
public class PinInput : TemplatedControl
{
    /// <summary>Current code string (two-way, filtered when <see cref="NumericOnly"/> is true and truncated to <see cref="Length"/>).</summary>
    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<PinInput, string>(nameof(Code), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, coerce: CoerceCode);

    /// <summary>Number of character cells (clamped to 1..12): 6.</summary>
    public static readonly StyledProperty<int> LengthProperty =
        AvaloniaProperty.Register<PinInput, int>(nameof(Length), 6, coerce: (_, v) => Math.Clamp(v, 1, 12));

    /// <summary>Whether to accept digits only and request a numeric soft keyboard: true.</summary>
    public static readonly StyledProperty<bool> NumericOnlyProperty =
        AvaloniaProperty.Register<PinInput, bool>(nameof(NumericOnly), true);

    /// <summary>Optional masking character (for example '•') shown instead of entered characters for PIN/passcode entry.</summary>
    public static readonly StyledProperty<char?> MaskCharProperty =
        AvaloniaProperty.Register<PinInput, char?>(nameof(MaskChar));

    /// <summary>Whether the input is in an invalid/error state (highlights cells with the danger color): false.</summary>
    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<PinInput, bool>(nameof(HasError));

    /// <summary>Optional placeholder character shown in empty cells (for example "○" or "-").</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<PinInput, string?>(nameof(Placeholder));

    /// <summary>Whether <see cref="Code"/> has reached <see cref="Length"/> characters.</summary>
    public static readonly DirectProperty<PinInput, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<PinInput, bool>(nameof(IsComplete), p => p.IsComplete);

    /// <summary>Raised whenever <see cref="Code"/> changes.</summary>
    public static readonly RoutedEvent<PinCodeEventArgs> CodeChangedEvent =
        RoutedEvent.Register<PinInput, PinCodeEventArgs>(nameof(CodeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Code"/> reaches <see cref="Length"/> characters.</summary>
    public static readonly RoutedEvent<PinCodeEventArgs> CompletedEvent =
        RoutedEvent.Register<PinInput, PinCodeEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    private Control? _root;
    private TextField? _input;
    private Grid? _cellsHost;
    private readonly List<CellView> _cells = [];
    private bool _syncingInput;
    private bool _isComplete;

    /// <summary>Creates a mobile PIN/OTP input.</summary>
    public PinInput()
    {
        ActualThemeVariantChanged += (_, _) => UpdateCellsVisual();
    }

    /// <inheritdoc cref="CodeProperty"/>
    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <inheritdoc cref="LengthProperty"/>
    public int Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <inheritdoc cref="NumericOnlyProperty"/>
    public bool NumericOnly
    {
        get => GetValue(NumericOnlyProperty);
        set => SetValue(NumericOnlyProperty, value);
    }

    /// <inheritdoc cref="MaskCharProperty"/>
    public char? MaskChar
    {
        get => GetValue(MaskCharProperty);
        set => SetValue(MaskCharProperty, value);
    }

    /// <inheritdoc cref="HasErrorProperty"/>
    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <inheritdoc cref="IsCompleteProperty"/>
    public bool IsComplete => _isComplete;

    /// <inheritdoc cref="CodeChangedEvent"/>
    public event EventHandler<PinCodeEventArgs>? CodeChanged
    {
        add => AddHandler(CodeChangedEvent, value);
        remove => RemoveHandler(CodeChangedEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<PinCodeEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(PinInput);

    /// <summary>Clears the entered code and focuses the input if enabled.</summary>
    public void Clear()
    {
        Code = string.Empty;
        if (IsEffectivelyEnabled)
        {
            FocusInput();
        }
    }

    /// <summary>Focuses the backing text input so soft and hardware keyboards direct keystrokes here.</summary>
    public void FocusInput()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        if (_input is not null)
        {
            _input.Focus();
            _input.CaretIndex = _input.Text?.Length ?? 0;
        }
        else
        {
            Focus();
        }
    }

    private static string CoerceCode(AvaloniaObject o, string? value)
    {
        if (o is not PinInput pin || string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return Sanitize(value, pin.Length, pin.NumericOnly);
    }

    private static string Sanitize(string raw, int length, bool numericOnly)
    {
        if (string.IsNullOrEmpty(raw) || length <= 0)
        {
            return string.Empty;
        }
        var sb = new StringBuilder(Math.Min(raw.Length, length));
        foreach (var ch in raw)
        {
            if (char.IsControl(ch) || char.IsWhiteSpace(ch))
            {
                continue;
            }
            if (numericOnly && !char.IsAsciiDigit(ch))
            {
                continue;
            }
            sb.Append(ch);
            if (sb.Length >= length)
            {
                break;
            }
        }
        return sb.ToString();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _root = e.NameScope.Find<Control>("PART_Root");
        _input = e.NameScope.Find<TextField>("PART_Input");
        _cellsHost = e.NameScope.Find<Grid>("PART_Cells");

        if (_root is not null)
        {
            _root.PointerPressed += OnRootPointerPressed;
        }
        if (_input is not null)
        {
            _input.TextChanged += OnInputTextChanged;
            _input.GotFocus += OnInputFocusChanged;
            _input.LostFocus += OnInputFocusChanged;
            ConfigureInputOptions();
            SyncInputText();
        }
        RebuildCells();
        UpdateCellsVisual();
    }

    private void DetachParts()
    {
        if (_root is not null)
        {
            _root.PointerPressed -= OnRootPointerPressed;
        }
        if (_input is not null)
        {
            _input.TextChanged -= OnInputTextChanged;
            _input.GotFocus -= OnInputFocusChanged;
            _input.LostFocus -= OnInputFocusChanged;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_input is not null && !_input.IsFocused && IsEffectivelyEnabled)
        {
            _input.Focus();
            _input.CaretIndex = _input.Text?.Length ?? 0;
        }
        UpdateFocusPseudoClass();
        UpdateCellsVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFocusPseudoClass();
        UpdateCellsVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty)
        {
            var oldCode = change.GetOldValue<string>() ?? string.Empty;
            var newCode = change.GetNewValue<string>() ?? string.Empty;
            SyncInputText();
            var complete = newCode.Length >= Length;
            SetAndRaise(IsCompleteProperty, ref _isComplete, complete);
            PseudoClasses.Set(":complete", complete);
            UpdateCellsVisual();
            UpdateAutomation();
            if (!string.Equals(oldCode, newCode, StringComparison.Ordinal))
            {
                RaiseEvent(new PinCodeEventArgs(CodeChangedEvent, newCode));
                if (newCode.Length > oldCode.Length)
                {
                    Haptics.Trigger(complete ? HapticKind.Success : HapticKind.Selection);
                }
                if (complete && oldCode.Length < Length)
                {
                    RaiseEvent(new PinCodeEventArgs(CompletedEvent, newCode));
                }
            }
        }
        else if (change.Property == LengthProperty)
        {
            CoerceValue(CodeProperty);
            ConfigureInputOptions();
            RebuildCells();
            var complete = Code.Length >= Length;
            SetAndRaise(IsCompleteProperty, ref _isComplete, complete);
            PseudoClasses.Set(":complete", complete);
            UpdateCellsVisual();
            UpdateAutomation();
        }
        else if (change.Property == NumericOnlyProperty)
        {
            CoerceValue(CodeProperty);
            ConfigureInputOptions();
            UpdateCellsVisual();
        }
        else if (change.Property == HasErrorProperty)
        {
            var hasError = HasError;
            PseudoClasses.Set(":error", hasError);
            if (hasError && change.GetOldValue<bool>() == false)
            {
                Haptics.Trigger(HapticKind.Error);
            }
            UpdateCellsVisual();
        }
        else if (change.Property == MaskCharProperty || change.Property == PlaceholderProperty
                 || change.Property == FontSizeProperty || change.Property == FontWeightProperty
                 || change.Property == ForegroundProperty)
        {
            UpdateCellsVisual();
            UpdateAutomation();
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        FocusInput();
        e.Handled = true;
    }

    private void OnInputFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        UpdateFocusPseudoClass();
        UpdateCellsVisual();
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingInput || _input is null)
        {
            return;
        }
        var raw = _input.Text ?? string.Empty;
        var clean = Sanitize(raw, Length, NumericOnly);
        if (!string.Equals(raw, clean, StringComparison.Ordinal))
        {
            _syncingInput = true;
            try
            {
                _input.Text = clean;
                _input.CaretIndex = clean.Length;
            }
            finally
            {
                _syncingInput = false;
            }
        }
        if (!string.Equals(Code, clean, StringComparison.Ordinal))
        {
            SetCurrentValue(CodeProperty, clean);
        }
    }

    private void ConfigureInputOptions()
    {
        if (_input is null)
        {
            return;
        }
        _input.MaxLength = Length;
        TextInputOptions.SetContentType(_input, NumericOnly ? TextInputContentType.Digits : TextInputContentType.Normal);
        TextInputOptions.SetReturnKeyType(_input, TextInputReturnKeyType.Done);
    }

    private void SyncInputText()
    {
        if (_input is null)
        {
            return;
        }
        var target = Code ?? string.Empty;
        if (string.Equals(_input.Text ?? string.Empty, target, StringComparison.Ordinal))
        {
            return;
        }
        _syncingInput = true;
        try
        {
            _input.Text = target;
            _input.CaretIndex = target.Length;
        }
        finally
        {
            _syncingInput = false;
        }
    }

    private void UpdateFocusPseudoClass()
    {
        var focused = IsFocused || (_input?.IsFocused ?? false);
        PseudoClasses.Set(":focused", focused);
    }

    private void RebuildCells()
    {
        if (_cellsHost is null)
        {
            return;
        }
        _cellsHost.Children.Clear();
        _cellsHost.ColumnDefinitions.Clear();
        _cells.Clear();

        var count = Length;
        var cellHeight = ResolveToken(PinInputTokenKind.CellHeight, 48);
        var cellMaxWidth = ResolveToken(PinInputTokenKind.CellMaxWidth, 52);
        var spacing = ResolveToken(PinInputTokenKind.CellSpacing, 8);
        var halfGap = spacing / 2.0;
        var radius = ResolveDesignResource<CornerRadius>(ArcDesignTokenKind.RadiusControl, new CornerRadius(10));

        for (var i = 0; i < count; i++)
        {
            _cellsHost.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var text = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var caret = new Border
            {
                Width = 2,
                Height = 20,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
            };
            var inner = new Panel();
            inner.Children.Add(text);
            inner.Children.Add(caret);

            var margin = new Thickness(
                i == 0 ? 0 : halfGap,
                0,
                i == count - 1 ? 0 : halfGap,
                0);
            var box = new Border
            {
                Height = cellHeight,
                MaxWidth = cellMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = radius,
                Margin = margin,
                Child = inner,
            };
            Grid.SetColumn(box, i);
            _cellsHost.Children.Add(box);
            _cells.Add(new CellView(box, text, caret));
        }
    }

    private void UpdateCellsVisual()
    {
        if (_cells.Count == 0)
        {
            return;
        }
        var code = Code ?? string.Empty;
        var focused = IsFocused || (_input?.IsFocused ?? false);
        var hasError = HasError;
        var activeIndex = focused ? Math.Min(code.Length, _cells.Count - 1) : -1;

        var surface = ResolveDesignBrush(ArcDesignTokenKind.SurfaceMuted);
        var surfaceFocused = ResolveDesignBrush(ArcDesignTokenKind.Surface);
        var borderDefault = ResolveDesignBrush(ArcDesignTokenKind.Border);
        var borderActive = ResolveDesignBrush(ArcDesignTokenKind.Accent);
        var borderDanger = ResolveDesignBrush(ArcDesignTokenKind.Danger);
        var textPrimary = Foreground ?? ResolveDesignBrush(ArcDesignTokenKind.Foreground);
        var textMuted = ResolveDesignBrush(ArcDesignTokenKind.TextMuted);
        var normalWidth = ResolveToken(PinInputTokenKind.BorderWidth, 1);
        var activeWidth = ResolveToken(PinInputTokenKind.ActiveBorderWidth, 2);

        for (var i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            var hasChar = i < code.Length;
            var isActive = i == activeIndex;

            if (hasChar)
            {
                cell.Text.Text = MaskChar.HasValue ? MaskChar.Value.ToString() : code[i].ToString();
                cell.Text.Foreground = textPrimary;
                cell.Caret.IsVisible = false;
            }
            else if (isActive)
            {
                cell.Text.Text = string.Empty;
                cell.Caret.Background = hasError ? borderDanger : borderActive;
                cell.Caret.IsVisible = true;
            }
            else
            {
                cell.Text.Text = Placeholder ?? string.Empty;
                cell.Text.Foreground = textMuted;
                cell.Caret.IsVisible = false;
            }

            cell.Text.FontSize = FontSize;
            cell.Text.FontWeight = FontWeight;
            cell.Box.Background = isActive || hasChar ? surfaceFocused : surface;
            if (hasError)
            {
                cell.Box.BorderBrush = borderDanger;
                cell.Box.BorderThickness = new Thickness(isActive ? activeWidth : normalWidth);
            }
            else if (isActive)
            {
                cell.Box.BorderBrush = borderActive;
                cell.Box.BorderThickness = new Thickness(activeWidth);
            }
            else
            {
                cell.Box.BorderBrush = borderDefault;
                cell.Box.BorderThickness = new Thickness(normalWidth);
            }
        }
    }

    private void UpdateAutomation()
    {
        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)))
        {
            var count = Code?.Length ?? 0;
            AutomationProperties.SetHelpText(this, string.Create(CultureInfo.InvariantCulture, $"{count} of {Length} characters entered"));
        }
    }

    private double ResolveToken(PinInputTokenKind kind, double fallback)
        => this.TryFindResource(PinInputTokens.Key(kind), out var v) && v is double d && d >= 0 ? d : fallback;

    private T ResolveDesignResource<T>(ArcDesignTokenKind kind, T fallback)
        => this.TryFindResource(ArcDesignTokens.Key(kind), out var v) && v is T t ? t : fallback;

    private IBrush? ResolveDesignBrush(ArcDesignTokenKind kind)
    {
        if (this.TryFindResource(ArcDesignTokens.Key(kind), out var v))
        {
            if (v is IBrush brush)
            {
                return brush;
            }
            if (v is Color color)
            {
                return new SolidColorBrush(color);
            }
        }
        return null;
    }

    private sealed record CellView(Border Box, TextBlock Text, Border Caret);
}
