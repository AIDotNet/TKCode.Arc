using System.Collections.Specialized;
using System.Text;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The 2px line left of a block while the person asks about a selection in it (Arc's <c>::before</c>: Accent, radius 1,
/// 16px left of the text, 4px in from the block's top and bottom). <c>:on</c> shows it; the opacity change is a theme
/// transition (160ms standard; 120ms at reduced motion; at once when motion is off).
/// </summary>
internal sealed class AiPanelBlockMarker : Control
{
    public const string OnPseudoClass = ":on";

    /// <summary>The line's color.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AiPanelBlockMarker, IBrush?>(nameof(Fill));

    static AiPanelBlockMarker()
    {
        AffectsRender<AiPanelBlockMarker>(FillProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiPanelBlockMarker>(false);
    }

    protected override Type StyleKeyOverride => typeof(AiPanelBlockMarker);

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>Whether the line shows.</summary>
    public bool IsOn
    {
        get => PseudoClasses.Contains(OnPseudoClass);
        set => PseudoClasses.Set(OnPseudoClass, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Fill is { } fill && Bounds.Height > 0)
        {
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(Bounds.Size), 1));
        }
    }
}

/// <summary>
/// The tint that sweeps a block when a change is applied or undone (spec 5.6): Accent at 13% over the block's text area,
/// radius 10, revealed left to right like Arc's <c>clip-path: inset(0 X% 0 0 round 10)</c> over 420ms (standard), held
/// to 770ms, then faded out by 1400ms (CSS <c>ease</c>). The clip has round corners on the moving edge too, so the
/// visible part is a rounded rectangle of the revealed width. Reduced motion shows the whole tint and fades it in 600ms;
/// motion off shows nothing. Drawn in <see cref="Render"/> from one timeline value: no layout while it plays.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): a ControlTheme transition cannot express a three-phase keyframe timeline
/// (reveal, hold, fade) on a clip that depends on the measured width.
/// </para>
/// </summary>
internal sealed class AiPanelSweep : Control
{
    /// <summary>The reveal: 30% of 1400ms.</summary>
    public const double RevealMs = 420;

    /// <summary>The fade starts at 55% of 1400ms.</summary>
    public const double FadeStartMs = 770;

    /// <summary>The whole sweep.</summary>
    public const double TotalMs = 1400;

    /// <summary>The reduced-motion fade.</summary>
    public const double ReducedMs = 600;

    /// <summary>The tint's opacity over the accent.</summary>
    public const double TintAlpha = 0.13;

    public const double Radius = 10;

    /// <summary>The tint color (Accent).</summary>
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<AiPanelSweep, IBrush?>(nameof(Tint));

    /// <summary>CSS <c>ease</c>.</summary>
    internal static readonly Easing CssEase = new SplineEasing(0.25, 0.1, 0.25, 1);

    private MotionValue<double>? _time;
    private bool _reduced;
    private int _generation;

