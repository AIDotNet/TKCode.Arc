using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using StreamMark.Core.Highlighting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a code row of a <see cref="CodeSuggestion"/> is.</summary>
internal enum CodeSuggestionRowKind
{
    /// <summary>An original line no hunk touches.</summary>
    Context,

    /// <summary>An original line a hunk removes.</summary>
    Removed,

    /// <summary>A new line of a hunk.</summary>
    Added
}

/// <summary>
/// One 20px code row. Rows live as long as their line does (an original line keeps its row when a hunk claims it), so
/// their three animated values carry over: <see cref="Open"/> (the share of the row's height, 0 to 1), <see cref="Fade"/>
/// (the text's opacity) and <see cref="Tint"/> (the red or green tint and the sign).
/// </summary>
internal sealed class CodeSuggestionRow
{
    public CodeSuggestionRow(string key, MotionValue<double> open, MotionValue<double> fade, MotionValue<double> tint)
    {
        Key = key;
        Open = open;
        Fade = fade;
        Tint = tint;
    }

    public string Key { get; }

    public CodeSuggestionRowKind Kind { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>The line in the original file (context and removed rows), else −1.</summary>
    public int OriginalLine { get; set; } = -1;

    /// <summary>The line in the file with every hunk applied (added rows), else −1.</summary>
    public int ProposedLine { get; set; } = -1;

    public CodeSuggestionHunkState? Hunk { get; set; }

    /// <summary>The line number in the gutter, or null for a line not in the file as it now stands.</summary>
    public string? Number { get; set; }

    public MotionValue<double> Open { get; }

    public MotionValue<double> Fade { get; }

    public MotionValue<double> Tint { get; }

    /// <summary>The height target last asked for (so a sync only animates what changed).</summary>
    public double OpenTarget { get; set; } = double.NaN;

    public double TintTarget { get; set; } = double.NaN;

    public TextLayout? Layout { get; set; }
}

/// <summary>One hunk on screen: its span, its decision and its review bar.</summary>
internal sealed class CodeSuggestionHunkState
{
    public CodeSuggestionHunkState(string id, CodeSuggestionReviewBar bar, MotionValue<double> barOpen)
    {
        Id = id;
        Bar = bar;
        BarOpen = barOpen;
    }

    public string Id { get; }

    public CodeSuggestionSpan Span { get; set; } = null!;

    /// <summary>1-based position among the hunks that apply (names a hunk without a summary).</summary>
    public int Number { get; set; }

    public CodeSuggestionDecision Decision { get; set; }

    public CodeSuggestionReviewBar Bar { get; }

    public MotionValue<double> BarOpen { get; }

    public double BarTarget { get; set; } = double.NaN;

    /// <summary>How many rows come before the bar (it sits after the hunk's last row).</summary>
    public int BarAfter { get; set; }

