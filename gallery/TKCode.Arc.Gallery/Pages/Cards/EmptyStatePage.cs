using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class EmptyStatePage
{
    public static Control Create()
    {
        var create = new Button { Content = "New project", Variant = ButtonVariant.Primary };
        var empty = new EmptyState
        {
            Label = "Projects",
            Title = "No projects yet",
            Description = "Projects keep your files, people and conversations in one place.",
            Action = create
        };
        create.Click += (_, _) =>
        {
            empty.Icon = ArcIcons.Check;
            empty.Title = "Project created";
            empty.Description = "Invite your team or start adding files whenever you're ready.";
            empty.Action = null;
        };
        var reset = new Button { Content = "Reset", Variant = ButtonVariant.Ghost };
        reset.Click += (_, _) =>
        {
            empty.Icon = null;
            empty.Title = "No projects yet";
            empty.Description = "Projects keep your files, people and conversations in one place.";
            empty.Action = create;
        };

        var search = new EmptyState
        {
            Icon = ArcIcons.Search,
            Title = "No results for “quarterly plan”",
            Description = "Check the spelling or try a broader term.",
            Action = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new Button { Content = "Clear search", Variant = ButtonVariant.Secondary },
                    new Button { Content = "Browse all", Variant = ButtonVariant.Ghost }
                }
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("First use", "Keep it mounted and change its properties: the icon pops, new copy rises in and the height springs.",
                GalleryUi.Column(12, Surface(empty), GalleryUi.Row(reset))),
            GalleryUi.Card("Zero results", "A custom icon and two actions.", Surface(search)));
    }

    private static Border Surface(Control content)
    {
        var border = new Border { CornerRadius = new CornerRadius(26), BorderThickness = new Thickness(1), Child = content };
        ArcTokens.Bind(border, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(border, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return border;
    }
}
