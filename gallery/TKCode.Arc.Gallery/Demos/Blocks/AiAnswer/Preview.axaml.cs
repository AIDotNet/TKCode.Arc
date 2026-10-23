using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiAnswerPreviewDemo : UserControl
{
    public AiAnswerPreviewDemo()
    {
        InitializeComponent();
        Answer.Providers.AddRange(AiAnswerDemoData.Models().Select(m => new AiModelProvider { Id = m.Id, Name = m.Name }));
        Answer.Models.AddRange(AiAnswerDemoData.Models());
        Answer.Model = Answer.Models[0];

        // A finished answer shows whole, with no word-by-word reveal. The responder answers the next question or the next version.
        Answer.Question = AiAnswerDemoData.FirstQuestion;
        Answer.Answer = AiAnswerDemoData.FirstAnswer;
        Answer.Reasoning = AiAnswerDemoData.FirstReasoning;
        Answer.Sources.AddRange(AiAnswerDemoData.Sources());
        Answer.FollowUps.AddRange(AiAnswerDemoData.Questions.Skip(1));
        Answer.Usage = new AiAnswerUsage(812, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(18.6));
        Answer.Responder = new AiAnswerDemoData.ScriptedResponder();

        // Without a provider the share buttons stay hidden: the block never copies a made-up link.
        Answer.ShareLinkProvider = (info, _) => ValueTask.FromResult("https://example.com/answers/ci-migration" + (info.VersionIndex is { } v ? $"?v={v + 1}" : string.Empty));
    }
}
