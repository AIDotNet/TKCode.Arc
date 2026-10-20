using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Which field message a <see cref="FieldMessage"/> shows.</summary>
public enum FieldMessageKind
{
    /// <summary>Helper copy in <c>TextMuted</c>.</summary>
    Description,

    /// <summary>Error copy in <c>Danger</c>, announced assertively (Arc's <c>role="alert"</c>).</summary>
    Error
}

/// <summary>How a <see cref="FieldMessage"/> moves between two texts.</summary>
public enum FieldMessageSwap
{
    /// <summary>Only the words that changed rise in (Arc Input, Textarea, PasswordField); counts roll their digits.</summary>
    Words,

    /// <summary>The whole copy is replaced in the direction of <see cref="FieldMessage.Direction"/> (Arc PasswordStrength).</summary>
    Whole
}

/// <summary>
/// The helper / error row under a field (P4 + P6 + P14). The row opens its height from zero on the smooth spring
/// while the copy rises 0.35em out of a soft blur; when the copy changes only the words that changed rise in and
/// unblur, the words that left lift away, and counts in the copy ("12 characters") roll only the digits that
/// changed. Clearing the text closes the row on the same spring. Reduced motion shows the row at full height and
/// fades changed words in; Off is immediate. Drawn in <see cref="Render"/>: no control per word, and the only
/// layout work while animating is this row's own height. Assistive technology reads the plain text.
/// </summary>
public class FieldMessage : Control
{
    /// <summary>The copy. Null or empty closes the row.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FieldMessage, string?>(nameof(Text));

    /// <summary>Description or error (colour and announcement).</summary>
    public static readonly StyledProperty<FieldMessageKind> KindProperty =
        AvaloniaProperty.Register<FieldMessage, FieldMessageKind>(nameof(Kind));

    /// <summary>Word-level swaps (default) or whole-copy swaps.</summary>
    public static readonly StyledProperty<FieldMessageSwap> SwapProperty =
        AvaloniaProperty.Register<FieldMessage, FieldMessageSwap>(nameof(Swap));

    /// <summary>For <see cref="FieldMessageSwap.Whole"/>: 1 rises the new copy from below, -1 drops it from above.</summary>
    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<FieldMessage, int>(nameof(Direction), 1);

    /// <summary>Space inside the clipped row (Arc: <c>padding-top: var(--space-2)</c>), so the height opens from a true zero.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<FieldMessage>();

    /// <summary>Line box height in px; NaN uses 1.4 × <see cref="FontSize"/>.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<FieldMessage, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<FieldMessage>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<FieldMessage>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<FieldMessage>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<FieldMessage>();

    /// <summary>The copy. Null or empty closes the row.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Description or error.</summary>
    public FieldMessageKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>Word-level or whole-copy swaps.</summary>
    public FieldMessageSwap Swap { get => GetValue(SwapProperty); set => SetValue(SwapProperty, value); }

    /// <summary>Direction of whole-copy swaps.</summary>
    public int Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <summary>Space inside the clipped row.</summary>
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Line box height in px.</summary>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>True while the row is open or opening (its text is not empty).</summary>
    public bool IsOpen => _present;

    /// <summary>The words currently laid out (leaving words excluded), for tests and diagnostics.</summary>
    internal IReadOnlyList<string> LiveWords => _segments.Select(s => s.Word).ToList();

    private const double BlurSoft = 4;
    private const double BlurSubtle = 2;

    private sealed class Segment
    {
        public required string Key { get; init; }
        public string Word { get; set; } = string.Empty;
        public bool TrailingSpace { get; set; }
        public bool IsNumber { get; set; }
        public FormattedText? Shaped { get; set; }
        public List<Column>? Columns { get; set; }
        public int Line { get; set; }
        public double LastX { get; set; }
        public double LastY { get; set; }
        public MotionValue<double>? Enter { get; set; }
        public bool FadeOnly { get; set; }
        public MotionValue<double>? Exit { get; set; }
        public bool Leaving { get; set; }
        public int Direction { get; set; } = 1;
    }

    private sealed class Column
    {
        public int Place { get; init; }
        public char Char { get; set; }
        public char? OldChar { get; set; }
        public double Width { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public MotionValue<double>? Roll { get; set; }
        public int Direction { get; set; } = 1;
        public bool Leaving { get; set; }
    }

