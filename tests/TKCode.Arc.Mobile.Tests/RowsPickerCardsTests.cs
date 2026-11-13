using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>Issue #111: content cells, the leading slot, the check mark of an action row, Picker, RadioCards, the sheet safe area and the avatar tokens.</summary>
public class RowsPickerCardsTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 400)
    {
        var window = new Window { Content = content, Width = width, Height = 700 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Named<T>(Control root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Point CenterOf(Control control, Visual to)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), to)!.Value;

    private static void Tap(Window window, Control control)
    {
        var at = CenterOf(control, window);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool SeparatorShown(Control row) => Named<Border>(row, "PART_Separator").IsVisible;

    // ---- ListCell -------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void ListCell_Is_A_Row_Of_Its_Own_With_The_Item_Separator_Rule_And_No_Press_Response()
    {
        var cell = new ListCell { Content = new TextBlock { Text = "Scan to join" } };
        var group = new ListGroup { Items = { new ListItem { Content = "Name" }, cell, new ListItem { Content = "Email" } } };
        var window = Show(new Page { Content = group });
        try
        {
            group.ContainerFromIndex(1).ShouldBeSameAs(cell);
            cell.GetVisualAncestors().OfType<ListItem>().ShouldBeEmpty();
            cell.Bounds.Height.ShouldBeGreaterThanOrEqualTo(48);
            cell.Focusable.ShouldBeFalse();
            SeparatorShown(cell).ShouldBeTrue();
            SeparatorShown((Control)group.ContainerFromIndex(2)!).ShouldBeFalse();

            var before = cell.Background;
            var at = CenterOf(cell, window);
            window.MouseDown(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            cell.Classes.Contains(":pressed").ShouldBeFalse();
            cell.Background.ShouldBe(before);
            window.MouseUp(at, MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ListCell_As_The_Last_Row_Hides_Its_Separator()
    {
        var cell = new ListCell { Content = "Footer content" };
        var group = new ListGroup { Items = { new ListItem { Content = "Name" }, cell } };
        var window = Show(new Page { Content = group });
        try
        {
            SeparatorShown((Control)group.ContainerFromIndex(0)!).ShouldBeTrue();
            SeparatorShown(cell).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ListGroup_Uses_A_ListCell_Template_Root_As_The_Row()
    {
        var group = new ListGroup
        {
            ItemsSource = new[] { "Ada", "Grace" },
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((_, _) => new ListCell
            {
                Content = new TextBlock { [!TextBlock.TextProperty] = new Avalonia.Data.Binding(".") },
            }),
        };
        var window = Show(new Page { Content = group });
        try
        {
            var cells = group.GetVisualDescendants().OfType<ListCell>().ToList();
            cells.Count.ShouldBe(2);
            group.GetVisualDescendants().OfType<ListItem>().ShouldBeEmpty();
            group.ContainerFromIndex(0).ShouldBeSameAs(cells[0]);
            cells.Select(c => c.GetVisualDescendants().OfType<TextBlock>().First().Text).ShouldBe(["Ada", "Grace"]);
            SeparatorShown(cells[0]).ShouldBeTrue();
            SeparatorShown(cells[1]).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- ListItem.Leading -----------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Leading_Takes_The_First_Column_And_The_Subtitle_Stays_Beside_It()
    {
        var avatar = new Border { Width = 40, Height = 40 };
        var item = new ListItem { Content = "Ada Lovelace", Subtitle = "ada@example.com", Leading = avatar, Icon = ArcIcons.User };
        var plain = new ListItem { Content = "Plain" };
        var window = Show(new Page { Content = new ListGroup { Items = { item, plain } } });
        try
        {
            item.Classes.Contains(":leading").ShouldBeTrue();
            Named<ArcGlyph>(item, "PART_Icon").IsVisible.ShouldBeFalse();
            var leading = Named<ContentPresenter>(item, "PART_Leading");
            Grid.GetColumn(leading).ShouldBe(0);
            avatar.GetVisualAncestors().ShouldContain(leading);

            var subtitle = Named<TextBlock>(item, "PART_Subtitle");
            var avatarBox = new Rect(avatar.TranslatePoint(default, item)!.Value, avatar.Bounds.Size);
            var subtitleBox = new Rect(subtitle.TranslatePoint(default, item)!.Value, subtitle.Bounds.Size);
            subtitleBox.X.ShouldBeGreaterThanOrEqualTo(avatarBox.Right + 12 - 0.5);
            subtitleBox.Top.ShouldBeLessThan(avatarBox.Bottom);
            item.Bounds.Height.ShouldBe(60, 0.5);

            // Rows without leading content carry no presenter for it.
            plain.GetVisualDescendants().OfType<ContentPresenter>().ShouldNotContain(p => p.Name == "PART_Leading");

            item.Leading = null;
            Dispatcher.UIThread.RunJobs();
            item.Classes.Contains(":leading").ShouldBeFalse();
            Named<ArcGlyph>(item, "PART_Icon").IsVisible.ShouldBeTrue();
            leading.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Avatar_Size_Tokens_Resolve_For_A_Row()
    {
        var item = new ListItem { Content = "Me" };
        var window = Show(new Page { Content = new ListGroup { Items = { item } } });
        try
        {
            double Token(ListItemTokenKind kind) => (double)item.FindResource(ListItemTokens.Key(kind))!;
            Token(ListItemTokenKind.AvatarSizeSM).ShouldBe(32);
            Token(ListItemTokenKind.AvatarSizeMD).ShouldBe(40);
            Token(ListItemTokenKind.AvatarSizeLG).ShouldBe(48);
            Token(ListItemTokenKind.AvatarSizeXL).ShouldBe(64);
            Token(ListItemTokenKind.AvatarSize2XL).ShouldBe(88);
            item.FindResource(ListItemTokens.Key(ListItemTokenKind.LeadingMargin)).ShouldBe(new Thickness(0, 0, 12, 0));
        }
        finally
        {
            window.Close();
        }
    }

    // ---- ActionSheetItem.IsChecked --------------------------------------------------------------------------------

    [AvaloniaFact]
    public void ActionSheetItem_IsChecked_Shows_A_Check_Mark_And_Keeps_The_Label_Centered()
    {
        var plain = new ActionSheetItem { Content = "Small" };
        var checkedRow = new ActionSheetItem { Content = "Medium", IsChecked = true };
        var sheet = new ActionSheet { Actions = { plain, checkedRow }, IsOpen = true };
        var window = Show(new Page { Overlay = sheet });
        try
        {
            Named<ArcGlyph>(plain, "PART_Check").IsVisible.ShouldBeFalse();
            plain.Classes.Contains(":checked").ShouldBeFalse();
            var check = Named<ArcGlyph>(checkedRow, "PART_Check");
            check.IsVisible.ShouldBeTrue();
            checkedRow.Classes.Contains(":checked").ShouldBeTrue();
            // The mark sits at the row's end; the label stays centered and clear of it.
            var row = checkedRow.Bounds;
            var checkBox = new Rect(check.TranslatePoint(default, checkedRow)!.Value, check.Bounds.Size);
            checkBox.Right.ShouldBeGreaterThan(row.Width - 24);
            var label = Named<ContentPresenter>(checkedRow, "PART_ContentPresenter");
            var labelBox = new Rect(label.TranslatePoint(default, checkedRow)!.Value, label.Bounds.Size);
            labelBox.Center.X.ShouldBe(row.Width / 2, 1);
            labelBox.Right.ShouldBeLessThanOrEqualTo(checkBox.Left);

            checkedRow.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            check.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Picker ---------------------------------------------------------------------------------------------------

    private static Picker NewPicker(out List<PickerSelectionChangedEventArgs> changes, out List<PickerItemInvokedEventArgs> invoked)
    {
        var picker = new Picker { Content = "Size", Items = { "Small", "Medium", "Large" } };
        var changeLog = new List<PickerSelectionChangedEventArgs>();
        var invokeLog = new List<PickerItemInvokedEventArgs>();
        picker.SelectionChanged += (_, e) => changeLog.Add(e);
        picker.ItemInvoked += (_, e) => invokeLog.Add(e);
        changes = changeLog;
        invoked = invokeLog;
        return picker;
    }

    private static string ValueText(Picker picker)
        => Named<ContentPresenter>(picker, Picker.ValuePart).GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty;

    [AvaloniaFact]
    public void Picker_Shows_The_Placeholder_Then_The_Selected_Value_And_Index_And_Item_Follow_Each_Other()
    {
        var picker = NewPicker(out var changes, out _);
        var window = Show(new Page { Content = new ListGroup { Items = { picker } } });
        try
        {
            picker.ShowChevron.ShouldBeTrue();
            picker.Classes.Contains(":placeholder").ShouldBeTrue();
            ValueText(picker).ShouldBe("Not set");

            picker.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            picker.SelectedItem.ShouldBe("Medium");
            picker.Classes.Contains(":placeholder").ShouldBeFalse();
            ValueText(picker).ShouldBe("Medium");

            picker.SelectedItem = "Large";
            picker.SelectedIndex.ShouldBe(2);

            picker.SelectedItem = null;
            picker.SelectedIndex.ShouldBe(-1);
            ValueText(picker).ShouldBe("Not set");

            changes.Select(c => (c.OldItem, c.NewItem, c.OldIndex, c.NewIndex)).ShouldBe(
                [(null, "Medium", -1, 1), ("Medium", "Large", 1, 2), ("Large", null, 2, -1)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Tap_Opens_An_ActionSheet_Over_The_Page_With_The_Current_Choice_Checked()
    {
        var picker = NewPicker(out _, out _);
        picker.SelectedIndex = 2;
        var page = new Page { Content = new ListGroup { Items = { picker } }, UsePlatformSafeArea = false, SafeArea = new Thickness(0, 47, 0, 34) };
        var window = Show(page);
        try
        {
            Tap(window, picker);
            picker.IsOpen.ShouldBeTrue();
            var sheet = picker.ChoiceSheet.ShouldNotBeNull();
            sheet.IsOpen.ShouldBeTrue();
            sheet.GetVisualAncestors().ShouldContain(page);
            sheet.GetVisualAncestors().ShouldNotContain(picker);
            sheet.Title.ShouldBe("Size");
            sheet.Actions.Select(a => a.Content).ShouldBe(["Small", "Medium", "Large"]);
            sheet.Actions.Select(a => a.IsChecked).ShouldBe([false, false, true]);
            // The sheet covers the whole page and keeps the page's bottom safe area free on its own.
            sheet.Bounds.Height.ShouldBe(page.Bounds.Height, 0.5);
            sheet.EffectiveSafeAreaBottom.ShouldBe(34);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Choosing_A_Row_Selects_It_Raises_The_Events_And_Closes()
    {
        var picker = NewPicker(out var changes, out var invoked);
        var window = Show(new Page { Content = new ListGroup { Items = { picker } } });
        try
        {
            Tap(window, picker);
            var sheet = picker.ChoiceSheet!;
            Tap(window, sheet.Actions[1]);
            picker.SelectedItem.ShouldBe("Medium");
            picker.SelectedIndex.ShouldBe(1);
            picker.IsOpen.ShouldBeFalse();
            sheet.IsOpen.ShouldBeFalse();
            changes.Count.ShouldBe(1);
            changes[0].NewItem.ShouldBe("Medium");
            invoked.Select(i => (i.Item, i.Index)).ShouldBe([("Medium", 1)]);
            ValueText(picker).ShouldBe("Medium");

            // Choosing the selected row again is a choice, not a change.
            Tap(window, picker);
            sheet.Actions[1].IsChecked.ShouldBeTrue();
            Tap(window, sheet.Actions[1]);
            changes.Count.ShouldBe(1);
            invoked.Count.ShouldBe(2);

            // Cancel closes without choosing.
            Tap(window, picker);
            Tap(window, Named<ActionSheetItem>(sheet, "PART_Cancel"));
            picker.IsOpen.ShouldBeFalse();
            invoked.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Index_Set_Before_The_Choices_Waits_For_Them_And_The_Item_Keeps_Its_Place()
    {
        var sizes = new ObservableCollection<string> { "Small", "Medium", "Large" };
        var picker = new Picker { Content = "Size", SelectedIndex = 1 };
        var changes = new List<PickerSelectionChangedEventArgs>();
        picker.SelectionChanged += (_, e) => changes.Add(e);
        var window = Show(new Page { Content = new ListGroup { Items = { picker } } });
        try
        {
            picker.SelectedIndex.ShouldBe(1);
            picker.SelectedItem.ShouldBeNull();
            changes.ShouldBeEmpty();

            picker.ItemsSource = sizes;
            picker.SelectedItem.ShouldBe("Medium");
            changes.Count.ShouldBe(1);

            sizes.Insert(0, "Tiny");
            picker.SelectedItem.ShouldBe("Medium");
            picker.SelectedIndex.ShouldBe(2);
            changes.Count.ShouldBe(1);

            sizes.Remove("Medium");
            picker.SelectedItem.ShouldBe("Medium");
            picker.SelectedIndex.ShouldBe(-1);
            ValueText(picker).ShouldBe("Medium");
        }
        finally
        {
            window.Close();
        }
    }

    private sealed record Plan(string Name, int Price);

    [AvaloniaFact]
    public void Picker_DisplayMemberBinding_Gives_The_Text_Of_The_Value_And_The_Rows()
    {
        var plans = new[] { new Plan("Free", 0), new Plan("Pro", 12) };
        var picker = new Picker
        {
            Content = "Plan",
            ItemsSource = plans,
            SelectedIndex = 1,
            DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Plan.Name)),
            SheetTitle = "Choose a plan",
            CancelText = null,
        };
        var window = Show(new Page { Content = new ListGroup { Items = { picker } } });
        try
        {
            ValueText(picker).ShouldBe("Pro");
            Tap(window, picker);
            var sheet = picker.ChoiceSheet!;
            sheet.Title.ShouldBe("Choose a plan");
            sheet.CancelText.ShouldBeNull();
            sheet.Actions.Select(a => a.Content).ShouldBe(["Free", "Pro"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Disabled_Or_Without_Choices_Does_Not_Open_And_Disabling_Closes_It()
    {
        var empty = new Picker { Content = "Nothing" };
        var disabled = NewPicker(out _, out _);
        disabled.IsEnabled = false;
        var picker = NewPicker(out _, out _);
        var window = Show(new Page { Content = new ListGroup { Items = { empty, disabled, picker } } });
        try
        {
            Tap(window, empty);
            empty.IsOpen.ShouldBeFalse();
            Tap(window, disabled);
            disabled.IsOpen.ShouldBeFalse();
            disabled.IsOpen = true;
            disabled.IsOpen.ShouldBeFalse();

            Tap(window, picker);
            picker.IsOpen.ShouldBeTrue();
            picker.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            picker.IsOpen.ShouldBeFalse();
            picker.ChoiceSheet!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Opens_From_The_Keyboard_Focuses_The_Checked_Row_And_Gives_The_Focus_Back()
    {
        var picker = NewPicker(out _, out _);
        picker.SelectedIndex = 1;
        var window = Show(new Page { Content = new ListGroup { Items = { picker } } });
        try
        {
            picker.Focus(NavigationMethod.Tab);
            picker.Classes.Contains(":focus-visible").ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsOpen.ShouldBeTrue();
            var sheet = picker.ChoiceSheet!;
            sheet.Actions[1].IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsOpen.ShouldBeFalse();
            picker.IsFocused.ShouldBeTrue();
            picker.Classes.Contains(":focus-visible").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Outside_A_Page_Opens_On_The_Window_Overlay_And_Leaves_With_The_Picker()
    {
        var picker = NewPicker(out _, out _);
        var host = new StackPanel { Children = { picker } };
        var window = Show(host);
        try
        {
            Tap(window, picker);
            var sheet = picker.ChoiceSheet!;
            sheet.IsOpen.ShouldBeTrue();
            sheet.GetVisualAncestors().ShouldNotContain(picker);
            sheet.Bounds.Width.ShouldBe(window.Bounds.Width, 0.5);

            host.Children.Remove(picker);
            Dispatcher.UIThread.RunJobs();
            sheet.GetVisualParent().ShouldBeNull();
            picker.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Sheet safe area ------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Sheet_In_The_Overlay_Keeps_The_Page_Safe_Area_Free_Unless_SafeAreaBottom_Is_Set()
    {
        var overlay = new Sheet { Content = new Border { Height = 100 }, IsOpen = true };
        var actions = new ActionSheet { Actions = { new ActionSheetItem { Content = "Share" } }, IsOpen = true };
        var inContent = new Sheet { Content = new Border { Height = 100 } };
        var page = new Page
        {
            UsePlatformSafeArea = false,
            SafeArea = new Thickness(0, 47, 0, 34),
            Content = inContent,
            Overlay = new Grid { Children = { overlay, actions } },
        };
        var window = Show(page);
        try
        {
            Page.GetInheritedSafeArea(page).ShouldBe(new Thickness(0, 47, 0, 34));
            overlay.EffectiveSafeAreaBottom.ShouldBe(34);
            Named<Border>(overlay, "PART_SafeArea").Bounds.Height.ShouldBe(34);
            actions.EffectiveSafeAreaBottom.ShouldBe(34);
            Named<Border>(actions, "PART_SafeArea").Bounds.Height.ShouldBe(34);
            // The content is already inside the safe area.
            Page.GetInheritedSafeArea(inContent).ShouldBe(default);
            inContent.EffectiveSafeAreaBottom.ShouldBe(0);

            // The page's safe area changes (rotation): the sheet follows.
            page.SafeArea = new Thickness(44, 0, 44, 21);
            overlay.EffectiveSafeAreaBottom.ShouldBe(21);

            // An explicit value wins, even 0; clearing it returns to the page's.
            overlay.SafeAreaBottom = 0;
            overlay.EffectiveSafeAreaBottom.ShouldBe(0);
            overlay.SafeAreaBottom = 50;
            overlay.EffectiveSafeAreaBottom.ShouldBe(50);
            overlay.ClearValue(Sheet.SafeAreaBottomProperty);
            overlay.EffectiveSafeAreaBottom.ShouldBe(21);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- RadioCards -----------------------------------------------------------------------------------------------

    private static RadioCards NewCards(out List<SelectionChangedEventArgs> changes)
    {
        var cards = new RadioCards
        {
            Items =
            {
                new RadioCardOption("circle", "Circle", "Round corners") { Icon = ArcIcons.Circle },
                new RadioCardOption("square", "Square"),
                new RadioCardOption("triangle", "Triangle") { IsEnabled = false },
                new RadioCardOption("star", "Star"),
                new RadioCardOption("heart", "Heart"),
            },
        };
        var log = new List<SelectionChangedEventArgs>();
        cards.SelectionChanged += (_, e) => log.Add(e);
        changes = log;
        return cards;
    }

    private static RadioCard Card(RadioCards cards, int index) => (RadioCard)cards.ContainerFromIndex(index)!;

    [AvaloniaFact]
    public void RadioCards_Share_The_Width_In_Columns_And_Rows_Take_The_Tallest_Card()
    {
        var cards = NewCards(out _);
        var window = Show(new StackPanel { Margin = new Thickness(16), Children = { cards } }, width: 400);
        try
        {
            var a = Card(cards, 0).Bounds;
            var b = Card(cards, 1).Bounds;
            var c = Card(cards, 2).Bounds;
            a.Width.ShouldBe((cards.Bounds.Width - 10) / 2, 0.5);
            b.X.ShouldBe(a.Right + 10, 0.5);
            b.Height.ShouldBe(a.Height, 0.5);
            c.Y.ShouldBe(a.Bottom + 10, 0.5);
            c.X.ShouldBe(0, 0.5);
            Card(cards, 0).Icon.ShouldBe(ArcIcons.Circle);
            Named<ContentPresenter>(Card(cards, 0), RadioCard.IconPart).Content.ShouldBeOfType<ArcGlyph>();
            Card(cards, 0).Description.ShouldBe("Round corners");
            Card(cards, 2).IsEnabled.ShouldBeFalse();

            cards.Columns = 3;
            Dispatcher.UIThread.RunJobs();
            Card(cards, 3).Bounds.Y.ShouldBeGreaterThan(Card(cards, 0).Bounds.Bottom);
            Card(cards, 2).Bounds.Y.ShouldBe(Card(cards, 0).Bounds.Y, 0.5);

            // A hidden card takes no room: the next one moves into its place.
            ((RadioCardOption)cards.Items[1]!).IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Card(cards, 1).IsVisible.ShouldBeFalse();
            Card(cards, 2).Bounds.X.ShouldBe(Card(cards, 0).Bounds.Right + 10, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RadioCards_A_Tap_Selects_And_Reports_The_Value_And_A_Disabled_Card_Cannot_Be_Chosen()
    {
        var cards = NewCards(out var changes);
        var window = Show(new StackPanel { Children = { cards } });
        try
        {
            Tap(window, Card(cards, 1));
            cards.SelectedIndex.ShouldBe(1);
            cards.SelectedValue.ShouldBe("square");
            Card(cards, 1).IsSelected.ShouldBeTrue();
            Card(cards, 1).Classes.Contains(":selected").ShouldBeTrue();
            changes.Count.ShouldBe(1);

            Tap(window, Card(cards, 2));
            cards.SelectedIndex.ShouldBe(1);

            cards.SelectedValue = "heart";
            cards.SelectedIndex.ShouldBe(4);
            Card(cards, 1).IsSelected.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RadioCards_Press_Shades_The_Card_Without_Hover_And_Does_Not_Scale_Without_Full_Motion()
    {
        var cards = NewCards(out _);
        var window = Show(new StackPanel { Children = { cards } });
        try
        {
            var card = Card(cards, 0);
            var normal = ((Avalonia.Media.ISolidColorBrush)card.Background!).Color;
            var at = CenterOf(card, window);
            window.MouseMove(at);
            card.Classes.Contains(":pressed").ShouldBeFalse();
            ((Avalonia.Media.ISolidColorBrush)card.Background!).Color.ShouldBe(normal);
            window.MouseDown(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            card.Classes.Contains(":pressed").ShouldBeTrue();
            ((Avalonia.Media.ISolidColorBrush)card.Background!).Color.ShouldNotBe(normal);
            card.RenderTransform!.Value.M11.ShouldBe(1);
            window.MouseUp(at, MouseButton.Left);
            card.Classes.Contains(":pressed").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RadioCards_Arrow_Keys_Move_Through_The_Grid_And_Skip_Disabled_And_Hidden_Cards()
    {
        var cards = NewCards(out _);
        ((RadioCardOption)cards.Items[3]!).IsVisible = false;
        // Shown in a 2-column grid: circle, square / triangle (disabled), heart.
        var window = Show(new StackPanel { Children = { cards } });
        try
        {
            cards.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Card(cards, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(1);
            Card(cards, 1).IsFocused.ShouldBeTrue();
            // Down from square: heart (the hidden star takes no place in the grid).
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(4);
            // Left from heart skips the disabled triangle and lands on square.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(1);
            // Down from circle reaches the disabled triangle only, so it stays.
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(4);
            // Right wraps to the first card.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            cards.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RadioCards_Ring_Covers_The_Selected_Card_And_Hides_With_It()
    {
        var cards = NewCards(out _);
        var window = Show(new StackPanel { Children = { cards } });
        try
        {
            cards.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            var ring = cards.Ring.ShouldNotBeNull();
            var card = Card(cards, 1);
            var expected = new Rect(card.TranslatePoint(default, ring)!.Value, card.Bounds.Size).Deflate(1);
            ring.CurrentRect.ShouldBe(expected);
            ring.CurrentOpacity.ShouldBeGreaterThan(0.99);

            ((RadioCardOption)cards.Items[1]!).IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            cards.SelectedIndex.ShouldBe(1);
            ring.CurrentOpacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RadioCards_Cards_Follow_Later_Changes_Of_Their_Options()
    {
        var cards = NewCards(out _);
        var window = Show(new StackPanel { Children = { cards } });
        try
        {
            var option = (RadioCardOption)cards.Items[2]!;
            option.Label = "Triangle (soon)";
            option.Description = "Coming next month";
            option.IsEnabled = true;
            Card(cards, 2).Content.ShouldBe("Triangle (soon)");
            Card(cards, 2).Description.ShouldBe("Coming next month");
            Card(cards, 2).IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
