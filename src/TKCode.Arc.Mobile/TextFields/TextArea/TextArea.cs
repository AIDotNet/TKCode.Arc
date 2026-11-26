using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Mobile.DesignTokens;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile multiline text area: a touch-first filled box with wrapping text, multiline soft-keyboard hints,
/// <c>MinLines</c> / <c>MaxLines</c> sizing (inherited from <see cref="TextBox"/>, defaulting to 3 minimum lines),
/// an optional character counter (<see cref="ShowCounter"/>) and the long-press <see cref="TextEditMenu"/>
/// (with native iOS 16+ menu support through <c>TKCode.Arc.Mobile.iOS</c>). There is no hover state; the border
/// takes the accent color while focused.
/// </summary>
[TemplatePart("PART_Clear", typeof(Avalonia.Controls.Button))]
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[PseudoClasses(":at-limit")]
public class TextArea : TextBox
{
    /// <summary>Whether a clear button shows in the top-right corner while focused and non-empty: false.</summary>
    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<TextArea, bool>(nameof(ShowClearButton));

    /// <summary>Whether to show a character count in the bottom-right corner (uses <c>MaxLength</c> when set): false.</summary>
    public static readonly StyledProperty<bool> ShowCounterProperty =
        AvaloniaProperty.Register<TextArea, bool>(nameof(ShowCounter));

    /// <summary>Whether the clear button is currently shown.</summary>
    public static readonly DirectProperty<TextArea, bool> IsClearVisibleProperty =
        AvaloniaProperty.RegisterDirect<TextArea, bool>(nameof(IsClearVisible), a => a.IsClearVisible);

    /// <summary>Whether the character counter is currently shown.</summary>
    public static readonly DirectProperty<TextArea, bool> IsCounterVisibleProperty =
        AvaloniaProperty.RegisterDirect<TextArea, bool>(nameof(IsCounterVisible), a => a.IsCounterVisible);

    /// <summary>Formatted counter text (for example "12 / 140" when <c>MaxLength</c> is set, or "12").</summary>
    public static readonly DirectProperty<TextArea, string?> CounterTextProperty =
        AvaloniaProperty.RegisterDirect<TextArea, string?>(nameof(CounterText), a => a.CounterText);

    private Avalonia.Controls.Button? _clear;
    private bool _isClearVisible;
    private bool _isCounterVisible;
    private string? _counterText;

    static TextArea()
    {
        AcceptsReturnProperty.OverrideDefaultValue<TextArea>(true);
        TextWrappingProperty.OverrideDefaultValue<TextArea>(TextWrapping.Wrap);
        MinLinesProperty.OverrideDefaultValue<TextArea>(3);
    }

    /// <summary>Creates a multiline mobile text area.</summary>
    public TextArea()
    {
        TextInputOptions.SetMultiline(this, true);
        ContextRequested += OnContextRequestedAtPress;
    }

    /// <inheritdoc cref="ShowClearButtonProperty"/>
    public bool ShowClearButton
    {
        get => GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    /// <inheritdoc cref="ShowCounterProperty"/>
    public bool ShowCounter
    {
        get => GetValue(ShowCounterProperty);
        set => SetValue(ShowCounterProperty, value);
    }

    /// <inheritdoc cref="IsClearVisibleProperty"/>
    public bool IsClearVisible => _isClearVisible;

    /// <inheritdoc cref="IsCounterVisibleProperty"/>
    public bool IsCounterVisible => _isCounterVisible;

    /// <inheritdoc cref="CounterTextProperty"/>
    public string? CounterText => _counterText;

    protected override Type StyleKeyOverride => typeof(TextArea);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        _clear = e.NameScope.Find<Avalonia.Controls.Button>("PART_Clear");
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        UpdateClearVisible();
        UpdateCounter();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            HideEditMenu();
            UpdateCounter();
        }
        if (change.Property == TextProperty || change.Property == IsFocusedProperty
            || change.Property == IsReadOnlyProperty || change.Property == ShowClearButtonProperty)
        {
            UpdateClearVisible();
        }
        if (change.Property == ShowCounterProperty || change.Property == MaxLengthProperty)
        {
            UpdateCounter();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        HideEditMenu();
    }

    private void OnContextRequestedAtPress(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || ContextMenu is not null || ContextFlyout is not TextEditMenu menu || !e.TryGetPosition(this, out var point))
        {
            return;
        }
        e.Handled = menu.ShowAbove(this, point.X);
    }

    private void HideEditMenu()
    {
        if (ContextFlyout is TextEditMenu menu)
        {
            menu.HideFor(this);
        }
    }

    private void UpdateClearVisible()
        => SetAndRaise(IsClearVisibleProperty, ref _isClearVisible,
            ShowClearButton && !IsReadOnly && IsEnabled && IsFocused && !string.IsNullOrEmpty(Text));

    private void UpdateCounter()
    {
        var visible = ShowCounter;
        SetAndRaise(IsCounterVisibleProperty, ref _isCounterVisible, visible);
        var length = Text?.Length ?? 0;
        var max = MaxLength;
        var text = max > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{length} / {max}")
            : length.ToString(CultureInfo.InvariantCulture);
        SetAndRaise(CounterTextProperty, ref _counterText, visible ? text : null);
        PseudoClasses.Set(":at-limit", visible && max > 0 && length >= max);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Clear();
        Focus();
    }
}
