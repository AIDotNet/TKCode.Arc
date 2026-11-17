using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests;

/// <summary>
/// A data template handed to a control is only built with data. A <see cref="ContentPresenter"/> builds the template it still has
/// when its content is cleared, and a template set while the content is still null is built with null, so a template that reads
/// its data (<c>vm.Title</c>) threw. Each test records what the template was built with.
/// </summary>
public class TemplateContractTests
{
    private static FuncDataTemplate<string> Recording(List<string?> built) =>
        new((text, _) =>
        {
            built.Add(text);
            return new TextBlock { Text = text };
        });

    [AvaloniaFact]
    public void Tabs_Build_Their_Panel_Template_Only_With_Content()
    {
        var built = new List<string?>();
        var tabs = new Tabs { ItemsSource = new[] { "Inbox", "Drafts", "Sent" }, ContentTemplate = Recording(built) };
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedItem = "Sent";
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedItem = "Drafts";
            Dispatcher.UIThread.RunJobs();
            built.ShouldNotBeEmpty();
            built.ShouldAllBe(text => text != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PageHeader_Builds_Its_Tab_And_Body_Templates_Only_With_Content()
    {
        var tabTexts = new List<string?>();
        var bodyTexts = new List<string?>();
        var header = new PageHeader
        {
            Title = "API",
            Width = 800,
            Height = 400,
            ItemTemplate = Recording(tabTexts),
            ContentTemplate = Recording(bodyTexts),
            ItemsSource = new AvaloniaList<string> { "Overview", "Endpoints", "Errors" }
        };
        var window = TestHelpers.Show(header);
        try
        {
            header.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            header.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            tabTexts.ShouldNotBeEmpty();
            bodyTexts.ShouldNotBeEmpty();
            tabTexts.ShouldAllBe(text => text != null);
            bodyTexts.ShouldAllBe(text => text != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SearchResults_Builds_Its_Detail_Template_Only_With_The_Opened_Item()
    {
        var built = new List<SearchResultItem?>();
        var item = new SearchResultItem { Id = "a", Title = "Launch checklist", Kind = "Docs", KindLabel = "Doc", Snippet = "Ship it" };
        var search = new SearchResults
        {
            ItemsSource = new[] { item },
            Height = 640,
            DetailTemplate = new FuncDataTemplate<SearchResultItem>((opened, _) =>
            {
                built.Add(opened);
                return new TextBlock { Text = opened.Title };
            }, true)
        };
        var window = TestHelpers.Show(new Border { Width = 900, Child = search });
        try
        {
            search.ShowDetail(item);
            Dispatcher.UIThread.RunJobs();
            search.CloseDetail();
            Dispatcher.UIThread.RunJobs();
            built.ShouldNotBeEmpty();
            built.ShouldAllBe(opened => opened != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breadcrumb_Builds_Its_Item_Template_Only_With_Items()
    {
        var built = new List<string?>();
        var breadcrumb = new Breadcrumb
        {
            ItemsSource = new[] { "Workspace", "Settings", "Billing" },
            ItemTemplate = new FuncDataTemplate<string>((text, _) =>
            {
                built.Add(text);
                return new TextBlock { Text = text!.ToUpperInvariant() };
            })
        };
        var window = TestHelpers.Show(breadcrumb);
        try
        {
            Dispatcher.UIThread.RunJobs();
            built.ShouldNotBeEmpty();
            built.ShouldAllBe(text => text != null);
        }
        finally
        {
            window.Close();
        }
    }
}
