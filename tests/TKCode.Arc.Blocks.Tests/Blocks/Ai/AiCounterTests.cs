using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiCounter"/>: the thresholds at 79 / 80 / 94 / 95 / 100 / 101% of the limit, texts and their fallbacks, the
/// tooltip and accessible name, colors, the ring and the alert mark, tiny mode, the folded width and its opening spring,
/// and the last look kept while folding away.
/// </summary>
public class AiCounterTests
{
    private const int Max = 4000;

    private sealed class Rig : IDisposable
    {
        private readonly IDisposable? _clockInstall;

        public ManualFrameClock Clock { get; } = new();
        public AiCounter Counter { get; }
        public Window Window { get; }
        public int LeftCalls { get; private set; }

        public Rig(int length = 0, ArcMotionLevel level = ArcMotionLevel.Off, Action<AiCounter>? configure = null)
        {
            if (level == ArcMotionLevel.Full)
            {
                _clockInstall = Clock.Install();
            }
            Counter = new AiCounter
            {
                Max = Max,
                Length = length,
                LeftText = n =>
                {
                    LeftCalls++;
                    return $"{n:N0} left";
                },
                OverText = n => $"{n:N0} over",
                Tip = (l, m) => l > m ? $"{l - m:N0} characters over the limit" : $"{m - l:N0} of {m:N0} characters left"
            };
            configure?.Invoke(Counter);
            Window = TestHelpers.Show(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(24), Children = { Counter } }, level);
            Dispatcher.UIThread.RunJobs();
        }

        public ArcRing Ring => Counter.Ring!;
        public ArcGlyph Alert => TestHelpers.Descendant<ArcGlyph>(Counter, "PART_Alert");
        public TextBlock Label => TestHelpers.Descendant<TextBlock>(Counter, "PART_Text");

        public void Set(int length)
        {
            Counter.Length = length;
            Dispatcher.UIThread.RunJobs();
        }

        public void Frames(int count)
        {
            CoreFrames.Run(Clock, count);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose()
        {
            Window.Close();
            _clockInstall?.Dispose();
        }
    }

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    [AvaloniaTheory]
    [InlineData(3160, false, false, false, null)]
    [InlineData(3200, true, false, false, "800 left")]
    [InlineData(3760, true, false, false, "240 left")]
    [InlineData(3800, true, true, false, "200 left")]
    [InlineData(4000, true, true, false, "0 left")]
    [InlineData(4040, true, false, true, "40 over")]
    public void Thresholds_At_79_80_94_95_100_And_101_Percent(int length, bool shown, bool near, bool over, string? text)
    {
        using var rig = new Rig(length);
        var counter = rig.Counter;
        Assert.Equal(shown, counter.IsShown);
        Assert.Equal(near, counter.IsNear);
        Assert.Equal(over, counter.IsOver);
        Assert.Equal(shown, counter.Classes.Contains(AiCounter.ShownPseudoClass));
        Assert.Equal(near, counter.Classes.Contains(AiCounter.NearPseudoClass));
        Assert.Equal(over, counter.Classes.Contains(AiCounter.OverPseudoClass));
        Assert.Equal(text, counter.Text);
        Assert.Equal(Math.Min(1, length / (double)Max), counter.Used, 6);
        if (shown)
        {
            Assert.True(counter.Bounds.Width > 20);
            Assert.Equal(text, rig.Label.Text);
        }
        else
        {
            Assert.Equal(0, counter.Bounds.Width);
        }
    }

