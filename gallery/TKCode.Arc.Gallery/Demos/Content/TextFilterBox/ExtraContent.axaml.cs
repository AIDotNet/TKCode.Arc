using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextFilterBoxExtraContentDemo : UserControl
{
    private static readonly string[] s_files = ["MainWindow.axaml", "App.AXAML", "MainWindow.axaml.cs", "Styles.axaml"];

    public TextFilterBoxExtraContentDemo()
    {
        InitializeComponent();
        // The checkbox in ExtraContent drives IsCaseSensitive; it is disabled with the filter.
        MatchCase.IsCheckedChanged += (_, _) => FileFilter.IsCaseSensitive = MatchCase.IsChecked == true;
        FileFilter.FilterChanged += (_, _) => Show();
        Show();
    }

    private void Show() =>
        Summary.Text = "Matches: " + string.Join(", ", s_files.Where(FileFilter.Matches));
}
