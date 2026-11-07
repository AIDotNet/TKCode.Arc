namespace TKCode.Arc;

/// <summary>
/// The refresh cycle of <see cref="StretchRefresh"/> without any UI: Idle → Pulling ⇄ Armed (at the threshold, both
/// ways) → Refreshing on a release while armed (or Idle on a release before it) → Done or Failed when the work ends →
/// Idle when the sheet has settled. The button and <see cref="StretchRefresh.RefreshAsync"/> go straight to Refreshing
/// from Idle. Pulls, wheel pulls and the button are ignored from Refreshing until Idle.
/// </summary>
internal sealed class StretchRefreshMachine
{
    public StretchRefreshState State { get; private set; } = StretchRefreshState.Idle;

    /// <summary>The displayed pull that arms a refresh (72 by default).</summary>
    public double Threshold { get; set; } = 72;

    /// <summary>True from the start of a refresh until the cycle has settled (the button is busy).</summary>
    public bool IsBusy => State is StretchRefreshState.Refreshing or StretchRefreshState.Done or StretchRefreshState.Failed;

    /// <summary>True while a pull may start or continue.</summary>
    public bool CanPull => !IsBusy;

    /// <summary>The displayed pull changed. Returns true when the state changed (Pulling ⇄ Armed, or Idle → Pulling).</summary>
    public bool Pull(double pull)
    {
        if (IsBusy)
        {
            return false;
        }
        var next = pull >= Threshold ? StretchRefreshState.Armed : StretchRefreshState.Pulling;
        return Set(next);
    }

    /// <summary>The pull was let go. Returns true when this starts a refresh (it was armed).</summary>
    public bool Release()
    {
        switch (State)
        {
            case StretchRefreshState.Armed:
                Set(StretchRefreshState.Refreshing);
                return true;
            case StretchRefreshState.Pulling:
                Set(StretchRefreshState.Idle);
                return false;
            default:
                return false;
        }
    }

    /// <summary>The pull was abandoned (capture lost): back to Idle without refreshing.</summary>
    public void Cancel()
    {
        if (State is StretchRefreshState.Pulling or StretchRefreshState.Armed)
        {
            Set(StretchRefreshState.Idle);
        }
    }

    /// <summary>The button or code asks for a refresh. Returns false while busy.</summary>
    public bool Trigger()
    {
        if (IsBusy)
        {
            return false;
        }
        Set(StretchRefreshState.Refreshing);
        return true;
    }

    /// <summary>The work ended. Returns false when no refresh was running.</summary>
    public bool Complete(bool failed)
    {
        if (State != StretchRefreshState.Refreshing)
        {
            return false;
        }
        return Set(failed ? StretchRefreshState.Failed : StretchRefreshState.Done);
    }

    /// <summary>The sheet has settled after a result: back to Idle.</summary>
    public bool Settle()
    {
        if (State is not (StretchRefreshState.Done or StretchRefreshState.Failed))
        {
            return false;
        }
        return Set(StretchRefreshState.Idle);
    }

    private bool Set(StretchRefreshState next)
    {
        if (State == next)
        {
            return false;
        }
        State = next;
        return true;
    }
}
