using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a <see cref="SandText"/> was set off in earlier versions (see <see cref="SandText.Trigger"/>).</summary>
public enum SandTextTrigger
{
    /// <summary>The pointer does nothing; only code moves the sand.</summary>
    None,

    /// <summary>The pointer digs (formerly: a click toggled the text).</summary>
    Click,

    /// <summary>The pointer digs (formerly: hovering blew the text away).</summary>
    Hover
}

/// <summary>The way the wind blew the grains of a <see cref="SandText"/> in earlier versions; no longer used.</summary>
public enum SandTextDrift
{
    /// <summary>Grains blew to the right.</summary>
    Right,

    /// <summary>Grains blew to the left.</summary>
    Left,

    /// <summary>Grains rose.</summary>
    Up
}

/// <summary>
/// Large text drawn as grains of sand. The grains pour in from above and fill each letter from the bottom up, the
/// pointer digs through the letters and throws grains that tumble and pile up, and once nobody digs the letters fill
/// back in while the spilled sand disappears. A new <see cref="Text"/> collapses the old letters into dunes while the
/// new word pours in.
/// </summary>
/// <remarks>
/// The control is as wide as it is given and 1.55 em tall, leaving room under the letters for piles; the text is
/// centered and shrunk to 94 % of the width when it would not fit. Each grain is a <see cref="GrainSize"/> square of a
/// <see cref="Palette"/> color (the text color by default) lightened or darkened in five steps. Under reduced motion the
/// letters are always formed, a new text replaces the old one at once and the pointer does not dig. The text is exposed
/// to assistive technology as a heading; the grains are decoration.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(DissolvedPseudoClass)]
public class SandText : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string DissolvedPseudoClass = ":dissolved";

    /// <summary>The box is this many em tall (room for piles under the letters).</summary>
    internal const double BoxHeightEm = 1.55;

    /// <summary>The alphabetic baseline sits this many em above the bottom of the sand.</summary>
    internal const double BaselineFromBottomEm = 0.515;

    /// <summary>Text wider than this share of the width is shrunk to it.</summary>
    internal const double FitShare = 0.94;

    /// <summary>Letter spacing of the sand letters, in em.</summary>
    internal const double TrackingEm = -0.03;

    /// <summary>A word with descenders sits higher by this share of its deepest descender.</summary>
    internal const double DescentLift = 1.0 / 3;

    /// <summary>A cell joins a letter when this many of its 16 sample points are inside a glyph (a quarter).</summary>
    internal const int CoverageSamples = 4;

    private const double FrameSeconds = 1.0 / 60;
    private const int MaxStepsPerFrame = 4;
    private const double PointerVelocityWindowMs = 100;

    /// <summary>The text the letters form. Keep it to one short line; changing it drops the old letters and pours the new.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SandText, string?>(nameof(Text));

    /// <summary>
    /// Whether the letters are let go (true): every grain turns loose, falls and disappears. Setting it back to false
    /// pours the letters in again. Two-way.
    /// </summary>
    public static readonly StyledProperty<bool> IsDissolvedProperty =
        AvaloniaProperty.Register<SandText, bool>(nameof(IsDissolved), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The side of one grain in px (2 by default).</summary>
    public static readonly StyledProperty<double> GrainSizeProperty =
        AvaloniaProperty.Register<SandText, double>(nameof(GrainSize), 2);

    /// <summary>How far around the pointer the sand is dug away, in px (14 by default).</summary>
    public static readonly StyledProperty<double> DigRadiusProperty =
        AvaloniaProperty.Register<SandText, double>(nameof(DigRadius), 14);

    /// <summary>Whether the pointer (mouse, pen or a dragging finger) digs through the letters (true by default).</summary>
    public static readonly StyledProperty<bool> IsDigEnabledProperty =
        AvaloniaProperty.Register<SandText, bool>(nameof(IsDigEnabled), true);

    /// <summary>
    /// The grain colors: each grain picks one and lightens or darkens it a little. Null or empty uses the text color
    /// (<see cref="TemplatedControl.Foreground"/>, the accent by default). At most 51 colors are used.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<Color>?> PaletteProperty =
        AvaloniaProperty.Register<SandText, IReadOnlyList<Color>?>(nameof(Palette));

    /// <summary>
    /// The heading level assistive technology reports for the text (2 by default, like an h2); 0 reports plain text.
    /// </summary>
    public static readonly StyledProperty<int> HeadingLevelProperty =
        AvaloniaProperty.Register<SandText, int>(nameof(HeadingLevel), 2);

    /// <summary>
    /// The letters' size as a share of the control's width (for example 0.18); 0 (the default) uses
    /// <see cref="TemplatedControl.FontSize"/>.
    /// </summary>
    public static readonly StyledProperty<double> FontSizeRatioProperty =
        AvaloniaProperty.Register<SandText, double>(nameof(FontSizeRatio));

    /// <summary>No longer used: the pointer digs instead. <see cref="SandTextTrigger.None"/> turns digging off.</summary>
    [Obsolete("Use IsDigEnabled instead (deprecated in 1.2.0).")]
    public static readonly StyledProperty<SandTextTrigger> TriggerProperty =
        AvaloniaProperty.Register<SandText, SandTextTrigger>(nameof(Trigger), SandTextTrigger.Click);

    /// <summary>No longer used: grains fall under gravity instead of drifting on a wind.</summary>
    [Obsolete("Sand now falls under gravity; this setting has no effect (deprecated in 1.2.0).")]
    public static readonly StyledProperty<SandTextDrift> DriftProperty =
        AvaloniaProperty.Register<SandText, SandTextDrift>(nameof(Drift));

    /// <summary>No longer used: the pour and the fall follow fixed physics.</summary>
    [Obsolete("The sand follows fixed physics; this setting has no effect (deprecated in 1.2.0).")]
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<SandText, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(1.6));

    /// <summary>No longer used: thrown grains fly with the pointer's velocity.</summary>
    [Obsolete("Use DigRadius instead; how far grains fly follows the pointer (deprecated in 1.2.0).")]
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<SandText, double>(nameof(Distance), 110);

    /// <summary>No longer used: there is one grain per grid cell of <see cref="GrainSize"/>.</summary>
    [Obsolete("Use GrainSize instead; there is one grain per grid cell (deprecated in 1.2.0).")]
    public static readonly StyledProperty<int> MaxGrainsProperty =
        AvaloniaProperty.Register<SandText, int>(nameof(MaxGrains), 4000);

    /// <summary>Raised when the letters have been let go (<see cref="IsDissolved"/>) and the last grain has gone.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DissolvedEvent =
        RoutedEvent.Register<SandText, RoutedEventArgs>(nameof(Dissolved), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the last grain of a pour is back in its letter: after the first show, a new text,
    /// <see cref="Gather"/>, or healing after digging.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> GatheredEvent =
        RoutedEvent.Register<SandText, RoutedEventArgs>(nameof(Gathered), RoutingStrategies.Bubble);

    private readonly SandTextField _field = new();
    private readonly LiveLoop _loop;
    private readonly uint[] _colors = new uint[256];
    private readonly (byte R, byte G, byte B)[] _paletteRgb = new (byte, byte, byte)[SandTextField.MaxPalette];
    private int[] _pixels = [];
    private bool[] _mask = [];
    private readonly List<(Geometry Outline, double X, double Y)> _glyphs = [];
    private WriteableBitmap? _front;
    private WriteableBitmap? _back;
    private bool _bitmapFailed;
    private double _grain = 2;
    private Size _gridSize;
    private Point _gridOrigin;
    private bool _gridBuilt;
    private bool _textDirty;
    private double _accumulator;
    private Point _lastPointer;
    private ulong _lastPointerTime;
    private bool _hasPointer;

    static SandText()
    {
        AffectsMeasure<SandText>(TextProperty, FontSizeProperty, FontFamilyProperty, FontWeightProperty, FontStyleProperty,
            FontSizeRatioProperty);
        ClipToBoundsProperty.OverrideDefaultValue<SandText>(false);
    }

    public SandText()
    {
        // The sand is a simulation stepped every frame while anything moves; no theme transition can express it.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Full };
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        BuildColors();
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="IsDissolvedProperty"/>
    public bool IsDissolved
    {
        get => GetValue(IsDissolvedProperty);
        set => SetValue(IsDissolvedProperty, value);
    }

    /// <inheritdoc cref="GrainSizeProperty"/>
    public double GrainSize
    {
        get => GetValue(GrainSizeProperty);
        set => SetValue(GrainSizeProperty, value);
    }

    /// <inheritdoc cref="DigRadiusProperty"/>
    public double DigRadius
    {
        get => GetValue(DigRadiusProperty);
        set => SetValue(DigRadiusProperty, value);
    }

    /// <inheritdoc cref="IsDigEnabledProperty"/>
    public bool IsDigEnabled
    {
        get => GetValue(IsDigEnabledProperty);
        set => SetValue(IsDigEnabledProperty, value);
    }

    /// <inheritdoc cref="PaletteProperty"/>
    public IReadOnlyList<Color>? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <inheritdoc cref="HeadingLevelProperty"/>
    public int HeadingLevel
    {
        get => GetValue(HeadingLevelProperty);
        set => SetValue(HeadingLevelProperty, value);
    }

    /// <inheritdoc cref="FontSizeRatioProperty"/>
    public double FontSizeRatio
    {
        get => GetValue(FontSizeRatioProperty);
        set => SetValue(FontSizeRatioProperty, value);
    }

#pragma warning disable CS0618
    /// <inheritdoc cref="TriggerProperty"/>
    [Obsolete("Use IsDigEnabled instead (deprecated in 1.2.0).")]
    public SandTextTrigger Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <inheritdoc cref="DriftProperty"/>
    [Obsolete("Sand now falls under gravity; this setting has no effect (deprecated in 1.2.0).")]
    public SandTextDrift Drift
    {
        get => GetValue(DriftProperty);
        set => SetValue(DriftProperty, value);
    }

    /// <inheritdoc cref="DurationProperty"/>
    [Obsolete("The sand follows fixed physics; this setting has no effect (deprecated in 1.2.0).")]
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <inheritdoc cref="DistanceProperty"/>
    [Obsolete("Use DigRadius instead; how far grains fly follows the pointer (deprecated in 1.2.0).")]
    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    /// <inheritdoc cref="MaxGrainsProperty"/>
    [Obsolete("Use GrainSize instead; there is one grain per grid cell (deprecated in 1.2.0).")]
    public int MaxGrains
    {
        get => GetValue(MaxGrainsProperty);
        set => SetValue(MaxGrainsProperty, value);
    }
#pragma warning restore CS0618

    /// <inheritdoc cref="DissolvedEvent"/>
    public event EventHandler<RoutedEventArgs>? Dissolved
    {
        add => AddHandler(DissolvedEvent, value);
        remove => RemoveHandler(DissolvedEvent, value);
    }

    /// <inheritdoc cref="GatheredEvent"/>
    public event EventHandler<RoutedEventArgs>? Gathered
    {
        add => AddHandler(GatheredEvent, value);
        remove => RemoveHandler(GatheredEvent, value);
    }

    /// <summary>How much of the text is missing: 0 when every letter is whole, 1 when none of it stands.</summary>
    public double Progress => !_gridBuilt
        ? (IsDissolved ? 1 : 0)
        : _field.MaskCount == 0 ? (IsDissolved ? 1 : 0) : 1 - _field.StuckCount / (double)_field.MaskCount;

    internal SandTextField Field => _field;

    /// <summary>Where the grid's top-left corner sits in the control, and the size of one grain.</summary>
    internal Point GridOrigin => _gridOrigin;

    internal double Grain => _grain;

    /// <summary>Lets the letters go: every grain falls and disappears (sets <see cref="IsDissolved"/>).</summary>
    public void Dissolve() => SetCurrentValue(IsDissolvedProperty, true);

    /// <summary>Pours the letters back in (clears <see cref="IsDissolved"/>).</summary>
    public void Gather() => SetCurrentValue(IsDissolvedProperty, false);

    /// <summary>Lets the letters go when they stand, pours them back when they are gone (or going).</summary>
    public void Toggle() => SetCurrentValue(IsDissolvedProperty, !IsDissolved);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateAutomation();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_gridBuilt && _field.IsActive)
        {
            _loop.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TextProperty)
        {
            _textDirty = true;
            UpdateAutomation();
            InvalidateArrange();
        }
        else if (property == FontSizeProperty || property == FontFamilyProperty || property == FontWeightProperty
                 || property == FontStyleProperty || property == FontSizeRatioProperty)
        {
            _textDirty = true;
            InvalidateArrange();
        }
        else if (property == GrainSizeProperty)
        {
            _gridBuilt = false;
            InvalidateArrange();
        }
        else if (property == IsDissolvedProperty)
        {
            PseudoClasses.Set(DissolvedPseudoClass, IsDissolved);
            if (_gridBuilt)
            {
                var animate = CanAnimate();
                _field.SetDissolved(IsDissolved, animate);
                AfterChange(animate);
            }
        }
        else if (property == ForegroundProperty || property == PaletteProperty)
        {
            BuildColors();
            Publish();
        }
        else if (property == HeadingLevelProperty)
        {
            UpdateAutomation();
        }
#pragma warning disable CS0618
        else if (property == TriggerProperty)
        {
            SetCurrentValue(IsDigEnabledProperty, Trigger != SandTextTrigger.None);
        }
#pragma warning restore CS0618
        else if (property == ArcMotion.LevelProperty)
        {
            if (_gridBuilt && !ArcMotion.IsFull(this) && _field.IsActive)
            {
                _field.Settle();
                Publish();
                RaiseEvents();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        var width = availableSize.Width;
        if (double.IsInfinity(width))
        {
            // No width to fill: as wide as the text needs at FontSize.
            var natural = NaturalWidth(FontSize);
            return new Size(natural / FitShare, BoxHeightEm * FontSize);
        }
        return new Size(width, BoxHeightEm * EmFor(width));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateGrid(finalSize);
        return size;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        if (e.Pointer.Type == PointerType.Touch && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        Dig(point.Position, e.Timestamp);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hasPointer = false;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _hasPointer = false;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SandTextAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        // The whole box takes the pointer, not only the grains, so digging starts anywhere over it.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_front is not { } picture)
        {
            return;
        }
        var pixels = picture.PixelSize;
        context.DrawImage(picture, new Rect(0, 0, pixels.Width, pixels.Height),
            new Rect(_gridOrigin.X, _gridOrigin.Y, pixels.Width * _grain, pixels.Height * _grain));
    }

    /// <summary>Digs at a point in control coordinates (pointer input and tests).</summary>
    internal void Dig(Point position, ulong timestamp)
    {
        if (!IsDigEnabled || !_gridBuilt || !ArcMotion.IsFull(this) || IsDissolved)
        {
            return;
        }
        double vx = 0, vy = 0;
        if (_hasPointer && timestamp > _lastPointerTime && timestamp - _lastPointerTime <= PointerVelocityWindowMs)
        {
            var frames = (timestamp - _lastPointerTime) / (1000.0 * FrameSeconds);
            vx = (position.X - _lastPointer.X) / _grain / frames;
            vy = (position.Y - _lastPointer.Y) / _grain / frames;
        }
        _lastPointer = position;
        _lastPointerTime = timestamp;
        _hasPointer = true;
        var x = (position.X - _gridOrigin.X) / _grain;
        var y = (position.Y - _gridOrigin.Y) / _grain;
        _field.Dig(x, y, DigRadius / _grain, vx, vy);
        if (_field.IsActive)
        {
            _loop.Start();
        }
    }

    internal LiveLoop Loop => _loop;

    /// <summary>Steps the field at 60 Hz (several steps when frames come late), then shows the result.</summary>
    private bool Step(double dt)
    {
        if (!_gridBuilt)
        {
            return false;
        }
        _accumulator = Math.Min(_accumulator + dt, FrameSeconds * MaxStepsPerFrame);
        var stepped = false;
        while (_accumulator >= FrameSeconds * 0.5)
        {
            _accumulator -= FrameSeconds;
            _field.Step();
            stepped = true;
        }
        if (stepped)
        {
            Publish();
            RaiseEvents();
        }
        return _field.IsActive;
    }

    private void RaiseEvents()
    {
        if (_field.TakeGathered())
        {
            RaiseEvent(new RoutedEventArgs(GatheredEvent, this));
        }
        if (_field.TakeDissolved())
        {
            RaiseEvent(new RoutedEventArgs(DissolvedEvent, this));
        }
    }

    private bool CanAnimate() => ArcMotion.IsFull(this) && VisualRoot is not null;

    private void AfterChange(bool animate)
    {
        Publish();
        if (animate)
        {
            if (_field.IsActive)
            {
                _loop.Start();
            }
        }
        else
        {
            RaiseEvents();
        }
    }

    /// <summary>Builds the grid for a new size (the letters appear formed), or takes a new text into the live grid.</summary>
    private void UpdateGrid(Size size)
    {
        var grain = Math.Max(1, GrainSize);
        var columns = (int)Math.Floor(size.Width / grain);
        // The sand is 1.55 em tall (layout rounding may make the box a fraction taller).
        var rows = (int)Math.Floor(Math.Min(size.Height, BoxHeightEm * EmFor(size.Width) + 0.01) / grain);
        var gridSize = new Size(columns * grain, rows * grain);
        var resized = !_gridBuilt || grain != _grain || gridSize != _gridSize;
        if (!resized && !_textDirty)
        {
            return;
        }
        var first = !_gridBuilt && _field.Columns == 0;
        _grain = grain;
        _gridSize = gridSize;
        _gridOrigin = new Point(Math.Round((size.Width - gridSize.Width) / 2 / grain * 2) / 2 * grain,
            Math.Floor((size.Height - gridSize.Height) / 2));
        _textDirty = false;
        if (resized)
        {
            _field.Resize(columns, rows);
            if (_pixels.Length < columns * rows)
            {
                _pixels = new int[columns * rows];
                _mask = new bool[columns * rows];
            }
            DisposeBitmaps();
        }
        BuildMask(columns, rows, size.Width);
        var mask = _mask.AsSpan(0, columns * rows);
        var animate = CanAnimate();
        if (resized)
        {
            _gridBuilt = true;
            _field.SetDissolved(IsDissolved, animate: false);
            // A first state shows as it is, without events.
            _field.TakeDissolved();
            // Only the very first show pours; a resize shows the letters formed at once.
            if (first && animate && !IsDissolved)
            {
                _field.SetPoured(mask, SandTextField.IntroWindow);
            }
            else
            {
                _field.SetFormed(mask);
                _field.TakeGathered();
            }
            Publish();
            if (_field.IsActive)
            {
                _loop.Start();
            }
            return;
        }
        if (animate)
        {
            _field.Retarget(mask, SandTextField.ChangeWindow);
        }
        else
        {
            _field.SetFormed(mask);
        }
        AfterChange(animate);
    }

    /// <summary>The em size the letters get at a given control width (before fitting).</summary>
    private double EmFor(double width)
    {
        var ratio = FontSizeRatio;
        return ratio > 0 && width > 0 && !double.IsInfinity(width) ? width * ratio : FontSize;
    }

    private Typeface Typeface => new(FontFamily, FontStyle, FontWeight);

    private TextLayout? Layout(double em)
    {
        var text = Text;
        if (string.IsNullOrEmpty(text) || em <= 0)
        {
            return null;
        }
        return new TextLayout(text, Typeface, em, Brushes.Black, letterSpacing: TrackingEm * em, maxLines: 1);
    }

    private double NaturalWidth(double em)
    {
        using var layout = Layout(em);
        return layout?.Width ?? 0;
    }

    /// <summary>
    /// Fills the letter mask: the text centered on its advance width, shrunk to 94 % of the width when too wide, its
    /// baseline 0.515 em above the bottom of the sand (raised by a third of the deepest descender), and each cell taken
    /// when at least a quarter of it lies inside a glyph outline.
    /// </summary>
    private void BuildMask(int columns, int rows, double width)
    {
        var cells = columns * rows;
        Array.Clear(_mask, 0, cells);
        var em = EmFor(width);
        var layout = Layout(em);
        if (layout is null || columns == 0 || rows == 0)
        {
            layout?.Dispose();
            return;
        }
        var gridWidth = columns * _grain;
        var gridHeight = rows * _grain;
        if (layout.Width > gridWidth * FitShare && layout.Width > 0)
        {
            em *= gridWidth * FitShare / layout.Width;
            layout.Dispose();
            layout = Layout(em)!;
        }
        using (layout)
        {
            // One outline per glyph (baseline at y 0), so each sample tests one small path.
            _glyphs.Clear();
            var left = (gridWidth - layout.Width) / 2;
            var descent = 0.0;
            foreach (var line in layout.TextLines)
            {
                var x = left + line.Start;
                foreach (var run in line.TextRuns)
                {
                    if (run is ShapedTextRun shaped)
                    {
                        var glyphs = shaped.GlyphRun;
                        var pen = x;
                        foreach (var info in glyphs.GlyphInfos)
                        {
                            var single = new GlyphRun(glyphs.GlyphTypeface, glyphs.FontRenderingEmSize,
                                glyphs.Characters, [new GlyphInfo(info.GlyphIndex, info.GlyphCluster, info.GlyphAdvance)],
                                new Point(0, 0));
                            var outline = single.BuildGeometry();
                            if (outline is not null && outline.Bounds.Width > 0 && outline.Bounds.Height > 0)
                            {
                                _glyphs.Add((outline, pen + info.GlyphOffset.X, info.GlyphOffset.Y));
                                descent = Math.Max(descent, outline.Bounds.Bottom + info.GlyphOffset.Y);
                            }
                            pen += info.GlyphAdvance;
                        }
                    }
                    x += run is DrawableTextRun drawable ? drawable.Size.Width : 0;
                }
            }
            var baseline = gridHeight - BaselineFromBottomEm * em - descent * DescentLift;
            if (_glyphs.Count == 0)
            {
                // No glyph outlines on this platform: fill the text's box from cap height to baseline.
                FillBox(left, baseline - 0.7 * em, left + layout.Width, baseline, columns, rows);
                return;
            }
            foreach (var (outline, gx, gy) in _glyphs)
            {
                Sample(outline, gx, baseline + gy, columns, rows);
            }
            _glyphs.Clear();
        }
    }

    /// <summary>
    /// Marks the cells a glyph covers by at least <see cref="CoverageSamples"/> of 16 sample points. A cell whose center
    /// and four corners agree is taken as wholly inside or outside without the finer samples.
    /// </summary>
    private void Sample(Geometry outline, double originX, double baseline, int columns, int rows)
    {
        var bounds = outline.Bounds;
        var grain = _grain;
        var c0 = Math.Max(0, (int)Math.Floor((originX + bounds.Left) / grain));
        var c1 = Math.Min(columns - 1, (int)Math.Ceiling((originX + bounds.Right) / grain));
        var r0 = Math.Max(0, (int)Math.Floor((baseline + bounds.Top) / grain));
        var r1 = Math.Min(rows - 1, (int)Math.Ceiling((baseline + bounds.Bottom) / grain));
        for (var row = r0; row <= r1; row++)
        {
            var top = row * grain - baseline;
            for (var column = c0; column <= c1; column++)
            {
                var i = row * columns + column;
                if (_mask[i])
                {
                    continue;
                }
                var leftEdge = column * grain - originX;
                var center = outline.FillContains(new Point(leftEdge + grain / 2, top + grain / 2));
                var corners = 0;
                corners += outline.FillContains(new Point(leftEdge, top)) ? 1 : 0;
                corners += outline.FillContains(new Point(leftEdge + grain, top)) ? 1 : 0;
                corners += outline.FillContains(new Point(leftEdge, top + grain)) ? 1 : 0;
                corners += outline.FillContains(new Point(leftEdge + grain, top + grain)) ? 1 : 0;
                if (corners == 4 && center)
                {
                    _mask[i] = true;
                    continue;
                }
                if (corners == 0 && !center)
                {
                    continue;
                }
                var inside = 0;
                for (var sy = 0; sy < 4 && inside < CoverageSamples; sy++)
                {
                    for (var sx = 0; sx < 4; sx++)
                    {
                        if (outline.FillContains(new Point(leftEdge + (sx + 0.5) / 4 * grain, top + (sy + 0.5) / 4 * grain)))
                        {
                            inside++;
                        }
                    }
                }
                _mask[i] = inside >= CoverageSamples;
            }
        }
    }

    private void FillBox(double left, double top, double right, double bottom, int columns, int rows)
    {
        var c0 = Math.Max(0, (int)Math.Floor(left / _grain));
        var c1 = Math.Min(columns - 1, (int)Math.Ceiling(right / _grain) - 1);
        var r0 = Math.Max(0, (int)Math.Floor(top / _grain));
        var r1 = Math.Min(rows - 1, (int)Math.Ceiling(bottom / _grain) - 1);
        for (var row = r0; row <= r1; row++)
        {
            for (var column = c0; column <= c1; column++)
            {
                _mask[row * columns + column] = true;
            }
        }
    }

    private void BuildColors()
    {
        var palette = Palette;
        var count = 0;
        if (palette is { Count: > 0 })
        {
            count = Math.Min(palette.Count, SandTextField.MaxPalette);
            for (var i = 0; i < count; i++)
            {
                var c = palette[i];
                _paletteRgb[i] = (c.R, c.G, c.B);
            }
        }
        else
        {
            var c = Foreground is ISolidColorBrush solid ? solid.Color : Colors.Gray;
            _paletteRgb[0] = (c.R, c.G, c.B);
            count = 1;
        }
        _field.PaletteCount = count;
        SandTextField.BuildColors(_paletteRgb.AsSpan(0, count), _colors);
    }

    /// <summary>Paints the grid into the back bitmap and swaps it to the front.</summary>
    private void Publish()
    {
        if (!_gridBuilt || _field.Columns == 0 || _field.Rows == 0)
        {
            InvalidateVisual();
            return;
        }
        var columns = _field.Columns;
        var rows = _field.Rows;
        _field.Paint(MemoryMarshal.Cast<int, uint>(_pixels.AsSpan(0, columns * rows)), _colors);
        if (_bitmapFailed)
        {
            return;
        }
        try
        {
            _back ??= new WriteableBitmap(new PixelSize(columns, rows), new Vector(96, 96), PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            using (var locked = _back.Lock())
            {
                if (locked.RowBytes == columns * 4)
                {
                    Marshal.Copy(_pixels, 0, locked.Address, columns * rows);
                }
                else
                {
                    for (var y = 0; y < rows; y++)
                    {
                        Marshal.Copy(_pixels, y * columns, locked.Address + y * locked.RowBytes, columns);
                    }
                }
            }
        }
        catch (Exception)
        {
            // No pixel access on this platform: the sand is not drawn (the text stays readable to assistive technology).
            _bitmapFailed = true;
            DisposeBitmaps();
            return;
        }
        (_front, _back) = (_back, _front);
        InvalidateVisual();
    }

    private void DisposeBitmaps()
    {
        _front?.Dispose();
        _back?.Dispose();
        _front = null;
        _back = null;
    }

    private void UpdateAutomation()
    {
        SetCurrentValue(AutomationProperties.HeadingLevelProperty, Math.Clamp(HeadingLevel, 0, 9));
    }

    private sealed class SandTextAutomationPeer(SandText owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
