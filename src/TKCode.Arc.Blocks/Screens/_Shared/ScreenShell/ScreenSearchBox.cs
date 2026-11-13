using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The search box's text input: no frame of its own (the box draws it), a muted placeholder.</summary>
[TemplatePart("PART_TextPresenter", typeof(TextPresenter))]
internal sealed class ScreenSearchInput : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(ScreenSearchInput);
}

/// <summary>
/// The shell's search box (Arc screen shell, design reference): 32 high, radius 8, a 1px Border on a slightly raised fill
/// (dark Surface mixed 17% toward the page; light white), padding 0 4 0 10, a 16 search glyph (muted, stroke 2) and either a
/// key cap (the sidebar's ⌘K) or a 24 clear button while there is text. The border turns to the foreground while focused.
/// </summary>
internal sealed class ScreenSearchBox : Border
{
    private readonly ScreenSearchInput _input = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ScreenPressable _clear;
    private readonly Control? _key;
    private ScreenPaint _paint;

    public ScreenSearchBox(ScreenPaint paint, string placeholder, string clearName, Control? key = null)
    {
        _paint = paint;
        _key = key;
        Height = 32;
        Padding = new Thickness(10, 0, 4, 0);
        BorderThickness = new Thickness(1);
        Cursor = new Cursor(StandardCursorType.Ibeam);
        this.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusStep8);
        _input.PlaceholderText = placeholder;
        AutomationProperties.SetName(_input, placeholder);
        _clear = ScreenButtons.Icon(paint, ArcIcons.X, clearName, () =>
        {
            _input.Text = string.Empty;
            _input.Focus();
        }, glyphSize: 14, foreground: paint.MutedBrush, size: 24);
        _clear.Radius = 12;
        _clear.IsVisible = false;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        var glyph = new ArcGlyph { Data = ArcIcons.Search, Size = 16, StrokeThickness = 2, Foreground = paint.MutedBrush, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(glyph);
        Grid.SetColumn(_input, 1);
        grid.Children.Add(_input);
        var slot = new Panel { MinWidth = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center };
        slot.Children.Add(_clear);
        if (key is not null)
        {
            slot.Children.Add(key);
        }
        Grid.SetColumn(slot, 2);
        grid.Children.Add(slot);
        Child = grid;
        _input.TextChanged += (_, _) =>
        {
            var empty = string.IsNullOrEmpty(_input.Text);
            _clear.IsVisible = !empty;
            if (_key is not null)
            {
                _key.IsVisible = empty;
            }
            TextChanged?.Invoke(_input.Text ?? string.Empty);
        };
        _input.GotFocus += (_, _) => ApplyPaint(_paint);
        _input.LostFocus += (_, _) => ApplyPaint(_paint);
        ApplyPaint(paint);
    }

    /// <summary>Raised with the new text on every change.</summary>
    public event Action<string>? TextChanged;

    public ScreenSearchInput Input => _input;

    public string Text
    {
        get => _input.Text ?? string.Empty;
        set
        {
            if ((_input.Text ?? string.Empty) != value)
            {
                _input.Text = value;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled)
        {
            _input.Focus(NavigationMethod.Pointer);
        }
    }

    public void ApplyPaint(ScreenPaint paint)
    {
        _paint = paint;
        Background = paint.IsDark
            ? new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Oklch.MixOklab(paint.Surface, paint.Background, 0.3))
            : paint.SurfaceBrush;
        BorderBrush = _input.IsFocused ? paint.ForegroundBrush : paint.BorderBrush;
    }

    public void FocusInput() => _input.Focus(NavigationMethod.Tab);
}
