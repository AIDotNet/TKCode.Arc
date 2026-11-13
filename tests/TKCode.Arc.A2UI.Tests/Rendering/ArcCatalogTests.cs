using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc;

namespace TKCode.Arc.A2UI.Tests.Rendering;

public sealed class ArcCatalogTests
{
    private const string Basic = A2UICatalogs.BasicCatalogId;
    private const string ArcId = A2UICatalogs.ArcCatalogId;

    private static string Create(string components, string data = "{}")
        => $$$"""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"{{{Basic}}}","sendDataModel":true,"dataModel":{{{data}}},"components":{{{components}}}}}""";

    private static (A2UIRenderer Renderer, A2UISurfaceView View, Window Window, List<string> Errors) Setup()
    {
        var renderer = new A2UIRenderer();
        var errors = new List<string>();
        renderer.ErrorReported += (_, e) => errors.Add(e.Error.Message);
        var view = new A2UISurfaceView { Renderer = renderer, SurfaceId = "s" };
        return (renderer, view, Ui.Show(view), errors);
    }

    [Fact]
    public void The_renderer_supports_the_arc_catalog_by_default()
    {
        var caps = new A2UIRenderer().GetRendererCapabilities(includeInlineCatalogs: false);
        var ids = caps["1.0"]!["supportedCatalogIds"]!.AsArray().Select(n => A2UIJson.AsString(n)).ToList();
        Assert.Equal([Basic, ArcId], ids);
        Assert.Equal(18, A2UICatalogs.Arc.Components.Count);
    }

    [AvaloniaFact]
    public void Every_arc_component_renders_its_arc_control()
    {
        var (renderer, view, window, errors) = Setup();
        renderer.Process(Create($$$"""
            [{"id":"root","component":"Column","children":["bar","line","donut","spark","metric","table","timeline","stepper","progress","alert","badge","avatar","md","code","acc","sw","seg","rate"]},
             {"id":"bar","component":"BarChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"values":[1,2]},
             {"id":"line","component":"LineChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"series":[{"name":"x","values":[1,2]}]},
             {"id":"donut","component":"DonutChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"values":[1,2]},
             {"id":"spark","component":"Sparkline","@catalogId":"{{{ArcId}}}","values":[1,3,2]},
             {"id":"metric","component":"Metric","@catalogId":"{{{ArcId}}}","label":"Users","value":42},
             {"id":"table","component":"Table","@catalogId":"{{{ArcId}}}","columns":[{"key":"n","title":"Name"},{"key":"v","format":"currency:USD"}],"rows":{"@path":"/rows"}},
             {"id":"timeline","component":"Timeline","@catalogId":"{{{ArcId}}}","events":[{"id":"e","at":"2026-11-14T10:00:00Z","title":"Started"}]},
             {"id":"stepper","component":"Stepper","@catalogId":"{{{ArcId}}}","steps":[{"label":"One"},{"label":"Two"}],"current":1},
             {"id":"progress","component":"Progress","@catalogId":"{{{ArcId}}}","value":40},
             {"id":"alert","component":"Alert","@catalogId":"{{{ArcId}}}","title":"Heads up","tone":"warning"},
             {"id":"badge","component":"Badge","@catalogId":"{{{ArcId}}}","text":"New","tone":"success"},
             {"id":"avatar","component":"Avatar","@catalogId":"{{{ArcId}}}","name":"Ada Lovelace"},
             {"id":"md","component":"Markdown","@catalogId":"{{{ArcId}}}","text":"## Notes\n- one\n- two"},
             {"id":"code","component":"CodeBlock","@catalogId":"{{{ArcId}}}","code":"var x = 1;","language":"csharp"},
             {"id":"acc","component":"Accordion","@catalogId":"{{{ArcId}}}","items":[{"title":"First","child":"acc_1"},{"title":"Second","child":"acc_2"}]},
             {"id":"acc_1","component":"Text","text":"Inside one"},
             {"id":"acc_2","component":"Text","text":"Inside two"},
             {"id":"sw","component":"Switch","@catalogId":"{{{ArcId}}}","label":"Alerts","value":{"@path":"/on"}},
             {"id":"seg","component":"SegmentedControl","@catalogId":"{{{ArcId}}}","options":[{"label":"Day","value":"day"},{"label":"Week","value":"week"}],"value":{"@path":"/period"}},
             {"id":"rate","component":"Rating","@catalogId":"{{{ArcId}}}","label":"Stars","value":{"@path":"/stars"}}]
            """, """{"rows":[{"n":"A","v":1200}],"on":false,"period":"day","stars":3}"""));
        Ui.Pump();
        Assert.Empty(errors);
        var controls = view.GetVisualDescendants().ToList();
        Assert.Contains(controls, c => c is BarChart);
        Assert.Contains(controls, c => c is LineChart);
        Assert.Contains(controls, c => c is DonutChart);
        Assert.Contains(controls, c => c is Sparkline);
        Assert.Contains(controls, c => c is MetricCard);
        Assert.Contains(controls, c => c is Table);
        Assert.Contains(controls, c => c is Timeline);
        Assert.Contains(controls, c => c is Stepper);
        Assert.Contains(controls, c => c is Progress);
        Assert.Contains(controls, c => c is Alert);
        Assert.Contains(controls, c => c is Badge);
        Assert.Contains(controls, c => c is Avatar);
        Assert.Contains(controls, c => c is Markdown);
        Assert.Contains(controls, c => c is CodeBlock);
        Assert.Contains(controls, c => c is Accordion);
        Assert.Contains(controls, c => c is Switch);
        Assert.Contains(controls, c => c is SegmentedControl);
        Assert.Contains(controls, c => c is Rating);
        Assert.Equal(["First", "Second"], view.GetVisualDescendants().OfType<AccordionItem>().Select(i => i.Header as string));
        Assert.Equal(1, view.GetVisualDescendants().OfType<Stepper>().Single().CurrentIndex);
        window.Close();
    }

