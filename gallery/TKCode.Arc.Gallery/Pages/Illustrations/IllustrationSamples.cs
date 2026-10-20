using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

/// <summary>Bits shared by the illustration pages.</summary>
internal static class IllustrationSamples
{
    /// <summary>Speed buttons (0.5×, 1×, 2×) for an illustration.</summary>
    public static Control SpeedRow(Illustration illustration)
    {
        Button Speed(string text, double speed)
        {
            var button = new Button { Content = text, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
            button.Click += (_, _) => illustration.Speed = speed;
            return button;
        }

        var restart = new Button { Content = "Start over", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        restart.Click += (_, _) => illustration.Restart();
        return GalleryUi.Row(Speed("0.5×", 0.5), Speed("1×", 1), Speed("2×", 2), restart);
    }

    /// <summary>The still frame shown under reduced motion.</summary>
    public static T Reduced<T>(T illustration) where T : Illustration
    {
        ArcMotion.SetLevel(illustration, ArcMotionLevel.Reduced);
        return illustration;
    }
}
