using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiAnswerNarrowDemo : UserControl
{
    public AiAnswerNarrowDemo()
    {
        InitializeComponent();
        Answer.Models.AddRange(AiAnswerDemoData.Models());
        Answer.Question = AiAnswerDemoData.FirstQuestion;
        Answer.Answer = AiAnswerDemoData.FirstAnswer;
        Answer.Reasoning = AiAnswerDemoData.FirstReasoning;
        Answer.Sources.AddRange(AiAnswerDemoData.Sources());
        Answer.FollowUps.AddRange(AiAnswerDemoData.Questions.Skip(1));
        Answer.Responder = new AiAnswerDemoData.ScriptedResponder();
    }
}
