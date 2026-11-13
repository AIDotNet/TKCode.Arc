using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using TKCode.Arc;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.A2UI.Tests.Rendering;

public sealed class SurfaceViewTests
{
    private const string Basic = "https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json";

    private static string Create(string components, string data = "{}")
        => $$$"""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"{{{Basic}}}","sendDataModel":true,"dataModel":{{{data}}},"components":{{{components}}}}}""";

    private static (A2UIRenderer Renderer, A2UISurfaceView View, Window Window, List<A2UIOutboundEventArgs> Outbound) Setup()
    {
        var renderer = new A2UIRenderer();
        var outbound = new List<A2UIOutboundEventArgs>();
        renderer.Outbound += (_, e) => outbound.Add(e);
        var view = new A2UISurfaceView { Renderer = renderer, SurfaceId = "s" };
        var window = Ui.Show(view);
        return (renderer, view, window, outbound);
    }

    [AvaloniaFact]
    public void Form_renders_binds_and_sends_the_action()
    {
        var (renderer, view, window, outbound) = Setup();
        renderer.Process(Create("""
            [{"id":"root","component":"Column","children":["title","name","agree","send"]},
             {"id":"title","component":"Text","text":"# Contact"},
             {"id":"name","component":"TextField","label":"Name","value":{"@path":"/name"}},
             {"id":"agree","component":"CheckBox","label":"I agree","value":{"@path":"/agree"}},
             {"id":"send","component":"Button","child":"send_label","variant":"primary",
              "action":{"event":{"name":"submit","context":{"who":{"@path":"/name"},"ok":{"@path":"/agree"}}}}},
             {"id":"send_label","component":"Text","text":"Send"}]
            """, """{"name":"Ada"}"""));
        Ui.Pump();

        var input = view.GetVisualDescendants().OfType<Input>().Single();
        Assert.Equal("Name", input.Label);
        Assert.Equal("Ada", input.Text);

        input.Text = "Grace";
        Ui.Pump();
        Assert.Equal("Grace", A2UIJson.AsString(renderer.GetSurface("s")!.DataModel.Get("/name")));

        var checkbox = view.GetVisualDescendants().OfType<Checkbox>().Single();
        checkbox.IsChecked = true;
        Ui.Pump();
        Assert.Equal(true, A2UIJson.AsBoolean(renderer.GetSurface("s")!.DataModel.Get("/agree")));

        var button = view.GetVisualDescendants().OfType<Button>().Single();
        Assert.Equal(ButtonVariant.Primary, button.Variant);
        button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        var action = outbound.Single(o => o.Kind == A2UIOutboundKind.Action).Action!;
        Assert.Equal("submit", A2UIJson.AsString(action["name"]));
        Assert.Equal("Grace", A2UIJson.AsString(action["context"]!["who"]));
        Assert.Equal(true, A2UIJson.AsBoolean(action["context"]!["ok"]));
        window.Close();
    }

