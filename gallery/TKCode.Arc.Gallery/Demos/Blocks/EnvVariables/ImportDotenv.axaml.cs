using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnvVariablesImportDotenvDemo : UserControl
{
    public EnvVariablesImportDotenvDemo()
    {
        InitializeComponent();
        EnvVariablesDemoData.Fill(Block);
        // Import .env opens with this text; paste your own over it, or paste several lines into the Key field.
        Block.SampleDotenv = EnvVariablesDemoData.SampleDotenv;
        Block.Loaded += (_, _) => Block.OpenAddPanel(EnvAddMode.Dotenv);
    }
}
