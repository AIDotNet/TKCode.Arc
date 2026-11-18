using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using ArcButton = TKCode.Arc.Button;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Documents;

namespace TKCode.Arc;

/// <summary>A round avatar with a person's initials on a color picked from their name (the card's people have no photos).</summary>
internal sealed class CodeReviewAvatar : Control
{
    private readonly string _initials;
    private readonly double _hue;
    private readonly double _size;
    private TextLayout? _layout;
    private bool _dark;

    public CodeReviewAvatar(string name, double size)
    {
        _size = size;
        _initials = Initials(name);
        var hash = 0u;
        foreach (var ch in name)
        {
            hash = hash * 31 + ch;
        }
        _hue = hash % 360;
        Width = size;
        Height = size;
        VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>The ring drawn around an overlapping face (the surface color), or transparent.</summary>
    public Color Ring { get; set; }

    internal static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => char.ToUpperInvariant(parts[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]))
        };
    }

    public override void Render(DrawingContext context)
    {
        var dark = CodeReviewUi.IsDark(this);
        if (_layout is null || dark != _dark)
        {
            _dark = dark;
            _layout?.Dispose();
            var ink = new ImmutableSolidColorBrush(Oklch.ToColor(dark ? 0.2 : 0.99, 0, 0));
            _layout = new TextLayout(_initials, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), _size * 0.4, ink);
        }
        var fill = Oklch.ToColor(dark ? 0.78 : 0.6, 0.11, _hue);
        var rect = new Rect(0, 0, _size, _size);
        if (Ring.A > 0)
        {
            context.DrawEllipse(new ImmutableSolidColorBrush(Ring), null, rect.Inflate(2));
        }
        context.DrawEllipse(new ImmutableSolidColorBrush(fill), null, rect);
        _layout.Draw(context, new Point((_size - _layout.WidthIncludingTrailingWhitespace) / 2, (_size - _layout.Height) / 2));
    }
}

/// <summary>
/// A focusable, clickable surface built in code (tabs, the folded thread, the Viewed and Suggest toggles, link buttons):
/// Enter and Space click it, a hover fill fades in, and keyboard focus draws Arc's inset 2px ring.
/// </summary>
internal class CodeReviewPressable : Decorator
{
    /// <summary>The corner radius of the hover fill and the focus ring.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<CodeReviewPressable>();

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    private MotionValue<double>? _hover;
    private double _hoverT;
    private bool _over;
    private ImmutableSolidColorBrush? _fill;
    private Pen? _ring;

    public CodeReviewPressable()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Button);
    }

    /// <summary>The fill under the pointer (transparent: no hover fill).</summary>
    public ArcDesignTokenKind? HoverFill { get; init; } = ArcDesignTokenKind.SurfaceMuted;

    /// <summary>Raised on a click, Enter or Space.</summary>
    public event Action? Click;

    /// <summary>Clicks it from code (automation, tests).</summary>
    public void PerformClick() => Click?.Invoke();

    /// <summary>Whether to keep the hover fill (a checked toggle's resting fill).</summary>
    public bool IsLit
    {
        get => _lit;
        set
        {
            if (_lit != value)
            {
                _lit = value;
                UpdateHover();
            }
        }
    }

    private bool _lit;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _over = true;
            UpdateHover();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _over = false;
        UpdateHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Focus(NavigationMethod.Pointer);
            Click?.Invoke();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Click?.Invoke();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    private void UpdateHover()
    {
        var target = _over || _lit ? 1.0 : 0.0;
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _hover?.Jump(target);
            _hoverT = target;
            InvalidateVisual();
            return;
        }
        if (_hover is null)
        {
            _hover = new MotionValue<double>(this, _hoverT, MotionVectors.Double);
            _hover.Changed += v =>
            {
                _hoverT = v;
                InvalidateVisual();
            };
        }
        _ = _hover.AnimateTo(target, MotionSpec.Tween(CodeReviewMotion.Hover, ArcEasings.Standard));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var radius = CornerRadius.TopLeft;
        var rect = new Rect(Bounds.Size);
        // The whole surface takes the pointer, not only its text and glyphs.
        context.FillRectangle(Brushes.Transparent, rect);
        if (_hoverT > 0.001 && HoverFill is { } fill)
        {
            var color = ArcTokens.GetColor(this, fill);
            if (_fill is null || _fill.Color != color)
            {
                _fill = new ImmutableSolidColorBrush(color);
            }
            using (context.PushOpacity(Math.Clamp(_hoverT, 0, 1)))
            {
                context.DrawRectangle(_fill, null, rect, radius, radius);
            }
        }
        if (IsKeyboardFocusWithin && IsFocused && Classes.Contains(":focus-visible"))
        {
            var ink = CodeReviewUi.Color(this, CodeReviewTokenKind.FocusInset);
            if (_ring is null || _ring.Brush is not ISolidColorBrush b || b.Color != ink)
            {
                _ring = new Pen(new ImmutableSolidColorBrush(ink), 2);
            }
            context.DrawRectangle(null, _ring, rect.Deflate(1), Math.Max(0, radius - 1), Math.Max(0, radius - 1));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}

