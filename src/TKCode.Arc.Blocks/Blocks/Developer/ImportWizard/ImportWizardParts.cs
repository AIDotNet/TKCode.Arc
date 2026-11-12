using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Builders for the code-made parts of <see cref="ImportWizard"/>, bound to ArcDesign and ImportWizard tokens.</summary>
internal static class ImportWizardUi
{
    public static readonly FontFeatureCollection TabularNumbers = FontFeatureCollection.Parse("+tnum");

    /// <summary>A text block with token-bound size, color and body font (the card sets Arc's −0.14 letter spacing for all text).</summary>
    public static TextBlock Text(string? text, ArcDesignTokenKind size, ArcDesignTokenKind color,
        FontWeight weight = FontWeight.Normal, double lineHeight = double.NaN, bool tabular = false)
    {
        var block = new TextBlock { Text = text, FontWeight = weight, LineHeight = lineHeight };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        if (tabular)
        {
            block.FontFeatures = TabularNumbers;
        }
        return block;
    }

    /// <summary>A Lucide glyph in a token color.</summary>
    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color, double stroke = 2)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = stroke, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>Binds a property to an ImportWizard token.</summary>
    public static void Bind(StyledElement target, AvaloniaProperty property, ImportWizardTokenKind kind) =>
        target.Bind(property, target.GetResourceObservable(ImportWizardTokens.Key(kind)));

    /// <summary>A step heading: the title (22 / 24.2, medium, −0.22) over a subtitle, 4 apart.</summary>
    public static StackPanel Heading(string title, Control subtitle)
    {
        // 24 rather than Arc's 24.2: layout rounding would make it 25 and push every step body a pixel down.
        var block = Text(title, ArcDesignTokenKind.TypeXL, ArcDesignTokenKind.Foreground, FontWeight.Medium, 24);
        block.LetterSpacing = -0.22;
        AutomationProperties.SetHeadingLevel(block, 2);
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(block);
        panel.Children.Add(subtitle);
        return panel;
    }

    /// <summary>A subtitle line (14 / 19.6, secondary text).</summary>
    public static TextBlock Subtitle(string? text) =>
        Text(text, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);

    /// <summary>A bordered box (1px Border, the given radius, clipped) around <paramref name="child"/>.</summary>
    public static Border Box(Control? child, ArcDesignTokenKind radius)
    {
        var box = new Border { Child = child, ClipToBounds = true }.BindCornerRadius(radius);
        ArcTokens.Bind(box, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(box, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        return box;
    }

    /// <summary>A 1px divider line (BorderSubtle) above (or below) a part.</summary>
    public static void TopLine(Border border, ArcDesignTokenKind color = ArcDesignTokenKind.BorderSubtle)
    {
        ArcTokens.Bind(border, Border.BorderBrushProperty, color);
        ArcTokens.Bind(border, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
    }

    /// <summary>A whole number with group separators in the current culture.</summary>
    public static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
}

/// <summary>
/// A sentence with numbers that roll in place ("{0} of {1} columns mapped"): the literal parts are text blocks and every
/// <c>{n}</c> placeholder is a <see cref="RollingDigits"/> strip (one line tall, digits clipped, no fade) that follows its
/// value on the quick spring and keeps chasing a target that moves, which makes counts cascade. Changing the format (a
/// plural) keeps the strips, so the numbers still roll. The accessible name is the plain sentence.
/// </summary>
internal class ImportWizardCountText : StackPanel
{
    private readonly List<RollingDigits> _digits = new();
    private readonly ArcDesignTokenKind _size;
    private readonly ArcDesignTokenKind _color;
    private readonly FontWeight _weight;
    private string _format = string.Empty;
    private double[] _values = [];
    private IBrush? _foreground;

    public ImportWizardCountText(ArcDesignTokenKind size, ArcDesignTokenKind color, FontWeight weight = FontWeight.Normal)
    {
        _size = size;
        _color = color;
        _weight = weight;
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>The plain sentence (screen readers, tests).</summary>
    public string PlainText { get; private set; } = string.Empty;

    /// <summary>The format shown.</summary>
    public string Format => _format;

    /// <summary>When true the next values jump instead of rolling (a view opening with its final numbers).</summary>
    public bool IsInstant { get; set; }

    /// <summary>Overrides the token color (a level color), or null to use the token.</summary>
    public IBrush? ForegroundOverride
    {
        get => _foreground;
        set
        {
            _foreground = value;
            foreach (var child in Children)
            {
                Apply(child);
            }
        }
    }

    /// <summary>Shows <paramref name="format"/> with <paramref name="values"/> in its placeholders.</summary>
    public void Set(string format, params double[] values)
    {
        _values = values;
        if (format != _format)
        {
            _format = format;
            Rebuild();
        }
        for (var i = 0; i < _digits.Count && i < values.Length; i++)
        {
            _digits[i].IsInstant = IsInstant;
            _digits[i].Value = values[i];
        }
        var args = values.Select(v => (object)v.ToString("N0", CultureInfo.CurrentCulture)).ToArray();
        PlainText = DeveloperText.Format(format, args);
        AutomationProperties.SetName(this, PlainText);
    }

    private void Rebuild()
    {
        // Keep the strips by placeholder index so their values roll across a change of words.
        var old = _digits.ToArray();
        _digits.Clear();
        Children.Clear();
        var format = _format;
        var i = 0;
        var literal = new System.Text.StringBuilder();
        while (i < format.Length)
        {
            if (format[i] == '{' && i + 2 < format.Length && char.IsAsciiDigit(format[i + 1]))
            {
                var close = format.IndexOf('}', i);
                if (close > i && int.TryParse(format.AsSpan(i + 1, close - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    Flush(literal);
                    while (_digits.Count <= index)
                    {
                        _digits.Add(_digits.Count < old.Length ? old[_digits.Count] : CreateDigits());
                    }
                    var digits = _digits[index];
                    if (digits.Parent is Panel parent)
                    {
                        parent.Children.Remove(digits);
                    }
                    Children.Add(digits);
                    i = close + 1;
                    continue;
                }
            }
            literal.Append(format[i]);
            i++;
        }
        Flush(literal);
    }

    private void Flush(System.Text.StringBuilder literal)
    {
        if (literal.Length == 0)
        {
            return;
        }
        var block = CreateLiteral(literal.ToString());
        Apply(block);
        Children.Add(block);
        literal.Clear();
    }

    /// <summary>Makes the text block of a literal part.</summary>
    protected virtual Control CreateLiteral(string text)
    {
        var block = ImportWizardUi.Text(text, _size, _color, _weight);
        block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    /// <summary>Makes the rolling strip of a placeholder.</summary>
    protected virtual RollingDigits CreateDigits()
    {
        var digits = new RollingDigits
        {
            FontWeight = _weight,
            Falloff = RollingDigitsFalloff.None,
            WheelMotion = MotionSpec.Spring(ImportWizardMotion.Quick),
            GlyphPitch = 1.4,
            IsArmed = false,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        ArcTokens.Bind(digits, RollingDigits.FontSizeProperty, _size);
        ArcTokens.Bind(digits, RollingDigits.ForegroundProperty, _color);
        ArcTokens.Bind(digits, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        Apply(digits);
        return digits;
    }

    private void Apply(Control child)
    {
        if (_foreground is null)
        {
            return;
        }
        switch (child)
        {
            case TextBlock block:
                block.Foreground = _foreground;
                break;
            case RollingDigits digits:
                digits.Foreground = _foreground;
                break;
        }
    }
}

/// <summary>
/// One step of <see cref="ImportWizard"/>: padding 24 (16 compact), the heading, the body and the footer 20 apart (the
/// footer 8 more). Built in code from the wizard's <see cref="ImportWizard.Session"/>; a new view is made on every visit,
/// so the state it shows lives in the session and the wizard.
/// </summary>
internal abstract class ImportWizardStepView : Border
{
    private readonly StackPanel _stack;
    private readonly Control?[] _slots = new Control?[3];
    private bool _compact;

    protected ImportWizardStepView(ImportWizard owner)
    {
        Owner = owner;
        // A stack, not a grid: the 20 gap only goes between the parts a step has (the import step has no heading or footer).
        _stack = new StackPanel();
        ImportWizardUi.Bind(_stack, StackPanel.SpacingProperty, ImportWizardTokenKind.StepSpacing);
        Child = _stack;
        _compact = owner.IsCompact;
        ApplyPadding();
    }

    /// <summary>The wizard.</summary>
    public ImportWizard Owner { get; }

    /// <summary>The wizard's session.</summary>
    public ImportWizardSession Session => Owner.Session;

    /// <summary>The wizard's words.</summary>
    public ImportWizardLabels Labels => Owner.Labels;

    /// <summary>Which step this view shows.</summary>
    public abstract ImportWizardStep Step { get; }

    /// <summary>True in the compact layout.</summary>
    public bool IsCompact => _compact;

    /// <summary>True when motion plays in full (not reduced, not off, attached).</summary>
    public bool IsMotionFull => ArcMotion.IsFull(Owner) && Owner.IsLoaded;

    /// <summary>Places the heading.</summary>
    protected void SetHead(Control head) => SetRow(head, 0);

    /// <summary>Places the body.</summary>
    protected void SetBody(Control? body) => SetRow(body, 1);

    /// <summary>Places the footer (8 below the body's gap, like Arc's padding-top).</summary>
    protected void SetFooter(Control? footer)
    {
        if (footer is not null)
        {
            footer.Margin = new Thickness(0, 8, 0, 0);
        }
        SetRow(footer, 2);
    }

    private void SetRow(Control? control, int row)
    {
        _slots[row] = control;
        _stack.Children.Clear();
        foreach (var slot in _slots)
        {
            if (slot is not null)
            {
                _stack.Children.Add(slot);
            }
        }
    }

    /// <summary>Called once the view became the shown step; <paramref name="animate"/> is false on the first show.</summary>
    public virtual void OnEntered(bool animate)
    {
    }

    /// <summary>Called when another view replaces this one (stop timers, drop subscriptions).</summary>
    public virtual void OnLeaving()
    {
    }

    /// <summary>Called when the labels, the nouns or the sample changed.</summary>
    public virtual void Refresh()
    {
    }

    /// <summary>Called when the compact layout turned on or off.</summary>
    protected virtual void OnCompactChanged(bool compact)
    {
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Owner.CompactChanged += OnOwnerCompactChanged;
        if (_compact != Owner.IsCompact)
        {
            OnOwnerCompactChanged();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Owner.CompactChanged -= OnOwnerCompactChanged;
    }

    private void OnOwnerCompactChanged()
    {
        if (_compact == Owner.IsCompact)
        {
            return;
        }
        _compact = Owner.IsCompact;
        ApplyPadding();
        OnCompactChanged(_compact);
    }

    private void ApplyPadding() =>
        ImportWizardUi.Bind(this, PaddingProperty, _compact ? ImportWizardTokenKind.CompactStepPadding : ImportWizardTokenKind.StepPadding);
}

/// <summary>
/// Holds the shown step and, during a change, the one leaving. Both run at once on the smooth spring: the leaving view
/// slides 28 against the direction of travel, fades and blurs to 4px; the entering one comes from 28 the other way,
/// fades in and sharpens. The leaving view overlaps (it takes no space), so the deck measures as the entering view and
/// the wizard's height spring follows it. Reduced motion keeps the fade and blur without travel; Off swaps at once.
/// Code-driven because the direction and the overlap depend on which step comes next.
/// </summary>
internal sealed class ImportWizardDeck : Panel
{
    private Control? _leaving;
    private MotionValue<double>? _progress;
    private bool _forward;
    private bool _travel;

    /// <summary>The shown view.</summary>
    public Control? Current { get; private set; }

    /// <summary>True while a change plays.</summary>
    public bool IsMoving => _leaving is not null;

    /// <summary>Shows <paramref name="view"/>; <paramref name="forward"/> is the direction of travel.</summary>
    public void Show(Control view, bool forward, bool animate)
    {
        if (_leaving is not null)
        {
            Children.Remove(_leaving);
            _leaving = null;
        }
        var old = Current;
        Current = view;
        Children.Add(view);
        if (old is null || !animate || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            if (old is not null)
            {
                Children.Remove(old);
            }
            Reset(view);
            _progress?.Jump(1);
            return;
        }
        _leaving = old;
        _leaving.IsHitTestVisible = false;
        _forward = forward;
        _travel = ArcMotion.IsFull(this);
        _progress ??= CreateProgress();
        _progress.Jump(0);
        Apply(0);
        _ = Play();
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += Apply;
        return value;
    }

    private async Task Play()
    {
        var progress = _progress!;
        // Reduced motion still crossfades on the spring (MotionValue would jump a spring there), so run it as a tween then.
        var spec = _travel ? MotionSpec.Spring(ImportWizardMotion.Smooth) : MotionSpec.Tween(TimeSpan.FromMilliseconds(600), ImportWizardMotion.EaseOut);
        var done = await progress.AnimateTo(1, spec);
        if (done && _leaving is { } leaving)
        {
            Children.Remove(leaving);
            _leaving = null;
            if (Current is { } current)
            {
                Reset(current);
            }
        }
    }

    private void Apply(double p)
    {
        var sign = _forward ? 1 : -1;
        if (Current is { } current)
        {
            current.Opacity = p;
            Move(current, sign * ImportWizardMotion.StepTravel * (1 - p));
            current.Effect = ArcBlur.Get(ImportWizardMotion.StepBlur * (1 - p));
        }
        if (_leaving is { } leaving)
        {
            leaving.Opacity = 1 - p;
            Move(leaving, -sign * ImportWizardMotion.StepTravel * p);
            leaving.Effect = ArcBlur.Get(ImportWizardMotion.StepBlur * p);
        }
    }

    private void Move(Control view, double x)
    {
        if (!_travel)
        {
            return;
        }
        if (view.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            view.RenderTransform = translate;
        }
        translate.X = x;
    }

    private static void Reset(Control view)
    {
        view.Opacity = 1;
        view.RenderTransform = null;
        view.Effect = null;
        view.IsHitTestVisible = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size();
        foreach (var child in Children)
        {
            child.Measure(availableSize.WithHeight(double.PositiveInfinity));
            if (child == Current)
            {
                size = child.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child == Current ? finalSize.Height : child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>One side of an <see cref="ImportWizardSwap"/> change: where a part starts (enter) or ends (exit).</summary>
/// <param name="Scale">Scale at the far end (1 = none).</param>
/// <param name="Y">Vertical offset at the far end.</param>
/// <param name="Blur">Blur radius at the far end.</param>
/// <param name="Spring">The spring that drives it.</param>
internal readonly record struct ImportSwapMotion(double Scale, double Y, double Blur, SpringSpec Spring);

/// <summary>
/// Swaps one part for another in place (the drop zone and the file card, the import progress and its summary): the
/// leaving part stays where it was drawn without taking room while it fades out with its own scale, offset and blur,
/// and the entering one fades in from its own. Each runs on its own spring. Reduced motion keeps only the fades (240ms);
/// Off swaps at once. Code-driven because each side has its own spring and the leaving part must overlap.
/// </summary>
internal sealed class ImportWizardSwap : Panel
{
    private readonly Dictionary<Control, (MotionValue<double> Value, ImportSwapMotion Motion, bool Entering)> _moving = new();

    /// <summary>The part shown.</summary>
    public Control? Current { get; private set; }

    /// <summary>True while a change plays (tests).</summary>
    public bool IsMoving => _moving.Count > 0;

    /// <summary>Shows <paramref name="next"/> (null shows nothing).</summary>
    public void Show(Control? next, ImportSwapMotion exit, ImportSwapMotion enter, bool animate)
    {
        var old = Current;
        if (ReferenceEquals(old, next))
        {
            return;
        }
        Current = next;
        if (next is not null && !Children.Contains(next))
        {
            Children.Add(next);
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || !IsLoaded)
        {
            if (old is not null)
            {
                Stop(old);
                Children.Remove(old);
            }
            if (next is not null)
            {
                Stop(next);
                Rest(next);
            }
            InvalidateMeasure();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        if (old is not null)
        {
            old.IsHitTestVisible = false;
            Run(old, exit, entering: false, full);
        }
        if (next is not null)
        {
            next.IsHitTestVisible = true;
            Run(next, enter, entering: true, full);
        }
        InvalidateMeasure();
    }

    private void Run(Control part, ImportSwapMotion motion, bool entering, bool full)
    {
        MotionValue<double> value;
        if (_moving.TryGetValue(part, out var running))
        {
            value = running.Value;
        }
        else
        {
            // 0 = at rest, 1 = at the far end (gone, or not yet in).
            value = new MotionValue<double>(this, entering ? 1 : 0, MotionVectors.Double);
            value.Changed += v => Apply(part, v);
        }
        _moving[part] = (value, motion, entering);
        Apply(part, value.Value);
        var spec = full ? MotionSpec.Spring(motion.Spring) : MotionSpec.Tween(ImportWizardMotion.Standard, ImportWizardMotion.EaseOut);
        _ = Finish(part, value, value.AnimateTo(entering ? 0 : 1, spec), entering);
    }

    private async Task Finish(Control part, MotionValue<double> value, Task<bool> run, bool entering)
    {
        var done = await run;
        if (!done || !_moving.TryGetValue(part, out var state) || state.Value != value)
        {
            return;
        }
        _moving.Remove(part);
        if (entering)
        {
            Rest(part);
        }
        else if (!ReferenceEquals(part, Current))
        {
            Children.Remove(part);
            Rest(part);
        }
    }

    private void Apply(Control part, double far)
    {
        if (!_moving.TryGetValue(part, out var state))
        {
            return;
        }
        var motion = state.Motion;
        var full = ArcMotion.IsFull(this);
        part.Opacity = 1 - far;
        if (full)
        {
            var scale = 1 + (motion.Scale - 1) * far;
            if (part.RenderTransform is not MatrixTransform matrix)
            {
                matrix = new MatrixTransform();
                part.RenderTransform = matrix;
            }
            matrix.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, motion.Y * far);
            part.Effect = ArcBlur.Get(motion.Blur * far);
        }
    }

    private void Stop(Control part)
    {
        if (_moving.Remove(part, out var state))
        {
            state.Value.Jump(0);
        }
    }

    private static void Rest(Control part)
    {
        part.Opacity = 1;
        part.RenderTransform = null;
        part.Effect = null;
        part.IsHitTestVisible = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size();
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            if (child == Current)
            {
                size = child.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child == Current ? finalSize.Height : child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// A step's footer: the secondary button (or nothing) at the left and the primary at the right, 12 apart. Compact (Arc's
/// phone layout) stretches the primary over the rest of the row.
/// </summary>
internal sealed class ImportWizardFooter : Panel
{
    private const double Gap = 12;
    private Control? _left;
    private readonly Control _primary;
    private bool _compact;

    public ImportWizardFooter(Control primary, Control? left = null)
    {
        _primary = primary;
        // The footer places both buttons itself; alignment inside their slots would undo the stretch.
        primary.HorizontalAlignment = HorizontalAlignment.Stretch;
        Children.Add(primary);
        Left = left;
    }

    /// <summary>The secondary button at the left, or null.</summary>
    public Control? Left
    {
        get => _left;
        set
        {
            if (ReferenceEquals(_left, value))
            {
                return;
            }
            if (_left is not null)
            {
                Children.Remove(_left);
            }
            _left = value;
            if (value is not null)
            {
                value.HorizontalAlignment = HorizontalAlignment.Stretch;
                // First child, so Tab reaches it before the primary.
                Children.Insert(0, value);
            }
            InvalidateMeasure();
        }
    }

    /// <summary>Stretches the primary (compact layout).</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _left?.Measure(availableSize);
        var leftWidth = _left?.DesiredSize.Width ?? 0;
        _primary.Measure(availableSize);
        var height = Math.Max(_primary.DesiredSize.Height, _left?.DesiredSize.Height ?? 0);
        var width = double.IsInfinity(availableSize.Width) ? leftWidth + Gap + _primary.DesiredSize.Width : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var leftWidth = 0.0;
        if (_left is not null)
        {
            leftWidth = _left.DesiredSize.Width;
            _left.Arrange(new Rect(0, (finalSize.Height - _left.DesiredSize.Height) / 2, leftWidth, _left.DesiredSize.Height));
        }
        var primaryWidth = _compact
            ? Math.Max(_primary.DesiredSize.Width, finalSize.Width - (_left is null ? 0 : leftWidth + Gap))
            : _primary.DesiredSize.Width;
        _primary.Arrange(new Rect(finalSize.Width - primaryWidth, (finalSize.Height - _primary.DesiredSize.Height) / 2,
            primaryWidth, _primary.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>Buttons in centered rows, 8 apart, that wrap when they do not fit (the summary's actions on a phone).</summary>
internal sealed class ImportWizardCenteredWrap : Panel
{
    private const double Gap = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0, row = 0, rowHeight = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var w = child.DesiredSize.Width;
            if (row > 0 && row + Gap + w > availableSize.Width)
            {
                width = Math.Max(width, row);
                height += rowHeight + Gap;
                row = 0;
                rowHeight = 0;
            }
            row += (row > 0 ? Gap : 0) + w;
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }
        width = Math.Max(width, row);
        height += rowHeight;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rows = new List<List<Control>>();
        var current = new List<Control>();
        double row = 0;
        foreach (var child in Children)
        {
            var w = child.DesiredSize.Width;
            if (current.Count > 0 && row + Gap + w > finalSize.Width)
            {
                rows.Add(current);
                current = new List<Control>();
                row = 0;
            }
            row += (current.Count > 0 ? Gap : 0) + w;
            current.Add(child);
        }
        if (current.Count > 0)
        {
            rows.Add(current);
        }
        var y = 0.0;
        foreach (var line in rows)
        {
            var lineWidth = line.Sum(c => c.DesiredSize.Width) + Gap * (line.Count - 1);
            var lineHeight = line.Max(c => c.DesiredSize.Height);
            var x = (finalSize.Width - lineWidth) / 2;
            foreach (var child in line)
            {
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, lineHeight));
                x += child.DesiredSize.Width + Gap;
            }
            y += lineHeight + Gap;
        }
        return finalSize;
    }
}
