using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.HeadersFooters;

public class SiteFooterTests
{
    private static (Window Window, SiteFooter Footer) Open(Action<SiteFooter>? configure = null, double width = 1200, ArcMotionLevel level = ArcMotionLevel.Off, double height = 900)
    {
        var footer = new SiteFooter();
        configure?.Invoke(footer);
        var window = TestHelpers.Show(footer, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        footer.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, footer);
    }

    private static IEnumerable<T> All<T>(Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static IEnumerable<SiteFooterLinkButton> Visible(Visual root) =>
        All<SiteFooterLinkButton>(root).Where(b => b.IsEffectivelyVisible);

    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Default_Shows_Arcs_Sample_Footer()
    {
        var (window, footer) = Open();
        try
        {
            All<SiteFooterColumnView>(footer).Select(c => c.Title).ShouldBe(["Product", "Resources", "Company"]);
            var labels = Visible(footer).Select(b => b.Label).ToList();
            labels.ShouldContain("Components");
            labels.ShouldContain("Figma kit");
            labels.ShouldContain("Privacy");
            labels.ShouldContain("LinkedIn");
            labels.ShouldContain("All systems normal");
            footer.Newsletter.ShouldNotBeNull();
            footer.Newsletter!.IsEffectivelyVisible.ShouldBeTrue();
            footer.Newsletter.Title.ShouldBe("Get the monthly release notes");
            All<SiteFooterLabel>(footer).Select(l => l.Text).ShouldContain($"© {DateTime.Now.Year} Arc");
            footer.Variant.ShouldBe(SiteFooterVariant.Columns);
            Visible(footer).Count(b => b.IsExternal).ShouldBe(4);
            AutomationProperties.GetLandmarkType(footer).ShouldBe(AutomationLandmarkType.ContentInfo);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Sit_One_To_Two_And_A_Quarter_Of_The_Width()
    {
        var (window, footer) = Open(width: 1280);
        try
        {
            var columns = All<SiteFooterColumnGrid>(footer).First();
            var intro = TestHelpers.Descendant<StackPanel>(footer, "PART_Intro");
            // (1280 - 48 padding - 48 gap) / 2.25 for the intro, the columns after the gap.
            intro.Bounds.Width.ShouldBe((1280 - 48 - 48) / 2.25, 1);
            columns.TranslatePoint(default, footer)!.Value.X.ShouldBe(24 + (1280 - 48 - 48) / 2.25 + 48, 1);
            var views = All<SiteFooterColumnView>(footer).ToList();
            views[1].Bounds.X.ShouldBeGreaterThan(views[0].Bounds.X);
            views[0].Bounds.Width.ShouldBe(views[1].Bounds.Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Variants_Switch_The_Visible_Layout()
    {
        var (window, footer) = Open();
        try
        {
            TestHelpers.Descendant<Grid>(footer, "PART_Top").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<StackPanel>(footer, "PART_Minimal").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<SiteFooterMark>(footer, "PART_Mark").IsVisible.ShouldBeFalse();

            footer.Variant = SiteFooterVariant.Minimal;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            TestHelpers.Descendant<Grid>(footer, "PART_Top").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<StackPanel>(footer, "PART_Minimal").IsVisible.ShouldBeTrue();
            footer.Classes.Contains(SiteFooter.MinimalPseudoClass).ShouldBeTrue();
            footer.Newsletter!.IsEffectivelyVisible.ShouldBeFalse();
            var minimalLinks = TestHelpers.Descendant<WrapPanel>(footer, "PART_MinimalLinks").Children.OfType<SiteFooterLinkButton>().Select(b => b.Label);
            minimalLinks.ShouldBe(["Components", "Documentation", "About", "Privacy", "Terms"]);
            All<SiteFooterLabel>(footer).Where(l => l.IsEffectivelyVisible).Select(l => l.Text)
                .ShouldContain($"© {DateTime.Now.Year} Arc. Interface components that move with intent.");

            footer.Variant = SiteFooterVariant.Logo;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            TestHelpers.Descendant<SiteFooterMark>(footer, "PART_Mark").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Grid>(footer, "PART_Top").IsVisible.ShouldBeTrue();
            footer.Newsletter!.IsEffectivelyVisible.ShouldBeFalse();
            footer.Padding.Bottom.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Data_Replaces_The_Sample()
    {
        var (window, footer) = Open(f =>
        {
            f.BrandName = "Northwind";
            f.Tagline = null;
            f.Year = 2030;
            f.IsNewsletterVisible = false;
            f.IsStatusVisible = false;
            f.Columns.Add(new SiteFooterColumn("Platform", new SiteFooterLink("Tracking"), new SiteFooterLink("API", "https://x.example", true)));
            f.LegalLinks.Add(new SiteFooterLink("Imprint"));
            f.SocialLinks.Add(new SiteFooterLink("Mastodon", isExternal: true));
        });
        try
        {
            All<SiteFooterColumnView>(footer).Select(c => c.Title).ShouldBe(["Platform"]);
            var labels = Visible(footer).Select(b => b.Label).ToList();
            labels.ShouldContain("Imprint");
            labels.ShouldContain("Mastodon");
            labels.ShouldNotContain("Privacy");
            labels.ShouldNotContain("All systems normal");
            labels.ShouldContain("Northwind");
            All<SiteFooterLabel>(footer).Select(l => l.Text).ShouldContain("© 2030 Northwind");
            All<SiteFooterLabel>(footer).Where(l => l.Kind == SiteFooterLabelKind.Tagline).ShouldAllBe(l => !l.IsEffectivelyVisible);
            footer.Newsletter!.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ColumnsSource_Wins_And_Is_Observed()
    {
        var source = new Avalonia.Collections.AvaloniaList<SiteFooterColumn> { new("One", new SiteFooterLink("A")) };
        var (window, footer) = Open(f => f.ColumnsSource = source);
        try
        {
            All<SiteFooterColumnView>(footer).Select(c => c.Title).ShouldBe(["One"]);
            source.Add(new SiteFooterColumn("Two", new SiteFooterLink("B")));
            Dispatcher.UIThread.RunJobs();
            All<SiteFooterColumnView>(footer).Select(c => c.Title).ShouldBe(["One", "Two"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Footers_Own_Width()
    {
        var (window, footer) = Open(width: 1200);
        try
        {
            footer.Classes.Contains(SiteFooter.StackedPseudoClass).ShouldBeFalse();
            window.Width = 700;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            footer.Classes.Contains(SiteFooter.StackedPseudoClass).ShouldBeTrue();
            footer.Classes.Contains(SiteFooter.NarrowPseudoClass).ShouldBeFalse();
            var intro = TestHelpers.Descendant<StackPanel>(footer, "PART_Intro");
            var columns = All<SiteFooterColumnGrid>(footer).First();
            columns.TranslatePoint(default, footer)!.Value.Y.ShouldBeGreaterThan(intro.TranslatePoint(default, footer)!.Value.Y + intro.Bounds.Height);
            columns.Columns.ShouldBe(3);

            window.Width = 500;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            footer.Classes.Contains(SiteFooter.NarrowPseudoClass).ShouldBeTrue();
            columns.Columns.ShouldBe(2);
            footer.Newsletter!.IsUnbounded.ShouldBeTrue();
            // Two rows of columns: the third column sits under the first.
            var views = All<SiteFooterColumnView>(footer).ToList();
            views[2].Bounds.X.ShouldBe(views[0].Bounds.X, 0.5);
            views[2].Bounds.Y.ShouldBeGreaterThan(views[0].Bounds.Bottom);
            footer.Newsletter.Bounds.Width.ShouldBeGreaterThan(400);

            window.Width = 1200;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            footer.Classes.Contains(SiteFooter.NarrowPseudoClass).ShouldBeFalse();
            footer.Newsletter.Bounds.Width.ShouldBe(400, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legal_Row_Splits_On_One_Line_Then_Stacks()
    {
        var (window, footer) = Open(width: 1200);
        try
        {
            var row = All<SiteFooterSplitRow>(footer).First();
            row.IsSingleLine.ShouldBeTrue();
            window.Width = 500;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            row.IsStacked.ShouldBeTrue();
            row.IsSingleLine.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_Links_Raises_Navigate_With_Kind_And_Runs_The_Command()
    {
        var pressed = new List<(string Label, SiteFooterNavigationKind Kind)>();
        object? commandArg = null;
        var (window, footer) = Open(f =>
        {
            f.Navigate += (_, e) =>
            {
                pressed.Add((e.Link.Label, e.Kind));
                e.Handled = true;
            };
            f.NavigateCommand = new RelayCommand(a => commandArg = a);
        });
        try
        {
            Press(footer, "Blocks");
            Press(footer, "Terms");
            Press(footer, "GitHub");
            Press(footer, "All systems normal");
            Press(footer, "Arc");
            pressed.ShouldBe(
            [
                ("Blocks", SiteFooterNavigationKind.Column),
                ("Terms", SiteFooterNavigationKind.Legal),
                ("GitHub", SiteFooterNavigationKind.Social),
                ("All systems normal", SiteFooterNavigationKind.Status),
                ("Arc", SiteFooterNavigationKind.Brand)
            ]);
            commandArg.ShouldBeOfType<SiteFooterNavigateEventArgs>().Link.Label.ShouldBe("Arc");
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(SiteFooter footer, string label)
    {
        var button = Visible(footer).First(b => b.Label == label);
        button.Focus();
        ((Window)TopLevel.GetTopLevel(footer)!).KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Link_Types_Are_Hyperlinks_With_An_Address_And_Buttons_Without()
    {
        var (window, footer) = Open(f => f.Columns.Add(new SiteFooterColumn("X", new SiteFooterLink("With", "https://a.example"), new SiteFooterLink("Without"))));
        try
        {
            var with = Visible(footer).First(b => b.Label == "With");
            var without = Visible(footer).First(b => b.Label == "Without");
            ControlAutomationPeer.CreatePeerForElement(with).GetAutomationControlType()
                .ShouldBe(AutomationControlType.Hyperlink);
            ControlAutomationPeer.CreatePeerForElement(without).GetAutomationControlType()
                .ShouldBe(AutomationControlType.Button);
            AutomationProperties.GetName(with).ShouldBe("With");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_Tone_And_Visibility()
    {
        var (window, footer) = Open(f => { f.StatusTone = SiteStatusTone.Danger; f.StatusLabel = "Outage"; f.StatusHref = "https://s.example"; });
        try
        {
            var status = Visible(footer).First(b => b.Kind == SiteFooterNavigationKind.Status);
            status.Tone.ShouldBe(SiteStatusTone.Danger);
            status.Label.ShouldBe("Outage");
            status.Link!.Href.ShouldBe("https://s.example");
            footer.IsStatusVisible = false;
            Dispatcher.UIThread.RunJobs();
            Visible(footer).Any(b => b.Kind == SiteFooterNavigationKind.Status).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // --- Newsletter -------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Newsletter_Starts_With_The_Hint_And_An_Idle_Button()
    {
        var (window, footer) = Open();
        try
        {
            var news = footer.Newsletter!;
            news.State.ShouldBe(SiteFooterNewsletterState.Idle);
            news.Message.ShouldBe(new SiteFooterMessage("One email a month. Unsubscribe anytime.", SiteFooterMessageTone.Hint));
            news.SubmitButton!.Content.ShouldBe("Subscribe");
            news.Input!.PlaceholderText.ShouldBe("you@example.com");
            AutomationProperties.GetName(news.Input).ShouldBe("Email address");
            news.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Empty_And_Invalid_Addresses_Show_Errors_Without_Sending()
    {
        var calls = 0;
        var (window, footer) = Open(f => f.SubscribeAsync = (_, _) => { calls++; return Task.CompletedTask; });
        try
        {
            var news = footer.Newsletter!;
            await news.SubmitAsync();
            news.Error.ShouldBe("Enter your email address");
            news.Classes.Contains(SiteFooterNewsletter.InvalidPseudoClass).ShouldBeTrue();
            news.Message.ShouldBe(new SiteFooterMessage("Enter your email address", SiteFooterMessageTone.Error));
            news.Announcer!.IsAssertive.ShouldBeTrue();
            news.Announcer.Announcement.ShouldBe("Enter your email address");

            news.Email = "abc";
            news.Error.ShouldBe("That email doesn't look right");
            news.Email = "ada@example.com";
            news.Error.ShouldBeNull();
            news.Message!.Tone.ShouldBe(SiteFooterMessageTone.Hint);
            calls.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Field_With_Text_Starts_Validating()
    {
        var (window, footer) = Open();
        try
        {
            var news = footer.Newsletter!;
            news.Input!.Focus();
            news.Email = "abc";
            news.Error.ShouldBeNull();
            // Focus leaves the field.
            footer.Focusable = true;
            Visible(footer).First().Focus();
            Dispatcher.UIThread.RunJobs();
            news.Error.ShouldBe("That email doesn't look right");
            news.Email = "";
            news.Error.ShouldBe("Enter your email address");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Valid_Address_Loads_Then_Subscribes()
    {
        var gate = new TaskCompletionSource();
        string? sent = null;
        SiteFooterSubscribedEventArgs? raised = null;
        var (window, footer) = Open(f =>
        {
            f.SubscribeAsync = async (email, _) => { sent = email; await gate.Task; };
            f.Subscribed += (_, e) => raised = e;
        });
        try
        {
            var news = footer.Newsletter!;
            news.Email = "  ada.lin@example.com ";
            var task = news.SubmitAsync();
            news.State.ShouldBe(SiteFooterNewsletterState.Loading);
            news.SubmitButton!.IsLoading.ShouldBeTrue();
            news.Input!.IsReadOnly.ShouldBeTrue();
            sent.ShouldBe("ada.lin@example.com");

            // A second press while loading is ignored.
            await news.SubmitAsync();

            gate.SetResult();
            await task;
            Dispatcher.UIThread.RunJobs();
            news.State.ShouldBe(SiteFooterNewsletterState.Done);
            news.SubmitButton.IsLoading.ShouldBeFalse();
            news.Message.ShouldBe(new SiteFooterMessage("Check ada.lin@example.com to confirm", SiteFooterMessageTone.Success));
            news.Input.IsDone.ShouldBeTrue();
            news.Announcer!.IsAssertive.ShouldBeFalse();
            news.Announcer.Announcement.ShouldBe("Check ada.lin@example.com to confirm");
            raised.ShouldNotBeNull();
            raised!.Email.ShouldBe("ada.lin@example.com");
            var content = news.SubmitButton.Content.ShouldBeOfType<StackPanel>();
            content.Children.OfType<TextBlock>().Single().Text.ShouldBe("Subscribed");
            content.Children.OfType<ArcGlyph>().Single().Size.ShouldBe(15);

            // Done cannot be submitted again.
            sent = null;
            await news.SubmitAsync();
            sent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failing_Send_Returns_To_Idle_With_The_Failure_Message()
    {
        var (window, footer) = Open(f => f.SubscribeAsync = (_, _) => throw new InvalidOperationException("down"));
        try
        {
            var news = footer.Newsletter!;
            news.Email = "ada@example.com";
            await news.SubmitAsync();
            news.State.ShouldBe(SiteFooterNewsletterState.Idle);
            news.Error.ShouldBe("We couldn't subscribe you. Try again in a moment");
            news.Input!.IsReadOnly.ShouldBeFalse();
            news.SubmitButton!.IsLoading.ShouldBeFalse();
            news.Announcer!.IsAssertive.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Enter_In_The_Field_Submits_And_Texts_Are_Properties()
    {
        var sent = new List<string>();
        var (window, footer) = Open(f =>
        {
            f.SubscribeAsync = (email, _) => { sent.Add(email); return Task.CompletedTask; };
            f.NewsletterSubscribedLabel = "Done";
            f.NewsletterConfirmFormat = "See {0}";
        });
        try
        {
            var news = footer.Newsletter!;
            news.Input!.Focus();
            news.Email = "ada@example.com";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await Settle(() => news.State == SiteFooterNewsletterState.Done);
            sent.ShouldBe(["ada@example.com"]);
            news.Message!.Text.ShouldBe("See ada@example.com");
            news.SubmitButton!.Content.ShouldBeOfType<StackPanel>().Children.OfType<TextBlock>().Single().Text.ShouldBe("Done");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Default_Send_Takes_900_Milliseconds_And_Succeeds()
    {
        var (window, footer) = Open();
        try
        {
            var news = footer.Newsletter!;
            news.Email = "ada@example.com";
            var task = news.SubmitAsync();
            news.State.ShouldBe(SiteFooterNewsletterState.Loading);
            await Task.Delay(300);
            news.State.ShouldBe(SiteFooterNewsletterState.Loading);
            await task;
            news.State.ShouldBe(SiteFooterNewsletterState.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Messages_Swap_In_Place_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var scope = clock.Install();
        var (window, footer) = Open(level: ArcMotionLevel.Full);
        try
        {
            var news = footer.Newsletter!;
            var swap = TestHelpers.Descendant<SiteSwap>(news, "PART_Message");
            swap.EnterOffset.ShouldBe(4);
            swap.ExitOffset.ShouldBe(-4);
            swap.ExitDuration.ShouldBe(TimeSpan.FromMilliseconds(120));
            swap.Content.ShouldBe(new SiteFooterMessage("One email a month. Unsubscribe anytime.", SiteFooterMessageTone.Hint));
            news.Email = "x";
            _ = news.SubmitAsync();
            Dispatcher.UIThread.RunJobs();
            swap.ExitingCount.ShouldBe(1);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            // The old copy is removed from a continuation that posts to the UI thread.
            await Settle(() => swap.ExitingCount == 0);
            swap.ExitingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    // --- Logo mark --------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Mark_Rises_In_Once_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var scope = clock.Install();
        // A short window keeps the arch below the fold, so it waits for the viewport.
        var (window, footer) = Open(f => f.Variant = SiteFooterVariant.Logo, level: ArcMotionLevel.Full, height: 300);
        try
        {
            var mark = TestHelpers.Descendant<SiteFooterMark>(footer, "PART_Mark");
            mark.IsRevealed.ShouldBeFalse();
            mark.Opacity.ShouldBe(0);
            mark.Reveal();
            mark.IsRevealed.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(450));
            mark.Opacity.ShouldBeInRange(0.3, 0.999);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            mark.Opacity.ShouldBe(1, 0.001);
            mark.RenderTransform.ShouldBeOfType<TranslateTransform>().Y.ShouldBe(0, 0.01);
            mark.OpacityMask.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void The_Mark_Shows_At_Once_Without_Full_Motion(ArcMotionLevel level)
    {
        var (window, footer) = Open(f => f.Variant = SiteFooterVariant.Logo, level: level);
        try
        {
            var mark = TestHelpers.Descendant<SiteFooterMark>(footer, "PART_Mark");
            mark.IsRevealed.ShouldBeTrue();
            mark.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Mark_Keeps_The_64_By_30_Ratio_And_Reaches_The_Edges()
    {
        var (window, footer) = Open(f => f.Variant = SiteFooterVariant.Logo, width: 1280);
        try
        {
            var mark = TestHelpers.Descendant<SiteFooterMark>(footer, "PART_Mark");
            mark.Bounds.Width.ShouldBe(1280, 0.5);
            mark.Bounds.Height.ShouldBe(1280 * 30.0 / 64, 0.5);
            mark.TranslatePoint(default, footer)!.Value.X.ShouldBe(0, 0.5);
            mark.IsHitTestVisible.ShouldBeFalse();
            AutomationProperties.GetAccessibilityView(mark).ShouldBe(AccessibilityView.Raw);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Footer_Is_Collected_While_Its_Data_Lives_On()
    {
        var source = new System.Collections.ObjectModel.ObservableCollection<SiteFooterColumn> { new("Docs", new SiteFooterLink("Guides")) };
        var links = new Avalonia.Collections.AvaloniaList<SiteFooterLink> { new("Home") };
        var withData = FooterOver(f =>
        {
            f.ColumnsSource = source;
            f.Links = links;
        });
        // The sample columns are static: they must not keep a default footer either.
        var withSamples = FooterOver(null);
        for (var i = 0; i < 4 && (withData.IsAlive || withSamples.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Dispatcher.UIThread.RunJobs();
        }
        withData.IsAlive.ShouldBeFalse();
        withSamples.IsAlive.ShouldBeFalse();
        source[0].Links.Add(new SiteFooterLink("Later"));
        source.Add(new SiteFooterColumn("More"));
        links.Add(new SiteFooterLink("Later"));
    }

    [AvaloniaFact]
    public void A_Footer_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var source = new System.Collections.ObjectModel.ObservableCollection<SiteFooterColumn> { new("Docs", new SiteFooterLink("Guides")) };
        var footer = new SiteFooter { ColumnsSource = source };
        var host = new Border { Child = footer };
        var window = TestHelpers.Show(host);
        window.Width = 1200;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        try
        {
            All<SiteFooterColumnView>(footer).Select(c => c.Title).ShouldBe(["Docs"]);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            source.Add(new SiteFooterColumn("Company", new SiteFooterLink("About")));
            source[0].Links.Add(new SiteFooterLink("Recipes"));
            host.Child = footer;
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            All<SiteFooterColumnView>(footer).Select(c => c.Title).ShouldBe(["Docs", "Company"]);
            Visible(footer).Select(b => b.Label).ShouldContain("Recipes");
            // Back in the window, it listens again.
            source[1].Links.Add(new SiteFooterLink("Careers"));
            Dispatcher.UIThread.RunJobs();
            footer.UpdateLayout();
            Visible(footer).Select(b => b.Label).ShouldContain("Careers");
        }
        finally
        {
            window.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference FooterOver(Action<SiteFooter>? configure)
    {
        var (window, footer) = Open(configure);
        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return new WeakReference(footer);
    }

    private sealed class RelayCommand(Action<object?> run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run(parameter);
    }
}