    [AvaloniaFact]
    public void Data_updates_change_the_existing_control()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create("""[{"id":"root","component":"Text","text":{"@path":"/greeting"}}]""", """{"greeting":"Hello"}"""));
        Ui.Pump();
        var text = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Hello");

        renderer.Process("""{"version":"1.0","updateDataModel":{"surfaceId":"s","path":"/greeting","value":"Hi"}}""");
        Ui.Pump();
        Assert.Equal("Hi", text.Text);
        Assert.Same(text, view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Hi"));
        window.Close();
    }

    [AvaloniaFact]
    public void Template_list_follows_the_data()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create("""
            [{"id":"root","component":"List","children":{"componentId":"row","path":"/items"}},
             {"id":"row","component":"Text","text":{"@call":"formatString","args":{"value":"${@index(offset: 1)}. ${name}"}}}]
            """, """{"items":[{"name":"Milk"},{"name":"Eggs"}]}"""));
        Ui.Pump();
        string[] Rows() => view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).Where(t => t.Length > 0).ToArray();
        Assert.Equal(["1. Milk", "2. Eggs"], Rows());

        renderer.Process("""{"version":"1.0","updateDataModel":{"surfaceId":"s","path":"/items/2","value":{"name":"Tea"}}}""");
        Ui.Pump();
        Assert.Equal(["1. Milk", "2. Eggs", "3. Tea"], Rows());
        window.Close();
    }

    [AvaloniaFact]
    public void Streamed_children_fill_their_place()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process($$$"""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"{{{Basic}}}"}}""");
        Ui.Pump();
        Assert.IsType<Skeleton>(view.Child);

        renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"root","component":"Row","children":["a","b"]}]}}""");
        renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"b","component":"Text","text":"second"}]}}""");
        Ui.Pump();
        Assert.Equal(["second"], view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToArray());

        renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"a","component":"Text","text":"first"}]}}""");
        Ui.Pump();
        Assert.Equal(["first", "second"], view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToArray());
        window.Close();
    }

    [AvaloniaFact]
    public void Failed_checks_disable_the_button()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create("""
            [{"id":"root","component":"Button","child":"l","action":{"event":{"name":"go"}},
              "@checks":[{"condition":{"@call":"required","args":{"value":{"@path":"/email"}}},"message":"Email is required."}]},
             {"id":"l","component":"Text","text":"Go"}]
            """));
        Ui.Pump();
        var button = view.GetVisualDescendants().OfType<Button>().Single();
        Assert.False(button.IsEnabled);

        renderer.Process("""{"version":"1.0","updateDataModel":{"surfaceId":"s","path":"/email","value":"a@b.c"}}""");
        Ui.Pump();
        Assert.True(button.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void Every_basic_component_builds()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create("""
            [{"id":"root","component":"Column","children":["card","tabs","modal","divider","img","icon","video","audio","picker","chips","slider","date"]},
             {"id":"card","component":"Card","child":"t1"},{"id":"t1","component":"Text","text":"In a card","variant":"caption"},
             {"id":"tabs","component":"Tabs","tabs":[{"title":"One","child":"t2"},{"title":"Two","child":"t3"}]},
             {"id":"t2","component":"Text","text":"Tab one"},{"id":"t3","component":"Text","text":"Tab two"},
             {"id":"modal","component":"Modal","trigger":"open","content":"t4"},
             {"id":"open","component":"Button","child":"ol","action":{"event":{"name":"noop"}}},{"id":"ol","component":"Text","text":"Open"},
             {"id":"t4","component":"Text","text":"Modal body"},
             {"id":"divider","component":"Divider"},
             {"id":"img","component":"Image","url":"https://example.com/a.png","variant":"smallFeature"},
             {"id":"icon","component":"Icon","name":"star"},
             {"id":"video","component":"Video","url":"https://example.com/v.mp4"},
             {"id":"audio","component":"AudioPlayer","url":"https://example.com/a.mp3","description":"Podcast"},
             {"id":"picker","component":"ChoicePicker","label":"Size","options":[{"label":"S","value":"s"},{"label":"M","value":"m"}],"value":{"@path":"/size"}},
             {"id":"chips","component":"ChoicePicker","variant":"multipleSelection","displayStyle":"chips","options":[{"label":"A","value":"a"},{"label":"B","value":"b"}],"value":["a"]},
             {"id":"slider","component":"Slider","min":0,"max":10,"value":{"@path":"/n"},"steps":10},
             {"id":"date","component":"DateTimeInput","enableDate":true,"value":"2026-11-14"}]
            """, """{"size":["m"],"n":3}"""));
        Ui.Pump();
        Assert.NotNull(view.GetVisualDescendants().OfType<Card>().SingleOrDefault());
        Assert.NotNull(view.GetVisualDescendants().OfType<Tabs>().SingleOrDefault());
        Assert.Equal("m", view.GetVisualDescendants().OfType<RadioGroup>().Single().SelectedValue);
        Assert.Equal(3, view.GetVisualDescendants().OfType<Slider>().Single().Value);
        Assert.Equal(new DateOnly(2026, 11, 14), view.GetVisualDescendants().OfType<DatePicker>().Single().SelectedDate);
        Assert.DoesNotContain(renderer.Surfaces[0].Nodes.Root!.AllChildren(), n => n.State != A2UINodeState.Resolved);
        window.Close();
    }

    [AvaloniaFact]
    public void Text_in_a_primary_button_takes_the_button_foreground()
    {
        var (renderer, view, window, _) = Setup();
        renderer.Process(Create("""
            [{"id":"root","component":"Button","variant":"primary","child":"l","action":{"event":{"name":"go"}}},
             {"id":"l","component":"Text","text":"Continue"}]
            """));
        Ui.Pump();
        var button = view.GetVisualDescendants().OfType<Button>().Single();
        var label = view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Continue");
        Assert.False(label.IsSet(TextBlock.ForegroundProperty));
        Assert.Equal(((Avalonia.Media.ISolidColorBrush)button.Foreground!).Color, ((Avalonia.Media.ISolidColorBrush)label.Foreground!).Color);
        window.Close();
    }
}
