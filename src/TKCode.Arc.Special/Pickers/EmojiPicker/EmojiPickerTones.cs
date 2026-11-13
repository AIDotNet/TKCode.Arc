using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The skin tone choice of an <see cref="EmojiPicker"/>: a raised hand in the current tone that opens into the six tones
/// in a row. Closed, Enter, Space or Down opens it; open, Left and Right move, Enter or Space chooses, Escape closes.
/// A click on the hand opens it and a click on a tone chooses it; it closes when focus leaves.
/// </summary>
internal sealed class EmojiPickerTones : Control
{
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<EmojiPickerTones, double>(nameof(CellSize), 32);

    public static readonly StyledProperty<double> EmojiFontSizeProperty =
        AvaloniaProperty.Register<EmojiPickerTones, double>(nameof(EmojiFontSize), 18);

    public static readonly StyledProperty<FontFamily> EmojiFontFamilyProperty =
        AvaloniaProperty.Register<EmojiPickerTones, FontFamily>(nameof(EmojiFontFamily), FontFamily.Default);

    public static readonly StyledProperty<IBrush?> HoverFillProperty =
        AvaloniaProperty.Register<EmojiPickerTones, IBrush?>(nameof(HoverFill));

    public static readonly StyledProperty<IBrush?> SelectedFillProperty =
        AvaloniaProperty.Register<EmojiPickerTones, IBrush?>(nameof(SelectedFill));

    public static readonly StyledProperty<IBrush?> FocusRingProperty =
        AvaloniaProperty.Register<EmojiPickerTones, IBrush?>(nameof(FocusRing));

    public static readonly StyledProperty<CornerRadius> CellCornerRadiusProperty =
        AvaloniaProperty.Register<EmojiPickerTones, CornerRadius>(nameof(CellCornerRadius));

    private const string Hand = "✋";
    private const int ToneCount = 6;

    private readonly TextLayout?[] _layouts = new TextLayout?[ToneCount];
    private EmojiSkinTone _tone;
    private bool _isExpanded;
    private int _focusIndex;
    private int _hover = -1;
    private Func<EmojiSkinTone, string> _toneName = static t => t.ToString();
    private string _buttonName = "Skin tone";
    private Pen? _focusPen;

    static EmojiPickerTones()
    {
        FocusableProperty.OverrideDefaultValue<EmojiPickerTones>(true);
        AffectsMeasure<EmojiPickerTones>(CellSizeProperty);
        AffectsRender<EmojiPickerTones>(HoverFillProperty, SelectedFillProperty, FocusRingProperty, CellCornerRadiusProperty);
        CursorProperty.OverrideDefaultValue<EmojiPickerTones>(new Cursor(StandardCursorType.Hand));
    }

    public double CellSize { get => GetValue(CellSizeProperty); set => SetValue(CellSizeProperty, value); }

    public double EmojiFontSize { get => GetValue(EmojiFontSizeProperty); set => SetValue(EmojiFontSizeProperty, value); }
    public FontFamily EmojiFontFamily { get => GetValue(EmojiFontFamilyProperty); set => SetValue(EmojiFontFamilyProperty, value); }

    public IBrush? HoverFill { get => GetValue(HoverFillProperty); set => SetValue(HoverFillProperty, value); }

    public IBrush? SelectedFill { get => GetValue(SelectedFillProperty); set => SetValue(SelectedFillProperty, value); }

    public IBrush? FocusRing { get => GetValue(FocusRingProperty); set => SetValue(FocusRingProperty, value); }

    public CornerRadius CellCornerRadius { get => GetValue(CellCornerRadiusProperty); set => SetValue(CellCornerRadiusProperty, value); }

    /// <summary>Raised when a tone is chosen.</summary>
    public event Action<EmojiSkinTone>? ToneChosen;

    /// <summary>Raised when the row opens or closes.</summary>
    public event Action<bool>? ExpandedChanged;

    public bool IsExpanded => _isExpanded;

    public EmojiSkinTone Tone => _tone;

    internal int FocusIndex => _focusIndex;

    public void SetTone(EmojiSkinTone tone)
    {
        _tone = tone;
        _focusIndex = (int)tone;
        UpdateAutomation();
        InvalidateVisual();
    }

    public void SetLabels(string buttonName, Func<EmojiSkinTone, string> toneName)
    {
        _buttonName = buttonName;
        _toneName = toneName;
        UpdateAutomation();
    }

