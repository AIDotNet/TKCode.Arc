using TKCode.Arc.Motion;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAnswerGlyphSwap"/> (the answer's icon swap: copy → check, outline → filled rating): in place when off, the
/// spec's motion at full (new 240ms fade in from 0.85 on the snappy spring, old 160ms fade out to 0.85), a crossfade without
/// scale when reduced.
/// </summary>
public class AiAnswerGlyphSwapTests
{
    private static AiAnswerGlyphSwap Swap() => new() { Data = ArcIcons.Copy, Size = 16 };

    [AvaloniaFact]
    public void Off_Changes_The_Glyph_In_Place()
    {
        using var rig = new AiAnswerActionsRig(Swap());
        var swap = (AiAnswerGlyphSwap)rig.Content;
        var glyph = swap.ShownGlyph!;
        swap.Data = ArcIcons.Check;
        rig.Settle();
        swap.ShownGlyph.ShouldBeSameAs(glyph);
        glyph.Data.ShouldBeSameAs(ArcIcons.Check);
        swap.ExitingCount.ShouldBe(0);
        swap.IsFilled = true;
        glyph.IsFilled.ShouldBeTrue();
        swap.Bounds.Size.ShouldBe(new Avalonia.Size(16, 16));
    }

    [AvaloniaFact]
    public void Full_Fades_The_New_Glyph_In_From_085_And_The_Old_Out_To_085()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiAnswerActionsRig(Swap(), ArcMotionLevel.Full, clock);
        var swap = (AiAnswerGlyphSwap)rig.Content;
        var old = swap.ShownGlyph!;
        swap.Data = ArcIcons.Check;
        rig.Settle();
        var fresh = swap.ShownGlyph!;
        fresh.ShouldNotBeSameAs(old);
        fresh.Data.ShouldBeSameAs(ArcIcons.Check);
        swap.ExitingCount.ShouldBe(1);
        fresh.Opacity.ShouldBeLessThan(0.1);
        swap.ShownScale.ShouldBe(AiAnswerGlyphSwap.SwapScale, 0.01);
        rig.Frames(100);
        old.Opacity.ShouldBeInRange(0.01, 0.6);
        ((Avalonia.Media.ScaleTransform)old.RenderTransform!).ScaleX.ShouldBeInRange(0.85, 0.99);
        fresh.Opacity.ShouldBeInRange(0.2, 0.99);
        rig.Frames(84);
        rig.Drain(() => swap.ExitingCount == 0).ShouldBeTrue();
        old.GetVisualParent().ShouldBeNull();
        rig.Frames(84);
        fresh.Opacity.ShouldBe(1, 0.001);
        rig.Frames(400);
        swap.ShownScale.ShouldBe(1, 0.005);
    }

    [AvaloniaFact]
    public void A_New_Fill_Swaps_Too()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiAnswerActionsRig(new AiAnswerGlyphSwap { Data = ArcIcons.ThumbsUp }, ArcMotionLevel.Full, clock);
        var swap = (AiAnswerGlyphSwap)rig.Content;
        var outline = swap.ShownGlyph!;
        swap.IsFilled = true;
        rig.Settle();
        swap.ShownGlyph.ShouldNotBeSameAs(outline);
        swap.ShownGlyph!.IsFilled.ShouldBeTrue();
        outline.IsFilled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Reduced_Crossfades_In_120ms_Without_Scaling()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiAnswerActionsRig(Swap(), ArcMotionLevel.Reduced, clock);
        var swap = (AiAnswerGlyphSwap)rig.Content;
        swap.Data = ArcIcons.Check;
        rig.Settle();
        swap.ShownScale.ShouldBe(1);
        rig.Frames(67);
        swap.ShownGlyph!.Opacity.ShouldBeInRange(0.2, 0.8);
        swap.ShownScale.ShouldBe(1);
        rig.Frames(67);
        swap.ShownGlyph.Opacity.ShouldBe(1, 0.001);
        rig.Drain(() => swap.ExitingCount == 0).ShouldBeTrue();
    }
}
