using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiConversationMenuButton"/>: the pill (34px, padding, chevron, title fallbacks, accessible name, crossfade and
/// width spring, truncation), opening by press and by keys, the menu's structure (New chat, separator, groups, headings,
/// check, derived titles), geometry (260 wide, left-aligned 6px below, 36px rows, 420 max), keyboard (wrapping arrows,
/// Home / End, typeahead, Enter, Escape, Tab), pointer (hover highlight, click), focus return, events and live data.
/// </summary>
public class AiConversationMenuButtonTests
{
    private sealed class Rig : IDisposable
    {
        private readonly IDisposable _clockInstall;

        public ManualFrameClock Clock { get; } = new();
        public AiConversationMenuButton Pill { get; }
        public AvaloniaList<AiChatConversation> Conversations { get; }
        public Window Window { get; }
        public Button Before { get; } = new() { Content = "Before" };
        public List<AiConversationChosenEventArgs> Chosen { get; } = new();
        public int NewChats { get; private set; }

        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double slot = 600, IEnumerable<AiChatConversation>? conversations = null)
        {
            _clockInstall = Clock.Install();
            Conversations = new AvaloniaList<AiChatConversation>(conversations ?? AiChatScrollSamples.Conversations());
            Pill = new AiConversationMenuButton
            {
                Conversations = Conversations,
                Current = Conversations.Count > 0 ? Conversations[0] : null,
                NewChatText = AiChatScrollSamples.NewChat,
                UntitledText = AiChatScrollSamples.Untitled,
                NameFormat = title => $"Chats, current: {title}"
            };
            Pill.ConversationChosen += (_, e) => Chosen.Add(e);
            Pill.NewChatRequested += (_, _) => NewChats++;
            var slotHost = new Border { Width = slot, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Child = Pill };
            var root = new StackPanel { Margin = new Thickness(20), Children = { Before, slotHost } };
            Window = TestHelpers.Show(root, level);
            Window.Width = 900;
            Window.Height = 700;
            Layout();
        }

