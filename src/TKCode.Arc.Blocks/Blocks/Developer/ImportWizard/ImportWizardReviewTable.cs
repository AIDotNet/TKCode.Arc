using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

/// <summary>The glyph in front of a review cell's value.</summary>
internal enum ImportReviewIcon
{
    None,

    /// <summary>The 13px circle-alert in Danger (the cell has a problem).</summary>
    Alert,

    /// <summary>The 13px check in Success (the cell was just fixed).</summary>
    Check
}

/// <summary>The tint behind a review cell.</summary>
internal enum ImportReviewTint
{
    None,

    /// <summary>Danger at 7% (a problem).</summary>
    Danger,

    /// <summary>Success at 10% (just fixed; held for 900ms).</summary>
    Success
}

/// <summary>
/// The colors, faces and brushes of the review table, read from the ArcDesign tokens once per theme (the table refreshes it
/// when the theme variant or the resources change and bumps <see cref="Version"/> so the parts rebuild their text).
/// </summary>
internal sealed class ImportWizardReviewPalette
{
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<uint, Pen> _pens = new();

    public Color Foreground { get; private set; }

    public Color Muted { get; private set; }

    public Color Danger { get; private set; }

    public Color Success { get; private set; }

    public Color Border { get; private set; }

    public Color Divider { get; private set; }

    public Color Hover { get; private set; }

    public Color FocusRing { get; private set; }

    public Typeface Regular { get; private set; }

    public Typeface Medium { get; private set; }

    /// <summary>Changes on every refresh.</summary>
    public int Version { get; private set; }

