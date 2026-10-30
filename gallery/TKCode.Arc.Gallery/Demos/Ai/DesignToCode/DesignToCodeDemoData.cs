using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The design the design to code demos play: a small pricing card "screenshot" drawn in code in the card's theme colors,
/// its five layout regions, the React and Tailwind-style component a model could write for it part by part, and a working
/// preview of that card. The model credited is real (<see cref="AiModelCatalog"/>, one that reads images); the plan,
/// prices and copy are invented. Demo content only.
/// </summary>
internal static class DesignToCodeDemoData
{
    /// <summary>The drawn card's natural size (it scales to fit the design pane).</summary>
    public const double Width = 240;

    public const double Height = 300;

    /// <summary>The card's words, shared by the drawing and the live preview.</summary>
    public const string Badge = "Pro";

    public const string Title = "Pro plan";

    public const string Subtitle = "For growing teams";

    public const string Monthly = "Monthly";

    public const string Yearly = "Yearly";

    public const int MonthlyPrice = 24;

    public const int YearlyPrice = 19;

    public const string PerMonth = "/month";

    public const string Cta = "Start free trial";

    public static readonly string[] Features = ["Unlimited projects", "Shared workspaces", "Priority support"];

    /// <summary>The design: the drawn pricing card.</summary>
    public static DesignToCodeSource Source { get; } = new("pricing-card.png", null, DesignToCodeSourceKind.Sample)
    {
        Sketch = Sketch,
        SketchSize = new Size(Width, Height),
    };

    /// <summary>The model credited in the code header: a real model that reads images (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.Gemini4Argon.Name, ArcIcons.Sparkles);

    /// <summary>The file the code is written to.</summary>
    public const string FileName = "PricingCard.tsx";

    /// <summary>The frame Import design file brings in.</summary>
    public const string FrameName = "Pricing / Pro card";

    /// <summary>The five regions, in percent of the drawing (240 × 300 units).</summary>
    public static IList<DesignToCodeRegion> Regions { get; } =
    [
        new("header", "Header", 7.5, 6, 85, 21.5),
        new("billing", "Billing toggle", 7.5, 30.5, 85, 9.5),
        new("price", "Price", 7.5, 42.5, 52, 14.5),
        new("features", "Features", 7.5, 59, 85, 22),
        new("cta", "Button", 7.5, 83, 85, 12),
    ];

    /// <summary>The code, part by part, each tagged with the region it implements (null for the component's frame).</summary>
    public static IList<DesignToCodeSimulationPart> Parts { get; } =
    [
        new("""
         import { useState } from "react";
         import { Check } from "./icons";

         const features = ["Unlimited projects", "Shared workspaces", "Priority support"];

         export function PricingCard() {
           const [yearly, setYearly] = useState(false);
           const price = yearly ? 19 : 24;

           return (
             <section className="w-full max-w-sm rounded-2xl border bg-white p-6 shadow-sm">

         """, null),
        new("""
               <header className="space-y-1">
                 <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-xs font-medium text-indigo-600">
                   Pro
                 </span>
                 <h2 className="text-lg font-semibold">Pro plan</h2>
                 <p className="text-sm text-gray-500">For growing teams</p>
               </header>

         """, "header"),
        new("""
               <div role="radiogroup" className="mt-4 grid grid-cols-2 rounded-full bg-gray-100 p-1 text-sm">
                 {["Monthly", "Yearly"].map((label, i) => (
                   <button
                     key={label}
                     role="radio"
                     aria-checked={yearly === (i === 1)}
                     onClick={() => setYearly(i === 1)}
                     className="rounded-full py-1.5 aria-checked:bg-white aria-checked:shadow-sm"
                   >
                     {label}
                   </button>
                 ))}
               </div>

         """, "billing"),
        new("""
               <p className="mt-5 flex items-baseline gap-1">
                 <span className="text-4xl font-semibold tabular-nums">${price}</span>
                 <span className="text-sm text-gray-500">/month</span>
               </p>

         """, "price"),
        new("""
               <ul className="mt-5 space-y-2 text-sm">
                 {features.map(feature => (
                   <li key={feature} className="flex items-center gap-2">
                     <Check className="size-4 text-indigo-600" />
                     {feature}
                   </li>
                 ))}
               </ul>

         """, "features"),
        new("""
               <button className="mt-6 w-full rounded-xl bg-indigo-600 py-2.5 text-sm font-medium text-white">
                 Start free trial
               </button>

         """, "cta"),
        new("""
             </section>
           );
         }

         """, null),
    ];

    /// <summary>Renders the finished code's preview: the pricing card as working controls.</summary>
    public static Func<string, DesignToCodeSource, Control?> Preview { get; } = (_, _) => new DesignToCodeDemoPricingCard();