        public AiConversationMenuPanel Panel => Pill.MenuPanel!;
        public IReadOnlyList<AiConversationMenuRow> Rows => Pill.Rows;

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                CoreFrames.Run(Clock, 1);
                Layout();
            }
        }

        public Point CenterOf(Visual visual) =>
            visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window) ?? default;

        public void Press(Visual visual)
        {
            var point = CenterOf(visual);
            Window.MouseMove(point, RawInputModifiers.None);
            Window.MouseDown(point, MouseButton.Left);
            Window.MouseUp(point, MouseButton.Left);
            Layout();
        }

        public void Hover(Visual visual)
        {
            Window.MouseMove(CenterOf(visual), RawInputModifiers.None);
            Layout();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Layout();
        }

        /// <summary>Tab from the button before the pill, so the pill has keyboard focus.</summary>
        public void TabToPill()
        {
            Before.Focus(NavigationMethod.Tab);
            Key(PhysicalKey.Tab);
            Assert.True(Pill.IsFocused);
        }

        public void Dispose()
        {
            Window.Close();
            _clockInstall.Dispose();
        }
    }

    private static string Texts(IEnumerable<AiConversationMenuRow> rows) => string.Join(" | ", rows.Select(r => r.Text));

    #region Pill

    [AvaloniaFact]
    public void The_Pill_Is_34px_With_The_Title_Then_A_14px_Chevron()
    {
        using var rig = new Rig();
        var pill = rig.Pill;
        Assert.Equal(34, pill.Bounds.Height);
        Assert.Equal("Speed up the nightly build", pill.CurrentText!.Text);
        var text = pill.CurrentText!;
        var chevron = TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");
        Assert.Equal(10, text.TranslatePoint(default, pill)!.Value.X, 1);
        Assert.Equal(14, chevron.Bounds.Width, 1);
        Assert.Equal(1.75, chevron.StrokeThickness);
        var chevronX = chevron.TranslatePoint(default, pill)!.Value.X;
        Assert.Equal(10 + text.Bounds.Width + 6, chevronX, 1);
        Assert.Equal(chevronX + 14 + 8, pill.Bounds.Width, 1);
        Assert.Equal(FontWeight.Medium, text.FontWeight);
        Assert.Equal(14, text.FontSize);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.Equal(ArcTokens.GetColor(pill, ArcDesignTokenKind.TextMuted), TestHelpers.ColorOf(chevron.Foreground));
    }

    [AvaloniaFact]
    public void The_Accessible_Name_Comes_From_NameFormat()
    {
        using var rig = new Rig();
        Assert.Equal("Chats, current: Speed up the nightly build", AutomationProperties.GetName(rig.Pill));
        rig.Pill.NameFormat = null;
        Assert.Equal("Speed up the nightly build", AutomationProperties.GetName(rig.Pill));
    }

    [AvaloniaFact]
    public void The_Title_Falls_Back_To_The_Current_Conversation_Then_New_Chat()
    {
        using var rig = new Rig();
        rig.Pill.Title = "Pinned title";
        Assert.Equal("Pinned title", rig.Pill.CurrentText!.Text);
        rig.Pill.Title = null;
        Assert.Equal("Speed up the nightly build", rig.Pill.CurrentText!.Text);
        rig.Pill.Current = AiChatScrollSamples.FromFirstMessage("Draft a short note to the team about the freeze");
        Assert.Equal("Draft a short note to the", rig.Pill.CurrentText!.Text);
        rig.Pill.Current = new AiChatConversation();
        Assert.Equal(AiChatScrollSamples.Untitled, rig.Pill.CurrentText!.Text);
        rig.Pill.Current = null;
        Assert.Equal(AiChatScrollSamples.NewChat, rig.Pill.CurrentText!.Text);
    }

    [AvaloniaFact]
    public void First_Words_Skip_Code_Marks_And_Extra_Space()
    {
        Assert.Equal("Add a region column to orders", AiConversationTitle.FirstWords("  Add a `region`\n column  to orders table now", 6));
        Assert.Equal("Two words", AiConversationTitle.FirstWords("Two words", 6));
        Assert.Equal(string.Empty, AiConversationTitle.FirstWords("   ", 6));
    }

    [AvaloniaFact]
    public void A_New_Title_Crossfades_And_The_Width_Springs()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        var before = rig.Pill.CurrentText;
        var start = rig.Pill.Bounds.Width;
        rig.Pill.Current = null;
        Assert.NotSame(before, rig.Pill.CurrentText);
        rig.Layout();
        var target = 10 + rig.Pill.CurrentText!.DesiredSize.Width + 6 + 14 + 8;
        Assert.True(target < start - 40);
        rig.Frames(3);
        var mid = rig.Pill.Bounds.Width;
        Assert.InRange(mid, target + 1, start - 1);
        Assert.True(rig.Pill.TextSize!.IsMoving);
        rig.Frames(40);
        Assert.Equal(target, rig.Pill.Bounds.Width, 1);
        Assert.False(rig.Pill.TextSize!.IsMoving);
    }

    [AvaloniaFact]
    public void Off_Changes_The_Width_At_Once()
    {
        using var rig = new Rig();
        rig.Pill.Current = null;
        rig.Layout();
        var target = 10 + rig.Pill.CurrentText!.DesiredSize.Width + 6 + 14 + 8;
        Assert.Equal(target, rig.Pill.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void Reduced_Crossfades_The_Title_But_Jumps_The_Width()
    {
        using var rig = new Rig(ArcMotionLevel.Reduced);
        var before = rig.Pill.CurrentText;
        rig.Pill.Current = null;
        rig.Layout();
        Assert.NotSame(before, rig.Pill.CurrentText);
        var target = 10 + rig.Pill.CurrentText!.DesiredSize.Width + 6 + 14 + 8;
        Assert.Equal(target, rig.Pill.Bounds.Width, 1);
        Assert.False(rig.Pill.TextSize!.IsMoving);
    }

    [AvaloniaFact]
    public void A_Long_Title_Truncates_Within_The_Room_Its_Slot_Leaves()
    {
        using var rig = new Rig(slot: 120);
        var pill = rig.Pill;
        Assert.True(pill.Bounds.Width <= 120.5);
        var text = pill.CurrentText!;
        // 120 − 10 − 6 − 14 − 8 = 82 for the title, which needs much more.
        Assert.True(text.Bounds.Width <= 82.5, text.Bounds.Width.ToString());
        Assert.True(text.TextLayout.TextLines.Count == 1);
        var chevron = TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");
        Assert.Equal(14, chevron.Bounds.Width, 1);
        Assert.True(chevron.TranslatePoint(default, pill)!.Value.X + 14 <= pill.Bounds.Width - 8 + 0.5);
    }

    [AvaloniaFact]
    public void Hover_And_The_Open_Menu_Tint_The_Pill_And_Turn_The_Chevron()
    {
        using var rig = new Rig();
        var pill = rig.Pill;
        Assert.Equal(Colors.Transparent, TestHelpers.ColorOf(pill.Background));
        rig.Hover(pill);
        Assert.Contains(":hover", pill.Classes);
        Assert.Equal(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(pill.Background));
        rig.Hover(rig.Before);
        Assert.Equal(Colors.Transparent, TestHelpers.ColorOf(pill.Background));
        rig.Press(pill);
        rig.Hover(rig.Before);
        Assert.Contains(":open", pill.Classes);
        Assert.Equal(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(pill.Background));
        var chevron = TestHelpers.Descendant<ArcGlyph>(pill, "PART_Chevron");
        var rotate = chevron.RenderTransform!.Value;
        Assert.Equal(-1, rotate.M11, 3);
    }

    #endregion

    #region Opening and structure

    [AvaloniaFact]
    public void A_Press_Opens_The_Menu_With_Nothing_Highlighted()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        Assert.True(rig.Pill.IsMenuOpen);
        Assert.True(rig.Pill.MenuPopup!.IsOpen);
        Assert.Null(rig.Pill.HighlightedRow);
        Assert.True(rig.Panel.IsFocused);
    }

    [AvaloniaFact]
    public void Enter_Space_And_Down_Open_With_The_First_Row_Highlighted()
    {
        foreach (var key in new[] { PhysicalKey.Enter, PhysicalKey.Space, PhysicalKey.ArrowDown })
        {
            using var rig = new Rig();
            rig.TabToPill();
            rig.Key(key);
            Assert.True(rig.Pill.IsMenuOpen, key.ToString());
            Assert.Same(rig.Rows[0], rig.Pill.HighlightedRow);
            Assert.True(rig.Rows[0].IsFocused);
            Assert.Contains(":highlighted", rig.Rows[0].Classes);
        }
    }

    [AvaloniaFact]
    public void The_Menu_Lists_New_Chat_Then_The_Groups_With_A_Check_On_The_Current()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        Assert.Equal("New chat | Speed up the nightly build | Rename the staging bucket | Write the 4.2 release notes | " +
                     "Summarize the support survey | Flaky login test", Texts(rig.Rows));
        Assert.Equal(AiConversationMenuRowKind.NewChat, rig.Rows[0].Kind);
        Assert.Same(ArcIcons.SquarePen, rig.Rows[0].Icon);
        Assert.Equal(new[] { false, true, false, false, false, false }, rig.Rows.Select(r => r.IsChecked).ToArray());
        var list = rig.Panel.List.Children;
        Assert.IsType<AiConversationMenuRow>(list[0]);
        Assert.IsType<AiConversationMenuSeparator>(list[1]);
        Assert.Equal("Today", Assert.IsType<AiConversationMenuHeading>(list[2]).Text);
        Assert.Equal("Previous 7 days", Assert.IsType<AiConversationMenuHeading>(list[6]).Text);
        Assert.Equal(9, list.Count);
    }

    [AvaloniaFact]
    public void Ungrouped_Conversations_Come_First_And_Titles_Are_Derived()
    {
        var conversations = new[]
        {
            new AiChatConversation { Title = "Grouped later", Group = "Earlier" },
            AiChatScrollSamples.FromFirstMessage("Why did the cache key change on Tuesday night?", group: null),
            new AiChatConversation(),
            new AiChatConversation { Title = "Second earlier", Group = "Earlier" }
        };
        using var rig = new Rig(conversations: conversations);
        rig.Press(rig.Pill);
        Assert.Equal("New chat | Why did the cache key change | Untitled chat | Grouped later | Second earlier", Texts(rig.Rows));
        Assert.Equal(1, rig.Panel.List.Children.OfType<AiConversationMenuHeading>().Count());
    }

    [AvaloniaFact]
    public void Without_Conversations_Only_New_Chat_Shows()
    {
        using var rig = new Rig(conversations: Array.Empty<AiChatConversation>());
        rig.Press(rig.Pill);
        Assert.Single(rig.Rows);
        Assert.Single(rig.Panel.List.Children);
    }

    [AvaloniaFact]
    public void The_Menu_Is_260_Wide_Left_Aligned_6px_Below_The_Pill_With_36px_Rows()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        var panel = rig.Panel;
        Assert.Equal(260, panel.Bounds.Width, 1);
        var pillOrigin = rig.Pill.TranslatePoint(default, rig.Window)!.Value;
        var panelOrigin = panel.TranslatePoint(default, rig.Window)!.Value;
        Assert.Equal(pillOrigin.X, panelOrigin.X, 1);
        Assert.Equal(pillOrigin.Y + 34 + 6, panelOrigin.Y, 1);
        var first = rig.Rows[0];
        Assert.Equal(36, first.Bounds.Height, 1);
        // 1px border + 6px padding.
        Assert.Equal(7, first.TranslatePoint(default, panel)!.Value.X, 1);
        Assert.Equal(7, first.TranslatePoint(default, panel)!.Value.Y, 1);
        Assert.Equal(260 - 14, first.Bounds.Width, 1);
        var separator = rig.Panel.List.Children.OfType<AiConversationMenuSeparator>().Single();
        Assert.Equal(1, separator.Bounds.Height, 1);
        Assert.Equal(7 + 4, separator.TranslatePoint(default, panel)!.Value.X, 1);
        Assert.Equal(7 + 36 + 6, separator.TranslatePoint(default, panel)!.Value.Y, 1);
        var heading = rig.Panel.List.Children.OfType<AiConversationMenuHeading>().First();
        Assert.True(Math.Abs(10 + 16.8 + 4 - heading.Bounds.Height) <= 0.5);
        // The check of the current row: 15px at the right, 10px in.
        var check = TestHelpers.Descendant<ArcGlyph>(rig.Rows[1], "PART_Check");
        Assert.True(check.IsVisible);
        Assert.Equal(15, check.Bounds.Width, 1);
        Assert.Equal(rig.Rows[1].Bounds.Width - 10 - 15, check.TranslatePoint(default, rig.Rows[1])!.Value.X, 1);
    }

    [AvaloniaFact]
    public void A_Long_List_Stops_At_420_And_Scrolls_To_The_Keyboard_Row()
    {
        var many = Enumerable.Range(1, 20).Select(i => new AiChatConversation { Title = $"Conversation {i}", Group = "Older" }).ToArray();
        using var rig = new Rig(conversations: many);
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(420, rig.Panel.Bounds.Height, 1);
        rig.Key(PhysicalKey.End);
        Assert.Same(rig.Rows[^1], rig.Pill.HighlightedRow);
        Assert.True(rig.Panel.Scroll!.Offset.Y > 0);
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void Arrows_Wrap_And_Home_End_Jump()
    {
        using var rig = new Rig();
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.ArrowUp);
        Assert.Same(rig.Rows[^1], rig.Pill.HighlightedRow);
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.Rows[0], rig.Pill.HighlightedRow);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.Rows[2], rig.Pill.HighlightedRow);
        Assert.True(rig.Rows[2].IsFocused);
        Assert.DoesNotContain(":highlighted", rig.Rows[0].Classes);
        rig.Key(PhysicalKey.End);
        Assert.Same(rig.Rows[^1], rig.Pill.HighlightedRow);
        rig.Key(PhysicalKey.Home);
        Assert.Same(rig.Rows[0], rig.Pill.HighlightedRow);
    }

    [AvaloniaFact]
    public void Down_From_A_Pointer_Open_Highlights_The_First_Row()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.Rows[0], rig.Pill.HighlightedRow);
    }

    [AvaloniaFact]
    public void Letters_Jump_To_The_Next_Row_Starting_With_Them()
    {
        using var rig = new Rig();
        var now = DateTime.UtcNow;
        rig.Pill.Now = () => now;
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.S);
        Assert.Equal("Speed up the nightly build", rig.Pill.HighlightedRow!.Text);
        now += TimeSpan.FromSeconds(1);
        rig.Key(PhysicalKey.S);
        Assert.Equal("Summarize the support survey", rig.Pill.HighlightedRow!.Text);
        now += TimeSpan.FromSeconds(1);
        rig.Key(PhysicalKey.S);
        Assert.Equal("Speed up the nightly build", rig.Pill.HighlightedRow!.Text);
        now += TimeSpan.FromSeconds(1);
        rig.Key(PhysicalKey.F);
        Assert.Equal("Flaky login test", rig.Pill.HighlightedRow!.Text);
        now += TimeSpan.FromSeconds(1);
        // Typed together, letters make one prefix.
        rig.Key(PhysicalKey.W);
        rig.Key(PhysicalKey.R);
        Assert.Equal("Write the 4.2 release notes", rig.Pill.HighlightedRow!.Text);
        now += TimeSpan.FromSeconds(1);
        rig.Key(PhysicalKey.Q);
        Assert.Equal("Write the 4.2 release notes", rig.Pill.HighlightedRow!.Text);
        Assert.True(rig.Pill.IsMenuOpen);
    }

    [AvaloniaFact]
    public void Enter_Chooses_The_Conversation_And_Focus_Returns_With_A_Ring()
    {
        using var rig = new Rig();
        var previous = rig.Pill.Current;
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        Assert.False(rig.Pill.IsMenuOpen);
        Assert.False(rig.Pill.MenuPopup!.IsOpen);
        Assert.Same(rig.Conversations[1], rig.Pill.Current);
        var chosen = Assert.Single(rig.Chosen);
        Assert.Same(rig.Conversations[1], chosen.Conversation);
        Assert.Same(previous, chosen.Previous);
        Assert.True(chosen.IsChange);
        Assert.True(rig.Pill.IsFocused);
        Assert.Contains(":focus-visible", rig.Pill.Classes);
        Assert.Equal("Rename the staging bucket", rig.Pill.CurrentText!.Text);
    }

    [AvaloniaFact]
    public void Space_On_New_Chat_Raises_NewChatRequested_And_Keeps_The_Current()
    {
        using var rig = new Rig();
        var current = rig.Pill.Current;
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Space);
        Assert.Equal(1, rig.NewChats);
        Assert.Empty(rig.Chosen);
        Assert.Same(current, rig.Pill.Current);
        Assert.False(rig.Pill.IsMenuOpen);
        Assert.True(rig.Pill.IsFocused);
    }

    [AvaloniaFact]
    public void Choosing_The_Current_Conversation_Reports_No_Change()
    {
        using var rig = new Rig();
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        Assert.False(Assert.Single(rig.Chosen).IsChange);
    }

    [AvaloniaFact]
    public void Escape_Closes_And_Gives_Focus_Back_To_The_Pill()
    {
        using var rig = new Rig();
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Escape);
        Assert.False(rig.Pill.IsMenuOpen);
        Assert.True(rig.Pill.IsFocused);
        Assert.Contains(":focus-visible", rig.Pill.Classes);
        Assert.Empty(rig.Chosen);
        Assert.Equal(0, rig.NewChats);
    }

    [AvaloniaFact]
    public void Tab_Stays_In_The_Menu()
    {
        using var rig = new Rig();
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Tab);
        Assert.True(rig.Pill.IsMenuOpen);
        Assert.True(rig.Rows[0].IsFocused);
    }

    #endregion

    #region Pointer

    [AvaloniaFact]
    public void Hover_Highlights_A_Row_And_Leaving_Clears_It()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Hover(rig.Rows[2]);
        Assert.Same(rig.Rows[2], rig.Pill.HighlightedRow);
        Assert.Equal(ArcTokens.GetColor(rig.Pill, ArcDesignTokenKind.SurfaceMuted), TestHelpers.ColorOf(rig.Rows[2].Background));
        rig.Hover(rig.Rows[3]);
        Assert.Same(rig.Rows[3], rig.Pill.HighlightedRow);
        Assert.Equal(Colors.Transparent, TestHelpers.ColorOf(rig.Rows[2].Background));
        rig.Hover(rig.Panel.List.Children.OfType<AiConversationMenuHeading>().Last());
        Assert.Null(rig.Pill.HighlightedRow);
        rig.Hover(rig.Before);
        Assert.Null(rig.Pill.HighlightedRow);
    }

    [AvaloniaFact]
    public void A_Click_Chooses_And_Focus_Returns_Without_A_Ring()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Press(rig.Rows[4]);
        Assert.False(rig.Pill.IsMenuOpen);
        Assert.Same(rig.Conversations[3], rig.Pill.Current);
        Assert.Single(rig.Chosen);
        Assert.True(rig.Pill.IsFocused);
        Assert.DoesNotContain(":focus-visible", rig.Pill.Classes);
    }

    [AvaloniaFact]
    public void A_Press_On_The_Pill_Closes_An_Open_Menu()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        Assert.True(rig.Pill.IsMenuOpen);
        rig.Press(rig.Pill);
        Assert.False(rig.Pill.IsMenuOpen);
        rig.Press(rig.Pill);
        Assert.True(rig.Pill.IsMenuOpen);
    }

    [AvaloniaFact]
    public void A_Press_Elsewhere_Closes_The_Menu()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Press(rig.Before);
        Assert.False(rig.Pill.IsMenuOpen);
        Assert.Empty(rig.Chosen);
    }

    [AvaloniaFact]
    public void A_Press_On_The_Menu_Surface_Keeps_It_Open()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Press(rig.Panel.List.Children.OfType<AiConversationMenuHeading>().First());
        Assert.True(rig.Pill.IsMenuOpen);
    }

    #endregion

    #region Live data and motion

    [AvaloniaFact]
    public void The_Open_Menu_Follows_The_Conversations_And_The_Current_One()
    {
        using var rig = new Rig();
        rig.TabToPill();
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Equal("Rename the staging bucket", rig.Pill.HighlightedRow!.Text);
        rig.Conversations.Insert(0, new AiChatConversation { Title = "Brand new", Group = "Today" });
        Assert.Equal("New chat | Brand new | Speed up the nightly build | Rename the staging bucket | Write the 4.2 release notes | " +
                     "Summarize the support survey | Flaky login test", Texts(rig.Rows));
        Assert.Equal("Rename the staging bucket", rig.Pill.HighlightedRow!.Text);
        Assert.True(rig.Pill.HighlightedRow!.IsFocused);
        rig.Conversations[1].Title = "Renamed: speed up the build";
        Assert.Equal("Renamed: speed up the build", rig.Rows[2].Text);
        Assert.Equal("Renamed: speed up the build", rig.Pill.CurrentText!.Text);
        rig.Pill.Current = rig.Conversations[4];
        Assert.Equal(new[] { false, false, false, false, false, true, false }, rig.Rows.Select(r => r.IsChecked).ToArray());
    }

    [AvaloniaFact]
    public void A_Closed_Menu_Lets_Go_Of_The_Data()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Key(PhysicalKey.Escape);
        var rows = rig.Rows.Count;
        rig.Conversations.Add(new AiChatConversation { Title = "Later" });
        Assert.Equal(rows, rig.Rows.Count);
        rig.Press(rig.Pill);
        Assert.Equal(rows + 1, rig.Rows.Count);
    }

    [AvaloniaFact]
    public void The_Menu_Opens_And_Closes_With_Motion_At_Full()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Press(rig.Pill);
        Assert.True(rig.Pill.MenuPopup!.IsOpen);
        rig.Frames(30);
        Assert.True(rig.Panel.IsEffectivelyVisible);
        rig.Key(PhysicalKey.Escape);
        Assert.False(rig.Pill.IsMenuOpen);
        rig.Frames(30);
        Assert.False(rig.Pill.MenuPopup!.IsOpen);
        // Reopening right away works.
        rig.Press(rig.Pill);
        rig.Key(PhysicalKey.Escape);
        rig.Press(rig.Pill);
        rig.Frames(30);
        Assert.True(rig.Pill.IsMenuOpen);
        Assert.True(rig.Pill.MenuPopup!.IsOpen);
    }

    [AvaloniaFact]
    public void A_Disabled_Pill_Does_Not_Open_And_Closes_Its_Menu()
    {
        using var rig = new Rig();
        rig.Press(rig.Pill);
        rig.Pill.IsEnabled = false;
        Assert.False(rig.Pill.IsMenuOpen);
        rig.Pill.OpenMenu();
        Assert.False(rig.Pill.IsMenuOpen);
    }

    #endregion
}
