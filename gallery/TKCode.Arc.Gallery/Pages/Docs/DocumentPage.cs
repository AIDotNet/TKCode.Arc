using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

/// <summary>A Docs page: its Markdown rendered on its own, with no page chrome and no Preview or Code tabs.</summary>
public static class DocumentPage
{
    /// <summary>Docs pages read at a comfortable line length, like uiarc.dev's documentation column.</summary>
    private const double ReadingWidth = 760;

    public static Control Create(string markdown)
    {
        var document = new Markdown { Text = markdown };
        return new ScrollViewer
        {
            Padding = new Thickness(40, 32),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                MaxWidth = ReadingWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = document,
            },
        };
    }
}
