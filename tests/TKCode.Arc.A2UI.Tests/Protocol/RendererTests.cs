using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI.Tests.Protocol;

public sealed class RendererTests
{
    private const string Basic = A2UICatalogs.BasicCatalogId;

    private static (A2UIRenderer Renderer, List<JsonObject> Out) Create(A2UIRendererOptions? options = null)
    {
        var renderer = new A2UIRenderer(options);
        var outbound = new List<JsonObject>();
        renderer.Outbound += (_, e) => outbound.Add(e.Envelope);
        return (renderer, outbound);
    }

    [Fact]
    public void Capabilities_list_the_catalogs()
    {
        var (renderer, _) = Create();
        var caps = renderer.GetRendererCapabilities(includeInlineCatalogs: true);
        Assert.Equal(Basic, A2UIJson.AsString(caps["1.0"]!["supportedCatalogIds"]![0]));
        Assert.Equal(Basic, A2UIJson.AsString(caps["1.0"]!["inlineCatalogs"]![0]!["catalogId"]));
    }

    [Fact]
    public void Data_model_metadata_includes_only_surfaces_that_asked()
    {
        var (renderer, _) = Create();
        renderer.Process("""{"version":"1.0","createSurface":{"surfaceId":"a","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json","sendDataModel":true,"dataModel":{"x":1}}}""");
        renderer.Process("""{"version":"1.0","createSurface":{"surfaceId":"b","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json","dataModel":{"y":2}}}""");
        var data = renderer.GetRendererDataModel()!;
        Assert.Equal(1, data["surfaces"]!["a"]!["x"]!.GetValue<int>());
        Assert.False(((JsonObject)data["surfaces"]!).ContainsKey("b"));
    }

    [Fact]
    public void Invalid_message_is_reported_with_the_validation_shape()
    {
        var (renderer, outbound) = Create();
        renderer.Process("""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json"}}""");
        Assert.False(renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"root","component":"Text","text":5}]}}"""));
        var error = (JsonObject)outbound.Single()["error"]!;
        Assert.Equal("VALIDATION_FAILED", A2UIJson.AsString(error["code"]));
        Assert.Equal("s", A2UIJson.AsString(error["surfaceId"]));
        Assert.Equal("/updateComponents/components/0/text", A2UIJson.AsString(error["path"]));
    }

    [Theory]
    [InlineData("2026-11-16", true)]
    [InlineData("09:30", true)]
    [InlineData("2026-11-16T09:30:00Z", true)]
    [InlineData("2026-11-16T09:30", true)]
    [InlineData("16/11/2026", false)]
    [InlineData("2026-13-01", false)]
    public void Date_time_limits_match_exactly_one_iso_form(string min, bool valid)
    {
        var (renderer, _) = Create();
        renderer.Process("""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json"}}""");
        var ok = renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"root","component":"DateTimeInput","value":"","enableDate":true,"min":""" + "\"" + min + "\"" + """}]}}""");
        Assert.Equal(valid, ok);
    }

    [Fact]
    public void Lenient_mode_shows_pending_children_then_fills_them()
    {
        var (renderer, outbound) = Create();
        renderer.Process("""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json","components":[{"id":"root","component":"Column","children":["later"]}]}}""");
        var root = renderer.GetSurface("s")!.Root!;
        var child = root.GetChildren()[0];
        Assert.Equal(A2UINodeState.Pending, child.State);
        renderer.Process("""{"version":"1.0","updateComponents":{"surfaceId":"s","components":[{"id":"later","component":"Text","text":"ok"}]}}""");
        Assert.Same(child, root.GetChildren()[0]);
        Assert.Equal(A2UINodeState.Resolved, child.State);
        Assert.Equal("ok", child.GetString("text"));
        Assert.Empty(outbound);
    }

    [Fact]
    public void Renderer_only_function_is_refused_to_the_agent()
    {
        var (renderer, outbound) = Create();
        renderer.Process("""{"version":"1.0","callRendererFunction":{"functionCallId":"c1","callFunction":{"@call":"formatNumber","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json","args":{"value":1234.5,"decimals":1}}}}""");
        var response = (JsonObject)outbound.Single()["rendererFunctionResponse"]!;
        Assert.Equal("c1", A2UIJson.AsString(response["functionCallId"]));
        Assert.Equal("INVALID_FUNCTION_CALL", A2UIJson.AsString(response["error"]!["code"]));
    }

    [Fact]
    public void Gesture_only_function_is_refused_to_the_agent()
    {
        var (renderer, outbound) = Create();
        renderer.Process("""{"version":"1.0","callRendererFunction":{"functionCallId":"c2","callFunction":{"@call":"openUrl","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json","args":{"url":"https://example.com"}}}}""");
        var response = (JsonObject)outbound.Single()["rendererFunctionResponse"]!;
        Assert.Equal("INVALID_FUNCTION_CALL", A2UIJson.AsString(response["error"]!["code"]));
    }

    [Fact]
    public void Agent_function_in_a_binding_is_pending_then_settles()
    {
        var catalog = A2UICatalog.FromJson("""
            {"catalogId":"travel","protocolVersion":"1.0",
             "components":{"Label":{"type":"object","properties":{"component":{"const":"Label"},"text":{"$ref":"common_types.json#/$defs/DynamicString"}},"required":["component"]}},
             "functions":{"getBags":{"type":"object","returnType":"string","allowedCallers":"rendererOrAgent",
               "properties":{"@call":{"const":"getBags"},"args":{"type":"object","properties":{"flight":{"type":"string"}}}},"required":["@call"]}}}
            """);
        var (renderer, outbound) = Create(new A2UIRendererOptions { Catalogs = [catalog] });
        renderer.Process("""{"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"travel","components":[{"id":"root","component":"Label","text":{"@call":"getBags","args":{"flight":"LH458"}}}]}}""");
        var call = (JsonObject)outbound.Single()["callAgentFunction"]!;
        Assert.Equal("getBags", A2UIJson.AsString(call["callFunction"]!["@call"]));
        var root = renderer.GetSurface("s")!.Root!;
        Assert.Null(root.GetString("text"));

        var callId = A2UIJson.AsString(call["functionCallId"]);
        renderer.Process("{\"version\":\"1.0\",\"agentFunctionResponse\":{\"functionCallId\":\"" + callId + "\",\"value\":\"2 bags\"}}");
        Assert.Equal("2 bags", root.GetString("text"));
        Assert.Single(outbound);
    }

    [Fact]
    public void OpenUrl_runs_only_from_a_gesture()
    {
        var (renderer, outbound) = Create();
        var opened = new List<Uri>();
        renderer.UrlOpener = opened.Add;
        renderer.Process("""
            {"version":"1.0","createSurface":{"surfaceId":"s","catalogId":"https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json","components":[
              {"id":"root","component":"Button","child":"l","action":{"functionCall":{"@call":"openUrl","args":{"url":"https://example.com/docs"}}}},
              {"id":"l","component":"Text","text":"Docs"}]}}
            """);
        var root = renderer.GetSurface("s")!.Root!;
        Assert.True(root.InvokeAction());
        Assert.Equal("https://example.com/docs", opened.Single().ToString());
    }
}
