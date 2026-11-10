using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>The colors the case list and the chips are drawn with, read from token brushes.</summary>
internal readonly record struct EvalSuitePalette(Color Foreground, Color Secondary, Color Muted, Color Success, Color Danger,
    Color Warning, Color Accent)
{
    public static EvalSuitePalette From(IBrush? foreground, IBrush? secondary, IBrush? muted, IBrush? success, IBrush? danger,
        IBrush? warning, IBrush? accent)
    {
        static Color C(IBrush? brush, Color fallback) => brush is ISolidColorBrush s ? s.Color : fallback;
        return new EvalSuitePalette(C(foreground, Colors.Black), C(secondary, Colors.DimGray), C(muted, Colors.Gray),
            C(success, Colors.Green), C(danger, Colors.Red), C(warning, Colors.Orange), C(accent, Colors.RoyalBlue));
    }

    /// <summary>The color and glyph of a status: a shape and a word go with every color.</summary>
    public EvalSuiteMark StatusMark(EvalCaseStatus status) => status switch
    {
        EvalCaseStatus.Regression => new EvalSuiteMark(Danger, ArcIcons.TrendingDown, true),
        EvalCaseStatus.Fixed => new EvalSuiteMark(Success, ArcIcons.TrendingUp, true),
        EvalCaseStatus.Passing => new EvalSuiteMark(Secondary, ArcIcons.Check, true),
        EvalCaseStatus.Failing => new EvalSuiteMark(Warning, ArcIcons.CircleAlert, true),
        EvalCaseStatus.Running => new EvalSuiteMark(Accent, ArcIcons.LoaderCircle, true),
        EvalCaseStatus.Queued => new EvalSuiteMark(Muted, ArcIcons.Clock3, true),
        _ => new EvalSuiteMark(Muted, ArcIcons.Minus, true)
    };

    /// <summary>The color and glyph of a result: a check for a pass, a cross for a fail, a warning for an error.</summary>
    public EvalSuiteMark VerdictMark(EvalResult? result) => result switch
    {
        null => new EvalSuiteMark(Muted, ArcIcons.Minus, false),
        { IsError: true } => new EvalSuiteMark(Warning, ArcIcons.TriangleAlert, false),
        { Passed: true } => new EvalSuiteMark(Success, ArcIcons.Check, false),
        _ => new EvalSuiteMark(Danger, ArcIcons.X, false)
    };
}

