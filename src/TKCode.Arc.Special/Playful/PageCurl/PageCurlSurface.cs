using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Which of the three drawing layers between and above the sheets a <see cref="PageCurlFx"/> paints.</summary>
internal enum PageCurlLayer
{
    /// <summary>Under every sheet: the book's shadow. Redrawn only when the book moves or a side empties.</summary>
    Book,

    /// <summary>Under the turning sheet: the book's shadow, the spine shade and the shade the fold casts on the page it uncovers.</summary>
    Under,

    /// <summary>Between the sheet and the flap: the shadow the flap casts downward.</summary>
    Mid,

    /// <summary>Over everything: the lit curl on the flap and the chevron.</summary>
    Over
}

/// <summary>A thin drawing layer of <see cref="PageCurlSurface"/>; it holds no state of its own.</summary>
internal sealed class PageCurlFx : Control
{
    private readonly PageCurlSurface _surface;
    private readonly PageCurlLayer _layer;

    public PageCurlFx(PageCurlSurface surface, PageCurlLayer layer)
    {
        _surface = surface;
        _layer = layer;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context) => _surface.DrawLayer(_layer, context);
}

/// <summary>
/// The book of <see cref="PageCurl"/>: it lays the sheets out once per size, then moves them with render transforms
/// and clips only (no layout per frame). It owns the turn state (resting, dragging, settling after a release, or
/// following the arc of a tap), the pointer handling, the corner peeks and the tease.
/// </summary>
internal sealed class PageCurlSurface : Panel
{
    /// <summary>Room under the book for the hint; zero when there is no hint.</summary>
    public static readonly StyledProperty<double> HintAreaHeightProperty =
        AvaloniaProperty.Register<PageCurlSurface, double>(nameof(HintAreaHeight), 34);

    /// <summary>The shadow under the pages; the theme uses the raised shadow.</summary>
    public static readonly StyledProperty<BoxShadows> PageShadowProperty =
        AvaloniaProperty.Register<PageCurlSurface, BoxShadows>(nameof(PageShadow));

    /// <summary>The disc behind the chevron.</summary>
    public static readonly StyledProperty<IBrush?> ChevronFillProperty =
        AvaloniaProperty.Register<PageCurlSurface, IBrush?>(nameof(ChevronFill));

    /// <summary>The chevron's stroke.</summary>
    public static readonly StyledProperty<IBrush?> ChevronStrokeProperty =
        AvaloniaProperty.Register<PageCurlSurface, IBrush?>(nameof(ChevronStroke));

    private const int PoolSize = 4;

    // Pointer travel before a press becomes a drag, and the release speed (px/s) that decides a turn on its own.
    private const double DragThreshold = 5;
    private const double TouchDragThreshold = 10;
    private const double FlickSpeed = 450;

    // Peeks, as a fraction of the page width the corner is lifted by.
    private const double HoverLift = 0.1;
    private const double HoverZone = 0.26;

    private static readonly MotionSpec s_peekSpring = MotionSpec.Spring(SpringSpec.FromVisual(0.4, 0));
    private static readonly MotionSpec s_arcSpring = MotionSpec.Spring(SpringSpec.FromVisual(0.36, 0));
    private static readonly MotionSpec s_settleSpring = MotionSpec.Spring(SpringSpec.FromVisual(0.42, 0));
    private static readonly MotionSpec s_fade = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    // The shading is filled straight into polygons with gradients laid along the fold's normal, so a frame draws a few
    // plain fills: no clip layers, no opacity layers and no blur.
    private static readonly ImmutableGradientStop[] s_foldShadeStops =
    [
        new(0, Color.FromArgb(70, 0, 0, 0)),
        new(0.18, Color.FromArgb(30, 0, 0, 0)),
        new(0.55, Color.FromArgb(8, 0, 0, 0)),
        new(1, Color.FromArgb(0, 0, 0, 0))
    ];

    private static readonly ImmutableGradientStop[] s_curlStops =
    [
        new(0, Color.FromArgb(34, 0, 0, 0)),
        new(0.22, Color.FromArgb(150, 255, 255, 255)),
        new(0.5, Color.FromArgb(70, 255, 255, 255)),
        new(1, Color.FromArgb(0, 255, 255, 255))
    ];

