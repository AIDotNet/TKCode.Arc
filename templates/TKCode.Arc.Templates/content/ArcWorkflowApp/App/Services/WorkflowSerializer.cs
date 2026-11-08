using Avalonia;
using ArcStarterApp.Models;
using TKCode.Arc;

namespace ArcStarterApp.Services;

/// <summary>Maps between the canvas's <see cref="WorkflowGraph"/> and the saved <see cref="WorkflowDocument"/>.</summary>
public static class WorkflowSerializer
{
    /// <summary>A new step of <paramref name="kind"/>, with its icon and outputs.</summary>
    public static WorkflowNode CreateStep(StepKind kind, string? id = null, string? title = null)
    {
        var node = new WorkflowNode(id, title ?? kind.Title)
        {
            Kind = kind.Key,
            Description = kind.Description,
            Icon = kind.Icon,
            HasDefaultInput = !kind.IsTrigger,
        };
        foreach (var output in kind.Outputs)
        {
            node.Outputs.Add(new WorkflowPort(WorkflowPortDirection.Output, output));
        }
        return node;
    }

    public static WorkflowDocument ToDocument(WorkflowGraph graph, string name)
    {
        var document = new WorkflowDocument { Name = name };
        foreach (var node in graph.Nodes)
        {
            var placed = !double.IsNaN(node.Position.X);
            document.Steps.Add(new SavedStep
            {
                Id = node.Id,
                Kind = node.Kind ?? string.Empty,
                Title = node.Title ?? string.Empty,
                Description = node.Description,
                X = placed ? node.Position.X : null,
                Y = placed ? node.Position.Y : null,
                IsLocked = node.IsLocked,
            });
        }
        foreach (var edge in graph.Edges)
        {
            var output = edge.SourcePort is { } port ? edge.Source.Outputs.IndexOf(port) : -1;
            document.Connections.Add(new SavedConnection
            {
                From = edge.Source.Id,
                Output = output >= 0 ? output : null,
                To = edge.Target.Id,
            });
        }
        return document;
    }

    public static WorkflowGraph ToGraph(WorkflowDocument document)
    {
        var graph = new WorkflowGraph();
        foreach (var step in document.Steps)
        {
            var node = CreateStep(StepKinds.Find(step.Kind), step.Id, step.Title);
            node.Description = step.Description;
            node.IsLocked = step.IsLocked;
            if (step.X is { } x && step.Y is { } y)
            {
                node.Position = new Point(x, y);
            }
            graph.Nodes.Add(node);
        }
        foreach (var connection in document.Connections)
        {
            if (graph.FindNode(connection.From) is not { } from || graph.FindNode(connection.To) is not { } to)
            {
                continue;
            }
            var port = connection.Output is { } index && index < from.Outputs.Count ? from.Outputs[index] : null;
            if (graph.CanConnect(from, port, to, null, out _))
            {
                graph.Connect(from, port, to, null);
            }
        }
        return graph;
    }
}
