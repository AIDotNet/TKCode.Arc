using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class BottomSheetPage
{
    public static Control Create()
    {
        var trip = new BottomSheet
        {
            Title = "Trip to Lisbon",
            Description = "3 nights · 12–15 June",
            Content = Itinerary()
        };
        var detent = GalleryUi.Text("Closed", ArcDesignTokenKind.TextSecondary);
        trip.DetentChanged += (_, e) => detent.Text = $"Resting at {e.Fraction:P0}";
        trip.Opened += (_, _) => detent.Text = "Resting at 45%";
        trip.Closed += (_, e) => detent.Text = $"Closed by {e.Reason}";
        var openTrip = new Button { Content = "Trip details", Variant = ButtonVariant.Secondary, Command = trip.OpenCommand };

        var share = new BottomSheet
        {
            Title = "Share",
            Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95"),
            InitialDetentIndex = 1,
            Content = GalleryUi.Column(12, GalleryUi.Text("Copy link"), GalleryUi.Text("Send by email"), GalleryUi.Text("Embed"), GalleryUi.Text("Export as PDF"))
        };
        var openShare = new Button { Content = "Share options", Variant = ButtonVariant.Secondary, Command = share.OpenCommand };

        return GalleryUi.Column(20,
            GalleryUi.Card("Detents",
                "Opens at 45% and expands to 92%. Drag the header, flick it, or use ArrowUp / ArrowDown / Home / End on the grabber; ArrowDown on the sheet opens it fully before scrolling. The dim follows the sheet.",
                GalleryUi.Column(12, GalleryUi.Row(openTrip, trip), detent)),
            GalleryUi.Card("Three detents", "30%, 60% and 95%, opening at the middle one. A quick flick always moves at least one detent.", GalleryUi.Row(openShare, share)));
    }

    private static Control Itinerary()
    {
        var column = new StackPanel { Spacing = 12 };
        string[] items =
        [
            "Fri · Arrive 14:05 at LIS, taxi to Alfama", "Fri · Dinner at Taberna da Rua das Flores", "Sat · Tram 28 and the castle",
            "Sat · Sunset at Miradouro da Senhora do Monte", "Sun · Belém: tower, monastery, pastéis", "Sun · LX Factory",
            "Mon · Check out 11:00", "Mon · Depart 16:40"
        ];
        foreach (var item in items)
        {
            column.Children.Add(GalleryUi.Text(item));
        }
        for (var i = 1; i <= 12; i++)
        {
            column.Children.Add(GalleryUi.Text($"Note {i}: bring the museum pass.", ArcDesignTokenKind.TextSecondary));
        }
        return column;
    }
}