    static AiPanelSweep()
    {
        AffectsRender<AiPanelSweep>(TintProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiPanelSweep>(false);
    }

    protected override Type StyleKeyOverride => typeof(AiPanelSweep);

    /// <inheritdoc cref="TintProperty"/>
    public IBrush? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

    /// <summary>True while the sweep plays.</summary>
    public bool IsPlaying => _time?.IsAnimating == true;

    /// <summary>The share of the width revealed (0–1).</summary>
    public double Reveal => _time is null || !IsVisible ? 0 : RevealAt(_time.Value, _reduced);

    /// <summary>The tint's opacity factor (0–1; multiplied by <see cref="TintAlpha"/>).</summary>
    public double Fade => _time is null || !IsVisible ? 0 : FadeAt(_time.Value, _reduced);

    /// <summary>The share revealed <paramref name="ms"/> after the start.</summary>
    public static double RevealAt(double ms, bool reduced) =>
        reduced ? 1 : ArcEasings.Standard.Ease(Math.Clamp(ms / RevealMs, 0, 1));

    /// <summary>The opacity factor <paramref name="ms"/> after the start.</summary>
    public static double FadeAt(double ms, bool reduced)
    {
        if (reduced)
        {
            return 1 - CssEase.Ease(Math.Clamp(ms / ReducedMs, 0, 1));
        }
        return ms <= FadeStartMs ? 1 : 1 - CssEase.Ease(Math.Clamp((ms - FadeStartMs) / (TotalMs - FadeStartMs), 0, 1));
    }

    /// <summary>Plays the sweep from the start at <paramref name="level"/> (Off shows nothing).</summary>
    public void Play(ArcMotionLevel level)
    {
        var generation = ++_generation;
        if (level == ArcMotionLevel.Off)
        {
            Stop();
            return;
        }
        if (_time is null)
        {
            _time = new MotionValue<double>(this, 0, MotionVectors.Double);
            _time.Changed += _ => InvalidateVisual();
        }
        _reduced = level == ArcMotionLevel.Reduced;
        IsVisible = true;
        _time.Jump(0);
        var end = _reduced ? ReducedMs : TotalMs;
        _ = HideWhenDone(_time.AnimateTo(end, MotionSpec.Tween(TimeSpan.FromMilliseconds(end), new LinearEasing())), generation);
    }

    /// <summary>Removes the tint at once.</summary>
    public void Stop()
    {
        _generation++;
        _time?.Stop();
        IsVisible = false;
    }

    private async Task HideWhenDone(Task<bool> run, int generation)
    {
        if (await run && generation == _generation)
        {
            IsVisible = false;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Tint is not { } tint || _time is null)
        {
            return;
        }
        var reveal = Reveal;
        var fade = Fade;
        var size = Bounds.Size;
        var width = size.Width * reveal;
        if (width <= 0.01 || fade <= 0.001 || size.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(Radius, Math.Min(width, size.Height) / 2);
        using (context.PushOpacity(TintAlpha * fade))
        {
            context.DrawRectangle(tint, null, new RoundedRect(new Rect(0, 0, width, size.Height), radius));
        }
    }
}

/// <summary>
/// The box of a block (Arc's <c>BlockBox</c>): the text area with 8px on each side and 4px above and below, radius 10, the
/// sweep under the text, and while a change plays the old text fading out over the new one. Its height follows the content
/// at once, except after <see cref="Play"/>, when it moves from the old height to the new one on the
/// <see cref="ArcSpring.Settle"/> spring (Arc measured ω ≈ 15.5, no overshoot) without the one-frame jump to the new height
/// Arc shows. While the height moves or old text shows, everything is clipped to the box.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): the height targets a measured content size and keeps its velocity when the
/// target moves (the page column narrowing mid-spring); the old and new text run different curves and delays (100ms
/// ease-out out; 80ms later, 180ms standard in).
/// </para>
/// </summary>
internal sealed class AiPanelBlockBox : Control
{
    /// <summary>Space left and right of the text inside the box.</summary>
    public const double InsetX = 8;

    /// <summary>Space above and below the text inside the box.</summary>
    public const double InsetY = 4;

    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly Easing s_linear = new LinearEasing();

    private readonly AiPanelSweep _sweep = new();
    private readonly RectangleGeometry _clip = new();
    private Control? _body;
    private Control? _leaving;
    private MotionValue<double>? _height;
    private MotionValue<double>? _bodyOpacity;
    private MotionValue<double>? _leavingOpacity;
    private bool _armed;
    private bool _measuring;
    private int _generation;

    public AiPanelBlockBox()
    {
        _sweep.IsVisible = false;
        VisualChildren.Add(_sweep);
        LogicalChildren.Add(_sweep);
    }

    /// <summary>The sweep layer.</summary>
    public AiPanelSweep Sweep => _sweep;

    /// <summary>The content on show.</summary>
    public Control? Body => _body;

    /// <summary>The old content fading out, while a change plays.</summary>
    public Control? Leaving => _leaving;

    /// <summary>True while the height springs.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>Shows <paramref name="body"/> at once (no sweep, no fade, the height jumps).</summary>
    public void Show(Control body)
    {
        _generation++;
        _armed = false;
        _sweep.Stop();
        RemoveLeaving();
        _bodyOpacity?.Jump(1);
        // A stopped height lets the next measure take the new content's height as it is.
        _height?.Stop();
        Replace(body);
        body.Opacity = 1;
        InvalidateMeasure();
    }

    /// <summary>
    /// Replaces the content with <paramref name="body"/> the way Arc applies a change at <paramref name="level"/>: Full
    /// sweeps, cross-fades and springs the height; Reduced fades the whole tint and cross-fades in 120ms with the height at
    /// once; Off is <see cref="Show"/>.
    /// </summary>
    public void Play(Control body, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off || _body is null)
        {
            Show(body);
            return;
        }
        var generation = ++_generation;
        EnsureValues();
        // Start from the height on screen: the next measure springs from it to the new content's height.
        _height!.Jump(Bounds.Height > 0 ? Bounds.Height : _height.Value);
        _armed = level == ArcMotionLevel.Full;
        RemoveLeaving();
        var old = _body;
        _body = null;
        _leaving = old;
        _leaving.IsHitTestVisible = false;
        _leavingOpacity!.Jump(old.Opacity);
        Add(body);
        _body = body;
        _bodyOpacity!.Jump(0);
        body.Opacity = 0;
        var reduced = level == ArcMotionLevel.Reduced;
        var outSpec = reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(100), EaseOut);
        var inSpec = reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, s_linear)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Standard, TimeSpan.FromMilliseconds(80));
        _ = RemoveWhenFaded(_leavingOpacity.AnimateTo(0, outSpec), generation);
        _ = _bodyOpacity.AnimateTo(1, inSpec);
        _sweep.Play(level);
        InvalidateMeasure();
        UpdateClip();
    }

    private void EnsureValues()
    {
        if (_height is not null)
        {
            return;
        }
        _height = new MotionValue<double>(this, Bounds.Height, MotionVectors.Double);
        _height.Changed += _ =>
        {
            // A jump made by the measure itself needs no second measure.
            if (!_measuring)
            {
                InvalidateMeasure();
            }
            UpdateClip();
        };
        _bodyOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _bodyOpacity.Changed += v =>
        {
            if (_body is not null)
            {
                _body.Opacity = Math.Clamp(v, 0, 1);
            }
        };
        _leavingOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _leavingOpacity.Changed += v =>
        {
            if (_leaving is not null)
            {
                _leaving.Opacity = Math.Clamp(v, 0, 1);
            }
        };
    }

    private async Task RemoveWhenFaded(Task<bool> fade, int generation)
    {
        if (await fade && generation == _generation)
        {
            RemoveLeaving();
            UpdateClip();
        }
    }

    private void RemoveLeaving()
    {
        if (_leaving is null)
        {
            return;
        }
        VisualChildren.Remove(_leaving);
        LogicalChildren.Remove(_leaving);
        _leaving = null;
    }

    private void Replace(Control body)
    {
        if (ReferenceEquals(_body, body))
        {
            return;
        }
        if (_body is not null)
        {
            VisualChildren.Remove(_body);
            LogicalChildren.Remove(_body);
        }
        _body = body;
        Add(body);
    }

    private void Add(Control body)
    {
        // Right after the sweep (under the text): a leaving text, already in the tree, draws over the new one, as Arc's
        // absolute layer does.
        VisualChildren.Insert(1, body);
        LogicalChildren.Add(body);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var inner = new Size(Math.Max(0, width - 2 * InsetX), double.PositiveInfinity);
        _leaving?.Measure(inner);
        var content = 0.0;
        if (_body is not null)
        {
            _body.Measure(inner);
            content = _body.DesiredSize.Height;
            if (!double.IsFinite(availableSize.Width))
            {
                width = _body.DesiredSize.Width + 2 * InsetX;
            }
        }
        var target = content + 2 * InsetY;
        var height = target;
        if (_height is not null)
        {
            _measuring = true;
            try
            {
                height = HeightFor(target);
            }
            finally
            {
                _measuring = false;
            }
        }
        return new Size(width, Math.Max(0, height));
    }

    private double HeightFor(double target)
    {
        var height = _height!;
        if (_armed)
        {
            _armed = false;
            _ = height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Settle));
        }
        else if (height.IsAnimating)
        {
            if (Math.Abs(height.Target - target) > 0.01)
            {
                // The content changed size mid-spring (the column narrowed): keep the velocity, go to the new height.
                _ = height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Settle));
            }
        }
        else if (Math.Abs(height.Value - target) > 0.01)
        {
            height.Jump(target);
        }
        return height.IsAnimating ? height.Value : target;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var innerWidth = Math.Max(0, finalSize.Width - 2 * InsetX);
        var bodyHeight = _body?.DesiredSize.Height ?? 0;
        _body?.Arrange(new Rect(InsetX, InsetY, innerWidth, bodyHeight));
        if (_leaving is { } leaving)
        {
            leaving.Arrange(new Rect(InsetX, InsetY, innerWidth, leaving.DesiredSize.Height));
        }
        // The tint covers the new text's area (Arc's sweep is inset 0 of the static content).
        _sweep.Arrange(new Rect(InsetX, InsetY, innerWidth, bodyHeight));
        UpdateClip(finalSize);
        return finalSize;
    }

    private void UpdateClip() => UpdateClip(Bounds.Size);

    private void UpdateClip(Size size)
    {
        var clips = IsMoving || _leaving is not null;
        if (!clips)
        {
            if (Clip is not null)
            {
                Clip = null;
            }
            return;
        }
        _clip.Rect = new Rect(size);
        Clip = _clip;
        InvalidateVisual();
    }
}

