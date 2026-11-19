using Avalonia.Controls;
using Avalonia.Media;
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
}
