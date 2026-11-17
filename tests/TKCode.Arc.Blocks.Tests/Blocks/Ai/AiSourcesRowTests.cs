using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Transformation;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiSourcesRow"/>: the button's geometry, colors and fan; opening and closing (pointer, keys, automation, Tab
/// order, inertness, focus); the grid and the narrow row at the 576 breakpoint; the cards' content, names and colors;
/// growth while sources stream in and the arrival stagger; the lighting protocol both ways; the narrow row's reveal,
/// snapping and edge fade; and the motion levels.
/// </summary>
public class AiSourcesRowTests
{
    private static AiSourcesRow Create(int count = 8, bool expanded = false, double bleed = 0) => new()
    {
        Sources = AiSourcesSamples.Sources(count),
        LabelText = AiSourcesSamples.Label(count),
        CardName = AiSourcesSamples.CardName,
        ListName = "Sources",
        IsExpanded = expanded,
        Bleed = bleed
    };

    /// <summary>A grid card: 1 + 11 + 16 + 6 + 19.6 + 12 + 1 = 66.6 (Arc), rounded to the pixel grid by layout.</summary>
    private const double CardHeight = 67;

    /// <summary>The open body: 2 above the cards, four rows 8 apart, 4 + 14 under them.</summary>
    private const double OpenBody = 2 + 4 * CardHeight + 3 * 8 + 4 + 14;

    private static AiSourcesToggle Toggle(AiSourcesRow row) => row.Toggle.ShouldNotBeNull();

    private static AiSourceCard Card(AiSourcesRow row, int number) => row.Cards.First(c => c.Number == number);

    private static Point At(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value;

    private static IInputElement? Focused(AiReplyRig rig) => rig.Window.FocusManager?.GetFocusedElement();

    private static List<int> Activations(AiSourcesRow row)
    {
        var numbers = new List<int>();
        row.CardActivated += (_, e) => numbers.Add(e.SourceNumber);
        return numbers;
    }

    #region The button

    [AvaloniaFact]
    public void The_Button_Is_A_34px_Pill_Set_10px_Out_With_The_Stack_The_Label_And_The_Chevron()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        toggle.Bounds.Height.ShouldBe(34);
        At(toggle, row).X.ShouldBe(-10, 0.01);
        toggle.Padding.ShouldBe(new Thickness(10, 0, 8, 0));
        TestHelpers.ColorOf(toggle.Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextSecondary));

