using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Rendering;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class StyleKeysTests
{
    [Fact]
    public async Task InlineCodeAndListMarkerColors_FallBack_AndCanBeOverridden()
    {
        await HeadlessApp.Run(async () =>
        {
            var plain = new MarkdownView();
            var window = new Window { Width = 520, Height = 200, Content = plain };
            window.Show();
            await HeadlessApp.Settle();
            var fallback = MarkdownStyle.Resolve(plain);
            Assert.Same(fallback.Foreground, fallback.InlineCodeForeground);
            Assert.Same(fallback.SecondaryForeground, fallback.ListMarkerForeground);

            var purple = new SolidColorBrush(Color.Parse("#B4A9FF"));
            var bullet = new SolidColorBrush(Color.Parse("#8B7CF6"));
            var view = new MarkdownView { Markdown = "Run `dotnet build` now.\n\n- first\n- second\n\n1. one\n2. two", Margin = new(16) };
            view.Resources[MarkdownResourceKeys.InlineCodeForeground] = purple;
            view.Resources[MarkdownResourceKeys.InlineCodeBackground] = new SolidColorBrush(Color.Parse("#2E2A4A"));
            view.Resources[MarkdownResourceKeys.ListMarkerForeground] = bullet;
            window.Content = view;
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            var style = MarkdownStyle.Resolve(view);
            Assert.Same(purple, style.InlineCodeForeground);
            Assert.Same(bullet, style.ListMarkerForeground);
            HeadlessApp.Save(window, "style-inline-code-list-marker");
            window.Close();
        });
    }

    [Fact]
    public async Task CodeFontSizeAndCodeLineHeight_FallBack_AndCanBeOverridden()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new MarkdownView();
            var window = new Window { Width = 520, Height = 200, Content = view };
            window.Show();
            await HeadlessApp.Settle();

            // Neither Md.CodeFontSize nor Md.CodeLineHeight set: falls back to 0.9 * FontSize and 1.55 * CodeFontSize.
            var inherited = MarkdownStyle.Resolve(view);
            var expectedInheritedCodeSize = Math.Round(inherited.FontSize * 0.9);
            Assert.Equal(expectedInheritedCodeSize, inherited.CodeFontSize);
            Assert.Equal(Math.Round(expectedInheritedCodeSize * 1.55), inherited.CodeLineHeight);

            // Issue #178 scenario: 13 px body defaults to 12 px code / 19 px line height.
            view.Resources[MarkdownResourceKeys.FontSize] = 13.0;
            var body13 = MarkdownStyle.Resolve(view);
            Assert.Equal(13.0, body13.FontSize);
            Assert.Equal(12.0, body13.CodeFontSize);
            Assert.Equal(19.0, body13.CodeLineHeight);

            // Setting Md.CodeFontSize alone updates CodeFontSize and derives CodeLineHeight from it.
            view.Resources[MarkdownResourceKeys.CodeFontSize] = 16.0;
            var codeSizeOnly = MarkdownStyle.Resolve(view);
            Assert.Equal(13.0, codeSizeOnly.FontSize);
            Assert.Equal(16.0, codeSizeOnly.CodeFontSize);
            Assert.Equal(Math.Round(16.0 * 1.55), codeSizeOnly.CodeLineHeight);

            // Setting Md.CodeLineHeight explicitly overrides CodeLineHeight.
            view.Resources[MarkdownResourceKeys.CodeLineHeight] = 30.0;
            var bothSet = MarkdownStyle.Resolve(view);
            Assert.Equal(16.0, bothSet.CodeFontSize);
            Assert.Equal(30.0, bothSet.CodeLineHeight);

            // Setting Md.CodeLineHeight alone preserves the derived CodeFontSize fallback.
            view.Resources.Remove(MarkdownResourceKeys.CodeFontSize);
            var lineHeightOnly = MarkdownStyle.Resolve(view);
            Assert.Equal(12.0, lineHeightOnly.CodeFontSize);
            Assert.Equal(30.0, lineHeightOnly.CodeLineHeight);

            window.Close();
        });
    }

    [Fact]
    public async Task CodeFontSizeAndCodeLineHeight_ResolveViaAlias_AndUpdateRenderedCodeBlock()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new MarkdownView
            {
                Markdown = "```csharp\nvar a = 1;\nvar b = 2;\nvar c = 3;\n```",
                Margin = new(16),
            };
            view.Resources[MarkdownResourceKeys.FontSize] = 13.0;
            var window = new Window { Width = 520, Height = 360, Content = view };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var codeBlock = Assert.Single(view.GetVisualDescendants().OfType<CodeBlockView>());
            Assert.Equal(12.0, codeBlock.CurrentContext!.Style.CodeFontSize);
            Assert.Equal(19.0, codeBlock.CurrentContext!.Style.CodeLineHeight);
            var initialHeight = codeBlock.Bounds.Height;
            Assert.Equal(34 + 3 * 19.0 + 14, initialHeight);

            // Override via MarkdownResourceAlias pointing at a window-scoped host resource.
            window.Resources["Host.CodeFontSize"] = 20.0;
            view.Resources[MarkdownResourceKeys.CodeFontSize] = new MarkdownResourceAlias("Host.CodeFontSize");
            await HeadlessApp.Settle();

            var aliased = MarkdownStyle.Resolve(view);
            Assert.Equal(20.0, aliased.CodeFontSize);
            Assert.Equal(Math.Round(20.0 * 1.55), aliased.CodeLineHeight);
            Assert.Equal(20.0, codeBlock.CurrentContext!.Style.CodeFontSize);
            Assert.Equal(31.0, codeBlock.CurrentContext!.Style.CodeLineHeight);
            Assert.True(codeBlock.Bounds.Height > initialHeight);
            Assert.Equal(34 + 3 * 31.0 + 14, codeBlock.Bounds.Height);

            // Override Md.CodeLineHeight via MarkdownResourceAlias as well.
            window.Resources["Host.CodeLineHeight"] = 40.0;
            view.Resources[MarkdownResourceKeys.CodeLineHeight] = new MarkdownResourceAlias("Host.CodeLineHeight");
            await HeadlessApp.Settle();

            Assert.Equal(20.0, codeBlock.CurrentContext!.Style.CodeFontSize);
            Assert.Equal(40.0, codeBlock.CurrentContext!.Style.CodeLineHeight);
            Assert.Equal(34 + 3 * 40.0 + 14, codeBlock.Bounds.Height);

            window.Close();
        });
    }

    [Fact]
    public void MarkdownStyle_ObjectInitializer_DefaultsAndOverridesCodeMetrics()
    {
        var defaults = CreateStyle(fontSize: 14);
        Assert.Equal(Math.Round(14 * 0.9), defaults.CodeFontSize);
        Assert.Equal(Math.Round(Math.Round(14 * 0.9) * 1.55), defaults.CodeLineHeight);

        var codeSizeOnly = CreateStyle(fontSize: 14, codeFontSize: 16);
        Assert.Equal(16, codeSizeOnly.CodeFontSize);
        Assert.Equal(Math.Round(16 * 1.55), codeSizeOnly.CodeLineHeight);

        var lineHeightOnly = CreateStyle(fontSize: 14, codeLineHeight: 26);
        Assert.Equal(Math.Round(14 * 0.9), lineHeightOnly.CodeFontSize);
        Assert.Equal(26, lineHeightOnly.CodeLineHeight);

        var both = CreateStyle(fontSize: 14, codeFontSize: 16, codeLineHeight: 28);
        Assert.Equal(16, both.CodeFontSize);
        Assert.Equal(28, both.CodeLineHeight);

        static MarkdownStyle CreateStyle(double fontSize, double? codeFontSize = null, double? codeLineHeight = null)
        {
            if (codeFontSize is null && codeLineHeight is null)
            {
                return new MarkdownStyle
                {
                    Foreground = Brushes.Black,
                    SecondaryForeground = Brushes.Gray,
                    LinkForeground = Brushes.Blue,
                    SelectionBackground = Brushes.LightBlue,
                    CodeBackground = Brushes.WhiteSmoke,
                    CodeBorderBrush = Brushes.LightGray,
                    InlineCodeBackground = Brushes.WhiteSmoke,
                    InlineCodeForeground = Brushes.Black,
                    ListMarkerForeground = Brushes.Gray,
                    MarkBackground = Brushes.Yellow,
                    SearchHighlightBackground = Brushes.Yellow,
                    SearchCurrentBackground = Brushes.Orange,
                    QuoteBorderBrush = Brushes.Gray,
                    TableBorderBrush = Brushes.Gray,
                    TableHeaderBackground = Brushes.WhiteSmoke,
                    HoverBackground = Brushes.LightGray,
                    FontFamily = FontFamily.Default,
                    MonospaceFontFamily = FontFamily.Default,
                    FontSize = fontSize,
                    LineHeight = Math.Round(fontSize * 1.6),
                    BlockSpacing = 12,
                    CornerRadius = new CornerRadius(6),
                    IsDark = false,
                };
            }
            if (codeFontSize is { } cfs && codeLineHeight is null)
            {
                return new MarkdownStyle
                {
                    Foreground = Brushes.Black,
                    SecondaryForeground = Brushes.Gray,
                    LinkForeground = Brushes.Blue,
                    SelectionBackground = Brushes.LightBlue,
                    CodeBackground = Brushes.WhiteSmoke,
                    CodeBorderBrush = Brushes.LightGray,
                    InlineCodeBackground = Brushes.WhiteSmoke,
                    InlineCodeForeground = Brushes.Black,
                    ListMarkerForeground = Brushes.Gray,
                    MarkBackground = Brushes.Yellow,
                    SearchHighlightBackground = Brushes.Yellow,
                    SearchCurrentBackground = Brushes.Orange,
                    QuoteBorderBrush = Brushes.Gray,
                    TableBorderBrush = Brushes.Gray,
                    TableHeaderBackground = Brushes.WhiteSmoke,
                    HoverBackground = Brushes.LightGray,
                    FontFamily = FontFamily.Default,
                    MonospaceFontFamily = FontFamily.Default,
                    FontSize = fontSize,
                    LineHeight = Math.Round(fontSize * 1.6),
                    BlockSpacing = 12,
                    CornerRadius = new CornerRadius(6),
                    IsDark = false,
                    CodeFontSize = cfs,
                };
            }
            if (codeFontSize is null && codeLineHeight is { } clh)
            {
                return new MarkdownStyle
                {
                    Foreground = Brushes.Black,
                    SecondaryForeground = Brushes.Gray,
                    LinkForeground = Brushes.Blue,
                    SelectionBackground = Brushes.LightBlue,
                    CodeBackground = Brushes.WhiteSmoke,
                    CodeBorderBrush = Brushes.LightGray,
                    InlineCodeBackground = Brushes.WhiteSmoke,
                    InlineCodeForeground = Brushes.Black,
                    ListMarkerForeground = Brushes.Gray,
                    MarkBackground = Brushes.Yellow,
                    SearchHighlightBackground = Brushes.Yellow,
                    SearchCurrentBackground = Brushes.Orange,
                    QuoteBorderBrush = Brushes.Gray,
                    TableBorderBrush = Brushes.Gray,
                    TableHeaderBackground = Brushes.WhiteSmoke,
                    HoverBackground = Brushes.LightGray,
                    FontFamily = FontFamily.Default,
                    MonospaceFontFamily = FontFamily.Default,
                    FontSize = fontSize,
                    LineHeight = Math.Round(fontSize * 1.6),
                    BlockSpacing = 12,
                    CornerRadius = new CornerRadius(6),
                    IsDark = false,
                    CodeLineHeight = clh,
                };
            }
            return new MarkdownStyle
            {
                Foreground = Brushes.Black,
                SecondaryForeground = Brushes.Gray,
                LinkForeground = Brushes.Blue,
                SelectionBackground = Brushes.LightBlue,
                CodeBackground = Brushes.WhiteSmoke,
                CodeBorderBrush = Brushes.LightGray,
                InlineCodeBackground = Brushes.WhiteSmoke,
                InlineCodeForeground = Brushes.Black,
                ListMarkerForeground = Brushes.Gray,
                MarkBackground = Brushes.Yellow,
                SearchHighlightBackground = Brushes.Yellow,
                SearchCurrentBackground = Brushes.Orange,
                QuoteBorderBrush = Brushes.Gray,
                TableBorderBrush = Brushes.Gray,
                TableHeaderBackground = Brushes.WhiteSmoke,
                HoverBackground = Brushes.LightGray,
                FontFamily = FontFamily.Default,
                MonospaceFontFamily = FontFamily.Default,
                FontSize = fontSize,
                LineHeight = Math.Round(fontSize * 1.6),
                BlockSpacing = 12,
                CornerRadius = new CornerRadius(6),
                IsDark = false,
                CodeFontSize = codeFontSize!.Value,
                CodeLineHeight = codeLineHeight!.Value,
            };
        }
    }
}