/// <summary>
/// One block of the page: the marker line on its left and its box (<see cref="AiPanelBlockBox"/>) with the heading,
/// paragraph or table. It observes its block's data and tells the page when it changed (<see cref="DataChanged"/>); the
/// page then calls <see cref="Sync"/> on the next dispatcher turn, so a host that changes the data and plays the change in
/// the same turn (in either order) keeps the animation. The block's label is its accessible name. Pointer presses reach
/// the page (the texts take no input): the block only shows the text cursor and gives the hit area.
/// </summary>
internal sealed class AiPanelBlockView : Control
{
    /// <summary>The marker's distance left of the text.</summary>
    public const double MarkerOffset = 16;

    /// <summary>The marker's width.</summary>
    public const double MarkerWidth = 2;

    private static Cursor? s_textCursor;

    private readonly AiPanelBlockMarker _marker = new();
    private readonly AiPanelBlockBox _box = new();
    private readonly List<(AiPanelText Text, int Row, int Column)> _texts = [];
    private AiPanelBlock _block;
    private string _shownKey = string.Empty;
    private bool _observing;
    private AiPanelTable? _observedTable;

    public AiPanelBlockView(AiPanelBlock block)
    {
        _block = block;
        // One text cursor for every block (made on first use, once the platform is up).
        Cursor = s_textCursor ??= new Cursor(StandardCursorType.Ibeam);
        VisualChildren.Add(_box);
        LogicalChildren.Add(_box);
        VisualChildren.Add(_marker);
        LogicalChildren.Add(_marker);
        Show(block);
        RefreshName();
    }

