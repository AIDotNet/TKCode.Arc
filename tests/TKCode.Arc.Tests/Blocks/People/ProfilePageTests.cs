using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>
/// <see cref="ProfilePage"/> on the invented Gallery sample (Rowan Ellery, three stats, seven activity rows, four projects):
/// rendering, the website rule, tabs and their keyboard, stars, Share, the edit sheet (focus, validation, Enter, Escape,
/// save, failed save, glow), breakpoints, the block height and the motion levels.
/// </summary>
public class ProfilePageTests
{
    private sealed class Rig : IDisposable
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 1200, double? height = null,
            Action<ProfilePage>? configure = null)
        {
            Page = new ProfilePage { SimulatedSaveDelay = TimeSpan.Zero };
            ProfilePageDemoData.Fill(Page, withPortraits: false);
            configure?.Invoke(Page);
            Page.TabChanged += (_, e) => TabEvents.Add(e);
            Page.StarChanged += (_, e) => StarEvents.Add(e);
            Page.Saved += (_, e) => SavedEvents.Add(e);
            Page.SaveFailed += (_, e) => FailedEvents.Add(e);
            Page.Shared += (_, e) => SharedEvents.Add(e);
            Host = new Border { Width = width, Child = Page, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            if (height is { } h)
            {
                Host.Height = h;
            }
            Window = TestHelpers.Show(Host, level);
            Window.Width = width + 40;
            Window.Height = 1000;
            Pump();
        }

        public ProfilePage Page { get; }
        public Border Host { get; }
        public Window Window { get; }
        public List<ProfilePageTabChangedEventArgs> TabEvents { get; } = new();
        public List<ProfileStarChangedEventArgs> StarEvents { get; } = new();
        public List<ProfileSavedEventArgs> SavedEvents { get; } = new();
        public List<ProfileSaveFailedEventArgs> FailedEvents { get; } = new();
        public List<ProfileSharedEventArgs> SharedEvents { get; } = new();
        public ProfilePageSheet Sheet => Page.Sheet!;

        public void Pump()
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }

        public void Advance(ManualFrameClock clock, TimeSpan time)
        {
            clock.Advance(time);
            Pump();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Pump();
        }

        public void Click(Control control)
        {
            control.Focus(NavigationMethod.Tab);
            Key(PhysicalKey.Enter);
        }

        public void OpenSheet()
        {
            Page.OpenEditor();
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    // Rules ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("x.1")]
    [InlineData("1.2.3.4")]
    [InlineData("X.IO")]
    [InlineData("www.x.io")]
    [InlineData("x_y.io")]
    [InlineData("-x.io")]
    [InlineData("x.io/")]
    [InlineData("http://x.io")]
    [InlineData("https://x.io/p?q=1")]
    [InlineData("  rowanellery.studio  ")]
    public void Website_Rule_Accepts(string value) => ProfilePageValidation.IsWebsiteValid(value).ShouldBeTrue();

    [Theory]
    [InlineData("abc")]
    [InlineData("abc.")]
    [InlineData("x..io")]
    [InlineData("http://x")]
    [InlineData("HTTPS://x.io")]
    [InlineData("ftp://x.io")]
    [InlineData("mailto:a@x.io")]
    [InlineData("localhost:3000")]
    [InlineData("x.io:8080")]
    [InlineData("x.io#a")]
    [InlineData("x.io?q")]
    [InlineData("ü.io")]
    [InlineData("@x.io")]
    [InlineData("x .io")]
    [InlineData("x.io/a b")]
    [InlineData("   ")]
    public void Website_Rule_Rejects(string value) => ProfilePageValidation.IsWebsiteValid(value).ShouldBeFalse();

    [Fact]
    public void Name_Rule_And_Changed_Fields()
    {
        ProfilePageValidation.IsNameValid("  ").ShouldBeFalse();
        ProfilePageValidation.IsNameValid(" a ").ShouldBeTrue();
        var before = ProfilePageDemoData.Profile(false);
        var after = before.Clone();
        after.Name = before.Name + "  ";
        after.Location = "";
        after.Bio = "New bio";
        ProfilePageValidation.Changed(before, after).ShouldBe(ProfileFields.Location | ProfileFields.Bio);
    }

    // Rendering --------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Shows_The_Profile_Stats_And_Counts()
    {
        using var rig = new Rig();
        rig.Page.NameText!.Text.ShouldBe("Rowan Ellery");
        rig.Page.HeadlineText!.Text.ShouldBe("Product designer at Fieldnote");
        rig.Page.LocationText!.Text.ShouldBe("Bergen, Norway");
        rig.Page.WebsiteButton!.Content.ShouldBe("rowanellery.studio");
        TestHelpers.Descendant<TextBlock>(rig.Page, ProfilePage.JoinedPart).Text.ShouldBe("Joined June 2020");
        TestHelpers.Descendant<TextBlock>(rig.Page, ProfilePage.HandlePart).Text.ShouldBe("@rowan");
        rig.Page.StatsPanel!.Children.Count.ShouldBe(3);
        var first = (StackPanel)rig.Page.StatsPanel.Children[0];
        ((TextBlock)first.Children[0]).Text.ShouldBe("3,107");
        rig.Page.ActivityTabButton.Count.ShouldBe("7");
        rig.Page.ProjectsTabButton.Count.ShouldBe("4");
        rig.Page.ActivityView.RowCount.ShouldBe(7);
        rig.Page.ActivityView.Children.Count.ShouldBe(2);
        rig.Page.AvatarView!.DisplayName.ShouldBe("Rowan Ellery");
        rig.Page.Cover!.Bounds.Height.ShouldBe(240);
        rig.Page.EditButton!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Empty_Location_And_Website_Hide_Their_Lines()
    {
        using var rig = new Rig();
        rig.Page.Profile!.Location = "";
        rig.Page.Profile.Website = " ";
        rig.Pump();
        rig.Page.GlowOf(ProfileFields.Location)!.IsVisible.ShouldBeFalse();
        rig.Page.GlowOf(ProfileFields.Website)!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Sources_Win_And_Are_Followed()
    {
        var rows = new ObservableCollection<ProfileActivity>(ProfilePageDemoData.Activity().Take(2));
        using var rig = new Rig(configure: p => p.ActivitySource = rows);
        rig.Page.ActivityTabButton.Count.ShouldBe("2");
        rows.Add(new ProfileActivity(ProfileActivityKind.Created, "created", "Moodboard", "Now", "Today"));
        rig.Pump();
        rig.Page.ActivityTabButton.Count.ShouldBe("3");
        rig.Page.ActivityView.Children.Count.ShouldBe(2);
    }

    // Tabs -------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Tab_Switches_The_Panel_And_Reports_It()
    {
        using var rig = new Rig();
        rig.Page.PanelSwap!.Current.ShouldBe(rig.Page.ActivityView);
        rig.Click(rig.Page.ProjectsTabButton);
        rig.Page.Tab.ShouldBe(ProfilePageTab.Projects);
        rig.Page.PanelSwap.Current.ShouldBe(rig.Page.ProjectsView);
        rig.Page.ProjectsTabButton.IsSelected.ShouldBeTrue();
        rig.Page.ActivityTabButton.IsSelected.ShouldBeFalse();
        rig.TabEvents.Count.ShouldBe(1);
        rig.TabEvents[0].OldTab.ShouldBe(ProfilePageTab.Activity);
        rig.TabEvents[0].NewTab.ShouldBe(ProfilePageTab.Projects);
        // Only the selected tab is a tab stop.
        KeyboardNavigation.GetIsTabStop(rig.Page.ActivityTabButton).ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(rig.Page.ProjectsTabButton).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Arrow_Keys_Wrap_And_The_Selection_Follows_The_Focus()
    {
        using var rig = new Rig();
        rig.Page.ActivityTabButton.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.ArrowRight);
        rig.Page.Tab.ShouldBe(ProfilePageTab.Projects);
        rig.Page.ProjectsTabButton.IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowRight);
        rig.Page.Tab.ShouldBe(ProfilePageTab.Activity);
        rig.Page.ActivityTabButton.IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowLeft);
        rig.Page.Tab.ShouldBe(ProfilePageTab.Projects);
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.Home);
        rig.Page.Tab.ShouldBe(ProfilePageTab.Projects);
    }

    [AvaloniaFact]
    public void The_Tab_Can_Be_Controlled_From_Outside()
    {
        using var rig = new Rig(configure: p => p.Tab = ProfilePageTab.Projects);
        rig.Page.PanelSwap!.Current.ShouldBe(rig.Page.ProjectsView);
        rig.Page.Tab = ProfilePageTab.Activity;
        rig.Pump();
        rig.Page.PanelSwap.Current.ShouldBe(rig.Page.ActivityView);
        rig.TabEvents.Last().NewTab.ShouldBe(ProfilePageTab.Activity);
    }

    [AvaloniaFact]
    public void The_Underline_Sits_Under_The_Selected_Tab()
    {
        using var rig = new Rig();
        var strip = rig.Page.TabStrip!;
        var underline = strip.Underline.CurrentRect;
        underline.X.ShouldBe(rig.Page.ActivityTabButton.Bounds.X, 0.5);
        underline.Width.ShouldBe(rig.Page.ActivityTabButton.Bounds.Width, 0.5);
        underline.Height.ShouldBe(2);
        underline.Bottom.ShouldBe(strip.Bounds.Height, 0.5);
        rig.Click(rig.Page.ProjectsTabButton);
        strip.Underline.CurrentRect.X.ShouldBe(rig.Page.ProjectsTabButton.Bounds.X, 0.5);
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_Out_Then_In_And_Glides_The_Underline()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var startX = rig.Page.TabStrip!.Underline.CurrentRect.X;
        rig.Page.Tab = ProfilePageTab.Projects;
        rig.Pump();
        rig.Advance(clock, TimeSpan.FromMilliseconds(60));
        // The old content is still there, fading out; the underline is on its way.
        rig.Page.PanelSwap!.Displayed.ShouldBe(rig.Page.ActivityView);
        rig.Page.ActivityView.Opacity.ShouldBeInRange(0.05, 0.95);
        var x = rig.Page.TabStrip.Underline.CurrentRect.X;
        x.ShouldBeGreaterThan(startX);
        x.ShouldBeLessThan(rig.Page.ProjectsTabButton.Bounds.X);
        rig.Advance(clock, TimeSpan.FromMilliseconds(90));
        rig.Page.PanelSwap.Displayed.ShouldBe(rig.Page.ProjectsView);
        rig.Advance(clock, TimeSpan.FromMilliseconds(60));
        rig.Page.ProjectsView.Opacity.ShouldBeInRange(0.05, 0.99);
        ((TranslateTransform)rig.Page.ProjectsView.RenderTransform!).Y.ShouldBeInRange(0.1, 6);
        rig.Advance(clock, TimeSpan.FromMilliseconds(900));
        rig.Page.ProjectsView.Opacity.ShouldBe(1);
        rig.Page.TabStrip.Underline.CurrentRect.X.ShouldBe(rig.Page.ProjectsTabButton.Bounds.X, 0.5);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Swaps_The_Panel_At_Once()
    {
        using var rig = new Rig(ArcMotionLevel.Reduced);
        rig.Page.Tab = ProfilePageTab.Projects;
        rig.Pump();
        rig.Page.PanelSwap!.Displayed.ShouldBe(rig.Page.ProjectsView);
        rig.Page.ProjectsView.Opacity.ShouldBe(1);
        rig.Page.TabStrip!.Underline.CurrentRect.X.ShouldBe(rig.Page.ProjectsTabButton.Bounds.X, 0.5);
    }

    // Stars ------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Starring_Updates_The_Count_At_Once_And_Reports_It()
    {
        using var rig = new Rig(configure: p => p.Tab = ProfilePageTab.Projects);
        var project = rig.Page.Projects[1];
        var star = rig.Page.ProjectsView.StarButtons[1];
        star.CountText.ShouldBe("34");
        AutomationProperties.GetName(star).ShouldBe("Star Signup study, 34 stars");
        rig.Click(star);
        project.IsStarred.ShouldBeTrue();
        project.StarCount.ShouldBe(35);
        star.IsStarred.ShouldBeTrue();
        star.CountText.ShouldBe("35");
        AutomationProperties.GetName(star).ShouldBe("Unstar Signup study, 35 stars");
        rig.StarEvents.Single().Project.ShouldBe(project);
        rig.StarEvents[0].IsStarred.ShouldBeTrue();
        rig.StarEvents[0].StarCount.ShouldBe(35);
        rig.Click(star);
        project.IsStarred.ShouldBeFalse();
        project.StarCount.ShouldBe(34);
    }

    [AvaloniaFact]
    public void Only_Starring_Pops_The_Star()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full, configure: p => p.Tab = ProfilePageTab.Projects);
        var star = rig.Page.ProjectsView.StarButtons[1];
        rig.Page.ToggleStar(rig.Page.Projects[1], star);
        rig.Pump();
        star.GlyphScale.ShouldBeInRange(0.59, 0.75);
        rig.Advance(clock, TimeSpan.FromMilliseconds(80));
        star.GlyphScale.ShouldBeInRange(0.65, 0.99);
        rig.Advance(clock, TimeSpan.FromMilliseconds(600));
        star.GlyphScale.ShouldBe(1, 0.002);
        rig.Page.ToggleStar(rig.Page.Projects[1], star);
        rig.Pump();
        star.GlyphScale.ShouldBe(1, 0.002);
    }

    // Share ------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Share_Copies_The_Link_Then_Reads_Share_Again()
    {
        string? copied = null;
        using var rig = new Rig(configure: p =>
        {
            p.CopyHandler = text => { copied = text; return Task.FromResult(true); };
            p.ShareFeedbackDuration = TimeSpan.FromMilliseconds(60);
        });
        var width = rig.Page.ShareButton!.Bounds.Width;
        rig.Click(rig.Page.ShareButton);
        copied.ShouldBe(ProfilePageDemoData.ProfileUrl);
        rig.Page.ShareStack!.Shown.ShouldBe(1);
        AutomationProperties.GetName(rig.Page.ShareButton).ShouldBe("Copied");
        rig.SharedEvents.Single().Succeeded.ShouldBeTrue();
        rig.Page.Announcer!.Announcement.ShouldBe("Profile link copied: https://fieldnote.app/@rowan");
        rig.Pump();
        rig.Page.ShareButton.Bounds.Width.ShouldBe(width, 0.01);
        TestHelpers.PumpUntil(() => rig.Page.ShareStack.Shown == 0, 3000).ShouldBeTrue();
        AutomationProperties.GetName(rig.Page.ShareButton).ShouldBe("Share");
    }

    [AvaloniaFact]
    public void A_Refused_Or_Throwing_Copy_Shows_Failed()
    {
        using var rig = new Rig(configure: p => p.CopyHandler = _ => throw new InvalidOperationException("denied"));
        rig.Click(rig.Page.ShareButton!);
        rig.Page.ShareStack!.Shown.ShouldBe(2);
        rig.SharedEvents.Single().Succeeded.ShouldBeFalse();
        rig.Page.CopyHandler = _ => Task.FromResult(false);
        rig.Click(rig.Page.ShareButton!);
        rig.Page.ShareStack.Shown.ShouldBe(2);
        rig.Page.Announcer!.Announcement.ShouldBe("Could not copy the profile link");
    }

    [AvaloniaFact]
    public void Without_A_Url_Share_Uses_The_Handle()
    {
        using var rig = new Rig(configure: p => p.ProfileUrl = null);
        rig.Page.EffectiveProfileUrl.ShouldBe("https://example.com/@rowan");
    }

    // Sheet ------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Opening_Focuses_The_Name_And_Escape_Returns_To_Edit_Profile()
    {
        using var rig = new Rig();
        rig.Click(rig.Page.EditButton!);
        rig.Page.IsEditing.ShouldBeTrue();
        rig.Sheet.IsVisible.ShouldBeTrue();
        rig.Page.Classes.Contains(":editing").ShouldBeTrue();
        rig.Sheet.NameBox!.IsFocused.ShouldBeTrue();
        rig.Sheet.SaveButton!.IsEnabled.ShouldBeFalse();
        rig.Sheet.NameBox.Text.ShouldBe("Rowan Ellery");
        rig.Sheet.BioCounter!.Text.ShouldBe($"{200 - rig.Page.Profile!.Bio.Length} left");
        rig.Sheet.NameBox.Text = "Changed";
        rig.Key(PhysicalKey.Escape);
        rig.Page.IsEditing.ShouldBeFalse();
        rig.Sheet.IsVisible.ShouldBeFalse();
        rig.Page.EditButton!.IsFocused.ShouldBeTrue();
        rig.Page.Profile.Name.ShouldBe("Rowan Ellery");
        // The draft was dropped.
        rig.OpenSheet();
        rig.Sheet.NameBox.Text.ShouldBe("Rowan Ellery");
    }

    [AvaloniaFact]
    public void Tab_Cycles_Inside_The_Sheet()
    {
        using var rig = new Rig();
        rig.Click(rig.Page.EditButton!);
        KeyboardNavigation.GetTabNavigation(rig.Sheet).ShouldBe(KeyboardNavigationMode.Cycle);
        for (var i = 0; i < 12; i++)
        {
            rig.Key(PhysicalKey.Tab);
            var focused = TopLevel.GetTopLevel(rig.Page)!.FocusManager!.GetFocusedElement() as Visual;
            focused.ShouldNotBeNull();
            rig.Sheet.IsVisualAncestorOf(focused!).ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void Errors_Appear_On_The_First_Submit_Then_Follow_The_Typing()
    {
        using var rig = new Rig();
        rig.OpenSheet();
        rig.Sheet.NameBox!.Text = "   ";
        rig.Sheet.WebsiteBox!.Text = "not a site";
        rig.Pump();
        rig.Sheet.NameError!.IsVisible.ShouldBeFalse();
        rig.Sheet.WebsiteError!.IsVisible.ShouldBeFalse();
        rig.Sheet.SaveButton!.IsEnabled.ShouldBeTrue();
        rig.Sheet.SaveButton.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Sheet.NameError.IsVisible.ShouldBeTrue();
        rig.Sheet.WebsiteError.IsVisible.ShouldBeTrue();
        rig.Sheet.NameBox.Classes.Contains("invalid").ShouldBeTrue();
        rig.Sheet.SaveButton.IsFocused.ShouldBeTrue();
        rig.Page.IsEditing.ShouldBeTrue();
        rig.SavedEvents.ShouldBeEmpty();
        rig.Sheet.NameBox.Text = "Rowan";
        rig.Pump();
        rig.Sheet.NameError.IsVisible.ShouldBeFalse();
        rig.Sheet.NameBox.Text = "";
        rig.Pump();
        rig.Sheet.NameError.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Enter_In_A_One_Line_Field_Saves_But_Not_In_The_Bio()
    {
        using var rig = new Rig();
        rig.OpenSheet();
        rig.Sheet.BioBox!.Focus();
        rig.Sheet.BioBox.Text = "Short bio.";
        rig.Key(PhysicalKey.Enter);
        rig.Page.IsEditing.ShouldBeTrue();
        rig.Sheet.HeadlineBox!.Focus();
        rig.Key(PhysicalKey.Enter);
        TestHelpers.PumpUntil(() => rig.SavedEvents.Count == 1, 3000).ShouldBeTrue();
        rig.Page.IsEditing.ShouldBeFalse();
        rig.Page.Profile!.Bio.ShouldStartWith("Short bio.");
    }

    [AvaloniaFact]
    public void Saving_Writes_Trimmed_Values_Back_And_Glows_The_Changed_Fields()
    {
        ProfileDetails? received = null;
        var gate = new TaskCompletionSource();
        using var rig = new Rig(ArcMotionLevel.Reduced, configure: p => p.SaveHandler = d => { received = d; return gate.Task; });
        var original = rig.Page.Profile!;
        rig.OpenSheet();
        rig.Sheet.NameBox!.Text = "  Rowan Ellery-Vik  ";
        rig.Sheet.LocationBox!.Text = "Oslo, Norway";
        rig.Sheet.SaveButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Page.IsSaving.ShouldBeTrue();
        rig.Sheet.SaveStack!.Shown.ShouldBe(1);
        rig.Sheet.SaveButton.IsEnabled.ShouldBeFalse();
        rig.Sheet.NameBox.IsEnabled.ShouldBeTrue();
        received!.Name.ShouldBe("Rowan Ellery-Vik");
        gate.SetResult();
        TestHelpers.PumpUntil(() => rig.SavedEvents.Count == 1, 3000).ShouldBeTrue();
        rig.Page.IsSaving.ShouldBeFalse();
        rig.Page.IsEditing.ShouldBeFalse();
        rig.Page.Profile.ShouldNotBeSameAs(original);
        rig.Page.Profile!.Name.ShouldBe("Rowan Ellery-Vik");
        rig.Page.Profile.Location.ShouldBe("Oslo, Norway");
        rig.Page.Profile.Handle.ShouldBe("rowan");
        rig.SavedEvents[0].ChangedFields.ShouldBe(ProfileFields.Name | ProfileFields.Location);
        rig.SavedEvents[0].Previous.ShouldBeSameAs(original);
        rig.Page.NameText!.Text.ShouldBe("Rowan Ellery-Vik");
        rig.Page.GlowOf(ProfileFields.Name)!.IsGlowing.ShouldBeTrue();
        rig.Page.GlowOf(ProfileFields.Location)!.IsGlowing.ShouldBeTrue();
        rig.Page.GlowOf(ProfileFields.Bio)!.IsGlowing.ShouldBeFalse();
        rig.Page.EditButton!.IsFocused.ShouldBeTrue();
        rig.Page.Announcer!.Announcement.ShouldBe("Profile saved");
    }

    [AvaloniaFact]
    public void The_Glow_Holds_Then_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var glow = rig.Page.GlowOf(ProfileFields.Bio)!;
        glow.Flash();
        rig.Pump();
        glow.Strength.ShouldBe(1);
        rig.Advance(clock, TimeSpan.FromMilliseconds(450));
        glow.Strength.ShouldBe(1);
        rig.Advance(clock, TimeSpan.FromMilliseconds(400));
        glow.Strength.ShouldBeInRange(0.02, 0.6);
        rig.Advance(clock, TimeSpan.FromMilliseconds(900));
        glow.Strength.ShouldBe(0, 0.001);
    }

    [AvaloniaFact]
    public void A_Trailing_Space_Enables_Save_But_Changes_Nothing()
    {
        using var rig = new Rig();
        rig.OpenSheet();
        rig.Sheet.NameBox!.Text = "Rowan Ellery ";
        rig.Pump();
        rig.Sheet.SaveButton!.IsEnabled.ShouldBeTrue();
        rig.Sheet.NameBox.Text = "Rowan Ellery";
        rig.Pump();
        rig.Sheet.SaveButton.IsEnabled.ShouldBeFalse();
        rig.Sheet.NameBox.Text = "Rowan Ellery ";
        rig.Sheet.NameBox.Focus();
        rig.Key(PhysicalKey.Enter);
        TestHelpers.PumpUntil(() => rig.SavedEvents.Count == 1, 3000).ShouldBeTrue();
        rig.SavedEvents[0].ChangedFields.ShouldBe(ProfileFields.None);
        rig.Page.Profile!.Name.ShouldBe("Rowan Ellery");
    }

    [AvaloniaFact]
    public void A_Failed_Save_Keeps_The_Sheet_Open_With_The_Message()
    {
        using var rig = new Rig(configure: p => p.SaveHandler = _ => Task.FromException(new InvalidOperationException("The server did not answer.")));
        rig.OpenSheet();
        rig.Sheet.HeadlineBox!.Text = "Design lead at Fieldnote";
        rig.Sheet.HeadlineBox.Focus();
        rig.Key(PhysicalKey.Enter);
        TestHelpers.PumpUntil(() => rig.FailedEvents.Count == 1, 3000).ShouldBeTrue();
        rig.Page.IsEditing.ShouldBeTrue();
        rig.Page.IsSaving.ShouldBeFalse();
        rig.Sheet.HasAlert.ShouldBeTrue();
        rig.Sheet.AlertText!.Text.ShouldBe("The server did not answer.");
        rig.Sheet.SaveButton!.IsEnabled.ShouldBeTrue();
        rig.Sheet.SaveStack!.Shown.ShouldBe(0);
        rig.Page.Profile!.Headline.ShouldBe("Product designer at Fieldnote");
        rig.FailedEvents[0].Draft.Headline.ShouldBe("Design lead at Fieldnote");
        rig.Page.Announcer!.Announcement.ShouldBe("The server did not answer.");
        // The next submit clears the strip while it runs.
        rig.Page.SaveHandler = null;
        rig.Key(PhysicalKey.Enter);
        TestHelpers.PumpUntil(() => rig.SavedEvents.Count == 1, 3000).ShouldBeTrue();
        rig.Page.Profile!.Headline.ShouldBe("Design lead at Fieldnote");
    }

    [AvaloniaFact]
    public void The_Activity_Rows_Follow_The_New_First_Name()
    {
        using var rig = new Rig();
        rig.OpenSheet();
        rig.Sheet.NameBox!.Text = "Ro Ellery Vik";
        rig.Sheet.NameBox.Focus();
        rig.Key(PhysicalKey.Enter);
        TestHelpers.PumpUntil(() => rig.SavedEvents.Count == 1, 3000).ShouldBeTrue();
        var first = rig.Page.ActivityView.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Inlines is { Count: > 0 });
        ((Avalonia.Controls.Documents.Run)first.Inlines![0]).Text.ShouldBe("Ro shipped ");
    }

    [AvaloniaFact]
    public void The_Bio_Is_Capped()
    {
        using var rig = new Rig(configure: p => p.BioMaxLength = 20);
        rig.OpenSheet();
        rig.Sheet.BioBox!.MaxLength.ShouldBe(20);
        rig.Sheet.BioBox.Text = "12345";
        rig.Pump();
        rig.Sheet.BioCounter!.Text.ShouldBe("15 left");
    }

    [AvaloniaFact]
    public void A_Read_Only_Page_Offers_No_Editing()
    {
        using var rig = new Rig(configure: p => p.IsEditable = false);
        rig.Page.EditButton!.IsVisible.ShouldBeFalse();
        rig.Page.OpenEditor();
        rig.Pump();
        rig.Page.IsEditing.ShouldBeFalse();
        rig.Sheet.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Pressing_The_Scrim_Closes_The_Sheet()
    {
        using var rig = new Rig();
        rig.OpenSheet();
        rig.Page.Scrim!.IsHitTestVisible.ShouldBeTrue();
        rig.Window.MouseDown(new Point(60, 400), MouseButton.Left);
        rig.Window.MouseUp(new Point(60, 400), MouseButton.Left);
        rig.Pump();
        rig.Page.IsEditing.ShouldBeFalse();
        rig.Page.Scrim.IsHitTestVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Sheet_Slides_In_On_Smooth_And_Out_Faster()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.OpenSheet();
        rig.Page.SheetOffset.ShouldBeGreaterThan(300);
        rig.Advance(clock, TimeSpan.FromMilliseconds(150));
        var mid = rig.Page.SheetOffset;
        mid.ShouldBeInRange(20, 400);
        rig.Advance(clock, TimeSpan.FromMilliseconds(900));
        rig.Page.SheetOffset.ShouldBe(0, 1);
        rig.Page.CloseEditor();
        rig.Pump();
        rig.Advance(clock, TimeSpan.FromMilliseconds(150));
        // ω ≈ 39: most of the way out after 150ms.
        rig.Page.SheetOffset.ShouldBeGreaterThan(300);
        rig.Advance(clock, TimeSpan.FromMilliseconds(400));
        TestHelpers.PumpUntil(() => !rig.Sheet.IsVisible, 2000).ShouldBeTrue();
        rig.Page.IsSheetShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_The_Sheet_Instead_Of_Sliding_It()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Reduced);
        rig.OpenSheet();
        rig.Page.SheetOffset.ShouldBe(0);
        rig.Advance(clock, TimeSpan.FromMilliseconds(60));
        rig.Page.SheetOpacity.ShouldBeInRange(0.05, 0.95);
        rig.Advance(clock, TimeSpan.FromMilliseconds(200));
        rig.Page.SheetOpacity.ShouldBe(1);
    }

    // Layout -----------------------------------------------------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(1200, 240, false, false, 2)]
    [InlineData(900, 200, false, false, 2)]
    [InlineData(700, 200, true, false, 2)]
    [InlineData(600, 128, true, true, 2)]
    [InlineData(500, 128, true, true, 1)]
    public void Breakpoints_Follow_The_Block_Width(double width, double cover, bool stacked, bool compact, int columns)
    {
        using var rig = new Rig(width: width);
        rig.Page.Cover!.Bounds.Height.ShouldBe(cover);
        rig.Page.Body!.IsStacked.ShouldBe(stacked);
        rig.Page.Classes.Contains(":compact").ShouldBe(compact);
        rig.Page.AvatarView!.Bounds.Width.ShouldBe(compact ? 84 : 104);
        rig.Page.ProjectsView.Columns.ShouldBe(columns);
    }

    [AvaloniaFact]
    public void A_Narrow_Sheet_Spans_The_Page_And_Stacks_Its_Pair()
    {
        using var rig = new Rig(width: 390);
        rig.OpenSheet();
        rig.Sheet.Bounds.Width.ShouldBe(390, 0.5);
        rig.Sheet.Pair!.Columns.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Block_Is_760_High_Or_Less()
    {
        using (var rig = new Rig())
        {
            rig.Page.Bounds.Height.ShouldBe(760);
            rig.Page.Scroller!.Extent.Height.ShouldBeGreaterThan(760);
        }
        using (var rig = new Rig(height: 500))
        {
            rig.Page.Bounds.Height.ShouldBe(500);
        }
    }
}
