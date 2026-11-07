using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Draws the active cues of a captions or subtitles track over the picture (design/components/video/video.md section 9).
/// <see cref="VideoPlayer"/> places it above its <see cref="VideoSurface"/> as <c>PART_Captions</c>, sized to the picture rectangle.
/// </summary>
/// <remarks>
/// <para>
/// Work happens only when something changes: a <see cref="Position"/> change costs one binary search and, unless the set of
/// active cues changed, nothing else. Text layouts are rebuilt when the cue set, the size, <see cref="CaptionScale"/> or the
/// font changes; <see cref="BottomInset"/> only moves the boxes. The player loads the track (<see cref="VideoTextTrack.LoadAsync"/>);
/// the layer follows <see cref="VideoTextTrack.CuesChanged"/> while it is attached.
/// </para>
/// <para>
/// Layout is WebVTT's (section 7.2 of the W3C spec), simplified: cues with an automatic line stack upward from the bottom
/// above <see cref="BottomInset"/> in start order; a numbered line snaps to rows of one-line box height (negative numbers count
/// from the bottom, which also keeps clear of the inset); a percentage puts the box top there. A box that would overlap an
/// earlier one moves past it (up for automatic and bottom-anchored lines, down for top-anchored ones, the other way when it runs
/// out of room). <c>position</c>, <c>size</c> and <c>align</c> place the box horizontally.
/// </para>
/// </remarks>
internal sealed class VideoCaptionLayer : Control
{
    /// <summary>Defines the <see cref="Track"/> property.</summary>
    public static readonly StyledProperty<VideoTextTrack?> TrackProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, VideoTextTrack?>(nameof(Track));

    /// <summary>Defines the <see cref="Position"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, TimeSpan>(nameof(Position));

    /// <summary>Defines the <see cref="BottomInset"/> property.</summary>
    public static readonly StyledProperty<double> BottomInsetProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, double>(nameof(BottomInset));

    /// <summary>Defines the <see cref="CaptionScale"/> property.</summary>
    public static readonly StyledProperty<double> CaptionScaleProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, double>(nameof(CaptionScale), 1d);

    /// <summary>Defines the <see cref="BackgroundOpacity"/> property.</summary>
    public static readonly StyledProperty<double> BackgroundOpacityProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, double>(nameof(BackgroundOpacity), 0.8);

    /// <summary>The caption font: the ArcDesign <c>BodyFontFamily</c> token, bound in the constructor.</summary>
    private static readonly StyledProperty<FontFamily> CaptionFontFamilyProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, FontFamily>("CaptionFontFamily", FontFamily.Default);

    /// <summary>The box corner radius: the ArcDesign <c>RadiusStep4</c> token, bound in the constructor.</summary>
    private static readonly StyledProperty<CornerRadius> CaptionCornerRadiusProperty =
        AvaloniaProperty.Register<VideoCaptionLayer, CornerRadius>("CaptionCornerRadius", new CornerRadius(4));

    /// <summary>Caption text height as a share of the picture height (WebVTT's 5vh, measured as a font size).</summary>
    internal const double HeightRatio = 0.045;

    /// <summary>Smallest caption font size.</summary>
    internal const double MinFontSize = 12;

    /// <summary>Largest caption font size.</summary>
    internal const double MaxFontSize = 40;

    /// <summary>Box padding left and right.</summary>
    internal const double PaddingX = 8;

    /// <summary>Box padding top and bottom.</summary>
    internal const double PaddingY = 4;

    /// <summary>Space between stacked boxes.</summary>
    internal const double StackGap = 2;

    /// <summary>Space below the lowest automatic cue, as a share of the font size (own choice: clear of the picture edge).</summary>
    internal const double BottomMarginRatio = 0.5;

    private static readonly IBrush s_textBrush = new ImmutableSolidColorBrush(Colors.White);

