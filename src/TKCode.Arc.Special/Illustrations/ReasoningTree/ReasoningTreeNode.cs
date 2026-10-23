using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One node of the reasoning tree (the question or a thought): a light focusable control that draws its own card, label,
/// score bar, counting digits and check from the scene's channels, so the tree is keyboard and screen-reader reachable
/// without a templated control per thought. Its automation peer is a tree item named with the full text, the score and
/// the fate. Keys go to the stage, which moves the roving focus.
/// </summary>
internal sealed class ReasoningTreeNode : Control
{
    // Card geometry (Arc Reasoning tree, design reference).
    private const double Radius = 12;
    private const double WidePadding = 12;
    private const double NarrowPadding = 10;
    private const double LabelLine = 16;
    private const double ScoreLine = 14;
    private const double RowGap = 7;
    private const double DigitsWidth = 26;
    private const double DigitsGap = 8;
    private const double CheckSize = 14;
    private const double CheckGap = 6;
    private const double TrackHeight = 3;
    private const double NarrowTrackHeight = 2;
    private const double NarrowTrackBottom = 8;
    private const double NarrowBottomPadding = 6;
    private const double FocusOffset = 3;

    private readonly ReasoningTreeStage _stage;

    public ReasoningTreeNode(ReasoningTreeStage stage, int index)
    {
        _stage = stage;
        Index = index;
        Focusable = true;
        FocusAdorner = null;
        ClipToBounds = false;
        RenderTransform = Translate;
    }

    /// <summary>The node's index in the model (0 = the question).</summary>
    public int Index { get; }

    /// <summary>The glide offset (narrow layout); mutated in place, never replaced.</summary>
    public TranslateTransform Translate { get; } = new();

    /// <summary>True while the node has keyboard focus that should show a ring.</summary>
    public bool IsKeyboardFocused => IsFocused && Classes.Contains(":focus-visible");