    [AvaloniaFact]
    public void The_Count_Changes_Its_Text_Color_And_Mark()
    {
        using var rig = new Rig(3300);
        var counter = rig.Counter;
        Assert.Equal("700 left", rig.Label.Text);
        Assert.Equal(Token(counter, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(rig.Label.Foreground));
        Assert.Equal(Token(counter, ArcDesignTokenKind.TextSecondary), TestHelpers.ColorOf(rig.Ring.Foreground));
        Assert.Equal(Token(counter, ArcDesignTokenKind.Border), TestHelpers.ColorOf(rig.Ring.TrackBrush));
        Assert.Equal(0.825, rig.Ring.Value, 6);
        Assert.True(rig.Ring.IsVisible);
        Assert.False(rig.Alert.IsVisible);

        rig.Set(3850);
        Assert.Equal("150 left", rig.Label.Text);
        Assert.Equal(Token(counter, ArcDesignTokenKind.Warning), TestHelpers.ColorOf(rig.Label.Foreground));
        Assert.Equal(Token(counter, ArcDesignTokenKind.Warning), TestHelpers.ColorOf(rig.Ring.Foreground));

        rig.Set(4100);
        Assert.Equal("100 over", rig.Label.Text);
        Assert.Equal(Token(counter, ArcDesignTokenKind.Danger), TestHelpers.ColorOf(rig.Label.Foreground));
        Assert.False(rig.Ring.IsVisible);
        Assert.True(rig.Alert.IsVisible);
        Assert.Equal(Token(counter, ArcDesignTokenKind.Danger), TestHelpers.ColorOf(rig.Alert.Foreground));
        Assert.Equal(16, rig.Alert.Size);
        Assert.Equal(2, rig.Alert.StrokeThickness);
    }

    [AvaloniaFact]
    public void Geometry_Follows_Arcs_Counter()
    {
        using var rig = new Rig(3300);
        var counter = rig.Counter;
        Assert.Equal(36, counter.Bounds.Height);
        // Arc's 18px ring (viewBox 20, r 8, stroke 2) is a 16.2px circle with a 1.8px stroke, centred 2px + 9px in.
        Assert.Equal(16.2, rig.Ring.Bounds.Width, 3);
        Assert.Equal(1.8, rig.Ring.Thickness, 3);
        Assert.False(rig.Ring.IsRoundCap);
        var ringCenter = rig.Ring.TranslatePoint(new Point(8.1, 8.1), counter)!.Value;
        Assert.Equal(11, ringCenter.X, 1);
        Assert.Equal(18, ringCenter.Y, 1);
        // 6px to the text, 6px after it.
        var label = rig.Label;
        var textLeft = label.TranslatePoint(default, counter)!.Value.X;
        Assert.Equal(26, textLeft, 1);
        Assert.Equal(textLeft + label.Bounds.Width + 6, counter.Bounds.Width, 1);
        Assert.Equal(12, label.FontSize);
        Assert.Equal(16.8, label.LineHeight, 3);
        Assert.Contains(label.FontFeatures!, f => f.Tag == "tnum");
    }

    [AvaloniaFact]
    public void Texts_Fall_Back_To_The_Number()
    {
        using var rig = new Rig(3500, configure: c =>
        {
            c.LeftText = null;
            c.OverText = null;
            c.Tip = null;
        });
        var number = 500.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Equal(number, rig.Counter.Text);
        // Without a tip the visible text names it.
        Assert.Equal(number, AutomationProperties.GetName(rig.Counter));
        Assert.Equal(number, Tooltip.GetTip(rig.Counter));
        rig.Set(5234);
        Assert.Equal(1234.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), rig.Counter.Text);
    }

    [AvaloniaFact]
    public void The_Tooltip_And_Name_Give_The_Full_Count_Only_While_Shown()
    {
        using var rig = new Rig(3300);
        var counter = rig.Counter;
        Assert.Equal("700 of 4,000 characters left", Tooltip.GetTip(counter));
        Assert.Equal("700 of 4,000 characters left", AutomationProperties.GetName(counter));
        rig.Set(4040);
        Assert.Equal("40 characters over the limit", Tooltip.GetTip(counter));
        Assert.Equal("40 characters over the limit", AutomationProperties.GetName(counter));
        rig.Set(100);
        Assert.Null(Tooltip.GetTip(counter));
        Assert.Null(AutomationProperties.GetName(counter));

        var peer = ControlAutomationPeer.CreatePeerForElement(counter);
        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
        Assert.False(peer.IsContentElement());
        Assert.Empty(peer.GetChildren());
        rig.Set(3999);
        Assert.True(peer.IsContentElement());
        Assert.True(peer.IsControlElement());
        Assert.Equal("1 of 4,000 characters left", peer.GetName());
    }

    [AvaloniaFact]
    public void Tiny_Keeps_Only_The_Ring_Or_The_Mark()
    {
        using var rig = new Rig(3300, configure: c => c.IsTiny = true);
        var counter = rig.Counter;
        Assert.Contains(AiCounter.TinyPseudoClass, counter.Classes);
        Assert.False(rig.Label.IsEffectivelyVisible);
        Assert.True(rig.Ring.IsEffectivelyVisible);
        // 2 + 18 + 6.
        Assert.Equal(26, counter.Bounds.Width, 1);
        Assert.Equal("700 of 4,000 characters left", Tooltip.GetTip(counter));
        rig.Set(4100);
        Assert.True(rig.Alert.IsEffectivelyVisible);
        Assert.Equal(26, counter.Bounds.Width, 1);

        counter.IsTiny = false;
        Dispatcher.UIThread.RunJobs();
        Assert.True(rig.Label.IsEffectivelyVisible);
        Assert.True(counter.Bounds.Width > 26);
    }

