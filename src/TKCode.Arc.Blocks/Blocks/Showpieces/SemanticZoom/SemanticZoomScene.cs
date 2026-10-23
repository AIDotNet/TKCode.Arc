using Avalonia;

namespace TKCode.Arc;

/// <summary>The scope chain under the camera: indices into <see cref="SemanticZoomLayout.Boxes"/> (−1 = none).</summary>
internal readonly record struct SemanticZoomScope(int Team, int Project, int Task)
{
    public static readonly SemanticZoomScope Organization = new(-1, -1, -1);

    /// <summary>The level of the scope.</summary>
    public SemanticZoomLevel Level =>
        Task >= 0 ? SemanticZoomLevel.Task
        : Project >= 0 ? SemanticZoomLevel.Project
        : Team >= 0 ? SemanticZoomLevel.Team
        : SemanticZoomLevel.Organization;

    /// <summary>The deepest box of the chain (−1 for the organization).</summary>
    public int Deepest => Task >= 0 ? Task : Project >= 0 ? Project : Team;

    /// <summary>The scope one level up.</summary>
    public SemanticZoomScope Parent =>
        Task >= 0 ? this with { Task = -1 }
        : Project >= 0 ? this with { Project = -1 }
        : Organization;

    /// <summary>The chain of a box.</summary>
    public static SemanticZoomScope Of(SemanticZoomLayout layout, int box)
    {
        if (box < 0 || box >= layout.Boxes.Length)
        {
            return Organization;
        }
        var b = layout.Boxes[box];
        return b.Kind switch
        {
            SemanticZoomNodeKind.Team => new SemanticZoomScope(box, -1, -1),
            SemanticZoomNodeKind.Project => new SemanticZoomScope(b.Parent, box, -1),
            _ => new SemanticZoomScope(layout.Boxes[b.Parent].Parent, b.Parent, box)
        };
    }
}

