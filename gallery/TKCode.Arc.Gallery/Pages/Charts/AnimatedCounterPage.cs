using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class AnimatedCounterPage
{
    public static Control Create()
    {
        var revenue = new AnimatedCounter { Label = "Revenue", Value = 48_250, Prefix = "$" };
        var users = new AnimatedCounter { Label = "Active users", Value = 999 };
        var rate = new AnimatedCounter { Label = "Conversion", Value = 3.42, Decimals = 2, Suffix = "%" };
        var random = new Random(7);
        var up = new Button { Content = "Increase", Variant = ButtonVariant.Secondary };
        up.Click += (_, _) =>
        {
            revenue.Value += random.Next(100, 4000);
            users.Value += random.Next(1, 40);
            rate.Value = Math.Round(rate.Value + random.NextDouble(), 2);
        };
        var down = new Button { Content = "Decrease", Variant = ButtonVariant.Secondary };
        down.Click += (_, _) =>
        {
            revenue.Value = Math.Max(0, revenue.Value - random.Next(100, 4000));
            users.Value = Math.Max(0, users.Value - random.Next(1, 40));
            rate.Value = Math.Max(0, Math.Round(rate.Value - random.NextDouble(), 2));
        };
        var onView = new AnimatedCounter { Label = "Rolled up on first view", Value = 128_400, AnimateOnView = true };
        return GalleryUi.Column(20,
            GalleryUi.Card("Rolling digits", "Only changed columns turn, in the direction the number moved; separators slide in and out (999 → 1,000).",
                GalleryUi.Column(20, GalleryUi.Row(revenue, users, rate), GalleryUi.Row(up, down))),
            GalleryUi.Card("Reveal on view", "Every digit rolls up from zero the first time 60% of the counter is visible.", onView));
    }
}
