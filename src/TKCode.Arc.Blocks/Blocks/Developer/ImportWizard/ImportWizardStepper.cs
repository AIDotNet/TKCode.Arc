using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Where a step stands in the step bar.</summary>
internal enum ImportStepState
{
    Todo,
    Current,
    Done
}

/// <summary>
/// One step of the step bar, drawn in one pass: a 24px circle with a 1px edge holding the step number or a check, and the
/// label 8 to its right (hidden when compact). Todo: strong border, muted number and label. Current: filled with the accent,
/// number on the accent, label in the text color. Done: accent edge and check, secondary label (text color and an accent
/// tint behind the mark on hover). Colors move over 240ms ease-out, the number fades out over 300ms and the check pops in
/// from half size on the quick spring. Only done steps can be pressed. Drawn in code because the three states cross-fade
/// inside one glyph (fill, edge, number and check at once), which a template of separate parts would double-draw.
/// </summary>
internal sealed class ImportWizardStepButton : AvaloniaButton
{
    private const double MarkSize = 24;
    private const double Gap = 8;
    private const double Height32 = 32;

    private ImportStepState _state = ImportStepState.Todo;
    private string _label = string.Empty;
    private string _number = "1";
    private bool _showLabel = true;
    private TextLayout? _labelLayout;
    private TextLayout? _numberLayout;
    private uint _labelColor;
    private uint _numberColor;
    private MotionValue<double>? _current;
    private MotionValue<double>? _done;
    private MotionValue<double>? _numberOpacity;
    private MotionValue<double>? _check;
    private MotionValue<double>? _hover;
    private double _currentValue;
    private double _doneValue;
    private double _numberValue = 1;
    private double _checkValue;
    private double _hoverValue;
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();

    static ImportWizardStepButton()
    {
        AffectsRender<ImportWizardStepButton>(IsPointerOverProperty);
        FocusableProperty.OverrideDefaultValue<ImportWizardStepButton>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ImportWizardStepButton>(false);
        CursorProperty.OverrideDefaultValue<ImportWizardStepButton>(Cursor.Default);
    }

    public ImportWizardStepButton()
    {
        Background = Brushes.Transparent;
        VerticalAlignment = VerticalAlignment.Center;
        ActualThemeVariantChanged += (_, _) => { _brushes.Clear(); InvalidateVisual(); };
    }

    /// <summary>The step's position, from 0.</summary>
    public int Index { get; init; }

    public ImportStepState State => _state;