/// <summary>
/// Arc's small checkbox toggle (the Viewed checkbox, "Suggest a change"): a 16px box, radius 5, the track color off and a
/// near-foreground fill with a check on; the label beside it. Reports itself as a check box with its state.
/// </summary>
internal sealed class CodeReviewToggle : CodeReviewPressable
{
    private readonly Border _box;
    private readonly ArcGlyph _check;
    private readonly TextBlock? _label;
    private bool _isChecked;

    public CodeReviewToggle(string? label, double fontSize = 12)
    {
        HoverFill = ArcDesignTokenKind.SurfaceMuted;
        _check = CodeReviewUi.Glyph(ArcIcons.Check, 11, ArcDesignTokenKind.Surface, 2.25);
        _check.Opacity = 0;
        _check.HorizontalAlignment = HorizontalAlignment.Center;
        _box = new Border { Width = 16, Height = 16, Child = _check, VerticalAlignment = VerticalAlignment.Center }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep5);
        ArcTokens.Bind(_box, Border.BackgroundProperty, ArcDesignTokenKind.ControlTrack);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_box);
        if (label is not null)
        {
            _label = CodeReviewUi.Text(label, fontSize, ArcDesignTokenKind.TextSecondary, FontWeight.Medium, 16.8);
            row.Children.Add(_label);
        }
        Child = row;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.CheckBox);
        Click += () => IsChecked = !IsChecked;
    }

    /// <summary>Raised when <see cref="IsChecked"/> changes by a click or key.</summary>
    public event Action<bool>? Toggled;

    /// <summary>Hides the label (the compact Viewed toggle shows only its box).</summary>
    public bool ShowsLabel
    {
        set
        {
            if (_label is not null)
            {
                _label.IsVisible = value;
            }
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }
            SetChecked(value);
            Toggled?.Invoke(value);
        }
    }

    /// <summary>Sets the state without raising <see cref="Toggled"/>.</summary>
    public void SetChecked(bool value)
    {
        _isChecked = value;
        _check.Opacity = value ? 1 : 0;
        ArcTokens.Bind(_box, Border.BackgroundProperty, value ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.ControlTrack);
        _box.Opacity = value ? 0.96 : 1;
        if (_label is not null)
        {
            ArcTokens.Bind(_label, TextBlock.ForegroundProperty, value ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
        }
        IsLit = value && LitWhenChecked;
        AutomationProperties.SetItemStatus(this, value ? "checked" : "not checked");
    }

    /// <summary>Keeps the hover fill while checked (the Viewed toggle).</summary>
    public bool LitWhenChecked { get; init; }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleAutomationPeer(this);

    private sealed class ToggleAutomationPeer(CodeReviewToggle owner) : ControlAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => owner.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => owner.IsChecked = !owner.IsChecked;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    }
}

/// <summary>Builders for the card's buttons, sized like Arc's (small: 32 tall, radius 12; medium: 40, radius 14).</summary>
internal static class CodeReviewButtons
{
    public static ArcButton Small(string text, ButtonVariant variant)
    {
        var button = new ArcButton
        {
            Content = text,
            Variant = variant,
            Size = ControlSize.Small,
            MinHeight = 32,
            Height = 32,
            Padding = new Thickness(12, 0),
            FontSize = 13,
            LabelSwapMode = ContentSwapMode.Label,
            LabelSpring = ArcSpring.Quick,
            VerticalAlignment = VerticalAlignment.Center
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        return button;
    }

    public static ArcButton Medium(string text, ButtonVariant variant)
    {
        var button = new ArcButton
        {
            Content = text,
            Variant = variant,
            MinHeight = 40,
            Height = 40,
            Padding = new Thickness(16, 0),
            FontSize = 13,
            LabelSwapMode = ContentSwapMode.Label,
            LabelSpring = ArcSpring.Quick,
            VerticalAlignment = VerticalAlignment.Top
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        return button;
    }

    /// <summary>A check glyph and a word, for "Approved" and "Applied".</summary>
    public static Control CheckLabel(string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var glyph = new ArcGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        glyph.Bind(ArcGlyph.ForegroundProperty, glyph.GetObservable(TextElement.ForegroundProperty));
        row.Children.Add(glyph);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
}
