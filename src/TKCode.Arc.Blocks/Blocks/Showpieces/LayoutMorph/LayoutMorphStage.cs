using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The stage of a <see cref="LayoutMorph"/>: hosts one <see cref="LayoutMorphCard"/> per item and moves them. Every card
/// has its own progress spring from where it is to where the layout wants it (Arc Layout morph, design reference:
/// k 100 / c 20, started 330ms × distance / farthest distance after the change, lifted 0.1 × the distance and leaning
/// into the motion while it flies), caption layers that fade out at once and in late, a hover lift spring and a dim
/// spring for the open detail. The carousel and helix turn with a front position that drags, flicks and snaps.
/// </summary>
/// <remarks>
/// <para>
/// Code-driven motion: 24 cards each follow a spring between two poses that are recomputed every frame (the ring turns
/// under them, the list scrolls, the world recedes) and start after their own stagger delay; the projective
/// <see cref="Visual.RenderTransform"/> of each card is built from the interpolated pose, and their draw order is
/// sorted by depth every frame. A ControlTheme transition can express neither per-item delays from a computed origin
/// nor a target that moves while it is chased. Everything advances in the stage's one frame loop (ShowpieceStage).
/// </para>
/// <para>
/// Rendering cost: pictures are drawn from copies at the size they are shown (<see cref="LayoutMorphPictures"/>). On a GPU
/// compositor (<see cref="ShowpieceRenderer"/>) a carousel card whose matrix changes while its drawing does not (the ring
/// turning or dragged, the world receding behind an opened card) is drawn from a bitmap cache: under a perspective matrix
/// its shadow, rounded clips, text and border would otherwise be rasterized again every frame (<see cref="CachesCards"/>
/// says why the other layouts keep vectors). A card whose drawing changes from frame to frame (a size morph during a
/// flight, a lift) stays vectors, since its bitmap would be rendered again every frame. The bitmaps go 500 ms after the
/// stage rests (<see cref="ShowpieceCacheHold"/>), when copies larger than needed shrink.
/// </para>
/// </remarks>
internal sealed class LayoutMorphStage : ShowpieceStage
{
    private const double ProgressRestDelta = 2e-4;
    private const double ProgressRestSpeed = 2e-3;

    private readonly LayoutMorph _owner;
    private readonly List<CardState> _cards = new();
    private readonly List<CardState> _leaving = new();
    private LayoutMorphPose[] _targets = Array.Empty<LayoutMorphPose>();
    private double[] _aspects = Array.Empty<double>();
    private Point[] _starts = Array.Empty<Point>();
    private double[] _delays = Array.Empty<double>();
    private int[] _order = Array.Empty<int>();
    private double[] _depths = Array.Empty<double>();

    private LayoutMorphMode _mode = LayoutMorphMode.Grid;
    private LayoutMorphMetrics _metrics = new(1080, 600);
    private bool _hasMetrics;
    private LayoutMorphCamera _camera;
    private double _scrollRange;
    private ShowpieceSpringChannel _front = new(0, 1e-4, 1e-3);
    private ShowpieceSpringChannel _scroll = new(0, 0.05, 0.5);
    private ShowpieceSpringChannel _recede = new(0, 0.05, 0.5);
    private double _carouselFront = -1;
    private double _helixFront = -1;
    private double _scrollRaw;
    private double _frontRaw;
    private DragKind _drag;
    private bool _targetsDirty = true;

    private int _openIndex = -1;
    private LayoutMorphPose _openPose;
    private LayoutMorphSheetLayout _openSheet;
    private int _hoverIndex = -1;
    private int _focusIndex = -1;
    private int _tabStop = -1;
    private int _frontIndex = -1;
    private int _subjectIndex = -2;
    private bool _themeDirty = true;
    private bool _palettePosted;

    private readonly LayoutMorphPictures _pictures = new();
    private readonly BitmapCache _cardCache = new();
    private ShowpieceCacheHold? _cacheHold;
    private bool _gpu;
    private bool? _cardCacheOverride;
    private bool _cacheMoving;
    private bool _shrinkPictures;
    private TopLevel? _scalingSource;

    private enum DragKind
    {
        None,
        Ring,
        Scroll
    }

    public LayoutMorphStage(LayoutMorph owner)
    {
        _owner = owner;
        Focusable = false;
        ClipToBounds = true;
        WheelMode = ShowpieceWheelMode.PassThrough;
        RoleDescription = "gallery";
    }

    internal LayoutMorphPalette Palette { get; } = new();

    internal new ShowpieceTextCache TextCache => base.TextCache;

    internal IReadOnlyList<LayoutMorphCard> Cards => _cards.Select(c => c.Card).ToList();

    /// <summary>Removed cards still fading out (tests).</summary>
    internal IReadOnlyList<LayoutMorphCard> LeavingCards => _leaving.Select(c => c.Card).ToList();

    internal LayoutMorphCard CardAt(int index) => _cards[index].Card;

    /// <summary>Card <paramref name="index"/>'s drawn pose now (camera folded in) with its flight lift and lean (tests).</summary>
    internal (LayoutMorphPose Pose, double FlightLift, double FlightTilt) PoseOf(int index) =>
        (_cards[index].Current, _cards[index].FlightLift, _cards[index].FlightTilt);

    internal int Count => _cards.Count;

    /// <summary>The downscaled pictures the cards draw.</summary>
    internal LayoutMorphPictures Pictures => _pictures;

    /// <summary>The bitmap cache moving cards share; its scale follows the largest magnification of a cached card.</summary>
    internal BitmapCache CardCache => _cardCache;

    /// <summary>Tests and measurements: true or false forces the card bitmaps on or off; null follows the renderer (GPU only).</summary>
    internal bool? CardCacheOverride
    {
        get => _cardCacheOverride;
        set
        {
            _cardCacheOverride = value;
            if (!AllowCardCache)
            {
                DropCardCaches();
            }
        }
    }

    /// <summary>Moving cards may draw from bitmaps: a GPU compositor (or the override) and full motion.</summary>
    internal bool AllowCardCache => (_cardCacheOverride ?? _gpu) && Animates;

    /// <summary>
    /// The layouts whose moving cards are drawn from bitmaps: the carousel (its turns and drags halve the frame on a GPU). In the
    /// helix the bitmaps gained under 10% of a frame and cost more on the slowest frames: cards keep turning their backs and the
    /// focused card lifts, so bitmaps are rendered again every frame; the flat layouts move cards without perspective.
    /// </summary>
    internal static bool CachesCards(LayoutMorphMode mode) => mode == LayoutMorphMode.Carousel;

    /// <summary>The stage moves, or rested less than the hold ago: moving cards keep their bitmaps (tests).</summary>
    internal bool IsCacheMoving => _cacheMoving;

    /// <summary>Whether card <paramref name="index"/> draws from the bitmap cache (tests).</summary>
    internal bool IsCardCached(int index) => _cards[index].Cached;

    internal LayoutMorphMode Mode => _mode;

    internal LayoutMorphMetrics Metrics => _metrics;

    internal double Front => _front.Value;

    internal double ScrollOffset => _scroll.Value;

    internal double ScrollRange => _scrollRange;

    internal double Recede => _recede.Value;

    internal int OpenIndex => _openIndex;

    internal int HoverIndex => _hoverIndex;

    internal int TabStopIndex => _tabStop;

    internal int FrontIndex => _frontIndex;

    internal bool IsScrollable => _mode == LayoutMorphMode.List && _scrollRange > 0.5;

    /// <summary>Any card still on its way (tests and the idle check).</summary>
    internal bool IsMoving =>
        _front.IsMoving || _scroll.IsMoving || _recede.IsMoving || _leaving.Count > 0 || _cards.Any(c => c.IsMoving);

    private bool Animates => MotionLevel == ArcMotionLevel.Full && this.IsAttachedToVisualTree();