    public void Refresh(Control owner)
    {
        Foreground = ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground);
        Muted = ArcTokens.GetColor(owner, ArcDesignTokenKind.TextMuted);
        Danger = ArcTokens.GetColor(owner, ArcDesignTokenKind.Danger);
        Success = ArcTokens.GetColor(owner, ArcDesignTokenKind.Success);
        Border = ArcTokens.GetColor(owner, ArcDesignTokenKind.Border);
        Divider = ArcTokens.GetColor(owner, ArcDesignTokenKind.BorderSubtle);
        Hover = ArcTokens.GetColor(owner, ArcDesignTokenKind.SurfaceMuted);
        FocusRing = ArcTokens.GetColor(owner, ArcDesignTokenKind.FocusRing);
        var family = owner.TryFindResource(ArcDesignTokenKind.BodyFontFamily, owner.ActualThemeVariant, out var value) && value is FontFamily found
            ? found
            : FontFamily.Default;
        Regular = new Typeface(family);
        Medium = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
        _brushes.Clear();
        _pens.Clear();
        Version++;
    }

    /// <summary>A cached immutable brush of <paramref name="color"/>.</summary>
    public IBrush Brush(Color color)
    {
        var key = color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            if (_brushes.Count > 512)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    /// <summary>A cached pen (round caps and joins) of <paramref name="color"/>, <paramref name="thickness"/> wide.</summary>
    public Pen Pen(Color color, double thickness)
    {
        var key = color.ToUInt32() ^ (uint)(thickness * 1000) * 2654435761u;
        if (!_pens.TryGetValue(key, out var pen) || pen.Thickness != thickness)
        {
            if (_pens.Count > 256)
            {
                _pens.Clear();
            }
            pen = new Pen(Brush(color), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    /// <summary>Mixes two colors in straight RGBA; a fully transparent end takes the other end's hue.</summary>
    public static Color Mix(Color a, Color b, double t)
    {
        if (t <= 0)
        {
            return a;
        }
        if (t >= 1)
        {
            return b;
        }
        if (a.A == 0)
        {
            a = Color.FromArgb(0, b.R, b.G, b.B);
        }
        else if (b.A == 0)
        {
            b = Color.FromArgb(0, a.R, a.G, a.B);
        }
        static byte Lerp(byte x, byte y, double t) => (byte)Math.Clamp(Math.Round(x + (y - x) * t), 0, 255);
        return Color.FromArgb(Lerp(a.A, b.A, t), Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t));
    }

    /// <summary>Draws a 24-unit Lucide glyph at <paramref name="origin"/>, <paramref name="size"/> wide, stroke 2 in glyph units.</summary>
    public void Glyph(DrawingContext context, Geometry glyph, Point origin, double size, Color color, double stroke = 2)
    {
        var scale = size / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            context.DrawGeometry(null, Pen(color, stroke), glyph);
        }
    }
}

/// <summary>
/// The header row of the review table: "Line" and the field labels (12 / 16.8, medium, muted, padding 8 / 12) over a 1px
/// Border line; the actions column has no visible header. Drawn in one pass at the table's column widths.
/// </summary>
internal sealed class ImportWizardReviewHeader : Control
{
    /// <summary>
    /// Text height plus padding (8 + 16.8 + 8, which Arc's collapsed borders draw as 32 pixels); the line comes below.
    /// </summary>
    public const double ContentHeight = 32;

    private readonly ImportWizardReviewTable _table;
    private string[] _labels = [];
    private TextLayout?[] _layouts = [];
    private int _version = -1;

    public ImportWizardReviewHeader(ImportWizardReviewTable table)
    {
        _table = table;
        IsHitTestVisible = false;
    }

    /// <summary>The height the table gives it (text, padding and line).</summary>
    public double LayoutHeight => ContentHeight + 1;

    /// <summary>The labels: the line column first, then the fields.</summary>
    public void SetLabels(IReadOnlyList<string> labels)
    {
        _labels = labels.ToArray();
        Dispose();
        _layouts = new TextLayout?[_labels.Length];
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>How wide column <paramref name="index"/> (0 = line) would be for its label alone.</summary>
    public double NaturalWidth(int index) => Layout(index) is { } layout ? layout.WidthIncludingTrailingWhitespace + 24 : 24;

    private TextLayout? Layout(int index)
    {
        var palette = _table.Palette;
        if (_version != palette.Version)
        {
            Dispose();
            _layouts = new TextLayout?[_labels.Length];
            _version = palette.Version;
        }
        if (index < 0 || index >= _labels.Length)
        {
            return null;
        }
        return _layouts[index] ??= new TextLayout(_labels[index], palette.Medium, 12, palette.Brush(palette.Muted), lineHeight: 16.8,
            letterSpacing: -0.12);
    }

    private void Dispose()
    {
        foreach (var layout in _layouts)
        {
            layout?.Dispose();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(_table.TotalWidth, LayoutHeight);

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        var widths = _table.Widths;
        var x = 0.0;
        for (var i = 0; i < _labels.Length && i < widths.Length; i++)
        {
            var layout = Layout(i);
            if (layout is not null)
            {
                using (context.PushClip(new Rect(x, 0, Math.Max(0, widths[i]), Bounds.Height)))
                {
                    layout.Draw(context, new Point(x + 12, 8));
                }
            }
            x += widths[i];
        }
        context.FillRectangle(palette.Brush(palette.Border), new Rect(0, Bounds.Height - 1, Bounds.Width, 1));
    }
}

/// <summary>
/// One field of a review row: a full-cell button (padding 6 / 12, 40 tall at least) drawn in one pass. The value is 14 /
/// 19.6 on one line with an ellipsis ("Blank" in the muted color when empty). A cell with a problem has a Danger 7% tint,
/// a 13px circle-alert before the value and the message on a second line (12 / 16.8, Danger), so its row grows from 41 to
/// 49.4. When clean-up or an edit fixes it, the tint turns to Success 10% in 240ms and a check replaces the alert; the
/// review step ends that glow after 900ms. Click, Enter or Space asks the review step to edit it. Drawn in code because a
/// table has a cell per row and field (a template per cell would be five controls each).
/// </summary>
internal sealed class ImportWizardReviewCell : AvaloniaButton
{
    public const double PaddingX = 12;
    public const double PaddingY = 6;
    public const double IconSize = 13;
    public const double IconGap = 6;
    public const double ValueLine = 19.6;
    public const double ErrorLine = 16.8;
    public const double MinHeight40 = 40;
    public const double EditorMinWidth = 140;

    private static Cursor? s_cursor;

    private readonly ImportWizardReviewTable _table;
    private string _value = string.Empty;
    private string? _problem;
    private string _fieldLabel;
    private ImportReviewIcon _icon;
    private ImportReviewTint _tint;
    private Color _tintFrom;
    private double _tintProgress = 1;
    private MotionValue<double>? _tintMotion;
    private bool _editing;
    private TextLayout? _valueLayout;
    private TextLayout? _valueTrimmed;
    private double _valueTrimmedWidth = -1;
    private TextLayout? _errorLayout;
    private TextLayout? _errorTrimmed;
    private double _errorTrimmedWidth = -1;
    private int _version = -1;

    static ImportWizardReviewCell()
    {
        FocusableProperty.OverrideDefaultValue<ImportWizardReviewCell>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ImportWizardReviewCell>(false);
    }

    public ImportWizardReviewCell(ImportWizardReviewTable table, ImportReviewRow row, int field, string fieldLabel)
    {
        _table = table;
        Row = row;
        Field = field;
        _fieldLabel = fieldLabel;
        Background = Brushes.Transparent;
        FocusAdorner = null;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Cursor = s_cursor ??= new Cursor(StandardCursorType.Ibeam);
        Click += (_, _) => _table.Owner.OnCellInvoked(this);
        // A cell that was fixed on an earlier visit does not glow again.
        Row.Fixed[field] = false;
        Sync(animate: false);
    }

    /// <summary>The row of the session.</summary>
    public ImportReviewRow Row { get; }

    /// <summary>The field position (in <see cref="ImportWizardSession.Fields"/> order).</summary>
    public int Field { get; }

    /// <summary>The value shown.</summary>
    public string Value => _value;

    /// <summary>The problem shown, or null.</summary>
    public string? Problem => _problem;

    /// <summary>The glyph shown.</summary>
    public ImportReviewIcon Icon => _icon;

    /// <summary>The tint the cell shows or moves to.</summary>
    public ImportReviewTint Tint => _tint;

    /// <summary>The field's label (accessible names).</summary>
    public string FieldLabel
    {
        get => _fieldLabel;
        set
        {
            _fieldLabel = value;
            UpdateName();
        }
    }

    /// <summary>True while the editor covers the cell (it draws only its tint and takes no focus).</summary>
    public bool IsEditing
    {
        get => _editing;
        set
        {
            if (_editing == value)
            {
                return;
            }
            _editing = value;
            Focusable = !value;
            IsHitTestVisible = !value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The height of the button part: 40, or 48.4 with the message line.</summary>
    public double ContentHeight => _problem is not null && !_editing ? PaddingY * 2 + ValueLine + ErrorLine : MinHeight40;

    /// <summary>
    /// How wide the column would be for this cell alone: padding, the 6px icon gap (Arc's grid keeps it with no icon), the
    /// icon and the wider of the two lines; at least 140 while editing.
    /// </summary>
    public double NaturalWidth
    {
        get
        {
            EnsureLayouts();
            var text = Math.Max(_valueLayout!.WidthIncludingTrailingWhitespace, _errorLayout?.WidthIncludingTrailingWhitespace ?? 0);
            var width = PaddingX * 2 + IconGap + (_icon == ImportReviewIcon.None ? 0 : IconSize) + text;
            return _editing ? Math.Max(width, EditorMinWidth) : width;
        }
    }

    /// <summary>
    /// Reads the row's value and problem again. Returns true when a problem the cell showed is gone because it was fixed
    /// (clean-up or an edit): the cell then glows (Success tint and check) until <see cref="EndGlow"/>.
    /// </summary>
    public bool Sync(bool animate)
    {
        var value = Row.Values[Field] ?? string.Empty;
        var problem = Row.Problems[Field];
        var hadProblem = _problem is not null;
        var fixedNow = hadProblem && problem is null && Row.Fixed[Field];
        var changed = value != _value || problem != _problem;
        _value = value;
        _problem = problem;
        if (changed)
        {
            DisposeLayouts();
        }
        if (problem is not null)
        {
            _icon = ImportReviewIcon.Alert;
            TintTo(ImportReviewTint.Danger, animate);
        }
        else if (fixedNow)
        {
            _icon = ImportReviewIcon.Check;
            TintTo(ImportReviewTint.Success, animate);
        }
        else if (hadProblem)
        {
            _icon = ImportReviewIcon.None;
            TintTo(ImportReviewTint.None, animate);
        }
        UpdateName();
        InvalidateMeasure();
        InvalidateVisual();
        return fixedNow;
    }

    /// <summary>Ends a glow: the tint fades back to nothing in 240ms and the check goes.</summary>
    public void EndGlow(bool animate)
    {
        if (_problem is not null || _tint != ImportReviewTint.Success)
        {
            return;
        }
        _icon = ImportReviewIcon.None;
        TintTo(ImportReviewTint.None, animate);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Rebuilds the text (labels or theme changed).</summary>
    public void Refresh()
    {
        DisposeLayouts();
        UpdateName();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdateName() => AutomationProperties.SetName(this, _table.Owner.Labels.CellName(_fieldLabel, Row.LineNumber, _problem));

    private void TintTo(ImportReviewTint target, bool animate)
    {
        if (_tint == target && _tintProgress >= 1)
        {
            return;
        }
        var palette = _table.Palette;
        _tintFrom = CurrentTint(palette);
        _tint = target;
        if (!animate || !IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _tintProgress = 1;
            _tintMotion?.Jump(1);
            InvalidateVisual();
            return;
        }
        _tintMotion ??= CreateTintMotion();
        _tintMotion.Jump(0);
        _ = _tintMotion.AnimateTo(1, MotionSpec.Tween(ImportWizardMotion.Standard, ImportWizardMotion.EaseOut));
    }

    private MotionValue<double> CreateTintMotion()
    {
        var motion = new MotionValue<double>(this, _tintProgress, MotionVectors.Double);
        motion.Changed += v =>
        {
            _tintProgress = v;
            InvalidateVisual();
        };
        return motion;
    }

    private Color TintColor(ImportWizardReviewPalette palette, ImportReviewTint tint) => tint switch
    {
        ImportReviewTint.Danger => Oklch.WithAlpha(palette.Danger, 0.07),
        ImportReviewTint.Success => Oklch.WithAlpha(palette.Success, 0.10),
        _ => Colors.Transparent,
    };

    private Color CurrentTint(ImportWizardReviewPalette palette) =>
        _tintProgress >= 1 ? TintColor(palette, _tint) : ImportWizardReviewPalette.Mix(_tintFrom, TintColor(palette, _tint), _tintProgress);

    private void EnsureLayouts()
    {
        var palette = _table.Palette;
        if (_version != palette.Version)
        {
            DisposeLayouts();
            _version = palette.Version;
        }
        if (_valueLayout is null)
        {
            var blank = _value.Length == 0;
            _valueLayout = ValueLayout(palette, blank, double.PositiveInfinity);
        }
        if (_errorLayout is null && _problem is not null)
        {
            _errorLayout = ErrorLayout(palette, double.PositiveInfinity);
        }
    }

    private TextLayout ValueLayout(ImportWizardReviewPalette palette, bool blank, double maxWidth)
    {
        var text = blank ? _table.Owner.Labels.Blank : _value.Replace('\n', ' ').Replace('\r', ' ');
        return new TextLayout(text, palette.Regular, 14, palette.Brush(blank ? palette.Muted : palette.Foreground),
            textTrimming: double.IsInfinity(maxWidth) ? TextTrimming.None : TextTrimming.CharacterEllipsis, maxWidth: maxWidth,
            lineHeight: ValueLine, letterSpacing: -0.14, maxLines: 1, fontFeatures: ImportWizardUi.TabularNumbers);
    }

    private TextLayout ErrorLayout(ImportWizardReviewPalette palette, double maxWidth) =>
        new(_problem ?? string.Empty, palette.Regular, 12, palette.Brush(palette.Danger),
            textTrimming: double.IsInfinity(maxWidth) ? TextTrimming.None : TextTrimming.CharacterEllipsis, maxWidth: maxWidth,
            lineHeight: ErrorLine, letterSpacing: -0.12, maxLines: 1, fontFeatures: ImportWizardUi.TabularNumbers);

    private void DisposeLayouts()
    {
        _valueLayout?.Dispose();
        _valueLayout = null;
        _valueTrimmed?.Dispose();
        _valueTrimmed = null;
        _valueTrimmedWidth = -1;
        _errorLayout?.Dispose();
        _errorLayout = null;
        _errorTrimmed?.Dispose();
        _errorTrimmed = null;
        _errorTrimmedWidth = -1;
    }

    protected override Size MeasureOverride(Size availableSize) => new(NaturalWidth, ContentHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;


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

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        var size = Bounds.Size;
        var whole = new Rect(size);
        context.FillRectangle(Brushes.Transparent, whole);
        var tint = CurrentTint(palette);
        if (tint.A > 0)
        {
            context.FillRectangle(palette.Brush(tint), whole);
        }
        if (_editing)
        {
            return;
        }
        EnsureLayouts();
        var contentHeight = ContentHeight;
        var top = Math.Max(0, (size.Height - contentHeight) / 2);
        var x = PaddingX;
        if (_icon != ImportReviewIcon.None)
        {
            var iconTop = top + PaddingY + (ValueLine - IconSize) / 2;
            if (_icon == ImportReviewIcon.Alert)
            {
                palette.Glyph(context, ArcIcons.CircleAlert, new Point(x, iconTop), IconSize, palette.Danger);
            }
            else
            {
                palette.Glyph(context, ArcIcons.Check, new Point(x, iconTop), IconSize, palette.Success);
            }
            x += IconSize + IconGap;
        }
        var room = Math.Max(0, size.Width - x - PaddingX);
        var valueTop = _problem is not null ? top + PaddingY : top + (contentHeight - ValueLine) / 2;
        FitValue(palette, room).Draw(context, new Point(x, valueTop));
        if (_problem is not null && _errorLayout is not null)
        {
            FitError(palette, room).Draw(context, new Point(x, top + PaddingY + ValueLine));
        }
        if (Classes.Contains(":focus-visible"))
        {
            var ring = new Rect(0, top, size.Width, contentHeight).Inflate(3);
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), ring);
        }
    }

    /// <summary>The value's layout, ending in an ellipsis when it is wider than <paramref name="room"/>.</summary>
    private TextLayout FitValue(ImportWizardReviewPalette palette, double room)
    {
        var natural = _valueLayout!;
        if (natural.WidthIncludingTrailingWhitespace <= room + 0.5)
        {
            return natural;
        }
        if (_valueTrimmed is null || Math.Abs(_valueTrimmedWidth - room) > 0.25)
        {
            _valueTrimmed?.Dispose();
            _valueTrimmed = ValueLayout(palette, _value.Length == 0, Math.Max(1, room));
            _valueTrimmedWidth = room;
        }
        return _valueTrimmed;
    }

    /// <summary>The message's layout, ending in an ellipsis when it is wider than <paramref name="room"/>.</summary>
    private TextLayout FitError(ImportWizardReviewPalette palette, double room)
    {
        var natural = _errorLayout!;
        if (natural.WidthIncludingTrailingWhitespace <= room + 0.5)
        {
            return natural;
        }
        if (_errorTrimmed is null || Math.Abs(_errorTrimmedWidth - room) > 0.25)
        {
            _errorTrimmed?.Dispose();
            _errorTrimmed = ErrorLayout(palette, Math.Max(1, room));
            _errorTrimmedWidth = room;
        }
        return _errorTrimmed;
    }
}

/// <summary>
/// The x at the end of a review row: a 30px circle with a 14px x in the muted color; hover fills it with SurfaceMuted and
/// turns the x to the text color (160ms ease-out). Drawn in code like the cells; the hover tween is a MotionValue because
/// the fill and the glyph cross-fade inside one drawing.
/// </summary>
internal sealed class ImportWizardReviewLeaveOut : AvaloniaButton
{
    public const double Size30 = 30;

    private static Cursor? s_cursor;

    private readonly ImportWizardReviewTable _table;
    private MotionValue<double>? _hover;
    private double _hoverValue;

    static ImportWizardReviewLeaveOut()
    {
        FocusableProperty.OverrideDefaultValue<ImportWizardReviewLeaveOut>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ImportWizardReviewLeaveOut>(false);
    }

    public ImportWizardReviewLeaveOut(ImportWizardReviewTable table, ImportReviewRow row)
    {
        _table = table;
        Row = row;
        Background = Brushes.Transparent;
        FocusAdorner = null;
        Cursor = s_cursor ??= new Cursor(StandardCursorType.Hand);
        Click += (_, _) => _table.Owner.OnLeaveOutInvoked(Row);
        Refresh();
    }

    /// <summary>The row it leaves out.</summary>
    public ImportReviewRow Row { get; }

    /// <summary>Applies the labels.</summary>
    public void Refresh()
    {
        var labels = _table.Owner.Labels;
        AutomationProperties.SetName(this, labels.LeaveOut(Row.LineNumber));
        Tooltip.SetTip(this, labels.LeaveOutTip);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size30, Size30);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty)
        {
            var target = IsPointerOver ? 1.0 : 0.0;
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                _hover ??= CreateHover();
                _ = _hover.AnimateTo(target, MotionSpec.Tween(ImportWizardMotion.Fast, ImportWizardMotion.EaseOut));
            }
            else
            {
                _hover?.Jump(target);
                _hoverValue = target;
                InvalidateVisual();
            }
        }
    }

    private MotionValue<double> CreateHover()
    {
        var motion = new MotionValue<double>(this, _hoverValue, MotionVectors.Double);
        motion.Changed += v =>
        {
            _hoverValue = v;
            InvalidateVisual();
        };
        return motion;
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

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        var bounds = new Rect(Bounds.Size);
        var center = bounds.Center;
        var radius = Math.Min(bounds.Width, bounds.Height) / 2;
        context.DrawEllipse(Brushes.Transparent, null, center, radius, radius);
        if (_hoverValue > 0.001)
        {
            context.DrawEllipse(palette.Brush(Oklch.WithAlpha(palette.Hover, _hoverValue)), null, center, radius, radius);
        }
        var color = ImportWizardReviewPalette.Mix(palette.Muted, palette.Foreground, _hoverValue);
        palette.Glyph(context, ArcIcons.X, new Point(center.X - 7, center.Y - 7), 14, color);
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawEllipse(null, palette.Pen(palette.FocusRing, 2), center, radius + 3, radius + 3);
        }
    }
}

/// <summary>
/// One row of the review table: the line number (12, muted, 12 from the left) in the 48px column, a cell per field, the
/// leave-out button in the 40px column and, while a cell is edited, the editor over that cell (inserted right after it, so
/// Tab moves on to the next cell). A 1px BorderSubtle line under it except under the last row. Its height is the tallest
/// cell plus the line (41, or 49.4 with a message).
/// </summary>
internal sealed class ImportWizardReviewRow : Control
{
    private readonly ImportWizardReviewTable _table;
    private readonly ImportWizardReviewCell[] _cells;
    private readonly ImportWizardReviewLeaveOut _leaveOut;
    private ImportWizardReviewEditor? _editor;
    private ImportWizardReviewCell? _edited;
    private TextLayout? _lineLayout;
    private int _version = -1;
    private bool _isLast;

    public ImportWizardReviewRow(ImportWizardReviewTable table, ImportReviewRow row, IReadOnlyList<string> fieldLabels)
    {
        _table = table;
        Row = row;
        _cells = new ImportWizardReviewCell[fieldLabels.Count];
        for (var f = 0; f < _cells.Length; f++)
        {
            var cell = new ImportWizardReviewCell(table, row, f, fieldLabels[f]);
            _cells[f] = cell;
            LogicalChildren.Add(cell);
            VisualChildren.Add(cell);
        }
        _leaveOut = new ImportWizardReviewLeaveOut(table, row);
        LogicalChildren.Add(_leaveOut);
        VisualChildren.Add(_leaveOut);
        AddHandler(GotFocusEvent, (_, _) => UpdateLift(), RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, (_, _) => UpdateLift(), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The row of the session.</summary>
    public ImportReviewRow Row { get; }

    /// <summary>The cells in field order.</summary>
    public IReadOnlyList<ImportWizardReviewCell> Cells => _cells;

    /// <summary>The leave-out button.</summary>
    public ImportWizardReviewLeaveOut LeaveOut => _leaveOut;

    /// <summary>The open editor, or null.</summary>
    public ImportWizardReviewEditor? Editor => _editor;

    /// <summary>The cell being edited, or null.</summary>
    public ImportWizardReviewCell? EditedCell => _edited;

    /// <summary>The last row has no line under it.</summary>
    public bool IsLast
    {
        get => _isLast;
        set
        {
            if (_isLast == value)
            {
                return;
            }
            _isLast = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The unrounded height the table stacks: the tallest cell plus the line.</summary>
    public double LayoutHeight { get; private set; } = 41;

    /// <summary>Puts <paramref name="editor"/> over <paramref name="cell"/> (right after it in the tab order).</summary>
    public void OpenEditor(ImportWizardReviewEditor editor, ImportWizardReviewCell cell)
    {
        CloseEditor();
        _editor = editor;
        _edited = cell;
        cell.IsEditing = true;
        var index = VisualChildren.IndexOf(cell);
        LogicalChildren.Add(editor);
        VisualChildren.Insert(index + 1, editor);
        InvalidateMeasure();
    }

    /// <summary>Takes the editor away and shows the cell again.</summary>
    public void CloseEditor()
    {
        if (_editor is { } editor)
        {
            VisualChildren.Remove(editor);
            LogicalChildren.Remove(editor);
        }
        if (_edited is { } cell)
        {
            cell.IsEditing = false;
        }
        _editor = null;
        _edited = null;
        InvalidateMeasure();
    }

    /// <summary>Applies the labels and the theme again.</summary>
    public void Refresh(IReadOnlyList<string> fieldLabels)
    {
        for (var f = 0; f < _cells.Length && f < fieldLabels.Count; f++)
        {
            _cells[f].FieldLabel = fieldLabels[f];
            _cells[f].Refresh();
        }
        _leaveOut.Refresh();
        _lineLayout?.Dispose();
        _lineLayout = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Draws the focused part above its neighbours (its ring reaches over them, like Arc's outline).</summary>
    private void UpdateLift()
    {
        var within = false;
        foreach (var child in VisualChildren)
        {
            if (child is Control control)
            {
                var focused = control.IsFocused;
                control.ZIndex = focused ? 1 : 0;
                within |= focused || control.IsKeyboardFocusWithin;
            }
        }
        ZIndex = within ? 1 : 0;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = _table.Widths;
        var content = ImportWizardReviewCell.MinHeight40;
        for (var f = 0; f < _cells.Length; f++)
        {
            var width = f + 1 < widths.Length ? widths[f + 1] : 0;
            _cells[f].Measure(new Size(width, double.PositiveInfinity));
            content = Math.Max(content, _cells[f].ContentHeight);
        }
        _leaveOut.Measure(new Size(ImportWizardReviewLeaveOut.Size30, ImportWizardReviewLeaveOut.Size30));
        if (_editor is not null && _edited is not null)
        {
            var width = _edited.Field + 1 < widths.Length ? widths[_edited.Field + 1] : 0;
            _editor.Measure(new Size(width, ImportWizardReviewCell.MinHeight40));
        }
        LayoutHeight = content + (_isLast ? 0 : 1);
        return new Size(_table.TotalWidth, LayoutHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = _table.Widths;
        var height = Math.Max(0, finalSize.Height - (_isLast ? 0 : 1));
        var x = widths.Length > 0 ? widths[0] : 0;
        for (var f = 0; f < _cells.Length; f++)
        {
            var width = f + 1 < widths.Length ? widths[f + 1] : 0;
            _cells[f].Arrange(new Rect(x, 0, width, height));
            if (_edited == _cells[f] && _editor is not null)
            {
                var editorHeight = ImportWizardReviewCell.MinHeight40;
                _editor.Arrange(new Rect(x, Math.Round((height - editorHeight) / 2), width, editorHeight));
            }
            x += width;
        }
        var size = ImportWizardReviewLeaveOut.Size30;
        _leaveOut.Arrange(new Rect(x, Math.Round((height - size) / 2), size, size));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        if (_version != palette.Version)
        {
            _lineLayout?.Dispose();
            _lineLayout = null;
            _version = palette.Version;
        }
        _lineLayout ??= new TextLayout(Row.LineNumber.ToString(System.Globalization.CultureInfo.CurrentCulture), palette.Regular, 12,
            palette.Brush(palette.Muted), lineHeight: 16.8, fontFeatures: ImportWizardUi.TabularNumbers);
        var height = Math.Max(0, Bounds.Height - (_isLast ? 0 : 1));
        _lineLayout.Draw(context, new Point(12, (height - _lineLayout.Height) / 2));
        if (!_isLast)
        {
            context.FillRectangle(palette.Brush(palette.Divider), new Rect(0, Bounds.Height - 1, Bounds.Width, 1));
        }
    }
}

/// <summary>
/// The rows of the review step as a table: the header, then a row per shown review row in file order. Columns are as wide
/// as their widest cell or header (fields at most 220), the line column 48 and the actions column 40; when they need less
/// than <see cref="FillWidth"/> the field columns share the rest in proportion (CSS <c>min-width: 100%</c>). Rows that
/// leave fade out moving 24 to the right (200ms ease-out) and keep their place until every leaving row is done; then they
/// go and the rest glide to their places (FLIP on the smooth spring, keeping speed when retargeted). Rows that arrive fade
/// in (smooth spring; a 600ms ease-out tween with reduced motion, which drops the travel and the glide). Any other change
/// of a row's place (a message line that goes, the editor) glides too. Code-driven because the motion depends on where
/// each row was drawn before the change.
/// </summary>
internal sealed class ImportWizardReviewTable : Control
{
    public const double LineWidth = 48;
    public const double ActionsWidth = 40;

    private readonly ImportWizardReviewHeader _header;
    private readonly List<Entry> _entries = new();
    private readonly Dictionary<ImportReviewRow, Entry> _byRow = new();
    private readonly ImportWizardReviewPalette _palette = new();
    private string[] _fieldLabels = [];
    private double[] _widths = [LineWidth, ActionsWidth];
    private double _fillWidth;
    private bool _paletteDirty = true;

    public ImportWizardReviewTable(ImportWizardReviewView owner)
    {
        Owner = owner;
        _header = new ImportWizardReviewHeader(this);
        LogicalChildren.Add(_header);
        VisualChildren.Add(_header);
        ActualThemeVariantChanged += (_, _) => MarkPaletteDirty();
        ResourcesChanged += (_, _) => MarkPaletteDirty();
    }

    /// <summary>The review step.</summary>
    public ImportWizardReviewView Owner { get; }

    /// <summary>The colors and faces of the table.</summary>
    public ImportWizardReviewPalette Palette
    {
        get
        {
            if (_paletteDirty)
            {
                _paletteDirty = false;
                _palette.Refresh(this);
            }
            return _palette;
        }
    }

    /// <summary>Column widths of the last measure: line, fields, actions.</summary>
    public double[] Widths => _widths;

    /// <summary>The sum of <see cref="Widths"/>.</summary>
    public double TotalWidth { get; private set; }

    /// <summary>The width the field columns stretch to when they need less (the viewport of the scroller).</summary>
    public double FillWidth
    {
        get => _fillWidth;
        set
        {
            if (Math.Abs(_fillWidth - value) < 0.5)
            {
                return;
            }
            _fillWidth = value;
            InvalidateMeasure();
        }
    }

    /// <summary>The header.</summary>
    public ImportWizardReviewHeader Header => _header;

    /// <summary>The rows drawn now, in order (with the leaving ones).</summary>
    public IEnumerable<ImportWizardReviewRow> Rows => _entries.Select(e => e.Visual);

    /// <summary>The rows shown and not leaving, in order.</summary>
    public IReadOnlyList<ImportReviewRow> ShownRows => _entries.Where(e => !e.IsLeaving).Select(e => e.Row).ToList();

    /// <summary>How many rows are still playing their exit.</summary>
    public int LeavingCount => _entries.Count(e => e.IsLeaving);

    /// <summary>The control of <paramref name="row"/>, or null when it is not drawn.</summary>
    public ImportWizardReviewRow? RowFor(ImportReviewRow row) => _byRow.TryGetValue(row, out var entry) ? entry.Visual : null;

    /// <summary>Sets the column headers: the line column and the field labels.</summary>
    public void SetLabels(string lineLabel, IReadOnlyList<string> fieldLabels)
    {
        _fieldLabels = fieldLabels.ToArray();
        _header.SetLabels([lineLabel, .. _fieldLabels]);
        foreach (var entry in _entries)
        {
            entry.Visual.Refresh(_fieldLabels);
        }
        InvalidateMeasure();
    }

    /// <summary>
    /// Shows <paramref name="rows"/> (in file order): rows not in it leave, rows not drawn yet arrive, leaving rows that are
    /// in it again come back.
    /// </summary>
    public void Sync(IReadOnlyList<ImportReviewRow> rows, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && IsLoaded && level != ArcMotionLevel.Off;
        var wanted = new HashSet<ImportReviewRow>(rows);
        foreach (var entry in _entries.ToArray())
        {
            if (!wanted.Contains(entry.Row))
            {
                if (!entry.IsLeaving)
                {
                    Leave(entry, animate);
                }
            }
            else if (entry.IsLeaving)
            {
                Return(entry, animate);
            }
        }
        foreach (var row in rows)
        {
            if (_byRow.ContainsKey(row))
            {
                continue;
            }
            var entry = Insert(row);
            if (animate)
            {
                entry.Fade.Jump(0);
                var spec = level == ArcMotionLevel.Full
                    ? MotionSpec.Spring(ImportWizardMotion.Smooth)
                    : MotionSpec.Tween(TimeSpan.FromMilliseconds(600), ImportWizardMotion.EaseOut);
                _ = entry.Fade.AnimateTo(1, spec);
            }
        }
        RemoveLeftRows();
        InvalidateMeasure();
    }

    private Entry Insert(ImportReviewRow row)
    {
        var visual = new ImportWizardReviewRow(this, row, _fieldLabels);
        var entry = new Entry(this, visual);
        var index = 0;
        while (index < _entries.Count && _entries[index].Row.Index < row.Index)
        {
            index++;
        }
        _entries.Insert(index, entry);
        _byRow[row] = entry;
        LogicalChildren.Add(visual);
        // The header is the first visual child; rows follow in order (the tab order).
        VisualChildren.Insert(index + 1, visual);
        return entry;
    }

    private void Leave(Entry entry, bool animate)
    {
        entry.IsLeaving = true;
        entry.ExitDone = false;
        entry.Visual.IsHitTestVisible = false;
        MoveFocusFrom(entry);
        if (!animate)
        {
            entry.ExitDone = true;
            entry.Exit.Jump(1);
            return;
        }
        _ = PlayExit(entry);
    }

    private async Task PlayExit(Entry entry)
    {
        var done = await entry.Exit.AnimateTo(1, MotionSpec.Tween(ImportWizardMotion.RowExitDuration, ImportWizardMotion.RowExit));
        if (done && entry.IsLeaving && _byRow.TryGetValue(entry.Row, out var current) && current == entry)
        {
            entry.ExitDone = true;
            RemoveLeftRows();
        }
    }

    private void Return(Entry entry, bool animate)
    {
        entry.IsLeaving = false;
        entry.ExitDone = false;
        entry.Visual.IsHitTestVisible = true;
        if (animate)
        {
            _ = entry.Exit.AnimateTo(0, MotionSpec.Tween(ImportWizardMotion.RowExitDuration, ImportWizardMotion.RowExit));
        }
        else
        {
            entry.Exit.Jump(0);
        }
    }

    /// <summary>Takes the leaving rows out once every one of them finished its exit.</summary>
    private void RemoveLeftRows()
    {
        var any = false;
        foreach (var entry in _entries)
        {
            if (entry.IsLeaving)
            {
                if (!entry.ExitDone)
                {
                    return;
                }
                any = true;
            }
        }
        if (!any)
        {
            return;
        }
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!entry.IsLeaving)
            {
                continue;
            }
            _entries.RemoveAt(i);
            _byRow.Remove(entry.Row);
            VisualChildren.Remove(entry.Visual);
            LogicalChildren.Remove(entry.Visual);
            entry.Dispose();
        }
        InvalidateMeasure();
    }

    /// <summary>A row that leaves with the keyboard focus hands it to the same column of the next row (or the previous).</summary>
    private void MoveFocusFrom(Entry entry)
    {
        var visual = entry.Visual;
        if (!visual.IsKeyboardFocusWithin)
        {
            return;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var field = -1;
        var onLeaveOut = focused == visual.LeaveOut;
        for (var f = 0; f < visual.Cells.Count; f++)
        {
            if (visual.Cells[f] == focused || visual.EditedCell == visual.Cells[f])
            {
                field = f;
            }
        }
        var index = _entries.IndexOf(entry);
        Entry? target = null;
        for (var i = index + 1; i < _entries.Count && target is null; i++)
        {
            if (!_entries[i].IsLeaving)
            {
                target = _entries[i];
            }
        }
        for (var i = index - 1; i >= 0 && target is null; i--)
        {
            if (!_entries[i].IsLeaving)
            {
                target = _entries[i];
            }
        }
        var method = BlockFocusModality.For(this);
        if (target is null)
        {
            Owner.FocusTableRegion(method);
            return;
        }
        Control next = onLeaveOut || field < 0 ? target.Visual.LeaveOut : target.Visual.Cells[Math.Min(field, target.Visual.Cells.Count - 1)];
        next.Focus(method);
    }

    private void MarkPaletteDirty()
    {
        if (_paletteDirty)
        {
            return;
        }
        _paletteDirty = true;
        _header.InvalidateMeasure();
        _header.InvalidateVisual();
        foreach (var entry in _entries)
        {
            foreach (var cell in entry.Visual.Cells)
            {
                cell.InvalidateMeasure();
                cell.InvalidateVisual();
            }
            entry.Visual.LeaveOut.InvalidateVisual();
            entry.Visual.InvalidateVisual();
        }
        InvalidateMeasure();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        MarkPaletteDirty();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var max = this.TryFindResource(ImportWizardTokens.Key(ImportWizardTokenKind.CellMaxWidth), ActualThemeVariant, out var value) && value is double found
            ? found
            : 220;
        var fields = _fieldLabels.Length;
        if (_widths.Length != fields + 2)
        {
            _widths = new double[fields + 2];
        }
        _widths[0] = LineWidth;
        _widths[^1] = ActionsWidth;
        for (var f = 0; f < fields; f++)
        {
            _widths[f + 1] = Math.Min(max, _header.NaturalWidth(f + 1));
        }
        var last = -1;
        for (var i = 0; i < _entries.Count; i++)
        {
            var cells = _entries[i].Visual.Cells;
            for (var f = 0; f < fields && f < cells.Count; f++)
            {
                _widths[f + 1] = Math.Max(_widths[f + 1], Math.Min(max, cells[f].NaturalWidth));
            }
            last = i;
        }
        var total = 0.0;
        foreach (var width in _widths)
        {
            total += width;
        }
        var fieldTotal = total - LineWidth - ActionsWidth;
        if (_fillWidth > total && fieldTotal > 0)
        {
            var extra = _fillWidth - total;
            for (var f = 0; f < fields; f++)
            {
                _widths[f + 1] += extra * _widths[f + 1] / fieldTotal;
            }
            total = _fillWidth;
        }
        TotalWidth = total;
        _header.Measure(new Size(total, double.PositiveInfinity));
        var height = _header.LayoutHeight;
        for (var i = 0; i < _entries.Count; i++)
        {
            var row = _entries[i].Visual;
            row.IsLast = i == last;
            row.InvalidateMeasure();
            row.Measure(new Size(total, double.PositiveInfinity));
            height += row.LayoutHeight;
        }
        return new Size(total, Math.Ceiling(height - 0.01));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Max(finalSize.Width, TotalWidth);
        var y = _header.LayoutHeight;
        _header.Arrange(new Rect(0, 0, width, Math.Round(y)));
        var top = Math.Round(y);
        var glide = IsLoaded && ArcMotion.IsFull(this);
        foreach (var entry in _entries)
        {
            y += entry.Visual.LayoutHeight;
            var bottom = Math.Round(y);
            entry.Visual.Arrange(new Rect(0, top, width, Math.Max(0, bottom - top)));
            if (entry.LayoutY is { } old && Math.Abs(old - top) > 0.01)
            {
                if (glide)
                {
                    var velocity = entry.Offset.Velocity;
                    entry.Offset.Jump(entry.Offset.Value + old - top);
                    _ = entry.Offset.AnimateTo(0, MotionSpec.Spring(ImportWizardMotion.Smooth), velocity);
                }
                else
                {
                    entry.Offset.Jump(0);
                }
            }
            entry.LayoutY = top;
            top = bottom;
        }
        return new Size(width, finalSize.Height);
    }

    /// <summary>A drawn row with its motion: the FLIP offset, the fade in and the exit.</summary>
    private sealed class Entry : IDisposable
    {
        private readonly ImportWizardReviewTable _table;
        private readonly TranslateTransform _transform = new();

        public Entry(ImportWizardReviewTable table, ImportWizardReviewRow visual)
        {
            _table = table;
            Visual = visual;
            Visual.RenderTransform = _transform;
            Offset = new MotionValue<double>(visual, 0, MotionVectors.Double);
            Fade = new MotionValue<double>(visual, 1, MotionVectors.Double);
            Exit = new MotionValue<double>(visual, 0, MotionVectors.Double);
            Offset.Changed += _ => Apply();
            Fade.Changed += _ => Apply();
            Exit.Changed += _ => Apply();
        }

        public ImportWizardReviewRow Visual { get; }

        public ImportReviewRow Row => Visual.Row;

        /// <summary>FLIP offset from the layout place (px).</summary>
        public MotionValue<double> Offset { get; }

        /// <summary>The arrival fade (0 to 1).</summary>
        public MotionValue<double> Fade { get; }

        /// <summary>The exit (0 to 1: opacity 1 to 0, x 0 to 24).</summary>
        public MotionValue<double> Exit { get; }

        public double? LayoutY { get; set; }

        public bool IsLeaving { get; set; }

        public bool ExitDone { get; set; }

        private void Apply()
        {
            Visual.Opacity = Math.Clamp(Fade.Value * (1 - Exit.Value), 0, 1);
            _transform.X = ArcMotion.IsFull(_table) ? ImportWizardMotion.RowExitTravel * Exit.Value : 0;
            _transform.Y = Offset.Value;
        }

        public void Dispose()
        {
            Offset.Dispose();
            Fade.Dispose();
            Exit.Dispose();
        }
    }
}

/// <summary>
/// The bordered box around the review table (1px Border, radius 18, contents clipped) that scrolls it sideways. The box is
/// the table's region for screen readers ("Rows to review"): it takes focus, Left / Right / Home / End scroll it, and it shows
/// the 2px focus ring 2 outside its edge. It keeps the table's <see cref="ImportWizardReviewTable.FillWidth"/> at its
/// inner width.
/// </summary>
internal sealed class ImportWizardReviewTableBox : Panel
{
    private readonly Border _frame;
    private readonly Border _ring;

    public ImportWizardReviewTableBox(ImportWizardReviewTable table)
    {
        Table = table;
        Viewer = new ScrollViewer
        {
            Content = table,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = true,
            FocusAdorner = null,
        };
        _frame = new Border { Child = Viewer, ClipToBounds = true }.BindCornerRadius(ArcDesignTokenKind.RadiusStep18);
        ArcTokens.Bind(_frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_frame, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        _ring = new Border { Margin = new Thickness(-4), IsHitTestVisible = false, IsVisible = false }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep22);
        ArcTokens.Bind(_ring, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
        ArcTokens.Bind(_ring, Border.BorderThicknessProperty, ArcDesignTokenKind.FocusEdgeThickness);
        Children.Add(_frame);
        Children.Add(_ring);
        Viewer.GotFocus += (_, _) => UpdateRing();
        Viewer.LostFocus += (_, _) => UpdateRing();
        Viewer.KeyDown += OnViewerKeyDown;
    }

    /// <summary>The table.</summary>
    public ImportWizardReviewTable Table { get; }

    /// <summary>The sideways scroller (the focusable region).</summary>
    public ScrollViewer Viewer { get; }

    private void UpdateRing() => _ring.IsVisible = Viewer.IsFocused && Viewer.Classes.Contains(":focus-visible");

    private void OnViewerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source != Viewer)
        {
            return;
        }
        var offset = Viewer.Offset.X;
        var max = Math.Max(0, Viewer.Extent.Width - Viewer.Viewport.Width);
        var target = e.Key switch
        {
            Key.Left => offset - 40,
            Key.Right => offset + 40,
            Key.Home => 0,
            Key.End => max,
            _ => double.NaN,
        };
        if (double.IsNaN(target))
        {
            return;
        }
        Viewer.Offset = new Vector(Math.Clamp(target, 0, max), Viewer.Offset.Y);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            Table.FillWidth = Math.Max(0, availableSize.Width - 2);
        }
        return base.MeasureOverride(availableSize);
    }
}
