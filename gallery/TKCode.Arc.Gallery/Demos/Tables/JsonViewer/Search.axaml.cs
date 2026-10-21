using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JsonViewerSearchDemo : UserControl
{
    public JsonViewerSearchDemo()
    {
        InitializeComponent();
        Viewer.Json = """
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
    }
}
