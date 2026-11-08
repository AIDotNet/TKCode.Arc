using System.Text.Json.Serialization;

namespace ArcStarterApp.Models;

/// <summary>A workflow as saved: its steps and the connections between them (Services/WorkflowSerializer.cs maps the canvas's graph to it).</summary>
public sealed class WorkflowDocument
{
    public string Name { get; set; } = "Untitled workflow";

    public List<SavedStep> Steps { get; set; } = [];

    public List<SavedConnection> Connections { get; set; } = [];
}

/// <summary>One step: its kind (Models/StepKinds.cs), its text and where it sits.</summary>
public sealed class SavedStep
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public bool IsLocked { get; set; }
}

/// <summary>A connection from a step's output (by index, for steps with several) to another step.</summary>
public sealed class SavedConnection
{
    public string From { get; set; } = string.Empty;

    public int? Output { get; set; }

    public string To { get; set; } = string.Empty;
}

/// <summary>The document's serializer, generated at build time (no reflection, trimming and AOT safe).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkflowDocument))]
internal sealed partial class WorkflowJsonContext : JsonSerializerContext
{
}
