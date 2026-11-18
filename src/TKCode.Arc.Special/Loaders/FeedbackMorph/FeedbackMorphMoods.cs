using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One of the built-in mood faces (or a mood's own glyph) drawn as a 24-unit stroke: a round head, two eyes and a mouth
/// whose curve goes from a deep frown to an open grin. The drawings are this library's own.
/// </summary>
internal sealed class FeedbackFaceGlyph : Control
{
    public static readonly StyledProperty<FeedbackFace> FaceProperty =
        AvaloniaProperty.Register<FeedbackFaceGlyph, FeedbackFace>(nameof(Face), FeedbackFace.Neutral);

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<FeedbackFaceGlyph, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<FeedbackFaceGlyph, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<FeedbackFaceGlyph, double>(nameof(Size), 24);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<FeedbackFaceGlyph, double>(nameof(StrokeThickness), 1.75);

    // 24-unit drawings: head r = 10 at the centre; eyes as round dots (zero-length strokes); mouths as quadratic curves.
    private static readonly Geometry s_head = Geometry.Parse("M12 2 A10 10 0 1 1 11.99 2 Z");
    private static readonly Geometry[] s_features =
    [
        // Upset: brows tilted down to the middle, small eyes, a deep frown.
        Geometry.Parse("M7.4 8.2 L10.1 9.4 M16.6 8.2 L13.9 9.4 M9 11.4 L9 11.45 M15 11.4 L15 11.45 M8.2 17 Q12 13.6 15.8 17"),
        // Sad: dot eyes, a gentle frown.
        Geometry.Parse("M9 9.6 L9 9.65 M15 9.6 L15 9.65 M8.6 16.4 Q12 14 15.4 16.4"),
        // Neutral: dot eyes, a straight mouth.
        Geometry.Parse("M9 9.6 L9 9.65 M15 9.6 L15 9.65 M8.6 15.4 L15.4 15.4"),
        // Happy: dot eyes, a soft smile.
        Geometry.Parse("M9 9.6 L9 9.65 M15 9.6 L15 9.65 M8.2 14 Q12 17.8 15.8 14"),
        // Delighted: closed smiling eyes and an open grin.
        Geometry.Parse("M7.6 10.2 Q9 8.6 10.4 10.2 M13.6 10.2 Q15 8.6 16.4 10.2 M7.8 13.4 L16.2 13.4 Q15.6 18.4 12 18.4 Q8.4 18.4 7.8 13.4 Z")
    ];

    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    static FeedbackFaceGlyph()
    {
        AffectsRender<FeedbackFaceGlyph>(FaceProperty, GlyphProperty, ForegroundProperty, StrokeThicknessProperty);
        AffectsMeasure<FeedbackFaceGlyph>(SizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FeedbackFaceGlyph>(false);
    }

    public FeedbackFace Face { get => GetValue(FaceProperty); set => SetValue(FaceProperty, value); }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || !_penThickness.Equals(StrokeThickness))
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = StrokeThickness;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (Glyph is { } glyph)
            {
                context.DrawGeometry(null, _pen, glyph);
                return;
            }
            context.DrawGeometry(null, _pen, s_head);
            context.DrawGeometry(null, _pen, s_features[Math.Clamp((int)Face, 0, s_features.Length - 1)]);
        }
    }
}

/// <summary>
/// One face of the mood scale: a 44 px round radio button. The picked face grows to 1.14 and turns to the foreground ink,
/// the others shrink to 0.92 (theme transitions); a mouse over a face lights it and shows its name under the row.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(HoverPseudoClass, DimmedPseudoClass)]
internal sealed class FeedbackMoodButton : RadioButton
{
    internal const string HoverPseudoClass = ":hover";
    internal const string DimmedPseudoClass = ":dimmed";
    internal const string RootPart = "PART_Root";
    internal const string GlyphPart = "PART_Glyph";

    private readonly FeedbackMoodPicker _owner;
    private LazyFocusRing? _focusRing;

