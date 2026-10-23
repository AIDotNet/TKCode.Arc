using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiAnswerEmptyDemo : UserControl
{
    public AiAnswerEmptyDemo()
    {
        InitializeComponent();
        Answer.Models.AddRange(AiAnswerDemoData.Models());

        // No question yet: the empty page shows the suggestions. Pressing one asks it and the answer streams in.
        Answer.Suggestions.AddRange(AiAnswerDemoData.Questions);
        Answer.Responder = new AiAnswerDemoData.ScriptedResponder();
    }
}