    /// <summary>Sets the label and the number.</summary>
    public void SetText(string label, int number)
    {
        _label = label;
        _number = number.ToString(CultureInfo.CurrentCulture);
        _labelLayout?.Dispose();
        _labelLayout = null;
        _numberLayout?.Dispose();
        _numberLayout = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Shows or hides the label (compact).</summary>
    public void SetLabelVisible(bool visible)
    {
        if (_showLabel == visible)
        {
            return;
        }
        _showLabel = visible;
        InvalidateMeasure();
    }

    /// <summary>Moves to <paramref name="state"/>, animated or not.</summary>
    public void SetState(ImportStepState state, bool animate)
    {
        var changed = state != _state;
        _state = state;
        Cursor = state == ImportStepState.Done && IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        var current = state == ImportStepState.Current ? 1.0 : 0.0;
        var done = state == ImportStepState.Done ? 1.0 : 0.0;
        if (!changed && !animate)
        {
            Jump(current, done);
            return;
        }
        if (!animate || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            Jump(current, done);
            return;
        }
        var color = MotionSpec.Tween(ImportWizardMotion.Standard, ImportWizardMotion.EaseOut);
        _ = Value(ref _current, v => _currentValue = v, _currentValue).AnimateTo(current, color);
        _ = Value(ref _done, v => _doneValue = v, _doneValue).AnimateTo(done, color);
        // The number fades out (300ms) when the check comes, and back in the same way when it leaves.
        _ = Value(ref _numberOpacity, v => _numberValue = v, _numberValue).AnimateTo(1 - done, MotionSpec.Tween(ImportWizardMotion.FadeOutDuration, ImportWizardMotion.FadeOut));
        _ = Value(ref _check, v => _checkValue = v, _checkValue).AnimateTo(done, MotionSpec.Spring(ImportWizardMotion.Quick));
    }

    private void Jump(double current, double done)
    {
        _currentValue = current;
        _doneValue = done;
        _numberValue = 1 - done;
        _checkValue = done;
        _current?.Jump(current);
        _done?.Jump(done);
        _numberOpacity?.Jump(1 - done);
        _check?.Jump(done);
        InvalidateVisual();
    }

    private MotionValue<double> Value(ref MotionValue<double>? field, Action<double> set, double initial)
    {
        if (field is null)
        {
            field = new MotionValue<double>(this, initial, MotionVectors.Double);
            field.Changed += v =>
            {
                set(v);
                InvalidateVisual();
            };
        }
        return field;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty)
        {
            var target = IsPointerOver && _state == ImportStepState.Done && IsEnabled ? 1.0 : 0.0;
            if (ArcMotion.IsFull(this) && IsLoaded)
            {
                _ = Value(ref _hover, v => _hoverValue = v, _hoverValue).AnimateTo(target, MotionSpec.Tween(ImportWizardMotion.Standard, ImportWizardMotion.EaseOut));
            }
            else
            {
                _hoverValue = target;
                _hover?.Jump(target);
                InvalidateVisual();
            }
        }
        else if (change.Property == IsEnabledProperty)
        {
            Cursor = _state == ImportStepState.Done && IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayouts();
        var width = MarkSize + (_showLabel ? Gap + _labelLayout!.WidthIncludingTrailingWhitespace : 0);
        return new Size(width, Height32);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    private void EnsureLayouts(Color? labelColor = null, Color? numberColor = null)
    {
        var family = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
        // The layouts keep their brush; they are rebuilt only when the color changes (during a 240ms state change).
        if (labelColor is { } label && label.ToUInt32() != _labelColor)
        {
            _labelLayout?.Dispose();
            _labelLayout = null;
            _labelColor = label.ToUInt32();
        }
        if (numberColor is { } number && number.ToUInt32() != _numberColor)
        {
            _numberLayout?.Dispose();
            _numberLayout = null;
            _numberColor = number.ToUInt32();
        }
        _labelLayout ??= new TextLayout(_label, new Typeface(family), 14, Brush(Color.FromUInt32(_labelColor)), lineHeight: 19.6);
        _numberLayout ??= new TextLayout(_number, new Typeface(family), 12, Brush(Color.FromUInt32(_numberColor)),
            fontFeatures: ImportWizardUi.TabularNumbers);
    }

    public override void Render(DrawingContext context)
    {
        EnsureLayouts();
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        var strong = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
        var onAccent = ArcTokens.GetColor(this, ArcDesignTokenKind.AccentForeground);
        var muted = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        var text = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        var secondary = ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary);
        var dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

        var top = (bounds.Height - MarkSize) / 2;
        var mark = new Rect(0, top, MarkSize, MarkSize);
        var center = mark.Center;
        var accented = Math.Max(_currentValue, _doneValue);

        // Hover tint behind a done mark (accent 13% dark, 10% light).
        if (_hoverValue > 0.001)
        {
            context.DrawEllipse(Brush(Oklch.WithAlpha(accent, (dark ? 0.13 : 0.10) * _hoverValue)), null, center, MarkSize / 2, MarkSize / 2);
        }
        // The current step's fill.
        if (_currentValue > 0.001)
        {
            context.DrawEllipse(Brush(Oklch.WithAlpha(accent, _currentValue)), null, center, MarkSize / 2, MarkSize / 2);
        }
        // The 1px edge (inside the 24px box).
        var edge = new Pen(Brush(Mix(strong, accent, accented)), 1);
        context.DrawEllipse(null, edge, center, MarkSize / 2 - 0.5, MarkSize / 2 - 0.5);

        // The number.
        if (_numberValue > 0.001)
        {
            EnsureLayouts(numberColor: Mix(muted, onAccent, _currentValue));
            using (context.PushOpacity(_numberValue))
            {
                var layout = _numberLayout!;
                layout.Draw(context, new Point(center.X - layout.WidthIncludingTrailingWhitespace / 2, center.Y - layout.Height / 2));
            }
        }
        // The check (13px, stroke 2.4 in the 24-unit glyph space), scaling from 0.5.
        if (_checkValue > 0.001)
        {
            var scale = 13.0 / 24 * (0.5 + 0.5 * _checkValue);
            var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y);
            using (context.PushOpacity(Math.Clamp(_checkValue, 0, 1)))
            using (context.PushTransform(matrix))
            {
                var pen = new Pen(Brush(accent), 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                context.DrawGeometry(null, pen, ArcIcons.Check);
            }
        }

        // The label.
        if (_showLabel)
        {
            var todo = 1 - accented;
            var labelColor = Mix(Mix(Mix(muted, text, _currentValue), secondary, _doneValue * (1 - _currentValue)), text, _hoverValue);
            if (todo > 0.999)
            {
                labelColor = muted;
            }
            EnsureLayouts(labelColor: labelColor);
            var layout = _labelLayout!;
            layout.Draw(context, new Point(MarkSize + Gap, (bounds.Height - layout.Height) / 2));
        }

        if (Classes.Contains(":focus-visible"))
        {
            var ring = new Pen(Brush(ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing)), 2);
            var ringRect = (bounds.Width > MarkSize + 1 ? bounds.Deflate(new Thickness(0, 2)) : mark).Inflate(3);
            context.DrawRectangle(null, ring, ringRect, ringRect.Height / 2, ringRect.Height / 2);
        }
    }