    /// <summary>Raised when the observed block's content or label changed.</summary>
    public event EventHandler? DataChanged;

    /// <summary>The data block shown.</summary>
    public AiPanelBlock Block => _block;

    /// <summary>The marker line.</summary>
    public AiPanelBlockMarker Marker => _marker;

    /// <summary>The box.</summary>
    public AiPanelBlockBox Box => _box;

    /// <summary>The texts of the content on show, in reading order (one, or the table's cells).</summary>
    public IReadOnlyList<(AiPanelText Text, int Row, int Column)> Texts => _texts;

    /// <summary>The content key of what is on show (tests).</summary>
    internal string ShownKey => _shownKey;

    /// <summary>Whether the marker shows.</summary>
    public bool IsMarked
    {
        get => _marker.IsOn;
        set => _marker.IsOn = value;
    }

    /// <summary>Observes <paramref name="block"/> instead (the page replaced the object); the content is synced by the page.</summary>
    public void Rebind(AiPanelBlock block)
    {
        if (ReferenceEquals(block, _block))
        {
            return;
        }
        Unobserve();
        _block = block;
        if (IsAttachedToVisualTree())
        {
            Observe();
        }
        RefreshName();
    }

    /// <summary>Shows the content of <paramref name="content"/> at once.</summary>
    public void Show(AiPanelBlock content)
    {
        _shownKey = ContentKey(content);
        _box.Show(CreateBody(content));
    }

    /// <summary>Plays a change to the content of <paramref name="content"/> at <paramref name="level"/>.</summary>
    public void Play(AiPanelBlock content, ArcMotionLevel level)
    {
        _shownKey = ContentKey(content);
        _box.Play(CreateBody(content), level);
    }

    /// <summary>True when the data's content differs from what is on show.</summary>
    public bool NeedsSync => ContentKey(_block) != _shownKey;

    /// <summary>Shows the data's content at once if it differs from what is on show; true when it did.</summary>
    public bool Sync()
    {
        RefreshName();
        if (!NeedsSync)
        {
            return false;
        }
        Show(_block);
        return true;
    }