    /// <summary>What the loop does on the preview: it flips the billing switch to Yearly, so the preview visibly works.</summary>
    public static Action<Control> FlipBilling { get; } = control =>
    {
        if (control is DesignToCodeDemoPricingCard card)
        {
            card.IsYearly = true;
        }
    };

    /// <summary>A new simulation of the design (tests and code use it; the XAML demos spell it out).</summary>
    public static DesignToCodeSimulation Simulation() => new()
    {
        Source = Source,
        Regions = Regions,
        Parts = Parts,
        Model = Model,
        FileName = FileName,
        FrameName = FrameName,
        PreviewFactory = Preview,
        PreviewAction = FlipBilling,
    };

    /// <summary>Draws the pricing card on a 240 × 300 grid scaled to the frame, in the card's theme colors.</summary>
    public static void Sketch(DrawingContext context, Rect bounds, DesignToCodeSketchPalette palette)
    {
        var s = bounds.Width / Width;
        var radius = palette.CornerRadius;
        Rect R(double x, double y, double w, double h) => new(bounds.X + x * s, bounds.Y + y * s, w * s, h * s);

        var line = palette.Line is { } lineBrush ? new Pen(lineBrush) : null;
        context.DrawRectangle(palette.Paper, null, new RoundedRect(bounds, radius));
        if (line is not null)
        {
            context.DrawRectangle(null, line, new RoundedRect(bounds.Deflate(0.5), radius));
        }

        // Header: badge, title and subtitle.
        var badge = R(20, 20, 34, 16);
        context.DrawRectangle(palette.AccentSubtle, null, new RoundedRect(badge, badge.Height / 2));
        DrawText(context, palette.FontFamily, s, Badge, 9, FontWeight.SemiBold, palette.Accent, badge, center: true);
        DrawText(context, palette.FontFamily, s, Title, 17, FontWeight.SemiBold, palette.Ink, R(20, 42, 200, 22));
        DrawText(context, palette.FontFamily, s, Subtitle, 10.5, FontWeight.Normal, palette.MutedInk, R(20, 66, 200, 14));

        // Billing toggle: a well with a raised thumb on Monthly.
        var well = R(20, 94, 200, 24);
        context.DrawRectangle(palette.Well, null, new RoundedRect(well, well.Height / 2));
        var thumb = R(22, 96, 98, 20);
        context.DrawRectangle(palette.Paper, line, new RoundedRect(thumb, thumb.Height / 2));
        DrawText(context, palette.FontFamily, s, Monthly, 10, FontWeight.Medium, palette.Ink, thumb, center: true);
        DrawText(context, palette.FontFamily, s, Yearly, 10, FontWeight.Medium, palette.MutedInk, R(120, 96, 98, 20), center: true);

        // Price.
        var price = DrawText(context, palette.FontFamily, s, "$" + MonthlyPrice, 30, FontWeight.SemiBold, palette.Ink, R(20, 128, 120, 38));
        DrawText(context, palette.FontFamily, s, PerMonth, 10.5, FontWeight.Normal, palette.MutedInk,
            new Rect(bounds.X + 20 * s + price + 4 * s, bounds.Y + 147 * s, 80 * s, 14 * s));

        // Features: a check dot and a line of text each.
        for (var i = 0; i < Features.Length; i++)
        {
            var y = 184 + i * 20;
            var dot = R(20, y + 2, 10, 10);
            context.DrawEllipse(palette.AccentSubtle, null, dot);
            context.DrawEllipse(palette.Accent, null, dot.Center, 2.2 * s, 2.2 * s);
            DrawText(context, palette.FontFamily, s, Features[i], 10.5, FontWeight.Normal, palette.Ink, R(38, y, 180, 14));
        }

        // Call to action.
        var cta = R(20, 252, 200, 30);
        context.DrawRectangle(palette.Accent, null, new RoundedRect(cta, 9 * s));
        DrawText(context, palette.FontFamily, s, Cta, 11, FontWeight.SemiBold, palette.AccentForeground, cta, center: true);
    }

    /// <summary>Draws one line of text in <paramref name="box"/> (vertically centered) and returns its width.</summary>
    private static double DrawText(DrawingContext context, FontFamily font, double s, string text, double size, FontWeight weight, IBrush? brush, Rect box, bool center = false)
    {
        using var layout = new TextLayout(text, new Typeface(font, FontStyle.Normal, weight), Math.Max(1, size * s), brush ?? Brushes.Black);
        var x = center ? box.X + (box.Width - layout.Width) / 2 : box.X;
        var y = box.Y + (box.Height - layout.Height) / 2;
        layout.Draw(context, new Point(x, y));
        return layout.Width;
    }
}