    private IBrush Brush(Color color)
    {
        var key = color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            if (_brushes.Count > 256)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    private static Color Mix(Color a, Color b, double t) => t <= 0 ? a : t >= 1 ? b : Oklch.MixOklab(a, b, t);
}

/// <summary>
/// The line before a step: a 2px track in the border color with an accent fill that grows from the left (scaleX) on the
/// smooth spring when the step is reached and empties when it is left.
/// </summary>
internal sealed class ImportWizardConnector : Control
{
    private MotionValue<double>? _fill;
    private double _value;

    static ImportWizardConnector()
    {
        AffectsRender<ImportWizardConnector>(IsVisibleProperty);
    }

    public ImportWizardConnector()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        Height = 2;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
    }

    /// <summary>The drawn fill, 0–1 (tests).</summary>
    public double Fill => _value;

    /// <summary>Fills (1) or empties (0) the line.</summary>
    public void SetFilled(bool filled, bool animate)
    {
        var target = filled ? 1.0 : 0.0;
        if (!animate || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            _value = target;
            _fill?.Jump(target);
            InvalidateVisual();
            return;
        }
        if (_fill is null)
        {
            _fill = new MotionValue<double>(this, _value, MotionVectors.Double);
            _fill.Changed += v =>
            {
                _value = v;
                InvalidateVisual();
            };
        }
        _ = _fill.AnimateTo(target, MotionSpec.Spring(ImportWizardMotion.Smooth));
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        context.DrawRectangle(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Border)), null, bounds, radius, radius);
        if (_value > 0.0005)
        {
            var fill = new Rect(0, 0, bounds.Width * Math.Clamp(_value, 0, 1), bounds.Height);
            using (context.PushClip(new RoundedRect(bounds, radius)))
            {
                context.DrawRectangle(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Accent)), null, fill, radius, radius);
            }
        }
    }
}

/// <summary>
/// The step bar of <see cref="ImportWizard"/>: four steps joined by three lines that share the room left by the buttons
/// (12 on each side, at least 16 long). Only done steps take focus and jump back. Compact: "Step N of 4" (N rolls) at the
/// left and the four marks without labels at the right, joined by 16px lines 6 from each mark.
/// </summary>
internal sealed class ImportWizardStepper : Panel
{
    private const double ConnectorMargin = 12;
    private const double CompactConnectorMargin = 6;
    private const double ConnectorMin = 16;

    private readonly ImportWizardStepButton[] _buttons = new ImportWizardStepButton[4];
    private readonly ImportWizardConnector[] _connectors = new ImportWizardConnector[3];
    private readonly ImportWizardCountText _counter;
    private ImportWizardStep _step;
    private bool _compact;

    public ImportWizardStepper()
    {
        _counter = new ImportWizardCountText(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, FontWeight.Medium)
        {
            IsVisible = false,
        };
        AutomationProperties.SetAccessibilityView(_counter, AccessibilityView.Raw);
        Children.Add(_counter);
        for (var i = 0; i < 4; i++)
        {
            if (i > 0)
            {
                _connectors[i - 1] = new ImportWizardConnector();
                Children.Add(_connectors[i - 1]);
            }
            var button = new ImportWizardStepButton { Index = i };
            var index = i;
            button.Click += (_, e) =>
            {
                e.Handled = true;
                StepInvoked?.Invoke((ImportWizardStep)index);
            };
            _buttons[i] = button;
            Children.Add(button);
        }
        Refresh();
    }

    /// <summary>Raised when a done step is pressed.</summary>
    public event Action<ImportWizardStep>? StepInvoked;

    /// <summary>The step buttons (tests).</summary>
    public IReadOnlyList<ImportWizardStepButton> Buttons => _buttons;

    /// <summary>The lines between the steps (tests).</summary>
    public IReadOnlyList<ImportWizardConnector> Connectors => _connectors;

