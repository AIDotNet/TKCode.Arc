using System.Runtime.CompilerServices;
using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.SampleData;

/// <summary>Prints a build without building: the steps, one warning per unused exception variable, and a summary.</summary>
public sealed class SampleBuildService : IBuildService
{
    /// <summary>The pause between two lines.</summary>
    public static TimeSpan Pace { get; set; } = TimeSpan.FromMilliseconds(120);

    public async IAsyncEnumerable<BuildEvent> BuildAsync(IWorkspace workspace, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var started = DateTime.UtcNow;
        yield return new BuildEvent($"Building {workspace.Name}...");
        await Task.Delay(Pace, cancellation);
        yield return new BuildEvent("  Restoring packages");
        await Task.Delay(Pace, cancellation);
        var warnings = 0;
        foreach (var path in Files(workspace.Root).Where(p => p.EndsWith(".cs", StringComparison.Ordinal)))
        {
            await Task.Delay(Pace, cancellation);
            yield return new BuildEvent($"  Compiling {path}");
            var lines = workspace.Read(path).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("catch (FormatException e)", StringComparison.Ordinal))
                {
                    warnings++;
                    var problem = new BuildProblem(ProblemSeverity.Warning, "CS0168", "The variable 'e' is declared but never used.", path, i + 1);
                    yield return new BuildEvent($"{problem.Location}: warning {problem.Code}: {problem.Message}", problem);
                }
            }
        }
        await Task.Delay(Pace, cancellation);
        var seconds = (DateTime.UtcNow - started).TotalSeconds;
        yield return new BuildEvent(warnings == 0
            ? $"Build succeeded in {seconds:0.0}s."
            : $"Build succeeded with {warnings} warning(s) in {seconds:0.0}s.");
    }

    private static IEnumerable<string> Files(IEnumerable<WorkspaceNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children is { } children)
            {
                foreach (var path in Files(children))
                {
                    yield return path;
                }
            }
            else
            {
                yield return node.Path;
            }
        }
    }
}
