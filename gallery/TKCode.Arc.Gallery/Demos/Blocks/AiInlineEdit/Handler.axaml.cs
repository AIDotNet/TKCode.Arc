using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiInlineEditHandlerDemo : UserControl
{
    public AiInlineEditHandlerDemo()
    {
        InitializeComponent();
        var calls = 0;
        Editor.RewriteHandler = async (request, cancellation) =>
        {
            await Task.Delay(700, cancellation);
            if (++calls == 1)
            {
                throw new InvalidOperationException("the model is busy, try again");
            }
            // A stand-in for a model call that follows request.Instruction.
            return request.Selection.Replace("is scheduled for", "is on").Replace("we kindly ask that every team lead prepares", "each team lead should bring");
        };
        Log.Text = "Send any instruction twice: the first try fails.";
        Editor.EditApplied += (_, e) => Log.Text = $"EditApplied: {e.Accepted} accepted, {e.Rejected} rejected for \"{e.Instruction}\"";
    }
}
