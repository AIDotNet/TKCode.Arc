using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiSearchBarOwnAnswersDemo : UserControl
{
    public AiSearchBarOwnAnswersDemo()
    {
        InitializeComponent();
        Search.SuggestedQuestions = ["What is a sprint?", "What does WIP mean?"];
        Search.AnswerProvider = async (query, cancellation) =>
        {
            await Task.Delay(500, cancellation);
            if (query.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The glossary service is not answering right now.");
            }
            return new AiSearchAnswer
            {
                Text = $"\"{query}\" is in the team glossary [1]. A sprint is a fixed stretch of work, and WIP is the work in progress.",
                Sources = [new AiSearchSource { Title = "Team glossary", Location = "Wiki › Team" }],
                Results = [new AiSearchResult { Title = "Team glossary", Location = "Wiki › Team", Snippet = "Words we use every day, from backlog to WIP." }],
            };
        };
    }
}
