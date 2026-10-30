using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Automation.Peers;

namespace TKCode.Arc;

/// <summary>
/// A logo on a ring: a round raised face with the mark in it. It is a toggle button whose state the orbit owns
/// (clicking asks the orbit to open or close the logo, it never flips on its own), moved around the ring by a render
/// transform the stage writes every frame.
/// </summary>
internal sealed class OrbitLogosChip : ToggleButton
{
    private readonly TranslateTransform _translate = new();

    public OrbitLogosChip(OrbitLogo logo, int index)
    {
        Logo = logo;
        Index = index;
        RenderTransform = _translate;
        Padding = new Thickness(12);
        Content = CreateMark(logo);
    }

    public OrbitLogo Logo { get; }

    /// <summary>Position in the orbit's logo list.</summary>
    public int Index { get; }

    /// <summary>Raised when the name tag should show (hover with a mouse or pen, keyboard focus) or hide.</summary>
    public event Action<OrbitLogosChip, bool>? TagChanged;

    public double X
    {
        get => _translate.X;
        set => _translate.X = value;
    }

    public double Y
    {
        get => _translate.Y;
        set => _translate.Y = value;
    }

    protected override Type StyleKeyOverride => typeof(OrbitLogosChip);

    // The orbit decides what a click does; the button must not toggle itself.
    protected override void Toggle()
    {
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleButtonAutomationPeer(this);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            TagChanged?.Invoke(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsKeyboardFocused())
        {
            TagChanged?.Invoke(this, false);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            TagChanged?.Invoke(this, true);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsPointerOver)
        {
            TagChanged?.Invoke(this, false);
        }
    }

    private bool IsKeyboardFocused() => IsFocused && Classes.Contains(":focus-visible");

    /// <summary>Builds the mark for a logo's icon: path data, an image or a control.</summary>
    public static Control? CreateMark(OrbitLogo logo, bool forCard = false)
    {
        switch (logo.Icon)
        {
            case Control control:
                return forCard ? null : control;
            case ImageSource source:
                return new ArcImage { Source = source, Stretch = Stretch.Uniform };
            case IImage image:
                return new Image { Source = image, Stretch = Stretch.Uniform };
            case string text when !string.IsNullOrWhiteSpace(text):
                if (IsImageAddress(text) && ImageSource.TryParse(text, out var parsed))
                {
                    return new ArcImage { Source = parsed, Stretch = Stretch.Uniform };
                }
                if (OrbitLogosMark.TryParse(text) is { } geometry)
                {
                    var mark = new OrbitLogosMark { Data = geometry };
                    if (logo.Color is { } color)
                    {
                        mark.Fill = new ImmutableSolidColorBrush(color);
                    }
                    return mark;
                }
                break;
        }
        return null;
    }

    private static bool IsImageAddress(string text)
    {
        var value = text.Trim();
        if (value.Contains("://", StringComparison.Ordinal) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A category chip above the orbit: a pill label whose state the orbit owns, like <see cref="OrbitLogosChip"/>.</summary>
internal sealed class OrbitLogosCategoryChip : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(OrbitLogosCategoryChip);

    protected override void Toggle()
    {
    }
}