    [AvaloniaFact]
    public void Arc_inputs_write_back_to_the_data_model()
    {
        var (renderer, view, window, errors) = Setup();
        renderer.Process(Create($$$"""
            [{"id":"root","component":"Column","children":["sw","seg","rate"]},
             {"id":"sw","component":"Switch","@catalogId":"{{{ArcId}}}","label":"Alerts","value":{"@path":"/on"}},
             {"id":"seg","component":"SegmentedControl","@catalogId":"{{{ArcId}}}","options":[{"label":"Day","value":"day"},{"label":"Week","value":"week"}],"value":{"@path":"/period"}},
             {"id":"rate","component":"Rating","@catalogId":"{{{ArcId}}}","value":{"@path":"/stars"}}]
            """, """{"on":false,"period":"day","stars":3}"""));
        Ui.Pump();
        var data = renderer.Surfaces[0].DataModel;
        var segmented = view.GetVisualDescendants().OfType<SegmentedControl>().Single();
        Assert.Equal(0, segmented.SelectedIndex);
        Assert.Equal(3, view.GetVisualDescendants().OfType<Rating>().Single().Value);

        view.GetVisualDescendants().OfType<Switch>().Single().IsChecked = true;
        segmented.SelectedIndex = 1;
        Ui.Pump();
        Assert.True(A2UIJson.AsBoolean(data.Get("/on")));
        Assert.Equal("week", A2UIJson.AsString(data.Get("/period")));

        // And the other way: the agent changes the model, the controls follow.
        renderer.Process("""{"version":"1.0","updateDataModel":{"surfaceId":"s","path":"/stars","value":5}}""");
        renderer.Process("""{"version":"1.0","updateDataModel":{"surfaceId":"s","path":"/period","value":"day"}}""");
        Ui.Pump();
        Assert.Equal(5, view.GetVisualDescendants().OfType<Rating>().Single().Value);
        Assert.Equal(0, segmented.SelectedIndex);
        Assert.Empty(errors);
        window.Close();
    }

