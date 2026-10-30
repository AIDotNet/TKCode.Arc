using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A flow of its own with a runner that returns outputs after a delay and fails the chat step once.</summary>
public partial class AiWorkflowOwnRunnerDemo : UserControl
{
    public AiWorkflowOwnRunnerDemo()
    {
        InitializeComponent();
        Flow.Steps =
        [
            new AiWorkflowStep
            {
                Id = "signup", Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Webhook, Title = "New signup",
                Detail = "POST /hooks/signup", Activity = "Waiting for a signup"
            },
            new AiWorkflowStep
            {
                Id = "extract", Kind = AiWorkflowStepKind.Ai, App = AiWorkflowDemoData.Claude,
                Title = "Find the company with " + AiModelCatalog.ClaudeHaiku55.Name,
                Detail = "From the email domain", Activity = "Looking up the domain"
            },
            new AiWorkflowStep
            {
                Id = "crm", Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Crm, Title = "Update contact",
                Detail = "Company and size", Activity = "Saving the contact"
            },
            new AiWorkflowStep
            {
                Id = "notify", Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Chat, Title = "Post message",
                Detail = "#sales", Activity = "Posting to #sales"
            }
        ];
        Flow.RunStep = RunAsync;
        Flow.StepsChanged += (_, e) => Log.Text = $"{e.Kind}: {e.Step.Title}. The flow has {e.Steps.Count} top-level steps.";
        Flow.RunCompleted += (_, e) => Log.Text = e.Status == AiWorkflowRunStatus.Passed
            ? $"Passed in {e.Duration.TotalSeconds:0.0}s with {e.Outputs.Count} outputs."
            : $"Failed at {e.FailedStepId}.";
    }

    // Stands in for a call to your test endpoint. Pass the cancellation token on so Stop ends it at once.
    private static async Task<AiWorkflowOutput?> RunAsync(AiWorkflowStep step, AiWorkflowRunStepContext context)
    {
        await Task.Delay(step.Kind == AiWorkflowStepKind.Ai ? 1100 : 500, context.Cancellation);
        return step.Id switch
        {
            "signup" => new AiWorkflowRecordOutput { Id = "usr_4821", Title = "maya@fabrikam.example", Meta = "Signed up from the pricing page" },
            "extract" => new AiWorkflowLabelOutput
            {
                Label = "Fabrikam",
                Confidence = 0.88,
                Reason = "The email domain matches the company website, and the sample record lists about 200 people."
            },
            "crm" => new AiWorkflowRecordOutput { Id = "Maya Chen", Title = "Company set to Fabrikam", Meta = "Sample size: 200" },
            "notify" when context.Attempt == 1 => throw new InvalidOperationException("The chat app did not answer. Try again."),
            "notify" => new AiWorkflowMessageOutput { Channel = "#sales", Author = "Workflow bot", Text = "New signup from Fabrikam: Maya Chen." },
            _ => new AiWorkflowTextOutput { Text = "Done." }
        };
    }
}
