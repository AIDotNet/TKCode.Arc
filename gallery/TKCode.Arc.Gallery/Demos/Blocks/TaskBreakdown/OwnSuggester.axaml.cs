using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TaskBreakdownOwnSuggesterDemo : UserControl
{
    public TaskBreakdownOwnSuggesterDemo()
    {
        InitializeComponent();
        Breakdown.People.AddRange([new ProjectPerson("Noor Haddad"), new ProjectPerson("Felix Wong")]);
        Breakdown.Description = "Export the old posts. Map the old links to new ones, then check the images.";
        Breakdown.SuggestAction = async (request, cancellation) =>
        {
            await Task.Delay(700, cancellation);
            return TaskBreakdown.SuggestFromText(request.Title, request.Description, request.People);
        };
        Breakdown.Created += (_, e) => Status.Text = $"{e.Subtasks.Count} subtasks created for {e.Title}";
    }
}