        var stack = toggle.Stack.ShouldNotBeNull();
        stack.Count.ShouldBe(4);
        stack.Bounds.Size.ShouldBe(new Size(70, 20));
        // The first disc lines up with the text column (the stack's own 1px pad).
        At(stack, row).X.ShouldBe(0, 0.01);
        var label = TestHelpers.Descendant<TextBlock>(toggle, AiSourcesToggle.LabelPart);
        label.Text.ShouldBe("8 sources");
        label.LineHeight.ShouldBe(19.6);
        label.FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
        At(label, row).X.ShouldBe(70 + 2 + 8, 0.01);
        var chevron = TestHelpers.Descendant<ArcGlyph>(toggle, AiSourcesToggle.ChevronPart);
        chevron.Data.ShouldBe(ArcIcons.ChevronDown);
        chevron.Size.ShouldBe(15);
        chevron.StrokeThickness.ShouldBe(1.75);
        TestHelpers.ColorOf(chevron.Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextMuted));
        At(chevron, row).X.ShouldBe(80 + label.Bounds.Width + 8, 0.01);
        toggle.Bounds.Width.ShouldBe(10 + 70 + 2 + 8 + label.Bounds.Width + 8 + 15 + 8, 0.01);
    }

    [AvaloniaFact]
    public void Closed_The_Row_Is_42_High_With_No_Cards_Built()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        row.Bounds.Height.ShouldBe(42, 0.01);
        row.Cards.Count.ShouldBe(0);
        row.Body.ShouldNotBeNull().IsMounted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Hover_Fills_The_Pill_Darkens_The_Label_And_Fans_The_Stack()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        rig.Hover(toggle);
        toggle.Classes.ShouldContain(AiSourcesToggle.HoverPseudoClass);
        TestHelpers.ColorOf(toggle.Background).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(toggle.Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Foreground));
        toggle.Stack!.IsFanned.ShouldBeTrue();
        // The ring follows the fill so it still cuts the overlap cleanly.
        TestHelpers.ColorOf(toggle.Stack.RingBrush).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted));

        rig.Window.MouseMove(new Point(1, 590), RawInputModifiers.None);
        rig.Settle();
        toggle.Classes.ShouldNotContain(AiSourcesToggle.HoverPseudoClass);
        toggle.Stack.IsFanned.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Pressed_The_Fill_Is_Muted_Surface_And_Border_Half_And_Half()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        var point = rig.CenterOf(toggle);
        rig.Window.MouseMove(point, RawInputModifiers.None);
        rig.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        rig.Settle();
        toggle.IsPressed.ShouldBeTrue();
        var expected = Oklch.MixOklab(ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted), ArcTokens.GetColor(row, ArcDesignTokenKind.Border), 0.5);
        TestHelpers.ColorOf(TestHelpers.Descendant<Border>(toggle, "PART_Frame").Background).ShouldBe(expected);
        rig.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        rig.Settle();
        TestHelpers.ColorOf(TestHelpers.Descendant<Border>(toggle, "PART_Frame").Background).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted));
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Ring_And_Fans_The_Stack_Without_A_Fill()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        toggle.Focus(NavigationMethod.Tab);
        rig.Settle();
        TestHelpers.Descendant<Border>(toggle, "PART_FocusRing").IsVisible.ShouldBeTrue();
        toggle.Stack!.IsFanned.ShouldBeTrue();
        TestHelpers.ColorOf(toggle.Background).A.ShouldBe((byte)0);
    }

    [AvaloniaFact]
    public void The_Label_Follows_The_Owner_And_Names_The_Button()
    {
        var row = Create(count: 3);
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        AutomationProperties.GetName(toggle).ShouldBe("3 sources");
        row.LabelText = "4 sources";
        AutomationProperties.GetName(toggle).ShouldBe("4 sources");
        TestHelpers.Descendant<TextBlock>(toggle, AiSourcesToggle.LabelPart).Text.ShouldBe("4 sources");
    }

    #endregion

    #region Open and close

    [AvaloniaFact]
    public void A_Press_Opens_And_Closes_The_Cards_Two_Way()
    {
        var row = Create();
        var changes = new List<bool>();
        row.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiSourcesRow.IsExpandedProperty)
            {
                changes.Add(e.GetNewValue<bool>());
            }
        };
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        rig.Click(toggle);
        row.IsExpanded.ShouldBeTrue();
        row.Classes.ShouldContain(AiSourcesRow.ExpandedPseudoClass);
        toggle.Classes.ShouldContain(AiSourcesToggle.ExpandedPseudoClass);
        row.Body!.IsMounted.ShouldBeTrue();
        row.Cards.Count.ShouldBe(8);
        var chevron = TestHelpers.Descendant<ArcGlyph>(toggle, AiSourcesToggle.ChevronPart);
        chevron.RenderTransform!.Value.M11.ShouldBe(-1, 0.0001);

        rig.Click(toggle);
        row.IsExpanded.ShouldBeFalse();
        row.Body.IsMounted.ShouldBeFalse();
        // Built once: the cards wait for the next opening.
        row.Cards.Count.ShouldBe(8);
        chevron.RenderTransform!.Value.M11.ShouldBe(1, 0.0001);
        changes.ShouldBe([true, false]);
    }

    [AvaloniaTheory]
    [InlineData(PhysicalKey.Enter)]
    [InlineData(PhysicalKey.Space)]
    public void Enter_And_Space_Toggle_The_Cards(PhysicalKey key)
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        Toggle(row).Focus(NavigationMethod.Tab);
        rig.Key(key);
        row.IsExpanded.ShouldBeTrue();
        rig.Key(key);
        row.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Automation_Reports_And_Changes_The_Expanded_State()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        var peer = ControlAutomationPeer.CreatePeerForElement(Toggle(row));
        var expand = peer.ShouldBeAssignableTo<IExpandCollapseProvider>()!;
        peer.GetName().ShouldBe("8 sources");
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        peer.GetChildren().ShouldBeEmpty();
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        expand.Expand();
        rig.Settle();
        row.IsExpanded.ShouldBeTrue();
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        expand.Collapse();
        rig.Settle();
        row.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Tab_Reaches_The_Cards_After_The_Button_Only_While_Open()
    {
        var row = Create(count: 3);
        using var rig = new AiReplyRig(row);
        var toggle = Toggle(row);
        toggle.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        Focused(rig).ShouldNotBeOfType<AiSourceCard>();

        row.IsExpanded = true;
        rig.Settle();
        toggle.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        Focused(rig).ShouldBeSameAs(Card(row, 1));
        rig.Key(PhysicalKey.Tab);
        Focused(rig).ShouldBeSameAs(Card(row, 2));
        rig.Key(PhysicalKey.Tab);
        Focused(rig).ShouldBeSameAs(Card(row, 3));
    }

    [AvaloniaFact]
    public void Closing_With_Focus_On_A_Card_Gives_It_Back_To_The_Button()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        Card(row, 2).Focus(NavigationMethod.Tab);
        rig.Settle();
        row.IsExpanded = false;
        rig.Settle();
        Focused(rig).ShouldBeSameAs(Toggle(row));
        Toggle(row).Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void While_Closing_The_Cards_Are_Already_Out_Of_Reach()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row, ArcMotionLevel.Full, clock);
        var scroller = row.Scroller.ShouldNotBeNull();
        scroller.IsHitTestVisible.ShouldBeTrue();
        KeyboardNavigation.GetTabNavigation(scroller).ShouldBe(KeyboardNavigationMode.Continue);

        row.IsExpanded = false;
        rig.Frames(50);
        row.Body!.IsMounted.ShouldBeTrue();
        row.Body.IsMoving.ShouldBeTrue();
        scroller.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetTabNavigation(scroller).ShouldBe(KeyboardNavigationMode.None);
        rig.Frames(1200);
        row.Body.IsMounted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Opening_Springs_The_Height_And_Fades_The_Cards_In_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create();
        using var rig = new AiReplyRig(row, ArcMotionLevel.Full, clock);
        row.IsExpanded = true;
        rig.Frames(50);
        var body = row.Body!;
        body.IsMoving.ShouldBeTrue();
        body.DisplayedHeight.ShouldBeGreaterThan(0);
        body.DisplayedHeight.ShouldBeLessThan(OpenBody);
        body.DisplayedOpacity.ShouldBeLessThan(1);
        // The cards present at the opening come in with the body, not one by one.
        row.Cards.ShouldAllBe(c => Math.Abs(c.Opacity - 1) < 0.001);
        rig.Frames(1200);
        body.IsMoving.ShouldBeFalse();
        body.DisplayedHeight.ShouldBe(OpenBody, 0.01);
        row.Bounds.Height.ShouldBe(34 + 8 + OpenBody, 0.01);
    }

    [AvaloniaFact]
    public void Open_At_Off_Motion_Jumps()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        row.IsExpanded = true;
        rig.Settle();
        row.Body!.IsMoving.ShouldBeFalse();
        row.Bounds.Height.ShouldBe(34 + 8 + OpenBody, 0.01);
    }

    #endregion

    #region Grid and narrow row

    [AvaloniaFact]
    public void Wide_The_Cards_Form_Two_Equal_Columns_8px_Apart()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row, width: 700);
        row.IsNarrow.ShouldBeFalse();
        row.Classes.ShouldNotContain(AiSourcesRow.NarrowPseudoClass);
        row.Scroller!.HorizontalScrollBarVisibility.ShouldBe(Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        for (var i = 0; i < 8; i++)
        {
            var card = row.Cards[i];
            var origin = At(card, row);
            origin.X.ShouldBe(i % 2 == 0 ? 0 : 354, 0.01);
            origin.Y.ShouldBe(34 + 8 + 2 + i / 2 * (CardHeight + 8), 0.01);
            card.Bounds.Size.ShouldBe(new Size(346, CardHeight));
            card.IsNarrow.ShouldBeFalse();
        }
        // The open row: the button, 8, the list's 2 / 4 padding and its 14 margin around four rows of cards.
        row.Bounds.Height.ShouldBe(34 + 8 + OpenBody, 0.01);
        row.HasEdgeFade.ShouldBeFalse();
        row.Scroller.OpacityMask.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Lone_Card_Takes_One_Column()
    {
        var row = Create(count: 1, expanded: true);
        using var rig = new AiReplyRig(row, width: 700);
        row.Cards[0].Bounds.Width.ShouldBe(346, 0.01);
    }

    [AvaloniaFact]
    public void A_Card_Shows_Its_Mark_Domain_Title_And_Number()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row, width: 700);
        var card = Card(row, 2);
        card.Padding.ShouldBe(new Thickness(12, 11, 12, 12));
        card.CornerRadius.ShouldBe(new CornerRadius(14));
        card.BorderThickness.ShouldBe(new Thickness(1));
        var mark = TestHelpers.Descendant<AiSourceMark>(card, "PART_Mark");
        mark.Size.ShouldBe(14);
        mark.Source.ShouldBeSameAs(row.Sources![1]);
        At(mark, card).ShouldBe(new Point(13, 13));
        var domain = TestHelpers.Descendant<TextBlock>(card, "PART_Domain");
        domain.Text.ShouldBe("eng.northgate.io");
        domain.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(domain.Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextMuted));
        At(domain, card).X.ShouldBe(13 + 14 + 7, 0.01);
        // Clear of the number: 28px on the right of the top line.
        (At(domain, card).X + domain.Bounds.Width).ShouldBeLessThanOrEqualTo(card.Bounds.Width - 13 - 28 + 0.01);
        var title = TestHelpers.Descendant<TextBlock>(card, "PART_Title");
        title.Text.ShouldBe("Caching dependencies across runners");
        title.FontWeight.ShouldBe(FontWeight.Medium);
        title.MaxLines.ShouldBe(1);
        title.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
        At(title, card).Y.ShouldBe(12 + 16 + 6, 0.01);
        var number = TestHelpers.Descendant<Border>(card, "PART_Number");
        number.Bounds.Height.ShouldBe(18);
        number.Bounds.Width.ShouldBeGreaterThanOrEqualTo(18);
        At(number, card).Y.ShouldBe(11, 0.01);
        (At(number, card).X + number.Bounds.Width).ShouldBe(card.Bounds.Width - 11, 0.01);
        TestHelpers.Descendant<TextBlock>(card, "PART_NumberText").Text.ShouldBe("2");
    }

    [AvaloniaTheory]
    [InlineData(576, 0, true)]
    [InlineData(577, 0, false)]
    [InlineData(536, 20, true)]
    [InlineData(537, 20, false)]
    public void The_Row_Turns_Narrow_At_576_With_Its_Bleed(double width, double bleed, bool narrow)
    {
        var row = Create(expanded: true, bleed: bleed);
        using var rig = new AiReplyRig(row, width: width);
        row.IsNarrow.ShouldBe(narrow);
        row.Classes.Contains(AiSourcesRow.NarrowPseudoClass).ShouldBe(narrow);
        row.Panel!.IsRow.ShouldBe(narrow);
        row.Cards.ShouldAllBe(c => c.IsNarrow == narrow);
    }

    [AvaloniaFact]
    public void Narrow_The_Cards_Scroll_In_One_Line_From_Edge_To_Edge()
    {
        var row = Create(expanded: true, bleed: 16);
        using var rig = new AiReplyRig(row, width: 358);
        row.IsNarrow.ShouldBeTrue();
        var scroller = row.Scroller!;
        scroller.HorizontalScrollBarVisibility.ShouldBe(Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
        // The scroller reaches 16px past the row on both sides; the first card starts at the column.
        At(scroller, row).X.ShouldBe(-16, 0.01);
        scroller.Bounds.Width.ShouldBe(358 + 32, 0.01);
        var width = AiSourceSnap.CardWidth(358);
        width.ShouldBe(248);
        var height = row.Cards[0].Bounds.Height;
        height.ShouldBeGreaterThanOrEqualTo(CardHeight);
        for (var i = 0; i < 8; i++)
        {
            var card = row.Cards[i];
            At(card, row).X.ShouldBe(i * (248 + 8), 0.01);
            At(card, row).Y.ShouldBe(34 + 8 + 2, 0.01);
            card.Bounds.Width.ShouldBe(248, 0.01);
            // One line, all as high as the tallest.
            card.Bounds.Height.ShouldBe(height, 0.01);
            var title = TestHelpers.Descendant<TextBlock>(card, "PART_Title");
            title.MaxLines.ShouldBe(2);
            title.TextWrapping.ShouldBe(TextWrapping.Wrap);
        }
        scroller.Extent.Width.ShouldBe(16 + 8 * 248 + 7 * 8 + 16, 0.01);
    }

    [AvaloniaFact]
    public void Narrow_Cards_Take_76_Percent_Of_A_Small_Row()
    {
        var row = Create(expanded: true, bleed: 16);
        using var rig = new AiReplyRig(row, width: 288);
        // 218.88, rounded to the pixel grid by layout.
        row.Cards[0].Bounds.Width.ShouldBe(288 * 0.76, 0.5);
    }

    [AvaloniaFact]
    public void Turning_Wide_Again_Restores_The_Grid_And_The_Scroll()
    {
        var row = Create(expanded: true, bleed: 16);
        using var rig = new AiReplyRig(row, width: 358);
        row.Scroller!.Offset = new Vector(300, 0);
        rig.Settle();
        ((StackPanel)row.Parent!).Width = 700;
        rig.Settle();
        row.IsNarrow.ShouldBeFalse();
        row.Scroller.Offset.X.ShouldBe(0);
        At(row.Cards[1], row).X.ShouldBe(354, 0.01);
    }

    #endregion

    #region Card colors

    [AvaloniaFact]
    public void A_Card_At_Rest_Is_Surface_With_The_Subtle_Hairline()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        var card = Card(row, 1);
        TestHelpers.ColorOf(card.Background).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(card.BorderBrush).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.BorderSubtle));
        TestHelpers.ColorOf(TestHelpers.Descendant<Border>(card, "PART_Number").Background).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(card, "PART_NumberText").Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.TextSecondary));
    }

    [AvaloniaFact]
    public void Hover_Lit_And_Keyboard_Focus_Share_The_Lit_Look()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        var muted = ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted);
        var litNumber = Oklch.MixOklab(muted, ArcTokens.GetColor(row, ArcDesignTokenKind.Accent), 0.18);

        void ShouldLookLit(AiSourceCard card)
        {
            TestHelpers.ColorOf(card.Background).ShouldBe(muted);
            TestHelpers.ColorOf(card.BorderBrush).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Border));
            TestHelpers.ColorOf(TestHelpers.Descendant<Border>(card, "PART_Number").Background).ShouldBe(litNumber);
            TestHelpers.ColorOf(TestHelpers.Descendant<TextBlock>(card, "PART_NumberText").Foreground).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Foreground));
        }

        rig.Hover(Card(row, 1));
        ShouldLookLit(Card(row, 1));
        row.ActiveNumber = 4;
        rig.Settle();
        ShouldLookLit(Card(row, 4));
        Card(row, 6).Focus(NavigationMethod.Tab);
        rig.Settle();
        ShouldLookLit(Card(row, 6));
        TestHelpers.Descendant<Border>(Card(row, 6), "PART_FocusRing").IsVisible.ShouldBeTrue();
        TestHelpers.ColorOf(Card(row, 3).Background).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Surface));
    }

    [AvaloniaFact]
    public async Task In_Dark_The_Hairline_Is_Border_And_The_Lit_Edge_BorderStrong()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        await TestHelpers.ApplyAsync(TestHelpers.Dark);
        try
        {
            rig.Settle();
            var card = Card(row, 1);
            card.Classes.ShouldContain(AiSourceCard.DarkPseudoClass);
            TestHelpers.ColorOf(card.BorderBrush).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Border));
            TestHelpers.ColorOf(card.Background).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.Surface));
            row.ActiveNumber = 1;
            rig.Settle();
            TestHelpers.ColorOf(card.BorderBrush).ShouldBe(ArcTokens.GetColor(row, ArcDesignTokenKind.BorderStrong));
            TestHelpers.ColorOf(TestHelpers.Descendant<Border>(card, "PART_Number").Background)
                .ShouldBe(Oklch.MixOklab(ArcTokens.GetColor(row, ArcDesignTokenKind.SurfaceMuted), ArcTokens.GetColor(row, ArcDesignTokenKind.Accent), 0.18));
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Full, true)]
    [InlineData(ArcMotionLevel.Reduced, false)]
    [InlineData(ArcMotionLevel.Off, false)]
    public void Tints_Ease_Only_At_Full_Motion(ArcMotionLevel level, bool eased)
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row, level);
        var card = Card(row, 1);
        (card.Transitions?.Count > 0).ShouldBe(eased);
        (TestHelpers.Descendant<Border>(card, "PART_Number").Transitions?.Count > 0).ShouldBe(eased);
        (TestHelpers.Descendant<TextBlock>(card, "PART_NumberText").Transitions?.Count > 0).ShouldBe(eased);
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Full, true)]
    [InlineData(ArcMotionLevel.Reduced, false)]
    [InlineData(ArcMotionLevel.Off, false)]
    public void The_Chevron_Turns_On_The_Css_Spring_Only_At_Full_Motion(ArcMotionLevel level, bool sprung)
    {
        var row = Create();
        using var rig = new AiReplyRig(row, level);
        var chevron = TestHelpers.Descendant<ArcGlyph>(Toggle(row), AiSourcesToggle.ChevronPart);
        var transition = chevron.Transitions?.OfType<ArcTransformOperationsTransition>().SingleOrDefault();
        (transition is not null).ShouldBe(sprung);
        if (transition is not null)
        {
            transition.Tween.ShouldBe(ArcTween.Spring);
        }
    }

    #endregion

    #region Names

    [AvaloniaFact]
    public void Cards_Are_Links_Named_By_The_Owner_In_A_Named_List()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        var card = Card(row, 1);
        var peer = ControlAutomationPeer.CreatePeerForElement(card);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Hyperlink);
        peer.GetName().ShouldBe("Source 1: Running two pipelines side by side, docs.pipewright.dev");
        peer.GetChildren().ShouldBeEmpty();
        AutomationProperties.GetName(row.Panel!).ShouldBe("Sources");
        ControlAutomationPeer.CreatePeerForElement(row.Panel!).GetAutomationControlType().ShouldBe(AutomationControlType.List);

        row.Sources![0].Title = "Running pipelines in parallel";
        AutomationProperties.GetName(card).ShouldBe("Source 1: Running pipelines in parallel, docs.pipewright.dev");
        TestHelpers.Descendant<TextBlock>(card, "PART_Title").Text.ShouldBe("Running pipelines in parallel");
        row.CardName = null;
        AutomationProperties.GetName(card).ShouldBe("Running pipelines in parallel");
        row.ListName = null;
        AutomationProperties.GetName(row.Panel!).ShouldBeNull();
    }

    #endregion

    #region Streaming in

    [AvaloniaFact]
    public void The_Cards_Follow_The_List_While_Sources_Arrive()
    {
        var row = Create(count: 2, expanded: true);
        using var rig = new AiReplyRig(row);
        var sources = (AvaloniaList<AiSource>)row.Sources!;
        var more = AiSourcesSamples.Sources();
        sources.Add(more[2]);
        sources.AddRange([more[3], more[4]]);
        rig.Settle();
        row.Cards.Select(c => c.Number).ShouldBe([1, 2, 3, 4, 5]);
        Toggle(row).Stack!.Count.ShouldBe(4);
        sources.Insert(0, more[7]);
        row.Cards.Select(c => c.Number).ShouldBe([8, 1, 2, 3, 4, 5]);
        sources.RemoveAt(2);
        row.Cards.Select(c => c.Number).ShouldBe([8, 1, 3, 4, 5]);
        sources[0] = more[6];
        row.Cards.Select(c => c.Number).ShouldBe([7, 1, 3, 4, 5]);
        sources.Move(0, 4);
        row.Cards.Select(c => c.Number).ShouldBe([1, 3, 4, 5, 7]);
        sources.Clear();
        row.Cards.ShouldBeEmpty();
        row.Panel!.Children.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_New_List_Replaces_The_Cards()
    {
        var row = Create(count: 3, expanded: true);
        using var rig = new AiReplyRig(row);
        var before = row.Cards.ToArray();
        row.Sources = AiSourcesSamples.Sources(5);
        rig.Settle();
        row.Cards.Count.ShouldBe(5);
        row.Cards.ShouldAllBe(c => !before.Contains(c));
        row.Cards.Select(c => c.Source).ShouldBe(row.Sources!);
        row.Sources = null;
        row.Cards.ShouldBeEmpty();
        Toggle(row).Stack!.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Sources_Arriving_While_Open_Fade_In_One_After_Another()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create(count: 2, expanded: true);
        using var rig = new AiReplyRig(row, ArcMotionLevel.Full, clock);
        var more = AiSourcesSamples.Sources();
        for (var i = 2; i < 5; i++)
        {
            row.Sources!.Add(more[i]);
        }
        rig.Settle();
        var arrived = row.Cards.Skip(2).ToArray();
        arrived.ShouldAllBe(c => c.Opacity == 0);
        row.Cards.Take(2).ShouldAllBe(c => c.Opacity == 1);
        // 40ms, 60ms and 80ms waits (Arc's 40 + 20 · i), then 200ms each.
        rig.Frames(50);
        arrived[0].Opacity.ShouldBeGreaterThan(0);
        arrived[2].Opacity.ShouldBe(0);
        rig.Frames(100);
        arrived[0].Opacity.ShouldBeGreaterThan(arrived[1].Opacity);
        arrived[1].Opacity.ShouldBeGreaterThan(arrived[2].Opacity);
        arrived[2].Opacity.ShouldBeGreaterThan(0);
        rig.Frames(300);
        arrived.ShouldAllBe(c => c.Opacity == 1);
    }

    [AvaloniaFact]
    public void A_New_List_While_Open_Arrives_With_The_Stagger()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create(count: 2, expanded: true);
        using var rig = new AiReplyRig(row, ArcMotionLevel.Full, clock);
        row.Sources = AiSourcesSamples.Sources(3);
        rig.Settle();
        row.Cards.ShouldAllBe(c => c.Opacity == 0);
        rig.Frames(500);
        row.Cards.ShouldAllBe(c => c.Opacity == 1);
    }

    [AvaloniaFact]
    public void A_Long_Batch_Waits_At_Most_160ms()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create(count: 0, expanded: true);
        using var rig = new AiReplyRig(row, ArcMotionLevel.Full, clock);
        foreach (var source in AiSourcesSamples.Sources())
        {
            row.Sources!.Add(source);
        }
        rig.Settle();
        rig.Frames(170);
        row.Cards[6].Opacity.ShouldBeGreaterThan(0);
        row.Cards[7].Opacity.ShouldBe(row.Cards[6].Opacity, 0.001);
        row.Cards[5].Opacity.ShouldBeGreaterThan(row.Cards[6].Opacity);
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_Arrivals_Fade_Briefly_Without_Waiting()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create(count: 1, expanded: true);
        using var rig = new AiReplyRig(row, ArcMotionLevel.Reduced, clock);
        row.Sources!.Add(AiSourcesSamples.Sources()[1]);
        row.Sources.Add(AiSourcesSamples.Sources()[2]);
        rig.Settle();
        rig.Frames(50);
        row.Cards[1].Opacity.ShouldBeInRange(0.2, 0.7);
        row.Cards[2].Opacity.ShouldBe(row.Cards[1].Opacity, 0.001);
        rig.Frames(100);
        row.Cards.ShouldAllBe(c => c.Opacity == 1);
    }

    [AvaloniaFact]
    public void At_Off_Motion_Arrivals_Show_At_Once()
    {
        var row = Create(count: 1, expanded: true);
        using var rig = new AiReplyRig(row);
        row.Sources!.Add(AiSourcesSamples.Sources()[1]);
        rig.Settle();
        row.Cards[1].Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Sources_Arriving_While_Closed_Do_Not_Fade()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var row = Create(count: 1);
        using var rig = new AiReplyRig(row, ArcMotionLevel.Full, clock);
        row.IsExpanded = true;
        rig.Frames(1000);
        row.IsExpanded = false;
        rig.Frames(1000);
        row.Sources!.Add(AiSourcesSamples.Sources()[1]);
        rig.Settle();
        row.Cards[1].Opacity.ShouldBe(1);
    }

    #endregion

    #region Lighting

    [AvaloniaFact]
    public void The_Active_Number_Lights_That_Card_Only()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        row.ActiveNumber = 3;
        row.Cards.Where(c => c.IsLit).Select(c => c.Number).ShouldBe([3]);
        Card(row, 3).Classes.ShouldContain(AiSourceCard.LitPseudoClass);
        row.ActiveNumber = 0;
        row.Cards.ShouldAllBe(c => !c.IsLit);
        // A number no card has lights nothing.
        row.ActiveNumber = 42;
        row.Cards.ShouldAllBe(c => !c.IsLit);
    }

    [AvaloniaFact]
    public void A_Card_Lit_Before_The_Row_Opened_Shows_Lit_When_It_Opens()
    {
        var row = Create();
        using var rig = new AiReplyRig(row);
        row.ActiveNumber = 2;
        row.IsExpanded = true;
        rig.Settle();
        Card(row, 2).IsLit.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Card_Whose_Number_Changes_Follows_The_Active_Number()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row);
        row.ActiveNumber = 9;
        var source = row.Sources![2];
        source.Index = 9;
        rig.Settle();
        row.Cards[2].IsLit.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(row.Cards[2], "PART_NumberText").Text.ShouldBe("9");
    }

    [AvaloniaFact]
    public void Pointing_At_Cards_Raises_Their_Numbers_And_Zero_On_Leaving()
    {
        var row = Create(expanded: true);
        var numbers = Activations(row);
        AiSourceCardEventArgs? last = null;
        row.CardActivated += (_, e) => last = e;
        using var rig = new AiReplyRig(row);
        rig.Hover(Card(row, 2));
        numbers.ShouldBe([2]);
        last!.Item.ShouldBeSameAs(row.Sources![1]);
        last.Source.ShouldBeSameAs(Card(row, 2));
        rig.Hover(Card(row, 5));
        numbers.ShouldBe([2, 0, 5]);
        rig.Window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        rig.Settle();
        numbers.ShouldBe([2, 0, 5, 0]);
        last.SourceNumber.ShouldBe(0);
        last.Item.ShouldBeNull();
        row.ActivatedNumber.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Raises_The_Number_A_Click_Focus_Does_Not()
    {
        var row = Create(expanded: true);
        var numbers = Activations(row);
        using var rig = new AiReplyRig(row);
        Card(row, 3).Focus(NavigationMethod.Tab);
        rig.Settle();
        numbers.ShouldBe([3]);
        Card(row, 4).Focus(NavigationMethod.Tab);
        rig.Settle();
        numbers.ShouldBe([3, 0, 4]);
        Toggle(row).Focus(NavigationMethod.Tab);
        rig.Settle();
        numbers.ShouldBe([3, 0, 4, 0]);
        Card(row, 6).Focus(NavigationMethod.Pointer);
        rig.Settle();
        numbers.ShouldBe([3, 0, 4, 0]);
    }

    [AvaloniaFact]
    public void The_Pointer_Wins_Over_Keyboard_Focus_And_Hands_Back_To_It()
    {
        var row = Create(expanded: true);
        var numbers = Activations(row);
        using var rig = new AiReplyRig(row);
        Card(row, 1).Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Hover(Card(row, 4));
        rig.Window.MouseMove(new Point(1, 1), RawInputModifiers.None);
        rig.Settle();
        numbers.ShouldBe([1, 4, 1]);
    }

    [AvaloniaFact]
    public void Touch_Does_Not_Light_A_Card()
    {
        var row = Create(expanded: true);
        var numbers = Activations(row);
        using var rig = new AiReplyRig(row);
        var card = Card(row, 2);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        card.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, card, pointer, rig.Window, default, 0,
            new PointerPointProperties(), KeyModifiers.None));
        card.Classes.ShouldNotContain(AiSourceCard.HoverPseudoClass);
        numbers.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Closing_Or_Leaving_The_Window_Ends_The_Activation()
    {
        var row = Create(expanded: true);
        var numbers = Activations(row);
        using var rig = new AiReplyRig(row);
        Card(row, 2).Focus(NavigationMethod.Tab);
        rig.Settle();
        row.IsExpanded = false;
        rig.Settle();
        numbers.ShouldBe([2, 0]);

        row.IsExpanded = true;
        rig.Settle();
        rig.Hover(Card(row, 3));
        ((StackPanel)row.Parent!).Children.Remove(row);
        numbers.ShouldBe([2, 0, 3, 0]);
    }

    [AvaloniaFact]
    public void A_Card_Removed_Under_The_Pointer_Ends_Its_Activation()
    {
        var row = Create(expanded: true);
        var numbers = Activations(row);
        using var rig = new AiReplyRig(row);
        rig.Hover(Card(row, 3));
        row.Sources!.RemoveAt(2);
        rig.Settle();
        numbers.Take(2).ShouldBe([3, 0]);
    }

    [AvaloniaFact]
    public void Pressing_A_Card_Raises_Card_Invoked_With_Its_Source()
    {
        var row = Create(expanded: true);
        var invoked = new List<AiSourceCardEventArgs>();
        using var rig = new AiReplyRig(row);
        rig.Window.AddHandler(AiSourcesRow.CardInvokedEvent, (_, e) => invoked.Add(e));
        rig.Click(Card(row, 3));
        invoked.Count.ShouldBe(1);
        invoked[0].SourceNumber.ShouldBe(3);
        invoked[0].Item.ShouldBeSameAs(row.Sources![2]);

        Card(row, 5).Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        invoked.Count.ShouldBe(2);
        invoked[1].Item.ShouldBeSameAs(row.Sources[4]);
    }

    #endregion

    #region Narrow row: reveal, snap, fade

    private static (AiSourcesRow Row, AiReplyRig Rig) Narrow(ArcMotionLevel level = ArcMotionLevel.Off, ManualFrameClock? clock = null)
    {
        var row = Create(expanded: true, bleed: 16);
        var rig = new AiReplyRig(row, level, clock, width: 358);
        return (row, rig);
    }

    [AvaloniaFact]
    public void Lighting_A_Card_Out_Of_View_Scrolls_Its_Start_To_The_Column()
    {
        var (row, rig) = Narrow();
        using (rig)
        {
            row.ActiveNumber = 5;
            rig.Settle();
            // Card 5 starts at 16 + 4 · 256; its start goes to the 16px scroll padding.
            row.Scroller!.Offset.X.ShouldBe(4 * 256, 0.01);
            At(Card(row, 5), row).X.ShouldBe(0, 0.01);
            // Card 4 is partly in view on the left: back to its start.
            row.ActiveNumber = 4;
            rig.Settle();
            row.Scroller.Offset.X.ShouldBe(3 * 256, 0.01);
            // Card 5 now shows whole: no scroll.
            // Card 5 (1040–1288) shows whole between the paddings at 950: no scroll.
            row.Scroller.Offset = new Vector(950, 0);
            rig.Settle();
            row.ActiveNumber = 0;
            row.ActiveNumber = 5;
            rig.Settle();
            row.Scroller.Offset.X.ShouldBe(950, 0.01);
        }
    }

    [AvaloniaFact]
    public void The_Last_Card_Scrolls_Only_To_The_End()
    {
        var (row, rig) = Narrow();
        using (rig)
        {
            row.ActiveNumber = 8;
            rig.Settle();
            var scroller = row.Scroller!;
            scroller.Offset.X.ShouldBe(scroller.Extent.Width - scroller.Viewport.Width, 0.01);
        }
    }

    [AvaloniaFact]
    public void A_Card_Lit_Before_The_Row_Laid_Out_Is_Shown()
    {
        var row = Create(expanded: true, bleed: 16);
        row.ActiveNumber = 6;
        using var rig = new AiReplyRig(row, width: 358);
        rig.Settle();
        row.Scroller!.Offset.X.ShouldBe(5 * 256, 0.01);
    }

    [AvaloniaFact]
    public void The_Wide_Grid_Never_Scrolls_For_A_Lit_Card()
    {
        var row = Create(expanded: true);
        using var rig = new AiReplyRig(row, width: 700);
        row.ActiveNumber = 8;
        rig.Settle();
        row.Scroller!.Offset.X.ShouldBe(0);
        row.IsScrolling.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Reveal_Is_Smooth_And_The_Person_Can_Take_Over()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (row, rig) = Narrow(ArcMotionLevel.Full, clock);
        using (rig)
        {
            row.ActiveNumber = 5;
            rig.Frames(100);
            row.IsScrolling.ShouldBeTrue();
            row.ScrollTarget.ShouldBe(4 * 256, 0.01);
            var x = row.Scroller!.Offset.X;
            x.ShouldBeGreaterThan(0);
            x.ShouldBeLessThan(4 * 256);
            rig.Frames(1000);
            row.IsScrolling.ShouldBeFalse();
            row.Scroller.Offset.X.ShouldBe(4 * 256, 0.5);

            // A second reveal, interrupted: the person's scroll wins.
            row.ActiveNumber = 1;
            rig.Frames(50);
            row.IsScrolling.ShouldBeTrue();
            row.Scroller.Offset = new Vector(row.Scroller.Offset.X - 40, 0);
            rig.Settle();
            row.IsScrolling.ShouldBeFalse();
            var held = row.Scroller.Offset.X;
            rig.Frames(300);
            row.Scroller.Offset.X.ShouldBe(held, 0.01);
        }
    }

    [AvaloniaFact]
    public void A_Press_On_The_Row_Stops_A_Running_Scroll()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (row, rig) = Narrow(ArcMotionLevel.Full, clock);
        using (rig)
        {
            row.ActiveNumber = 5;
            rig.Frames(50);
            row.IsScrolling.ShouldBeTrue();
            var point = rig.CenterOf(row.Cards[1]);
            rig.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            rig.Settle();
            row.IsScrolling.ShouldBeFalse();
            // No snap while the pointer is down.
            row.SettleScroll();
            row.IsScrolling.ShouldBeFalse();
            rig.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            rig.Settle();
        }
    }

    [AvaloniaTheory]
    [InlineData(100, 0)]
    [InlineData(150, 256)]
    [InlineData(700, 768)]
    public void After_A_Scroll_The_Row_Snaps_To_The_Nearest_Card(double scrolled, double snapped)
    {
        var (row, rig) = Narrow();
        using (rig)
        {
            row.Scroller!.Offset = new Vector(scrolled, 0);
            rig.Settle();
            row.SettleScroll();
            rig.Settle();
            row.Scroller.Offset.X.ShouldBe(snapped, 0.01);
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Snap_Springs_There()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (row, rig) = Narrow(ArcMotionLevel.Full, clock);
        using (rig)
        {
            row.Scroller!.Offset = new Vector(150, 0);
            rig.Settle();
            row.SettleScroll();
            rig.Frames(50);
            row.IsScrolling.ShouldBeTrue();
            row.Scroller.Offset.X.ShouldBeInRange(150.5, 255.5);
            rig.Frames(1000);
            row.Scroller.Offset.X.ShouldBe(256, 0.5);
        }
    }

    [AvaloniaFact]
    public void The_Snap_Waits_For_The_Scroll_To_Rest()
    {
        var (row, rig) = Narrow();
        using (rig)
        {
            row.Scroller!.Offset = new Vector(150, 0);
            rig.Settle();
            // The idle timer has not fired (headless timers run only on a pushed frame): nothing moved yet.
            row.Scroller.Offset.X.ShouldBe(150);
            AiSourcesRow.SnapIdle.ShouldBe(TimeSpan.FromMilliseconds(150));
        }
    }

    [AvaloniaFact]
    public void The_Right_Edge_Fades_Only_While_More_Cards_Follow()
    {
        var (row, rig) = Narrow();
        using (rig)
        {
            var scroller = row.Scroller!;
            row.HasEdgeFade.ShouldBeTrue();
            var mask = scroller.OpacityMask.ShouldBeOfType<ImmutableLinearGradientBrush>();
            mask.GradientStops.Count.ShouldBe(3);
            mask.GradientStops[1].Offset.ShouldBe(1 - 28 / scroller.Bounds.Width, 0.0001);
            mask.GradientStops[1].Color.ShouldBe(Colors.Black);
            mask.GradientStops[2].Color.A.ShouldBe((byte)0);
            mask.StartPoint.ShouldBe(new RelativePoint(0, 0.5, RelativeUnit.Relative));
            mask.EndPoint.ShouldBe(new RelativePoint(1, 0.5, RelativeUnit.Relative));

            scroller.Offset = new Vector(scroller.Extent.Width, 0);
            rig.Settle();
            row.HasEdgeFade.ShouldBeFalse();
            scroller.OpacityMask.ShouldBeNull();

            scroller.Offset = new Vector(500, 0);
            rig.Settle();
            row.HasEdgeFade.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void A_Short_Narrow_Row_That_Fits_Has_No_Fade()
    {
        var row = Create(count: 1, expanded: true, bleed: 16);
        using var rig = new AiReplyRig(row, width: 358);
        row.IsNarrow.ShouldBeTrue();
        row.HasEdgeFade.ShouldBeFalse();
        row.Scroller!.OpacityMask.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Tab_To_A_Card_Out_Of_View_Brings_It_In()
    {
        var (row, rig) = Narrow();
        using (rig)
        {
            Card(row, 1).Focus(NavigationMethod.Tab);
            rig.Settle();
            rig.Key(PhysicalKey.Tab);
            Focused(rig).ShouldBeSameAs(Card(row, 2));
            row.Scroller!.Offset.X.ShouldBe(256, 0.01);
        }
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_The_Reveal_Jumps()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (row, rig) = Narrow(ArcMotionLevel.Reduced, clock);
        using (rig)
        {
            row.ActiveNumber = 3;
            rig.Frames(17);
            row.Scroller!.Offset.X.ShouldBe(2 * 256, 0.01);
        }
    }

    #endregion
}
