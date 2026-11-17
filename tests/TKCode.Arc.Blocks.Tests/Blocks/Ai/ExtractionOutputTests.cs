using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The texts the document extraction hands out: the typed output as JSON, the schema as JSON Schema, and the document as
/// plain text for the model (golden strings for the fixtures' invoice and email), plus how the JSON is cut into lines.
/// </summary>
public class ExtractionOutputTests
{
    private static ExtractionStore PlayedInvoice()
    {
        var store = new ExtractionStore(ExtractionFixtures.InvoiceSchema(), () => 0.8);
        foreach (var value in ExtractionFixtures.InvoiceEvents().OfType<ExtractionValueEvent>())
        {
            store.ApplyModelValue(value);
        }
        return store;
    }

    internal const string InvoiceJson = """
        {
          "vendor": "Oakridge Ceramics Ltd",
          "invoiceNumber": "OC-2026-1187",
          "issueDate": "2026-09-03",
          "dueDate": null,
          "lineItems": [
            {
              "description": "Glazed floor tiles, 20×20 cm",
              "quantity": 2,
              "unitPrice": 640.00,
              "amount": 1280.00
            },
            {
              "description": "Hand-thrown planters",
              "quantity": 3,
              "unitPrice": 210.00,
              "amount": 630.00
            },
            {
              "description": "Kiln firing service",
              "quantity": 2,
              "unitPrice": 95.00,
              "amount": 190.00
            },
            {
              "description": "Packing and freight",
              "quantity": 1,
              "unitPrice": 120.00,
              "amount": 120.00
            }
          ],
          "currency": "EUR",
          "subtotal": 2220.00,
          "vat": 466.20,
          "total": 2686.20,
          "iban": "NL70ZZTE0417263598"
        }
        """;

    [AvaloniaFact]
    public void The_Invoice_Output_Is_Typed_Json_With_The_Invalid_Date_As_Null()
    {
        var store = PlayedInvoice();
        store.EditByUser("dueDate", "16 Oct");
        store.BuildOutput().ToJson().ShouldBe(InvoiceJson);
    }

    [AvaloniaFact]
    public void Compact_Json_Has_No_Spaces_Between_Values()
    {
        var store = new ExtractionStore(ExtractionFixtures.InvoiceSchema(), () => 0.8);
        store.ApplyModelValue(new ExtractionValueEvent("vendor", "Oakridge \"Ceramics\" Ltd", null, 0.9));
        store.ApplyModelValue(new ExtractionValueEvent("lineItems.0.quantity", "2", null, 0.9));
        store.BuildOutput().ToJson(false).ShouldBe(
            """{"vendor":"Oakridge \"Ceramics\" Ltd","invoiceNumber":null,"issueDate":null,"dueDate":null,"lineItems":[{"description":null,"quantity":2,"unitPrice":null,"amount":null}],"currency":null,"subtotal":null,"vat":null,"total":null,"iban":null}""");
    }

    [AvaloniaFact]
    public void An_Empty_Table_Is_An_Empty_Array()
    {
        var json = new ExtractionStore(ExtractionFixtures.EmailSchema(), () => 0.8).BuildOutput().ToJson();
        json.ShouldContain("\n  \"items\": [],\n");
        json.ShouldStartWith("{\n  \"merchant\": null,\n");
    }

    [AvaloniaFact]
    public void Output_Built_By_Hand_Writes_Every_Kind_Of_Value()
    {
        var output = new ExtractionOutput([
            new("text", "line\nbreak <b> & ' + é"),
            new("flag", true),
            new("off", false),
            new("long", 9_007_199_254_740_993L),
            new("int", 3),
            new("double", 0.5),
            new("nan", double.NaN),
            new("date", new DateOnly(2026, 10, 23)),
            new("nested", new Dictionary<string, object?> { ["a"] = 1m }),
            new("list", new object?[] { "x", null, 2.50m })
        ]);
        output.ToJson(false).ShouldBe(
            """{"text":"line\nbreak <b> & ' + é","flag":true,"off":false,"long":9007199254740993,"int":3,"double":0.5,"nan":null,"date":"2026-10-23","nested":{"a":1},"list":["x",null,2.50]}""");
    }

