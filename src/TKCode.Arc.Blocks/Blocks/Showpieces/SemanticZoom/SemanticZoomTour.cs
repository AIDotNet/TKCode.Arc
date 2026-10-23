namespace TKCode.Arc;

/// <summary>What the tour asks its owner to do after a step.</summary>
internal enum SemanticZoomTourAction
{
    None,

    /// <summary>Fly (or jump, at reduced motion) to stop <see cref="SemanticZoomTour.StopIndex"/>.</summary>
    GoToStop,

    /// <summary>The tour ended after its last stop.</summary>
    Finished
}

/// <summary>
/// The guided tour's clock (Arc Semantic zoom 6.6, design reference), free of UI: it leaves at once for the first stop,
/// waits for the camera to arrive, dwells (1.5 s; 2.4 s at reduced motion, where the camera jumps), moves on, and ends
/// about 200 ms after arriving at the last stop. The owner flies the camera and reports whether it is still moving;
/// any manual input stops the tour through <see cref="Stop"/>.
/// </summary>
internal sealed class SemanticZoomTour
{
    /// <summary>Dwell at reduced motion (measured 2.37–2.41 s).</summary>
    public const double ReducedDwellSeconds = 2.4;

    /// <summary>Delay between arriving at the last stop and ending.</summary>
    public const double FinishDelaySeconds = 0.2;

    private enum Phase
    {
        Idle,
        Travelling,
        Dwelling,
        Finishing
    }

    private Phase _phase = Phase.Idle;
    private double _remaining;
    private int _stops;

    /// <summary>The stop being travelled to or dwelt at (−1 when idle).</summary>
    public int StopIndex { get; private set; } = -1;

    /// <summary>The tour is running.</summary>
    public bool IsPlaying => _phase != Phase.Idle;

    /// <summary>Waiting at a stop (no camera motion).</summary>
    public bool IsDwelling => _phase is Phase.Dwelling or Phase.Finishing;

    /// <summary>Dwell at full motion.</summary>
    public double DwellSeconds { get; set; } = 1.5;

    /// <summary>Reduced or no motion: the camera jumps and each stop lasts <see cref="ReducedDwellSeconds"/>.</summary>
    public bool IsReduced { get; set; }

    /// <summary>Seconds left in the current dwell (tests).</summary>
    internal double Remaining => _remaining;

    /// <summary>Starts at stop 0 of <paramref name="stopCount"/>; the owner should go there at once.</summary>
    public SemanticZoomTourAction Start(int stopCount)
    {
        if (stopCount <= 0)
        {
            Stop();
            return SemanticZoomTourAction.None;
        }
        _stops = stopCount;
        StopIndex = 0;
        _phase = Phase.Travelling;
        _remaining = 0;
        return SemanticZoomTourAction.GoToStop;
    }

    /// <summary>Stops where it is.</summary>
    public void Stop()
    {
        _phase = Phase.Idle;
        StopIndex = -1;
        _remaining = 0;
    }

    /// <summary>
    /// Advances by <paramref name="dt"/> seconds. <paramref name="cameraMoving"/>: the camera is still flying (a stop is
    /// reached when it rests).
    /// </summary>
    public SemanticZoomTourAction Advance(double dt, bool cameraMoving)
    {
        switch (_phase)
        {
            case Phase.Travelling:
                if (cameraMoving)
                {
                    return SemanticZoomTourAction.None;
                }
                if (StopIndex >= _stops - 1)
                {
                    _phase = Phase.Finishing;
                    _remaining = FinishDelaySeconds;
                }
                else
                {
                    _phase = Phase.Dwelling;
                    _remaining = IsReduced ? ReducedDwellSeconds : DwellSeconds;
                }
                return SemanticZoomTourAction.None;
            case Phase.Dwelling:
                _remaining -= dt;
                if (_remaining > 0)
                {
                    return SemanticZoomTourAction.None;
                }
                StopIndex++;
                _phase = Phase.Travelling;
                _remaining = 0;
                return SemanticZoomTourAction.GoToStop;
            case Phase.Finishing:
                _remaining -= dt;
                if (_remaining > 0)
                {
                    return SemanticZoomTourAction.None;
                }
                Stop();
                return SemanticZoomTourAction.Finished;
            default:
                return SemanticZoomTourAction.None;
        }
    }

    /// <summary>
    /// The default stops of an organization (team, one of its projects, two tasks of another project, a second team,
    /// one of its projects, then the whole organization), skipping what the data does not have.
    /// </summary>
    public static List<SemanticZoomNode> DefaultStops(SemanticZoomOrganization organization)
    {
        var stops = new List<SemanticZoomNode>(7);
        var teams = organization.Teams;
        if (teams.Count > 0)
        {
            var first = teams[0];
            stops.Add(first);
            if (first.Projects.Count > 0)
            {
                stops.Add(first.Projects[0]);
            }
            // Two tasks of the team's next project (or of its first when it has only one).
            var taskProject = first.Projects.Count > 1 ? first.Projects[1] : first.Projects.Count > 0 ? first.Projects[0] : null;
            if (taskProject is not null)
            {
                for (var i = 0; i < Math.Min(2, taskProject.Tasks.Count); i++)
                {
                    stops.Add(taskProject.Tasks[i]);
                }
            }
            if (teams.Count > 1)
            {
                var second = teams[1];
                stops.Add(second);
                if (second.Projects.Count > 0)
                {
                    stops.Add(second.Projects[0]);
                }
            }
        }
        stops.Add(organization);
        return stops;
    }
}