    protected override AutomationPeer OnCreateAutomationPeer() => new ReasoningTreeNodeAutomationPeer(this, _stage);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _stage.OnNodeFocus(this, true);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _stage.OnNodeFocus(this, false);
        InvalidateVisual();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _stage.OnNodeHover(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _stage.OnNodeHover(this, false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _stage.Navigate(this, e.Key))
        {
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var palette = _stage.Palette;
        var state = _stage.StateOf(Index);
        var data = _stage.DataOf(Index);
        if (state is null || data is null)
        {
            return;
        }
        var now = _stage.Now;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var isQuestion = Index == 0;
        var hover = state.Hover.Value(now);
        var opacity = state.Opacity.Value(now);
        if (state.Fate == ReasoningThoughtFate.Pruned)
        {
            opacity += hover * Math.Max(0, 0.8 - opacity);
        }
        var press = ReasoningTreeScene.Press(state, now);
        var scale = press == 1
            ? Matrix.Identity
            : Matrix.CreateTranslation(-w / 2, -h / 2) * Matrix.CreateScale(press, press) * Matrix.CreateTranslation(w / 2, h / 2);
        using (context.PushTransform(scale))
        {
            using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
            {
                var answer = state.AnswerMix.Value(now);
                var path = state.Path.Value(now);
                var fill = ReasoningTreePalette.Mix(isQuestion ? palette.SurfaceMuted : palette.Surface, palette.AnswerFill, answer);
                var border = ReasoningTreePalette.Mix(palette.Border, palette.BorderStrong, hover * (1 - path) * (1 - answer));
                border = ReasoningTreePalette.Mix(border, palette.PathBorder, path);
                border = ReasoningTreePalette.Mix(border, palette.AnswerBorder, answer);
                context.DrawRectangle(palette.Brush(fill), palette.BorderPen(border),
                    new RoundedRect(new Rect(0.5, 0.5, Math.Max(0, w - 1), Math.Max(0, h - 1)), Radius - 0.5));
                if (isQuestion)
                {
                    DrawQuestion(context, palette, data, w, h);
                }
                else if (_stage.IsWide)
                {
                    DrawWideThought(context, palette, state, data, w, now);
                }
                else
                {
                    DrawNarrowThought(context, palette, state, data, w, h, now);
                }
            }
        }
        if (IsKeyboardFocused)
        {
            context.DrawRectangle(null, palette.FocusPen,
                new RoundedRect(new Rect(-FocusOffset, -FocusOffset, w + 2 * FocusOffset, h + 2 * FocusOffset), Radius + FocusOffset));
        }
    }

    private void DrawQuestion(DrawingContext context, ReasoningTreePalette palette, ReasoningTreeNodeData data, double w, double h)
    {
        var wide = _stage.IsWide;
        var layout = palette.Texts.Get(data.Label, palette.Medium, 14, palette.Brush(palette.Foreground), w - 2 * WidePadding,
            wide ? 3 : 1, 20, letterSpacing: -0.14);
        layout.Draw(context, new Point(WidePadding, Math.Round((h - layout.Height) / 2)));
    }

    private void DrawWideThought(DrawingContext context, ReasoningTreePalette palette, ReasoningTreeNodeState state,
        ReasoningTreeNodeData data, double w, double now)
    {
        var inner = w - 2 * WidePadding;
        var top = (Bounds.Height - (LabelLine + RowGap + ScoreLine)) / 2;
        var reserve = data.Depth == ReasoningTreeModel.MaxDepth ? CheckSize + CheckGap : 0;
        var label = palette.Texts.Get(data.Label, palette.Medium, 12, palette.Brush(palette.Foreground), inner - reserve, 1, LabelLine,
            letterSpacing: -0.12);
        label.Draw(context, new Point(WidePadding, top));
        DrawCheck(context, palette, state, w - WidePadding - CheckSize / 2, top + LabelLine / 2, now);
        var rowTop = top + LabelLine + RowGap;
        var trackWidth = Math.Max(0, inner - DigitsGap - DigitsWidth);
        var trackY = rowTop + (ScoreLine - TrackHeight) / 2;
        DrawBar(context, palette, state, new Rect(WidePadding, trackY, trackWidth, TrackHeight), now);
        DrawDigits(context, palette, state, w - WidePadding - DigitsWidth, rowTop, now);
    }

    private void DrawNarrowThought(DrawingContext context, ReasoningTreePalette palette, ReasoningTreeNodeState state,
        ReasoningTreeNodeData data, double w, double h, double now)
    {
        var rowCenter = (h - NarrowBottomPadding) / 2;
        var digitsX = w - NarrowPadding - DigitsWidth;
        var isAnswer = state.Fate == ReasoningThoughtFate.Answer;
        var reserve = isAnswer ? CheckSize + CheckGap : 0;
        var labelWidth = Math.Max(0, digitsX - DigitsGap - reserve - NarrowPadding);
        var label = palette.Texts.Get(data.Label, palette.Medium, 12, palette.Brush(palette.Foreground), labelWidth, 1, LabelLine,
            letterSpacing: -0.12);
        label.Draw(context, new Point(NarrowPadding, rowCenter - LabelLine / 2));
        if (isAnswer)
        {
            DrawCheck(context, palette, state, digitsX - DigitsGap - CheckSize / 2, rowCenter, now);
        }
        DrawDigits(context, palette, state, digitsX, rowCenter - ScoreLine / 2, now);
        var trackY = h - NarrowTrackBottom - NarrowTrackHeight;
        DrawBar(context, palette, state, new Rect(NarrowPadding, trackY, Math.Max(0, w - 2 * NarrowPadding), NarrowTrackHeight), now);
    }

    private static void DrawBar(DrawingContext context, ReasoningTreePalette palette, ReasoningTreeNodeState state, Rect track, double now)
    {
        var radius = track.Height / 2;
        context.DrawRectangle(palette.Brush(palette.Border), null, new RoundedRect(track, radius));
        var fill = Math.Clamp(state.Fill.Value(now), 0, 1) * track.Width;
        if (fill <= 0.25)
        {
            return;
        }
        var color = ReasoningTreePalette.Mix(palette.TextMuted, palette.Accent, state.Kept.Value(now));
        color = ReasoningTreePalette.Mix(color, palette.BorderStrong, state.Pruned.Value(now));
        color = ReasoningTreePalette.Mix(color, palette.Success, state.AnswerBar.Value(now));
        context.DrawRectangle(palette.Brush(color), null, new RoundedRect(new Rect(track.X, track.Y, fill, track.Height), radius));
    }

    private void DrawDigits(DrawingContext context, ReasoningTreePalette palette, ReasoningTreeNodeState state, double x, double y, double now)
    {
        var opacity = state.Digits.Value(now);
        if (opacity <= 0.005)
        {
            return;
        }
        var text = _stage.DigitText(ReasoningTreeSearch.Hundredths(state.Count.Value(now)));
        // Right-aligned in the fixed slot (tabular figures, so the digits never shift as they count).
        var right = x + DigitsWidth;
        // Muted while scored or pruned, secondary once kept or chosen: a crossfade of two cached layouts.
        var secondary = Math.Clamp(Math.Max(state.Kept.Value(now), state.AnswerBar.Value(now)), 0, 1);
        if (secondary < 0.999)
        {
            var muted = palette.Texts.Get(text, palette.Regular, 12, palette.Brush(palette.TextMuted), double.PositiveInfinity, 1, ScoreLine,
                tabular: true);
            using (context.PushOpacity(opacity * (1 - secondary)))
            {
                muted.Draw(context, new Point(right - muted.WidthIncludingTrailingWhitespace, y));
            }
        }
        if (secondary > 0.001)
        {
            var strong = palette.Texts.Get(text, palette.Regular, 12, palette.Brush(palette.TextSecondary), double.PositiveInfinity, 1,
                ScoreLine, tabular: true);
            using (context.PushOpacity(opacity * secondary))
            {
                strong.Draw(context, new Point(right - strong.WidthIncludingTrailingWhitespace, y));
            }
        }
    }

    private static void DrawCheck(DrawingContext context, ReasoningTreePalette palette, ReasoningTreeNodeState state, double cx, double cy,
        double now)
    {
        var opacity = state.Check.Value(now);
        if (opacity <= 0.005)
        {
            return;
        }
        var k = CheckSize / 24 * state.CheckScale.Value(now);
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(k, k) * Matrix.CreateTranslation(cx, cy);
        using (context.PushOpacity(opacity))
        {
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(null, palette.CheckPen, ArcIcons.CircleCheck);
            }
        }
    }
}

/// <summary>A thought as a tree item: its name, its depth and place as help text, and whether it is the current one.</summary>
internal sealed class ReasoningTreeNodeAutomationPeer : ControlAutomationPeer, ISelectionItemProvider
{
    private readonly ReasoningTreeStage _stage;

    public ReasoningTreeNodeAutomationPeer(ReasoningTreeNode owner, ReasoningTreeStage stage) : base(owner)
    {
        _stage = stage;
    }

    private ReasoningTreeNode Node => (ReasoningTreeNode)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : _stage.NameOf(Node.Index);

    protected override string? GetHelpTextCore() => _stage.PositionOf(Node.Index);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public bool IsSelected => _stage.Current == Node.Index;

    public ISelectionProvider? SelectionContainer => null;

    public void Select() => _stage.FocusNode(Node.Index, NavigationMethod.Directional);

    public void AddToSelection() => Select();

    public void RemoveFromSelection()
    {
    }
}
