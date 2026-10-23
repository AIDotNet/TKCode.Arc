using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TokenTextEditorGrowthDemo : UserControl
{
    public TokenTextEditorGrowthDemo()
    {
        InitializeComponent();
        Editor.SubmitRequested += (_, e) =>
        {
            Result.Text = $"Submitted {e.Text.Length} characters.";
            Editor.Clear();
            e.Handled = true;
        };
    }
}
