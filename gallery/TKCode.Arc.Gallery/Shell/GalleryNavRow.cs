using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// A full-width navigation row: sidebar groups and items, "On this page" entries and links. Arc's own
/// <see cref="Button"/> hugs its label (its label slot is sized to the content), which is right for buttons
/// and wrong for rows that must span the sidebar, so rows are a small purpose-built control: a surface that
/// can fill on hover, text that moves from its resting color to the foreground, and a keyboard focus ring.
/// </summary>
public sealed class GalleryNavRow : Panel
{
    private readonly Border _surface = new();
    private readonly Border _ring = new()
    {
        IsVisible = false,
        IsHitTestVisible = false,
        Margin = new Thickness(-3),
        BorderThickness = new Thickness(2)
    };
    private readonly ArcDesignTokenKind _rest;
    private readonly ArcDesignTokenKind _hover;
    private readonly ArcDesignTokenKind? _restFill;
    private readonly ArcDesignTokenKind? _hoverFill;
    private readonly FontWeight _weight;
    private IDisposable? _foreground;
    private IDisposable? _fill;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;

    public GalleryNavRow(Control content, string? name = null, double fontSize = 13, double height = 30,
                         Thickness? padding = null, double radius = 18, FontWeight weight = FontWeight.Normal,
                         ArcDesignTokenKind rest = ArcDesignTokenKind.TextSecondary,
                         ArcDesignTokenKind hover = ArcDesignTokenKind.Foreground,
                         ArcDesignTokenKind? restFill = null, ArcDesignTokenKind? hoverFill = null,
                         ArcDesignTokenKind? stroke = null)
    {
        _rest = rest;
        _hover = hover;
        _restFill = restFill;
        _hoverFill = hoverFill;
        _weight = weight;

        _surface.MinHeight = height;
        _surface.Padding = padding ?? new Thickness(14, 0, 8, 0);
        _surface.CornerRadius = new CornerRadius(radius);
        _surface.Child = content;
        if (stroke is { } strokeToken)
        {
            _surface.BorderThickness = new Thickness(1);
            ArcTokens.Bind(_surface, Border.BorderBrushProperty, strokeToken);
        }
        _ring.CornerRadius = new CornerRadius(radius + 3);
        ArcTokens.Bind(_ring, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
        Children.Add(_surface);
        Children.Add(_ring);

        TextElement.SetFontSize(this, fontSize);
        ArcTokens.Bind(this, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Button);
        if (name is not null)
        {
            AutomationProperties.SetName(this, name);
        }
        Refresh();
    }

    /// <summary>A selected row reads at the foreground color in a heavier weight.</summary>
    public bool IsSelected
    {
        get => _selected;
        set
        {
            if (_selected != value)
            {
                _selected = value;
                Refresh();
            }
        }
    }

    public event Action? Click;

    private void Refresh()
    {
        _foreground?.Dispose();
        var kind = _selected ? ArcDesignTokenKind.Foreground : _hovered ? _hover : _rest;
        _foreground = ArcTokens.Bind(this, TextElement.ForegroundProperty, kind);
        TextElement.SetFontWeight(this, _selected ? FontWeight.Medium : _weight);

        _fill?.Dispose();
        _fill = null;
        var fill = _hovered ? _hoverFill ?? _restFill : _restFill;
        if (fill is { } token)
        {
            _fill = ArcTokens.Bind(_surface, Border.BackgroundProperty, token);
        }
        else
        {
            // Transparent, not null: the whole row takes the pointer, not only its text and glyph (a collapsed rail hides the text).
            _surface.Background = Brushes.Transparent;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        Refresh();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        Refresh();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var activate = _pressed && new Rect(Bounds.Size).Contains(e.GetPosition(this));
        _pressed = false;
        e.Pointer.Capture(null);
        if (activate)
        {
            Focus();
            Click?.Invoke();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space)
        {
            Click?.Invoke();
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring.IsVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring.IsVisible = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