    static FeedbackMoodButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<FeedbackMoodButton>(false);
    }

    public FeedbackMoodButton(FeedbackMoodPicker owner, FeedbackMood mood)
    {
        _owner = owner;
        Mood = mood;
        AutomationProperties.SetName(this, mood.Label ?? mood.Value ?? string.Empty);
        // Grouping is the picker's job (the row may hold several pickers' worth of buttons in tests).
        GroupName = null;
    }

    public FeedbackMood Mood { get; }

    protected override Type StyleKeyOverride => typeof(FeedbackMoodButton);

    /// <summary>Set while another face is picked (the theme shrinks this one).</summary>
    public bool IsDimmed
    {
        set => PseudoClasses.Set(DimmedPseudoClass, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<FeedbackFaceGlyph>(GlyphPart) is { } glyph)
        {
            glyph.Face = Mood.Face;
            glyph.Glyph = Mood.Glyph;
        }
        _focusRing = new LazyFocusRing(this, FeedbackMorphTokens.Key(FeedbackMorphTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: true);
    }

    protected override void OnClick()
    {
        // The picker owns the selection: a click picks this face (a second click keeps it).
        _owner.Pick(this, focus: false);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        var hover = e.Pointer.Type != PointerType.Touch;
        PseudoClasses.Set(HoverPseudoClass, hover);
        if (hover)
        {
            _owner.SetHovered(this);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
        _owner.ClearHovered(this);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>
/// The mood scale: equal columns, each holding a face button, over a 48 px round highlight that glides to the picked
/// column. A radio group with a roving tab stop: arrows move and pick (wrapping), Home and End go to the ends.
/// </summary>
internal sealed class FeedbackMoodPicker : Control
{
    public static readonly StyledProperty<IBrush?> HighlightFillProperty =
        AvaloniaProperty.Register<FeedbackMoodPicker, IBrush?>(nameof(HighlightFill));

    public static readonly StyledProperty<IBrush?> HighlightStrokeProperty =
        AvaloniaProperty.Register<FeedbackMoodPicker, IBrush?>(nameof(HighlightStroke));

    public static readonly StyledProperty<double> HighlightSizeProperty =
        AvaloniaProperty.Register<FeedbackMoodPicker, double>(nameof(HighlightSize), 48);

    public static readonly StyledProperty<double> ButtonSizeProperty =
        AvaloniaProperty.Register<FeedbackMoodPicker, double>(nameof(ButtonSize), 44);

    private readonly GlidingIndicator _highlight = new() { IsHitTestVisible = false };
    private readonly List<FeedbackMoodButton> _buttons = new();
    private FeedbackMoodButton? _picked;
    private FeedbackMoodButton? _hovered;
    private bool _placed;
    private Rect? _highlightTarget;

    static FeedbackMoodPicker()
    {
        AffectsMeasure<FeedbackMoodPicker>(ButtonSizeProperty);
        AffectsArrange<FeedbackMoodPicker>(HighlightSizeProperty);
        FocusableProperty.OverrideDefaultValue<FeedbackMoodPicker>(false);
    }

    public FeedbackMoodPicker()
    {
        _highlight.CornerRadius = new CornerRadius(999);
        _highlight.StrokeThickness = 1;
        VisualChildren.Add(_highlight);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    public IBrush? HighlightFill { get => GetValue(HighlightFillProperty); set => SetValue(HighlightFillProperty, value); }

    public IBrush? HighlightStroke { get => GetValue(HighlightStrokeProperty); set => SetValue(HighlightStrokeProperty, value); }

    public double HighlightSize { get => GetValue(HighlightSizeProperty); set => SetValue(HighlightSizeProperty, value); }

    public double ButtonSize { get => GetValue(ButtonSizeProperty); set => SetValue(ButtonSizeProperty, value); }

    /// <summary>The face buttons, in order.</summary>
    public IReadOnlyList<FeedbackMoodButton> Buttons => _buttons;

    /// <summary>The picked mood (null when none).</summary>
    public FeedbackMood? Selected => _picked?.Mood;

    /// <summary>The mood under the mouse (null when none).</summary>
    public FeedbackMood? Hovered => _hovered?.Mood;

    /// <summary>The highlight (tests).</summary>
    internal GlidingIndicator Highlight => _highlight;

    /// <summary>Raised when a face is picked by the person (click, arrows, Home, End).</summary>
    public event Action<FeedbackMood>? Picked;

    /// <summary>Raised when the face under the mouse changes.</summary>
    public event Action? HoverChanged;

    /// <summary>Replaces the faces with <paramref name="moods"/> and picks <paramref name="selected"/>.</summary>
    public void SetMoods(IReadOnlyList<FeedbackMood> moods, FeedbackMood? selected)
    {
        if (_buttons.Count == moods.Count && _buttons.Select(b => b.Mood).SequenceEqual(moods))
        {
            Select(selected, animate: false);
            return;
        }
        foreach (var button in _buttons)
        {
            VisualChildren.Remove(button);
            LogicalChildren.Remove(button);
        }
        _buttons.Clear();
        _picked = null;
        _hovered = null;
        foreach (var mood in moods)
        {
            var button = new FeedbackMoodButton(this, mood);
            _buttons.Add(button);
            VisualChildren.Add(button);
            LogicalChildren.Add(button);
        }
        Select(selected, animate: false);
        InvalidateMeasure();
    }

    /// <summary>Shows <paramref name="mood"/> as picked (from the owner's property; raises nothing).</summary>
    public void Select(FeedbackMood? mood, bool animate)
    {
        _picked = mood is null ? null : _buttons.FirstOrDefault(b => ReferenceEquals(b.Mood, mood));
        foreach (var button in _buttons)
        {
            var on = ReferenceEquals(button, _picked);
            button.IsChecked = on;
            button.IsDimmed = _picked is not null && !on;
        }
        UpdateTabStops();
        PlaceHighlight(animate && _placed);
    }

    /// <summary>The button that takes focus when the group is entered: the picked face, else the first.</summary>
    public FeedbackMoodButton? FocusTarget => _picked ?? (_buttons.Count > 0 ? _buttons[0] : null);

    internal void Pick(FeedbackMoodButton button, bool focus)
    {
        if (focus)
        {
            button.Focus(NavigationMethod.Directional);
        }
        if (!ReferenceEquals(_picked, button))
        {
            Select(button.Mood, animate: true);
        }
        Picked?.Invoke(button.Mood);
    }

    internal void SetHovered(FeedbackMoodButton button)
    {
        if (!ReferenceEquals(_hovered, button))
        {
            _hovered = button;
            HoverChanged?.Invoke();
        }
    }

    internal void ClearHovered(FeedbackMoodButton button)
    {
        if (ReferenceEquals(_hovered, button))
        {
            _hovered = null;
            HoverChanged?.Invoke();
        }
    }

    private void UpdateTabStops()
    {
        var stop = FocusTarget;
        foreach (var button in _buttons)
        {
            button.IsTabStop = ReferenceEquals(button, stop);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _buttons.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var current = e.Source as FeedbackMoodButton ?? _picked;
        var index = current is null ? -1 : _buttons.IndexOf(current);
        var next = e.Key switch
        {
            Key.Right or Key.Down => index < 0 ? 0 : (index + 1) % _buttons.Count,
            Key.Left or Key.Up => index < 0 ? _buttons.Count - 1 : (index - 1 + _buttons.Count) % _buttons.Count,
            Key.Home => 0,
            Key.End => _buttons.Count - 1,
            _ => -2
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        Pick(_buttons[next], focus: true);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = ButtonSize;
        foreach (var button in _buttons)
        {
            button.Measure(new Size(size, size));
        }
        _highlight.Measure(availableSize);
        var width = double.IsInfinity(availableSize.Width) ? _buttons.Count * (size + 8) : availableSize.Width;
        return new Size(width, size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _highlight.Arrange(new Rect(finalSize));
        var count = _buttons.Count;
        if (count == 0)
        {
            return finalSize;
        }
        var column = finalSize.Width / count;
        var size = ButtonSize;
        for (var i = 0; i < count; i++)
        {
            _buttons[i].Arrange(new Rect(column * i + (column - size) / 2, (finalSize.Height - size) / 2, size, size));
        }
        PlaceHighlight(animate: _placed);
        _placed = true;
        return finalSize;
    }

    private void PlaceHighlight(bool animate)
    {
        if (_picked is null)
        {
            _highlightTarget = null;
            _highlight.Hide(animate);
            return;
        }
        var count = _buttons.Count;
        var index = _buttons.IndexOf(_picked);
        if (count == 0 || index < 0 || Bounds.Width <= 0)
        {
            return;
        }
        var column = Bounds.Width / count;
        var d = HighlightSize;
        var rect = new Rect(column * index + (column - d) / 2, (Bounds.Height - d) / 2, d, d);
        if (_highlightTarget == rect)
        {
            return;
        }
        _highlightTarget = rect;
        _highlight.MoveTo(rect, FeedbackMorphSurface.ShapeSpring, animate);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighlightFillProperty)
        {
            _highlight.Fill = HighlightFill;
        }
        else if (change.Property == HighlightStrokeProperty)
        {
            _highlight.Stroke = HighlightStroke;
        }
    }
}

/// <summary>
/// The panel's row of faces with its caption, which tells the faces apart: the hint until a face is hovered or picked,
/// then that face's name (the one under the mouse wins).
/// </summary>
internal static class FeedbackMoodCaption
{
    /// <summary>The caption text for the picker's current hover and pick.</summary>
    public static string Text(FeedbackMoodPicker picker, string? hint) =>
        (picker.Hovered ?? picker.Selected) is { } mood ? mood.Label ?? mood.Value ?? string.Empty : hint ?? string.Empty;
}