    private void RefreshName() => AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(_block.Label) ? null : _block.Label);

    private Control CreateBody(AiPanelBlock content)
    {
        _texts.Clear();
        switch (content)
        {
            case AiPanelTable table:
            {
                var view = new AiPanelTableView(table) { Margin = new Thickness(0, 6, 0, 0) };
                _texts.AddRange(view.Cells());
                return view;
            }
            case AiPanelHeading heading:
            {
                var text = new AiPanelText(AiPanelTextRole.Heading) { Text = heading.Text, TextWrapping = TextWrapping.Wrap };
                _texts.Add((text, -1, -1));
                return text;
            }
            case AiPanelParagraph paragraph:
            {
                var text = new AiPanelText(AiPanelTextRole.Paragraph) { Text = paragraph.Text, TextWrapping = TextWrapping.Wrap };
                _texts.Add((text, -1, -1));
                return text;
            }
            default:
                return new Panel();
        }
    }

    /// <summary>A key that is equal for blocks that look the same (kind, text, columns, cells).</summary>
    public static string ContentKey(AiPanelBlock block)
    {
        switch (block)
        {
            case AiPanelHeading heading:
                return "h\u001f" + heading.Text;
            case AiPanelParagraph paragraph:
                return "p\u001f" + paragraph.Text;
            case AiPanelTable table:
            {
                var key = new StringBuilder("t");
                foreach (var column in table.Columns)
                {
                    key.Append('\u001f').Append(column.Header).Append('\u001e').Append((int)column.Alignment).Append(column.IsRowHeader ? '1' : '0');
                }
                foreach (var row in table.Rows)
                {
                    key.Append('\u001d');
                    foreach (var cell in row.Cells)
                    {
                        key.Append(cell).Append('\u001e');
                    }
                }
                return key.ToString();
            }
            default:
                return block.GetType().Name;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unobserve();
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private void Observe()
    {
        if (_observing)
        {
            return;
        }
        _observing = true;
        _block.PropertyChanged += OnBlockPropertyChanged;
        if (_block is AiPanelTable table)
        {
            _observedTable = table;
            table.Columns.CollectionChanged += OnTableCollectionChanged;
            table.Rows.CollectionChanged += OnTableCollectionChanged;
            foreach (var column in table.Columns)
            {
                column.PropertyChanged += OnTablePartChanged;
            }
            foreach (var row in table.Rows)
            {
                row.Cells.CollectionChanged += OnTableCollectionChanged;
            }
        }
    }

    private void Unobserve()
    {
        if (!_observing)
        {
            return;
        }
        _observing = false;
        _block.PropertyChanged -= OnBlockPropertyChanged;
        if (_observedTable is { } table)
        {
            table.Columns.CollectionChanged -= OnTableCollectionChanged;
            table.Rows.CollectionChanged -= OnTableCollectionChanged;
            foreach (var column in table.Columns)
            {
                column.PropertyChanged -= OnTablePartChanged;
            }
            foreach (var row in table.Rows)
            {
                row.Cells.CollectionChanged -= OnTableCollectionChanged;
            }
            _observedTable = null;
        }
    }

    private void OnBlockPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => DataChanged?.Invoke(this, EventArgs.Empty);

    private void OnTablePartChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => DataChanged?.Invoke(this, EventArgs.Empty);

    private void OnTableCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // New columns or rows need their own subscriptions: observe the table again.
        if (_observing && (ReferenceEquals(sender, _observedTable?.Columns) || ReferenceEquals(sender, _observedTable?.Rows)))
        {
            Unobserve();
            Observe();
        }
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        _box.Measure(new Size(width + 2 * AiPanelBlockBox.InsetX, double.PositiveInfinity));
        _marker.Measure(Size.Infinity);
        var boxWidth = _box.DesiredSize.Width - 2 * AiPanelBlockBox.InsetX;
        return new Size(double.IsFinite(width) ? width : Math.Max(0, boxWidth), _box.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var height = _box.DesiredSize.Height;
        _box.Arrange(new Rect(-AiPanelBlockBox.InsetX, 0, finalSize.Width + 2 * AiPanelBlockBox.InsetX, height));
        _marker.Arrange(new Rect(-MarkerOffset, AiPanelBlockBox.InsetY, MarkerWidth, Math.Max(0, height - 2 * AiPanelBlockBox.InsetY)));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The whole block takes the pointer (the texts do not), so a press anywhere in it starts a selection.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }
}
