using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The GitHub button of the top bar: the mark, "Star" and the repository's star count (left out while it is unknown),
/// opening the repository in a new tab or the system browser.
/// </summary>
public sealed class GalleryGitHubButton : Button
{
    /// <summary>The GitHub mark, 16 by 16.</summary>
    private const string MarkData =
        "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0 0 16 8c0-4.42-3.58-8-8-8z";

    private readonly Border _divider;
    private readonly TextBlock _count;
    private bool _requested;

    /// <summary>A subclass takes its theme from <see cref="Button"/>: the style key would otherwise be its own type.</summary>
    protected override Type StyleKeyOverride => typeof(Button);

    public GalleryGitHubButton()
    {
        Variant = ButtonVariant.Secondary;
        Size = ControlSize.Small;
        IsPressScaleEnabled = false;

        var mark = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(MarkData), Width = 16, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(mark, Shape.FillProperty, ArcDesignTokenKind.Foreground);
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(label, TextBlock.TextProperty, GalleryShellLangResourceKind.GitHubStar);
        _divider = new Border { Width = 1, Height = 12, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        ArcTokens.Bind(_divider, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
        _count = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 13, FontWeight.Medium);
        _count.IsVisible = false;
        _count.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(mark);
        row.Children.Add(label);
        row.Children.Add(_divider);
        row.Children.Add(_count);
        Content = row;
        GalleryText.Bind(this, AutomationProperties.NameProperty, GalleryShellLangResourceKind.GitHubOpen);
        Click += (_, _) => _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(GalleryGitHub.RepositoryUrl);
    }

    /// <summary>The count shown, or null while it is unknown.</summary>
    public string? CountText => _count.IsVisible ? _count.Text : null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_requested)
        {
            _requested = true;
            _ = ShowStarsAsync();
        }
    }

    private async Task ShowStarsAsync()
    {
        if (await GalleryGitHub.GetStarsAsync() is not { } stars)
        {
            return;
        }
        _count.Text = GalleryGitHub.Format(stars);
        _count.IsVisible = true;
        _divider.IsVisible = true;
    }
}