/// <summary>
/// The suite's case list, drawn as one control: a row per case with its status chip, title and input, check, and the two
/// verdicts. Only the rows inside the viewport are drawn and their text is laid out once per width. A keyboard cursor
/// moves with the arrows, Home / End and PageUp / PageDown; Enter, Space or a click opens the case. A finished case's chip
/// fades in (and rises 4 px in full motion); running cases turn their glyph.
/// </summary>
internal sealed class EvalSuiteCaseList : Control
{
    #region Properties

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(SecondaryBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(SuccessBrush));

    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(DangerBrush));

    public static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(WarningBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(HoverBrush));

    public static readonly StyledProperty<IBrush?> SelectedBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(SelectedBrush));

    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(DividerBrush));

    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, IBrush?>(nameof(FocusBrush));

    public static readonly StyledProperty<CornerRadius> RowRadiusProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, CornerRadius>(nameof(RowRadius));

    public static readonly StyledProperty<CornerRadius> ChipRadiusProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, CornerRadius>(nameof(ChipRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<EvalSuiteCaseList>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<EvalSuiteCaseList>();

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, double>(nameof(RowHeight), 56);

    public static readonly StyledProperty<double> RowInsetProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, double>(nameof(RowInset), 20);

    public static readonly StyledProperty<double> StatusColumnWidthProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, double>(nameof(StatusColumnWidth), 124);

    public static readonly StyledProperty<double> CheckColumnWidthProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, double>(nameof(CheckColumnWidth), 96);

    public static readonly StyledProperty<double> VerdictColumnWidthProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, double>(nameof(VerdictColumnWidth), 92);

    public static readonly StyledProperty<double> ChipHeightProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, double>(nameof(ChipHeight), 24);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, bool>(nameof(IsCompact));

    public static readonly StyledProperty<TimeSpan> RevealDurationProperty =
        AvaloniaProperty.Register<EvalSuiteCaseList, TimeSpan>(nameof(RevealDuration), TimeSpan.FromMilliseconds(240));

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    public IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }
    public IBrush? SelectedBrush { get => GetValue(SelectedBrushProperty); set => SetValue(SelectedBrushProperty, value); }
    public IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public CornerRadius RowRadius { get => GetValue(RowRadiusProperty); set => SetValue(RowRadiusProperty, value); }
    public CornerRadius ChipRadius { get => GetValue(ChipRadiusProperty); set => SetValue(ChipRadiusProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double RowInset { get => GetValue(RowInsetProperty); set => SetValue(RowInsetProperty, value); }
    public double StatusColumnWidth { get => GetValue(StatusColumnWidthProperty); set => SetValue(StatusColumnWidthProperty, value); }
    public double CheckColumnWidth { get => GetValue(CheckColumnWidthProperty); set => SetValue(CheckColumnWidthProperty, value); }
    public double VerdictColumnWidth { get => GetValue(VerdictColumnWidthProperty); set => SetValue(VerdictColumnWidthProperty, value); }
    public double ChipHeight { get => GetValue(ChipHeightProperty); set => SetValue(ChipHeightProperty, value); }
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    public TimeSpan RevealDuration { get => GetValue(RevealDurationProperty); set => SetValue(RevealDurationProperty, value); }

    #endregion

    private const double EmptyHeight = 96;
    private static readonly Avalonia.Input.Cursor s_hand = new(StandardCursorType.Hand);
    private ImmutablePen? _focusPen;

    private readonly EvalSuitePainter _painter = new();
    private IReadOnlyList<EvalSuiteRow> _rows = Array.Empty<EvalSuiteRow>();
    private readonly LiveLoop _loop;
    private Rect _viewport;
    private int _cursor;
    private int _hover = -1;
    private int _pressed = -1;
    private double _spin;
    private string? _emptyText;
    private TextLayout? _emptyLayout;
    private EvalSuitePalette _palette;
    private bool _paletteValid;

    static EvalSuiteCaseList()
    {
        FocusableProperty.OverrideDefaultValue<EvalSuiteCaseList>(true);
        ClipToBoundsProperty.OverrideDefaultValue<EvalSuiteCaseList>(false);
        AffectsMeasure<EvalSuiteCaseList>(RowHeightProperty);
        AffectsRender<EvalSuiteCaseList>(HoverBrushProperty, SelectedBrushProperty, DividerBrushProperty, FocusBrushProperty,
            RowRadiusProperty, ChipRadiusProperty, ChipHeightProperty);
    }

    public EvalSuiteCaseList()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                // Paused or finished: draw the resting frame (chips fully in).
                foreach (var row in _rows)
                {
                    row.Reveal = null;
                }
                InvalidateVisual();
            }
        };
        // Subscribe before the first layout so the first viewport is reported.
        EffectiveViewportChanged += (_, e) =>
        {
            _viewport = e.EffectiveViewport;
            InvalidateVisual();
        };
    }

    /// <summary>The labels (verdict words, accessible names).</summary>
    public EvalSuiteLabels Labels { get; set; } = EvalSuiteLabels.Default;

    /// <summary>Formats scores.</summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>The rows shown, in order.</summary>
    public IReadOnlyList<EvalSuiteRow> Rows => _rows;

    /// <summary>The id of the case shown open below the list, drawn selected.</summary>
    public string? SelectedCaseId { get; private set; }

    /// <summary>The keyboard cursor, an index into <see cref="Rows"/>.</summary>
    public int CursorIndex => _cursor;

    /// <summary>Raised when a row is opened by a click, Enter or Space.</summary>
    public event Action<EvalSuiteRow>? Invoked;

    /// <summary>Raised when the keys move the cursor to another row.</summary>
    public event Action<EvalSuiteRow>? CursorMoved;

    /// <summary>The row under the cursor, or null without rows.</summary>
    public EvalSuiteRow? CursorRow => _cursor >= 0 && _cursor < _rows.Count ? _rows[_cursor] : null;

    /// <summary>Replaces the shown rows; the cursor stays on its case when it is still shown.</summary>
    public void SetRows(IReadOnlyList<EvalSuiteRow> rows, string? emptyText)
    {
        var keep = CursorRow?.Case.Id;
        _rows = rows;
        _hover = -1;
        _pressed = -1;
        _cursor = 0;
        if (keep is not null)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Case.Id == keep)
                {
                    _cursor = i;
                    break;
                }
            }
        }
        if (_emptyText != emptyText)
        {
            _emptyText = emptyText;
            _emptyLayout?.Dispose();
            _emptyLayout = null;
        }
        UpdateName();
        InvalidateMeasure();
        InvalidateVisual();
        Animate();
    }

    /// <summary>Draws a row as selected (the open case), or none.</summary>
    public void SetSelected(string? caseId)
    {
        SelectedCaseId = caseId;
        InvalidateVisual();
    }

    /// <summary>Repaints rows whose results or status changed in place and starts the reveal or spin when needed.</summary>
    public void Refresh()
    {
        foreach (var row in _rows)
        {
            // Verdict and chip text depend on the results; lay them out again.
            row.ClearLayouts();
        }
        UpdateName();
        InvalidateVisual();
        Animate();
    }

    /// <summary>Moves the cursor to the case and scrolls it into view.</summary>
    public bool MoveTo(string caseId)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Case.Id == caseId)
            {
                SetCursor(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>The rectangle of a shown row, in list coordinates.</summary>
    public Rect RowRect(int index) => new(0, index * RowHeight, Bounds.Width, RowHeight);

    private void Animate()
    {
        var wants = false;
        foreach (var row in _rows)
        {
            if (row.Reveal is not null || row.Status == EvalCaseStatus.Running)
            {
                wants = true;
                break;
            }
        }
        if (wants)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private bool Step(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var duration = Math.Max(0.001, RevealDuration.TotalSeconds);
        var busy = false;
        foreach (var row in _rows)
        {
            if (row.Reveal is { } t)
            {
                t += dt;
                row.Reveal = t >= duration ? null : t;
                busy |= row.Reveal is not null;
            }
            if (row.Status == EvalCaseStatus.Running)
            {
                busy = true;
            }
        }
        if (full)
        {
            _spin = (_spin + dt * Math.PI * 2 * 0.9) % (Math.PI * 2);
        }
        InvalidateVisual();
        return busy;
    }

    #region Layout and drawing

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var height = _rows.Count == 0 ? EmptyHeight : _rows.Count * RowHeight;
        return new Size(width, height);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == ForegroundProperty || p == SecondaryBrushProperty || p == MutedBrushProperty || p == SuccessBrushProperty ||
            p == DangerBrushProperty || p == WarningBrushProperty || p == AccentBrushProperty || p == FontFamilyProperty ||
            p == FontSizeProperty || p == IsCompactProperty || p == StatusColumnWidthProperty || p == CheckColumnWidthProperty ||
            p == VerdictColumnWidthProperty || p == RowInsetProperty || p == FocusBrushProperty)
        {
            _focusPen = null;
            _paletteValid = false;
            foreach (var row in _rows)
            {
                row.ClearLayouts();
            }
            _emptyLayout?.Dispose();
            _emptyLayout = null;
            InvalidateVisual();
        }
    }

    private EvalSuitePalette Palette
    {
        get
        {
            if (!_paletteValid)
            {
                _palette = EvalSuitePalette.From(Foreground, SecondaryBrush, MutedBrush, SuccessBrush, DangerBrush, WarningBrush, AccentBrush);
                _paletteValid = true;
            }
            return _palette;
        }
    }

    internal EvalSuiteColumns Columns =>
        EvalSuiteColumns.Compute(Bounds.Width, RowInset, StatusColumnWidth, CheckColumnWidth, VerdictColumnWidth, IsCompact);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole list takes the pointer, not only the drawn pixels.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_rows.Count == 0)
        {
            RenderEmpty(context, bounds);
            return;
        }
        var height = RowHeight;
        var view = _viewport.Width > 0 && _viewport.Height > 0 ? _viewport.Intersect(bounds) : bounds;
        var first = Math.Max(0, (int)Math.Floor(view.Top / height) - 1);
        var last = Math.Min(_rows.Count - 1, (int)Math.Ceiling(view.Bottom / height));
        var columns = Columns;
        var palette = Palette;
        var focused = Classes.Contains(":focus-visible");
        for (var i = first; i <= last; i++)
        {
            RenderRow(context, i, _rows[i], new Rect(0, i * height, bounds.Width, height), columns, palette, focused && i == _cursor);
        }
    }

    private void RenderEmpty(DrawingContext context, Rect bounds)
    {
        if (string.IsNullOrEmpty(_emptyText))
        {
            return;
        }
        _emptyLayout ??= new TextLayout(_emptyText, new Typeface(FontFamily), FontSize, MutedBrush ?? Brushes.Gray,
            TextAlignment.Center, TextWrapping.Wrap, maxWidth: Math.Max(1, bounds.Width - 2 * RowInset));
        _emptyLayout.Draw(context, new Point(RowInset, (bounds.Height - _emptyLayout.Height) / 2));
    }

    private void RenderRow(DrawingContext context, int index, EvalSuiteRow row, Rect rect, EvalSuiteColumns columns,
        EvalSuitePalette palette, bool focusRing)
    {
        var radius = RowRadius.TopLeft;
        var inner = rect.Deflate(new Thickness(Math.Max(0, RowInset - 12), 2));
        if (row.Case.Id == SelectedCaseId && SelectedBrush is { } selected)
        {
            context.DrawRectangle(selected, null, inner, radius, radius);
        }
        else if (index == _hover && HoverBrush is { } hover)
        {
            context.DrawRectangle(hover, null, inner, radius, radius);
        }
        if (index < _rows.Count - 1 && DividerBrush is { } divider)
        {
            context.FillRectangle(divider, new Rect(rect.X + RowInset, rect.Bottom - 1, Math.Max(0, rect.Width - 2 * RowInset), 1));
        }
        EnsureLayouts(row, columns, palette);

        // Status chip.
        var chipHeight = ChipHeight;
        var mark = palette.StatusMark(row.Status);
        var chipWidth = Math.Min(columns.StatusWidth, EvalSuitePainter.ChipWidth(row.Chip!, chipHeight));
        var chipRect = new Rect(columns.StatusX, rect.Center.Y - chipHeight / 2, chipWidth, chipHeight);
        var reveal = RevealProgress(row);
        var lift = ArcMotion.IsFull(this) ? (1 - reveal) * 4 : 0;
        using (context.PushOpacity(0.25 + 0.75 * reveal))
        using (context.PushTransform(Matrix.CreateTranslation(0, lift)))
        {
            _painter.Chip(context, chipRect, mark, row.Chip!, ChipRadius.TopLeft, row.Status == EvalCaseStatus.Running ? _spin : 0);
        }

        // Title and input.
        var lineGap = 2;
        var textHeight = row.Title!.Height + lineGap + row.Input!.Height;
        var top = rect.Center.Y - textHeight / 2;
        row.Title.Draw(context, new Point(columns.CaseX, top));
        row.Input.Draw(context, new Point(columns.CaseX, top + row.Title.Height + lineGap));

        if (columns.CheckWidth > 0)
        {
            row.Check!.Draw(context, new Point(columns.CheckX, rect.Center.Y - row.Check.Height / 2));
        }

        using (context.PushOpacity(0.25 + 0.75 * reveal))
        {
            RenderVerdict(context, row.Baseline, row.BaseVerdict, columns.BaseX, rect, palette);
            RenderVerdict(context, row.Candidate, row.CandidateVerdict, columns.CandidateX, rect, palette);
        }

        if (focusRing && FocusBrush is { } focus)
        {
            _focusPen ??= new ImmutablePen(focus.ToImmutable(), 2);
            context.DrawRectangle(null, _focusPen, inner.Deflate(1), radius, radius);
        }
    }

    private void RenderVerdict(DrawingContext context, EvalResult? result, TextLayout? text, double x, Rect rect, EvalSuitePalette palette)
    {
        var mark = palette.VerdictMark(result);
        _painter.Glyph(context, mark.Glyph, mark.Color, new Point(x + 8, rect.Center.Y), 16);
        if (text is not null)
        {
            text.Draw(context, new Point(x + 22, rect.Center.Y - text.Height / 2));
        }
    }

    private double RevealProgress(EvalSuiteRow row)
    {
        if (row.Reveal is not { } t)
        {
            return 1;
        }
        var p = Math.Clamp(t / Math.Max(0.001, RevealDuration.TotalSeconds), 0, 1);
        // Ease out (cubic).
        return 1 - Math.Pow(1 - p, 3);
    }

    private void EnsureLayouts(EvalSuiteRow row, EvalSuiteColumns columns, EvalSuitePalette palette)
    {
        if (row.Title is not null && Math.Abs(row.LayoutWidth - Bounds.Width) < 0.5)
        {
            return;
        }
        row.ClearLayouts();
        row.LayoutWidth = Bounds.Width;
        var family = FontFamily;
        var size = FontSize;
        var regular = new Typeface(family);
        var medium = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
        var small = Math.Max(10, size - 1);
        var caseWidth = Math.Max(1, columns.CaseWidth);
        row.Title = new TextLayout(row.Case.Title, medium, size, _painter.Solid(palette.Foreground), maxWidth: caseWidth,
            textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1);
        row.Input = new TextLayout(OneLine(row.Case.Input), regular, small, _painter.Solid(palette.Muted), maxWidth: caseWidth,
            textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1);
        row.Check = new TextLayout(Labels.CheckName(row.Case.Check), regular, small, _painter.Solid(palette.Secondary),
            maxWidth: Math.Max(1, columns.CheckWidth), textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1);
        var mark = palette.StatusMark(row.Status);
        row.Chip = new TextLayout(Labels.StatusName(row.Status), medium, 12, _painter.Solid(mark.Color),
            maxWidth: Math.Max(1, columns.StatusWidth - 30), textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1);
        if (!IsCompact)
        {
            row.BaseVerdict = Verdict(row.Baseline, regular, small, palette);
            row.CandidateVerdict = Verdict(row.Candidate, regular, small, palette);
        }
    }

    private TextLayout Verdict(EvalResult? result, Typeface face, double size, EvalSuitePalette palette)
    {
        var text = result is null ? "—" : Labels.Verdict(result, Culture);
        var color = result is null ? palette.Muted : palette.Secondary;
        return new TextLayout(text, face, size, _painter.Solid(color), maxWidth: Math.Max(1, VerdictColumnWidth - 24),
            textTrimming: TextTrimming.CharacterEllipsis, maxLines: 1);
    }

    private static string OneLine(string text) => text.Replace('\n', ' ').Replace('\r', ' ');

    #endregion

    #region Input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _rows.Count == 0)
        {
            return;
        }
        var page = Math.Max(1, (int)((_viewport.Height > 0 ? _viewport.Height : 400) / RowHeight) - 1);
        int? next = e.Key switch
        {
            Key.Down => Math.Min(_rows.Count - 1, _cursor + 1),
            Key.Up => Math.Max(0, _cursor - 1),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            Key.PageDown => Math.Min(_rows.Count - 1, _cursor + page),
            Key.PageUp => Math.Max(0, _cursor - page),
            _ => null
        };
        if (next is { } index)
        {
            var moved = index != _cursor;
            SetCursor(index);
            if (moved && CursorRow is { } current)
            {
                CursorMoved?.Invoke(current);
            }
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Space && CursorRow is { } row)
        {
            Invoked?.Invoke(row);
            e.Handled = true;
        }
    }

    private void SetCursor(int index)
    {
        _cursor = Math.Clamp(index, 0, Math.Max(0, _rows.Count - 1));
        UpdateName();
        this.BringIntoView(RowRect(_cursor));
        InvalidateVisual();
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

    private int IndexAt(Point point)
    {
        if (_rows.Count == 0 || point.Y < 0)
        {
            return -1;
        }
        var index = (int)(point.Y / RowHeight);
        return index < _rows.Count ? index : -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = e.Pointer.Type == PointerType.Touch ? -1 : IndexAt(e.GetPosition(this));
        if (index != _hover)
        {
            _hover = index;
            Cursor = index >= 0 ? s_hand : null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover != -1)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = IndexAt(e.GetPosition(this));
        if (_pressed >= 0)
        {
            _cursor = _pressed;
            UpdateName();
            Focus(NavigationMethod.Pointer);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var index = IndexAt(e.GetPosition(this));
        if (_pressed >= 0 && index == _pressed && index < _rows.Count)
        {
            Invoked?.Invoke(_rows[index]);
            e.Handled = true;
        }
        _pressed = -1;
    }

    #endregion

    /// <summary>The accessible text of a row: position, title, status and both verdicts.</summary>
    internal string RowName(EvalSuiteRow row, int index)
    {
        var labels = Labels;
        var position = string.Format(Culture, labels.RowPositionFormat, index + 1, _rows.Count);
        return string.Format(Culture, labels.RowNameFormat, position, row.Case.Title, labels.StatusName(row.Status),
            labels.Verdict(row.Baseline, Culture), labels.Verdict(row.Candidate, Culture));
    }

    private void UpdateName()
    {
        // The focused list speaks for its cursor row; the help text names the list.
        AutomationProperties.SetHelpText(this, string.Format(Culture, Labels.ListNameFormat, _rows.Count));
        AutomationProperties.SetName(this, CursorRow is { } row ? RowName(row, _cursor) : _emptyText ?? string.Empty);
    }
}