    [AvaloniaFact]
    public void Markdown_links_go_through_the_renderer_url_policy()
    {
        var (renderer, view, window, _) = Setup();
        var opened = new List<Uri>();
        renderer.UrlOpener = opened.Add;
        renderer.Process(Create($$$"""
            [{"id":"root","component":"Markdown","@catalogId":"{{{ArcId}}}","text":"[docs](https://example.com/docs)"}]
            """));
        Ui.Pump();
        var markdown = view.GetVisualDescendants().OfType<Markdown>().Single();
        Assert.False(markdown.OpensLinks);
        markdown.RaiseEvent(new StreamMark.Avalonia.Rendering.LinkClickedEventArgs(Markdown.LinkClickedEvent, markdown, "https://example.com/docs", null));
        markdown.RaiseEvent(new StreamMark.Avalonia.Rendering.LinkClickedEventArgs(Markdown.LinkClickedEvent, markdown, "javascript:alert(1)", null));
        Assert.Equal([new Uri("https://example.com/docs")], opened);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(TKCode.Arc.Motion.ArcMotionLevel.Full)]
    [InlineData(TKCode.Arc.Motion.ArcMotionLevel.Off)]
    public void Components_that_arrive_while_streaming_ease_in(TKCode.Arc.Motion.ArcMotionLevel level)
    {
        var (renderer, view, window, errors) = Setup();
        TKCode.Arc.Motion.ArcMotion.SetLevel(window, level);
        renderer.Process(Create("""[{"id":"root","component":"Column","children":["first"]},{"id":"first","component":"Text","text":"First"}]"""));
        Ui.Pump();

        // A child that arrives later: it starts faded and lowered (full motion), or simply appears (off).
        renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"root","component":"Column","children":["first","later"]},{"id":"later","component":"Text","text":"Later"}]}}""");
        Dispatcher.UIThread.RunJobs();
        var later = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Later");
        var slot = later.GetVisualAncestors().OfType<Decorator>().First(d => d.GetType().Name == "A2UINodePresenter");
        if (level == TKCode.Arc.Motion.ArcMotionLevel.Off)
        {
            Assert.Equal(1, slot.Opacity);
            Assert.Null(slot.RenderTransform);
        }
        else
        {
            Assert.True(slot.Opacity < 1);
            Assert.IsType<Avalonia.Media.TranslateTransform>(slot.RenderTransform);
            var until = DateTime.UtcNow.AddSeconds(3);
            while (slot.RenderTransform is not null && DateTime.UtcNow < until)
            {
                Ui.Pump();
                Thread.Sleep(16);
            }

            Assert.Equal(1, slot.Opacity);
            Assert.Null(slot.RenderTransform);
        }

        // The component already shown does not animate again when its values change.
        var first = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "First").GetVisualAncestors().OfType<Decorator>().First(d => d.GetType().Name == "A2UINodePresenter");
        renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"first","component":"Text","text":"First!"}]}}""");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, first.Opacity);
        Assert.Empty(errors);
        window.Close();
    }

    [AvaloniaFact]
    public void Components_in_a_card_do_not_inherit_the_card_paragraph_line_height()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create($$$"""
            [{"id":"root","component":"Card","child":"col"},
             {"id":"col","component":"Column","children":["t","d"]},
             {"id":"t","component":"Text","text":"Body"},
             {"id":"d","component":"DonutChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"values":[659,838]}]
            """));
        Ui.Pump();
        foreach (var text in view.GetVisualDescendants().OfType<TextBlock>())
        {
            Assert.True(double.IsNaN(text.LineHeight), $"'{text.Text}' has line height {text.LineHeight}");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void Charts_take_the_agent_height_within_limits()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create($$$"""
            [{"id":"root","component":"Column","children":["a","b","c"]},
             {"id":"a","component":"LineChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"series":[{"name":"x","values":[1,2]}],"height":300},
             {"id":"b","component":"BarChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"values":[1,2],"height":5000},
             {"id":"c","component":"LineChart","@catalogId":"{{{ArcId}}}","labels":["a","b"],"series":[{"name":"x","values":[1,2]}]}]
            """));
        Ui.Pump();
        var lines = view.GetVisualDescendants().OfType<LineChart>().ToList();
        Assert.Equal(300, lines[0].PlotHeight);
        Assert.Equal(200, lines[1].PlotHeight);
        Assert.Equal(600, view.GetVisualDescendants().OfType<BarChart>().Single().PlotHeight);
        window.Close();
    }
}
