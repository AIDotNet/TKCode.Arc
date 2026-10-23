using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiAnswerStaticDemo : UserControl
{
    public AiAnswerStaticDemo()
    {
        InitializeComponent();
        Answer.Answer = "Block merges on the checks that catch real regressions: the build, the unit tests and the linter. [5]\n\n"
            + "### Keep the list short\n\n"
            + "- Build and unit tests: they fail for real reasons.\n- Lint: cheap and fast.\n- Leave slow device tests as advisory until they are stable. [6]\n\n"
            + "Check the names once after the move, because a renamed job silently stops blocking. [5]";
        Answer.Reasoning = "Required checks are matched by name, so the first thing to protect is the list itself.";
        Answer.Sources.AddRange(AiAnswerDemoData.Sources().Where(s => s.Index is 5 or 6));
        Answer.Sources[0].Index = 5;
        Answer.Sources[1].Index = 6;
        Answer.Model = AiAnswerDemoData.Models()[1];
        Answer.Usage = new AiAnswerUsage(430, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10.2));
    }
}
