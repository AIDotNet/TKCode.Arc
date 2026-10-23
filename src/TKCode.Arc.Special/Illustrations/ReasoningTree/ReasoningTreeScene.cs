using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What has happened to a node so far in the current search (also the fate its tag and name report).</summary>
internal enum ReasoningThoughtFate
{
    /// <summary>Not proposed yet (or not part of this search).</summary>
    Hidden,

    /// <summary>Proposed, not scored yet.</summary>
    Candidate,

    /// <summary>Scored; its depth is not pruned yet.</summary>
    Scored,

    /// <summary>Survived its depth.</summary>
    Kept,

    /// <summary>Did not survive its depth.</summary>
    Pruned,

    /// <summary>Kept and on the winning path.</summary>
    OnPath,

    /// <summary>The answer.</summary>
    Answer,

    /// <summary>The question (always shown).</summary>
    Question
}

/// <summary>The beat the story is at.</summary>
internal enum ReasoningTreeBeat
{
    /// <summary>Before the first thoughts (reading the question).</summary>
    Ready,

    /// <summary>Thoughts of a depth appeared.</summary>
    Propose,

    /// <summary>Thoughts of a depth were scored.</summary>
    Score,

    /// <summary>A depth kept its best thoughts.</summary>
    Keep,

    /// <summary>The answer was chosen.</summary>
    Answer
}

/// <summary>
/// The animated state of one node. Every channel is a tween on the wall clock (seconds of frames), not on the story
/// clock: the story's beats pause, step and follow the speed, but a change that a beat started finishes on its own at its
/// measured duration, as the reference does (pausing never freezes a fade halfway).
/// </summary>
internal sealed class ReasoningTreeNodeState
{
    public ReasoningThoughtFate Fate;

    /// <summary>Card opacity: 0 hidden, 1 shown, 0.4 pruned.</summary>
    public ExplainerValue Opacity;

    /// <summary>The share of the edge from the parent that is drawn (0..1).</summary>
    public ExplainerValue Edge;

    /// <summary>The score bar's length as a score (0..score).</summary>
    public ExplainerValue Fill;

    /// <summary>The digits' value as a score (counts to the score on the bar's curve).</summary>
    public ExplainerValue Count;

    /// <summary>The digits' opacity.</summary>
    public ExplainerValue Digits;

    /// <summary>Bar muted → accent and digits muted → secondary (kept).</summary>
    public ExplainerValue Kept;

    /// <summary>Bar muted → strong border (pruned).</summary>
    public ExplainerValue Pruned;

    /// <summary>Bar → success (answer).</summary>
    public ExplainerValue AnswerBar;

    /// <summary>Card border → path color (lit by the path).</summary>
    public ExplainerValue Path;

    /// <summary>Card fill and border → success tints (the answer settles).</summary>
    public ExplainerValue AnswerMix;

    /// <summary>The answer's check: opacity and scale.</summary>
    public ExplainerValue Check;

    public ExplainerValue CheckScale;

    /// <summary>Hover or focus emphasis (border to strong, pruned to 0.8).</summary>
    public ExplainerValue Hover;

    /// <summary>The wall time the answer's press starts (NaN for none).</summary>
    public double PressAt = double.NaN;

    /// <summary>The wall time after which a hidden node stops being drawn and focusable (NaN while shown).</summary>
    public double CollapseAt = double.NaN;

    /// <summary>True from the moment the node is proposed until a restart hides it.</summary>
    public bool IsShown => Fate != ReasoningThoughtFate.Hidden;
}

/// <summary>
/// The state of a <see cref="ReasoningTree"/> search on screen: each node's fate and channels, the column headers and the
/// best path, changed by the story's beats (durations, delays and curves of Arc Reasoning tree, design reference). Under
/// reduced motion every change applies at once.
/// </summary>
internal sealed class ReasoningTreeScene
{
    /// <summary>Columns: the question and depths 1 to 3.</summary>
    public const int ColumnCount = 4;

