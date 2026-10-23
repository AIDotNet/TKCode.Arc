using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>
/// Core <see cref="TextRangeRenderer"/> and <see cref="DecoratedTextBlock"/>: per-line decorations without relayout.
/// Headless text has fixed advances (14 DIP a character at size 14), so the line breaks and rectangles are exact.
/// </summary>
public class TextRangeDecorationTests
{
    // At 14 DIP a character and 110 DIP wide, "alpha beta gamma …" wraps after every one or two words (11 lines).
    private const string Sentence = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda";

    private static (Window Window, DecoratedTextBlock Block) Show(string text, double width = 110, double lineHeight = double.NaN)
    {
        var block = new DecoratedTextBlock
        {
            Text = text,
            Width = width,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Black,
            LineHeight = lineHeight
        };
        var window = TestHelpers.Show(new StackPanel { Children = { block } });
        window.UpdateLayout();
        return (window, block);
    }

    private static DrawingGroup Record(Action<DrawingContext> draw)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            draw(context);
        }
        return group;
    }

    private static List<GeometryDrawing> Shapes(DrawingGroup group)
    {
        var shapes = new List<GeometryDrawing>();
        void Walk(DrawingGroup g)
        {
            foreach (var child in g.Children)
            {
                if (child is GeometryDrawing shape)
                {
                    shapes.Add(shape);
                }
                else if (child is DrawingGroup nested)
                {
                    Walk(nested);
                }
            }
        }
        Walk(group);
        return shapes;
    }

    [AvaloniaFact]
    public void Defaults()
    {
        var block = new DecoratedTextBlock();
        block.Decorations.ShouldBeEmpty();
        block.ClipToBounds.ShouldBeFalse();
        var decoration = new TextRangeDecoration(3, 4);
        decoration.Start.ShouldBe(3);
        decoration.Length.ShouldBe(4);
        decoration.Extent.ShouldBe(TextRangeExtent.Text);
        decoration.Opacity.ShouldBe(1);
        decoration.UnderlineOffset.ShouldBe(2);
        decoration.Background.ShouldBeNull();
        decoration.StrikeThrough.ShouldBeNull();
        decoration.Underline.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Range_That_Wraps_Gives_One_Rectangle_Per_Line_Without_The_Hanging_Space()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var layout = block.TextLayout;
            layout.TextLines.Count.ShouldBeGreaterThan(5);
            var rects = TextRangeRenderer.GetRects(layout, 0, Sentence.Length);
            rects.Count.ShouldBe(layout.TextLines.Count);
            for (var i = 0; i < rects.Count; i++)
            {
                rects[i].Left.ShouldBe(0, 0.01);
                // The line's content width: the space that hangs at the end of a wrapped line is not decorated.
                rects[i].Width.ShouldBe(layout.TextLines[i].Width, 0.01);
                if (i > 0)
                {
                    rects[i].Top.ShouldBeGreaterThan(rects[i - 1].Top);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Range_Inside_One_Line_Is_One_Rectangle_At_The_Right_Offset()
    {
        var (window, block) = Show(Sentence);
        try
        {
            // "alpha " is the first line; characters 1..3 are "lph" (14 DIP each).
            var rects = TextRangeRenderer.GetRects(block.TextLayout, 1, 3);
            rects.Count.ShouldBe(1);
            rects[0].Left.ShouldBe(14, 0.01);
            rects[0].Width.ShouldBe(42, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Range_Across_Two_Lines_Starts_Mid_Line_And_Continues_From_The_Left_Edge()
    {
        var (window, block) = Show(Sentence);
        try
        {
            // Index 8 is inside "beta " (line 2); 14 characters later is inside the third line.
            var rects = TextRangeRenderer.GetRects(block.TextLayout, 8, 8);
            rects.Count.ShouldBeGreaterThan(1);
            rects[0].Left.ShouldBeGreaterThan(0);
            rects[1].Left.ShouldBe(0, 0.01);
            rects[1].Top.ShouldBeGreaterThan(rects[0].Top);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Text_Extent_Is_The_Font_Box_And_The_Line_Extent_Follows_LineHeight()
    {
        var (window, block) = Show(Sentence, lineHeight: 40);
        try
        {
            var text = TextRangeRenderer.GetRects(block.TextLayout, 0, 5, TextRangeExtent.Text)[0];
            var line = TextRangeRenderer.GetRects(block.TextLayout, 0, 5, TextRangeExtent.Line)[0];
            line.Height.ShouldBe(40, 0.01);
            text.Height.ShouldBeLessThan(line.Height - 10);
            text.Height.ShouldBe(14, 0.5); // descent − ascent of the 14 DIP font
            // The font box sits inside the line box.
            text.Top.ShouldBeGreaterThanOrEqualTo(line.Top);
            text.Bottom.ShouldBeLessThanOrEqualTo(line.Bottom);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Outset_Grows_Every_Line_Rectangle_On_All_Sides()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var layout = block.TextLayout;
            var plain = new TextRangeDecoration(0, 5) { Background = Brushes.Red };
            var grown = new TextRangeDecoration(0, 5) { Background = Brushes.Red, Outset = new Thickness(2, 3, 4, 5) };
            var a = Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [plain], default))).Single().Geometry!.Bounds;
            var b = Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [grown], default))).Single().Geometry!.Bounds;
            b.Left.ShouldBe(a.Left - 2, 0.01);
            b.Top.ShouldBe(a.Top - 3, 0.01);
            b.Width.ShouldBe(a.Width + 2 + 4, 0.01);
            b.Height.ShouldBe(a.Height + 3 + 5, 0.01);

            // A negative outset insets it.
            var inset = new TextRangeDecoration(0, 5) { Background = Brushes.Red, Outset = new Thickness(-1) };
            var c = Shapes(Record(ctx => TextRangeRenderer.DrawBackgrounds(ctx, layout, [inset], default))).Single().Geometry!.Bounds;
            c.Width.ShouldBe(a.Width - 2, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Origin_Moves_The_Drawing_And_The_Corner_Radius_Is_Clamped_To_Half_The_Height()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var layout = block.TextLayout;
            var square = new TextRangeDecoration(0, 5) { Background = Brushes.Red };
            var flat = Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [square], new Point(10, 20)))).Single().Geometry!.Bounds;
            flat.Left.ShouldBe(10, 0.01);
            flat.Top.ShouldBeGreaterThan(20);

            var line = new TextRangeLine(new Rect(0, 4, 70, 14), 12, 8);
            RoundedRect Background(double radius, Thickness outset = default)
            {
                var decoration = new TextRangeDecoration { CornerRadius = radius, Outset = outset };
                TextRangeRenderer.TryGetBackground(decoration, line, new Point(10, 20), out var rounded).ShouldBeTrue();
                return rounded;
            }
            var normal = Background(4);
            normal.Rect.ShouldBe(new Rect(10, 24, 70, 14));
            normal.RadiiTopLeft.X.ShouldBe(4);
            normal.RadiiBottomRight.Y.ShouldBe(4);
            // Above half the height it is clamped, not rejected (a capsule); below zero or NaN it is square.
            Background(500).RadiiTopLeft.X.ShouldBe(7);
            Background(500, new Thickness(0, 3)).RadiiTopLeft.X.ShouldBe(10);
            Background(-3).RadiiTopLeft.X.ShouldBe(0);
            Background(double.NaN).RadiiTopLeft.X.ShouldBe(0);
            Background(2, new Thickness(2)).Rect.ShouldBe(new Rect(8, 22, 74, 18));

            // Nothing left after a big inset: nothing to draw.
            TextRangeRenderer.TryGetBackground(new TextRangeDecoration { Outset = new Thickness(-8) }, line, default, out _).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opacity_Pushes_A_Layer_Zero_Draws_Nothing_And_No_Brush_Draws_Nothing()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var layout = block.TextLayout;
            var half = new TextRangeDecoration(0, 5) { Background = Brushes.Red, Opacity = 0.5 };
            var group = Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [half], default));
            group.Children.OfType<DrawingGroup>().Single().Opacity.ShouldBe(0.5, 0.001);

            var full = new TextRangeDecoration(0, 5) { Background = Brushes.Red };
            Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [full], default)).Children.OfType<DrawingGroup>().ShouldBeEmpty();

            var none = new TextRangeDecoration(0, 5) { Background = Brushes.Red, Opacity = 0 };
            var plain = new TextRangeDecoration(0, 5);
            Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [none, plain], default))).ShouldBeEmpty();
            var negative = new TextRangeDecoration(0, 5) { Background = Brushes.Red, Opacity = -1 };
            var notANumber = new TextRangeDecoration(0, 5) { Background = Brushes.Red, Opacity = double.NaN };
            Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [negative, notANumber], default))).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Strikethrough_And_Underline_Cross_Every_Line_At_The_Font_Positions()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var layout = block.TextLayout;
            var strike = new Pen(Brushes.Gray, 1);
            var dotted = new Pen(Brushes.Orange, 1, new DashStyle([1, 2], 0));
            var both = new TextRangeDecoration(8, 8) { StrikeThrough = strike, Underline = dotted, Outset = new Thickness(5) };
            var rects = TextRangeRenderer.GetRects(layout, 8, 8);
            var shapes = Shapes(Record(c => TextRangeRenderer.DrawLines(c, layout, [both], default)));
            shapes.Count.ShouldBe(rects.Count * 2);

            var first = layout.TextLines[1];
            var lineTop = layout.TextLines[0].Height;
            // Strikethrough: baseline − 3.6 (the font's position), underline: baseline + the 2 DIP offset. The outset does not widen them.
            var strikeShape = shapes[0].Geometry!.Bounds;
            strikeShape.Center.Y.ShouldBe(lineTop + first.Baseline - 3.6, 0.5);
            strikeShape.Left.ShouldBe(rects[0].Left, 0.01);
            strikeShape.Right.ShouldBe(rects[0].Right, 0.01);
            shapes[1].Geometry!.Bounds.Center.Y.ShouldBe(lineTop + first.Baseline + 2, 0.5);
            shapes[0].Pen.ShouldBe(strike);
            shapes[1].Pen!.DashStyle.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ranges_Outside_The_Text_Are_Clipped_Or_Ignored_Never_Thrown()
    {
        var (window, block) = Show("Short text", width: 400);
        try
        {
            var layout = block.TextLayout;
            TextRangeRenderer.GetRects(layout, 0, 0).ShouldBeEmpty();
            TextRangeRenderer.GetRects(layout, 4, -3).ShouldBeEmpty();
            TextRangeRenderer.GetRects(layout, 500, 10).ShouldBeEmpty();
            TextRangeRenderer.GetRects(layout, -50, 10).ShouldBeEmpty();
            TextRangeRenderer.GetRects(layout, int.MaxValue, 5).ShouldBeEmpty();
            TextRangeRenderer.GetRects(layout, 0, int.MaxValue).Count.ShouldBe(1);

            // Clipped to the text: [−5, 5) becomes [0, 5) and [6, 100) becomes [6, 10).
            TextRangeRenderer.GetRects(layout, -5, 10)[0].Width.ShouldBe(5 * 14, 0.01);
            TextRangeRenderer.GetRects(layout, 6, 100)[0].Width.ShouldBe(4 * 14, 0.01);

            var odd = new TextRangeDecoration[]
            {
                new(500, 10) { Background = Brushes.Red },
                new(0, 0) { Background = Brushes.Red },
                new(-9, 3) { Background = Brushes.Red, StrikeThrough = new Pen(Brushes.Red) }
            };
            Record(c =>
            {
                TextRangeRenderer.DrawBackgrounds(c, layout, odd, default);
                TextRangeRenderer.DrawLines(c, layout, odd, default);
            }).Children.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Text_Has_No_Rectangles()
    {
        var (window, block) = Show("", width: 100);
        try
        {
            TextRangeRenderer.GetRects(block.TextLayout, 0, 5).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inlines_Are_Indexed_By_Their_Concatenated_Text_And_A_Control_Counts_As_One_Character()
    {
        var block = new DecoratedTextBlock { FontSize = 14, Width = 400, Foreground = Brushes.Black };
        block.Inlines!.Add(new Run("Hello "));
        block.Inlines.Add(new InlineUIContainer { Child = new Border { Width = 30, Height = 14 } });
        block.Inlines.Add(new Run(" world"));
        var window = TestHelpers.Show(new StackPanel { Children = { block } });
        try
        {
            window.UpdateLayout();
            var layout = block.TextLayout;
            // "Hello " = 0..5 (6 × 14 = 84), the control = index 6 (30 wide), " world" starts at index 7.
            var hello = TextRangeRenderer.GetRects(layout, 0, 5).Single();
            hello.Left.ShouldBe(0, 0.01);
            hello.Width.ShouldBe(5 * 14, 0.5);
            var control = TextRangeRenderer.GetRects(layout, 6, 1).Single();
            control.Left.ShouldBe(84, 0.5);
            control.Width.ShouldBe(30, 0.5);
            var world = TextRangeRenderer.GetRects(layout, 8, 5).Single();
            world.Left.ShouldBe(84 + 30 + 14, 0.5);
            world.Width.ShouldBe(5 * 14, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Layout_Measures_Again_And_Repainting_Does_Not()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var layout = block.TextLayout;
            var decoration = new TextRangeDecoration(0, Sentence.Length) { Background = Brushes.Red };
            Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [decoration], default));
            decoration.MeasureCount.ShouldBe(1);

            decoration.Opacity = 0.4;
            decoration.Background = Brushes.Blue;
            decoration.Outset = new Thickness(2);
            decoration.CornerRadius = 4;
            decoration.UnderlineOffset = 3;
            for (var i = 0; i < 5; i++)
            {
                Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [decoration], default));
            }
            decoration.MeasureCount.ShouldBe(1);

            // The same layout and range with another extent is a different measurement.
            decoration.Extent = TextRangeExtent.Line;
            Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [decoration], default));
            decoration.MeasureCount.ShouldBe(2);

            // Wider text wraps less: a new layout, a new measurement, fewer lines.
            var before = Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, layout, [decoration], default))).Count;
            block.Width = 800;
            window.UpdateLayout();
            var after = Shapes(Record(c => TextRangeRenderer.DrawBackgrounds(c, block.TextLayout, [decoration], default))).Count;
            decoration.MeasureCount.ShouldBe(3);
            after.ShouldBeLessThan(before);
            after.ShouldBe(block.TextLayout.TextLines.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Decorations_Redraw_But_Never_Relayout()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var decoration = new TextRangeDecoration(0, 20) { Background = Brushes.Red };
            block.Decorations.Add(decoration);
            block.IsMeasureValid.ShouldBeTrue();
            block.IsArrangeValid.ShouldBeTrue();
            var measured = block.DesiredSize;

            decoration.Opacity = 0.25;
            block.InvalidateDecorations();
            block.Decorations.Add(new TextRangeDecoration(30, 5) { StrikeThrough = new Pen(Brushes.Gray) });
            block.Decorations.Remove(decoration);
            block.Decorations.Clear();
            block.IsMeasureValid.ShouldBeTrue();
            block.IsArrangeValid.ShouldBeTrue();
            block.DesiredSize.ShouldBe(measured);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class SpyBlock : DecoratedTextBlock
    {
        public int Renders { get; set; }

        protected override void RenderTextLayout(DrawingContext context, Point origin)
        {
            Renders++;
            base.RenderTextLayout(context, origin);
        }
    }

    [AvaloniaFact]
    public void It_Renders_With_Decorations_Selection_And_Every_Decoration_Kind()
    {
        var block = new SpyBlock
        {
            Text = Sentence,
            Width = 110,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Black,
            SelectionBrush = Brushes.LightBlue
        };
        block.Decorations.Add(new TextRangeDecoration(0, 20) { Background = Brushes.Gold, CornerRadius = 4, Outset = new Thickness(2), Opacity = 0.6 });
        block.Decorations.Add(new TextRangeDecoration(24, 12) { StrikeThrough = new Pen(Brushes.Gray, 1), Tag = "old" });
        block.Decorations.Add(new TextRangeDecoration(40, 10) { Underline = new Pen(Brushes.Orange, 1, new DashStyle([1, 2], 0)), Background = Brushes.Gold });
        var panel = new StackPanel { Children = { block } };
        var window = TestHelpers.Show(panel);
        try
        {
            window.UpdateLayout();
            block.SelectionStart = 2;
            block.SelectionEnd = 12;
            block.SelectedText.ShouldBe(Sentence.Substring(2, 10));
            block.Decorations[1].Tag.ShouldBe("old");
            block.Renders = 0;
            block.InvalidateVisual();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            block.Renders.ShouldBeGreaterThan(0);

            // Detaching lets go of the layout: the caches clear and the block can be shown again.
            panel.Children.Remove(block);
            panel.Children.Add(block);
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_A_Decoration_Lets_Go_Of_The_Layout()
    {
        var (window, block) = Show(Sentence);
        try
        {
            var decoration = new TextRangeDecoration(0, 5) { Background = Brushes.Red };
            block.Decorations.Add(decoration);
            Record(c => TextRangeRenderer.DrawBackgrounds(c, block.TextLayout, [decoration], default));
            decoration.MeasureCount.ShouldBe(1);
            block.Decorations.Remove(decoration);
            // The cache was dropped: drawing again with the very same layout measures again.
            Record(c => TextRangeRenderer.DrawBackgrounds(c, block.TextLayout, [decoration], default));
            decoration.MeasureCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}
