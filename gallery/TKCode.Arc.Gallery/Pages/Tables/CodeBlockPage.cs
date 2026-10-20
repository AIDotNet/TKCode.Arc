using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class CodeBlockPage
{
    private const string Greeting = """
        import { useState } from "react";

        /** Says hello, then counts the clicks. */
        export function Greeting({ name }: { name: string }) {
          const [count, setCount] = useState(0);
          return <button onClick={() => setCount(count + 1)}>Hello {name}, {count} clicks</button>;
        }
        """;

    private const string Styles = """
        .card {
          display: grid;
          gap: 12px;
          border-radius: 26px;
          padding: 24px;
        }
        """;

    private const string Config = """
        {
          "name": "arc-demo",
          "version": "1.4.0",
          "private": true,
          "scripts": { "dev": "next dev", "build": "next build" },
          "dependencies": { "motion": "^12.0.0", "lucide-react": "^0.460.0" }
        }
        """;

    private const string Csharp = """
        using Avalonia.Controls;

        namespace Shop;

        public sealed class CartView : UserControl
        {
            // Totals update when an item changes.
            public decimal Total { get; private set; }

            public void Add(decimal price, int quantity = 1)
            {
                Total += price * quantity;
                Status.Text = $"Total: {Total:C}";
            }
        }
        """;

    private const string Xaml = """
        <arc:CodeBlock FileName="greeting.tsx"
                       Language="tsx"
                       IsCollapsible="True"
                       MaxLines="6"
                       Code="{Binding Source}" />
        """;

    public static Control Create()
    {
        var longSource = string.Join("\n", Enumerable.Range(1, 40).Select(i =>
            i % 5 == 0 ? $"  // step {i}: checkpoint" : $"  const value{i} = compute({i}, \"item-{i}\");"));
        longSource = "export function pipeline() {\n" + longSource + "\n  return value40;\n}";

        var live = new CodeBlock { FileName = "greeting.tsx", Code = Greeting };
        var files = new (string Name, string Language, string Code)[]
        {
            ("greeting.tsx", "tsx", Greeting),
            ("card.module.css", "css", Styles),
            ("package.json", "json", Config)
        };
        var switcher = GalleryUi.Row(files.Select(f =>
        {
            var button = new Button { Content = f.Name, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
            button.Click += (_, _) =>
            {
                live.FileName = f.Name;
                live.Language = f.Language;
                live.Code = f.Code;
            };
            return (Control)button;
        }).ToArray());

        var copied = GalleryUi.Text("Copy the code to see the event.", ArcDesignTokenKind.TextSecondary);
        var main = new CodeBlock { FileName = "greeting.tsx", Code = Greeting };
        main.Copied += (_, _) => copied.Text = $"Copied {main.LineCount} lines at {DateTime.Now:T}";

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "File name header, lightweight highlighting and a copy action. Long lines scroll inside the block.",
                GalleryUi.Column(12, main, copied)),
            GalleryUi.Card("Collapsible", "IsCollapsible with MaxLines=6: the toggle expands the rest in place on a smooth height spring; the label swaps and the chevron turns.",
                new CodeBlock { FileName = "pipeline.ts", Language = "ts", Code = longSource, IsCollapsible = true, MaxLines = 6 }),
            GalleryUi.Card("Switching files", "A new file name swaps in while its slot follows the new width; the new source rises in and the height follows on a spring.",
                GalleryUi.Column(12, switcher, live)),
            GalleryUi.Card("Line numbers", "ShowLineNumbers adds a gutter (an extension); C# and XAML modes extend Arc's highlighter.",
                GalleryUi.Column(12,
                    new CodeBlock { FileName = "CartView.cs", Language = "cs", Code = Csharp, ShowLineNumbers = true },
                    new CodeBlock { FileName = "Usage.axaml", Language = "axaml", Code = Xaml })),
            GalleryUi.Card("Narrow", "Below 420px the padding and text shrink and the copy action keeps only its icon.",
                new CodeBlock { Width = 360, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Language = "css", FileName = "card.module.css", Code = Styles }));
    }
}
