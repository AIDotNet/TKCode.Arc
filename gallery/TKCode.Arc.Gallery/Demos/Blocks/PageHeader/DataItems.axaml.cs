using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>One section of the data-bound page header demo.</summary>
public sealed class PageHeaderDemoSection
{
    public PageHeaderDemoSection(string name, int? count, string summary)
    {
        Name = name;
        Count = count;
        Summary = summary;
    }

    public string Name { get; }

    public int? Count { get; set; }

    public string Summary { get; }
}

public partial class PageHeaderDataItemsDemo : UserControl
{
    private readonly PageHeaderDemoSection[] _sections =
    [
        new("Overview", null, "Requests, latency and errors for the last 24 hours."),
        new("Endpoints", 36, "Every public route with its owner and rate limit."),
        new("Errors", 3, "Errors grouped by cause, newest first.")
    ];

    public PageHeaderDataItemsDemo()
    {
        InitializeComponent();
        // Data items get a PageHeaderTab container; its count comes from the item.
        Header.ContainerPrepared += (_, e) =>
        {
            if (e.Container is PageHeaderTab tab && tab.Content is PageHeaderDemoSection section)
            {
                tab.Count = section.Count;
            }
        };
        Header.ItemsSource = _sections;
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
        => Header.SelectedIndex = (Header.SelectedIndex + _sections.Length - 1) % _sections.Length;

    private void OnNextClick(object? sender, RoutedEventArgs e)
        => Header.SelectedIndex = (Header.SelectedIndex + 1) % _sections.Length;

    // Changing a tab's count rolls its digits.
    private void OnAddErrorClick(object? sender, RoutedEventArgs e)
    {
        var errors = _sections[2];
        errors.Count = (errors.Count ?? 0) + 1;
        if (Header.ContainerFromIndex(2) is PageHeaderTab tab)
        {
            tab.Count = errors.Count;
        }
    }
}
