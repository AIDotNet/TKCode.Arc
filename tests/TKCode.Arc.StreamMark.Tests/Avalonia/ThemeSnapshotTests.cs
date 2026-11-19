using Avalonia.Controls;
using Avalonia.Media;
using StreamMark.Avalonia;
using Xunit;

namespace StreamMark.Avalonia.Tests;

/// <summary>M0：验证无头渲染管线与默认主题资源可用；M2 起替换为真正的 Markdown 渲染快照。</summary>
public sealed class ThemeSnapshotTests
{
    [Fact]
    public async Task DefaultTheme_ResolvesResources_AndRenders()
    {
        await HeadlessApp.Session.Dispatch(() =>
        {
            var text = new TextBlock { Text = "StreamMark", Margin = new(16) };
            var window = new Window { Width = 320, Height = 120, Content = text };
            window.Show();

            Assert.True(window.TryFindResource(MarkdownResourceKeys.Foreground, window.ActualThemeVariant, out var fg));
            Assert.IsAssignableFrom<IBrush>(fg);
            Assert.True(window.TryFindResource(MarkdownResourceKeys.BlockSpacing, out var size));
            Assert.Equal(12d, size);

            HeadlessApp.Save(window, "m0-theme");
            window.Close();
            return Task.CompletedTask;
        }, CancellationToken.None);
    }
}
