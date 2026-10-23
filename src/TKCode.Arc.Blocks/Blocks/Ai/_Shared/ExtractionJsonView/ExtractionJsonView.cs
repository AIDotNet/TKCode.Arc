using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The code view of the JSON and Schema tabs of the document extraction (spec 3.5): the typed output (<see cref="Output"/>)
/// or a schema (<see cref="SchemaText"/>) as indented JSON, monospace 12 / 20.4 with keys in Foreground, strings 78%
/// Success, numbers and <c>true</c> 80% Series1 (both mixed with Foreground), <c>null</c> and <c>false</c> TextMuted and
/// punctuation TextSecondary, in a 1 px Border box (radius 14, SurfaceMuted, padding 10 0, lines padded 0 14) that scrolls
/// sideways when a line is too long. In output mode every line that holds a value maps to its path
/// (<c>vendor</c>, <c>lineItems.0.amount</c>; a table's array line to the table key): pointing at it raises
/// <see cref="PathEntered"/> / <see cref="PathExited"/> and pressing it <see cref="PathPressed"/>; the owner links it by
/// setting <see cref="LinkedPath"/> (the line is tinted 13% in the group color of <see cref="Tones"/>, 160 ms), and
/// <see cref="Flash"/> marks a changed line (Success 20% fading to rest over 1400 ms). A schema has no paths.
/// </summary>
/// <remarks>
/// <para>
/// The lines are drawn by <see cref="ExtractionJsonLines"/> from the text <see cref="ExtractionOutput.ToJson"/> writes
/// (<see cref="Text"/> is exactly what Copy should copy). A new output writes the text at once; it is cut into lines when
/// they are next needed (a measure, a lookup, the pointer) and layouts are made only for lines whose text changed, so a
/// hidden tab costs a string per change and an edit relays out one line.
/// </para>
/// <para>
/// Motion is code-driven because the lines are drawn, not elements a ControlTheme could transition: the link tint fades in
/// 160 ms (standard) and the previous line out in the same time; the flash is a 1400 ms standard tween. Reduced: the tint
/// fades in 120 ms (linear) and there is no flash; Off: the tint jumps and there is no flash. A flash asked for while the
/// view is hidden plays when it is next laid out. The view is a Tab stop (an inset focus ring, 2 px inside the edge) and
/// Left / Right scroll it sideways.
/// </para>
/// </remarks>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(LinesPart, typeof(ExtractionJsonLines))]
internal sealed class ExtractionJsonView : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string LinesPart = "PART_Lines";

    /// <summary>Opacity of the group color under a linked line.</summary>
    internal const double LinkAlpha = 0.13;

    /// <summary>Opacity of Success when a changed line starts to flash.</summary>
    internal const double FlashAlpha = 0.2;

    /// <summary>How long a changed line flashes.</summary>
    internal static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(1400);

    /// <summary>How far Left and Right scroll.</summary>
    internal const double KeyScrollStep = 40;

    #region Properties

    /// <summary>The typed output to show; when set it is shown instead of <see cref="SchemaText"/>.</summary>
    public static readonly StyledProperty<ExtractionOutput?> OutputProperty =
        AvaloniaProperty.Register<ExtractionJsonView, ExtractionOutput?>(nameof(Output));

    /// <summary>JSON text shown as it is when there is no <see cref="Output"/> (the schema); its lines have no paths.</summary>
    public static readonly StyledProperty<string?> SchemaTextProperty =
        AvaloniaProperty.Register<ExtractionJsonView, string?>(nameof(SchemaText));

    /// <summary>The series tone (1 to 4) of a path, for the linked line's tint; tone 1 when null.</summary>
    public static readonly StyledProperty<Func<string, int>?> TonesProperty =
        AvaloniaProperty.Register<ExtractionJsonView, Func<string, int>?>(nameof(Tones));

    /// <summary>The path of the linked line (hovered or selected), or null.</summary>
    public static readonly StyledProperty<string?> LinkedPathProperty =
        AvaloniaProperty.Register<ExtractionJsonView, string?>(nameof(LinkedPath));

    /// <summary>The path of the line that changed last; setting a new one flashes it (<see cref="Flash"/> flashes it again).</summary>
    public static readonly StyledProperty<string?> ChangedPathProperty =
        AvaloniaProperty.Register<ExtractionJsonView, string?>(nameof(ChangedPath));

    /// <summary>The accessible name of the block ("Typed output as JSON").</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<ExtractionJsonView, string?>(nameof(AutomationName));

    /// <summary>Line height (20.4).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<ExtractionJsonView>();

    /// <summary>Punctuation (the theme gives TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> PunctuationForegroundProperty =
        AvaloniaProperty.Register<ExtractionJsonView, IBrush?>(nameof(PunctuationForeground));

    /// <summary><c>null</c> and <c>false</c> (the theme gives TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<ExtractionJsonView, IBrush?>(nameof(MutedForeground));

    /// <summary>The color strings are mixed from, 78% with 22% <c>Foreground</c> (the theme gives Success).</summary>
    public static readonly StyledProperty<IBrush?> StringBaseProperty =
        AvaloniaProperty.Register<ExtractionJsonView, IBrush?>(nameof(StringBase));

    /// <summary>The color numbers are mixed from, 80% with 20% <c>Foreground</c> (the theme gives Series1).</summary>
    public static readonly StyledProperty<IBrush?> NumberBaseProperty =
        AvaloniaProperty.Register<ExtractionJsonView, IBrush?>(nameof(NumberBase));

    /// <summary>The flash color of a changed line, used at 20% (the theme gives Success).</summary>
    public static readonly StyledProperty<IBrush?> FlashBrushProperty =
        AvaloniaProperty.Register<ExtractionJsonView, IBrush?>(nameof(FlashBrush));

    /// <summary>The JSON shown (what Copy copies).</summary>
    public static readonly DirectProperty<ExtractionJsonView, string> TextProperty =
        AvaloniaProperty.RegisterDirect<ExtractionJsonView, string>(nameof(Text), o => o.Text);

    /// <inheritdoc cref="OutputProperty"/>
    public ExtractionOutput? Output { get => GetValue(OutputProperty); set => SetValue(OutputProperty, value); }

    /// <inheritdoc cref="SchemaTextProperty"/>
    public string? SchemaText { get => GetValue(SchemaTextProperty); set => SetValue(SchemaTextProperty, value); }

    /// <inheritdoc cref="TonesProperty"/>
    public Func<string, int>? Tones { get => GetValue(TonesProperty); set => SetValue(TonesProperty, value); }

    /// <inheritdoc cref="LinkedPathProperty"/>
    public string? LinkedPath { get => GetValue(LinkedPathProperty); set => SetValue(LinkedPathProperty, value); }

    /// <inheritdoc cref="ChangedPathProperty"/>
    public string? ChangedPath { get => GetValue(ChangedPathProperty); set => SetValue(ChangedPathProperty, value); }

    /// <inheritdoc cref="AutomationNameProperty"/>
    public string? AutomationName { get => GetValue(AutomationNameProperty); set => SetValue(AutomationNameProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="PunctuationForegroundProperty"/>
    public IBrush? PunctuationForeground { get => GetValue(PunctuationForegroundProperty); set => SetValue(PunctuationForegroundProperty, value); }

    /// <inheritdoc cref="MutedForegroundProperty"/>
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    /// <inheritdoc cref="StringBaseProperty"/>
    public IBrush? StringBase { get => GetValue(StringBaseProperty); set => SetValue(StringBaseProperty, value); }

    /// <inheritdoc cref="NumberBaseProperty"/>
    public IBrush? NumberBase { get => GetValue(NumberBaseProperty); set => SetValue(NumberBaseProperty, value); }

    /// <inheritdoc cref="FlashBrushProperty"/>
    public IBrush? FlashBrush { get => GetValue(FlashBrushProperty); set => SetValue(FlashBrushProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => _text;
        private set => SetAndRaise(TextProperty, ref _text, value);
    }

    #endregion

    #region Events

    /// <summary>Raised when the pointer moves onto a line that has a path.</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathEnteredEvent =
        RoutedEvent.Register<ExtractionJsonView, ExtractionPathEventArgs>(nameof(PathEntered), RoutingStrategies.Bubble);

    /// <summary>Raised when the pointer leaves a line that has a path (onto another line, a line without one, or out).</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathExitedEvent =
        RoutedEvent.Register<ExtractionJsonView, ExtractionPathEventArgs>(nameof(PathExited), RoutingStrategies.Bubble);

    /// <summary>Raised when a line that has a path is pressed.</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathPressedEvent =
        RoutedEvent.Register<ExtractionJsonView, ExtractionPathEventArgs>(nameof(PathPressed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PathEnteredEvent"/>
    public event EventHandler<ExtractionPathEventArgs>? PathEntered { add => AddHandler(PathEnteredEvent, value); remove => RemoveHandler(PathEnteredEvent, value); }

    /// <inheritdoc cref="PathExitedEvent"/>
    public event EventHandler<ExtractionPathEventArgs>? PathExited { add => AddHandler(PathExitedEvent, value); remove => RemoveHandler(PathExitedEvent, value); }

    /// <inheritdoc cref="PathPressedEvent"/>
    public event EventHandler<ExtractionPathEventArgs>? PathPressed { add => AddHandler(PathPressedEvent, value); remove => RemoveHandler(PathPressedEvent, value); }

    #endregion

    private readonly Dictionary<string, int> _indexOf = new(StringComparer.Ordinal);
    private static readonly LinearEasing Linear = new();
    private readonly LineTint _link;
    private readonly LineTint _unlink;
    private readonly LineTint _flash;
    private ExtractionJsonLine[] _lines = [];
    private bool _linesStale;
    private string _text = string.Empty;
    private ScrollViewer? _scroll;
    private ExtractionJsonLines? _linesPart;
    private Point? _pointer;
    private string? _hoveredPath;
    private bool _flashPending;

    static ExtractionJsonView()
    {
        FocusableProperty.OverrideDefaultValue<ExtractionJsonView>(true);
    }

    /// <summary>Creates the view.</summary>
    public ExtractionJsonView()
    {
        _link = new LineTint(this, LinkAlpha);
        _unlink = new LineTint(this, LinkAlpha);
        _flash = new LineTint(this, FlashAlpha);
    }

    /// <summary>The lines (text, colored pieces and paths).</summary>
    internal IReadOnlyList<ExtractionJsonLine> Lines
    {
        get
        {
            EnsureLines();
            return _lines;
        }
    }

    /// <summary>The path of the line under the pointer, or null.</summary>
    internal string? HoveredPath => _hoveredPath;

    /// <summary>The drawing part.</summary>
    internal ExtractionJsonLines? Drawing => _linesPart;

    /// <summary>The scroll viewer of the template.</summary>
    internal ScrollViewer? Scroll => _scroll;

    /// <summary>The tint under the linked line now (transparent when none).</summary>
    internal Color LinkedTint => _link.Path is null ? Colors.Transparent : _link.Brush.Color;

    /// <summary>The tint under the line that was linked before and is fading out (transparent when none).</summary>
    internal Color FadingTint => _unlink.Path is null ? Colors.Transparent : _unlink.Brush.Color;

    /// <summary>The flash over the changed line now (transparent when none).</summary>
    internal Color FlashTint => _flash.Path is null ? Colors.Transparent : _flash.Brush.Color;

    /// <summary>Whether a flash waits for the view to be shown.</summary>
    internal bool IsFlashPending => _flashPending;

    /// <summary>The index of the line showing <paramref name="path"/>, or -1.</summary>
    public int LineIndexOf(string path)
    {
        EnsureLines();
        return _indexOf.GetValueOrDefault(path, -1);
    }

    /// <summary>
    /// Flashes the line of <paramref name="path"/> (Success 20% fading to rest over 1400 ms; nothing under Reduced or Off),
    /// also when it already is <see cref="ChangedPath"/>. While the view is hidden the flash waits until it is shown.
    /// </summary>
    public void Flash(string path)
    {
        if (string.Equals(ChangedPath, path, StringComparison.Ordinal))
        {
            StartFlash(path);
        }
        else
        {
            SetCurrentValue(ChangedPathProperty, path);
        }
    }

    /// <summary>
    /// The rectangle of the line of <paramref name="path"/> in <paramref name="relativeTo"/>'s coordinates: as wide as the
    /// visible part of the block (so the link's end sits at the block's left edge), one line high. False when no line has
    /// the path or the view is not laid out.
    /// </summary>
    public bool TryGetLineBounds(string path, Visual relativeTo, out Rect bounds)
    {
        bounds = default;
        var index = LineIndexOf(path);
        if (index < 0 || _linesPart is not { } lines || _scroll is not { } scroll)
        {
            return false;
        }
        var top = lines.TranslatePoint(new Point(0, index * lines.LineHeight), relativeTo);
        var left = scroll.TranslatePoint(default, relativeTo);
        if (top is not { } t || left is not { } l)
        {
            return false;
        }
        bounds = new Rect(l.X, t.Y, scroll.Bounds.Width, lines.LineHeight);
        return true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_linesPart is not null)
        {
            _linesPart.Owner = null;
        }
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _linesPart = e.NameScope.Find<ExtractionJsonLines>(LinesPart);
        if (_linesPart is not null)
        {
            _linesPart.Owner = this;
            UpdateStyle();
            EnsureLines();
            _linesPart.SetLines(_lines);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == OutputProperty || property == SchemaTextProperty)
        {
            Rebuild();
        }
        else if (property == LinkedPathProperty)
        {
            UpdateLink(change.GetNewValue<string?>(), true);
        }
        else if (property == TonesProperty)
        {
            UpdateLink(LinkedPath, false);
        }
        else if (property == ChangedPathProperty)
        {
            if (change.GetNewValue<string?>() is { } path)
            {
                StartFlash(path);
            }
        }
        else if (property == FontFamilyProperty || property == FontSizeProperty || property == FontWeightProperty ||
                 property == LineHeightProperty || property == ForegroundProperty || property == PunctuationForegroundProperty ||
                 property == MutedForegroundProperty || property == StringBaseProperty || property == NumberBaseProperty)
        {
            UpdateStyle();
        }
        else if (property == FlashBrushProperty)
        {
            RecolorFlash();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        UpdateLink(LinkedPath, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        UpdateHover(null);
        _pointer = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLines();
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_flashPending && ChangedPath is { } path)
        {
            // Asked for while hidden: the view is being laid out, so it is about to be seen.
            StartFlash(path, true);
        }
        return size;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch || _linesPart is null)
        {
            return;
        }
        _pointer = e.GetPosition(_linesPart);
        UpdateHover(PathAt(_pointer.Value));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        UpdateHover(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_linesPart is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (PathAt(e.GetPosition(_linesPart)) is { } path)
        {
            RaiseEvent(new ExtractionPathEventArgs(PathPressedEvent, this, path));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _scroll is null || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var step = e.Key switch
        {
            Key.Left => -KeyScrollStep,
            Key.Right => KeyScrollStep,
            _ => 0
        };
        if (step == 0)
        {
            return;
        }
        var max = Math.Max(0, _scroll.Extent.Width - _scroll.Viewport.Width);
        var x = Math.Clamp(_scroll.Offset.X + step, 0, max);
        if (x != _scroll.Offset.X)
        {
            _scroll.Offset = _scroll.Offset.WithX(x);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExtractionJsonViewAutomationPeer(this);

    /// <summary>Draws the tinted line backgrounds between lines <paramref name="first"/> and <paramref name="last"/> (called by the lines part).</summary>
    internal void DrawLineBackgrounds(DrawingContext context, double width, double lineHeight, int first, int last)
    {
        DrawTint(context, _unlink, width, lineHeight, first, last);
        DrawTint(context, _link, width, lineHeight, first, last);
        DrawTint(context, _flash, width, lineHeight, first, last);
    }

    private void DrawTint(DrawingContext context, LineTint tint, double width, double lineHeight, int first, int last)
    {
        if (tint.Path is not { } path || tint.Brush.Color.A == 0 || !_indexOf.TryGetValue(path, out var index) || index < first || index >= last)
        {
            return;
        }
        context.FillRectangle(tint.Brush, new Rect(0, index * lineHeight, width, lineHeight));
    }

    /// <summary>Writes the shown JSON again; its lines follow when needed.</summary>
    private void Rebuild()
    {
        Text = Output is { } output ? output.ToJson() : SchemaText ?? string.Empty;
        _linesStale = true;
        InvalidateMeasure();
    }

    /// <summary>Cuts the text into lines and maps their paths, if the text changed since the last time.</summary>
    private void EnsureLines()
    {
        if (!_linesStale)
        {
            return;
        }
        _linesStale = false;
        _lines = ExtractionJsonText.Split(Text, Output is not null);
        _indexOf.Clear();
        for (var i = 0; i < _lines.Length; i++)
        {
            if (_lines[i].Path is { } path)
            {
                _indexOf.TryAdd(path, i);
            }
        }
        _linesPart?.SetLines(_lines);
        if (_pointer is { } pointer)
        {
            UpdateHover(PathAt(pointer));
        }
        else if (_hoveredPath is not null && !_indexOf.ContainsKey(_hoveredPath))
        {
            UpdateHover(null);
        }
    }

    private string? PathAt(Point point)
    {
        EnsureLines();
        if (_linesPart is null || point.X < 0 || point.X > _linesPart.Bounds.Width)
        {
            return null;
        }
        var index = (int)Math.Floor(point.Y / _linesPart.LineHeight);
        return index >= 0 && index < _lines.Length && point.Y >= 0 ? _lines[index].Path : null;
    }

    private void UpdateHover(string? path)
    {
        if (string.Equals(path, _hoveredPath, StringComparison.Ordinal))
        {
            return;
        }
        var old = _hoveredPath;
        _hoveredPath = path;
        if (old is not null)
        {
            RaiseEvent(new ExtractionPathEventArgs(PathExitedEvent, this, old));
        }
        if (path is not null)
        {
            RaiseEvent(new ExtractionPathEventArgs(PathEnteredEvent, this, path));
        }
    }

    /// <summary>Moves the tint to the line of <paramref name="path"/>; the previous line fades out unless it is the same line.</summary>
    private void UpdateLink(string? path, bool animate)
    {
        var color = path is null ? Colors.Transparent : PromptTone.SeriesColor(this, Tones?.Invoke(path) ?? 1);
        if (string.Equals(path, _link.Path, StringComparison.Ordinal))
        {
            _link.Color = color;
            _link.Paint();
            return;
        }
        var spec = !animate ? MotionSpec.Instant
            : ArcMotion.IsFull(this) ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, Linear);
        // The line that was linked fades from where it is (the one fading before it is dropped).
        _unlink.Path = _link.Path;
        _unlink.Color = _link.Color;
        var from = _link.Path is null ? 0 : _link.Opacity.Value;
        _unlink.Opacity.Jump(from);
        _ = _unlink.Opacity.AnimateTo(0, spec);
        _link.Path = path;
        _link.Color = color;
        _link.Opacity.Jump(0);
        _ = _link.Opacity.AnimateTo(path is null ? 0 : 1, spec);
    }

    private void StartFlash(string path, bool laidOut = false)
    {
        _flash.Path = path;
        RecolorFlash();
        if (!ArcMotion.IsFull(this))
        {
            // Reduced and Off: no flash; the line is at rest.
            _flashPending = false;
            _flash.Opacity.Jump(0);
            return;
        }
        if (!laidOut && (!IsEffectivelyVisible || VisualRoot is null || Bounds.Width <= 0))
        {
            _flashPending = true;
            _flash.Opacity.Jump(0);
            return;
        }
        _flashPending = false;
        _flash.Opacity.Jump(1);
        _ = _flash.Opacity.AnimateTo(0, MotionSpec.Tween(FlashDuration, ArcEasings.Standard));
    }

    private void RecolorFlash()
    {
        _flash.Color = FlashBrush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        _flash.Paint();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateLink(LinkedPath, false);
        _unlink.Path = null;
    }

    /// <summary>Hands the font and the five piece colors to the lines part.</summary>
    private void UpdateStyle()
    {
        if (_linesPart is null)
        {
            return;
        }
        var foreground = ColorOf(Foreground);
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var lineHeight = double.IsNaN(LineHeight) || LineHeight <= 0 ? FontSize * 1.7 : LineHeight;
        _linesPart.SetStyle(new ExtractionJsonStyle(typeface, FontSize, lineHeight,
            Foreground ?? Brushes.Black,
            new ImmutableSolidColorBrush(Oklch.MixOklab(ColorOf(StringBase), foreground, 0.22)),
            new ImmutableSolidColorBrush(Oklch.MixOklab(ColorOf(NumberBase), foreground, 0.2)),
            MutedForeground ?? Foreground ?? Brushes.Gray,
            PunctuationForeground ?? Foreground ?? Brushes.Gray));
    }

    private static Color ColorOf(IBrush? brush) =>
        brush is ISolidColorBrush solid ? PromptTone.WithOpacity(solid.Color, solid.Opacity) : Colors.Gray;

    /// <summary>
    /// A tinted line: its path, the base color at a fixed strength and a 0–1 opacity driven by a motion value; the
    /// brush is recolored when the opacity steps (between frames, never while rendering) and the lines part draws again.
    /// </summary>
    private sealed class LineTint
    {
        private readonly ExtractionJsonView _owner;
        private readonly double _alpha;
        private MotionValue<double>? _opacity;

        public LineTint(ExtractionJsonView owner, double alpha)
        {
            _owner = owner;
            _alpha = alpha;
        }

        public string? Path { get; set; }

        public Color Color { get; set; }

        /// <summary>The brush the background is drawn with (mutable: recolored between frames).</summary>
        public SolidColorBrush Brush { get; } = new(Colors.Transparent);

        /// <summary>The 0–1 strength of the tint (made on first use).</summary>
        public MotionValue<double> Opacity
        {
            get
            {
                if (_opacity is null)
                {
                    _opacity = new MotionValue<double>(_owner, 0, MotionVectors.Double);
                    _opacity.Changed += _ => Paint();
                }
                return _opacity;
            }
        }

        public void Paint()
        {
            Brush.Color = PromptTone.WithOpacity(Color, _alpha * (_opacity?.Value ?? 0));
            _owner._linesPart?.InvalidateVisual();
        }
    }

    /// <summary>A document named by <see cref="AutomationName"/> whose value is the JSON text.</summary>
    private sealed class ExtractionJsonViewAutomationPeer : ControlAutomationPeer, IValueProvider
    {
        public ExtractionJsonViewAutomationPeer(ExtractionJsonView owner) : base(owner)
        {
        }

        public bool IsReadOnly => true;

        public string? Value => ((ExtractionJsonView)Owner).Text;

        public void SetValue(string? value)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? ((ExtractionJsonView)Owner).AutomationName : name;
        }
    }
}
