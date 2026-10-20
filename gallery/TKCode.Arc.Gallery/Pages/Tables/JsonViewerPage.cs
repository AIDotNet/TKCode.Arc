using System.Text.Json.Nodes;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class JsonViewerPage
{
    private const string Webhook = """
        {
          "id": "evt_1Q2w3E4r5T",
          "type": "invoice.paid",
          "created": 1735689600,
          "livemode": false,
          "data": {
            "customer": {
              "id": "cus_9s8d7f6g",
              "name": "Ada Lovelace",
              "email": "ada@example.com",
              "tags": ["vip", "beta", "annual"]
            },
            "lines": [
              { "description": "Pro plan", "amount": 4800, "quantity": 1 },
              { "description": "Additional seats", "amount": 1200, "quantity": 4 }
            ],
            "total": 9600,
            "currency": "usd",
            "discount": null,
            "metadata": {
              "order-id": "A-1042",
              "note": "Renewed early to keep the founding-member price for the team."
            }
          }
        }
        """;

    public static Control Create()
    {
        var status = GalleryUi.Text("Hover a row and use its copy actions, or press Ctrl/Cmd+C on the current row.", ArcDesignTokenKind.TextSecondary);
        var webhook = new JsonViewer { Json = Webhook, ExpandDepth = 2 };
        webhook.Copied += (_, e) => status.Text = $"Copied {(e.Kind == JsonViewerCopyKind.Path ? "path" : "value")} of {e.Path}";
        var current = GalleryUi.Text("Current: root", ArcDesignTokenKind.TextSecondary);
        webhook.CurrentRowChanged += (_, e) => current.Text = $"Current: {e.Path} ({e.ValueType})";

        var search = new JsonViewer { Json = Webhook, Query = "amount" };

        var items = new JsonArray();
        for (var i = 0; i < 1200; i++)
        {
            // JsonNode overload: the generic Add<T> serializes through reflection (not AOT safe).
            items.Add((JsonNode)new JsonObject
            {
                ["id"] = i + 1,
                ["sku"] = $"SKU-{i + 1:0000}",
                ["inStock"] = i % 3 != 0,
                ["price"] = Math.Round(4.5 + i * 0.25, 2)
            });
        }
        var catalog = new JsonViewer { Value = new JsonObject { ["count"] = 1200, ["items"] = items }, ExpandDepth = 2, MaxTreeHeight = 320 };

        var narrow = new JsonViewer
        {
            Json = """{ "user": { "id": 7, "handle": "grace", "roles": ["admin", "editor"], "active": true } }""",
            ExpandDepth = 3,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Webhook payload", "Values are colored by type; branches unfold row by row and the current row's highlight glides. Arrows, Home/End, Enter/Space and / for search.",
                GalleryUi.Column(12, webhook, current, status)),
            GalleryUi.Card("Search", "Matches open the branches that hold them; Enter and Shift+Enter step through them, Escape clears.", search),
            GalleryUi.Card("Long arrays", "1,200 items load in pages of 50; rows are virtualized, so expanding everything stays smooth.", catalog),
            GalleryUi.Card("Narrow", "At 420px and below, indentation tightens and the path bar drops the type.", narrow));
    }
}
