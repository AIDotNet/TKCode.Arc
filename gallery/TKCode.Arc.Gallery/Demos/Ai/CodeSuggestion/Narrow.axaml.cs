using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The demo suggestion in a narrow column.</summary>
public partial class CodeSuggestionNarrowDemo : UserControl
{
    public CodeSuggestionNarrowDemo()
    {
        InitializeComponent();
        CodeSuggestionDemoData.Show(Card);
    }
}