    // Durations and delays in seconds (notes 2b.2, 2b.3, 6.1).
    public const double HideFade = 0.12;
    public const double HideBar = 0.16;
    public const double Collapse = 0.2;
    public const double Change = 0.24;
    public const double EdgeDraw = 0.24;
    public const double NodeFade = 0.18;
    public const double Stagger = 0.02;
    public const double WideNodeDelay = 0.1;
    public const double NarrowEdgeDelay = 0.14;
    public const double NarrowNodeDelay = 0.2;
    public const double ScoreFill = 0.64;
    public const double DigitsFade = 0.16;
    public const double CountFade = 0.16;
    public const double PrunedOpacity = 0.4;
    public const double PathDelay = 0.16;
    public const double PathDraw = 0.76;
    public const double SheenLength = 0.07;
    public const double SheenFade = 0.22;
    public const double SettleAt = 0.92;
    public const double PressDelay = 0.88;
    public const double PressDuration = 0.38;
    public const double PressDepth = 0.965;
    public const double PressTurn = 0.45;
    public const double HoverChange = 0.16;

    private double _now;
    private bool _animate;

    public ReasoningTreeNodeState[] Nodes { get; private set; } = [];

    /// <summary>Header emphasis per column (0 muted, 1 foreground).</summary>
    public ExplainerValue[] ColumnActive { get; } = new ExplainerValue[ColumnCount];

    /// <summary>The count beside each header and its opacity.</summary>
    public ExplainerValue[] CountOpacity { get; } = new ExplainerValue[ColumnCount];

    public string?[] CountText { get; } = new string?[ColumnCount];

    /// <summary>How much of the best path is drawn (0..1), and the sheen at its head.</summary>
    public ExplainerValue PathDrawn;

    public ExplainerValue SheenOpacity;

    /// <summary>True once the answer beat has drawn (or is drawing) the best path.</summary>
    public bool HasPath { get; private set; }

    /// <summary>The beat the story is at and its depth.</summary>
    public ReasoningTreeBeat Beat { get; private set; }

    public int BeatDepth { get; private set; }

    /// <summary>The wall time when the last started change ends.</summary>
    public double BusyUntil { get; private set; }

    /// <summary>True while a change is still running at <paramref name="now"/>.</summary>
    public bool IsBusy(double now) => now < BusyUntil;

    /// <summary>Hides every thought (fading out what was shown) and leaves the question; column 0 is active.</summary>
    public void Reset(int nodeCount, double now, bool animate)
    {
        Begin(now, animate);
        if (Nodes.Length != nodeCount)
        {
            var nodes = new ReasoningTreeNodeState[nodeCount];
            for (var i = 0; i < nodeCount; i++)
            {
                nodes[i] = new ReasoningTreeNodeState();
            }
            Nodes = nodes;
            // A new tree has nothing on screen to fade out.
            _animate = false;
        }
        for (var i = 0; i < Nodes.Length; i++)
        {
            var node = Nodes[i];
            if (i == 0)
            {
                node.Fate = ReasoningThoughtFate.Question;
                node.Opacity.Jump(1);
                Go(ref node.Path, 0, Change);
                Go(ref node.AnswerMix, 0, Change);
                node.PressAt = double.NaN;
                continue;
            }
            var wasShown = node.IsShown;
            node.Fate = ReasoningThoughtFate.Hidden;
            Go(ref node.Opacity, 0, HideFade);
            Go(ref node.Digits, 0, HideFade);
            Go(ref node.Fill, 0, HideBar);
            Go(ref node.Count, 0, HideBar);
            Go(ref node.Edge, 0, EdgeDraw, ArcEasings.InOut);
            Go(ref node.Path, 0, Change);
            Go(ref node.AnswerMix, 0, Change);
            Go(ref node.Check, 0, HideFade);
            Go(ref node.CheckScale, 0.7, Change);
            Go(ref node.Kept, 0, HideFade);
            Go(ref node.Pruned, 0, HideFade);
            Go(ref node.AnswerBar, 0, HideFade);
            node.PressAt = double.NaN;
            node.CollapseAt = wasShown && _animate ? now + Collapse : now;
        }
        for (var c = 0; c < ColumnCount; c++)
        {
            Go(ref ColumnActive[c], c == 0 ? 1 : 0, Change);
            Go(ref CountOpacity[c], 0, HideFade);
        }
        PathDrawn.Jump(0);
        SheenOpacity.Jump(0);
        HasPath = false;
        Beat = ReasoningTreeBeat.Ready;
        BeatDepth = 0;
    }