    // Items ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Replaces the cards, keeping the card (and its pose, focus and state) of every item that stays, matched by
    /// reference or <see cref="LayoutMorphItem.Key"/>. With full motion, a removed card fades out where it is and a new
    /// card flies in from its nearest neighbour in data order while it fades in; the others move to their new places.
    /// </summary>
    internal void SetItems(IReadOnlyList<LayoutMorphItem> items)
    {
        var old = _cards.ToList();
        var openItem = _openIndex >= 0 && _openIndex < old.Count ? old[_openIndex].Card.Item : null;
        var focusItem = _focusIndex >= 0 && _focusIndex < old.Count ? old[_focusIndex].Card.Item : null;
        var tabItem = _tabStop >= 0 && _tabStop < old.Count ? old[_tabStop].Card.Item : null;
        var hoverItem = _hoverIndex >= 0 && _hoverIndex < old.Count ? old[_hoverIndex].Card.Item : null;
        var focusedCard = old.FirstOrDefault(c => c.Card.IsFocused);
        var focusSlot = focusedCard is null ? -1 : old.IndexOf(focusedCard);
        var focusWasKeyboard = focusedCard?.Card.IsKeyboardFocused == true;
        var animate = old.Count > 0 && Animates;
        _cards.Clear();
        var reused = new HashSet<CardState>();
        foreach (var item in items)
        {
            var match = FindMatch(old, item, reused) ?? FindMatch(_leaving, item, reused);
            if (match is not null)
            {
                reused.Add(match);
                if (_leaving.Remove(match))
                {
                    // Removed and added back before it had faded: it fades in again from where it is.
                    match.IsLeaving = false;
                    match.Card.IsHitTestVisible = true;
                    match.Appear.SpringTo(1, ShowpieceSprings.MorphCaptionIn);
                }
                match.Card.Item = item;
                _cards.Add(match);
            }
            else
            {
                var state = new CardState(new LayoutMorphCard(this, item));
                state.IsNew = true;
                _cards.Add(state);
            }
        }
        foreach (var gone in old.Where(c => !reused.Contains(c)))
        {
            if (animate)
            {
                // A removed card stays where it is, out of reach of the pointer and the keyboard, and fades out.
                gone.IsLeaving = true;
                gone.LeavingOpacity = gone.Appear.Value > 0.01 ? gone.Card.Opacity / gone.Appear.Value : 0;
                gone.Card.Index = -1;
                gone.Card.IsHitTestVisible = false;
                gone.Card.Focusable = false;
                KeyboardNavigation.SetIsTabStop(gone.Card, false);
                gone.Appear.SpringTo(0, ShowpieceSprings.MorphCaptionOut);
                _leaving.Add(gone);
            }
            else
            {
                RemoveCard(gone);
            }
        }
        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i].Card;
            card.Index = i;
            if (card.GetVisualParent() is null)
            {
                VisualChildren.Add(card);
                LogicalChildren.Add(card);
            }
        }
        var n = _cards.Count;
        _targets = new LayoutMorphPose[n];
        _aspects = new double[n];
        _starts = new Point[n];
        _delays = new double[n];
        _order = new int[n];
        _depths = new double[n];
        RefreshAspects();
        _openIndex = openItem is null ? -1 : IndexOfItem(openItem);
        if (openItem is not null && _openIndex < 0)
        {
            // The open card's item went away: the world comes forward again and the other cards light up.
            ReleaseOpenWorld(animate);
        }
        _focusIndex = focusItem is null ? -1 : IndexOfItem(focusItem);
        _hoverIndex = hoverItem is null ? -1 : IndexOfItem(hoverItem);
        _subjectIndex = -2;
        _tabStop = tabItem is null ? LayoutMorphEngine.DefaultTabStop(n) : IndexOfItem(tabItem);
        if (_tabStop < 0)
        {
            _tabStop = focusSlot >= 0 && n > 0 ? Math.Min(focusSlot, n - 1) : LayoutMorphEngine.DefaultTabStop(n);
        }
        if (_mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix)
        {
            // The first cards of a ring start on its default front card (or where it was left).
            var remembered = _mode == LayoutMorphMode.Carousel ? _carouselFront : _helixFront;
            var front = old.Count == 0 ? (remembered >= 0 ? remembered : LayoutMorphEngine.DefaultFront(_mode, n)) : Math.Round(_front.Value);
            _front.Jump(Math.Clamp(front, 0, Math.Max(0, n - 1)));
            _frontIndex = -1;
        }
        _targetsDirty = true;
        RecomputeTargets();
        var first = old.Count == 0;
        for (var i = 0; i < _cards.Count; i++)
        {
            var state = _cards[i];
            if (!state.IsNew)
            {
                continue;
            }
            state.IsNew = false;
            var target = TargetBaked(i);
            state.JumpCaptions(target.Caption);
            if (animate && NeighbourOf(i) is { } neighbour)
            {
                // Starts on its neighbour (as if dealt from it), then flies to its place with the others.
                var start = neighbour.Current;
                start.Caption = target.Caption;
                start.Opacity = target.Opacity;
                state.Current = start;
                state.From = start;
            }
            else
            {
                state.Current = target;
                state.From = target;
            }
            if (animate)
            {
                state.Appear.Jump(0);
                state.Appear.SpringTo(1, ShowpieceSprings.MorphCaptionIn);
            }
        }
        if (!first)
        {
            StartFlight(null, ShowpieceSprings.MorphProgress, 0.4, LayoutMorphEngine.DefaultStagger.TotalSeconds);
        }
        UpdateRoving();
        InvalidateArrange();
        PrunePictures();
        Kick();
        NotifySubject();
        if (focusedCard is not null && !_cards.Contains(focusedCard) && _cards.Count > 0)
        {
            // The focused card went away: focus stays in the gallery, on the card now in its place.
            _cards[Math.Clamp(focusSlot, 0, _cards.Count - 1)].Card.Focus(
                focusWasKeyboard ? NavigationMethod.Directional : NavigationMethod.Unspecified);
        }
    }

    private static CardState? FindMatch(List<CardState> states, LayoutMorphItem item, HashSet<CardState> taken)
    {
        foreach (var state in states)
        {
            if (!taken.Contains(state) &&
                (ReferenceEquals(state.Card.Item, item) || (item.Key is { } key && state.Card.Item.Key == key)))
            {
                return state;
            }
        }
        return null;
    }

    /// <summary>The nearest card in data order that was already on the stage (earlier first), or null.</summary>
    private CardState? NeighbourOf(int index)
    {
        for (var d = 1; d < _cards.Count; d++)
        {
            if (index - d >= 0 && !_cards[index - d].IsNew && !_cards[index - d].Appear.IsMoving)
            {
                return _cards[index - d];
            }
            if (index + d < _cards.Count && !_cards[index + d].IsNew && !_cards[index + d].Appear.IsMoving)
            {
                return _cards[index + d];
            }
        }
        return null;
    }

    private void RemoveCard(CardState state)
    {
        SetCardCached(state, false);
        VisualChildren.Remove(state.Card);
        LogicalChildren.Remove(state.Card);
    }

    /// <summary>Removes the cards that have faded out; true while some are still fading.</summary>
    private bool SweepLeaving()
    {
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var state = _leaving[i];
            if (!state.Appear.IsMoving && state.Appear.Value <= 0.01)
            {
                _leaving.RemoveAt(i);
                RemoveCard(state);
            }
            else
            {
                state.Card.Opacity = state.LeavingOpacity * state.Appear.Value;
            }
        }
        return _leaving.Count > 0;
    }

    /// <summary>The world in front again and every card at full strength (no card open).</summary>
    private void ReleaseOpenWorld(bool animate)
    {
        if (animate)
        {
            _recede.SpringTo(0, ShowpieceSprings.MorphOpen);
        }
        else
        {
            _recede.Jump(0);
        }
        foreach (var state in _cards)
        {
            if (animate)
            {
                state.Dim.SpringTo(1, ShowpieceSprings.MorphCaptionOut);
            }
            else
            {
                state.Dim.Jump(1);
            }
        }
    }

    internal int IndexOfItem(LayoutMorphItem? item)
    {
        if (item is null)
        {
            return -1;
        }
        for (var i = 0; i < _cards.Count; i++)
        {
            if (ReferenceEquals(_cards[i].Card.Item, item))
            {
                return i;
            }
        }
        return -1;
    }

    internal void RefreshAspects()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            _aspects[i] = _cards[i].Card.Item.AspectRatio;
        }
        _targetsDirty = true;
    }

    /// <summary>An item's text or picture changed: redraw its card and the panel.</summary>
    internal void OnItemChanged(LayoutMorphItem item, AvaloniaProperty property)
    {
        var index = IndexOfItem(item);
        if (index < 0)
        {
            return;
        }
        var card = _cards[index].Card;
        card.DropTextCache();
        card.UpdateAutomation();
        card.Repaint();
        if (property == LayoutMorphItem.ImageProperty)
        {
            PrunePictures();
            Kick();
        }
        if (property == LayoutMorphItem.AspectRatioProperty)
        {
            RefreshAspects();
            if (_mode == LayoutMorphMode.Masonry)
            {
                StartFlight(null, ShowpieceSprings.MorphProgress, 0.4, LayoutMorphEngine.DefaultStagger.TotalSeconds);
                Kick();
            }
        }
        if (_subjectIndex == index)
        {
            _subjectIndex = -2;
            NotifySubject();
        }
    }

    // Layout changes ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Moves every card to <paramref name="mode"/>. <paramref name="origin"/> (stage px) is where the stagger starts:
    /// the view option that was chosen, or the focused card for the arrow keys; null staggers from the stage center.
    /// </summary>
    internal void SetLayout(LayoutMorphMode mode, Point? origin, double stagger, int frontCard = -1)
    {
        if (_openIndex >= 0)
        {
            CloseCore(animate: false);
        }
        var old = _mode;
        RememberFront(old);
        _mode = mode;
        if (mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix)
        {
            var front = frontCard >= 0 ? frontCard : mode == LayoutMorphMode.Carousel ? _carouselFront : _helixFront;
            if (front < 0)
            {
                front = LayoutMorphEngine.DefaultFront(mode, _cards.Count);
            }
            _front.Jump(front);
        }
        _scroll.Jump(0);
        _scrollRaw = 0;
        _targetsDirty = true;
        RecomputeTargets();
        StartFlight(origin, ShowpieceSprings.MorphProgress, 0.4, stagger);
        UpdateFrontIndex();
        if (mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix && _frontIndex >= 0 && frontCard < 0)
        {
            _tabStop = _frontIndex;
            UpdateRoving();
        }
        Kick();
        NotifySubject();
    }

    private void RememberFront(LayoutMorphMode mode)
    {
        if (mode == LayoutMorphMode.Carousel)
        {
            _carouselFront = ClampFront(Math.Round(_front.Target));
        }
        else if (mode == LayoutMorphMode.Helix)
        {
            _helixFront = ClampFront(Math.Round(_front.Target));
        }
    }

    /// <summary>The carousel and the helix both run from the first card to the last (neither wraps round).</summary>
    private double ClampFront(double front) => Math.Clamp(front, 0, Math.Max(0, _cards.Count - 1));

    /// <summary>The stage was resized: every card jumps to its new place in one frame (Arc does not animate resizes).</summary>
    internal void SetMetrics(Size size)
    {
        if (size.Width < 32 || size.Height < 32)
        {
            // Not laid out yet (the host's height arrives with the first size pass).
            return;
        }
        var metrics = new LayoutMorphMetrics(size.Width, size.Height);
        var changed = !_hasMetrics || Math.Abs(metrics.Width - _metrics.Width) > 0.01 || Math.Abs(metrics.Height - _metrics.Height) > 0.01;
        _metrics = metrics;
        _hasMetrics = true;
        if (!changed)
        {
            return;
        }
        _targetsDirty = true;
        RecomputeTargets();
        if (_openIndex >= 0)
        {
            // The open detail takes its new size (and stacks or unstacks) at once, with its buttons' layer.
            _openPose = LayoutMorphEngine.OpenPose(_metrics, _cards[_openIndex].Card.Item.OpenVariant, out _openSheet);
            var open = _cards[_openIndex].Card;
            open.Sheet = _openSheet;
            open.IsSheetNarrow = _openSheet.IsStacked;
            open.DropTextCache();
            _owner.OnOpenPoseChanged();
        }
        _scroll.Jump(Math.Clamp(_scroll.Target, -_scrollRange, 0));
        _scrollRaw = _scroll.Value;
        foreach (var state in _cards)
        {
            var target = TargetBaked(state.Card.Index);
            state.JumpTo(target);
            state.JumpCaptions(target.Caption);
        }
        // Resizes are not animated: picture copies follow the new sizes at once, down as well as up.
        ApplyVisualsAndShrink();
        InvalidateArrange();
        _owner.OnStageStateChanged();
    }

    private void RecomputeTargets()
    {
        if (!_targetsDirty || _cards.Count == 0)
        {
            _targetsDirty = false;
            return;
        }
        _targetsDirty = false;
        LayoutMorphEngine.Arrange(_mode, _metrics, _aspects, _front.Value, _targets, out _camera, out _scrollRange);
    }

    /// <summary>The target of a card outside the camera (the camera, scroll and recede folded in), or the open pose.</summary>
    private LayoutMorphPose TargetBaked(int index)
    {
        if (index == _openIndex)
        {
            return _openPose;
        }
        ref var target = ref _targets[index];
        if (_camera.Radius == 0 && _camera.Tilt == 0 && _camera.FocusY == 0 && _camera.TranslateY == 0 &&
            _scroll.Value == 0 && _recede.Value == 0)
        {
            var flat = target;
            flat.UsesCamera = false;
            return flat;
        }
        return LayoutMorphEngine.Bake(target, _camera, _scroll.Value, _recede.Value);
    }

    /// <summary>
    /// Starts every card from where it is now toward its target: delayed by its distance from <paramref name="origin"/>,
    /// with the old caption fading out now and the new one fading in <paramref name="captionLag"/>s after the card starts.
    /// </summary>
    private void StartFlight(Point? origin, SpringSpec spring, double captionLag, double stagger)
    {
        var animate = Animates;
        var size = Bounds.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var o = origin ?? center;
        for (var i = 0; i < _cards.Count; i++)
        {
            _starts[i] = _cards[i].Card.ScreenCenter;
        }
        LayoutMorphEngine.StaggerDelays(_starts, o, animate ? stagger : 0, _delays);
        for (var i = 0; i < _cards.Count; i++)
        {
            var state = _cards[i];
            var target = TargetBaked(i);
            if (animate)
            {
                state.Launch(target, _delays[i], spring);
                state.SwapCaptions(target.Caption, _delays[i] + captionLag);
            }
            else
            {
                state.JumpTo(target);
                state.JumpCaptions(target.Caption);
            }
        }
        InvalidateArrange();
    }

    // Opening ------------------------------------------------------------------------------------------------------------

    internal void Open(int index)
    {
        if (index < 0 || index >= _cards.Count || index == _openIndex)
        {
            return;
        }
        if (_openIndex >= 0)
        {
            CloseCore(animate: false);
        }
        var animate = Animates;
        _openIndex = index;
        _hoverIndex = -1;
        _openPose = LayoutMorphEngine.OpenPose(_metrics, _cards[index].Card.Item.OpenVariant, out _openSheet);
        var card = _cards[index].Card;
        card.Sheet = _openSheet;
        card.IsSheetNarrow = _openSheet.IsStacked;
        for (var i = 0; i < _cards.Count; i++)
        {
            var state = _cards[i];
            if (i == index)
            {
                if (animate)
                {
                    state.Launch(_openPose, 0, ShowpieceSprings.MorphOpen);
                    state.SwapCaptions(LayoutMorphCaption.Sheet, 0.36);
                }
                else
                {
                    state.JumpTo(_openPose);
                    state.JumpCaptions(LayoutMorphCaption.Sheet);
                }
                state.Dim.Jump(1);
            }
            else if (animate)
            {
                state.Dim.SpringTo(LayoutMorphEngine.OpenDimOpacity, ShowpieceSprings.MorphCaptionOut);
            }
            else
            {
                state.Dim.Jump(LayoutMorphEngine.OpenDimOpacity);
            }
        }
        if (animate)
        {
            _recede.SpringTo(LayoutMorphEngine.OpenRecede, ShowpieceSprings.MorphOpen);
        }
        else
        {
            _recede.Jump(LayoutMorphEngine.OpenRecede);
        }
        UpdateArrangeSize(_cards[index], _openPose);
        _tabStop = index;
        UpdateRoving();
        Kick();
        NotifySubject();
    }

    internal void Close() => CloseCore(Animates);

    private void CloseCore(bool animate)
    {
        var index = _openIndex;
        if (index < 0)
        {
            return;
        }
        _openIndex = -1;
        if (animate)
        {
            _recede.SpringTo(0, ShowpieceSprings.MorphOpen);
        }
        else
        {
            _recede.Jump(0);
        }
        for (var i = 0; i < _cards.Count; i++)
        {
            var state = _cards[i];
            var target = TargetBaked(i);
            if (i == index)
            {
                if (animate)
                {
                    state.Launch(target, 0, ShowpieceSprings.MorphOpen);
                    state.SwapCaptions(target.Caption, 0.66);
                }
                else
                {
                    state.JumpTo(target);
                    state.JumpCaptions(target.Caption);
                }
            }
            else if (animate)
            {
                state.Dim.SpringTo(1, ShowpieceSprings.MorphCaptionOut);
            }
            else
            {
                state.Dim.Jump(1);
            }
        }
        UpdateRoving();
        Kick();
        NotifySubject();
    }

    // Front, focus, hover ----------------------------------------------------------------------------------------------

    /// <summary>Moves keyboard focus to a card; the carousel and helix turn it to the front (the long way, like Arc), a scrolling list scrolls to it.</summary>
    internal void FocusCard(int index, bool animate, bool focus = true)
    {
        if (index < 0 || index >= _cards.Count)
        {
            return;
        }
        _tabStop = index;
        UpdateRoving();
        BringToFront(index, animate);
        if (focus)
        {
            _cards[index].Card.Focus(NavigationMethod.Directional);
        }
    }

    private void BringToFront(int index, bool animate)
    {
        if (_mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix)
        {
            // The strip turns the direct way, however far (Arc: End from the first card turns 23 places).
            double target = index;
            if (animate && Animates)
            {
                _front.SpringTo(target, ShowpieceSprings.MorphSnap);
            }
            else
            {
                _front.Jump(target);
            }
            _targetsDirty = true;
            Kick();
        }
        else if (IsScrollable)
        {
            ScrollIntoView(index, animate);
        }
    }

    private void ScrollIntoView(int index, bool animate)
    {
        ref var t = ref _targets[index];
        var top = t.Y - t.Height / 2 + _scroll.Target;
        var bottom = t.Y + t.Height / 2 + _scroll.Target;
        var min = -_metrics.Height / 2 + _metrics.Pad;
        var max = _metrics.Height / 2 - _metrics.Pad;
        var offset = _scroll.Target;
        if (top < min)
        {
            offset += min - top;
        }
        else if (bottom > max)
        {
            offset -= bottom - max;
        }
        ScrollTo(offset, animate, 0);
    }

    private void ScrollTo(double offset, bool animate, double velocity)
    {
        offset = Math.Clamp(offset, -_scrollRange, 0);
        if (animate && Animates)
        {
            _scroll.SpringTo(offset, ShowpieceSprings.MorphProgress, velocity);
        }
        else
        {
            _scroll.Jump(offset);
        }
        _scrollRaw = offset;
        Kick();
    }

    /// <summary>
    /// A card got or lost focus. Keyboard (and programmatic) focus turns the carousel or helix to the card and scrolls the
    /// list to it; a pointer press only focuses: what the click does is decided on release (<see cref="OnTap"/>), so the
    /// ring must not turn under the pointer between the press and the release.
    /// </summary>
    internal void OnCardFocusChanged(LayoutMorphCard card, bool focused, NavigationMethod method = NavigationMethod.Unspecified)
    {
        if (focused)
        {
            _focusIndex = card.Index;
            if (_tabStop != card.Index)
            {
                _tabStop = card.Index;
                UpdateRoving();
            }
            var moves = method != NavigationMethod.Pointer;
            if (moves && _mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix && _frontIndex != card.Index)
            {
                BringToFront(card.Index, true);
            }
            else if (moves && IsScrollable)
            {
                ScrollIntoView(card.Index, true);
            }
        }
        else if (_focusIndex == card.Index)
        {
            _focusIndex = -1;
        }
        Kick();
        NotifySubject();
    }

    private void UpdateRoving()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i].Card;
            var open = _openIndex >= 0;
            card.Focusable = !open;
            KeyboardNavigation.SetIsTabStop(card, !open && i == _tabStop);
        }
    }

    private void UpdateFrontIndex()
    {
        var index = -1;
        if (_mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix && _cards.Count > 0)
        {
            index = (int)ClampFront(Math.Round(_front.Value));
        }
        if (index == _frontIndex)
        {
            return;
        }
        _frontIndex = index;
        // The carousel's and helix's tab stop is the card in front (Arc is one card off; the group spec fixes that).
        if (index >= 0 && _openIndex < 0 && _tabStop != index)
        {
            _tabStop = index;
            UpdateRoving();
        }
        _owner.OnFrontChanged(index >= 0 ? _cards[index].Card.Item : null);
        NotifySubject();
    }

    /// <summary>The card the panel and the tag describe: open, else pointed at, else keyboard-focused, else in front.</summary>
    private int SubjectIndex
    {
        get
        {
            if (_openIndex >= 0)
            {
                return _openIndex;
            }
            if (_hoverIndex >= 0)
            {
                return _hoverIndex;
            }
            if (_focusIndex >= 0 && _cards[_focusIndex].Card.IsKeyboardFocused)
            {
                return _focusIndex;
            }
            return _frontIndex;
        }
    }

    private void NotifySubject()
    {
        var index = SubjectIndex;
        if (index == _subjectIndex)
        {
            return;
        }
        _subjectIndex = index;
        _owner.OnSubjectChanged(index >= 0 && index < _cards.Count ? _cards[index].Card.Item : null);
    }

    /// <summary>The card with a floating tag (Masonry, Stack and Helix only: their cards carry no caption).</summary>
    private int TagIndex
    {
        get
        {
            if (_openIndex >= 0 || IsDragging || _mode is not (LayoutMorphMode.Masonry or LayoutMorphMode.Stack or LayoutMorphMode.Helix))
            {
                return -1;
            }
            if (_hoverIndex >= 0)
            {
                return _hoverIndex;
            }
            return _focusIndex >= 0 && _cards[_focusIndex].Card.IsKeyboardFocused ? _focusIndex : -1;
        }
    }

    internal void InvokeCard(LayoutMorphCard card, NavigationMethod method = NavigationMethod.Unspecified)
    {
        if (_openIndex >= 0)
        {
            return;
        }
        _tabStop = card.Index;
        UpdateRoving();
        _owner.InvokeItem(card.Item, method);
    }

    // Input --------------------------------------------------------------------------------------------------------------

    protected override object? HitTestItem(Point position) => CardAtPoint(position);

    /// <summary>
    /// The nearest card under a stage point: cards are tested from the top of the draw order down, each through the
    /// inverse of its own projection, so overlapping cards resolve by depth (no visual-tree hit test, no allocation).
    /// </summary>
    internal LayoutMorphCard? CardAtPoint(Point position)
    {
        for (var rank = _order.Length - 1; rank >= 0; rank--)
        {
            var index = _order[rank];
            if (index >= _cards.Count)
            {
                continue;
            }
            var card = _cards[index].Card;
            if (!card.IsHitTestVisible || card.Opacity <= 0.02 ||
                !ShowpieceProjection.TryInvert(card.ScreenMatrix, out var inverse))
            {
                continue;
            }
            if (card.HitTest(ShowpieceProjection.Apply(inverse, position)))
            {
                return card;
            }
        }
        return null;
    }

    protected override void OnHoverChanged(Point? position, object? item)
    {
        var index = item is LayoutMorphCard card && _openIndex < 0 ? card.Index : -1;
        if (index == _hoverIndex)
        {
            return;
        }
        _hoverIndex = index;
        Kick();
        NotifySubject();
    }

    protected override void OnTap(Point position, object? item, KeyModifiers modifiers)
    {
        if (_openIndex >= 0)
        {
            // A press on the open card stays; anywhere else on the stage closes it.
            if (item is not LayoutMorphCard card || card.Index != _openIndex)
            {
                _owner.Close();
            }
            return;
        }
        if (item is LayoutMorphCard target)
        {
            if (_mode == LayoutMorphMode.Carousel && target.Index != _frontIndex)
            {
                // A card beside the front turns to the front; only the front card opens (Arc measured: one and two
                // places away both turn; the helix opens any card).
                FocusCard(target.Index, animate: true, focus: false);
                target.Focus(NavigationMethod.Pointer);
                return;
            }
            target.Focus(NavigationMethod.Pointer);
            InvokeCard(target, NavigationMethod.Pointer);
        }
    }

    protected override bool OnDragStarting(Point position, object? pressedItem)
    {
        if (_openIndex >= 0 || _cards.Count == 0)
        {
            return false;
        }
        return _mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix || IsScrollable;
    }

    protected override void OnDragStarted(Point position)
    {
        // A drag moves the cards from pointer events, with no frames until it is released.
        BeginCacheMotion();
        if (_mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix)
        {
            _drag = DragKind.Ring;
            _front.Stop();
            _frontRaw = _front.Value;
        }
        else
        {
            _drag = DragKind.Scroll;
            _scroll.Stop();
            _scrollRaw = _scroll.Value;
        }
        _hoverIndex = -1;
        _owner.OnStageStateChanged();
        Kick();
        NotifySubject();
    }

    protected override void OnDragMoved(Vector delta, Point position)
    {
        if (_drag == DragKind.Ring)
        {
            var arc = LayoutMorphEngine.ArcPerCard(_mode, _metrics);
            if (arc > 0)
            {
                // The card under the pointer follows it 1:1 along the arc; past the first or last card it stretches.
                _frontRaw -= delta.X / arc;
                _front.Jump(LayoutMorphEngine.StretchFront(_frontRaw, _cards.Count, arc, _metrics.InnerHeight));
                _targetsDirty = true;
            }
        }
        else if (_drag == DragKind.Scroll)
        {
            _scrollRaw += delta.Y;
            _scroll.Jump(RubberBand.Clamp(_scrollRaw, -_scrollRange, 0, _metrics.InnerHeight));
        }
        Kick();
    }

    protected override void OnDragCompleted(Vector velocity)
    {
        var kind = _drag;
        _drag = DragKind.None;
        _owner.OnStageStateChanged();
        if (kind == DragKind.Ring)
        {
            var arc = LayoutMorphEngine.ArcPerCard(_mode, _metrics);
            // Past an end it springs back to that end card.
            var target = ClampFront(LayoutMorphEngine.SnapFront(_front.Value, velocity.X, arc));
            if (Animates)
            {
                _front.SpringTo(target, ShowpieceSprings.MorphSnap, arc > 0 ? -velocity.X / arc : 0);
            }
            else
            {
                _front.Jump(target);
            }
            _targetsDirty = true;
        }
        else if (kind == DragKind.Scroll)
        {
            var target = Math.Clamp(_scroll.Value + velocity.Y * 0.2, -_scrollRange, 0);
            ScrollTo(target, true, velocity.Y);
        }
        Kick();
        if (!IsLoopRunning)
        {
            // Released where it rests: no frames will end the motion.
            RestCacheMotion();
        }
    }

    protected override void OnDragCancelled() => OnDragCompleted(default);

    protected override void OnWheelScrolled(PointerWheelEventArgs e)
    {
        if (_openIndex >= 0 || _cards.Count == 0)
        {
            return;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_mode is LayoutMorphMode.Carousel or LayoutMorphMode.Helix)
        {
            var webDx = shift ? ShowpieceWheel.WebDeltaY(e.Delta.Y) : -100 * e.Delta.X;
            if (Math.Abs(webDx) < 1e-6)
            {
                return;
            }
            var arc = LayoutMorphEngine.ArcPerCard(_mode, _metrics);
            // One 60px wheel step nudges the ring by about 30px of arc, then it settles on the nearest card.
            var next = Math.Clamp(_front.Value + 0.5 * webDx / arc, -0.49, _cards.Count - 0.51);
            _front.Jump(next);
            var target = Math.Round(next);
            if (Animates)
            {
                _front.SpringTo(target, ShowpieceSprings.MorphSnap);
            }
            else
            {
                _front.Jump(target);
            }
            _targetsDirty = true;
            e.Handled = true;
            Kick();
            return;
        }
        if (IsScrollable && !shift)
        {
            var webDy = ShowpieceWheel.WebDeltaY(e.Delta.Y);
            var target = Math.Clamp(_scroll.Target - webDy, -_scrollRange, 0);
            if (Math.Abs(target - _scroll.Target) < 0.01)
            {
                return;
            }
            ScrollTo(target, true, _scroll.Velocity);
            e.Handled = true;
        }
    }

    protected override bool OnStageKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }
        if (_openIndex >= 0)
        {
            if (e.Key == Key.Escape)
            {
                _owner.Close();
                return true;
            }
            return false;
        }
        var focused = e.Source is Visual source ? FindCard(source) : null;
        if (focused is null)
        {
            return false;
        }
        var index = focused.Index;
        switch (e.Key)
        {
            case Key.Up:
                FocusCard(Math.Max(0, index - 1), true);
                return true;
            case Key.Down:
                FocusCard(Math.Min(_cards.Count - 1, index + 1), true);
                return true;
            case Key.Home:
                FocusCard(0, true);
                return true;
            case Key.End:
                FocusCard(_cards.Count - 1, true);
                return true;
            case Key.Left:
            case Key.Right:
                _owner.StepLayout(e.Key == Key.Right ? 1 : -1, focused);
                return true;
            case Key.Enter:
            case Key.Space:
                InvokeCard(focused, NavigationMethod.Tab);
                return true;
        }
        return false;
    }

    private LayoutMorphCard? FindCard(Visual source)
    {
        for (Visual? v = source; v is not null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (v is LayoutMorphCard card)
            {
                return card;
            }
        }
        return null;
    }

    protected override string? GetAccessibleSummary() => _owner.StageSummary();

    // Frames -------------------------------------------------------------------------------------------------------------

    /// <summary>Applies the current state now and asks for frames while anything moves (none under motion Off).</summary>
    internal void Kick()
    {
        ApplyVisuals();
        if (IsMoving || _cards.Any(c => c.NeedsFrames))
        {
            RequestFrames();
        }
    }

    protected override bool OnAdvance(double dt)
    {
        BeginCacheMotion();
        var moving = false;
        if (_front.IsMoving)
        {
            _front.Step(dt);
            _targetsDirty = true;
            moving = true;
        }
        moving |= _scroll.Step(dt);
        moving |= _recede.Step(dt);
        moving |= _scroll.IsMoving || _recede.IsMoving;
        foreach (var state in _cards)
        {
            moving |= state.Step(dt);
        }
        foreach (var state in _leaving)
        {
            state.Step(dt);
        }
        moving |= SweepLeaving();
        ApplyVisuals();
        return moving;
    }

    protected override void OnLoopStopped()
    {
        if (_front.IsMoving || _cards.Any(c => c.IsMoving))
        {
            return;
        }
        ApplyVisuals();
        RestCacheMotion();
    }

    protected override void OnMotionLevelChanged(ArcMotionLevel level)
    {
        if (level != ArcMotionLevel.Full)
        {
            foreach (var state in _cards)
            {
                state.Settle(TargetBaked(state.Card.Index));
            }
            foreach (var state in _leaving)
            {
                state.Appear.Jump(0);
            }
            SweepLeaving();
            if (_front.IsMoving)
            {
                _front.Jump(_front.Target);
                _targetsDirty = true;
            }
            _scroll.Jump(_scroll.Target);
            _recede.Jump(_recede.Target);
            ApplyVisuals();
        }
    }

    protected override void OnThemeChanged()
    {
        // A detach raises a resource change too: the palette stays dirty and is resolved again on attach.
        _themeDirty = true;
        SchedulePalette();
    }

    /// <summary>
    /// Resolves the new colors once the theme's resources have settled (one job however many resource changes arrive),
    /// then repaints every card. The job checks again when it runs: the stage may have left the tree in between.
    /// </summary>
    private void SchedulePalette()
    {
        if (_palettePosted || !CanResolvePalette)
        {
            return;
        }
        _palettePosted = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _palettePosted = false;
            EnsurePalette();
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// The ArcDesign tokens can be read: the stage is in a window's tree. Fonts are read through the theme context, which
    /// a detached control does not have (reading them throws), and colors read while detached would be wrong.
    /// </summary>
    private bool CanResolvePalette =>
        this.IsAttachedToVisualTree() && ((Avalonia.LogicalTree.ILogical)this).IsAttachedToLogicalTree &&
        TopLevel.GetTopLevel(this) is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The theme may have changed while the stage was away (its resource change came before it could read tokens).
        _themeDirty = true;
        EnsurePalette();
        ShowpieceRenderer.Detect(this, ApplyRenderer);
        _scalingSource = TopLevel.GetTopLevel(this);
        if (_scalingSource is not null)
        {
            _scalingSource.ScalingChanged += OnScalingChanged;
        }
        if (_hasMetrics)
        {
            // The picture copies went with the detach; the cards ask for them again.
            ApplyVisuals();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_scalingSource is not null)
        {
            _scalingSource.ScalingChanged -= OnScalingChanged;
            _scalingSource = null;
        }
        _cacheHold?.Cancel();
        _cacheMoving = false;
        DropCardCaches();
        _pictures.Clear();
    }

    /// <summary>The window moved to a screen with another scaling: the picture copies are made again for it.</summary>
    private void OnScalingChanged(object? sender, EventArgs e)
    {
        _pictures.Clear();
        foreach (var state in _cards)
        {
            state.Card.Repaint();
        }
        foreach (var state in _leaving)
        {
            state.Card.Repaint();
        }
        ApplyVisuals();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SetMetrics(e.NewSize);
    }

    protected override void RenderStage(DrawingContext context)
    {
        // The cards draw themselves; the stage paints nothing of its own.
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // A themed tree's layout pass is a safe place to catch up on a palette that is still dirty (no-op otherwise).
        EnsurePalette();
        foreach (var state in _cards)
        {
            state.Card.Measure(Size.Infinity);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var state in _cards)
        {
            state.Card.Arrange(new Rect(state.ArrangeSize));
        }
        return finalSize;
    }

    private void UpdateArrangeSize(CardState state, in LayoutMorphPose target)
    {
        var size = new Size(
            Math.Ceiling(Math.Max(1, Math.Max(Math.Max(state.From.Width, state.Current.Width), target.Width))) + 1,
            Math.Ceiling(Math.Max(1, Math.Max(Math.Max(state.From.Height, state.Current.Height), target.Height))) + 1);
        if (size != state.ArrangeSize)
        {
            state.ArrangeSize = size;
            InvalidateArrange();
        }
    }

    /// <summary>
    /// Reads the card colors and fonts when the theme changed or nothing was read yet. Out of a themed tree it reads
    /// nothing and keeps the palette dirty (the cards keep their last palette, or draw nothing before the first one);
    /// attaching, the next layout pass or the next frame resolves it.
    /// </summary>
    private void EnsurePalette()
    {
        if (!_themeDirty && Palette.CardFillBrush is not null)
        {
            return;
        }
        if (!CanResolvePalette)
        {
            return;
        }
        _themeDirty = false;
        var p = Palette;
        p.CardFill = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceRaised);
        p.CardBorder = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        p.MediaFill = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
        p.Foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        p.TextSecondary = ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary);
        p.TextMuted = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        p.Rule = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderSubtle);
        p.FocusRing = ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing);
        var background = ArcTokens.GetColor(this, ArcDesignTokenKind.Background);
        p.IsDark = (background.R + background.G + background.B) / 3.0 < 128;
        p.BodyFamily = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
        p.DisplayFamily = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.DisplayFontFamily) ?? FontFamily.Default;
        p.Body = new Typeface(p.BodyFamily);
        p.BodyMedium = new Typeface(p.BodyFamily, FontStyle.Normal, FontWeight.Medium);
        p.DisplayMedium = new Typeface(p.DisplayFamily, FontStyle.Normal, FontWeight.Medium);
        p.CardFillBrush = new ImmutableSolidColorBrush(p.CardFill);
        p.CardBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(p.CardBorder), 1);
        p.MediaFillBrush = new ImmutableSolidColorBrush(p.MediaFill);
        p.FocusPen = new ImmutablePen(new ImmutableSolidColorBrush(p.FocusRing), 2);
        p.RulePen = new ImmutablePen(new ImmutableSolidColorBrush(p.Rule), 1);
        TextCache.Clear();
        foreach (var state in _cards)
        {
            state.Card.DropTextCache();
            state.Card.Repaint();
        }
    }

    /// <summary>Writes every card's interpolated pose, matrix, opacity, draw state and draw order; places the tag.</summary>
    internal void ApplyVisuals()
    {
        var n = _cards.Count;
        if (n == 0 || !_hasMetrics)
        {
            _owner.OnTagChanged(null, default);
            return;
        }
        EnsurePalette();
        RecomputeTargets();
        UpdateFrontIndex();
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            size = new Size(_metrics.Width, _metrics.Height);
        }
        var perspective = _metrics.Perspective;
        var tagIndex = TagIndex;
        // Picture copies are sized for the window's scaling (none while detached: the cards draw the originals).
        var top = TopLevel.GetTopLevel(this);
        var scaling = top?.RenderScaling ?? 1;
        var allowCache = _cacheMoving && AllowCardCache && CachesCards(_mode);
        var largest = 0.0;
        var resting = !_front.IsMoving && !_scroll.IsMoving && !_recede.IsMoving && _leaving.Count == 0 && _drag == DragKind.None;
        _pictures.BeginFrame();
        for (var i = 0; i < n; i++)
        {
            var state = _cards[i];
            var card = state.Card;
            var target = TargetBaked(i);
            state.Resolve(target);
            ref readonly var pose = ref state.Current;
            var focused = card.IsKeyboardFocused && _openIndex < 0;
            var lifted = _openIndex < 0 && (i == _hoverIndex || focused) && _drag == DragKind.None;
            state.SetLift(lifted ? 1 : 0, Animates);

            var plane = LayoutMorphEngine.CardTransform(pose, state.Lift.Value, state.FlightLift, state.FlightTilt,
                LayoutMorphCamera.Identity, 0, 0);
            var matrix = LayoutMorphEngine.ScreenMatrix(plane, perspective, size);
            card.ScreenMatrix = matrix;
            var moved = card.SetMatrix(matrix);
            var center = plane.Transform(pose.Width / 2, pose.Height / 2, 0);
            _depths[i] = center.Z;

            // Opacity: the target layout's own (the carousel window), the back step, the open dim, a new card's fade.
            // A card whose face points away from the eye drops to 0.72 and shows only its base, in every layout and
            // frame by frame (Arc: the cards leaving the helix or the carousel's hidden back keep the step until they
            // turn round; the test is the face against the eye in perspective, which matched every recorded frame).
            var layoutOpacity = 1.0;
            var hidesContent = false;
            if (i != _openIndex)
            {
                var back = LayoutMorphEngine.FacesAway(plane, pose.Width, pose.Height, perspective);
                layoutOpacity = (_mode == LayoutMorphMode.Helix ? 1 : _targets[i].Opacity) *
                                (back ? LayoutMorphEngine.HelixBackOpacity : 1);
                hidesContent = back;
            }
            var opacity = layoutOpacity * state.Dim.Value * state.Appear.Value;
            card.Opacity = opacity;
            card.IsHitTestVisible = opacity > 0.02 && (_openIndex < 0 || i == _openIndex);

            // Draw state: only a real change repaints the card.
            var media = new Rect(pose.MediaX, pose.MediaY, Math.Max(0, pose.MediaWidth), Math.Max(0, pose.MediaHeight));
            var dirty = card.DrawWidth != pose.Width || card.DrawHeight != pose.Height || card.DrawRadius != pose.Radius ||
                        card.Media != media || card.MediaRadius != pose.MediaRadius || card.HidesContent != hidesContent ||
                        Math.Abs(card.Lift - state.Lift.Value) > 1e-4;
            card.DrawWidth = pose.Width;
            card.DrawHeight = pose.Height;
            card.DrawRadius = pose.Radius;
            card.Media = media;
            card.MediaRadius = pose.MediaRadius;
            card.HidesContent = hidesContent;
            card.Lift = state.Lift.Value;
            for (var k = 0; k < 4; k++)
            {
                var w = state.Captions[k].Value;
                if (Math.Abs(card.Captions[k] - w) > 1e-4)
                {
                    card.Captions[k] = w;
                    dirty = true;
                }
            }
            if (dirty)
            {
                card.Repaint();
            }
            if (top is not null)
            {
                WantPicture(state, card, matrix, media, opacity > 0.003 && !hidesContent, target, perspective, size, scaling);
            }
            UpdateCardCache(state, card, moved, dirty, allowCache, ref largest);
            state.WasDirty = dirty;
            resting &= !state.NeedsFrames;
            UpdateArrangeSize(state, target);
        }
        // Copies grow at once; they shrink after the hold (or at rest when nothing animates).
        if (top is not null && _pictures.EndFrame(resting, _shrinkPictures || (resting && !Animates)))
        {
            RepaintChangedPictures();
        }
        if (largest > 0)
        {
            // Only up while cached (a new scale renders every cached card again).
            var scale = ShowpieceRenderer.CacheScale(largest);
            if (scale > _cardCache.RenderAtScale)
            {
                _cardCache.RenderAtScale = scale;
            }
        }
        SortByDepth();
        if (tagIndex >= 0 && tagIndex < n)
        {
            _owner.OnTagChanged(_cards[tagIndex].Card.Item, _cards[tagIndex].Card.ScreenTop);
        }
        else
        {
            _owner.OnTagChanged(null, default);
        }
    }

    /// <summary>Nearer cards draw (and hit-test) on top: ZIndex by depth, data order on ties; only changes are written.</summary>
    private void SortByDepth()
    {
        var n = _cards.Count;
        for (var i = 0; i < n; i++)
        {
            _order[i] = i;
        }
        // Insertion sort: the order barely changes between frames.
        for (var i = 1; i < n; i++)
        {
            var key = _order[i];
            var j = i - 1;
            while (j >= 0 && Compare(_order[j], key) > 0)
            {
                _order[j + 1] = _order[j];
                j--;
            }
            _order[j + 1] = key;
        }
        for (var rank = 0; rank < n; rank++)
        {
            var card = _cards[_order[rank]].Card;
            if (card.ZIndex != rank)
            {
                card.ZIndex = rank;
            }
        }
    }

    private int Compare(int a, int b)
    {
        var da = a == _openIndex ? double.MaxValue : _depths[a];
        var db = b == _openIndex ? double.MaxValue : _depths[b];
        if (Math.Abs(da - db) > 1e-6)
        {
            return da < db ? -1 : 1;
        }
        return a.CompareTo(b);
    }

    // Rendering cost -------------------------------------------------------------------------------------------------------

    private void ApplyRenderer(bool gpu)
    {
        _gpu = gpu;
        if (!AllowCardCache)
        {
            DropCardCaches();
        }
    }

    /// <summary>Something moves: moving cards may take bitmaps (a running hold is cancelled).</summary>
    private void BeginCacheMotion()
    {
        _cacheHold?.Cancel();
        _cacheMoving = true;
    }

    /// <summary>The stage rested: the bitmaps go and larger copies shrink after the hold (at once without full motion).</summary>
    private void RestCacheMotion()
    {
        if (Animates)
        {
            _cacheHold ??= new ShowpieceCacheHold(this, OnCacheHoldElapsed);
            _cacheHold.Start();
        }
        else
        {
            OnCacheHoldElapsed();
        }
    }

    private void OnCacheHoldElapsed()
    {
        _cacheMoving = false;
        // No card may keep a bitmap now; copies are sized to what is drawn.
        ApplyVisualsAndShrink();
        DropCardCaches();
    }

    /// <summary><see cref="ApplyVisuals"/>, letting picture copies larger than needed shrink.</summary>
    private void ApplyVisualsAndShrink()
    {
        _shrinkPictures = true;
        try
        {
            ApplyVisuals();
        }
        finally
        {
            _shrinkPictures = false;
        }
    }

    /// <summary>Every card back to vectors.</summary>
    private void DropCardCaches()
    {
        foreach (var state in _cards)
        {
            SetCardCached(state, false);
        }
        foreach (var state in _leaving)
        {
            SetCardCached(state, false);
        }
        _cardCache.RenderAtScale = 1;
    }

    private void SetCardCached(CardState state, bool cached)
    {
        if (state.Cached != cached)
        {
            state.Cached = cached;
            ShowpieceRenderer.SetCache(state.Card, cached ? _cardCache : null);
        }
    }

    /// <summary>
    /// A card draws from the shared bitmap while only its matrix changes: it moved this frame and its drawing changed in
    /// neither this frame nor the last. A drawing that changes two frames running (a size morph, a lift) is drawn as vectors;
    /// a single change (a card turning its back) renders the bitmap again. Cards are cached only after their first frame (a
    /// cache set earlier is ignored).
    /// </summary>
    private void UpdateCardCache(CardState state, LayoutMorphCard card, bool moved, bool dirty, bool allow, ref double largest)
    {
        var cached = state.Cached;
        if (!allow || (dirty && state.WasDirty))
        {
            cached = false;
        }
        else if (moved && !dirty && !state.WasDirty && card.Face.HasRendered)
        {
            cached = true;
        }
        SetCardCached(state, cached);
        if (cached)
        {
            // The focus ring is drawn 3px outside the card.
            largest = Math.Max(largest, ShowpieceRenderer.Magnification(card.ScreenMatrix,
                new Rect(-3, -3, card.DrawWidth + 6, card.DrawHeight + 6)));
        }
    }

    /// <summary>
    /// Reports at which density the card's picture is drawn now (when it shows) and, while the card flies, where it lands, so
    /// the copy is the right size when it gets there.
    /// </summary>
    private void WantPicture(CardState state, LayoutMorphCard card, in Matrix matrix, Rect media, bool drawn,
        in LayoutMorphPose target, double perspective, Size size, double scaling)
    {
        if (card.Item.Image is not Bitmap bitmap || card.UsesOriginalPicture)
        {
            return;
        }
        var pixels = bitmap.PixelSize;
        if (pixels.Width <= 0 || pixels.Height <= 0)
        {
            return;
        }
        if (drawn && media.Width > 0.5 && media.Height > 0.5)
        {
            _pictures.Want(bitmap, scaling * PictureDensity(matrix, media, pixels), ahead: false);
        }
        if (state.IsMoving && target.Opacity > 0.003 && target.MediaWidth > 0.5 && target.MediaHeight > 0.5)
        {
            var plane = LayoutMorphEngine.CardTransform(target, 0, 0, 0, LayoutMorphCamera.Identity, 0, 0);
            if (!LayoutMorphEngine.FacesAway(plane, target.Width, target.Height, perspective))
            {
                var landing = LayoutMorphEngine.ScreenMatrix(plane, perspective, size);
                var landingMedia = new Rect(target.MediaX, target.MediaY, target.MediaWidth, target.MediaHeight);
                _pictures.Want(bitmap, scaling * PictureDensity(landing, landingMedia, pixels), ahead: true);
            }
        }
    }

    /// <summary>Stage px per picture pixel where <paramref name="media"/> is drawn through <paramref name="screen"/> (cover fit).</summary>
    internal static double PictureDensity(in Matrix screen, Rect media, PixelSize pixels) =>
        Math.Max(media.Width / pixels.Width, media.Height / pixels.Height) * ShowpieceRenderer.Magnification(screen, media);

    private void RepaintChangedPictures()
    {
        foreach (var state in _cards)
        {
            if (state.Card.Item.Image is { } image && _pictures.IsChanged(image))
            {
                state.Card.Repaint();
            }
        }
        foreach (var state in _leaving)
        {
            if (state.Card.Item.Image is { } image && _pictures.IsChanged(image))
            {
                state.Card.Repaint();
            }
        }
        _pictures.ClearChanged();
    }

    /// <summary>Drops the copies of pictures no card shows any more.</summary>
    private void PrunePictures() =>
        _pictures.Prune(_cards.Select(c => c.Card.Item.Image).Concat(_leaving.Select(c => c.Card.Item.Image)));

    // Per-card state -------------------------------------------------------------------------------------------------------

    private sealed class CardState
    {
        public CardState(LayoutMorphCard card)
        {
            Card = card;
            for (var k = 0; k < Captions.Length; k++)
            {
                Captions[k] = new ShowpieceSpringChannel(0, 0.002, 0.02);
            }
        }

        public readonly LayoutMorphCard Card;
        public LayoutMorphPose From;
        public LayoutMorphPose Current;
        public ShowpieceSpringChannel Progress = new(1, ProgressRestDelta, ProgressRestSpeed);
        public ShowpieceSpringChannel Lift = new(0, 0.002, 0.02);
        public ShowpieceSpringChannel Dim = new(1, 0.002, 0.02);
        public ShowpieceSpringChannel Appear = new(1, 0.002, 0.02);
        public readonly ShowpieceSpringChannel[] Captions = new ShowpieceSpringChannel[4];
        public SpringSpec Spring = ShowpieceSprings.MorphProgress;
        public double Delay;
        public bool Waiting;
        public double Travel;
        public double TravelX;
        public double FlightLift;
        public double FlightTilt;
        public int CaptionTarget = -1;
        public double CaptionDelay;
        public bool CaptionWaiting;
        public bool IsNew;
        public bool IsLeaving;
        public double LeavingOpacity;
        public Size ArrangeSize;

        /// <summary>The card draws from the stage's bitmap cache.</summary>
        public bool Cached;

        /// <summary>The card's drawing changed in the previous frame.</summary>
        public bool WasDirty;

        public bool IsMoving => Waiting || Progress.IsMoving;

        public bool NeedsFrames => IsMoving || CaptionWaiting || Lift.IsMoving || Dim.IsMoving || Appear.IsMoving ||
                                   Captions[0].IsMoving || Captions[1].IsMoving || Captions[2].IsMoving || Captions[3].IsMoving;

        /// <summary>Starts a flight from where the card is now (its current lift and lean kept, so nothing jumps).</summary>
        public void Launch(in LayoutMorphPose target, double delay, in SpringSpec spring)
        {
            var from = Current;
            from.Z += FlightLift;
            from.RotZ += FlightTilt;
            from.UsesCamera = false;
            From = from;
            FlightLift = 0;
            FlightTilt = 0;
            Travel = Math.Sqrt((target.X - from.X) * (target.X - from.X) + (target.Y - from.Y) * (target.Y - from.Y));
            TravelX = target.X - from.X;
            Spring = spring;
            Progress.Jump(0);
            Delay = Math.Max(0, delay);
            Waiting = true;
        }

        public void JumpTo(in LayoutMorphPose target)
        {
            From = target;
            Current = target;
            Progress.Jump(1);
            Waiting = false;
            FlightLift = 0;
            FlightTilt = 0;
        }

        /// <summary>Lands at once (motion turned down mid-flight).</summary>
        public void Settle(in LayoutMorphPose target)
        {
            JumpTo(target);
            if (CaptionTarget >= 0)
            {
                JumpCaptions((LayoutMorphCaption)CaptionTarget);
            }
            Lift.Jump(Lift.Target);
            Dim.Jump(Dim.Target);
            Appear.Jump(1);
        }

        /// <summary>The old caption fades out now; the new one fades in once the card is nearly there.</summary>
        public void SwapCaptions(LayoutMorphCaption caption, double delay)
        {
            var next = (int)caption;
            for (var k = 0; k < Captions.Length; k++)
            {
                if (k != next && Captions[k].Value + Captions[k].Target > 0)
                {
                    Captions[k].SpringTo(0, ShowpieceSprings.MorphCaptionOut);
                }
            }
            CaptionTarget = next;
            CaptionWaiting = false;
            if (next >= 0 && !(Captions[next].Value >= 0.999 && Captions[next].Target >= 0.999))
            {
                Captions[next].SpringTo(0, ShowpieceSprings.MorphCaptionOut);
                CaptionDelay = delay;
                CaptionWaiting = true;
            }
        }

        public void JumpCaptions(LayoutMorphCaption caption)
        {
            var next = (int)caption;
            for (var k = 0; k < Captions.Length; k++)
            {
                Captions[k].Jump(k == next ? 1 : 0);
            }
            CaptionTarget = next;
            CaptionWaiting = false;
        }

        public void SetLift(double target, bool animate)
        {
            if (Math.Abs(Lift.Target - target) < 1e-9 && (Lift.IsMoving || Math.Abs(Lift.Value - target) < 1e-9))
            {
                return;
            }
            if (animate)
            {
                Lift.SpringTo(target, ShowpieceSprings.MorphLift);
            }
            else
            {
                Lift.Jump(target);
            }
        }

        public bool Step(double dt)
        {
            var moving = false;
            if (Waiting)
            {
                Delay -= dt;
                if (Delay <= 0)
                {
                    var over = -Delay;
                    Waiting = false;
                    Progress.SpringTo(1, Spring);
                    Progress.Step(over);
                }
                moving = true;
            }
            else if (Progress.IsMoving)
            {
                Progress.Step(dt);
                moving = true;
            }
            var velocity = Progress.IsMoving ? Progress.Velocity : 0;
            FlightLift = LayoutMorphEngine.FlightLift(Travel, velocity);
            FlightTilt = LayoutMorphEngine.FlightTilt(TravelX, velocity);
            if (CaptionWaiting)
            {
                CaptionDelay -= dt;
                if (CaptionDelay <= 0)
                {
                    var over = -CaptionDelay;
                    CaptionWaiting = false;
                    Captions[CaptionTarget].SpringTo(1, ShowpieceSprings.MorphCaptionIn);
                    Captions[CaptionTarget].Step(over);
                }
                moving = true;
            }
            for (var k = 0; k < Captions.Length; k++)
            {
                moving |= Captions[k].Step(dt);
            }
            moving |= Lift.Step(dt);
            moving |= Dim.Step(dt);
            moving |= Appear.Step(dt);
            return moving;
        }

        /// <summary>The pose to draw: the target once arrived, else the interpolation from where the flight began.</summary>
        public void Resolve(in LayoutMorphPose target)
        {
            if (!Waiting && !Progress.IsMoving)
            {
                Current = target;
                return;
            }
            Current = LayoutMorphPose.Lerp(From, target, Waiting ? 0 : Progress.Value);
        }
    }
}
