using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using StreamMark.Core.Highlighting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One snippet of an <see cref="InstallSnippet"/>, drawn in the monospace font and highlighted with the library palette
/// (<see cref="InstallSnippetHighlighter"/>). Every <c>{{key}}</c> is a key slot: until a key exists it shows the
/// placeholder, tinted. A new key eases the slot from the old text's width to the key's width (exact, in characters)
/// while the key wipes in from the left over 340ms, then its tint fades out. The view reserves
/// <see cref="MinLines"/> lines, so the longest snippet of a step sets every snippet's height; lines never wrap (the code
/// block scrolls sideways).
/// </summary>
/// <remarks>
/// Code-driven motion (<see cref="MotionValue{T}"/>): the slot is a character range inside drawn text, so its width, its
/// clip and its tint have no property a ControlTheme could animate. Reduced motion keeps the end states and drops the
/// wipe and the width ease (the tint still fades); Off changes everything at once.
/// </remarks>
internal sealed class InstallSnippetCodeView : Control
{
    /// <summary>How long the key takes to wipe in, and the slot to ease to its width.</summary>
    internal static readonly TimeSpan WipeDuration = TimeSpan.FromMilliseconds(340);

    /// <summary>How long the new key's tint takes to fade once the wipe is done.</summary>
    internal static readonly TimeSpan TintFadeDuration = ArcMotionPresets.Considered;

    /// <summary>The monospace font.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<InstallSnippetCodeView>();

    /// <summary>The code's size.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<InstallSnippetCodeView>();

    /// <summary>Plain code's color.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<InstallSnippetCodeView>();

    /// <summary>The height of a line (inherited; NaN uses 1.55 times the font size).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<InstallSnippetCodeView>();

    /// <summary>The space around the code.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<InstallSnippetCodeView>();

    /// <summary>The lines the view reserves (the step's longest snippet).</summary>
    public static readonly StyledProperty<int> MinLinesProperty =
        AvaloniaProperty.Register<InstallSnippetCodeView, int>(nameof(MinLines), 1);

    /// <summary>The text color of a tinted slot (the placeholder, a key that just arrived).</summary>
    public static readonly StyledProperty<IBrush?> SlotForegroundProperty =
        AvaloniaProperty.Register<InstallSnippetCodeView, IBrush?>(nameof(SlotForeground));

    /// <summary>The fill behind a tinted slot.</summary>
    public static readonly StyledProperty<IBrush?> SlotBackgroundProperty =
        AvaloniaProperty.Register<InstallSnippetCodeView, IBrush?>(nameof(SlotBackground));

    /// <summary>The surface under the code; its lightness picks the light or dark syntax palette.</summary>
    public static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<InstallSnippetCodeView, IBrush?>(nameof(PageBrush));

    private readonly string _placeholder;
    private readonly List<Line> _lines = [];
    private readonly string _display;
    private readonly Dictionary<uint, IBrush> _brushes = new();
    private IReadOnlyList<ImmutableArray<HighlightToken>>? _tokens;
    private bool _dark;
    private bool _hasDark;
    private int _highlightVersion;
    private TaskCompletionSource? _highlighted;
    private string? _key;
    private string _from;
    private TextLayout? _fromLayout;
    private TextLayout? _keyLayout;
    private TextLayout? _placeholderLayout;
    private readonly Dictionary<IBrush, TextLayout> _plainKeyLayouts = new();
    private double _charWidth = double.NaN;
    private MotionValue<double>? _wipe;
    private MotionValue<double>? _chars;
    private MotionValue<double>? _tint;
    private double _wipeValue = 1;
    private double _charsValue;
    private double _tintValue;
    private int _keyVersion;

    static InstallSnippetCodeView()
    {
        AffectsMeasure<InstallSnippetCodeView>(FontFamilyProperty, FontSizeProperty, LineHeightProperty, PaddingProperty, MinLinesProperty);
        AffectsRender<InstallSnippetCodeView>(ForegroundProperty, SlotForegroundProperty, SlotBackgroundProperty);
        ClipToBoundsProperty.OverrideDefaultValue<InstallSnippetCodeView>(false);
    }