    public void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
        {
            return;
        }
        _isExpanded = expanded;
        _focusIndex = (int)_tone;
        _hover = -1;
        PseudoClasses.Set(":expanded", expanded);
        InvalidateMeasure();
        InvalidateVisual();
        UpdateAutomation();
        ExpandedChanged?.Invoke(expanded);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _isExpanded ? ToneCount : 1;
        return new Size(CellSize * count, CellSize);
    }

    private Rect CellRect(int slot) => new(slot * CellSize, (Bounds.Height - CellSize) / 2, CellSize, CellSize);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var focusVisible = IsFocused && Classes.Contains(":focus-visible");
        var count = _isExpanded ? ToneCount : 1;
        for (var slot = 0; slot < count; slot++)
        {
            var tone = _isExpanded ? slot : (int)_tone;
            var rect = CellRect(slot);
            if (_isExpanded && tone == (int)_tone && SelectedFill is { } selected)
            {
                context.DrawRectangle(selected, null, new RoundedRect(rect.Deflate(1), CellCornerRadius));
            }
            else if (slot == _hover && HoverFill is { } hover)
            {
                context.DrawRectangle(hover, null, new RoundedRect(rect.Deflate(1), CellCornerRadius));
            }
            if (focusVisible && (!_isExpanded || slot == _focusIndex) && FocusRing is { } ring)
            {
                _focusPen ??= new Pen(ring, 2);
                context.DrawRectangle(null, _focusPen, new RoundedRect(rect.Deflate(1), CellCornerRadius));
            }
            var layout = Layout(tone);
            layout.Draw(context, new Point(rect.X + (rect.Width - layout.WidthIncludingTrailingWhitespace) / 2, rect.Y + (rect.Height - layout.Height) / 2));
        }
    }

    private TextLayout Layout(int tone)
    {
        if (_layouts[tone] is { } layout)
        {
            return layout;
        }
        var text = EmojiPickerData.Find(Hand)?.Display((EmojiSkinTone)tone, double.MaxValue) ?? Hand;
        layout = new TextLayout(text, new Typeface(EmojiFontFamily), EmojiFontSize, Brushes.Black);
        _layouts[tone] = layout;
        return layout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmojiFontSizeProperty || change.Property == EmojiFontFamilyProperty || change.Property == TextElement.FontFamilyProperty)
        {
            for (var i = 0; i < _layouts.Length; i++)
            {
                _layouts[i]?.Dispose();
                _layouts[i] = null;
            }
        }
        else if (change.Property == FocusRingProperty)
        {
            _focusPen = null;
        }
    }

    private int SlotAt(Point point)
    {
        var slot = (int)Math.Floor(point.X / CellSize);
        var count = _isExpanded ? ToneCount : 1;
        return slot >= 0 && slot < count ? slot : -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var slot = SlotAt(e.GetPosition(this));
        if (slot != _hover)
        {
            _hover = slot;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        var slot = SlotAt(e.GetPosition(this));
        if (slot < 0)
        {
            return;
        }
        e.Handled = true;
        if (_isExpanded)
        {
            Choose((EmojiSkinTone)slot);
        }
        else
        {
            SetExpanded(true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (!_isExpanded)
        {
            if (e.Key is Key.Enter or Key.Space or Key.Down)
            {
                e.Handled = true;
                SetExpanded(true);
            }
            return;
        }
        switch (e.Key)
        {
            case Key.Left:
                _focusIndex = Math.Max(0, _focusIndex - 1);
                break;
            case Key.Right:
                _focusIndex = Math.Min(ToneCount - 1, _focusIndex + 1);
                break;
            case Key.Home:
                _focusIndex = 0;
                break;
            case Key.End:
                _focusIndex = ToneCount - 1;
                break;
            case Key.Enter or Key.Space:
                Choose((EmojiSkinTone)_focusIndex);
                e.Handled = true;
                return;
            case Key.Escape:
                SetExpanded(false);
                e.Handled = true;
                return;
            default:
                return;
        }
        e.Handled = true;
        UpdateAutomation();
        InvalidateVisual();
    }

    private void Choose(EmojiSkinTone tone)
    {
        SetTone(tone);
        SetExpanded(false);
        ToneChosen?.Invoke(tone);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SetExpanded(false);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        var name = _isExpanded
            ? _toneName((EmojiSkinTone)_focusIndex)
            : _buttonName + ": " + _toneName(_tone);
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetItemStatus(this, _isExpanded ? "Expanded" : "Collapsed");
    }
}