    /// <summary>Shows the candidates of a depth: each edge draws out of its parent and the card fades in, 20ms apart per sibling.</summary>
    public void Propose(ReasoningTreeModel model, ReasoningTreeDepth depth, double now, bool animate, bool wide)
    {
        Begin(now, animate);
        foreach (var index in depth.Candidates)
        {
            var node = Nodes[index];
            var i = model.Nodes[index].SiblingIndex;
            node.Fate = ReasoningThoughtFate.Candidate;
            node.CollapseAt = double.NaN;
            node.Edge.Jump(0);
            node.Opacity.Jump(0);
            node.Fill.Jump(0);
            node.Count.Jump(0);
            node.Digits.Jump(0);
            node.Kept.Jump(0);
            node.Pruned.Jump(0);
            node.AnswerBar.Jump(0);
            node.Path.Jump(0);
            node.AnswerMix.Jump(0);
            node.Check.Jump(0);
            node.CheckScale.Jump(0.7);
            Go(ref node.Edge, 1, EdgeDraw, ArcEasings.Standard, (wide ? 0 : NarrowEdgeDelay) + Stagger * i);
            Go(ref node.Opacity, 1, NodeFade, ArcEasings.Standard, (wide ? WideNodeDelay : NarrowNodeDelay) + Stagger * i);
        }
        for (var c = 0; c < ColumnCount; c++)
        {
            Go(ref ColumnActive[c], c == depth.Depth ? 1 : 0, Change);
        }
        Beat = ReasoningTreeBeat.Propose;
        BeatDepth = depth.Depth;
    }

    /// <summary>Fills every candidate's bar to its score with the digits counting along, 20ms apart per sibling.</summary>
    public void Score(ReasoningTreeModel model, ReasoningTreeDepth depth, double now, bool animate)
    {
        Begin(now, animate);
        foreach (var index in depth.Candidates)
        {
            var node = Nodes[index];
            var data = model.Nodes[index];
            var delay = Stagger * data.SiblingIndex;
            node.Fate = ReasoningThoughtFate.Scored;
            Go(ref node.Fill, data.Score, ScoreFill, ArcEasings.Standard, delay);
            Go(ref node.Count, data.Score, ScoreFill, ArcEasings.Standard, delay);
            Go(ref node.Digits, 1, DigitsFade, ArcEasings.Standard, delay);
        }
        Beat = ReasoningTreeBeat.Score;
        BeatDepth = depth.Depth;
    }

    /// <summary>Keeps the survivors (bar to the accent) and prunes the rest (fade to 40%, edge back into the parent).</summary>
    public void Keep(ReasoningTreeDepth depth, string count, double now, bool animate)
    {
        Begin(now, animate);
        foreach (var index in depth.Kept)
        {
            var node = Nodes[index];
            node.Fate = ReasoningThoughtFate.Kept;
            Go(ref node.Kept, 1, Change);
        }
        foreach (var index in depth.Pruned)
        {
            PruneCore(Nodes[index]);
        }
        CountText[depth.Depth] = count;
        Go(ref CountOpacity[depth.Depth], 1, CountFade);
        Beat = ReasoningTreeBeat.Keep;
        BeatDepth = depth.Depth;
    }