/// <summary>
/// The map's per-frame scene state and rules, free of UI (Arc Semantic zoom 4.8, design reference):
/// <list type="bullet">
/// <item>the scope derived from the camera: the node chain under the stage center, as deep as the readout zoom allows
/// (team from 1.08×, project from 2.35×, task from 18.3×); a center over empty space stops one level up;</item>
/// <item>hit rules: the scope's children and its siblings are hit; the scope itself and its ancestors are not;</item>
/// <item>dimming: teams outside the scope × 0.42, projects and tasks beside the scope's project × 0.5 (one factor per
/// node, smoothed ×0.885 per frame, τ ≈ 140ms), multiplied down the chain;</item>
/// <item>hover outline strength per node (×0.83 per frame, τ ≈ 90ms) and the level-of-detail layers smoothed
/// (τ ≈ 140ms) toward <see cref="SemanticZoomLod"/>'s settled curves;</item>
/// <item>keyboard cursor geometry: the nearest candidate in a direction.</item>
/// </list>
/// Nothing here allocates per frame.
/// </summary>
internal sealed class SemanticZoomScene
{
    /// <summary>Hover outline time constant (×0.83 per 60fps frame).</summary>
    public static readonly double HoverTimeConstant = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.HoverFrameFactor);

    /// <summary>Dimming and LOD time constant (×0.885 per 60fps frame).</summary>
    public static readonly double DimTimeConstant = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.DimFrameFactor);

    private SemanticZoomLayout _layout = SemanticZoomLayout.Empty;
    private double[] _dim = Array.Empty<double>();
    private double[] _dimTarget = Array.Empty<double>();
    private double[] _hover = Array.Empty<double>();
    private readonly double[] _lod = new double[SemanticZoomLod.LayerCount];
    private readonly double[] _lodTarget = new double[SemanticZoomLod.LayerCount];
    private int _hovered = -1;
    private int _cursor = -1;
    private int _activeHover;

    public SemanticZoomLayout Layout => _layout;

    /// <summary>The current scope (set by the owner from the camera).</summary>
    public SemanticZoomScope Scope { get; private set; } = SemanticZoomScope.Organization;

    /// <summary>The hovered box (−1 none).</summary>
    public int Hovered => _hovered;

    /// <summary>The keyboard cursor's box (−1 none; checklist items are tracked by the owner).</summary>
    public int Cursor => _cursor;

    /// <summary>Animate smoothing (false at reduced motion: values land at once).</summary>
    public bool Animates { get; set; } = true;

    /// <summary>New layout: state arrays resized, everything undimmed and settled.</summary>
    public void Load(SemanticZoomLayout layout, double k)
    {
        _layout = layout;
        var n = layout.Boxes.Length;
        _dim = new double[n];
        _dimTarget = new double[n];
        _hover = new double[n];
        Array.Fill(_dim, 1.0);
        Array.Fill(_dimTarget, 1.0);
        _hovered = -1;
        _cursor = -1;
        _activeHover = 0;
        Scope = SemanticZoomScope.Organization;
        SemanticZoomLod.Targets(k, _lodTarget);
        Array.Copy(_lodTarget, _lod, _lod.Length);
    }

    // Scope ------------------------------------------------------------------------------------------------------------

    /// <summary>The scope chain at world point <paramref name="center"/> for readout <paramref name="zoom"/>.</summary>
    public SemanticZoomScope ScopeAt(Point center, double zoom)
    {
        var level = SemanticZoomLod.LevelForZoom(zoom);
        if (level == SemanticZoomLevel.Organization || _layout.IsEmpty)
        {
            return SemanticZoomScope.Organization;
        }
        var boxes = _layout.Boxes;
        var team = -1;
        for (var t = 0; t < _layout.TeamCount; t++)
        {
            if (boxes[t].Rect.Contains(center))
            {
                team = t;
                break;
            }
        }
        if (team < 0)
        {
            return SemanticZoomScope.Organization;
        }
        if (level == SemanticZoomLevel.Team)
        {
            return new SemanticZoomScope(team, -1, -1);
        }
        var project = ChildAt(team, center);
        if (project < 0)
        {
            return new SemanticZoomScope(team, -1, -1);
        }
        if (level == SemanticZoomLevel.Project)
        {
            return new SemanticZoomScope(team, project, -1);
        }
        var task = ChildAt(project, center);
        return new SemanticZoomScope(team, project, task);
    }

    private int ChildAt(int parent, Point world)
    {
        var box = _layout.Boxes[parent];
        for (var i = box.FirstChild; i < box.FirstChild + box.ChildCount; i++)
        {
            if (_layout.Boxes[i].Rect.Contains(world))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Sets the scope and retargets the dimming (smoothly unless <paramref name="immediate"/>).</summary>
    public void SetScope(SemanticZoomScope scope, bool immediate = false)
    {
        Scope = scope;
        var boxes = _layout.Boxes;
        for (var i = 0; i < boxes.Length; i++)
        {
            _dimTarget[i] = OwnDimTarget(i, scope);
            if (immediate || !Animates)
            {
                _dim[i] = _dimTarget[i];
            }
        }
        if (_cursor >= 0 && !IsCandidate(_cursor))
        {
            SetCursor(-1);
        }
    }

    private double OwnDimTarget(int i, SemanticZoomScope scope)
    {
        var box = _layout.Boxes[i];
        switch (box.Kind)
        {
            case SemanticZoomNodeKind.Team:
                return scope.Team < 0 || scope.Team == i ? 1 : SemanticZoomLod.TeamDim;
            case SemanticZoomNodeKind.Project:
                return scope.Project >= 0 && box.Parent == scope.Team && scope.Project != i ? SemanticZoomLod.ProjectDim : 1;
            default:
                return scope.Task >= 0 && box.Parent == scope.Project && scope.Task != i ? SemanticZoomLod.ProjectDim : 1;
        }
    }

    /// <summary>The settled dimming target of box <paramref name="i"/> including its ancestors.</summary>
    public double DimTarget(int i)
    {
        var value = 1.0;
        for (var b = i; b >= 0; b = _layout.Boxes[b].Parent)
        {
            value *= _dimTarget[b];
        }
        return value;
    }

    /// <summary>The current (smoothed) opacity of box <paramref name="i"/>: its own factor times its ancestors'.</summary>
    public double Dim(int i)
    {
        var value = 1.0;
        for (var b = i; b >= 0; b = _layout.Boxes[b].Parent)
        {
            value *= _dim[b];
        }
        return value;
    }

    /// <summary>The box's own smoothed factor (without ancestors).</summary>
    public double OwnDim(int i) => _dim[i];

    // Hit rules --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The box hit at <paramref name="world"/> for the current scope: at the organization any team (anywhere in its
    /// panel); in a team its projects, then the other teams; in a project its tasks, then the other projects of its team;
    /// in a task the other tasks of its project. The scope and its ancestors are never hit.
    /// </summary>
    public int HitTest(Point world)
    {
        if (_layout.IsEmpty)
        {
            return -1;
        }
        var scope = Scope;
        var boxes = _layout.Boxes;
        switch (scope.Level)
        {
            case SemanticZoomLevel.Organization:
                for (var t = 0; t < _layout.TeamCount; t++)
                {
                    if (boxes[t].Rect.Contains(world))
                    {
                        return t;
                    }
                }
                return -1;
            case SemanticZoomLevel.Team:
            {
                var project = ChildAt(scope.Team, world);
                if (project >= 0)
                {
                    return project;
                }
                for (var t = 0; t < _layout.TeamCount; t++)
                {
                    if (t != scope.Team && boxes[t].Rect.Contains(world))
                    {
                        return t;
                    }
                }
                return -1;
            }
            case SemanticZoomLevel.Project:
            {
                var task = ChildAt(scope.Project, world);
                if (task >= 0)
                {
                    return task;
                }
                var project = ChildAt(scope.Team, world);
                return project != scope.Project ? project : -1;
            }
            default:
            {
                var task = ChildAt(scope.Project, world);
                return task != scope.Task ? task : -1;
            }
        }
    }

    /// <summary>True when box <paramref name="i"/> is a child or a sibling of the scope (hit and cursor candidates).</summary>
    public bool IsCandidate(int i)
    {
        if (i < 0 || i >= _layout.Boxes.Length)
        {
            return false;
        }
        var scope = Scope;
        var box = _layout.Boxes[i];
        return scope.Level switch
        {
            SemanticZoomLevel.Organization => box.Kind == SemanticZoomNodeKind.Team,
            SemanticZoomLevel.Team => box.Kind == SemanticZoomNodeKind.Team ? i != scope.Team : box.Kind == SemanticZoomNodeKind.Project && box.Parent == scope.Team,
            SemanticZoomLevel.Project => box.Kind == SemanticZoomNodeKind.Project ? box.Parent == scope.Team && i != scope.Project : box.Kind == SemanticZoomNodeKind.Task && box.Parent == scope.Project,
            _ => box.Kind == SemanticZoomNodeKind.Task && box.Parent == scope.Project && i != scope.Task
        };
    }

    /// <summary>The cursor candidates of the current scope: children first (in order), then siblings.</summary>
    public int CandidateCount(Span<int> buffer)
    {
        var count = 0;
        var scope = Scope;
        var boxes = _layout.Boxes;
        void Add(int i, Span<int> b)
        {
            if (count < b.Length)
            {
                b[count] = i;
            }
            count++;
        }
        switch (scope.Level)
        {
            case SemanticZoomLevel.Organization:
                for (var t = 0; t < _layout.TeamCount; t++)
                {
                    Add(t, buffer);
                }
                break;
            case SemanticZoomLevel.Team:
                for (var i = boxes[scope.Team].FirstChild; i < boxes[scope.Team].FirstChild + boxes[scope.Team].ChildCount; i++)
                {
                    Add(i, buffer);
                }
                for (var t = 0; t < _layout.TeamCount; t++)
                {
                    if (t != scope.Team)
                    {
                        Add(t, buffer);
                    }
                }
                break;
            case SemanticZoomLevel.Project:
                for (var i = boxes[scope.Project].FirstChild; i < boxes[scope.Project].FirstChild + boxes[scope.Project].ChildCount; i++)
                {
                    Add(i, buffer);
                }
                for (var i = boxes[scope.Team].FirstChild; i < boxes[scope.Team].FirstChild + boxes[scope.Team].ChildCount; i++)
                {
                    if (i != scope.Project)
                    {
                        Add(i, buffer);
                    }
                }
                break;
            default:
                for (var i = boxes[scope.Project].FirstChild; i < boxes[scope.Project].FirstChild + boxes[scope.Project].ChildCount; i++)
                {
                    if (i != scope.Task)
                    {
                        Add(i, buffer);
                    }
                }
                break;
        }
        return count;
    }

    /// <summary>
    /// The index (into <paramref name="centers"/>) of the nearest point from <paramref name="from"/> in
    /// <paramref name="direction"/>: only points ahead count; the score is the distance along the direction plus twice
    /// the sideways offset. −1 when nothing lies that way.
    /// </summary>
    public static int Nearest(ReadOnlySpan<Point> centers, Point from, SemanticZoomDirection direction, int skip = -1)
    {
        var best = -1;
        var bestScore = double.PositiveInfinity;
        for (var i = 0; i < centers.Length; i++)
        {
            if (i == skip)
            {
                continue;
            }
            var d = centers[i] - from;
            double along, across;
            switch (direction)
            {
                case SemanticZoomDirection.Left:
                    along = -d.X;
                    across = Math.Abs(d.Y);
                    break;
                case SemanticZoomDirection.Right:
                    along = d.X;
                    across = Math.Abs(d.Y);
                    break;
                case SemanticZoomDirection.Up:
                    along = -d.Y;
                    across = Math.Abs(d.X);
                    break;
                default:
                    along = d.Y;
                    across = Math.Abs(d.X);
                    break;
            }
            if (along <= 1e-6)
            {
                continue;
            }
            var score = along + 2 * across;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    // Hover and cursor -------------------------------------------------------------------------------------------------

    /// <summary>Sets the hovered box (its outline grows; the previous one fades).</summary>
    public void SetHovered(int box)
    {
        _hovered = box;
        if (!Animates)
        {
            SnapHover();
        }
    }

    /// <summary>Sets the keyboard cursor's box (drawn with the hover outline).</summary>
    public void SetCursor(int box)
    {
        _cursor = box;
        if (!Animates)
        {
            SnapHover();
        }
    }

    private void SnapHover()
    {
        for (var i = 0; i < _hover.Length; i++)
        {
            _hover[i] = HoverTarget(i);
        }
    }

    private double HoverTarget(int i) => i == _hovered || i == _cursor ? 1 : 0;

    /// <summary>The outline strength of box <paramref name="i"/> (0…1).</summary>
    public double Hover(int i) => i >= 0 && i < _hover.Length ? _hover[i] : 0;

    // Level of detail ----------------------------------------------------------------------------------------------------

    /// <summary>The smoothed opacity of a detail layer.</summary>
    public double Lod(SemanticZoomLayer layer) => _lod[(int)layer];

    /// <summary>The settled opacity of a detail layer for the last <see cref="SetScale"/>.</summary>
    public double LodTarget(SemanticZoomLayer layer) => _lodTarget[(int)layer];

    /// <summary>New on-screen scale: retargets the detail layers (they follow in <see cref="Step"/>).</summary>
    public void SetScale(double k)
    {
        SemanticZoomLod.Targets(k, _lodTarget);
        if (!Animates)
        {
            Array.Copy(_lodTarget, _lod, _lod.Length);
        }
    }

    // Frames -----------------------------------------------------------------------------------------------------------

    /// <summary>Advances the smoothing by <paramref name="dt"/> seconds; true while anything still moves.</summary>
    public bool Step(double dt)
    {
        var moving = false;
        if (!Animates)
        {
            Array.Copy(_lodTarget, _lod, _lod.Length);
            Array.Copy(_dimTarget, _dim, _dim.Length);
            SnapHover();
            return false;
        }
        for (var i = 0; i < _lod.Length; i++)
        {
            if (_lod[i] != _lodTarget[i])
            {
                _lod[i] = ShowpieceInertia.Approach(_lod[i], _lodTarget[i], DimTimeConstant, dt);
                moving |= _lod[i] != _lodTarget[i];
            }
        }
        for (var i = 0; i < _dim.Length; i++)
        {
            if (_dim[i] != _dimTarget[i])
            {
                _dim[i] = ShowpieceInertia.Approach(_dim[i], _dimTarget[i], DimTimeConstant, dt);
                moving |= _dim[i] != _dimTarget[i];
            }
        }
        _activeHover = 0;
        for (var i = 0; i < _hover.Length; i++)
        {
            var target = HoverTarget(i);
            if (_hover[i] != target)
            {
                _hover[i] = ShowpieceInertia.Approach(_hover[i], target, HoverTimeConstant, dt);
                moving |= _hover[i] != target;
            }
            if (_hover[i] > 0)
            {
                _activeHover++;
            }
        }
        return moving;
    }

    /// <summary>Boxes with a visible outline after the last step (tests).</summary>
    internal int ActiveHoverCount => _activeHover;
}