    /// <summary>Creates the view for a snippet (<c>{{key}}</c> marks the slots) showing <paramref name="key"/> or the placeholder.</summary>
    public InstallSnippetCodeView(string template, string language, string placeholder, string? key)
    {
        Template = InstallSnippetText.Normalize(template);
        Language = language;
        _placeholder = placeholder;
        _key = string.IsNullOrEmpty(key) ? null : key;
        _from = _placeholder;
        var display = new List<string>();
        foreach (var text in Template.Split('\n'))
        {
            var segments = InstallSnippetText.Split(text, placeholder, out var shown);
            display.Add(shown);
            _lines.Add(new Line(shown, segments));
        }
        _display = string.Join('\n', display);
        _charsValue = (_key ?? _placeholder).Length;
        _tintValue = 0;
        AutomationProperties.SetName(this, ShownText);
    }

    /// <summary>The snippet as given (normalized), with <c>{{key}}</c> in its slots.</summary>
    public string Template { get; }

    /// <summary>The highlighting language.</summary>
    public string Language { get; }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>The line height in use.</summary>
    internal double EffectiveLineHeight => double.IsNaN(LineHeight) || LineHeight <= 0 ? Math.Ceiling(FontSize * 1.55) : LineHeight;

    /// <inheritdoc cref="PaddingProperty"/>
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <inheritdoc cref="MinLinesProperty"/>
    public int MinLines { get => GetValue(MinLinesProperty); set => SetValue(MinLinesProperty, value); }

    /// <inheritdoc cref="SlotForegroundProperty"/>
    public IBrush? SlotForeground { get => GetValue(SlotForegroundProperty); set => SetValue(SlotForegroundProperty, value); }

    /// <inheritdoc cref="SlotBackgroundProperty"/>
    public IBrush? SlotBackground { get => GetValue(SlotBackgroundProperty); set => SetValue(SlotBackgroundProperty, value); }

