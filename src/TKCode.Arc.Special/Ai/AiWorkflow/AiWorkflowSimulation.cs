namespace TKCode.Arc;

/// <summary>
/// A flow an <see cref="AiWorkflow"/> shows and test-runs on its own while the app sets no
/// <see cref="AiWorkflow.Steps"/>: a name, a line of context, the steps and the add step catalog, all supplied by the
/// caller. Each simulated run plays the steps' <see cref="AiWorkflowStep.Output"/>, <see cref="AiWorkflowStep.Duration"/>
/// and <see cref="AiWorkflowStep.Error"/>; with <see cref="AiWorkflow.AutoPlay"/> the card repeats the run while it is on
/// screen until someone presses or types inside it. Nothing is sent. Set the data before handing the simulation to the
/// card; to show another flow, give the card a new simulation.
/// </summary>
public sealed class AiWorkflowSimulation
{
    /// <summary>The flow's name in the header (the card's own <see cref="AiWorkflow.Title"/> wins).</summary>
    public string? Title { get; set; }

    /// <summary>One line of context under the name (the card's own <see cref="AiWorkflow.Description"/> wins).</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The steps, in order; a branch carries its paths and must be the last top-level step. The card treats them as
    /// values: adding or removing a step puts a new list in <see cref="AiWorkflow.Steps"/> and leaves this one alone.
    /// </summary>
    public IList<AiWorkflowStep>? Steps { get; set; }

    /// <summary>The add step menu's entries while this flow is shown (the card's own <see cref="AiWorkflow.Catalog"/> wins).</summary>
    public IList<AiWorkflowCatalogItem>? Catalog { get; set; }
}