    internal const string InvoiceSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Invoice",
          "description": "A supplier invoice.",
          "type": "object",
          "additionalProperties": false,
          "required": [
            "vendor",
            "invoiceNumber",
            "issueDate",
            "dueDate",
            "lineItems",
            "currency",
            "subtotal",
            "vat",
            "total",
            "iban"
          ],
          "properties": {
            "vendor": {
              "type": [
                "string",
                "null"
              ],
              "description": "Who sent the invoice"
            },
            "invoiceNumber": {
              "type": [
                "string",
                "null"
              ]
            },
            "issueDate": {
              "type": [
                "string",
                "null"
              ],
              "format": "date"
            },
            "dueDate": {
              "type": [
                "string",
                "null"
              ],
              "format": "date",
              "description": "The date payment is due"
            },
            "lineItems": {
              "type": [
                "array",
                "null"
              ],
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": [
                  "description",
                  "quantity",
                  "unitPrice",
                  "amount"
                ],
                "properties": {
                  "description": {
                    "type": [
                      "string",
                      "null"
                    ]
                  },
                  "quantity": {
                    "type": [
                      "number",
                      "null"
                    ]
                  },
                  "unitPrice": {
                    "type": [
                      "number",
                      "null"
                    ],
                    "multipleOf": 0.01
                  },
                  "amount": {
                    "type": [
                      "number",
                      "null"
                    ],
                    "multipleOf": 0.01
                  }
                }
              }
            },
            "currency": {
              "type": [
                "string",
                "null"
              ],
              "pattern": "^[A-Z]{3}$"
            },
            "subtotal": {
              "type": [
                "number",
                "null"
              ],
              "multipleOf": 0.01
            },
            "vat": {
              "type": [
                "number",
                "null"
              ],
              "multipleOf": 0.01
            },
            "total": {
              "type": [
                "number",
                "null"
              ],
              "multipleOf": 0.01
            },
            "iban": {
              "type": [
                "string",
                "null"
              ],
              "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$"
            }
          }
        }
        """;

    [AvaloniaFact]
    public void The_Invoice_Schema_Is_Draft_2020_12_Json_Schema()
    {
        ExtractionFixtures.InvoiceSchema().ToJsonSchema().ShouldBe(InvoiceSchemaJson);
    }

    [AvaloniaFact]
    public void The_Model_Id_Goes_In_A_Comment_And_The_Name_Is_The_Title_Without_One()
    {
        var schema = new ExtractionSchema { Name = "receipt" };
        schema.Fields.Add(new ExtractionField { Key = "guests", Type = ExtractionFieldType.Integer });
        schema.Fields.Add(new ExtractionField { Key = "guests", Type = ExtractionFieldType.Text });
        var json = schema.ToJsonSchema("lumen-extract-s");
        json.ShouldStartWith("""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$comment": "lumen-extract-s",
              "title": "receipt",
              "type": "object",
            """);
        json.ShouldContain("""
                "guests": {
                  "type": [
                    "integer",
                    "null"
                  ]
                }
              }
            """);
        // A repeated key is written once.
        json.Split("\"guests\"").Length.ShouldBe(3);
        new ExtractionSchema().ToJsonSchema().ShouldNotContain("title");
    }

    internal const string InvoiceText = """
        oakridge ceramics
        Invoice

        From
        Oakridge Ceramics Ltd
        14 Kiln Lane
        Bristol BS1 6QA, United Kingdom
        VAT GB 908 4417 22

        Bill to
        Bluefin Logistics BV
        Attn. Sanne de Vries
        Havenstraat 9
        3011 AB Rotterdam, Netherlands

        Invoice no.: OC-2026-1187
        Issue date: 3 September 2026
        Payment terms: 14 days net
        Customer no.: BL-5530

        Description | Qty | Unit price | Amount
        Glazed floor tiles, 20×20 cm (Batch 41, matte sand) | 2 pallets | 640.00 | 1,280.00
        Hand-thrown planters (Set of six, stoneware) | 3 | 210.00 | 630.00
        Kiln firing service (Customer pieces, two loads) | 2 | 95.00 | 190.00
        Packing and freight (Crated, insured) | 1 | 120.00 | 120.00

        Subtotal: 2,220.00
        VAT 21%: 466.20
        Total due: EUR 2,686.20

        Payment details

        Account holder: Oakridge Ceramics Ltd
        IBAN: NL70 ZZTE 0417 2635 98
        Reference: OC-2026-1187

        Please note our new account from 1 August 2026; the old account NL63 ZZTE 0398 1147 60 is closed.

        Oakridge Ceramics Ltd · 14 Kiln Lane · Bristol
        accounts@oakridge.example · oakridge.example
        Page 1 of 1
        """;

    [AvaloniaFact]
    public void The_Invoice_Reads_As_Plain_Text_Block_By_Block()
    {
        ExtractionFixtures.Invoice().ToPlainText().ShouldBe(InvoiceText);
    }

    [AvaloniaFact]
    public void The_Email_Reads_As_Plain_Text_With_Its_Header_First()
    {
        ExtractionFixtures.Email().ToPlainText().ShouldBe("""
            Your parcel FB-77302 has shipped
            Fernhill Books <orders@fernhillbooks.example>
            To Jonas Weber
            Tue, 22 Sep 2026, 08:15

            fernhill books

            Hello Jonas,

            Your order FB-77302 left our Leeds warehouse today. It is travelling with Swiftline Parcel and should reach you on Friday, 25 September.

            Tracking number: SWL 4410 2093 7781
            Shipping to: Jonas Weber

            Address
            Bergmannstraße 41, 10961 Berlin, Germany

            Items in this parcel

            Item | Qty | Price
            A Field Guide to Lichens | 1 | €24.00
            Notebook, dot grid, A5 | 2 | €19.00
            Bookmark set, brass | 1 | €8.50

            Shipping: €0.00
            Total: €51.50
            """);
    }

    [AvaloniaFact]
    public void Rules_And_Empty_Blocks_Add_No_Lines()
    {
        var document = new ExtractionDocument();
        document.Blocks.Add(new DocumentParagraph { Text = "One" });
        document.Blocks.Add(new DocumentRule());
        document.Blocks.Add(new DocumentParagraph { Text = "  " });
        document.Blocks.Add(new DocumentHeading { Text = "Two" });
        document.ToPlainText().ShouldBe("One\n\nTwo");
        new ExtractionDocument().ToPlainText().ShouldBe("");
    }

    [AvaloniaFact]
    public void Json_Lines_Know_The_Path_Of_Their_Value()
    {
        var lines = ExtractionJsonText.Split(InvoiceJson, true);
        string.Join("\n", lines.Select(l => l.Text)).ShouldBe(InvoiceJson);
        string? PathOf(string start) => lines.Single(l => l.Text.TrimStart().StartsWith(start, StringComparison.Ordinal)).Path;
        PathOf("\"vendor\"").ShouldBe("vendor");
        PathOf("\"dueDate\"").ShouldBe("dueDate");
        PathOf("\"lineItems\"").ShouldBe("lineItems");
        PathOf("\"iban\"").ShouldBe("iban");
        lines.Where(l => l.Text.TrimStart().StartsWith("\"amount\"", StringComparison.Ordinal)).Select(l => l.Path)
            .ShouldBe(["lineItems.0.amount", "lineItems.1.amount", "lineItems.2.amount", "lineItems.3.amount"]);
        // Braces, brackets and the row objects are structure.
        lines.Where(l => l.Path is null).Select(l => l.Text.Trim()).Distinct().ShouldBe(["{", "},", "}", "],"], ignoreOrder: true);
        lines.Count(l => l.Path is not null).ShouldBe(9 + 16 + 1);
    }

    [AvaloniaFact]
    public void Json_Lines_Are_Colored_By_Piece()
    {
        var lines = ExtractionJsonText.Split(InvoiceJson, true);
        string Kinds(string start)
        {
            var line = lines.First(l => l.Text.TrimStart().StartsWith(start, StringComparison.Ordinal));
            return string.Join(" ", line.Tokens.Select(t => $"{t.Kind}:{line.Text.Substring(t.Start, t.Length)}"));
        }
        Kinds("\"vendor\"").ShouldBe("Key:\"vendor\" Punctuation:: String:\"Oakridge Ceramics Ltd\" Punctuation:,");
        Kinds("\"dueDate\"").ShouldBe("Key:\"dueDate\" Punctuation:: Muted:null Punctuation:,");
        Kinds("\"vat\"").ShouldBe("Key:\"vat\" Punctuation:: Number:466.20 Punctuation:,");
        Kinds("\"lineItems\"").ShouldBe("Key:\"lineItems\" Punctuation:: Punctuation:[");
        Kinds("\"iban\"").ShouldBe("Key:\"iban\" Punctuation:: String:\"NL70ZZTE0417263598\"");

        var schema = ExtractionJsonText.Split(InvoiceSchemaJson, false);
        schema.ShouldAllBe(l => l.Path == null);
        var line = schema.First(l => l.Text.Contains("additionalProperties"));
        line.Tokens.Select(t => t.Kind).ShouldBe([JsonTokenKind.Key, JsonTokenKind.Punctuation, JsonTokenKind.Muted, JsonTokenKind.Punctuation]);
        var escaped = ExtractionJsonText.Split("{\n  \"a\\\"b\": \"c\\\\\",\n  \"t\": true\n}", true);
        escaped[1].Path.ShouldBe("a\"b");
        escaped[1].Tokens.Select(t => t.Kind).ShouldBe([JsonTokenKind.Key, JsonTokenKind.Punctuation, JsonTokenKind.String, JsonTokenKind.Punctuation]);
        escaped[2].Tokens[2].Kind.ShouldBe(JsonTokenKind.Number);
    }

    [AvaloniaFact]
    public void Arrays_Of_Plain_Values_And_Nested_Objects_Map_Their_Items()
    {
        var output = new ExtractionOutput([
            new("tags", new object?[] { "a", "b" }),
            new("meta", new Dictionary<string, object?> { ["pages"] = 1L }),
            new("empty", Array.Empty<object>())
        ]);
        var lines = ExtractionJsonText.Split(output.ToJson(), true);
        lines.Select(l => l.Path).ShouldBe([null, "tags", "tags.0", "tags.1", null, "meta", "meta.pages", null, "empty", null]);
    }
}
