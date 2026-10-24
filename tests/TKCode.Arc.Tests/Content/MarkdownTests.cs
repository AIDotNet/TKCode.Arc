using TKCode.Arc.DesignTokens;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Highlighting;

namespace TKCode.Arc.Tests.Content;

public class MarkdownTests
{
    private const string Document = """
        # Release notes

        Some **bold** text, `inline code` and a [link](https://uiarc.dev).

        ```cs
        // A comment
        public int Count => 42;
        ```
        """;

    private static Window Show(Markdown markdown)
    {
        var window = TestHelpers.Show(markdown);
        window.Width = 640;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>StreamMark parses on a worker: waits until the view shows the complete document.</summary>
    private static Task Parsed(Markdown markdown) => WaitUntil(() =>
    {
        markdown.View!.ApplyLatest();
        Dispatcher.UIThread.RunJobs();
        return markdown.View.Snapshot is { IsComplete: true };
    });

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        condition().ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Renders_Text_With_The_Default_Services()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await Parsed(markdown);
            var view = markdown.View!;
            view.Stream.ShouldBeNull();
            var document = view.Document.ShouldNotBeNull();
            document.GetText(0, document.Length).ShouldBe(Document);
            MarkdownServices.GetServices(view).ShouldBeSameAs(Markdown.DefaultServices);
            MarkdownServices.GetServices(view).Registry.Highlighter.ShouldBeOfType<MarkdownCodeHighlighter>();
            Markdown.DefaultServices.CodeBlocks.ShowDownload.ShouldBeFalse();
            view.Snapshot.ShouldNotBeNull();
            view.Snapshot!.Count.ShouldBe(3);
            markdown.Bounds.Height.ShouldBeGreaterThan(100);
            markdown.IsStreaming.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Style_Comes_From_ArcDesign_Tokens()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            var view = markdown.View!;
            var style = MarkdownStyle.Resolve(view);
            TestHelpers.ColorOf(style.Foreground).ShouldBe(Token(view, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(style.SecondaryForeground).ShouldBe(Token(view, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(style.LinkForeground).ShouldBe(Token(view, ArcDesignTokenKind.AccentStrong));
            TestHelpers.ColorOf(style.CodeBackground).ShouldBe(Token(view, ArcDesignTokenKind.Surface));
            TestHelpers.ColorOf(style.CodeBorderBrush).ShouldBe(Token(view, ArcDesignTokenKind.Border));
            TestHelpers.ColorOf(style.InlineCodeBackground).ShouldBe(Token(view, ArcDesignTokenKind.SurfaceMuted));
            TestHelpers.ColorOf(style.QuoteBorderBrush).ShouldBe(Token(view, ArcDesignTokenKind.BorderStrong));
            style.MonospaceFontFamily.ShouldBe(ArcTokens.Get<FontFamily>(view, ArcDesignTokenKind.MonoFontFamily));
            style.FontFamily.ShouldBe(ArcTokens.Get<FontFamily>(view, ArcDesignTokenKind.BodyFontFamily));
            style.FontSize.ShouldBe(16);
            style.LineHeight.ShouldBe(26); // 16 × 1.65, Arc's prose leading
            style.BlockSpacing.ShouldBe(12);
            style.CornerRadius.ShouldBe(ArcTokens.Get<Avalonia.CornerRadius>(view, ArcDesignTokenKind.RadiusControl));
            style.IsDark.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Font_Size_And_Spacing_Overrides_Reach_The_View()
    {
        var markdown = new Markdown { Text = Document, FontSize = 14, BlockSpacing = 20 };
        var window = Show(markdown);
        try
        {
            var style = MarkdownStyle.Resolve(markdown.View!);
            style.FontSize.ShouldBe(14);
            style.LineHeight.ShouldBe(23);
            style.BlockSpacing.ShouldBe(20);
            markdown.LineHeight = 30;
            MarkdownStyle.Resolve(markdown.View!).LineHeight.ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Follows_Dark_Mode_And_The_Accent()
    {
        var markdown = new Markdown { Text = Document };
        var window = Show(markdown);
        try
        {
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder()
                                         .WithAlgorithms(ThemeAlgorithm.Dark)
                                         .WithArc(a => a.Accent(ArcAccentPreset.Violet))
                                         .Build());
            var view = markdown.View!;
            view.ActualThemeVariant.ShouldBe(ThemeVariant.Dark);
            var style = MarkdownStyle.Resolve(view);
            style.IsDark.ShouldBeTrue();
            TestHelpers.ColorOf(style.Foreground).ShouldBe(ArcPalette.Neutrals(true).Foreground);
            TestHelpers.ColorOf(style.LinkForeground).ShouldBe(Token(view, ArcDesignTokenKind.AccentStrong));
            TestHelpers.ColorOf(style.LinkForeground).ShouldNotBe(ArcPalette.Neutrals(true).Foreground);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Fenced_Code_Is_Painted_In_Arcs_Palette()
    {
        var highlighter = new MarkdownHighlightPlugin().Highlighter;
        highlighter.SupportsLanguage("csharp").ShouldBeTrue();
        highlighter.SupportsLanguage("no-such-language").ShouldBeFalse();
        const string code = "// note\npublic int Count => 42;";
        foreach (var dark in new[] { false, true })
        {
            using var session = highlighter.CreateSession("cs", dark ? HighlightThemeKind.Dark : HighlightThemeKind.Light);
            var lines = session.Update(code);
            lines.Count.ShouldBe(2);
            var comment = lines[0].ShouldHaveSingleItem();
            comment.Foreground.ShouldBe(CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Comment, dark).ToUInt32());
            comment.Style.ShouldBe(HighlightFontStyle.Italic);
            var keyword = lines[1].First(t => t.Start == 0);
            keyword.Length.ShouldBe("public".Length);
            keyword.Foreground.ShouldBe(CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Keyword, dark).ToUInt32());
            lines[1].ShouldContain(t => t.Start == code.IndexOf("42", StringComparison.Ordinal) - 8
                                        && t.Foreground == CodeBlockSyntaxColors.Get(CodeBlockSyntaxKind.Number, dark).ToUInt32());
        }
    }

    [AvaloniaFact]
    public void Streaming_Session_Reuses_Unchanged_Lines()
    {
        using var session = MarkdownCodeHighlighter.Shared.CreateSession("ts", HighlightThemeKind.Light);
        var first = session.Update("const a = 1;\nconst b");
        var second = session.Update("const a = 1;\nconst b = 2;\n");
        second.Count.ShouldBe(3);
        (second[0] == first[0]).ShouldBeTrue(); // same array: StreamMark keeps the line's layout
        second[1].Length.ShouldBeGreaterThan(first[1].Length);
        MarkdownCodeHighlighter.Shared.CreateSession("no-such-language", HighlightThemeKind.Light).Update("a\nb").Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task Append_Streams_A_Reply_Until_Complete()
    {
        var markdown = new Markdown { Text = "Old text" };
        var window = Show(markdown);
        try
        {
            markdown.Append("Hello ");
            markdown.IsStreaming.ShouldBeTrue();
            markdown.Text.ShouldBeNull();
            markdown.Classes.ShouldContain(":streaming");
            var stream = markdown.Stream.ShouldNotBeNull();
            markdown.View!.Stream.ShouldBeSameAs(stream);
            markdown.Append("**world**");
            markdown.Complete();
            await WaitUntil(() => !markdown.IsStreaming);
            stream.Document.IsComplete.ShouldBeTrue();
            stream.Document.GetText(0, stream.Document.Length).ShouldBe("Hello **world**");
            markdown.Classes.ShouldNotContain(":streaming");

            // A new Append after Complete starts a new reply.
            markdown.Append("Next");
            markdown.Stream.ShouldNotBeSameAs(stream);
            markdown.IsStreaming.ShouldBeTrue();

            // Setting Text drops the stream.
            markdown.Text = "Static";
            markdown.Stream.ShouldBeNull();
            markdown.IsStreaming.ShouldBeFalse();
            markdown.View.Stream.ShouldBeNull();
            markdown.View.Document!.GetText(0, markdown.View.Document.Length).ShouldBe("Static");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Services_Override_And_Inheritance()
    {
        var custom = new MarkdownServices { Plugins = [new MarkdownHighlightPlugin()] };
        var markdown = new Markdown { Text = "x" };
        var window = Show(markdown);
        try
        {
            MarkdownServices.GetServices(markdown.View!).ShouldBeSameAs(Markdown.DefaultServices);
            MarkdownServices.SetServices(window, custom);
            Dispatcher.UIThread.RunJobs();
            MarkdownServices.GetServices(markdown.View!).ShouldBeSameAs(custom);

            var own = new MarkdownServices();
            markdown.Services = own;
            Dispatcher.UIThread.RunJobs();
            MarkdownServices.GetServices(markdown.View!).ShouldBeSameAs(own);

            markdown.Services = null;
            Dispatcher.UIThread.RunJobs();
            MarkdownServices.GetServices(markdown.View!).ShouldBeSameAs(custom);
            window.ClearValue(MarkdownServices.ServicesProperty);
            Dispatcher.UIThread.RunJobs();
            MarkdownServices.GetServices(markdown.View!).ShouldBeSameAs(Markdown.DefaultServices);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Shows_The_Document_Without_Manual_Frames()
    {
        // The view applies snapshots on animation frames; the control also applies them if a frame is missed.
        var markdown = new Markdown { Text = "# Title\n\nBody" };
        var window = TestHelpers.Show(markdown);
        try
        {
            await WaitUntil(() => markdown.View!.Snapshot is { IsComplete: true, Count: 2 });
            markdown.Text = "Changed";
            await WaitUntil(() => markdown.View!.Snapshot is { IsComplete: true, Count: 1 });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Link_Clicks_Reach_The_LinkClicked_Event()
    {
        var markdown = new Markdown { Text = "[Arc](https://uiarc.dev)", OpensLinks = false };
        var window = Show(markdown);
        try
        {
            string? url = null;
            markdown.LinkClicked += (_, e) => url = e.Url;
            markdown.View!.RaiseEvent(new LinkClickedEventArgs(Markdown.LinkClickedEvent, markdown.View, "https://uiarc.dev", null));
            url.ShouldBe("https://uiarc.dev");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Markdown_Is_Collected_While_Its_Stream_Lives_On()
    {
        var stream = new MarkdownStream(smooth: false);
        stream.Push("Hello");
        // StreamMark's MarkdownView (alpha.25) stays subscribed to the stream's document after it is detached, which
        // keeps its templated parent alive on its own: a template without it isolates the control's own subscriptions.
        var weak = TestHelpers.ShowAndClose(() => new Markdown
        {
            Stream = stream,
            Template = new Avalonia.Controls.Templates.FuncControlTemplate<Markdown>((_, _) => new Border())
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        stream.Push(" world");
        stream.Complete();
        Dispatcher.UIThread.RunJobs();
        stream.Document.IsComplete.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task A_Stream_That_Ends_While_Away_Ends_When_The_Markdown_Comes_Back()
    {
        var stream = new MarkdownStream(smooth: false);
        var markdown = new Markdown { Stream = stream };
        var host = new Border { Child = markdown };
        var window = TestHelpers.Show(host);
        try
        {
            stream.Push("Hello");
            Dispatcher.UIThread.RunJobs();
            markdown.IsStreaming.ShouldBeTrue();

            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            stream.Complete();
            Dispatcher.UIThread.RunJobs();
            stream.Document.IsComplete.ShouldBeTrue();

            host.Child = markdown;
            Dispatcher.UIThread.RunJobs();
            markdown.IsStreaming.ShouldBeFalse();
            markdown.Classes.ShouldNotContain(":streaming");

            // A reply that ends while the control is away still counts as ended: the next Append starts a new one.
            markdown.Append("Next");
            var reply = markdown.Stream.ShouldNotBeNull();
            reply.ShouldNotBeSameAs(stream);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            markdown.Complete();
            await WaitUntil(() => reply.Document.IsComplete);
            markdown.Append("Again");
            markdown.Stream.ShouldNotBeSameAs(reply);
            markdown.IsStreaming.ShouldBeTrue();

            host.Child = markdown;
            Dispatcher.UIThread.RunJobs();
            markdown.Complete();
            await WaitUntil(() => !markdown.IsStreaming);
            markdown.Stream!.Document.GetText(0, markdown.Stream.Document.Length).ShouldBe("Again");
        }
        finally
        {
            window.Close();
        }
    }
}
