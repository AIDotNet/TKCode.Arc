using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using AtomTextBox = AtomUI.Desktop.Controls.TextBox;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class DrawerPage
{
    public static Control Create()
    {
        var drawer = new Drawer
        {
            Title = "Filters",
            Description = "Narrow the list without leaving it.",
            Content = Filters(),
            Actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { Result(new Button { Content = "Reset", Variant = ButtonVariant.Secondary }, false), Result(new Button { Content = "Apply" }, true) }
            }
        };
        Button Open(string label, DrawerSide side)
        {
            var button = new Button { Content = label, Variant = ButtonVariant.Secondary };
            button.Click += (_, _) =>
            {
                drawer.Side = side;
                drawer.IsOpen = true;
            };
            return button;
        }

        var log = GalleryUi.Text("Closed", ArcDesignTokenKind.TextSecondary);
        drawer.Closed += (_, e) => log.Text = $"Closed by {e.Reason}";

        var details = new Drawer
        {
            Title = "INV-2041",
            Description = "Paid on 12 March by Ada Lovelace.",
            Content = Lines(30)
        };
        var openDetails = new Button { Content = "Open record", Variant = ButtonVariant.Secondary, Command = details.OpenCommand };

        return GalleryUi.Column(20,
            GalleryUi.Card("Sides",
                "The panel springs in from its edge. Drag the header toward the edge to dismiss: past a third, or a quick flick, closes and keeps the flick's speed; the other way it resists.",
                GalleryUi.Column(12, GalleryUi.Row(Open("Right", DrawerSide.Right), Open("Left", DrawerSide.Left), Open("Top", DrawerSide.Top),
                    Open("Bottom", DrawerSide.Bottom), drawer), log)),
            GalleryUi.Card("Long content", "The header stays put while the body scrolls.", GalleryUi.Row(openDetails, details)));
    }

    private static Button Result(Button button, object result)
    {
        Dialog.SetResult(button, result);
        return button;
    }

    private static Control Filters()
    {
        Control Option(string label, bool on)
        {
            var row = new DockPanel { LastChildFill = true };
            var toggle = new Switch { IsChecked = on };
            DockPanel.SetDock(toggle, Dock.Right);
            row.Children.Add(toggle);
            row.Children.Add(GalleryUi.Text(label));
            return row;
        }

        return GalleryUi.Column(16, Option("Only open invoices", true), Option("Include drafts", false), Option("Show archived", false),
            GalleryUi.Text("Owner", ArcDesignTokenKind.TextSecondary), new AtomTextBox { PlaceholderText = "Anyone" });
    }

    private static Control Lines(int count)
    {
        var column = new StackPanel { Spacing = 10 };
        for (var i = 1; i <= count; i++)
        {
            column.Children.Add(GalleryUi.Text($"Line item {i} · Design review · {i * 12} min", i % 2 == 0 ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground));
        }
        return column;
    }
}