    /// <summary>Whether the hunk is still on screen (false once the status leaves streaming and ready).</summary>
    public bool IsShown { get; set; }
}

/// <summary>
/// The code of a <see cref="CodeSuggestion"/>, drawn: fixed rows with a pinned gutter (line numbers and the minus and plus
/// signs), the red and green tints of pending lines, the syntax colors of <see cref="CodeSuggestionHighlighter"/>, and the
/// review bars as children placed under their hunks and pinned to the visible width. It scrolls inside the card's
/// <see cref="ScrollViewer"/>; the gutter and the bars follow the horizontal offset so they never scroll away. Heights move
/// only through each row's <see cref="CodeSuggestionRow.Open"/> share of a known row height, so nothing is measured mid
/// animation but the sum. A frame allocates nothing: text layouts are kept per line text and size.
/// </summary>
internal sealed class CodeSuggestionView : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<CodeSuggestionView, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(FontSize), 13);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(RowHeight), 20);

    public static readonly StyledProperty<double> GutterWidthProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(GutterWidth), 40);

    public static readonly StyledProperty<double> SignWidthProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(SignWidth), 16);

    public static readonly StyledProperty<double> CodeInsetProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(CodeInset), 4);

    public static readonly StyledProperty<double> CodeEndPaddingProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(CodeEndPadding), 24);

    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<CodeSuggestionView, Thickness>(nameof(Padding));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<CodeSuggestionView, double>(nameof(BarHeight), 38);

    /// <summary>The plain code color (tokens without a role).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(Foreground));

    /// <summary>The line numbers.</summary>
    public static readonly StyledProperty<IBrush?> NumberBrushProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(NumberBrush));

    /// <summary>The panel color: under the pinned gutter, and the light or dark palette of the highlighter.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> RemovedTintProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(RemovedTint));

    public static readonly StyledProperty<IBrush?> AddedTintProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(AddedTint));

    public static readonly StyledProperty<IBrush?> RemovedInkProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(RemovedInk));

    public static readonly StyledProperty<IBrush?> AddedInkProperty =
        AvaloniaProperty.Register<CodeSuggestionView, IBrush?>(nameof(AddedInk));

    private readonly List<CodeSuggestionRow> _rows = [];
    private readonly List<CodeSuggestionHunkState> _hunks = [];
    private readonly Dictionary<string, (ImmutableArray<HighlightToken>? Tokens, TextLayout Layout)> _layouts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private readonly List<string> _stale = [];
    private readonly Dictionary<string, TextLayout> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, IBrush> _brushes = new();
    private TextLayout? _minus;
    private TextLayout? _plus;
    private double _textWidth;
    private double _pinX;
    private double _viewportWidth = double.NaN;

    static CodeSuggestionView()
    {
        AffectsMeasure<CodeSuggestionView>(FontFamilyProperty, FontSizeProperty, RowHeightProperty, GutterWidthProperty, SignWidthProperty,
            CodeInsetProperty, CodeEndPaddingProperty, PaddingProperty, BarHeightProperty);
        AffectsRender<CodeSuggestionView>(ForegroundProperty, NumberBrushProperty, BackgroundProperty, RemovedTintProperty, AddedTintProperty,
            RemovedInkProperty, AddedInkProperty);
        ClipToBoundsProperty.OverrideDefaultValue<CodeSuggestionView>(false);
    }

    public CodeSuggestionView()
    {
        Original = new CodeSuggestionHighlighter(OnTokens);
        Proposed = new CodeSuggestionHighlighter(OnTokens);
    }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double GutterWidth { get => GetValue(GutterWidthProperty); set => SetValue(GutterWidthProperty, value); }

    public double SignWidth { get => GetValue(SignWidthProperty); set => SetValue(SignWidthProperty, value); }

    public double CodeInset { get => GetValue(CodeInsetProperty); set => SetValue(CodeInsetProperty, value); }

    public double CodeEndPadding { get => GetValue(CodeEndPaddingProperty); set => SetValue(CodeEndPaddingProperty, value); }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? NumberBrush { get => GetValue(NumberBrushProperty); set => SetValue(NumberBrushProperty, value); }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? RemovedTint { get => GetValue(RemovedTintProperty); set => SetValue(RemovedTintProperty, value); }

    public IBrush? AddedTint { get => GetValue(AddedTintProperty); set => SetValue(AddedTintProperty, value); }

    public IBrush? RemovedInk { get => GetValue(RemovedInkProperty); set => SetValue(RemovedInkProperty, value); }

    public IBrush? AddedInk { get => GetValue(AddedInkProperty); set => SetValue(AddedInkProperty, value); }

    /// <summary>The colors of the original file (context and removed rows).</summary>
    public CodeSuggestionHighlighter Original { get; }

    /// <summary>The colors of the file with every hunk applied (added rows).</summary>
    public CodeSuggestionHighlighter Proposed { get; }

    /// <summary>The rows in order (the owner edits it, then calls <see cref="Refresh"/>).</summary>
    public List<CodeSuggestionRow> Rows => _rows;

    /// <summary>The hunks with their bars (the owner edits it, then calls <see cref="Refresh"/>).</summary>
    public IReadOnlyList<CodeSuggestionHunkState> Hunks => _hunks;

    /// <summary>Whether the panel is dark (the highlighter's palette).</summary>
    public bool IsDark { get; private set; }

    /// <summary>Raised when the light or dark palette flips.</summary>
    public event Action? DarkChanged;

    /// <summary>The width of the code with its gutter (tests).</summary>
    internal double ContentWidth => GutterWidth + SignWidth + CodeInset + _textWidth + CodeEndPadding;

    /// <summary>The x of the pinned gutter and bars, and the visible width (from the scroll viewer).</summary>
    public void SetViewport(double offsetX, double width)
    {
        if (Math.Abs(offsetX - _pinX) < 0.01 && Math.Abs(width - _viewportWidth) < 0.01)
        {
            return;
        }
        var widthChanged = Math.Abs(width - _viewportWidth) >= 0.01;
        _pinX = offsetX;
        _viewportWidth = width;
        if (widthChanged)
        {
            InvalidateMeasure();
        }
        InvalidateArrange();
        InvalidateVisual();
    }

    public void AddHunk(CodeSuggestionHunkState hunk)
    {
        _hunks.Add(hunk);
        VisualChildren.Add(hunk.Bar);
        LogicalChildren.Add(hunk.Bar);
    }

    public void RemoveHunk(CodeSuggestionHunkState hunk)
    {
        _hunks.Remove(hunk);
        VisualChildren.Remove(hunk.Bar);
        LogicalChildren.Remove(hunk.Bar);
    }

    /// <summary>Puts the hunks in file order (bars are placed by <see cref="CodeSuggestionHunkState.BarAfter"/>).</summary>
    public void SortHunks() => _hunks.Sort((a, b) => a.BarAfter != b.BarAfter ? a.BarAfter.CompareTo(b.BarAfter) : a.Span.Start.CompareTo(b.Span.Start));

    /// <summary>The rows or bars changed: measure again.</summary>
    public void Refresh()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>A row's or bar's height moved.</summary>
    public void OnHeightMoved()
    {
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void OnTokens() => Refresh();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == ForegroundProperty
            || change.Property == NumberBrushProperty || change.Property == RemovedInkProperty || change.Property == AddedInkProperty)
        {
            ClearLayouts();
        }
        if (change.Property == BackgroundProperty)
        {
            var dark = Background is ISolidColorBrush solid && Oklch.FromColor(solid.Color).L < 0.5;
            if (dark != IsDark)
            {
                IsDark = dark;
                DarkChanged?.Invoke();
            }
        }
    }

    private void ClearLayouts()
    {
        foreach (var entry in _layouts.Values)
        {
            entry.Layout.Dispose();
        }
        _layouts.Clear();
        foreach (var layout in _numbers.Values)
        {
            layout.Dispose();
        }
        _numbers.Clear();
        _minus?.Dispose();
        _plus?.Dispose();
        _minus = null;
        _plus = null;
        foreach (var row in _rows)
        {
            row.Layout = null;
        }
    }

    private Typeface Typeface => new(FontFamily);

    /// <summary>The row's text layout: kept per text and tokens, so a line is laid out once however often it streams by.</summary>
    private TextLayout LayoutOf(CodeSuggestionRow row)
    {
        var tokens = row.Kind == CodeSuggestionRowKind.Added
            ? Proposed.TokensFor(row.ProposedLine, row.Text)
            : Original.TokensFor(row.OriginalLine, row.Text);
        // Plain and highlighted versions of a line are kept apart, so a row waiting for its colors never evicts another's.
        var key = (tokens is null ? "p\0" : "h\0") + row.Text;
        _used.Add(key);
        if (_layouts.TryGetValue(key, out var cached) && SameTokens(cached.Tokens, tokens))
        {
            return cached.Layout;
        }
        cached.Layout?.Dispose();
        var layout = Build(row.Text, tokens);
        _layouts[key] = (tokens, layout);
        return layout;
    }

    private static bool SameTokens(ImmutableArray<HighlightToken>? a, ImmutableArray<HighlightToken>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        return a.Value == b.Value || a.Value.AsSpan().SequenceEqual(b.Value.AsSpan());
    }

    private TextLayout Build(string text, ImmutableArray<HighlightToken>? tokens)
    {
        var typeface = Typeface;
        var size = FontSize;
        ValueSpan<TextRunProperties>[]? spans = null;
        if (tokens is { Length: > 0 } list)
        {
            var kept = new List<ValueSpan<TextRunProperties>>(list.Length);
            foreach (var token in list)
            {
                var start = Math.Clamp(token.Start, 0, text.Length);
                var length = Math.Clamp(token.Length, 0, text.Length - start);
                if (length == 0 || token.Foreground == 0)
                {
                    continue;
                }
                kept.Add(new ValueSpan<TextRunProperties>(start, length,
                    new GenericTextRunProperties(typeface, size, null, BrushFor(token.Foreground))));
            }
            spans = kept.Count > 0 ? [.. kept] : null;
        }
        return new TextLayout(text.Length == 0 ? " " : text, typeface, size, Foreground ?? Brushes.Gray, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.None, null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, RowHeight, 0, 0,
            null, spans);
    }

    private IBrush BrushFor(uint argb)
    {
        if (!_brushes.TryGetValue(argb, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromUInt32(argb));
            _brushes[argb] = brush;
        }
        return brush;
    }

    private TextLayout Small(string text, IBrush? brush) =>
        new(text, Typeface, Math.Max(1, FontSize - 1), brush ?? Brushes.Gray, TextAlignment.Right, TextWrapping.NoWrap, TextTrimming.None,
            null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, RowHeight);

    private TextLayout NumberOf(string number)
    {
        if (!_numbers.TryGetValue(number, out var layout))
        {
            layout = Small(number, NumberBrush);
            _numbers[number] = layout;
        }
        return layout;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        _used.Clear();
        foreach (var row in _rows)
        {
            if (row.Open.Value <= 0.001 && row.OpenTarget <= 0)
            {
                continue;
            }
            row.Layout = LayoutOf(row);
            width = Math.Max(width, row.Text.Length == 0 ? 0 : row.Layout.WidthIncludingTrailingWhitespace);
        }
        _textWidth = width;
        PruneLayouts();
        _minus ??= Small("−", RemovedInk);
        _plus ??= Small("+", AddedInk);
        var barWidth = double.IsNaN(_viewportWidth) || _viewportWidth <= 0
            ? double.IsFinite(availableSize.Width) ? availableSize.Width : ContentWidth
            : _viewportWidth;
        foreach (var hunk in _hunks)
        {
            hunk.Bar.Measure(new Size(barWidth, BarHeight));
        }
        return new Size(ContentWidth, TotalHeight());
    }

    /// <summary>Drops the layouts of lines no longer shown once the cache holds clearly more than the rows need.</summary>
    private void PruneLayouts()
    {
        if (_layouts.Count <= _rows.Count * 2 + 32)
        {
            return;
        }
        _stale.Clear();
        foreach (var key in _layouts.Keys)
        {
            if (!_used.Contains(key))
            {
                _stale.Add(key);
            }
        }
        foreach (var key in _stale)
        {
            _layouts[key].Layout.Dispose();
            _layouts.Remove(key);
        }
    }

    private double TotalHeight()
    {
        var height = Padding.Top + Padding.Bottom;
        foreach (var row in _rows)
        {
            height += RowHeight * Math.Max(0, row.Open.Value);
        }
        foreach (var hunk in _hunks)
        {
            height += BarHeight * Math.Max(0, hunk.BarOpen.Value);
        }
        return height;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var barWidth = double.IsNaN(_viewportWidth) || _viewportWidth <= 0 ? finalSize.Width : _viewportWidth;
        var y = Padding.Top;
        var h = 0;
        for (var i = 0; i <= _rows.Count; i++)
        {
            for (; h < _hunks.Count && _hunks[h].BarAfter <= i; h++)
            {
                y = PlaceBar(_hunks[h], y, barWidth);
            }
            if (i < _rows.Count)
            {
                y += RowHeight * Math.Max(0, _rows[i].Open.Value);
            }
        }
        return finalSize;
    }

    private double PlaceBar(CodeSuggestionHunkState hunk, double y, double width)
    {
        var open = Math.Clamp(hunk.BarOpen.Value, 0, 1);
        var bar = hunk.Bar;
        var shown = BarHeight * open;
        bar.IsVisible = open > 0.001;
        bar.Arrange(new Rect(_pinX, y, Math.Max(0, width), BarHeight));
        bar.Opacity = open;
        bar.SetVisibleHeight(shown);
        return y + shown;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // Clicks anywhere on the code land here (the code area takes focus).
        context.FillRectangle(Brushes.Transparent, bounds);
        var rowHeight = RowHeight;
        var gutter = GutterWidth + SignWidth;
        var codeX = gutter + CodeInset;
        var right = Math.Max(bounds.Width, _pinX + (double.IsNaN(_viewportWidth) ? 0 : _viewportWidth));
        var y = Padding.Top;
        var h = 0;
        for (var i = 0; i < _rows.Count; i++)
        {
            for (; h < _hunks.Count && _hunks[h].BarAfter <= i; h++)
            {
                y += BarHeight * Math.Clamp(_hunks[h].BarOpen.Value, 0, 1);
            }
            var row = _rows[i];
            var open = Math.Clamp(row.Open.Value, 0, 1);
            var height = rowHeight * open;
            if (height < 0.01)
            {
                continue;
            }
            var rect = new Rect(0, y, right, height);
            using (context.PushClip(rect))
            {
                DrawRow(context, row, y, right, codeX, gutter);
            }
            y += height;
        }
    }

    private void DrawRow(DrawingContext context, CodeSuggestionRow row, double y, double right, double codeX, double gutter)
    {
        var rowHeight = RowHeight;
        var tint = Math.Clamp(row.Tint.Value, 0, 1);
        var fade = Math.Clamp(row.Fade.Value, 0, 1);
        var removed = row.Kind == CodeSuggestionRowKind.Removed;
        var added = row.Kind == CodeSuggestionRowKind.Added;
        var tintBrush = removed ? RemovedTint : added ? AddedTint : null;
        var full = new Rect(0, y, right, rowHeight);
        using (context.PushOpacity(fade))
        {
            if (tintBrush is not null && tint > 0.001)
            {
                using (context.PushOpacity(tint))
                {
                    context.FillRectangle(tintBrush, full);
                }
            }
            if (row.Layout is { } layout && row.Text.Length > 0)
            {
                layout.Draw(context, new Point(codeX, y + (rowHeight - layout.Height) / 2));
            }
            // The gutter stays pinned to the left edge of what is visible: the panel color covers the scrolled code, the
            // row's tint goes back over it, then the number and the sign.
            var pinned = new Rect(_pinX, y, gutter, rowHeight);
            if (Background is { } background)
            {
                context.FillRectangle(background, pinned);
            }
            if (tintBrush is not null && tint > 0.001)
            {
                using (context.PushOpacity(tint))
                {
                    context.FillRectangle(tintBrush, pinned);
                }
            }
            if (row.Number is { } number)
            {
                var numberLayout = NumberOf(number);
                numberLayout.Draw(context, new Point(_pinX + GutterWidth - numberLayout.WidthIncludingTrailingWhitespace - 4,
                    y + (rowHeight - numberLayout.Height) / 2));
            }
            var sign = removed ? _minus : added ? _plus : null;
            if (sign is not null && tint > 0.001)
            {
                using (context.PushOpacity(tint))
                {
                    sign.Draw(context, new Point(_pinX + GutterWidth + (SignWidth - sign.WidthIncludingTrailingWhitespace) / 2,
                        y + (rowHeight - sign.Height) / 2));
                }
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Original.Resume();
        Proposed.Resume();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Original.Release();
        Proposed.Release();
    }
}