    private readonly List<Segment> _segments = new();
    private readonly List<Segment> _leaving = new();
    private readonly Dictionary<char, FormattedText> _glyphs = new();
    private FormattedText? _space;
    private double _digitWidth;
    private bool _shapeDirty = true;
    private bool _present;
    private bool _measuring;
    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _rise;

    static FieldMessage()
    {
        AffectsMeasure<FieldMessage>(PaddingProperty, LineHeightProperty, SwapProperty);
        AffectsRender<FieldMessage>(ForegroundProperty);
        ClipToBoundsProperty.OverrideDefaultValue<FieldMessage>(true);
        IsHitTestVisibleProperty.OverrideDefaultValue<FieldMessage>(false);
    }

    private double EffectiveLineHeight => double.IsNaN(LineHeight) || LineHeight <= 0 ? FontSize * 1.4 : LineHeight;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            ApplyText(Text ?? string.Empty, IsLoaded);
            AutomationProperties.SetName(this, Text ?? string.Empty);
        }
        else if (change.Property == KindProperty)
        {
            AutomationProperties.SetLiveSetting(this, Kind == FieldMessageKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Off);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty)
        {
            _shapeDirty = true;
            InvalidateMeasure();
        }
        else if (change.Property == ForegroundProperty)
        {
            var brush = Foreground ?? Brushes.Black;
            foreach (var segment in _segments.Concat(_leaving))
            {
                segment.Shaped?.SetForegroundBrush(brush);
            }
            foreach (var glyph in _glyphs.Values)
            {
                glyph.SetForegroundBrush(brush);
            }
        }
        else if (change.Property == SwapProperty && _segments.Count > 0)
        {
            ClearSegments();
            Build(Text ?? string.Empty, null);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FieldMessagePeer(this);

    private sealed class FieldMessagePeer : ControlAutomationPeer
    {
        public FieldMessagePeer(FieldMessage owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsControlElementCore() => !string.IsNullOrEmpty(((FieldMessage)Owner).Text);

        protected override bool IsContentElementCore() => IsControlElementCore();
    }

    // ---- state changes ----------------------------------------------------------------------------------------

    private void ApplyText(string next, bool loaded)
    {
        var level = loaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var full = level == ArcMotionLevel.Full;
        if (next.Length == 0)
        {
            if (!_present)
            {
                return;
            }
            _present = false;
            if (full)
            {
                // The copy stays while the row closes on the smooth spring; opacity leaves on the instant tween.
                EnsureRowMotion();
                _ = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            else
            {
                ClearSegments();
                _opacity?.Jump(1);
            }
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        if (!_present)
        {
            _present = true;
            var closed = _segments.Count == 0 || (_height?.Value ?? 0) < 0.5;
            if (closed)
            {
                // A fresh row: the words arrive together with the row (initial={false} for the words).
                ClearSegments();
                Build(next, null);
                if (full)
                {
                    EnsureRowMotion();
                    _opacity!.Jump(0);
                    _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                    _rise!.Jump(0);
                    _ = _rise.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
                }
                else
                {
                    _opacity?.Jump(1);
                    _rise?.Jump(1);
                }
            }
            else
            {
                Diff(next, level);
                if (full)
                {
                    _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
                else
                {
                    _opacity?.Jump(1);
                }
            }
        }
        else
        {
            Diff(next, level);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EnsureRowMotion()
    {
        if (_opacity is null)
        {
            _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _opacity.Changed += _ => InvalidateVisual();
        }
        if (_rise is null)
        {
            _rise = new MotionValue<double>(this, 1, MotionVectors.Double);
            _rise.Changed += _ => InvalidateVisual();
        }
    }

    private void ClearSegments()
    {
        foreach (var segment in _segments.Concat(_leaving))
        {
            DisposeSegment(segment);
        }
        _segments.Clear();
        _leaving.Clear();
    }

    private static void DisposeSegment(Segment segment)
    {
        segment.Enter?.Dispose();
        segment.Exit?.Dispose();
        if (segment.Columns is { } columns)
        {
            foreach (var column in columns)
            {
                column.Presence?.Dispose();
                column.Roll?.Dispose();
            }
        }
    }

    /// <summary>Builds the segments of <paramref name="text"/>; <paramref name="level"/> null means no entry motion.</summary>
    private void Build(string text, ArcMotionLevel? level)
    {
        if (text.Length == 0)
        {
            return;
        }
        if (Swap == FieldMessageSwap.Whole)
        {
            var whole = new Segment { Key = text, Word = text, Direction = Direction >= 0 ? 1 : -1 };
            StartEnter(whole, level);
            _segments.Add(whole);
            return;
        }
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            var segment = CreateWord(i, words[i], i < words.Length - 1);
            StartEnter(segment, level);
            _segments.Add(segment);
        }
    }

    private static string KeyOf(int index, string word) => FieldDrawing.IsNumber(word)
        ? string.Create(CultureInfo.InvariantCulture, $"{index}:#")
        : string.Create(CultureInfo.InvariantCulture, $"{index}:{word}");

    private Segment CreateWord(int index, string word, bool trailingSpace)
    {
        var segment = new Segment
        {
            Key = KeyOf(index, word),
            Word = word,
            TrailingSpace = trailingSpace,
            IsNumber = FieldDrawing.IsNumber(word)
        };
        if (segment.IsNumber)
        {
            segment.Columns = new List<Column>(word.Length);
            for (var i = 0; i < word.Length; i++)
            {
                segment.Columns.Add(new Column { Place = word.Length - i, Char = word[i] });
            }
        }
        return segment;
    }

    private void Diff(string next, ArcMotionLevel level)
    {
        if (Swap == FieldMessageSwap.Whole)
        {
            var current = _segments.Count > 0 ? _segments[0] : null;
            if (current is not null && current.Word == next)
            {
                return;
            }
            var direction = Direction >= 0 ? 1 : -1;
            if (current is not null)
            {
                current.Direction = direction;
                Leave(current, level);
            }
            _segments.Clear();
            var whole = new Segment { Key = next, Word = next, Direction = direction };
            StartEnter(whole, level);
            _segments.Add(whole);
            return;
        }

        var words = next.Split(' ');
        var live = new Dictionary<string, Segment>(_segments.Count, StringComparer.Ordinal);
        foreach (var segment in _segments)
        {
            live[segment.Key] = segment;
        }
        var ordered = new List<Segment>(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var trailing = i < words.Length - 1;
            if (live.Remove(KeyOf(i, word), out var existing))
            {
                if (existing.IsNumber && existing.Word != word)
                {
                    UpdateNumber(existing, word, level);
                }
                if (existing.TrailingSpace != trailing)
                {
                    existing.TrailingSpace = trailing;
                    existing.Shaped = null;
                }
                ordered.Add(existing);
                continue;
            }
            var segment = CreateWord(i, word, trailing);
            StartEnter(segment, level);
            ordered.Add(segment);
        }
        foreach (var gone in live.Values)
        {
            Leave(gone, level);
        }
        _segments.Clear();
        _segments.AddRange(ordered);
    }

    private void StartEnter(Segment segment, ArcMotionLevel? level)
    {
        if (level is not { } l || l == ArcMotionLevel.Off)
        {
            return;
        }
        var reduced = l == ArcMotionLevel.Reduced;
        segment.FadeOnly = reduced;
        segment.Enter = new MotionValue<double>(this, 0, MotionVectors.Double);
        segment.Enter.Changed += _ => InvalidateVisual();
        _ = segment.Enter.AnimateTo(1, reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }

    private void Leave(Segment segment, ArcMotionLevel level)
    {
        if (level != ArcMotionLevel.Full)
        {
            DisposeSegment(segment);
            return;
        }
        segment.Leaving = true;
        segment.Enter?.Dispose();
        segment.Enter = null;
        segment.Exit = new MotionValue<double>(this, 0, MotionVectors.Double);
        segment.Exit.Changed += _ => InvalidateVisual();
        _leaving.Add(segment);
        var duration = Swap == FieldMessageSwap.Whole ? ArcMotionPresets.Fast : ArcMotionPresets.Exit;
        _ = RemoveWhenDone(segment, segment.Exit.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.Standard)));
    }

    private async Task RemoveWhenDone(Segment segment, Task<bool> exit)
    {
        await exit;
        if (_leaving.Remove(segment))
        {
            DisposeSegment(segment);
            InvalidateVisual();
        }
    }

    private void UpdateNumber(Segment segment, string word, ArcMotionLevel level)
    {
        var previous = FieldDrawing.LeadingNumber(segment.Word);
        var value = FieldDrawing.LeadingNumber(word);
        var direction = value < previous ? -1 : 1;
        segment.Word = word;
        var columns = segment.Columns ??= new List<Column>();
        var byPlace = new Dictionary<int, Column>(columns.Count);
        foreach (var column in columns)
        {
            byPlace[column.Place] = column;
        }
        var full = level == ArcMotionLevel.Full;
        var ordered = new List<Column>(Math.Max(columns.Count, word.Length));
        // Places that no longer exist narrow away on the left.
        foreach (var column in columns)
        {
            if (column.Place > word.Length)
            {
                if (!full)
                {
                    column.Presence?.Dispose();
                    column.Roll?.Dispose();
                    continue;
                }
                if (!column.Leaving)
                {
                    column.Leaving = true;
                    var presence = EnsurePresence(column, 1);
                    _ = RemoveColumnWhenDone(segment, column, presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph)));
                }
                ordered.Add(column);
            }
        }
        for (var i = 0; i < word.Length; i++)
        {
            var place = word.Length - i;
            var ch = word[i];
            if (byPlace.TryGetValue(place, out var column))
            {
                if (column.Leaving)
                {
                    column.Leaving = false;
                    _ = EnsurePresence(column, 0).AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                }
                if (column.Char != ch)
                {
                    column.OldChar = column.Char;
                    column.Char = ch;
                    column.Direction = direction;
                    column.Width = 0;
                    if (full)
                    {
                        column.Roll ??= CreateProgress();
                        column.Roll.Jump(0);
                        _ = column.Roll.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
                    }
                    else
                    {
                        column.OldChar = null;
                    }
                }
                ordered.Add(column);
            }
            else
            {
                var added = new Column { Place = place, Char = ch, Direction = direction };
                if (full)
                {
                    _ = EnsurePresence(added, 0).AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                }
                ordered.Add(added);
            }
        }
        columns.Clear();
        columns.AddRange(ordered);
    }

    private MotionValue<double> EnsurePresence(Column column, double initial)
    {
        if (column.Presence is null)
        {
            column.Presence = CreateProgress();
            column.Presence.Jump(initial);
        }
        return column.Presence;
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private async Task RemoveColumnWhenDone(Segment segment, Column column, Task<bool> exit)
    {
        var done = await exit;
        if (done && column.Leaving && segment.Columns is { } columns && columns.Remove(column))
        {
            column.Presence?.Dispose();
            column.Roll?.Dispose();
            InvalidateMeasure();
        }
    }

    // ---- shaping and layout -----------------------------------------------------------------------------------

    private Typeface Typeface => new(FontFamily, FontStyle.Normal, FontWeight);

    private FormattedText Shape(string text) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        Typeface, FontSize, Foreground ?? Brushes.Black);

    private FormattedText Glyph(char ch)
    {
        if (!_glyphs.TryGetValue(ch, out var glyph))
        {
            glyph = Shape(ch.ToString());
            _glyphs[ch] = glyph;
        }
        return glyph;
    }

    private void EnsureShaped()
    {
        if (_shapeDirty)
        {
            _shapeDirty = false;
            _glyphs.Clear();
            _space = null;
            _digitWidth = 0;
            foreach (var segment in _segments.Concat(_leaving))
            {
                segment.Shaped = null;
                if (segment.Columns is { } columns)
                {
                    foreach (var column in columns)
                    {
                        column.Width = 0;
                    }
                }
            }
        }
        if (_digitWidth <= 0)
        {
            for (var d = '0'; d <= '9'; d++)
            {
                _digitWidth = Math.Max(_digitWidth, Glyph(d).WidthIncludingTrailingWhitespace);
            }
        }
        _space ??= Shape(" ");
        foreach (var segment in _segments)
        {
            if (segment.IsNumber && segment.Columns is { } columns)
            {
                foreach (var column in columns)
                {
                    if (column.Width <= 0)
                    {
                        column.Width = char.IsAsciiDigit(column.Char)
                            ? _digitWidth
                            : Glyph(column.Char).WidthIncludingTrailingWhitespace;
                    }
                }
            }
            else
            {
                segment.Shaped ??= Shape(segment.TrailingSpace ? segment.Word + " " : segment.Word);
            }
        }
    }

    private double TargetWidth(Segment segment)
    {
        if (!segment.IsNumber || segment.Columns is null)
        {
            return segment.Shaped?.WidthIncludingTrailingWhitespace ?? 0;
        }
        var width = 0.0;
        foreach (var column in segment.Columns)
        {
            if (!column.Leaving)
            {
                width += column.Width;
            }
        }
        return width + (segment.TrailingSpace ? _space?.WidthIncludingTrailingWhitespace ?? 0 : 0);
    }

    private double CurrentWidth(Segment segment)
    {
        if (!segment.IsNumber || segment.Columns is null)
        {
            return segment.Shaped?.WidthIncludingTrailingWhitespace ?? 0;
        }
        var width = 0.0;
        foreach (var column in segment.Columns)
        {
            width += column.Width * Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
        }
        return width + (segment.TrailingSpace ? _space?.WidthIncludingTrailingWhitespace ?? 0 : 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            EnsureShaped();
            var padding = Padding;
            var lineHeight = EffectiveLineHeight;
            var maxWidth = double.IsInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Width - padding.Left - padding.Right);
            double content = 0;
            double natural = 0;
            if (_segments.Count > 0)
            {
                if (Swap == FieldMessageSwap.Whole)
                {
                    var shaped = _segments[0].Shaped!;
                    shaped.LineHeight = lineHeight;
                    if (!double.IsInfinity(maxWidth) && maxWidth > 0)
                    {
                        shaped.MaxTextWidth = maxWidth;
                    }
                    content = Math.Max(lineHeight, shaped.Height);
                    natural = shaped.WidthIncludingTrailingWhitespace;
                }
                else
                {
                    var line = 0;
                    var x = 0.0;
                    foreach (var segment in _segments)
                    {
                        var width = TargetWidth(segment);
                        if (x > 0 && x + width > maxWidth + 0.01)
                        {
                            line++;
                            x = 0;
                        }
                        segment.Line = line;
                        x += width;
                        natural = Math.Max(natural, x);
                    }
                    content = (line + 1) * lineHeight;
                }
                content += padding.Top + padding.Bottom;
            }
            var target = _present ? content : 0;
            if (_height is null)
            {
                _height = new MotionValue<double>(this, target, MotionVectors.Double);
                _height.Changed += _ =>
                {
                    if (!_measuring)
                    {
                        InvalidateMeasure();
                    }
                };
            }
            else if (Math.Abs(_height.Target - target) > 0.25)
            {
                if (IsLoaded && ArcMotion.IsFull(this))
                {
                    var task = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
                    if (target <= 0)
                    {
                        _ = ClearWhenClosed(task);
                    }
                }
                else
                {
                    _height.Jump(target);
                    if (target <= 0)
                    {
                        ClearSegments();
                    }
                }
            }
            var desiredWidth = double.IsInfinity(availableSize.Width)
                ? natural + padding.Left + padding.Right
                : availableSize.Width;
            return new Size(desiredWidth, Math.Max(0, _height.Value));
        }
        finally
        {
            _measuring = false;
        }
    }

    private async Task ClearWhenClosed(Task<bool> closing)
    {
        var done = await closing;
        if (done && !_present)
        {
            ClearSegments();
            InvalidateVisual();
        }
    }

    // ---- rendering ----------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (_segments.Count == 0 && _leaving.Count == 0)
        {
            return;
        }
        var rowOpacity = Math.Clamp(_opacity?.Value ?? 1, 0, 1);
        if (rowOpacity <= 0.001)
        {
            return;
        }
        var em = FontSize;
        var lineHeight = EffectiveLineHeight;
        var padding = Padding;
        var rise = Math.Clamp(_rise?.Value ?? 1, 0, 1);
        var bounds = new Rect(Bounds.Size);
        using (context.PushOpacity(rowOpacity))
        using (context.PushTransform(Matrix.CreateTranslation(0, 0.35 * em * (1 - rise))))
        using (PushBlur(context, BlurSoft * (1 - rise), bounds))
        {
            var line = -1;
            var x = 0.0;
            foreach (var segment in _segments)
            {
                if (segment.Line != line)
                {
                    line = segment.Line;
                    x = padding.Left;
                }
                var y = padding.Top + line * lineHeight;
                segment.LastX = x;
                segment.LastY = y;
                var width = CurrentWidth(segment);
                DrawSegment(context, segment, x, y, lineHeight, em, new Rect(x, y, width, lineHeight));
                x += width;
            }
            foreach (var segment in _leaving)
            {
                DrawSegment(context, segment, segment.LastX, segment.LastY, lineHeight, em,
                    new Rect(segment.LastX, segment.LastY, CurrentWidth(segment), lineHeight));
            }
        }
    }

    private static DrawingContext.PushedState? PushBlur(DrawingContext context, double radius, Rect bounds)
    {
        var effect = ArcBlur.Get(radius);
        return effect is null ? null : context.PushEffect(effect, bounds);
    }

    private void DrawSegment(DrawingContext context, Segment segment, double x, double y, double lineHeight, double em, Rect bounds)
    {
        if (Swap == FieldMessageSwap.Whole && segment.Shaped is { } wholeText)
        {
            bounds = new Rect(x, y, wholeText.WidthIncludingTrailingWhitespace, Math.Max(lineHeight, wholeText.Height));
        }
        var rise = Swap == FieldMessageSwap.Whole ? 0.3 : 0.35;
        double opacity = 1, dy = 0, blur = 0;
        if (segment.Leaving && segment.Exit is { } exit)
        {
            var q = Math.Clamp(exit.Value, 0, 1);
            opacity = 1 - q;
            dy = -rise * em * q * segment.Direction;
            blur = BlurSubtle * q;
        }
        else if (segment.Enter is { } enter)
        {
            var p = Math.Clamp(enter.Value, 0, 1);
            opacity = p;
            if (!segment.FadeOnly)
            {
                dy = rise * em * (1 - p) * segment.Direction;
                blur = BlurSoft * (1 - p);
            }
        }
        if (opacity <= 0.001)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        using (context.PushTransform(Matrix.CreateTranslation(0, dy)))
        using (PushBlur(context, blur, bounds))
        {
            if (segment.IsNumber && segment.Columns is { } columns)
            {
                DrawColumns(context, columns, x, y, lineHeight, em);
            }
            else if (segment.Shaped is { } shaped)
            {
                var top = Swap == FieldMessageSwap.Whole ? y : y + (lineHeight - shaped.Height) / 2;
                context.DrawText(shaped, new Point(x, top));
            }
        }
    }

    private void DrawColumns(DrawingContext context, List<Column> columns, double x, double y, double lineHeight, double em)
    {
        foreach (var column in columns)
        {
            var presence = Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
            var width = column.Width * presence;
            if (presence > 0.001)
            {
                var glyph = Glyph(column.Char);
                var top = y + (lineHeight - glyph.Height) / 2;
                var gx = x + (width - glyph.WidthIncludingTrailingWhitespace) / 2;
                if (column.Roll is { IsAnimating: true } roll && column.OldChar is { } oldChar)
                {
                    // The old digit turns away and the new one turns in from the side the number moved.
                    var p = roll.Value;
                    var shown = Math.Clamp(p, 0, 1);
                    var travel = 0.6 * em * column.Direction;
                    var old = Glyph(oldChar);
                    var ox = x + (width - old.WidthIncludingTrailingWhitespace) / 2;
                    var cell = new Rect(x, y - Math.Abs(travel), Math.Max(width, 1), lineHeight + 2 * Math.Abs(travel));
                    using (context.PushOpacity((1 - shown) * presence))
                    using (PushBlur(context, BlurSubtle * shown, cell))
                    {
                        context.DrawText(old, new Point(ox, top - travel * p));
                    }
                    using (context.PushOpacity(shown * presence))
                    using (PushBlur(context, BlurSubtle * (1 - shown), cell))
                    {
                        context.DrawText(glyph, new Point(gx, top + travel * (1 - p)));
                    }
                }
                else
                {
                    using (context.PushOpacity(presence))
                    {
                        context.DrawText(glyph, new Point(gx, top));
                    }
                }
            }
            x += width;
        }
    }
}