    /// <summary>
    /// Chooses the answer: the last depth's other candidates are pruned, the answer's bar turns to success at once, and
    /// the best path draws from the question to the answer (760ms after 160ms), lighting each node on it as the line
    /// arrives; the answer then settles with one press and its check.
    /// </summary>
    public void Answer(ReasoningTreeResult result, string count, double now, bool animate)
    {
        Begin(now, animate);
        var last = result.Depths[^1];
        foreach (var index in last.Pruned)
        {
            PruneCore(Nodes[index]);
        }
        var steps = Math.Max(1, result.Path.Length);
        Go(ref Nodes[0].Path, 1, Change, ArcEasings.Standard, PathDelay);
        for (var p = 0; p < result.Path.Length; p++)
        {
            var node = Nodes[result.Path[p]];
            var isAnswer = p == result.Path.Length - 1;
            node.Fate = isAnswer ? ReasoningThoughtFate.Answer : ReasoningThoughtFate.OnPath;
            if (isAnswer)
            {
                Go(ref node.AnswerBar, 1, Change);
                Go(ref node.AnswerMix, 1, Change, ArcEasings.Standard, SettleAt);
                Go(ref node.Check, 1, DigitsFade, ArcEasings.Standard, SettleAt);
                Go(ref node.CheckScale, 1, Change, ArcEasings.Standard, SettleAt);
                node.PressAt = _animate ? now + PressDelay : double.NaN;
                Busy(PressDelay + PressDuration);
            }
            else
            {
                Go(ref node.Path, 1, Change, ArcEasings.Standard, PathDelay + (p + 1) * PathDraw / steps);
            }
        }
        for (var c = 0; c < ColumnCount; c++)
        {
            Go(ref ColumnActive[c], 0, Change);
        }
        CountText[last.Depth] = count;
        Go(ref CountOpacity[last.Depth], 1, CountFade);
        HasPath = result.Path.Length > 0;
        PathDrawn.Jump(0);
        SheenOpacity.Jump(_animate ? 1 : 0);
        Go(ref PathDrawn, 1, PathDraw, ArcEasings.InOut, PathDelay);
        Go(ref SheenOpacity, 0, SheenFade, ArcEasings.Standard, SettleAt);
        Beat = ReasoningTreeBeat.Answer;
        BeatDepth = last.Depth;
    }

    /// <summary>Moves hover or focus emphasis of a node to <paramref name="on"/>.</summary>
    public void SetHover(int index, bool on, double now, bool animate)
    {
        if (index < 0 || index >= Nodes.Length)
        {
            return;
        }
        Begin(now, animate);
        Go(ref Nodes[index].Hover, on ? 1 : 0, HoverChange);
    }

    /// <summary>The answer's press scale at <paramref name="now"/>: 1 → 0.965 (at 45%) → 1 over 380ms, each half eased in-out.</summary>
    public static double Press(ReasoningTreeNodeState node, double now)
    {
        if (double.IsNaN(node.PressAt))
        {
            return 1;
        }
        var t = now - node.PressAt;
        if (t <= 0 || t >= PressDuration)
        {
            return 1;
        }
        var p = t / PressDuration;
        return p < PressTurn
            ? 1 - (1 - PressDepth) * ArcEasings.InOut.Ease(p / PressTurn)
            : PressDepth + (1 - PressDepth) * ArcEasings.InOut.Ease((p - PressTurn) / (1 - PressTurn));
    }

    private void PruneCore(ReasoningTreeNodeState node)
    {
        node.Fate = ReasoningThoughtFate.Pruned;
        Go(ref node.Opacity, PrunedOpacity, Change);
        Go(ref node.Pruned, 1, Change);
        Go(ref node.Edge, 0, EdgeDraw, ArcEasings.InOut);
    }

    private void Begin(double now, bool animate)
    {
        _now = now;
        _animate = animate;
    }

    private void Busy(double seconds) => BusyUntil = Math.Max(BusyUntil, _now + seconds);

    private void Go(ref ExplainerValue value, double to, double duration, Easing? easing = null, double delay = 0)
    {
        if (!_animate)
        {
            value.Jump(to);
            return;
        }
        value.Tween(to, _now, duration, easing ?? ArcEasings.Standard, delay);
        Busy(delay + duration);
    }
}
