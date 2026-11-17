using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile text field: a 44px filled field with an optional <see cref="Icon"/> before the text and a clear button that
/// shows while the field has focus and text. The border takes the accent color while focused; there is no hover state.
/// It derives from Avalonia's <see cref="TextBox"/>, so text, selection and binding work as they do there. Show a
/// placeholder with <c>PlaceholderText</c>. A long press opens a <see cref="TextEditMenu"/> with Cut, Copy, Paste and
/// Select All (its theme sets it as <c>ContextFlyout</c>; set another flyout or <c>ContextMenu</c> to replace it).
/// </summary>
[TemplatePart("PART_Clear", typeof(Avalonia.Controls.Button))]
public class TextField : TextBox
{
    /// <summary>The glyph geometry shown before the text, for example a Lucide icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<TextField, Geometry?>(nameof(Icon));

    /// <summary>Whether a clear button shows while the field has focus and text: true.</summary>
    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<TextField, bool>(nameof(ShowClearButton), true);

    /// <summary>Whether the clear button is showing now.</summary>
    public static readonly DirectProperty<TextField, bool> IsClearVisibleProperty =
        AvaloniaProperty.RegisterDirect<TextField, bool>(nameof(IsClearVisible), f => f.IsClearVisible);

    private Avalonia.Controls.Button? _clear;
    private bool _isClearVisible;

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="ShowClearButtonProperty"/>
    public bool ShowClearButton
    {
        get => GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    /// <inheritdoc cref="IsClearVisibleProperty"/>
    public bool IsClearVisible => _isClearVisible;

    protected override Type StyleKeyOverride => typeof(TextField);

    public TextField()
    {
        // Added before the theme sets ContextFlyout, so it runs before Avalonia's flyout handler and can place the menu.
        ContextRequested += OnContextRequestedAtPress;
    }

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
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            // Typing closes the edit menu, as on a phone.
            HideEditMenu();
        }
        if (change.Property == TextProperty || change.Property == IsFocusedProperty
            || change.Property == IsReadOnlyProperty || change.Property == ShowClearButtonProperty)
        {
            UpdateClearVisible();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        HideEditMenu();
    }

    /// <summary>
    /// A long press puts the edit menu above the field at the finger, not under it where Avalonia puts a context flyout.
    /// It also covers a field whose Text is still null: TextBox ignores that press, and Avalonia then raises
    /// ContextRequested for the unhandled hold. Requests without a position (from the selection handles, which place
    /// the menu over the selection themselves, or from the keyboard) keep Avalonia's placement.
    /// </summary>
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

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Clear();
        Focus();
    }
}