    /// <summary>The compact "Step N of 4" line (tests).</summary>
    public ImportWizardCountText Counter => _counter;

    private ImportWizardLabels Labels => this.FindAncestorOfType<ImportWizard>()?.Labels ?? s_defaultLabels;

    private static readonly ImportWizardLabels s_defaultLabels = new();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    /// <summary>Re-reads the labels.</summary>
    public void Refresh()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.StepsName);
        for (var i = 0; i < 4; i++)
        {
            var name = i < labels.StepNames.Count ? labels.StepNames[i] : ((ImportWizardStep)i).ToString();
            _buttons[i].SetText(name, i + 1);
            AutomationProperties.SetName(_buttons[i], labels.StepButtonName(name, i + 1, 4, _buttons[i].State == ImportStepState.Done));
        }
        _counter.IsInstant = true;
        _counter.Set(labels.StepCounter, (int)_step + 1, 4);
    }

    /// <summary>Shows <paramref name="step"/> as current; <paramref name="importDone"/> marks the last step done too.</summary>
    public void SetState(ImportWizardStep step, bool importDone, bool navigable, bool animate)
    {
        _step = step;
        var labels = Labels;
        for (var i = 0; i < 4; i++)
        {
            var state = i < (int)step || (importDone && i == 3) ? ImportStepState.Done
                : i == (int)step ? ImportStepState.Current : ImportStepState.Todo;
            var button = _buttons[i];
            button.IsEnabled = navigable && state == ImportStepState.Done && i < (int)step;
            button.SetState(state, animate);
            var name = i < labels.StepNames.Count ? labels.StepNames[i] : ((ImportWizardStep)i).ToString();
            AutomationProperties.SetName(button, labels.StepButtonName(name, i + 1, 4, state == ImportStepState.Done));
            AutomationProperties.SetItemStatus(button, state == ImportStepState.Current ? "current step" : null);
        }
        for (var i = 0; i < 3; i++)
        {
            _connectors[i].SetFilled(i + 1 <= (int)step, animate);
        }
        _counter.IsInstant = !animate;
        _counter.Set(labels.StepCounter, (int)step + 1, 4);
    }

    /// <summary>Switches the compact layout.</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
        {
            return;
        }
        _compact = compact;
        _counter.IsVisible = compact;
        foreach (var button in _buttons)
        {
            button.SetLabelVisible(!compact);
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double buttons = 0;
        double height = 32;
        foreach (var button in _buttons)
        {
            button.Measure(Size.Infinity);
            buttons += button.DesiredSize.Width;
        }
        foreach (var connector in _connectors)
        {
            connector.Measure(Size.Infinity);
        }
        _counter.Measure(Size.Infinity);
        if (_compact)
        {
            var marks = buttons + 3 * (ConnectorMin + 2 * CompactConnectorMargin);
            var width = double.IsInfinity(availableSize.Width) ? _counter.DesiredSize.Width + 16 + marks : availableSize.Width;
            return new Size(width, height);
        }
        var natural = buttons + 3 * (ConnectorMin + 2 * ConnectorMargin);
        return new Size(double.IsInfinity(availableSize.Width) ? natural : Math.Max(natural, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var height = finalSize.Height;
        if (_compact)
        {
            _counter.Arrange(new Rect(0, (height - _counter.DesiredSize.Height) / 2, _counter.DesiredSize.Width, _counter.DesiredSize.Height));
            var marks = 0.0;
            foreach (var button in _buttons)
            {
                marks += button.DesiredSize.Width;
            }
            marks += 3 * (ConnectorMin + 2 * CompactConnectorMargin);
            var x = Math.Max(0, finalSize.Width - marks);
            Place(x, ConnectorMin, CompactConnectorMargin, height);
            return finalSize;
        }
        _counter.Arrange(default);
        var used = 0.0;
        foreach (var button in _buttons)
        {
            used += button.DesiredSize.Width;
        }
        var share = Math.Max(ConnectorMin, (finalSize.Width - used - 3 * 2 * ConnectorMargin) / 3);
        Place(0, share, ConnectorMargin, height);
        return finalSize;
    }

    private void Place(double x, double connector, double margin, double height)
    {
        for (var i = 0; i < 4; i++)
        {
            if (i > 0)
            {
                x += margin;
                _connectors[i - 1].Arrange(new Rect(x, (height - 2) / 2, connector, 2));
                x += connector + margin;
            }
            var button = _buttons[i];
            button.Arrange(new Rect(x, 0, button.DesiredSize.Width, height));
            x += button.DesiredSize.Width;
        }
    }
}