    private VideoCue[] _active = [];
    private readonly List<VideoCue> _scratch = [];
    private IReadOnlyList<VideoCue>? _indexed;
    private TimeSpan _longestCue;
    private VideoTextTrack? _subscribed;

    private CaptionBox[]? _boxes;
    private Size _builtSize;
    private bool _placed;
    private double _fontSize;
    private IBrush? _boxBrush;

    static VideoCaptionLayer()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<VideoCaptionLayer>(false);
        ClipToBoundsProperty.OverrideDefaultValue<VideoCaptionLayer>(true);
        AffectsRender<VideoCaptionLayer>(CaptionCornerRadiusProperty);
    }

    /// <summary>Creates the layer.</summary>
    public VideoCaptionLayer()
    {
        ArcTokens.Bind(this, CaptionFontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(this, CaptionCornerRadiusProperty, ArcDesignTokenKind.RadiusStep4);
    }

    /// <summary>The track whose cues are drawn; null draws nothing. Chapters and descriptions tracks draw nothing.</summary>
    public VideoTextTrack? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    /// <summary>The playback position; the layer redraws only when the set of active cues changes.</summary>
    public TimeSpan Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>Space kept free at the bottom (the control bar's height while it shows); auto-placed cues move up by it.</summary>
    public double BottomInset
    {
        get => GetValue(BottomInsetProperty);
        set => SetValue(BottomInsetProperty, value);
    }

    /// <summary>Text size multiplier (the captions menu offers 0.75, 1, 1.5, 2).</summary>
    public double CaptionScale
    {
        get => GetValue(CaptionScaleProperty);
        set => SetValue(CaptionScaleProperty, value);
    }

    /// <summary>Opacity of the black box behind the text, 0 to 1.</summary>
    public double BackgroundOpacity
    {
        get => GetValue(BackgroundOpacityProperty);
        set => SetValue(BackgroundOpacityProperty, value);
    }

    /// <summary>
    /// The cues shown now, in stacking order (start order: the first is the lowest automatic cue); for the player's
    /// screen-reader option and tests. Cues with blank text are left out. Empty for a chapters track; a descriptions track
    /// lists its cues here but draws nothing.
    /// </summary>
    public IReadOnlyList<VideoCue> ActiveCues { get; private set; } = [];

    /// <summary>Raised when <see cref="ActiveCues"/> changes.</summary>
    public event EventHandler? ActiveCuesChanged;

    /// <summary>The font size captions get at the current size and scale.</summary>
    internal double CaptionFontSize => FontSizeFor(Bounds.Height, CaptionScale);

    /// <summary>The boxes drawn now, in <see cref="ActiveCues"/> order (empty when nothing is drawn); for tests.</summary>
    internal IReadOnlyList<Rect> CueBoxes
    {
        get
        {
            EnsureLayout();
            return _boxes is null ? [] : Array.ConvertAll(_boxes, static b => b.Box);
        }
    }

    /// <summary>Caption font size for a picture height: 4.5% of it times the scale, between 12 and 40.</summary>
    internal static double FontSizeFor(double pictureHeight, double scale)
    {
        var size = pictureHeight * HeightRatio * (double.IsFinite(scale) && scale > 0 ? scale : 1);
        return Math.Clamp(double.IsFinite(size) ? size : MinFontSize, MinFontSize, MaxFontSize);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionProperty)
        {
            UpdateActiveCues();
        }
        else if (change.Property == TrackProperty)
        {
            if (VisualRoot is not null)
            {
                Subscribe(Track);
            }
            UpdateActiveCues();
            Rebuild();
        }
        else if (change.Property == CaptionScaleProperty || change.Property == CaptionFontFamilyProperty)
        {
            Rebuild();
        }
        else if (change.Property == BottomInsetProperty)
        {
            _placed = false;
            InvalidateVisual();
        }
        else if (change.Property == BackgroundOpacityProperty)
        {
            _boxBrush = null;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe(Track);
        UpdateActiveCues();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Subscribe(null);
        DisposeBoxes();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        EnsureLayout();
        if (_boxes is null || _boxes.Length == 0)
        {
            return;
        }
        _boxBrush ??= new ImmutableSolidColorBrush(Colors.Black, Math.Clamp(BackgroundOpacity, 0, 1));
        var radius = GetValue(CaptionCornerRadiusProperty);
        foreach (var box in _boxes)
        {
            context.DrawRectangle(_boxBrush, null, new RoundedRect(box.Box, radius));
            box.Layout.Draw(context, box.TextOrigin);
        }
    }

    private void Subscribe(VideoTextTrack? track)
    {
        if (ReferenceEquals(_subscribed, track))
        {
            return;
        }
        if (_subscribed is not null)
        {
            _subscribed.CuesChanged -= OnCuesChanged;
            _subscribed.PropertyChanged -= OnTrackPropertyChanged;
        }
        _subscribed = track;
        if (track is not null)
        {
            track.CuesChanged += OnCuesChanged;
            track.PropertyChanged += OnTrackPropertyChanged;
        }
    }

    private void OnCuesChanged(object? sender, EventArgs e) => UpdateActiveCues();

    private void OnTrackPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == VideoTextTrack.KindProperty)
        {
            UpdateActiveCues();
            Rebuild();
        }
    }

    /// <summary>Recomputes the active set; raises <see cref="ActiveCuesChanged"/> and redraws only when it changed.</summary>
    private void UpdateActiveCues()
    {
        var track = Track;
        IReadOnlyList<VideoCue> cues = track is null || track.Kind == VideoTextTrackKind.Chapters ? [] : track.Cues;
        if (!ReferenceEquals(cues, _indexed))
        {
            _indexed = cues;
            _longestCue = TimeSpan.Zero;
            foreach (var cue in cues)
            {
                var length = cue.End - cue.Start;
                _longestCue = length > _longestCue ? length : _longestCue;
            }
        }
        _scratch.Clear();
        var time = Position;
        // Cues are sorted by start: everything active starts at or before the time and no earlier than the longest cue allows.
        for (var i = VideoChapters.LastStartingAtOrBefore(cues, time); i >= 0 && cues[i].Start >= time - _longestCue; i--)
        {
            var cue = cues[i];
            if (cue.IsActiveAt(time) && !string.IsNullOrWhiteSpace(cue.Text))
            {
                _scratch.Add(cue);
            }
        }
        _scratch.Reverse();
        if (SameAsActive(_scratch))
        {
            return;
        }
        _active = _scratch.ToArray();
        ActiveCues = _active;
        Rebuild();
        ActiveCuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool SameAsActive(List<VideoCue> next)
    {
        if (next.Count != _active.Length)
        {
            return false;
        }
        for (var i = 0; i < next.Count; i++)
        {
            if (!ReferenceEquals(next[i], _active[i]))
            {
                return false;
            }
        }
        return true;
    }

    private void Rebuild()
    {
        DisposeBoxes();
        InvalidateVisual();
    }

    private void DisposeBoxes()
    {
        if (_boxes is not null)
        {
            foreach (var box in _boxes)
            {
                box.Layout.Dispose();
            }
            _boxes = null;
        }
    }

    /// <summary>Builds the text layouts (when stale) and places the boxes (when stale). Caches only; never invalidates.</summary>
    private void EnsureLayout()
    {
        var size = Bounds.Size;
        if (_boxes is not null && size != _builtSize)
        {
            DisposeBoxes();
        }
        if (_boxes is null)
        {
            _builtSize = size;
            _boxes = Build(size);
            _placed = false;
        }
        if (!_placed)
        {
            Place(_boxes, size);
            _placed = true;
        }
    }

    private CaptionBox[] Build(Size size)
    {
        var track = Track;
        if (_active.Length == 0 || track is null || track.Kind is not (VideoTextTrackKind.Captions or VideoTextTrackKind.Subtitles)
            || size.Width <= PaddingX * 2 || size.Height <= 0)
        {
            return [];
        }
        _fontSize = FontSizeFor(size.Height, CaptionScale);
        var family = GetValue(CaptionFontFamilyProperty) ?? FontFamily.Default;
        var boxes = new CaptionBox[_active.Length];
        for (var i = 0; i < _active.Length; i++)
        {
            boxes[i] = BuildBox(_active[i], family, size);
        }
        return boxes;
    }

    private CaptionBox BuildBox(VideoCue cue, FontFamily family, Size size)
    {
        var rtl = CaptionText.IsRightToLeft(cue.Text);
        var (regionLeft, regionWidth) = Region(cue, size.Width, rtl);
        var textWidth = Math.Max(regionWidth - PaddingX * 2, _fontSize);
        var alignment = cue.Align switch
        {
            VideoCueAlign.Start => TextAlignment.Start,
            VideoCueAlign.End => TextAlignment.End,
            VideoCueAlign.Left => TextAlignment.Left,
            VideoCueAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Center,
        };
        var typeface = new Typeface(family);
        var layout = new TextLayout(cue.Text, typeface, _fontSize, s_textBrush, alignment, TextWrapping.Wrap, TextTrimming.None, null,
            rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, textWidth, double.PositiveInfinity, double.NaN, 0, 0, null,
            StyleOverrides(cue, family));
        // The box hugs the laid-out lines (they are aligned inside the region's text width).
        var left = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        foreach (var line in layout.TextLines)
        {
            left = Math.Min(left, line.Start);
            right = Math.Max(right, line.Start + line.Width);
        }
        if (!double.IsFinite(left) || right <= left)
        {
            left = 0;
            right = 0;
        }
        var oneLine = layout.TextLines.Count > 0 ? layout.TextLines[0].Height : _fontSize;
        var boxWidth = right - left + PaddingX * 2;
        var boxHeight = layout.Height + PaddingY * 2;
        var boxX = regionLeft + left;
        boxX = Math.Clamp(boxX, 0, Math.Max(0, size.Width - boxWidth));
        return new CaptionBox(cue, layout, new Size(boxWidth, boxHeight), boxX, boxX + PaddingX - left, oneLine + PaddingY * 2);
    }

    /// <summary>
    /// The cue box's horizontal region (WebVTT computed position, position alignment and size). <c>start</c> and <c>end</c>
    /// resolve to left or right by the cue's base direction, for the automatic position too (a start-aligned cue without a
    /// position starts at the picture's start edge, as browsers draw it, rather than at 50%).
    /// </summary>
    internal static (double Left, double Width) Region(VideoCue cue, double width, bool rightToLeft = false)
    {
        var side = cue.Align switch
        {
            VideoCueAlign.Left => -1,
            VideoCueAlign.Right => 1,
            VideoCueAlign.Start => rightToLeft ? 1 : -1,
            VideoCueAlign.End => rightToLeft ? -1 : 1,
            _ => 0,
        };
        var position = cue.Position ?? side switch
        {
            < 0 => 0,
            > 0 => 100,
            _ => 50,
        };
        var maximum = side switch
        {
            < 0 => 100 - position,
            > 0 => position,
            _ => position <= 50 ? position * 2 : (100 - position) * 2,
        };
        var size = Math.Clamp(Math.Min(cue.Size, maximum), 0, 100);
        var left = side switch
        {
            < 0 => position,
            > 0 => position - size,
            _ => position - size / 2,
        };
        return (left / 100 * width, size / 100 * width);
    }

    private IReadOnlyList<ValueSpan<TextRunProperties>>? StyleOverrides(VideoCue cue, FontFamily family)
    {
        List<ValueSpan<TextRunProperties>>? spans = null;
        var at = 0;
        foreach (var run in cue.Runs)
        {
            if (run.IsBold || run.IsItalic || run.IsUnderline)
            {
                spans ??= [];
                var face = new Typeface(family, run.IsItalic ? FontStyle.Italic : FontStyle.Normal, run.IsBold ? FontWeight.Bold : FontWeight.Normal);
                spans.Add(new ValueSpan<TextRunProperties>(at, run.Text.Length,
                    new GenericTextRunProperties(face, _fontSize, run.IsUnderline ? TextDecorations.Underline : null, s_textBrush)));
            }
            at += run.Text.Length;
        }
        return spans;
    }

    /// <summary>Places the boxes vertically (see the class remarks); <see cref="BottomInset"/> changes only rerun this.</summary>
    private void Place(CaptionBox[] boxes, Size size)
    {
        if (boxes.Length == 0)
        {
            return;
        }
        var height = size.Height;
        var inset = Math.Clamp(double.IsFinite(BottomInset) ? BottomInset : 0, 0, height);
        var bottom = height - inset - Math.Round(_fontSize * BottomMarginRatio);
        var placed = new List<Rect>(boxes.Length);
        foreach (var box in boxes)
        {
            var cue = box.Cue;
            double y;
            bool upward;
            if (cue.Line is not { } line || !double.IsFinite(line))
            {
                y = bottom - box.Size.Height;
                upward = true;
            }
            else if (cue.LineIsPercent)
            {
                y = Math.Clamp(line / 100 * height, 0, Math.Max(0, height - box.Size.Height));
                upward = true;
            }
            else if (line >= 0)
            {
                y = Math.Round(line) * box.LineStep;
                upward = false;
            }
            else
            {
                y = bottom - box.Size.Height - (Math.Round(-line) - 1) * box.LineStep;
                upward = true;
            }
            y = Math.Clamp(y, 0, Math.Max(0, height - box.Size.Height));
            y = AvoidOverlap(placed, box, y, upward, height);
            box.Box = new Rect(box.X, y, box.Size.Width, box.Size.Height);
            box.TextOrigin = new Point(box.TextX, y + PaddingY);
            placed.Add(box.Box);
        }
    }

    /// <summary>Moves a box past the earlier boxes it overlaps; tries the other direction when it runs out of room, else stays.</summary>
    private static double AvoidOverlap(List<Rect> placed, CaptionBox box, double y, bool upward, double height)
    {
        if (TryMove(placed, box, y, upward, height, out var moved) || TryMove(placed, box, y, !upward, height, out moved))
        {
            return moved;
        }
        return y;
    }

    private static bool TryMove(List<Rect> placed, CaptionBox box, double y, bool upward, double height, out double result)
    {
        result = y;
        // Each step moves strictly past one placed box, so this ends after at most placed.Count steps.
        for (var step = 0; step <= placed.Count; step++)
        {
            var candidate = new Rect(box.X, result, box.Size.Width, box.Size.Height);
            Rect? hit = null;
            foreach (var other in placed)
            {
                if (other.Intersects(candidate))
                {
                    hit = other;
                    break;
                }
            }
            if (hit is not { } h)
            {
                return true;
            }
            result = upward ? h.Top - StackGap - box.Size.Height : h.Bottom + StackGap;
            if (result < 0 || result + box.Size.Height > height)
            {
                return false;
            }
        }
        return false;
    }

    private sealed class CaptionBox(VideoCue cue, TextLayout layout, Size size, double x, double textX, double lineStep)
    {
        public VideoCue Cue { get; } = cue;

        public TextLayout Layout { get; } = layout;

        public Size Size { get; } = size;

        public double X { get; } = x;

        public double TextX { get; } = textX;

        /// <summary>Height of a one-line box: the row size numbered lines snap to.</summary>
        public double LineStep { get; } = lineStep;

        public Rect Box { get; set; }

        public Point TextOrigin { get; set; }
    }
}
