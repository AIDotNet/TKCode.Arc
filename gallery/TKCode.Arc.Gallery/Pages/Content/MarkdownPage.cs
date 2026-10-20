using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Pages;

public static class MarkdownPage
{
    private const string Document = """
        # Release notes

        Arc **0.4** brings a `Markdown` control rendered by *StreamMark*. Code is highlighted with TextMate grammars and
        painted in Arc's own syntax palette, so a fenced block reads like a [CodeBlock](https://uiarc.dev/components/code-block).

        ## What changed

        - Headings, lists, tables, quotes and links follow the ArcDesign tokens.
        - Fenced code highlights **C#**, **TypeScript**, **JSON**, **XAML**, shell and many more.
          - Unknown languages stay plain text.
        - Light, dark and the accent switch live.

        1. Add the package.
        2. Call `UseArc()` in `UseAtomUI(...)`.
        3. Drop `<arc:Markdown Text="..." />` into a view.

        - [x] Static documents
        - [x] Streamed replies
        - [ ] Math and diagrams (add StreamMark's RaTeX and Mermaid plugins)

        > Color supports, but never replaces, the source text.
        > — Arc's code block notes

        | Feature | Status | Notes |
        |:--------|:------:|------:|
        | Text | Ready | `Text` |
        | Streaming | Ready | `Append` / `Complete` |
        | Links | Ready | `LinkClicked` |

        ## Code

        ```cs
        public sealed class CartView : UserControl
        {
            // Totals update when an item changes.
            public decimal Total { get; private set; }

            public void Add(decimal price, int quantity = 1) => Total += price * quantity;
        }
        ```

        ```ts
        export async function loadUser(id: number): Promise<User | null> {
          const response = await fetch(`/api/users/${id}`);
          return response.ok ? ((await response.json()) as User) : null;
        }
        ```

        ```json
        { "name": "arc-demo", "version": "1.4.0", "private": true, "tags": ["ui", "motion"] }
        ```

        ```xml
        <!-- A streamed reply in a chat bubble -->
        <arc:Markdown FontSize="14" Text="{Binding Reply}" />
        ```

        ```bash
        dotnet add package TKCode.Arc --prerelease
        dotnet run --project src/App
        ```
        """;

    private const string Reply = """
        Sure — here is the short version.

        **Streaming** keeps the document stable while text arrives: finished blocks never re-render, the last block grows,
        and new words fade in.

        ```cs
        markdown.Append(chunk);   // as tokens arrive
        markdown.Complete();      // when the reply ends
        ```

        | Step | Call |
        |------|------|
        | Start | `Append("...")` |
        | Finish | `Complete()` |

        Anything else?
        """;

    public static Control Create()
    {
        var clicked = GalleryUi.Text("Links open in the browser; LinkClicked reports them first.", ArcDesignTokenKind.TextSecondary);
        var document = new Markdown { Text = Document };
        document.LinkClicked += (_, e) => clicked.Text = $"LinkClicked: {e.Url}";

        return GalleryUi.Column(20,
            GalleryUi.Card("Document", "Headings, lists, a task list, a quote, a table, links, inline code and fenced code in several languages.",
                GalleryUi.Column(12, document, clicked)),
            GalleryUi.Card("Streamed reply", "Append pushes text as it arrives; StreamMark paces it, repairs unfinished Markdown and fades new words in. Complete ends the stream.",
                BuildStreaming()),
            GalleryUi.Card("Chat size", "FontSize=\"14\" for message bubbles; line height and code size follow.",
                new Markdown { FontSize = 14, Text = "Deployed **web-42** to `staging`. Logs: [build #1042](https://uiarc.dev).\n\n```sh\nkubectl rollout status deploy/web\n```" }));
    }

    private static Control BuildStreaming()
    {
        var markdown = new Markdown { Text = "_Press **Stream reply** to simulate a model answering._" };
        var status = GalleryUi.Text("Idle", ArcDesignTokenKind.TextMuted, 13);
        var stream = new Button { Content = "Stream reply", Variant = ButtonVariant.Primary, Size = ControlSize.Small };
        var reset = new Button { Content = "Reset", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        DispatcherTimer? timer = null;

        markdown.PropertyChanged += (_, e) =>
        {
            if (e.Property == Markdown.IsStreamingProperty)
            {
                status.Text = markdown.IsStreaming ? "Streaming…" : "Complete";
                stream.IsEnabled = !markdown.IsStreaming;
            }
        };
        stream.Click += (_, _) =>
        {
            timer?.Stop();
            var random = new Random(7);
            var position = 0;
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Normal, (_, _) =>
            {
                if (position >= Reply.Length)
                {
                    timer?.Stop();
                    markdown.Complete();
                    return;
                }
                var length = Math.Min(Reply.Length - position, random.Next(2, 9));
                markdown.Append(Reply.Substring(position, length));
                position += length;
            });
            timer.Start();
        };
        reset.Click += (_, _) =>
        {
            timer?.Stop();
            markdown.Text = "_Press **Stream reply** to simulate a model answering._";
            status.Text = "Idle";
        };

        return GalleryUi.Column(12, GalleryUi.Row(stream, reset, status), markdown);
    }
}