    private static readonly IImmutableBrush s_spineLeft = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Color.FromArgb(0, 0, 0, 0)), new ImmutableGradientStop(1, Color.FromArgb(34, 0, 0, 0))],
        startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));

    private static readonly IImmutableBrush s_spineRight = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Color.FromArgb(34, 0, 0, 0)), new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))],
        startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));

    // The flap's shadow on the pages: copies of its outline, each a little lower and fainter, that add up to a soft edge.
    private static readonly IImmutableBrush s_flapCast = new ImmutableSolidColorBrush(Color.FromArgb(9, 0, 0, 0));
    private static readonly double[] s_flapCastOffsets = [0.6, 1.4, 2.4, 3.6, 5, 6.8, 9];

    private readonly PageCurlPage[] _pool = new PageCurlPage[PoolSize];
    private readonly PageCurlFx _book;
    private readonly PageCurlFx _under;
    private readonly PageCurlFx _mid;
    private readonly PageCurlFx _over;
    private readonly BitmapCache _pageCache = new();
    private readonly ContentPresenter _hint = new() { IsHitTestVisible = false };
    private readonly MotionValue<double> _arc;
    private readonly MotionValue<Point> _settle;
    private readonly MotionValue<double> _lift;
    private readonly MotionValue<double> _chevron;
    private readonly MotionValue<double> _hintOpacity;
    private readonly VelocityTracker _velocity = new();
    private readonly Point[] _scratch = new Point[PageCurlGeometry.MaxVertices];
    private readonly Point[] _sheetFlap = new Point[PageCurlGeometry.MaxVertices];
    private readonly Point[] _mirrorFlap = new Point[PageCurlGeometry.MaxVertices];
    private readonly int[] _wanted = new int[PoolSize];

    private PageCurl? _owner;
    private ViewportVisibility? _visibility;
    private double _w;
    private double _h;
    private double _bookLeft;
    private int _shown;

    // The turn in progress.
    private TurnMode _mode;
    private int _dir;
    private bool _bottom;
    private Point _corner;
    private Point _pointer;
    private Point _dragBase;
    private Point _dragStart;
    private Vector _residue;
    private bool _raise;
    private bool _settleComplete;
    private int _turnId;

    // Pointer press.
    private bool _pressed;
    private bool _dragging;
    private bool _swapOnly;
    private int _pressDir;
    private bool _pressBottom;
    private Point _pressStart;
    private IPointer? _capturedPointer;

    // Corner peek (hover lift, tease), asked for through RequestPeek and followed by one pump.
    private int _peekDir;
    private bool _peekBottom;
    private int _wantDir;
    private bool _wantBottom;
    private double _wantAmount;
    private bool _pumping;
    private int _hoverDir;
    private int _teaseId;
    private int _chevronDir;
    private bool _interacted;
    private bool _hasTurned;
    private bool _teased;
    private long _lastTease = long.MinValue;

    // What the last Update worked out, for the drawing layers.
    private bool _haveCurl;
    private PageCurlFold _fold;
    private double _bookX;
    private double _progress;
    private int _sheetFlapCount;
    private int _mirrorFlapCount;
    private int _curlDir;
    private double _leftOccupancy;
    private double _rightOccupancy;
    private (double X, double Left, double Right, double W, double H) _bookDrawn;

    private enum TurnMode
    {
        Rest,
        Drag,
        Settle,
        Arc
    }

    public PageCurlSurface()
    {
        Background = Brushes.Transparent;
        Focusable = false;
        ClipToBounds = false;

        for (var i = 0; i < PoolSize; i++)
        {
            _pool[i] = new PageCurlPage(i);
            Children.Add(_pool[i]);
        }
        // The book's shadow is a large blur under pages that change every frame; drawn once into a bitmap, it is
        // composited instead of blurred again on each frame of a turn.
        _book = new PageCurlFx(this, PageCurlLayer.Book) { ZIndex = -1, CacheMode = new BitmapCache() };
        _under = new PageCurlFx(this, PageCurlLayer.Under) { ZIndex = 2 };
        _mid = new PageCurlFx(this, PageCurlLayer.Mid) { ZIndex = 4 };
        _over = new PageCurlFx(this, PageCurlLayer.Over) { ZIndex = 6 };
        Children.Add(_book);
        Children.Add(_under);
        Children.Add(_mid);
        Children.Add(_over);
        _hint.ZIndex = 7;
        Children.Add(_hint);

        _arc = new MotionValue<double>(this, 0, MotionVectors.Double);
        _settle = new MotionValue<Point>(this, default, MotionVectors.Point);
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _chevron = new MotionValue<double>(this, 0, MotionVectors.Double);
        _hintOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _arc.Changed += _ => Update();
        _settle.Changed += _ => Update();
        _lift.Changed += _ => Update();
        _chevron.Changed += _ => _over.InvalidateVisual();
        _hintOpacity.Changed += value => _hint.Opacity = value;
    }

    /// <inheritdoc cref="HintAreaHeightProperty"/>
    public double HintAreaHeight
    {
        get => GetValue(HintAreaHeightProperty);
        set => SetValue(HintAreaHeightProperty, value);
    }

    /// <inheritdoc cref="PageShadowProperty"/>
    public BoxShadows PageShadow
    {
        get => GetValue(PageShadowProperty);
        set => SetValue(PageShadowProperty, value);
    }

    /// <inheritdoc cref="ChevronFillProperty"/>
    public IBrush? ChevronFill
    {
        get => GetValue(ChevronFillProperty);
        set => SetValue(ChevronFillProperty, value);
    }

    /// <inheritdoc cref="ChevronStrokeProperty"/>
    public IBrush? ChevronStroke
    {
        get => GetValue(ChevronStrokeProperty);
        set => SetValue(ChevronStrokeProperty, value);
    }

    /// <summary>The spread on show (the property moves only when a turn finishes).</summary>
    public int ShownSpread => _shown;

    /// <summary>True while a page is being dragged, settling after a release or following a tap's arc.</summary>
    public bool IsTurning => _mode != TurnMode.Rest;

    /// <summary>True while a drag is in progress.</summary>
    public bool IsDragging => _mode == TurnMode.Drag;

    /// <summary>The page size in px, derived from the width.</summary>
    public Size PageSize => new(_w, _h);

    /// <summary>How far the turning page has gone, 0 to 1 (0 at rest).</summary>
    public double TurnProgress => _haveCurl ? _progress : 0;

    /// <summary>Fraction of the page width the corner is peeking by (hover lift or tease).</summary>
    public double PeekAmount => _peekDir == 0 ? 0 : _lift.Value;

    /// <summary>The direction of the corner that is peeking, or 0.</summary>
    public int PeekDirection => _peekDir;

    /// <summary>True once the hint has been dismissed for good.</summary>
    public bool IsHintDismissed => _interacted;

    /// <summary>The chevron's opacity and side (+1 right page, -1 left page, 0 none).</summary>
    public (double Opacity, int Direction) Chevron => (_chevron.Value, _chevronDir);

    /// <summary>The sheets in use, with the page each shows (for tests and the automation tree).</summary>
    public IEnumerable<PageCurlPage> Sheets => _pool.Where(sheet => sheet.PageNumber != 0);

    /// <summary>Number of tease runs started so far.</summary>
    public int TeaseCount { get; private set; }

    internal void Attach(PageCurl owner)
    {
        _owner = owner;
        _hint.Content = HintContent(owner.Hint);
        _hint.IsVisible = owner.Hint is not null;
        _shown = Math.Clamp(owner.Spread, 0, LastSpread);
        InvalidateMeasure();
        Update();
    }

    internal void Detach()
    {
        _owner = null;
    }

    private int PageCount => _owner?.PageCount ?? 0;

    private int LastSpread => PageCurlGeometry.SpreadCount(PageCount) - 1;

    private bool CanTurn(int dir) => dir > 0 ? _shown < LastSpread : dir < 0 && _shown > 0;

    private bool IsFull => ArcMotion.IsFull(this);

    private double Offset(int spread) => PageCurlGeometry.BookOffset(spread, PageCount, _w);

    private Point CornerFor(int dir, bool bottom) => PageCurlGeometry.CornerOf(dir, bottom, _w, _h);

    // ---- Owner updates -------------------------------------------------------------------------------------------

    /// <summary>The pages changed: let go of every sheet and show the spread again.</summary>
    public void Reload()
    {
        AbortTurn();
        foreach (var sheet in _pool)
        {
            sheet.Release();
        }
        _shown = Math.Clamp(_shown, 0, LastSpread);
        Update();
        _owner?.RefreshReadouts();
    }

    /// <summary>The page aspect changed.</summary>
    public void ResizeBook()
    {
        InvalidateMeasure();
    }

    /// <summary>The hint changed.</summary>
    public void SetHint(object? hint)
    {
        _hint.Content = HintContent(hint);
        _hint.IsVisible = hint is not null && !_interacted;
        InvalidateMeasure();
    }

    /// <summary>A text hint is set small and muted with an arrow toward the corner; any other content is shown as it is.</summary>
    private static object? HintContent(object? hint)
    {
        if (hint is not string text)
        {
            return hint;
        }
        var label = new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        ArcTokens.Bind(label, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        var arrow = new ArcGlyph { Data = ArcIcons.ArrowUpRight, Size = 13, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        ArcTokens.Bind(arrow, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5,
            Children = { label, arrow }
        };
    }

    /// <summary>The Spread property was set from outside: turn there when it is one step away, else jump.</summary>
    public void GoTo(int spread)
    {
        spread = Math.Clamp(spread, 0, LastSpread);
        if (spread == _shown && _mode == TurnMode.Rest)
        {
            return;
        }
        if (_mode != TurnMode.Rest)
        {
            FinishNow();
        }
        if (spread == _shown)
        {
            return;
        }
        var step = spread - _shown;
        if (Math.Abs(step) == 1 && IsFull && this.IsAttachedToVisualTree() && _w > 0)
        {
            TurnBy(step, raise: false);
            return;
        }
        AbortTurn();
        _shown = spread;
        Update();
        _owner?.RefreshReadouts();
    }

    /// <summary>
    /// Turns one spread in <paramref name="dir"/> (+1 forward, -1 back), as a tap, a key or a button does. Returns false
    /// when there is no such spread. Under reduced motion the spread swaps with no curl.
    /// </summary>
    public bool TurnBy(int dir, bool raise = true)
    {
        if (_owner is null || !CanTurn(dir) || _mode == TurnMode.Drag)
        {
            return false;
        }
        if (_mode != TurnMode.Rest)
        {
            FinishNow();
            if (!CanTurn(dir))
            {
                return false;
            }
        }
        MarkInteracted();
        if (!IsFull || !this.IsAttachedToVisualTree() || _w <= 0)
        {
            AbortTurn();
            Commit(dir, raise);
            return true;
        }
        var residue = PeekResidue(dir);
        BeginTurn(dir, bottom: true);
        _residue = residue;
        _raise = raise;
        _mode = TurnMode.Arc;
        _arc.Jump(0);
        _ = RunArc(_turnId);
        return true;
    }

    private async Task RunArc(int id)
    {
        var done = await _arc.AnimateTo(1, s_arcSpring);
        if (done && id == _turnId && _mode == TurnMode.Arc)
        {
            Commit(_dir, _raise);
        }
    }

    // ---- Turn state ----------------------------------------------------------------------------------------------

    private Vector PeekResidue(int dir)
    {
        if (_mode == TurnMode.Rest && _peekDir == dir && _lift.Value > 0.002)
        {
            var peek = PageCurlGeometry.PeekPoint(default, dir, _peekBottom, _lift.Value, _w);
            return new Vector(peek.X, peek.Y);
        }
        return default;
    }

    private void BeginTurn(int dir, bool bottom)
    {
        CancelPeek();
        _turnId++;
        _dir = dir;
        _bottom = bottom;
        _corner = CornerFor(dir, bottom);
        _residue = default;
    }

    private void CancelPeek()
    {
        _teaseId++;
        _wantDir = 0;
        _peekDir = 0;
        _hoverDir = 0;
        _lift.Jump(0);
    }

    /// <summary>Stops whatever is turning and lays the page flat again.</summary>
    private void AbortTurn()
    {
        _turnId++;
        _settle.Stop();
        _arc.Stop();
        _mode = TurnMode.Rest;
        _dir = 0;
        _pressed = false;
        _dragging = false;
        ReleaseCapture();
    }

    /// <summary>Ends a settling or arcing turn at once, the way it was going.</summary>
    private void FinishNow()
    {
        var dir = _dir;
        var complete = _mode == TurnMode.Arc || (_mode == TurnMode.Settle && _settleComplete);
        var raise = _mode != TurnMode.Arc || _raise;
        AbortTurn();
        if (complete)
        {
            Commit(dir, raise);
        }
        else
        {
            Update();
        }
    }

    private void Commit(int dir, bool raise)
    {
        _turnId++;
        _mode = TurnMode.Rest;
        _dir = 0;
        _arc.Jump(0);
        _hasTurned = true;
        _shown = Math.Clamp(_shown + dir, 0, LastSpread);
        Update();
        _owner?.CommitSpread(_shown, raise);
    }

    private void MarkInteracted()
    {
        if (_interacted)
        {
            return;
        }
        _interacted = true;
        _teaseId++;
        if (_hint.IsVisible)
        {
            _ = FadeHint();
        }
    }

    private async Task FadeHint()
    {
        if (await _hintOpacity.AnimateTo(0, s_fade) && _interacted)
        {
            _hint.IsVisible = false;
        }
    }

    // ---- Layout --------------------------------------------------------------------------------------------------

    private double HintArea => _owner?.Hint is null ? 0 : HintAreaHeight;

    private double AspectOf() => _owner is { PageAspect: > 0.05 } owner ? owner.PageAspect : 0.72;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        var pageWidth = width / 2;
        var pageHeight = pageWidth / AspectOf();
        var total = new Size(width, pageHeight + HintArea);
        foreach (var child in Children)
        {
            child.Measure(child is PageCurlPage ? new Size(pageWidth, pageHeight) : child == _hint ? Size.Infinity : total);
        }
        return total;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pageWidth = finalSize.Width / 2;
        var pageHeight = pageWidth / AspectOf();
        var changed = Math.Abs(pageWidth - _w) > 0.01 || Math.Abs(pageHeight - _h) > 0.01;
        _w = pageWidth;
        _h = pageHeight;
        _bookLeft = 0;
        foreach (var child in Children)
        {
            if (child is PageCurlPage)
            {
                child.Arrange(new Rect(0, 0, pageWidth, pageHeight));
            }
            else if (child == _hint)
            {
                child.Arrange(new Rect(0, pageHeight + 10, child.DesiredSize.Width, child.DesiredSize.Height));
            }
            else
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        if (changed)
        {
            Update();
        }
        return finalSize;
    }

    // ---- Frame ---------------------------------------------------------------------------------------------------

    private bool TryCurl(out int dir, out bool bottom, out Point corner, out Point pointer)
    {
        dir = 0;
        bottom = false;
        corner = default;
        pointer = default;
        switch (_mode)
        {
            case TurnMode.Rest:
                if (_peekDir == 0 || _lift.Value < 0.002 || !CanTurn(_peekDir))
                {
                    return false;
                }
                dir = _peekDir;
                bottom = _peekBottom;
                corner = CornerFor(dir, bottom);
                pointer = PageCurlGeometry.PeekPoint(corner, dir, bottom, _lift.Value, _w);
                return true;
            case TurnMode.Drag:
                dir = _dir;
                bottom = _bottom;
                corner = _corner;
                pointer = _pointer;
                return true;
            case TurnMode.Settle:
                dir = _dir;
                bottom = _bottom;
                corner = _corner;
                pointer = _settle.Value;
                return true;
            default:
            {
                dir = _dir;
                bottom = _bottom;
                corner = _corner;
                var t = _arc.Value;
                var arc = PageCurlGeometry.ArcPoint(corner, bottom, t, _w, _h);
                pointer = new Point(arc.X + _residue.X * (1 - t), arc.Y + _residue.Y * (1 - t));
                return true;
            }
        }
    }

    /// <summary>Works out the sheets and their placement for this frame; render-only, so nothing is laid out.</summary>
    private void Update()
    {
        if (_owner is null || _w <= 0 || _h <= 0)
        {
            return;
        }
        var n = PageCount;
        _haveCurl = TryCurl(out var dir, out var bottom, out var corner, out var pointer);
        _curlDir = dir;
        _fold = default;
        _progress = 0;
        _sheetFlapCount = 0;
        _mirrorFlapCount = 0;
        if (_haveCurl)
        {
            _fold = PageCurlGeometry.Fold(corner, pointer, _w);
            _progress = PageCurlGeometry.Progress(dir, corner, _fold.IsValid ? _fold.Pointer : corner, _w);
        }
        var from = Offset(_shown);
        var to = _haveCurl ? Offset(_shown + dir) : from;
        _bookX = _bookLeft + from + (to - from) * _progress;
        var toSpread = _haveCurl ? _shown + dir : _shown;
        _leftOccupancy = Lerp(PageCurlGeometry.LeftPage(_shown, n) != 0 ? 1 : 0, PageCurlGeometry.LeftPage(toSpread, n) != 0 ? 1 : 0, _progress);
        _rightOccupancy = Lerp(PageCurlGeometry.RightPage(_shown, n) != 0 ? 1 : 0, PageCurlGeometry.RightPage(toSpread, n) != 0 ? 1 : 0, _progress);

        var translate = Matrix.CreateTranslation(_bookX, 0);
        if (!_haveCurl)
        {
            AssignSheets(PageCurlGeometry.LeftPage(_shown, n), PageCurlGeometry.RightPage(_shown, n), 0, 0);
            Place(PageCurlGeometry.LeftPage(_shown, n), Matrix.CreateTranslation(_bookX, 0), null, 1, true, 1);
            Place(PageCurlGeometry.RightPage(_shown, n), Matrix.CreateTranslation(_bookX + _w, 0), null, 1, true, 1);
            FreeUnused();
        }
        else
        {
            var sheetOrigin = new Point(dir > 0 ? _w : 0, 0);
            var otherOrigin = new Point(dir > 0 ? 0 : _w, 0);
            var restOrigin = otherOrigin;
            int stay;
            int front;
            int back;
            int under;
            if (dir > 0)
            {
                stay = PageCurlGeometry.LeftPage(_shown, n);
                front = PageCurlGeometry.RightPage(_shown, n);
                back = 2 * _shown + 2 <= n ? 2 * _shown + 2 : 0;
                under = PageCurlGeometry.RightPage(_shown + 1, n);
            }
            else
            {
                stay = PageCurlGeometry.RightPage(_shown, n);
                front = PageCurlGeometry.LeftPage(_shown, n);
                back = _shown >= 1 ? 2 * _shown - 1 : 0;
                under = PageCurlGeometry.LeftPage(_shown - 1, n);
            }
            AssignSheets(stay, front, back, under);

            Place(stay, Matrix.CreateTranslation(_bookX + otherOrigin.X, 0), null, 1, false, 1);
            Place(under, Matrix.CreateTranslation(_bookX + sheetOrigin.X, 0), null, 0, false, 1);

            var sheetRect = new Rect(sheetOrigin.X, 0, _w, _h);
            Geometry? frontClip = null;
            Geometry? flapClip = null;
            var flapMatrix = Matrix.Identity;
            var flapOpacity = 0.0;
            if (_fold.IsValid)
            {
                var keep = PageCurlGeometry.ClipToSide(sheetRect, _fold, flap: false, _scratch);
                frontClip = keep >= 3 ? Polygon(_scratch, keep, -sheetOrigin.X, 0) : EmptyClip;
                _sheetFlapCount = PageCurlGeometry.ClipToSide(sheetRect, _fold, flap: true, _sheetFlap);
                if (_sheetFlapCount >= 3)
                {
                    for (var i = 0; i < _sheetFlapCount; i++)
                    {
                        _scratch[i] = PageCurlGeometry.BackLocal(_sheetFlap[i], _w, restOrigin);
                        _mirrorFlap[i] = PageCurlGeometry.Reflect(_fold, _sheetFlap[i]);
                    }
                    _mirrorFlapCount = _sheetFlapCount;
                    flapClip = Polygon(_scratch, _sheetFlapCount, 0, 0);
                    flapMatrix = PageCurlGeometry.FlapMatrix(_fold, _w, restOrigin) * translate;
                    flapOpacity = 1;
                }
            }
            Place(front, Matrix.CreateTranslation(_bookX + sheetOrigin.X, 0), frontClip, 3, false, 1);
            Place(back, flapMatrix, flapClip, 5, false, flapOpacity);
            FreeUnused();
        }

        PlaceHint();
        var book = (_bookX, _leftOccupancy, _rightOccupancy, _w, _h);
        if (book != _bookDrawn)
        {
            _bookDrawn = book;
            _book.InvalidateVisual();
        }
        _under.InvalidateVisual();
        _mid.InvalidateVisual();
        _over.InvalidateVisual();
    }

    private static Geometry EmptyClip { get; } = new RectangleGeometry(new Rect(0, 0, 0, 0));

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Lists the pages that need a sheet this frame and gives each one, freeing sheets nobody needs.</summary>
    private void AssignSheets(int a, int b, int c, int d)
    {
        var count = 0;
        Span<int> pages = [a, b, c, d];
        foreach (var page in pages)
        {
            if (page != 0)
            {
                _wanted[count++] = page;
            }
        }
        for (var i = count; i < PoolSize; i++)
        {
            _wanted[i] = 0;
        }
        foreach (var sheet in _pool)
        {
            if (sheet.PageNumber != 0 && Array.IndexOf(_wanted, sheet.PageNumber) < 0)
            {
                sheet.Release();
            }
        }
        for (var i = 0; i < count; i++)
        {
            if (SheetOf(_wanted[i]) is null)
            {
                var free = _pool.First(sheet => sheet.PageNumber == 0);
                free.Show(_wanted[i], _owner!.PageAt(_wanted[i]), _owner.PageTemplate);
            }
        }
    }

    private PageCurlPage? SheetOf(int page) => page == 0 ? null : _pool.FirstOrDefault(sheet => sheet.PageNumber == page);

    private void Place(int page, Matrix matrix, Geometry? clip, int z, bool resting, double opacity)
    {
        if (SheetOf(page) is not { } sheet)
        {
            return;
        }
        // Resting pages draw as vectors (crisp text); every sheet of a turn, the still ones included, draws from its
        // cache, because the turning sheet dirties the whole spread each frame.
        sheet.SetCache(_haveCurl ? _pageCache : null);
        sheet.IsVisible = true;
        sheet.ZIndex = z;
        sheet.Placement = matrix;
        sheet.Clip = clip;
        sheet.Opacity = opacity;
        var view = resting && page != 0 ? AccessibilityView.Content : AccessibilityView.Raw;
        if (AutomationProperties.GetAccessibilityView(sheet) != view)
        {
            AutomationProperties.SetAccessibilityView(sheet, view);
        }
    }

    private void FreeUnused()
    {
        foreach (var sheet in _pool)
        {
            if (sheet.PageNumber == 0 && sheet.IsVisible)
            {
                sheet.IsVisible = false;
            }
        }
    }

    private void PlaceHint()
    {
        if (!_hint.IsVisible)
        {
            return;
        }
        var n = PageCount;
        var right = PageCurlGeometry.RightPage(_shown, n) != 0;
        var x = right ? _bookX + 2 * _w - _hint.DesiredSize.Width : _bookX;
        _hint.RenderTransform = new TranslateTransform(x, 0);
    }

    private static StreamGeometry Polygon(Point[] points, int count, double dx, double dy)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(points[0].X + dx, points[0].Y + dy), true);
            for (var i = 1; i < count; i++)
            {
                context.LineTo(new Point(points[i].X + dx, points[i].Y + dy));
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    // ---- Drawing -------------------------------------------------------------------------------------------------

    public void DrawLayer(PageCurlLayer layer, DrawingContext context)
    {
        if (_owner is null || _w <= 0)
        {
            return;
        }
        using (context.PushTransform(Matrix.CreateTranslation(_bookX, 0)))
        {
            switch (layer)
            {
                case PageCurlLayer.Book:
                    DrawBook(context);
                    break;
                case PageCurlLayer.Under:
                    DrawUnder(context);
                    break;
                case PageCurlLayer.Mid:
                    DrawMid(context);
                    break;
                default:
                    DrawOver(context);
                    break;
            }
        }
    }

    private void DrawBook(DrawingContext context)
    {
        var shadow = PageShadow;
        var radius = new CornerRadius(3);
        DrawPageShadow(context, new Rect(0, 0, _w, _h), _leftOccupancy, radius, shadow);
        DrawPageShadow(context, new Rect(_w, 0, _w, _h), _rightOccupancy, radius, shadow);
    }

    private static void DrawPageShadow(DrawingContext context, Rect rect, double occupancy, CornerRadius radius, BoxShadows shadow)
    {
        if (occupancy <= 0.01)
        {
            return;
        }
        if (occupancy >= 0.99)
        {
            context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(rect, radius), shadow);
            return;
        }
        using (context.PushOpacity(occupancy))
        {
            context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(rect, radius), shadow);
        }
    }

    private void DrawUnder(DrawingContext context)
    {
        // The spine: both pages darken a little toward the binding while the book is still.
        var spine = _w * 0.1;
        var still = 1 - _progress;
        if (_leftOccupancy > 0.99 && still > 0.99)
        {
            context.DrawRectangle(s_spineLeft, null, new Rect(_w - spine, 0, spine, _h));
        }
        if (_rightOccupancy > 0.99 && still > 0.99)
        {
            context.DrawRectangle(s_spineRight, null, new Rect(_w, 0, spine, _h));
        }

        // The page the sheet uncovers darkens along the fold, most where the flap is close to it.
        if (_haveCurl && _fold.IsValid && _sheetFlapCount >= 3)
        {
            // It grows while the page lifts and fades over the last third, so the page lands without a shade on it.
            var reach = _w * (0.22 + 0.25 * Math.Min(1, _progress * 2));
            var fade = Math.Clamp((1 - _progress) * 3, 0, 1);
            if (fade > 0.01)
            {
                context.DrawGeometry(AlongFold(s_foldShadeStops, -reach, fade), null, Polygon(_sheetFlap, _sheetFlapCount, 0, 0));
            }
        }
    }

    private void DrawMid(DrawingContext context)
    {
        if (!_haveCurl || _mirrorFlapCount < 3)
        {
            return;
        }
        // The flap's shadow always falls downward, a little farther the more the page has lifted.
        var lift = 0.45 + 0.9 * Math.Min(1, _progress * 2.5);
        using (context.PushClip(new Rect(0, 0, 2 * _w, _h)))
        {
            foreach (var offset in s_flapCastOffsets)
            {
                context.DrawGeometry(s_flapCast, null, Polygon(_mirrorFlap, _mirrorFlapCount, 0.25 * offset * lift, offset * lift));
            }
        }
    }

    private void DrawOver(DrawingContext context)
    {
        if (_haveCurl && _fold.IsValid && _mirrorFlapCount >= 3)
        {
            // The lit curl: a shade right at the crease, a bright band where the paper bends toward the light, then the
            // flat back of the page.
            var reach = Math.Clamp(Distance(_fold.Mid, _fold.Pointer) * 0.7, 18, 0.32 * _w);
            context.DrawGeometry(AlongFold(s_curlStops, reach), null, Polygon(_mirrorFlap, _mirrorFlapCount, 0, 0));
        }

        if (_chevronDir != 0 && _chevron.Value > 0.01 && _mode == TurnMode.Rest && !_haveCurl)
        {
            DrawChevron(context, _chevronDir, _chevron.Value);
        }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>A gradient that starts on the fold line and runs <paramref name="reach"/> px along its normal (negative: toward the uncovered page).</summary>
    private IImmutableBrush AlongFold(ImmutableGradientStop[] stops, double reach, double opacity = 1)
    {
        var start = _fold.Mid;
        var end = new Point(start.X + _fold.Normal.X * reach, start.Y + _fold.Normal.Y * reach);
        return new ImmutableLinearGradientBrush(stops, opacity,
            startPoint: new RelativePoint(start, RelativeUnit.Absolute),
            endPoint: new RelativePoint(end, RelativeUnit.Absolute));
    }

    private void DrawChevron(DrawingContext context, int dir, double opacity)
    {
        var center = new Point(dir > 0 ? 2 * _w - 24 : 24, _h / 2);
        using (context.PushOpacity(opacity))
        {
            var fill = ChevronFill;
            var stroke = ChevronStroke;
            context.DrawEllipse(fill, null, center, 15, 15);
            var pen = new Pen(stroke, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var s = dir > 0 ? 1 : -1;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(center.X - 3 * s, center.Y - 6), false);
                g.LineTo(new Point(center.X + 3 * s, center.Y));
                g.LineTo(new Point(center.X - 3 * s, center.Y + 6));
                g.EndFigure(false);
            }
            context.DrawGeometry(null, pen, geometry);
        }
    }

    // ---- Pointer -------------------------------------------------------------------------------------------------

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse && _owner is { Tease: true } && !_hasTurned && !_interacted && _shown == 0
            && IsFull && Environment.TickCount64 - _lastTease >= 2500 && _mode == TurnMode.Rest)
        {
            PlayTease(0.125);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pressed)
        {
            return;
        }
        _hoverDir = 0;
        SetChevron(0);
        if (_mode == TurnMode.Rest)
        {
            RequestPeek(0, false, 0);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _pressed)
        {
            return;
        }
        var pos = e.GetPosition(this);
        if (_mode == TurnMode.Settle)
        {
            // Grabbing a page that is still settling catches it where it is.
            MarkInteracted();
            _turnId++;
            var at = _settle.Value;
            _settle.Stop();
            _mode = TurnMode.Drag;
            _pressed = true;
            _dragging = true;
            _swapOnly = false;
            _dragBase = at;
            _dragStart = pos;
            _pointer = at;
            _velocity.Reset();
            Capture(e.Pointer);
            e.Handled = true;
            return;
        }
        if (_mode != TurnMode.Rest)
        {
            return;
        }
        var bx = pos.X - _bookX;
        var dir = 0;
        if (pos.Y >= 0 && pos.Y <= _h)
        {
            if (bx >= _w && bx <= 2 * _w && CanTurn(1))
            {
                dir = 1;
            }
            else if (bx >= 0 && bx < _w && CanTurn(-1))
            {
                dir = -1;
            }
        }
        if (dir == 0)
        {
            return;
        }
        _pressed = true;
        _dragging = false;
        _swapOnly = false;
        _pressDir = dir;
        _pressBottom = pos.Y > _h / 2;
        _pressStart = pos;
        _velocity.Reset();
        _velocity.AddSample(pos, TimeSpan.FromMilliseconds(e.Timestamp));
        if (e.Pointer.Type == PointerType.Mouse)
        {
            Capture(e.Pointer);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        if (_pressed)
        {
            DragMove(e, pos);
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse)
        {
            Hover(pos);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        var pos = e.GetPosition(this);
        var time = TimeSpan.FromMilliseconds(e.Timestamp);
        _pressed = false;
        ReleaseCapture();
        if (!_dragging)
        {
            TurnBy(_pressDir);
            return;
        }
        _dragging = false;
        if (_swapOnly)
        {
            var moved = _pressDir > 0 ? _pressStart.X - pos.X : pos.X - _pressStart.X;
            if (moved >= 48)
            {
                MarkInteracted();
                Commit(_pressDir, true);
            }
            return;
        }
        EndDrag(time);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressed && _capturedPointer == e.Pointer)
        {
            _capturedPointer = null;
            _pressed = false;
            if (_dragging)
            {
                _dragging = false;
                if (!_swapOnly)
                {
                    EndDrag(_velocity.LastSampleTime ?? TimeSpan.Zero);
                }
            }
        }
    }

    private void Capture(IPointer pointer)
    {
        _capturedPointer = pointer;
        pointer.Capture(this);
    }

    private void ReleaseCapture()
    {
        var pointer = _capturedPointer;
        _capturedPointer = null;
        if (pointer?.Captured == this)
        {
            pointer.Capture(null);
        }
    }

    private void DragMove(PointerEventArgs e, Point pos)
    {
        _velocity.AddSample(pos, TimeSpan.FromMilliseconds(e.Timestamp));
        if (!_dragging)
        {
            var dx = pos.X - _pressStart.X;
            var dy = pos.Y - _pressStart.Y;
            var touch = e.Pointer.Type != PointerType.Mouse;
            if (Math.Sqrt(dx * dx + dy * dy) < (touch ? TouchDragThreshold : DragThreshold))
            {
                return;
            }
            if (touch && Math.Abs(dy) > Math.Abs(dx))
            {
                // A vertical swipe scrolls the page behind the book.
                _pressed = false;
                return;
            }
            MarkInteracted();
            _dragging = true;
            if (touch)
            {
                Capture(e.Pointer);
            }
            if (!IsFull)
            {
                _swapOnly = true;
                return;
            }
            var residue = PeekResidue(_pressDir);
            BeginTurn(_pressDir, _pressBottom);
            _mode = TurnMode.Drag;
            _dragBase = new Point(_corner.X + residue.X, _corner.Y + residue.Y);
            _dragStart = pos;
            _pointer = _dragBase;
            Update();
            return;
        }
        if (_swapOnly)
        {
            return;
        }
        // The corner follows the finger from where it was grabbed.
        var targetX = _dragBase.X + pos.X - _dragStart.X;
        var targetY = _dragBase.Y + pos.Y - _dragStart.Y;
        var x = _dir > 0 ? Math.Min(targetX, _corner.X) : Math.Max(targetX, _corner.X);
        _pointer = new Point(x, Math.Clamp(targetY, -0.25 * _h, 1.25 * _h));
        Update();
    }

    private void EndDrag(TimeSpan now)
    {
        var fold = PageCurlGeometry.Fold(_corner, _pointer, _w);
        var from = fold.IsValid ? fold.Pointer : _corner;
        var progress = PageCurlGeometry.Progress(_dir, _corner, from, _w);
        var v = _velocity.GetVelocity(now);
        var along = _dir > 0 ? -v.X : v.X;
        var complete = along > FlickSpeed || (progress > 0.5 && along > -FlickSpeed);
        _settleComplete = complete;
        _mode = TurnMode.Settle;
        _settle.Jump(from);
        _ = RunSettle(_turnId, complete, new Point(v.X, v.Y));
    }

    private async Task RunSettle(int id, bool complete, Point velocity)
    {
        var target = complete ? PageCurlGeometry.FarCorner(_corner, _w) : _corner;
        var done = await _settle.AnimateTo(target, s_settleSpring, velocity);
        if (!done || id != _turnId || _mode != TurnMode.Settle)
        {
            return;
        }
        if (complete)
        {
            Commit(_dir, true);
        }
        else
        {
            AbortTurn();
            Update();
        }
    }

    // ---- Hover, peeks and the tease -------------------------------------------------------------------------------

    private void Hover(Point pos)
    {
        if (_mode != TurnMode.Rest || _owner is null)
        {
            return;
        }
        var bx = pos.X - _bookX;
        var dir = 0;
        if (pos.Y >= 0 && pos.Y <= _h)
        {
            if (bx >= _w && bx <= 2 * _w && CanTurn(1))
            {
                dir = 1;
            }
            else if (bx >= 0 && bx < _w && CanTurn(-1))
            {
                dir = -1;
            }
        }
        SetChevron(dir);
        if (!IsFull)
        {
            return;
        }
        var hot = 0;
        var bottom = false;
        if (dir != 0)
        {
            var corner = CornerFor(dir, pos.Y > _h / 2);
            if (Math.Abs(corner.X - bx) < HoverZone * _w && Math.Abs(corner.Y - pos.Y) < HoverZone * _w)
            {
                hot = dir;
                bottom = pos.Y > _h / 2;
            }
        }
        _hoverDir = hot;
        if (hot != 0)
        {
            RequestPeek(hot, bottom, HoverLift);
        }
        else if (_peekDir != 0 && !_teasing)
        {
            RequestPeek(0, false, 0);
        }
    }

    private bool _teasing;

    private void SetChevron(int dir)
    {
        if (dir == _chevronDir && (dir != 0 || _chevron.Value <= 0.001))
        {
            if (dir != 0 && _chevron.Target < 0.99)
            {
                _ = _chevron.AnimateTo(1, s_fade);
            }
            return;
        }
        if (dir != 0)
        {
            _chevronDir = dir;
            _ = _chevron.AnimateTo(1, s_fade);
        }
        else
        {
            _ = _chevron.AnimateTo(0, s_fade);
        }
    }

    private void RequestPeek(int dir, bool bottom, double amount)
    {
        _wantDir = dir;
        _wantBottom = bottom;
        _wantAmount = amount;
        if (!_pumping)
        {
            _ = PumpPeek();
        }
    }

    private async Task PumpPeek()
    {
        _pumping = true;
        try
        {
            for (var guard = 0; guard < 16; guard++)
            {
                if (_mode != TurnMode.Rest)
                {
                    return;
                }
                var dir = _wantDir;
                var bottom = _wantBottom;
                var amount = _wantAmount;
                if (_peekDir != 0 && (dir != _peekDir || bottom != _peekBottom || amount <= 0))
                {
                    // One corner lies flat before another lifts.
                    if (_lift.Value > 0.002)
                    {
                        await _lift.AnimateTo(0, s_peekSpring);
                        continue;
                    }
                    _peekDir = 0;
                    Update();
                }
                if (_peekDir == 0 && dir != 0 && amount > 0 && CanTurn(dir))
                {
                    _peekDir = dir;
                    _peekBottom = bottom;
                }
                if (_peekDir != 0 && dir == _peekDir && bottom == _peekBottom && amount > 0)
                {
                    if (Math.Abs(_lift.Value - amount) > 0.002)
                    {
                        await _lift.AnimateTo(amount, s_peekSpring);
                        continue;
                    }
                }
                return;
            }
        }
        finally
        {
            _pumping = false;
        }
    }

    /// <summary>
    /// Lifts the cover's lower corner by <paramref name="amount"/> of a page width, holds, and lays it back: the
    /// invitation to turn the page. Does nothing under reduced motion or when the cover is not showing.
    /// </summary>
    public async void PlayTease(double amount)
    {
        if (!IsFull || _shown != 0 || _mode != TurnMode.Rest || !CanTurn(1))
        {
            return;
        }
        TeaseCount++;
        _lastTease = Environment.TickCount64;
        var id = ++_teaseId;
        _teasing = true;
        RequestPeek(1, true, amount);
        await Task.Delay(TimeSpan.FromMilliseconds(950));
        if (id == _teaseId)
        {
            _teasing = false;
            if (_hoverDir == 0)
            {
                RequestPeek(0, false, 0);
            }
        }
    }

    // ---- Visibility ----------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _visibility?.Dispose();
        _visibility = new ViewportVisibility(this, 0.6);
        _visibility.VisibilityChanged += OnVisibilityChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        _teaseId++;
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (visible && !_teased && _owner is { Tease: true } && !_interacted && !_hasTurned)
        {
            _teased = true;
            PlayTease(0.25);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HintAreaHeightProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == PageShadowProperty)
        {
            _book.InvalidateVisual();
        }
        else if (change.Property == ChevronFillProperty || change.Property == ChevronStrokeProperty)
        {
            _over.InvalidateVisual();
        }
        else if (change.Property == ArcMotion.LevelProperty && !IsFull && _mode != TurnMode.Rest)
        {
            FinishNow();
        }
    }
}
