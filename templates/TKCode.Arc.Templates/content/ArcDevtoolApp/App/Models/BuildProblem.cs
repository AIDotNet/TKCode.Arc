namespace ArcStarterApp.Models;

public enum ProblemSeverity
{
    Error,
    Warning,
}

/// <summary>A diagnostic the build reported, for the Problems pane.</summary>
public sealed record BuildProblem(ProblemSeverity Severity, string Code, string Message, string File, int Line)
{
    public string Location => $"{File}({Line})";
}

/// <summary>One step of a build: a line for the Output pane and, for a diagnostic, the problem it reports.</summary>
public sealed record BuildEvent(string Line, BuildProblem? Problem = null);
