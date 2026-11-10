using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="EvalSuite.RunStarted"/>.</summary>
public sealed class EvalSuiteRunStartedEventArgs : RoutedEventArgs
{
    internal EvalSuiteRunStartedEventArgs(RoutedEvent routedEvent, object source, EvalVersion baseline, EvalVersion candidate,
        IReadOnlyList<EvalCase> cases)
        : base(routedEvent, source)
    {
        Baseline = baseline;
        Candidate = candidate;
        Cases = cases;
    }

    /// <summary>The baseline version being run.</summary>
    public EvalVersion Baseline { get; }

    /// <summary>The candidate version being run.</summary>
    public EvalVersion Candidate { get; }

    /// <summary>The cases, in the order they start.</summary>
    public IReadOnlyList<EvalCase> Cases { get; }
}

/// <summary>Arguments of <see cref="EvalSuite.CaseCompleted"/>.</summary>
public sealed class EvalSuiteCaseCompletedEventArgs : RoutedEventArgs
{
    internal EvalSuiteCaseCompletedEventArgs(RoutedEvent routedEvent, object source, EvalCase evalCase, EvalResult baseline,
        EvalResult candidate, EvalCaseStatus status)
        : base(routedEvent, source)
    {
        Case = evalCase;
        Baseline = baseline;
        Candidate = candidate;
        Status = status;
    }

    /// <summary>The case that finished.</summary>
    public EvalCase Case { get; }

    /// <summary>Its result on the baseline.</summary>
    public EvalResult Baseline { get; }

    /// <summary>Its result on the candidate.</summary>
    public EvalResult Candidate { get; }

    /// <summary>How the case moved: passing, failing, a regression or a fix.</summary>
    public EvalCaseStatus Status { get; }
}

/// <summary>Arguments of <see cref="EvalSuite.RunCompleted"/>.</summary>
public sealed class EvalSuiteRunCompletedEventArgs : RoutedEventArgs
{
    internal EvalSuiteRunCompletedEventArgs(RoutedEvent routedEvent, object source, EvalSummary summary, bool wasStopped,
        int finished, TimeSpan elapsed)
        : base(routedEvent, source)
    {
        Summary = summary;
        WasStopped = wasStopped;
        Finished = finished;
        Elapsed = elapsed;
    }

    /// <summary>The comparison after the run.</summary>
    public EvalSummary Summary { get; }

    /// <summary>True when the run was stopped before every case finished.</summary>
    public bool WasStopped { get; }

    /// <summary>How many cases finished in this run.</summary>
    public int Finished { get; }

    /// <summary>How long the run took.</summary>
    public TimeSpan Elapsed { get; }
}
