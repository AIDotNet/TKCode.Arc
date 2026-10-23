using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The JSON and Schema code view of the document extraction: lines and their paths, the box's geometry, the linked line's
/// tint and the changed line's flash at every motion level, pointer events, line bounds for the link, sideways scrolling,
/// the relayout of changed lines only, drawing only what is visible, and what screen readers get.
/// </summary>
public class ExtractionJsonViewTests
{
    private static ExtractionStore PlayedInvoice()
    {
        var store = new ExtractionStore(ExtractionFixtures.InvoiceSchema(), () => 0.8);
        foreach (var value in ExtractionFixtures.InvoiceEvents().OfType<ExtractionValueEvent>())
        {
            store.ApplyModelValue(value);
        }
        return store;
    }

    private static (ExtractionJsonView View, Window Window) Show(ExtractionJsonView view, ArcMotionLevel level = ArcMotionLevel.Off, double width = 436)
    {
        view.Width = width;
        view.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        view.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = TestHelpers.Show(view, level);
        window.Width = 600;
        window.Height = 1000;
        Settle();
        return (view, window);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static ExtractionJsonView InvoiceView(Func<string, int>? tones = null) => new()
    {
        Output = PlayedInvoice().BuildOutput(),
        Tones = tones,
        AutomationName = "Typed output as JSON"
    };

    /// <summary>The middle of the line showing <paramref name="path"/>, in window coordinates.</summary>
    private static Point LineCenter(ExtractionJsonView view, Window window, string path)
    {
        view.TryGetLineBounds(path, window, out var bounds).ShouldBeTrue(path);
        return bounds.Center;
    }

    [AvaloniaFact]
    public void The_Output_Is_Shown_Line_By_Line_As_Its_Json()
    {
        var view = InvoiceView();
        var output = view.Output!;
        view.Text.ShouldBe(output.ToJson());
        view.Lines.Count.ShouldBe(output.ToJson().Split('\n').Length);
        view.LineIndexOf("vendor").ShouldBe(1);
        view.LineIndexOf("lineItems").ShouldBe(5);
        view.LineIndexOf("lineItems.0.description").ShouldBe(7);
        view.LineIndexOf("iban").ShouldBe(view.Lines.Count - 2);
        view.LineIndexOf("nothing").ShouldBe(-1);
    }

    [AvaloniaFact]
    public void A_Schema_Is_Shown_Without_Paths_And_The_Output_Wins_When_Both_Are_Set()
    {
        var schema = ExtractionFixtures.InvoiceSchema().ToJsonSchema("lumen-extract-s");
        var view = new ExtractionJsonView { SchemaText = schema };
        view.Text.ShouldBe(schema);
        view.Lines.ShouldAllBe(l => l.Path == null);
        view.LineIndexOf("vendor").ShouldBe(-1);

        view.Output = PlayedInvoice().BuildOutput();
        view.LineIndexOf("vendor").ShouldBe(1);
        view.Output = null;
        view.Text.ShouldBe(schema);
    }

    [AvaloniaFact]
    public void The_Box_Is_Monospace_12_On_20_4_Lines_Padded_10_0_In_A_Rounded_Border()
    {
        var (view, window) = Show(InvoiceView());
        try
        {
            var lines = view.Drawing.ShouldNotBeNull();
            view.FontSize.ShouldBe(12);
            view.LineHeight.ShouldBe(20.4);
            view.CornerRadius.ShouldBe(new CornerRadius(14));
            view.BorderThickness.ShouldBe(new Thickness(1));
            TestHelpers.ColorOf(view.Background).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.SurfaceMuted));
            TestHelpers.ColorOf(view.BorderBrush).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Border));
            view.FontFamily.ShouldBe(ArcTokens.Get<FontFamily>(view, ArcDesignTokenKind.MonoFontFamily));
            // Layout rounding puts the heights on whole pixels.
            lines.Bounds.Height.ShouldBe(view.Lines.Count * 20.4, 0.5);
            view.Bounds.Height.ShouldBe(view.Lines.Count * 20.4 + 20 + 2, 1);
            // The lines start 1 (border) + 10 (padding) down and reach both sides of the box.
            lines.TranslatePoint(default, view)!.Value.ShouldBe(new Point(1, 11));
            lines.Bounds.Width.ShouldBeGreaterThanOrEqualTo(434);
            view.Focusable.ShouldBeTrue();
            view.IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pieces_Take_The_Spec_Colors()
    {
        var (view, window) = Show(InvoiceView());
        try
        {
            var style = view.Drawing!.Style.ShouldNotBeNull();
            var foreground = ArcTokens.GetColor(view, ArcDesignTokenKind.Foreground);
            TestHelpers.ColorOf(style.Key).ShouldBe(foreground);
            TestHelpers.ColorOf(style.Punctuation).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(style.Muted).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(style.String).ShouldBe(Oklch.MixOklab(ArcTokens.GetColor(view, ArcDesignTokenKind.Success), foreground, 0.22));
            TestHelpers.ColorOf(style.Number).ShouldBe(Oklch.MixOklab(ArcTokens.GetColor(view, ArcDesignTokenKind.Series1), foreground, 0.2));
            style.FontSize.ShouldBe(12);
            style.LineHeight.ShouldBe(20.4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Linked_Line_Is_Tinted_13_Percent_In_Its_Group_Color()
    {
        var (view, window) = Show(InvoiceView(path => path.StartsWith("lineItems", StringComparison.Ordinal) ? 2 : path == "iban" ? 4 : 1));
        try
        {
            view.LinkedTint.ShouldBe(Colors.Transparent);
            view.LinkedPath = "lineItems.1.amount";
            var series2 = ArcTokens.GetColor(view, ArcDesignTokenKind.Series2);
            view.LinkedTint.ShouldBe(Color.FromArgb((byte)Math.Round(series2.A * 0.13), series2.R, series2.G, series2.B));
            view.LinkedPath = "iban";
            var series4 = ArcTokens.GetColor(view, ArcDesignTokenKind.Series4);
            view.LinkedTint.ShouldBe(Color.FromArgb((byte)Math.Round(series4.A * 0.13), series4.R, series4.G, series4.B));
            view.LinkedPath = null;
            view.LinkedTint.ShouldBe(Colors.Transparent);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tint_Fades_In_160_Ms_At_Full_Motion_And_120_At_Reduced()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        foreach (var (level, duration) in new[] { (ArcMotionLevel.Full, 160), (ArcMotionLevel.Reduced, 120) })
        {
            var (view, window) = Show(InvoiceView(), level);
            try
            {
                var full = (byte)Math.Round(ArcTokens.GetColor(view, ArcDesignTokenKind.Series1).A * 0.13);
                view.LinkedPath = "vendor";
                clock.Advance(TimeSpan.FromMilliseconds(duration / 2.0));
                view.LinkedTint.A.ShouldBeInRange((byte)1, (byte)(full - 1));
                clock.Advance(TimeSpan.FromMilliseconds(duration / 2.0 + 34));
                view.LinkedTint.A.ShouldBe(full);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Moving_The_Link_Fades_The_Old_Line_Out_While_The_New_One_Fades_In()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (view, window) = Show(InvoiceView(), ArcMotionLevel.Full);
        try
        {
            var full = (byte)Math.Round(ArcTokens.GetColor(view, ArcDesignTokenKind.Series1).A * 0.13);
            view.LinkedPath = "vendor";
            clock.Advance(TimeSpan.FromMilliseconds(200));
            view.LinkedPath = "iban";
            clock.Advance(TimeSpan.FromMilliseconds(50));
            view.FadingTint.A.ShouldBeInRange((byte)1, (byte)(full - 1));
            view.LinkedTint.A.ShouldBeInRange((byte)1, (byte)(full - 1));
            clock.Advance(TimeSpan.FromMilliseconds(150));
            view.FadingTint.A.ShouldBe((byte)0);
            view.LinkedTint.A.ShouldBe(full);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hidden_View_Writes_The_Text_At_Once_And_Cuts_Lines_When_Shown()
    {
        var store = PlayedInvoice();
        var (view, window) = Show(new ExtractionJsonView { Output = store.BuildOutput() });
        try
        {
            var lines = view.Drawing!;
            view.IsVisible = false;
            Settle();
            store.EditByUser("vat", "466.25");
            view.Output = store.BuildOutput();
            Settle();
            view.Text.ShouldContain("\"vat\": 466.25,");
            lines.Lines.ShouldContain(l => l.Text.Contains("466.20"));
            view.IsVisible = true;
            Settle();
            lines.Lines.ShouldContain(l => l.Text.Contains("466.25"));
            lines.LastBuiltCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Changed_Line_Flashes_Success_20_Percent_Back_To_Rest_Over_1400_Ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (view, window) = Show(InvoiceView(), ArcMotionLevel.Full);
        try
        {
            var success = ArcTokens.GetColor(view, ArcDesignTokenKind.Success);
            var start = (byte)Math.Round(success.A * 0.2);
            view.Flash("vat");
            view.ChangedPath.ShouldBe("vat");
            view.FlashTint.A.ShouldBe(start);
            view.FlashTint.R.ShouldBe(success.R);
            clock.Advance(TimeSpan.FromMilliseconds(700));
            view.FlashTint.A.ShouldBeInRange((byte)1, (byte)(start - 1));
            clock.Advance(TimeSpan.FromMilliseconds(734));
            view.FlashTint.A.ShouldBe((byte)0);

            // The same path again flashes again.
            view.Flash("vat");
            view.FlashTint.A.ShouldBe(start);
            // Setting a new changed path flashes that line.
            view.ChangedPath = "total";
            view.FlashTint.A.ShouldBe(start);
            view.LineIndexOf("total").ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Do_Not_Flash(ArcMotionLevel level)
    {
        var (view, window) = Show(InvoiceView(), level);
        try
        {
            view.Flash("vat");
            view.ChangedPath.ShouldBe("vat");
            view.FlashTint.A.ShouldBe((byte)0);
            view.IsFlashPending.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Flash_Asked_For_While_Hidden_Plays_When_The_View_Is_Shown()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (view, window) = Show(InvoiceView(), ArcMotionLevel.Full);
        try
        {
            view.IsVisible = false;
            Settle();
            view.Flash("subtotal");
            view.IsFlashPending.ShouldBeTrue();
            view.FlashTint.A.ShouldBe((byte)0);
            clock.Advance(TimeSpan.FromMilliseconds(2000));
            view.IsVisible = true;
            Settle();
            view.IsFlashPending.ShouldBeFalse();
            view.FlashTint.A.ShouldBeGreaterThan((byte)40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Lines_Whose_Text_Changed_Get_New_Layouts()
    {
        var store = PlayedInvoice();
        var (view, window) = Show(new ExtractionJsonView { Output = store.BuildOutput() });
        try
        {
            var lines = view.Drawing!;
            var distinct = view.Lines.Select(l => l.Text).Distinct().Count();
            lines.LastBuiltCount.ShouldBe(distinct);

            store.EditByUser("vat", "466.25");
            view.Output = store.BuildOutput();
            Settle();
            lines.LastBuiltCount.ShouldBe(1);
            lines.LayoutOf(view.LineIndexOf("vat"))!.ShouldNotBeNull();

            view.Output = store.BuildOutput();
            Settle();
            lines.LastBuiltCount.ShouldBe(0);

            // A new row: its four value lines are new; its braces and every other line are reused although they moved.
            store.ApplyModelValue(new ExtractionValueEvent("lineItems.4.description", "Glaze samples", null, 0.9));
            view.Output = store.BuildOutput();
            Settle();
            lines.LastBuiltCount.ShouldBe(4);
            view.LineIndexOf("iban").ShouldBe(view.Lines.Count - 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Over_A_Value_Line_Enters_And_Leaves_Its_Path()
    {
        var (view, window) = Show(InvoiceView());
        try
        {
            var log = new List<string>();
            view.PathEntered += (_, e) => log.Add("enter " + e.Path);
            view.PathExited += (_, e) => log.Add("exit " + e.Path);
            view.PathPressed += (_, e) => log.Add("press " + e.Path);
            window.MouseMove(LineCenter(view, window, "vendor"), RawInputModifiers.None);
            window.MouseMove(LineCenter(view, window, "lineItems.0.amount"), RawInputModifiers.None);
            var brace = LineCenter(view, window, "lineItems.0.description") - new Point(0, 20.4);
            window.MouseMove(brace, RawInputModifiers.None);
            view.HoveredPath.ShouldBeNull();
            var vat = LineCenter(view, window, "vat");
            window.MouseMove(vat, RawInputModifiers.None);
            window.MouseDown(vat, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(vat, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(new Point(590, 990), RawInputModifiers.None);
            log.ShouldBe([
                "enter vendor", "exit vendor", "enter lineItems.0.amount", "exit lineItems.0.amount", "enter vat", "press vat", "exit vat"
            ]);
            view.HoveredPath.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Changed_Output_Under_A_Still_Pointer_Moves_The_Hover()
    {
        var store = PlayedInvoice();
        var (view, window) = Show(new ExtractionJsonView { Output = store.BuildOutput() });
        try
        {
            var log = new List<string>();
            view.PathEntered += (_, e) => log.Add("enter " + e.Path);
            view.PathExited += (_, e) => log.Add("exit " + e.Path);
            window.MouseMove(LineCenter(view, window, "currency"), RawInputModifiers.None);
            // A fifth row pushes the currency down by six lines; the pointer now rests on the new row's lines.
            store.ApplyModelValue(new ExtractionValueEvent("lineItems.4.amount", "12.00", null, 0.9));
            view.Output = store.BuildOutput();
            Settle();
            log.ShouldBe(["enter currency", "exit currency", "enter lineItems.4.description"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Line_Bounds_Span_The_Visible_Box_At_The_Line()
    {
        var (view, window) = Show(InvoiceView());
        try
        {
            var origin = view.TranslatePoint(default, window)!.Value;
            view.TryGetLineBounds("vendor", window, out var bounds).ShouldBeTrue();
            bounds.X.ShouldBe(origin.X + 1, 0.01);
            bounds.Y.ShouldBe(origin.Y + 1 + 10 + 20.4, 0.01);
            bounds.Width.ShouldBe(434, 0.01);
            bounds.Height.ShouldBe(20.4, 0.01);
            view.TryGetLineBounds("nothing", window, out _).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Lines_Scroll_Sideways_With_Left_And_Right()
    {
        var (view, window) = Show(InvoiceView(), width: 200);
        try
        {
            var scroll = view.Scroll.ShouldNotBeNull();
            scroll.Extent.Width.ShouldBeGreaterThan(scroll.Viewport.Width);
            view.Focus(NavigationMethod.Tab);
            Settle();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Settle();
            scroll.Offset.X.ShouldBe(40);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Settle();
            scroll.Offset.X.ShouldBe(0);
            // The linked line's bounds stay on the visible box while it is scrolled.
            scroll.Offset = new Vector(60, 0);
            Settle();
            view.TryGetLineBounds("vendor", view, out var bounds).ShouldBeTrue();
            bounds.X.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Lines_In_The_Viewport_Are_Drawn()
    {
        var view = InvoiceView();
        view.Width = 436;
        var scroller = new ScrollViewer { Height = 200, Content = view };
        var window = TestHelpers.Show(scroller);
        try
        {
            Settle();
            var lines = view.Drawing!;
            // 200 high: about ten 20.4 lines of the 37.
            lines.LastDrawnCount.ShouldBeInRange(9, 11);
            scroller.Offset = new Vector(0, 400);
            Settle();
            lines.LastDrawnCount.ShouldBeInRange(9, 11);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Named_Document_Whose_Value_Is_The_Json()
    {
        var (view, window) = Show(InvoiceView());
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(view);
            peer.GetName().ShouldBe("Typed output as JSON");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Document);
            var value = peer.GetProvider<IValueProvider>().ShouldNotBeNull();
            value.Value.ShouldBe(view.Text);
            value.IsReadOnly.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
