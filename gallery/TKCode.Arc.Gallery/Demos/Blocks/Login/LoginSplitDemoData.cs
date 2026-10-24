using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Sample data of the LoginSplit demos: the Fieldnote team (invented names, initials avatars), a second product's words
/// and a picture drawn in code for the right column. Nothing here is a real person, company or photo.
/// </summary>
public static class LoginSplitDemoData
{
    /// <summary>Four invented teammates; Rowan is the account single sign-on falls back to.</summary>
    public static LoginSplitAccount[] Team() =>
    [
        new("Rowan Hale", "rowan@fieldnote.app"),
        new("Iris Vance", "iris@fieldnote.app"),
        new("Teo Marsh", "teo@fieldnote.app"),
        new("Juno Park", "juno@fieldnote.app")
    ];

    /// <summary>The words of Northdesk, an invented help-desk product, for the custom demo.</summary>
    public static LoginSplitLabels NorthdeskLabels() => new()
    {
        EmailTitle = "Back to the queue",
        EmailSubtitle = "Sign in with the address your team invited.",
        PasswordTitle = "Your password",
        SigningDescription = "Loading your open tickets and saved replies.",
        DoneTitle = first => $"Welcome back, {first}",
        DoneDescription = method => $"You used {method}. Your queue opens next.",
        DemoPasswordHint = password => $"Try {password} in this demo.",
        NoAccount = "New here?",
        SignUp = "Start a trial"
    };

    /// <summary>A building mark for the custom provider: a tower with windows (24 × 24, filled).</summary>
    public static Geometry WorkMark { get; } = Geometry.Parse(
        "M6 2h12a1 1 0 0 1 1 1v19h-5v-4h-4v4H5V3a1 1 0 0 1 1-1zm2 3v2h2V5H8zm6 0v2h2V5h-2zM8 9v2h2V9H8zm6 0v2h2V9h-2zM8 13v2h2v-2H8zm6 0v2h2v-2h-2z");

    private static Bitmap? s_coast;

    /// <summary>"A calm bay at dusk: layered blue-grey headlands over still water under a peach sky." Drawn once, 960 × 1200.</summary>
    public static Bitmap CoastPhoto()
    {
        if (s_coast is not null)
        {
            return s_coast;
        }
        const int width = 960;
        const int height = 1200;
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            // Sky: deep blue at the top through lilac to a peach horizon.
            context.DrawRectangle(new ImmutableLinearGradientBrush(
                [
                    new ImmutableGradientStop(0, Oklch.ToColor(0.5, 0.06, 265)),
                    new ImmutableGradientStop(0.45, Oklch.ToColor(0.72, 0.06, 320)),
                    new ImmutableGradientStop(0.62, Oklch.ToColor(0.86, 0.07, 55))
                ],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(0, 1, RelativeUnit.Relative)), null, new Rect(0, 0, width, height));
            // The low sun.
            context.DrawEllipse(new ImmutableSolidColorBrush(Oklch.ToColor(0.95, 0.06, 70)), null, new Point(610, 700), 70, 70);

            // Headlands from far (light, hazy) to near (dark), each a smooth ridge line.
            var horizon = 760.0;
            var ridges = new (double Base, double Amp, double Phase, double L, double C)[]
            {
                (horizon - 90, 50, 0.4, 0.66, 0.035),
                (horizon - 40, 70, 1.7, 0.52, 0.04),
                (horizon + 10, 90, 3.1, 0.38, 0.04)
            };
            foreach (var (baseLine, amp, phase, l, c) in ridges)
            {
                var ridge = new StreamGeometry();
                using (var g = ridge.Open())
                {
                    g.BeginFigure(new Point(0, height), true);
                    for (var x = 0; x <= width; x += 16)
                    {
                        var y = baseLine - amp * (0.55 + 0.45 * Math.Sin(x / 210.0 + phase)) * (0.6 + 0.4 * Math.Sin(x / 97.0 + phase * 2));
                        g.LineTo(new Point(x, y));
                    }
                    g.LineTo(new Point(width, height));
                    g.EndFigure(true);
                }
                context.DrawGeometry(new ImmutableSolidColorBrush(Oklch.ToColor(l, c, 270)), null, ridge);
            }

            // Still water with the sun's reflection.
            context.DrawRectangle(new ImmutableLinearGradientBrush(
                [
                    new ImmutableGradientStop(0, Oklch.ToColor(0.7, 0.05, 300)),
                    new ImmutableGradientStop(1, Oklch.ToColor(0.32, 0.05, 260))
                ],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(0, 1, RelativeUnit.Relative)), null, new Rect(0, horizon + 60, width, height - horizon - 60));
            var glint = new ImmutableSolidColorBrush(Oklch.ToColor(0.93, 0.06, 70, 0.55));
            for (var i = 0; i < 9; i++)
            {
                var y = horizon + 80 + i * 26;
                var half = 60 - i * 5;
                context.DrawRectangle(glint, null, new Rect(610 - half, y, half * 2, 3), 1.5, 1.5);
            }
        }
        s_coast = bitmap;
        return bitmap;
    }
}