    /// <inheritdoc cref="PageBrushProperty"/>
    public IBrush? PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }

    /// <summary>The key in the slots, or null while the placeholder shows.</summary>
    internal string? Key => _key;

    /// <summary>The text a slot shows until a key exists.</summary>
    internal string Placeholder => _placeholder;

    /// <summary>The code as it reads now (the key or the placeholder in every slot).</summary>
    internal string ShownText => Template.Replace(InstallSnippetText.KeyToken, _key ?? _placeholder, StringComparison.Ordinal);

    /// <summary>How far the key has wiped in (1 at rest).</summary>
    internal double WipeProgress => _wipeValue;

    /// <summary>The slot's width in characters as drawn now.</summary>
    internal double SlotCharacters => _charsValue;

    /// <summary>The new key's tint (1 just after it arrives, 0 at rest).</summary>
    internal double Tint => _key is null ? 1 : _tintValue;

    /// <summary>The number of lines.</summary>
    internal int LineCount => _lines.Count;

    /// <summary>Whether colors are in.</summary>
    internal bool HasTokens => _tokens is not null;

    /// <summary>Completes when the snippet has its colors (or cannot have any).</summary>
    internal Task WhenHighlighted => _highlighted?.Task ?? Task.CompletedTask;

    /// <summary>
    /// Puts <paramref name="key"/> in every slot (null: back to the placeholder). With <paramref name="animate"/> the slot
    /// eases to the key's width while the key wipes in, then its tint fades; otherwise it changes at once.
    /// </summary>
    public void SetKey(string? key, bool animate)
    {
        key = string.IsNullOrEmpty(key) ? null : key;
        if (key == _key)
        {
            return;
        }
        var from = _key ?? _placeholder;
        _key = key;
        _keyVersion++;
        _keyLayout?.Dispose();
        _keyLayout = null;
        ClearPlainKeyLayouts();
        AutomationProperties.SetName(this, ShownText);
        _wipe?.Stop();
        _chars?.Stop();
        _tint?.Stop();
        if (key is null || !animate || TopLevel.GetTopLevel(this) is null || !HasSlots
            || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _from = _placeholder;
            _wipeValue = 1;
            _charsValue = (key ?? _placeholder).Length;
            _tintValue = 0;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        _from = from;
        _fromLayout?.Dispose();
        _fromLayout = null;
        _ = PlayKey(from.Length, key!.Length, _keyVersion);
        InvalidateMeasure();
    }

    private bool HasSlots
    {
        get
        {
            foreach (var line in _lines)
            {
                foreach (var segment in line.Segments)
                {
                    if (segment.IsSlot)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    private async Task PlayKey(int fromChars, int toChars, int version)
    {
        _wipe ??= Track(v => _wipeValue = v);
        _chars ??= Track(v => _charsValue = v);
        _tint ??= Track(v => _tintValue = v);
        _tint.Jump(1);
        _tintValue = 1;
        if (ArcMotion.IsFull(this))
        {
            _wipe.Jump(0);
            _wipeValue = 0;
            _chars.Jump(fromChars);
            _charsValue = fromChars;
            InvalidateVisual();
            var spec = MotionSpec.Tween(WipeDuration, ArcEasings.Standard);
            var width = _chars.AnimateTo(toChars, spec);
            var wiped = await _wipe.AnimateTo(1, spec);
            await width;
            if (!wiped || version != _keyVersion)
            {
                return;
            }
        }
        else
        {
            _wipe.Jump(1);
            _wipeValue = 1;
            _chars.Jump(toChars);
            _charsValue = toChars;
            InvalidateMeasure();
        }
        if (version != _keyVersion)
        {
            return;
        }
        await _tint.AnimateTo(0, MotionSpec.Tween(TintFadeDuration, ArcEasings.Standard));
        if (version == _keyVersion)
        {
            // The slot now has the key's exact width: let the scroll extent follow.
            InvalidateMeasure();
        }
    }

    private MotionValue<double> Track(Action<double> write)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            write(v);
            InvalidateVisual();
        };
        return value;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == LineHeightProperty
            || change.Property == ForegroundProperty)
        {
            _charWidth = double.NaN;
            ClearLayouts();
        }
        else if (change.Property == SlotForegroundProperty)
        {
            ClearLayouts();
        }
        else if (change.Property == PageBrushProperty)
        {
            var page = (PageBrush as ISolidColorBrush)?.Color ?? Colors.White;
            var dark = Oklch.FromColor(page).L < 0.5;
            if (!_hasDark || dark != _dark)
            {
                _hasDark = true;
                _dark = dark;
                Highlight();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Highlight();
    }

    /// <summary>Takes the remembered colors of this snippet, or asks for them.</summary>
    private void Highlight()
    {
        var version = ++_highlightVersion;
        if (InstallSnippetHighlighter.TryGet(_display, Language, _dark, out var known))
        {
            ApplyTokens(known);
            _highlighted?.TrySetResult();
            return;
        }
        if (_highlighted is null || _highlighted.Task.IsCompleted)
        {
            _highlighted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _ = HighlightAsync(version);
    }

    private async Task HighlightAsync(int version)
    {
        var tokens = await InstallSnippetHighlighter.HighlightAsync(_display, Language, _dark);
        if (version == _highlightVersion)
        {
            ApplyTokens(tokens);
            _highlighted?.TrySetResult();
        }
    }

    private void ApplyTokens(IReadOnlyList<ImmutableArray<HighlightToken>>? tokens)
    {
        if (ReferenceEquals(tokens, _tokens))
        {
            return;
        }
        _tokens = tokens;
        ClearLayouts();
    }

    private void ClearLayouts()
    {
        foreach (var line in _lines)
        {
            line.Clear();
        }
        _keyLayout?.Dispose();
        _keyLayout = null;
        _fromLayout?.Dispose();
        _fromLayout = null;
        _placeholderLayout?.Dispose();
        _placeholderLayout = null;
        ClearPlainKeyLayouts();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ClearPlainKeyLayouts()
    {
        foreach (var layout in _plainKeyLayouts.Values)
        {
            layout.Dispose();
        }
        _plainKeyLayouts.Clear();
    }

    private Typeface Typeface => new(FontFamily);

    /// <summary>The width of one character of the monospace font.</summary>
    internal double CharWidth
    {
        get
        {
            if (double.IsNaN(_charWidth))
            {
                using var probe = new TextLayout("0000000000", Typeface, FontSize, Brushes.Black);
                _charWidth = Math.Max(1, probe.WidthIncludingTrailingWhitespace / 10);
            }
            return _charWidth;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var charWidth = CharWidth;
        // Measure with the widest slot of the move, so easing it never relayouts the block.
        var slotChars = Math.Max(Math.Max(_placeholder.Length, (_key ?? _placeholder).Length), _wipeValue < 1 || _tintValue > 0 ? _from.Length : 0);
        var widest = 0.0;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            EnsureLine(i, line);
            var width = 0.0;
            for (var s = 0; s < line.Segments.Count; s++)
            {
                width += line.Segments[s].IsSlot ? slotChars * charWidth : line.Layouts[s]!.WidthIncludingTrailingWhitespace;
            }
            widest = Math.Max(widest, width);
        }
        var lines = Math.Max(_lines.Count, Math.Max(1, MinLines));
        return new Size(Math.Ceiling(widest + padding.Left + padding.Right), lines * EffectiveLineHeight + padding.Top + padding.Bottom);
    }

    private void EnsureLine(int index, Line line)
    {
        if (line.Layouts.Length == line.Segments.Count && line.Built)
        {
            return;
        }
        line.Clear();
        line.Layouts = new TextLayout?[line.Segments.Count];
        line.SlotBrushes = new IBrush?[line.Segments.Count];
        var tokens = _tokens is { } all && index < all.Count ? all[index] : ImmutableArray<HighlightToken>.Empty;
        for (var s = 0; s < line.Segments.Count; s++)
        {
            var segment = line.Segments[s];
            if (segment.IsSlot)
            {
                line.SlotBrushes[s] = BrushAt(tokens, segment.Start);
                continue;
            }
            line.Layouts[s] = Build(line.Display.Substring(segment.Start, segment.Length), tokens, segment.Start, Foreground ?? Brushes.Black);
        }
        line.Built = true;
    }

    private TextLayout Build(string text, ImmutableArray<HighlightToken> tokens, int offset, IBrush foreground)
    {
        var typeface = Typeface;
        var size = FontSize;
        List<ValueSpan<TextRunProperties>>? spans = null;
        foreach (var token in tokens)
        {
            var from = Math.Max(token.Start - offset, 0);
            var to = Math.Min(token.Start + token.Length - offset, text.Length);
            if (to <= from)
            {
                continue;
            }
            spans ??= [];
            var face = token.Style.HasFlag(HighlightFontStyle.Italic) ? new Typeface(FontFamily, FontStyle.Italic) : typeface;
            spans.Add(new ValueSpan<TextRunProperties>(from, to - from,
                new GenericTextRunProperties(face, size, null, token.Foreground == 0 ? foreground : BrushFor(token.Foreground))));
        }
        return new TextLayout(text, typeface, size, foreground, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null,
            FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, EffectiveLineHeight, 0, 0, null, spans);
    }

    private TextLayout Plain(string text, IBrush brush) =>
        new(text, Typeface, FontSize, brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null,
            FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, EffectiveLineHeight);

    /// <summary>The color of the token that starts the slot (a key inside a string takes the string's color).</summary>
    private IBrush? BrushAt(ImmutableArray<HighlightToken> tokens, int position)
    {
        foreach (var token in tokens)
        {
            if (token.Start <= position && position < token.Start + token.Length && token.Foreground != 0)
            {
                return BrushFor(token.Foreground);
            }
        }
        return null;
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

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var padding = Padding;
        var lineHeight = EffectiveLineHeight;
        var charWidth = CharWidth;
        var slotBrush = SlotForeground ?? Foreground ?? Brushes.Black;
        var plainBrush = Foreground ?? Brushes.Black;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            EnsureLine(i, line);
            var y = padding.Top + i * lineHeight;
            var x = padding.Left;
            for (var s = 0; s < line.Segments.Count; s++)
            {
                if (line.Segments[s].IsSlot)
                {
                    x += DrawSlot(context, x, y, charWidth, lineHeight, slotBrush, line.SlotBrushes[s] ?? plainBrush);
                    continue;
                }
                var layout = line.Layouts[s]!;
                layout.Draw(context, new Point(x, y));
                x += layout.WidthIncludingTrailingWhitespace;
            }
        }
    }

    /// <summary>Draws one slot at <paramref name="x"/> and returns its width.</summary>
    private double DrawSlot(DrawingContext context, double x, double y, double charWidth, double lineHeight, IBrush slotBrush, IBrush plainBrush)
    {
        var width = (_key is null ? _placeholder.Length : _charsValue) * charWidth;
        var tint = Tint;
        if (tint > 0.001 && SlotBackground is { } fill)
        {
            using (context.PushOpacity(tint))
            {
                context.DrawRectangle(fill, null, new RoundedRect(new Rect(x - 2, y + 1, width + 4, lineHeight - 2), 4));
            }
        }
        if (_key is null)
        {
            (_placeholderLayout ??= Plain(_placeholder, slotBrush)).Draw(context, new Point(x, y));
            return width;
        }
        _keyLayout ??= Plain(_key, slotBrush);
        if (_wipeValue < 0.999)
        {
            var edge = x + _wipeValue * width;
            _fromLayout ??= Plain(_from, slotBrush);
            using (context.PushClip(new Rect(edge, y, Math.Max(0, x + width - edge), lineHeight)))
            using (context.PushOpacity(1 - _wipeValue))
            {
                _fromLayout.Draw(context, new Point(x, y));
            }
            using (context.PushClip(new Rect(x, y, Math.Max(0, edge - x), lineHeight)))
            {
                _keyLayout.Draw(context, new Point(x, y));
            }
            return width;
        }
        if (tint < 0.999)
        {
            if (!_plainKeyLayouts.TryGetValue(plainBrush, out var plain))
            {
                plain = Plain(_key, plainBrush);
                _plainKeyLayouts[plainBrush] = plain;
            }
            using (context.PushOpacity(1 - tint))
            {
                plain.Draw(context, new Point(x, y));
            }
        }
        if (tint > 0.001)
        {
            using (context.PushOpacity(tint))
            {
                _keyLayout.Draw(context, new Point(x, y));
            }
        }
        return width;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A view that leaves mid-wipe shows its end state when it comes back.
        _keyVersion++;
        _wipe?.Stop();
        _chars?.Stop();
        _tint?.Stop();
        _wipeValue = 1;
        _charsValue = (_key ?? _placeholder).Length;
        _tintValue = 0;
    }

    /// <summary>One line: its text with the placeholder in every slot, its segments and their layouts.</summary>
    private sealed class Line(string display, List<InstallSnippetSegment> segments)
    {
        public string Display { get; } = display;

        public List<InstallSnippetSegment> Segments { get; } = segments;

        public TextLayout?[] Layouts { get; set; } = [];

        public IBrush?[] SlotBrushes { get; set; } = [];

        public bool Built { get; set; }

        public void Clear()
        {
            foreach (var layout in Layouts)
            {
                layout?.Dispose();
            }
            Layouts = [];
            SlotBrushes = [];
            Built = false;
        }
    }
}
