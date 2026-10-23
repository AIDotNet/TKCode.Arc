using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The stage of a <see cref="ReasoningTree"/>: one drawn layer for the column headers, the edges and the best path, with
/// a light focusable control per node on top and the tag above them. It owns the geometry (wide columns or the narrow
/// outline, chosen by its own width), the narrow rows' glide, the hover and focus emphasis and the tree's keyboard model
/// (one tab stop on the current thought; arrows, Home and End move it). Colors come from ink properties set by the theme.
/// </summary>
internal sealed class ReasoningTreeStage : Control
{
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> SurfaceMutedInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(SurfaceMutedInk));
    public static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(BorderInk));
    public static readonly StyledProperty<IBrush?> BorderStrongInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(BorderStrongInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(SecondaryInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(MutedInk));
    public static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(SuccessInk));
    public static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(FocusInk));
    public static readonly StyledProperty<IBrush?> BackgroundInkProperty =
        AvaloniaProperty.Register<ReasoningTreeStage, IBrush?>(nameof(BackgroundInk));

    /// <summary>The narrow stage's height change after a new beam (480ms).</summary>
    private const double HeightTween = 0.48;

    /// <summary>Tag entrance (160ms).</summary>
    private const double TagEnter = 0.16;

    private readonly List<ReasoningTreeNode> _nodes = new();
    private readonly ReasoningTreeTag _tag;
    private readonly ExplainerPath _path = new();
    private readonly Pen _pathPen = new() { Thickness = 2, LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round, DashStyle = new DashStyle() };
    private readonly Pen _sheenPen = new() { Thickness = 2.5, LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round, DashStyle = new DashStyle() };
    private readonly ReasoningTreePalette _palette = new();
    private ReasoningTree? _owner;
    private ReasoningTreeScene? _scene;
    private ReasoningTreeModel? _model;
    private ReasoningTreeResult? _result;
    private ReasoningTreeLabels _labels = new();
    private Rect[] _rects = [];
    private ExplainerPath[] _edges = [];
    private Pen[] _edgePens = [];
    private byte[] _edgeShow = [];
    private ExplainerValue[] _y = [];
    private bool[] _wasShown = [];
    private int[] _order = [];
    private string[] _digits = [];
    private string[] _columns = [];
    private ReasoningTreeLabels? _digitLabels;
    private ImmutablePen? _pathFullPen;
    private ExplainerValue _height;
    private double _wideHeight = ReasoningTreeLayout.ContentTop + ReasoningTreeLayout.RootHeight;
    private double _layoutWidth = -1;
    private bool _layoutDirty = true;
    private bool _heightSet;
    private bool _pathShown;
    private bool _sheenShown;
    private bool _pathFull;
    private int _hover = -1;
    private int _focused = -1;
    private int _active = -1;
    private bool _animate;

    static ReasoningTreeStage()
    {
        FocusableProperty.OverrideDefaultValue<ReasoningTreeStage>(false);
    }

    public ReasoningTreeStage()
    {
        ClipToBounds = false;
        _tag = new ReasoningTreeTag(this);
        RebuildPalette();
    }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }
    public IBrush? SurfaceMutedInk { get => GetValue(SurfaceMutedInkProperty); set => SetValue(SurfaceMutedInkProperty, value); }
    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    public IBrush? BorderStrongInk { get => GetValue(BorderStrongInkProperty); set => SetValue(BorderStrongInkProperty, value); }
    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }
    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }
    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }

    /// <summary>The colors, pens and text layouts in use.</summary>
    public ReasoningTreePalette Palette => _palette;

    /// <summary>The wall time the stage draws at.</summary>
    public double Now { get; private set; }

    /// <summary>True for the four-column layout.</summary>
    public bool IsWide { get; private set; } = true;

    /// <summary>The node that holds the tree's tab stop.</summary>
    public int Current { get; private set; }

    /// <summary>The nodes (index as in the model).</summary>
    public IReadOnlyList<ReasoningTreeNode> Nodes => _nodes;

    /// <summary>The tag (tests read it).</summary>
    public ReasoningTreeTag TagView => _tag;

    /// <summary>The node whose tag shows (-1 for none).</summary>
    public int Active => _active;

    /// <summary>The entrance progress of the tag (0..1).</summary>
    public double TagProgress =>
        !_animate ? 1 : ArcEasings.Standard.Ease(Math.Clamp((Now - _tag.ShownAt) / TagEnter, 0, 1));

    /// <summary>The layout rectangle of a node (its resting place; narrow rows may still glide to it).</summary>
    public Rect RectOf(int index) => index >= 0 && index < _rects.Length ? _rects[index] : default;

    /// <summary>The current top of a node, glide included.</summary>
    public double CurrentTop(int index) =>
        index >= 0 && index < _rects.Length ? (IsWide ? _rects[index].Y : _y[index].Value(Now)) : 0;

    public ReasoningTreeNodeData? DataOf(int index) => _model is { } m && index >= 0 && index < m.Nodes.Count ? m.Nodes[index] : null;

    public ReasoningTreeNodeState? StateOf(int index) =>
        _scene is { } s && index >= 0 && index < s.Nodes.Length ? s.Nodes[index] : null;

    /// <summary>Connects the stage to its card and the scene it draws.</summary>
    public void Attach(ReasoningTree owner, ReasoningTreeScene scene)
    {
        _owner = owner;
        _scene = scene;
    }

    /// <summary>Takes a new snapshot and search: rebuilds the node controls when the tree changed.</summary>
    public void SetModel(ReasoningTreeModel model, IReadOnlyList<ReasoningTreeResult> allResults, ReasoningTreeResult result,
        ReasoningTreeLabels labels)
    {
        var treeChanged = !ReferenceEquals(model, _model);
        var resultChanged = !ReferenceEquals(result, _result);
        _model = model;
        _result = result;
        _labels = labels;
        if (treeChanged)
        {
            RebuildNodes(model.Nodes.Count);
            _wideHeight = ReasoningTreeLayout.WideHeight(model, allResults);
            Current = 0;
            _hover = -1;
            _focused = -1;
            SetActive(-1);
        }
        if (treeChanged || resultChanged)
        {
            _order = ReasoningTreeLayout.OutlineOrder(model, result);
            var target = ReasoningTreeLayout.NarrowHeight(result);
            if (!_heightSet || !_animate || IsWide)
            {
                _height.Jump(target);
                _heightSet = true;
            }
            else if (Math.Abs(_height.Target - target) > 0.5)
            {
                _height.Tween(target, Now, HeightTween, ArcEasings.Standard);
            }
            _layoutDirty = true;
            InvalidateMeasure();
        }
        UpdateTabStops();
    }

    /// <summary>
    /// Brings the nodes in line with the scene after a beat: visibility, accessible names, the narrow rows' places (new
    /// rows glide the ones below down), the best path and the tag.
    /// </summary>
    public void Sync(double now, bool animate)
    {
        Now = now;
        _animate = animate;
        if (_model is null || _scene is null)
        {
            return;
        }
        if (Current > 0 && Current < _nodes.Count && !_scene.Nodes[Current].IsShown)
        {
            // The current thought was hidden (a restart): the tab stop goes back to the question, with focus if it had it.
            var hadFocus = _nodes[Current].IsFocused;
            var keyboard = _nodes[Current].IsKeyboardFocused;
            Current = 0;
            UpdateTabStops();
            if (hadFocus)
            {
                FocusNode(0, keyboard ? NavigationMethod.Directional : NavigationMethod.Unspecified);
            }
        }
        for (var i = 0; i < _nodes.Count; i++)
        {
            var state = _scene.Nodes[i];
            var node = _nodes[i];
            node.Focusable = state.IsShown || i == 0;
            node.IsVisible = i == 0 || state.IsShown || now < state.CollapseAt;
            AutomationProperties.SetName(node, NameOf(i));
        }
        if (_hover > 0 && !_scene.Nodes[_hover].IsShown)
        {
            _hover = -1;
        }
        if (_focused > 0 && !_scene.Nodes[_focused].IsShown)
        {
            _focused = -1;
        }
        UpdateActive();
        if (!IsWide && UpdateNarrowTargets(animate))
        {
            InvalidateArrange();
        }
        BuildPath();
        RefreshTag();
        UpdateFrame(now);
        InvalidateAll();
    }

    /// <summary>
    /// Moves the stage's clock to <paramref name="now"/>: collapses nodes whose hide finished, glides rows, updates the
    /// partial strokes and redraws what moves. Returns the seconds it still needs frames for.
    /// </summary>
    public double OnFrame(double now)
    {
        Now = now;
        if (_scene is null || _model is null)
        {
            return 0;
        }
        var busy = _scene.IsBusy(now);
        var gliding = false;
        for (var i = 1; i < _nodes.Count; i++)
        {
            var state = _scene.Nodes[i];
            var visible = state.IsShown || now < state.CollapseAt;
            if (_nodes[i].IsVisible != visible)
            {
                _nodes[i].IsVisible = visible;
            }
            if (!IsWide && i < _y.Length && _y[i].IsMoving(now))
            {
                gliding = true;
            }
        }
        var growing = !IsWide && _height.IsMoving(now);
        if (growing)
        {
            InvalidateMeasure();
        }
        var tagMoving = _tag.IsVisible && _animate && now - _tag.ShownAt < TagEnter + 0.02;
        if (busy || gliding || growing || tagMoving)
        {
            UpdateFrame(now);
            InvalidateAll();
        }
        var remaining = Math.Max(0, _scene.BusyUntil - now);
        if (gliding || growing)
        {
            remaining = Math.Max(remaining, 0.1);
        }
        if (tagMoving)
        {
            remaining = Math.Max(remaining, TagEnter);
        }
        foreach (var state in _scene.Nodes)
        {
            if (!state.IsShown && now < state.CollapseAt)
            {
                // A hidden node still fading out: keep frames until it collapses.
                remaining = Math.Max(remaining, state.CollapseAt - now);
            }
        }
        return remaining;
    }

    /// <summary>The digits for <paramref name="hundredths"/> (formatted once per labels object).</summary>
    public string DigitText(int hundredths)
    {
        EnsureTexts();
        return _digits[Math.Clamp(hundredths, 0, 99)];
    }

    /// <summary>The header of a column (0 = the question), formatted once per labels object.</summary>
    public string ColumnName(int column)
    {
        EnsureTexts();
        return _columns[Math.Clamp(column, 0, _columns.Length - 1)];
    }

    // The digits and headers drawn every frame are formatted once per labels object, so frames allocate no strings.
    private void EnsureTexts()
    {
        if (ReferenceEquals(_digitLabels, _labels) && _digits.Length == 100)
        {
            return;
        }
        _digitLabels = _labels;
        _digits = new string[100];
        for (var i = 0; i < 100; i++)
        {
            _digits[i] = _labels.FormatScore(i / 100.0);
        }
        _columns = new string[ReasoningTreeScene.ColumnCount];
        for (var c = 0; c < _columns.Length; c++)
        {
            _columns[c] = c == 0 ? _labels.QuestionColumn : _labels.DepthColumn(c);
        }
    }

    /// <summary>The accessible name of a node: the full question, or the full thought with its score and fate.</summary>
    public string NameOf(int index)
    {
        if (DataOf(index) is not { } data)
        {
            return string.Empty;
        }
        return index == 0 ? _labels.QuestionName(data.Text) : _labels.ThoughtName(data.Text, MetaOf(index));
    }

    /// <summary>The second line of a node's tag: "Question", "Not scored yet" or the score with the fate.</summary>
    public string MetaOf(int index)
    {
        if (DataOf(index) is not { } data || StateOf(index) is not { } state)
        {
            return string.Empty;
        }
        if (index == 0)
        {
            return _labels.QuestionMeta;
        }
        var fate = state.Fate switch
        {
            ReasoningThoughtFate.Scored => _labels.FateScored,
            ReasoningThoughtFate.Kept => _labels.FateKept,
            ReasoningThoughtFate.Pruned => _labels.FatePruned,
            ReasoningThoughtFate.OnPath => _labels.FateOnPath,
            ReasoningThoughtFate.Answer => _labels.FateAnswer,
            _ => null
        };
        return fate is null ? _labels.NotScored : _labels.ScoreMeta(DigitText(data.Hundredths), fate);
    }

    /// <summary>The help text of a thought: its depth and its place among its shown siblings.</summary>
    public string? PositionOf(int index)
    {
        if (index <= 0 || DataOf(index) is not { } data || _model is null)
        {
            return null;
        }
        var siblings = _model.Nodes[data.Parent].Children.Where(IsShownNode).ToList();
        var position = siblings.IndexOf(index);
        return position < 0 ? null : _labels.ThoughtPosition(data.Depth, position + 1, siblings.Count);
    }

    /// <summary>Moves the tab stop (and focus) to <paramref name="index"/>.</summary>
    public void FocusNode(int index, NavigationMethod method)
    {
        if (index < 0 || index >= _nodes.Count)
        {
            return;
        }
        Current = index;
        UpdateTabStops();
        _nodes[index].Focus(method);
    }

    /// <summary>The tree's keys: arrows, Home and End move the roving focus. Returns true when the key belongs to the tree.</summary>
    public bool Navigate(ReasoningTreeNode from, Key key)
    {
        if (_model is null || _scene is null)
        {
            return false;
        }
        var target = key switch
        {
            Key.Up => Step(from.Index, -1),
            Key.Down => Step(from.Index, 1),
            Key.Right => BestChild(from.Index),
            Key.Left => from.Index > 0 ? _model.Nodes[from.Index].Parent : -1,
            Key.Home => 0,
            Key.End => Last(),
            _ => -2
        };
        if (target == -2)
        {
            return false;
        }
        if (target >= 0 && target != from.Index)
        {
            FocusNode(target, NavigationMethod.Directional);
        }
        return true;
    }

    /// <summary>A node took or lost focus.</summary>
    public void OnNodeFocus(ReasoningTreeNode node, bool focused)
    {
        if (focused)
        {
            _focused = node.Index;
            if (Current != node.Index)
            {
                Current = node.Index;
                UpdateTabStops();
            }
        }
        else if (_focused == node.Index)
        {
            _focused = -1;
        }
        UpdateActive();
    }

    /// <summary>The pointer entered or left a node.</summary>
    public void OnNodeHover(ReasoningTreeNode node, bool over)
    {
        if (over)
        {
            _hover = node.Index;
        }
        else if (_hover == node.Index)
        {
            _hover = -1;
        }
        UpdateActive();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReasoningTreeStageAutomationPeer(this);

    /// <summary>The accessible name of the tree.</summary>
    public string TreeName => _model is null ? string.Empty : _labels.TreeName(_model.QuestionDetail);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == typeof(ReasoningTreeStage) || change.Property == TextElement.FontFamilyProperty)
        {
            RebuildPalette();
            _tag.Refresh();
            InvalidateAll();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 694;
        EnsureLayout(width);
        foreach (var node in _nodes)
        {
            var rect = RectOf(node.Index);
            node.Measure(rect.Size);
        }
        _tag.Measure(Size.Infinity);
        return new Size(width, IsWide ? _wideHeight : Math.Max(0, _height.Value(Now)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.5)
        {
            var wasWide = IsWide;
            EnsureLayout(finalSize.Width);
            if (wasWide != IsWide)
            {
                InvalidateMeasure();
            }
        }
        foreach (var node in _nodes)
        {
            node.Arrange(RectOf(node.Index));
        }
        ArrangeTag(finalSize);
        UpdateFrame(Now);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_model is null || _scene is null)
        {
            return;
        }
        var now = Now;
        if (IsWide)
        {
            DrawHeaders(context, now);
            DrawWideEdges(context);
        }
        else
        {
            DrawNarrowEdges(context, now);
        }
        if (_pathShown)
        {
            if (_pathFull)
            {
                context.DrawGeometry(null, _pathFullPen, _path.Geometry);
            }
            else
            {
                context.DrawGeometry(null, _pathPen, _path.Geometry);
            }
        }
        if (_sheenShown)
        {
            using (context.PushOpacity(Math.Clamp(_scene.SheenOpacity.Value(now), 0, 1)))
            {
                context.DrawGeometry(null, _sheenPen, _path.Geometry);
            }
        }
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private void RebuildPalette()
    {
        _palette.Capture(
            ColorOf(SurfaceInk, Colors.White),
            ColorOf(SurfaceMutedInk, Color.FromRgb(0xF7, 0xF7, 0xF7)),
            ColorOf(BorderInk, Color.FromRgb(0xE8, 0xE8, 0xE8)),
            ColorOf(BorderStrongInk, Color.FromRgb(0xC8, 0xC8, 0xC8)),
            ColorOf(ForegroundInk, Color.FromRgb(0x17, 0x17, 0x17)),
            ColorOf(SecondaryInk, Color.FromRgb(0x5E, 0x5E, 0x5E)),
            ColorOf(MutedInk, Color.FromRgb(0x84, 0x84, 0x84)),
            ColorOf(SuccessInk, Color.FromRgb(0x1A, 0x7F, 0x37)),
            ColorOf(AccentInk, Color.FromRgb(0x1F, 0x7D, 0xCF)),
            ColorOf(FocusInk, Color.FromArgb(0xB8, 0x17, 0x17, 0x17)),
            ColorOf(BackgroundInk, Colors.White),
            TextElement.GetFontFamily(this));
        _pathPen.Brush = _palette.Brush(_palette.Accent);
        _sheenPen.Brush = _palette.Brush(_palette.Sheen);
        _pathFullPen = new ImmutablePen(_palette.Brush(_palette.Accent), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        foreach (var pen in _edgePens)
        {
            pen.Brush = _palette.Brush(_palette.BorderStrong);
        }
    }

    private void RebuildNodes(int count)
    {
        foreach (var node in _nodes)
        {
            VisualChildren.Remove(node);
            LogicalChildren.Remove(node);
        }
        VisualChildren.Remove(_tag);
        LogicalChildren.Remove(_tag);
        _nodes.Clear();
        for (var i = 0; i < count; i++)
        {
            var node = new ReasoningTreeNode(this, i) { IsVisible = i == 0 };
            _nodes.Add(node);
            VisualChildren.Add(node);
            LogicalChildren.Add(node);
        }
        VisualChildren.Add(_tag);
        LogicalChildren.Add(_tag);
        _y = new ExplainerValue[count];
        _wasShown = new bool[count];
        _edges = new ExplainerPath[count];
        _edgePens = new Pen[count];
        _edgeShow = new byte[count];
        var brush = _palette.Brush(_palette.BorderStrong);
        for (var i = 0; i < count; i++)
        {
            _edges[i] = new ExplainerPath();
            _edgePens[i] = new Pen(brush, 1.25, lineCap: PenLineCap.Round) { DashStyle = new DashStyle() };
        }
        _rects = new Rect[count];
        _layoutDirty = true;
    }

    private void EnsureLayout(double width)
    {
        var wide = ReasoningTreeLayout.IsWide(width);
        if (!_layoutDirty && Math.Abs(width - _layoutWidth) <= 0.5 && wide == IsWide)
        {
            return;
        }
        var modeChanged = wide != IsWide;
        _layoutDirty = false;
        _layoutWidth = width;
        IsWide = wide;
        if (_model is null || _result is null)
        {
            return;
        }
        if (wide)
        {
            _rects = ReasoningTreeLayout.Wide(_model, _result, width, _wideHeight);
            for (var i = 1; i < _rects.Length; i++)
            {
                var parent = _model.Nodes[i].Parent;
                ReasoningTreeLayout.WideEdge(_edges[i], _rects[parent], _rects[i]);
            }
            for (var i = 0; i < _y.Length; i++)
            {
                _y[i].Jump(_rects[i].Y);
            }
        }
        else
        {
            if (modeChanged)
            {
                _height.Jump(ReasoningTreeLayout.NarrowHeight(_result));
            }
            UpdateNarrowTargets(animate: false, rebuild: true);
        }
        BuildPath();
        foreach (var node in _nodes)
        {
            node.InvalidateVisual();
        }
        InvalidateVisual();
    }

    /// <summary>
    /// The narrow rows' resting places from the shown set: each shown thought takes the next row in outline order, a
    /// hidden one waits where it will be inserted. Rows that were shown glide (critically damped) to a new place; new and
    /// hidden ones jump. Returns true when a place changed.
    /// </summary>
    private bool UpdateNarrowTargets(bool animate, bool rebuild = false)
    {
        if (_model is null || _scene is null || _result is null)
        {
            return false;
        }
        var width = _layoutWidth > 0 ? _layoutWidth : 400;
        var changed = rebuild;
        var row = 0;
        var placed = new bool[_model.Nodes.Count];
        foreach (var index in _order)
        {
            var data = _model.Nodes[index];
            placed[index] = true;
            if (index == 0)
            {
                _rects[0] = ReasoningTreeLayout.NarrowRect(data, 0, width);
                _y[0].Jump(0);
                continue;
            }
            var shown = _scene.Nodes[index].IsShown;
            var rect = ReasoningTreeLayout.NarrowRect(data, row, width);
            if (shown)
            {
                row++;
            }
            if (Math.Abs(rect.Y - _y[index].Target) > 0.01 || rebuild)
            {
                changed = true;
                if (animate && shown && _wasShown[index] && !rebuild)
                {
                    _y[index].Spring(rect.Y, Now, ArcMotionPresets.Get(ArcSpring.Settle));
                }
                else
                {
                    _y[index].Jump(rect.Y);
                }
            }
            else if (!shown || !_wasShown[index])
            {
                _y[index].Jump(rect.Y);
            }
            _rects[index] = rect;
            _wasShown[index] = shown;
        }
        for (var i = 1; i < placed.Length; i++)
        {
            if (!placed[i])
            {
                _rects[i] = ReasoningTreeLayout.NarrowRect(_model.Nodes[i], row, width);
                _y[i].Jump(_rects[i].Y);
                _wasShown[i] = false;
            }
        }
        return changed;
    }

    private void BuildPath()
    {
        if (_scene is null || _result is null || !_scene.HasPath || _rects.Length == 0)
        {
            _path.Clear();
            return;
        }
        var chain = new List<Rect>(_result.Path.Length + 1) { _rects[0] };
        foreach (var index in _result.Path)
        {
            chain.Add(_rects[index]);
        }
        if (IsWide)
        {
            ReasoningTreeLayout.WidePath(_path, chain);
        }
        else
        {
            ReasoningTreeLayout.NarrowPath(_path, chain);
        }
    }

    /// <summary>Sets the glide offsets and the partial strokes for <paramref name="now"/> (outside the render pass).</summary>
    private void UpdateFrame(double now)
    {
        if (_scene is null || _model is null)
        {
            return;
        }
        for (var i = 0; i < _nodes.Count; i++)
        {
            var offset = IsWide || i >= _y.Length ? 0 : _y[i].Value(now) - _rects[i].Y;
            if (Math.Abs(_nodes[i].Translate.Y - offset) > 0.01)
            {
                _nodes[i].Translate.Y = offset;
            }
        }
        if (IsWide)
        {
            for (var i = 1; i < _edges.Length && i < _scene.Nodes.Length; i++)
            {
                var v = _scene.Nodes[i].Edge.Value(now);
                var visible = _scene.Nodes[i].IsShown || now < _scene.Nodes[i].CollapseAt;
                if (!visible || v <= 0.002)
                {
                    _edgeShow[i] = 0;
                }
                else if (v >= 0.998)
                {
                    _edgeShow[i] = 2;
                }
                else
                {
                    _edgeShow[i] = _edges[i].ShowPart(_edgePens[i], 0, v * _edges[i].Length) ? (byte)1 : (byte)0;
                }
            }
        }
        _pathShown = false;
        _sheenShown = false;
        if (_scene.HasPath && _path.Length > 0)
        {
            var v = Math.Clamp(_scene.PathDrawn.Value(now), 0, 1);
            _pathFull = v >= 0.999;
            _pathShown = _pathFull || _path.ShowPart(_pathPen, 0, v * _path.Length);
            var sheen = _scene.SheenOpacity.Value(now);
            if (sheen > 0.005 && v > 0)
            {
                _sheenShown = _path.ShowPart(_sheenPen, (v - ReasoningTreeScene.SheenLength) * _path.Length, v * _path.Length);
            }
        }
        if (_tag.IsVisible && _tag.Anchor >= 0)
        {
            var rise = (_tag.IsBelow ? -ReasoningTreeTag.Rise : ReasoningTreeTag.Rise) * (1 - TagProgress);
            var glide = _tag.Anchor < _nodes.Count ? _nodes[_tag.Anchor].Translate.Y : 0;
            var y = rise + glide;
            if (Math.Abs(_tag.Translate.Y - y) > 0.01)
            {
                _tag.Translate.Y = y;
            }
        }
    }

    private void InvalidateAll()
    {
        InvalidateVisual();
        foreach (var node in _nodes)
        {
            if (node.IsVisible)
            {
                node.InvalidateVisual();
            }
        }
        if (_tag.IsVisible)
        {
            _tag.InvalidateVisual();
        }
    }

    private void DrawHeaders(DrawingContext context, double now)
    {
        var palette = _palette;
        var width = _layoutWidth;
        for (var c = 0; c < ReasoningTreeScene.ColumnCount; c++)
        {
            var (x, w) = ReasoningTreeLayout.Column(c, width);
            var name = ColumnName(c);
            var active = Math.Clamp(_scene!.ColumnActive[c].Value(now), 0, 1);
            if (active < 0.999)
            {
                var muted = palette.Texts.Get(name, palette.Medium, 12, palette.Brush(palette.TextMuted), w * 0.6, 1,
                    ReasoningTreeLayout.HeaderHeight, letterSpacing: -0.12);
                using (context.PushOpacity(1 - active))
                {
                    muted.Draw(context, new Point(x, 0));
                }
            }
            if (active > 0.001)
            {
                var strong = palette.Texts.Get(name, palette.Medium, 12, palette.Brush(palette.Foreground), w * 0.6, 1,
                    ReasoningTreeLayout.HeaderHeight, letterSpacing: -0.12);
                using (context.PushOpacity(active))
                {
                    strong.Draw(context, new Point(x, 0));
                }
            }
            var count = _scene.CountText[c];
            var countOpacity = Math.Clamp(_scene.CountOpacity[c].Value(now), 0, 1);
            if (count is not null && countOpacity > 0.005)
            {
                var slot = w * 0.55;
                var layout = palette.Texts.Get(count, palette.Regular, 12, palette.Brush(palette.TextMuted), slot, 1,
                    ReasoningTreeLayout.HeaderHeight, Avalonia.Media.TextAlignment.Right, tabular: true);
                using (context.PushOpacity(countOpacity))
                {
                    layout.Draw(context, new Point(x + w - slot, 0));
                }
            }
        }
    }

    private void DrawWideEdges(DrawingContext context)
    {
        for (var i = 1; i < _edges.Length && i < _edgeShow.Length; i++)
        {
            switch (_edgeShow[i])
            {
                case 1:
                    context.DrawGeometry(null, _edgePens[i], _edges[i].Geometry);
                    break;
                case 2:
                    context.DrawGeometry(null, _palette.EdgePen, _edges[i].Geometry);
                    break;
            }
        }
    }

    private void DrawNarrowEdges(DrawingContext context, double now)
    {
        if (_model is null || _scene is null)
        {
            return;
        }
        var pen = _palette.EdgePen;
        for (var i = 1; i < _model.Nodes.Count; i++)
        {
            var state = _scene.Nodes[i];
            if (!state.IsShown && !(now < state.CollapseAt))
            {
                continue;
            }
            var v = Math.Clamp(state.Edge.Value(now), 0, 1);
            if (v <= 0.002)
            {
                continue;
            }
            var parent = _model.Nodes[i].Parent;
            var px = _rects[parent].X + ReasoningTreeLayout.ElbowInset;
            var top = _y[parent].Value(now) + _rects[parent].Height;
            var cy = _y[i].Value(now) + _rects[i].Height / 2;
            DrawElbow(context, pen, px, top, cy, _rects[i].X, v);
        }
    }

    /// <summary>
    /// Draws the first <paramref name="fraction"/> of an elbow: down from (x, top) to the corner, round the corner and
    /// right to <paramref name="end"/> at <paramref name="cy"/>. Straight lines only, so nothing is allocated.
    /// </summary>
    private static void DrawElbow(DrawingContext context, IPen pen, double x, double top, double cy, double end, double fraction)
    {
        var r = Math.Min(ReasoningTreeLayout.ElbowRadius, Math.Max(0, Math.Min(cy - top, end - x)));
        var down = Math.Max(0, cy - r - top);
        var corner = Math.PI * r / 2;
        var across = Math.Max(0, end - x - r);
        var total = down + corner + across;
        if (total <= 0)
        {
            return;
        }
        var left = fraction * total;
        var d = Math.Min(left, down);
        context.DrawLine(pen, new Point(x, top), new Point(x, top + d));
        left -= d;
        if (left <= 0)
        {
            return;
        }
        // The corner as four short chords around (x + r, cy - r).
        const int pieces = 4;
        var previous = new Point(x, cy - r);
        for (var k = 1; k <= pieces && left > 0; k++)
        {
            var angle = Math.PI / 2 * k / pieces;
            var point = new Point(x + r - r * Math.Cos(angle), cy - r + r * Math.Sin(angle));
            var piece = corner / pieces;
            if (left < piece)
            {
                var t = left / piece;
                point = new Point(previous.X + (point.X - previous.X) * t, previous.Y + (point.Y - previous.Y) * t);
            }
            context.DrawLine(pen, previous, point);
            left -= piece;
            previous = point;
        }
        if (left <= 0)
        {
            return;
        }
        context.DrawLine(pen, new Point(x + r, cy), new Point(x + r + Math.Min(left, across), cy));
    }

    private void UpdateTabStops()
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            KeyboardNavigation.SetIsTabStop(_nodes[i], i == Current);
        }
    }

    private bool IsShownNode(int index) => _scene is { } s && index >= 0 && index < s.Nodes.Length && (index == 0 || s.Nodes[index].IsShown);

    /// <summary>The previous or next shown node: in the same column (wide, top to bottom) or the outline (narrow).</summary>
    private int Step(int from, int direction)
    {
        if (_model is null)
        {
            return -1;
        }
        List<int> list;
        if (IsWide)
        {
            var depth = _model.Nodes[from].Depth;
            list = _model.Nodes.Where(n => n.Depth == depth && IsShownNode(n.Index)).Select(n => n.Index)
                         .OrderBy(i => _rects[i].Y).ThenBy(i => i).ToList();
        }
        else
        {
            list = _order.Where(IsShownNode).ToList();
        }
        var at = list.IndexOf(from);
        var next = at + direction;
        return at < 0 || next < 0 || next >= list.Count ? -1 : list[next];
    }

    /// <summary>The child on the winning path, else the best scored shown child, else the first shown child.</summary>
    private int BestChild(int from)
    {
        if (_model is null || _scene is null)
        {
            return -1;
        }
        var children = _model.Nodes[from].Children.Where(IsShownNode).ToList();
        if (children.Count == 0)
        {
            return -1;
        }
        foreach (var child in children)
        {
            if (_scene.Nodes[child].Fate is ReasoningThoughtFate.OnPath or ReasoningThoughtFate.Answer)
            {
                return child;
            }
        }
        var scored = children.Where(c => _scene.Nodes[c].Fate is not ReasoningThoughtFate.Candidate).ToList();
        return scored.Count > 0 ? ReasoningTreeSearch.Best(_model, scored) : children[0];
    }

    /// <summary>The answer once found; before that the first shown thought of the deepest shown depth.</summary>
    private int Last()
    {
        if (_model is null || _scene is null)
        {
            return -1;
        }
        for (var i = 1; i < _scene.Nodes.Length; i++)
        {
            if (_scene.Nodes[i].Fate == ReasoningThoughtFate.Answer)
            {
                return i;
            }
        }
        var shown = _model.Nodes.Where(n => n.Index > 0 && IsShownNode(n.Index)).ToList();
        if (shown.Count == 0)
        {
            return 0;
        }
        var deepest = shown.Max(n => n.Depth);
        return shown.Where(n => n.Depth == deepest)
                    .OrderBy(n => IsWide ? _rects[n.Index].Y : Array.IndexOf(_order, n.Index))
                    .First().Index;
    }

    private void UpdateActive()
    {
        SetActive(_hover >= 0 ? _hover : _focused);
    }

    private void SetActive(int index)
    {
        if (index == _active)
        {
            return;
        }
        var previous = _active;
        _active = index;
        if (_scene is not null)
        {
            _scene.SetHover(previous, false, Now, _animate);
            _scene.SetHover(index, true, Now, _animate);
        }
        _tag.ShownAt = Now;
        RefreshTag();
        _owner?.KeepFrames(0.25);
        if (previous >= 0 && previous < _nodes.Count)
        {
            _nodes[previous].InvalidateVisual();
        }
        if (index >= 0 && index < _nodes.Count)
        {
            _nodes[index].InvalidateVisual();
        }
    }

    private void RefreshTag()
    {
        if (_active < 0 || DataOf(_active) is not { } data)
        {
            _tag.Show(-1, _tag.Body, _tag.Meta);
            return;
        }
        _tag.Show(_active, data.Text, MetaOf(_active));
        InvalidateArrange();
    }

    private void ArrangeTag(Size stage)
    {
        if (_tag.Anchor < 0 || _tag.Anchor >= _rects.Length)
        {
            _tag.Arrange(default);
            return;
        }
        var node = _rects[_tag.Anchor];
        var size = _tag.DesiredSize;
        var y = node.Y - ReasoningTreeTag.Offset - size.Height;
        _tag.IsBelow = y < 0;
        if (_tag.IsBelow)
        {
            y = node.Bottom + ReasoningTreeTag.Offset;
        }
        double x;
        var depth = DataOf(_tag.Anchor)?.Depth ?? 0;
        if (!IsWide || depth == 0)
        {
            x = node.X;
        }
        else if (depth == ReasoningTreeModel.MaxDepth)
        {
            x = node.Right - size.Width;
        }
        else
        {
            x = node.Center.X - size.Width / 2;
        }
        x = Math.Max(0, Math.Min(x, stage.Width - size.Width));
        _tag.Arrange(new Rect(new Point(x, y), size));
    }
}

/// <summary>The stage as a tree named after the question; its items are the shown thoughts.</summary>
internal sealed class ReasoningTreeStageAutomationPeer : ControlAutomationPeer
{
    public ReasoningTreeStageAutomationPeer(ReasoningTreeStage owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tree;

    protected override string? GetNameCore() => ((ReasoningTreeStage)Owner).TreeName;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