    [AvaloniaFact]
    public void No_Limit_Never_Shows()
    {
        using var rig = new Rig(10_000, configure: c => c.Max = 0);
        Assert.False(rig.Counter.IsShown);
        Assert.False(rig.Counter.IsOver);
        Assert.Equal(0, rig.Counter.Used);
        Assert.Equal(0, rig.Counter.Bounds.Width);
    }

    [AvaloniaFact]
    public void Typing_Below_The_Threshold_Formats_Nothing()
    {
        using var rig = new Rig(10);
        for (var i = 11; i < 3000; i += 37)
        {
            rig.Set(i);
        }
        Assert.Equal(0, rig.LeftCalls);
        rig.Set(3200);
        Assert.Equal(1, rig.LeftCalls);
    }

    [AvaloniaFact]
    public void Thresholds_Can_Move()
    {
        using var rig = new Rig(50, configure: c =>
        {
            c.Max = 100;
            c.CounterThreshold = 0.5;
            c.WarningThreshold = 0.9;
        });
        Assert.True(rig.Counter.IsShown);
        Assert.False(rig.Counter.IsNear);
        rig.Set(90);
        Assert.True(rig.Counter.IsNear);
        rig.Set(49);
        Assert.False(rig.Counter.IsShown);
        // 95% of 100 is 94.999… in binary; 95 still counts.
        rig.Counter.CounterThreshold = 0.95;
        rig.Set(95);
        Assert.True(rig.Counter.IsShown);
    }

    [AvaloniaFact]
    public void It_Opens_From_Zero_Width_And_Folds_Away_With_Its_Last_Look()
    {
        using var rig = new Rig(3100, ArcMotionLevel.Full);
        var counter = rig.Counter;
        Assert.Equal(0, counter.Bounds.Width);

        rig.Set(3300);
        rig.Frames(4);
        var partWay = counter.Bounds.Width;
        var presence = TestHelpers.Descendant<PresenceHost>(counter);
        Assert.InRange(partWay, 1, 70);
        Assert.InRange(presence.Opacity, 0.05, 0.99);
        rig.Frames(60);
        var open = counter.Bounds.Width;
        Assert.True(open > partWay);
        Assert.Equal(1, presence.Opacity);

        // Over, then deleted below the threshold: it folds away still reading "N over" in the danger color.
        rig.Set(4040);
        rig.Frames(30);
        rig.Set(200);
        Assert.False(counter.IsShown);
        Assert.False(counter.IsOver);
        Assert.Equal("40 over", rig.Label.Text);
        Assert.Contains(AiCounter.OverPseudoClass, counter.Classes);
        Assert.True(rig.Alert.IsVisible);
        rig.Frames(3);
        Assert.True(counter.Bounds.Width > 0);
        rig.Frames(60);
        Assert.Equal(0, counter.Bounds.Width);
    }

    [AvaloniaFact]
    public void The_Ring_Moves_In_160ms_At_Full_Motion_And_At_Once_When_Reduced()
    {
        using (var rig = new Rig(3300, ArcMotionLevel.Full))
        {
            rig.Set(3600);
            // A ControlTheme transition: it runs on Avalonia's clock, so only its target is checked here.
            Assert.True(rig.Ring.Value <= 0.9 + 1e-9);
            Assert.NotNull(rig.Ring.Transitions);
            Assert.Contains(rig.Ring.Transitions!, t => t is Avalonia.Animation.TransitionBase b && b.Property == ArcRing.ValueProperty);
        }
        using (var rig = new Rig(3300, ArcMotionLevel.Reduced))
        {
            rig.Set(3600);
            Assert.Equal(0.9, rig.Ring.Value, 6);
            Assert.DoesNotContain(rig.Ring.Transitions ?? new Avalonia.Animation.Transitions(), t => t is Avalonia.Animation.TransitionBase b && b.Property == ArcRing.ValueProperty);
        }
    }
}
