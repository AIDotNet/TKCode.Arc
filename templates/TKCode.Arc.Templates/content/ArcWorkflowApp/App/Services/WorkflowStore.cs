using System.Text.Json;
using ArcStarterApp.Models;
using ArcStarterApp.Settings;

namespace ArcStarterApp.Services;

/// <summary>Keeps the workflow between runs. Replace it to keep workflows on a server, or several of them.</summary>
public interface IWorkflowStore
{
    /// <summary>The saved workflow, or null before the first save.</summary>
    WorkflowDocument? Load();

    void Save(WorkflowDocument document);
}

/// <summary>
/// Keeps the workflow as JSON in <see cref="Store"/>: on the desktop <c>workflow.json</c> beside the settings file;
/// tests keep it in memory. <see cref="ToJson"/> and <see cref="FromJson"/> are also the app's export and import format.
/// </summary>
public sealed class WorkflowStore : IWorkflowStore
{
    /// <summary><c>&lt;application data&gt;/ArcStarterApp/workflow.json</c>.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(Path.GetDirectoryName(AppSettings.DefaultPath)!, "workflow.json");

    /// <summary>Where the JSON goes; set it before the app is built to keep the workflow elsewhere.</summary>
    public static ISettingsStore Store { get; set; } = new FileSettingsStore(DefaultPath);

    public WorkflowDocument? Load() => Store.Read() is { Length: > 0 } json ? FromJson(json) : null;

    public void Save(WorkflowDocument document) => Store.Write(ToJson(document));

    public static string ToJson(WorkflowDocument document) =>
        JsonSerializer.Serialize(document, WorkflowJsonContext.Default.WorkflowDocument);

    /// <summary>The document in <paramref name="json"/>, or null when it is not a workflow.</summary>
    public static WorkflowDocument? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowDocument);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
