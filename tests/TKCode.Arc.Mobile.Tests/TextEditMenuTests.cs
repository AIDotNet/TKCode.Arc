using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>The long-press edit menu of <see cref="TextField"/> (issue #137).</summary>
public class TextEditMenuTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static TextEditMenu MenuOf(TextBox box) => box.ContextFlyout.ShouldBeOfType<TextEditMenu>();

    private static string[] ShownActions(TextEditMenu menu) =>
        menu.Presenter.ShouldNotBeNull().Items.Where(i => i.IsVisible).Select(i => i.Content as string ?? "").ToArray();

    /// <summary>A right click on the text, which raises ContextRequested with a position as a long press does.</summary>
    private static void PressForMenu(Window window, TextBox box)
    {
        var point = box.TranslatePoint(new Point(40, box.Bounds.Height / 2), window).ShouldNotBeNull();
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TextField_And_Searchbar_Have_The_Edit_Menu()
    {
        var field = new TextField();
        var bar = new Searchbar();
        var window = Show(new StackPanel { Children = { field, bar } });
        try
        {
            MenuOf(field).ShouldNotBeNull();
            MenuOf(bar).ShouldBeSameAs(MenuOf(field)); // one instance serves every field
            MenuOf(field).ShowMode.ShouldBe(FlyoutShowMode.Transient);
            MenuOf(field).OverlayDismissEventPassThrough.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Selection_Offers_Cut_Copy_Paste_And_Select_All_Above_The_Field()
    {
        var field = new TextField { Text = "sk-abc123", Margin = new Thickness(0, 200, 0, 0) };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            field.SelectionStart = 0;
            field.SelectionEnd = 2;
            PressForMenu(window, field);

            var menu = MenuOf(field);
            menu.IsOpen.ShouldBeTrue();
            menu.Target.ShouldBe(field);
            ShownActions(menu).ShouldBe(["Cut", "Copy", "Paste", "Select All"]);
            field.IsFocused.ShouldBeTrue(); // the menu does not take focus, so the keyboard stays up

            var presenter = menu.Presenter!;
            presenter.Items[0].Classes.Contains(TextEditMenuItem.LeadingPseudoClass).ShouldBeTrue();
            presenter.Items[1].Classes.Contains(TextEditMenuItem.LeadingPseudoClass).ShouldBeFalse();
            presenter.Items.ShouldAllBe(i => i.Bounds.Height >= 44);

            var top = presenter.TranslatePoint(new Point(0, presenter.Bounds.Height), window).ShouldNotBeNull().Y;
            var fieldTop = field.TranslatePoint(default, window).ShouldNotBeNull().Y;
            top.ShouldBeLessThanOrEqualTo(fieldTop); // above the field, not under the finger
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Password_Field_Never_Offers_Cut_Or_Copy()
    {
        var field = new TextField { Text = "hunter2", PasswordChar = '•' };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            field.SelectionStart = 0;
            field.SelectionEnd = 3;
            PressForMenu(window, field);

            ShownActions(MenuOf(field)).ShouldBe(["Paste", "Select All"]);
            MenuOf(field).Presenter!.Items[2].Classes.Contains(TextEditMenuItem.LeadingPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Field_Offers_Paste()
    {
        var field = new TextField(); // Text is null: nobody has typed in it yet
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Text.ShouldBeNull();
            field.Focus();
            PressForMenu(window, field);

            MenuOf(field).IsOpen.ShouldBeTrue();
            ShownActions(MenuOf(field)).ShouldBe(["Paste"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_And_Fully_Selected_Fields_Show_Only_What_Applies()
    {
        var field = new TextField { Text = "abc", IsReadOnly = true };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            field.SelectAll();
            PressForMenu(window, field);
            ShownActions(MenuOf(field)).ShouldBe(["Copy"]);
            MenuOf(field).Hide();

            // Nothing applies to an empty read-only field, so the menu stays closed.
            field.Text = "";
            PressForMenu(window, field);
            MenuOf(field).IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Select_All_Selects_The_Text_And_Brings_The_Menu_Back()
    {
        var field = new TextField { Text = "sk-abc123" };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            field.CaretIndex = 3;
            PressForMenu(window, field);
            var menu = MenuOf(field);
            ShownActions(menu).ShouldBe(["Paste", "Select All"]);

            menu.Invoke(TextEditMenuAction.SelectAll);
            Dispatcher.UIThread.RunJobs();

            field.SelectionStart.ShouldBe(0);
            field.SelectionEnd.ShouldBe(9);
            menu.IsOpen.ShouldBeTrue();
            ShownActions(menu).ShouldBe(["Cut", "Copy", "Paste"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cut_Removes_The_Selection_And_Closes_The_Menu()
    {
        var field = new TextField { Text = "sk-abc123" };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            field.SelectionStart = 0;
            field.SelectionEnd = 3;
            PressForMenu(window, field);
            var menu = MenuOf(field);
            menu.IsOpen.ShouldBeTrue();

            var cut = menu.Presenter!.Items.Single(i => i.Action == TextEditMenuAction.Cut);
            cut.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            menu.IsOpen.ShouldBeFalse();
            field.Text.ShouldBe("abc123");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Or_Losing_Focus_Closes_The_Menu()
    {
        var field = new TextField { Text = "abc" };
        var other = new TextField();
        var window = Show(new StackPanel { Children = { field, other } });
        try
        {
            field.Focus();
            PressForMenu(window, field);
            MenuOf(field).IsOpen.ShouldBeTrue();
            field.Text = "abcd";
            MenuOf(field).IsOpen.ShouldBeFalse();

            PressForMenu(window, field);
            MenuOf(field).IsOpen.ShouldBeTrue();
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            MenuOf(field).IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Request_Without_A_Position_Opens_With_Avalonias_Placement()
    {
        var field = new TextField { Text = "abc" };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            field.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            MenuOf(field).IsOpen.ShouldBeTrue();
            MenuOf(field).Placement.ShouldBe(PlacementMode.TopEdgeAlignedLeft);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Local_Context_Menu_Or_Flyout_Replaces_The_Edit_Menu()
    {
        var own = new Flyout { Content = new TextBlock { Text = "Mine" } };
        var field = new TextField { Text = "abc", ContextFlyout = own };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            PressForMenu(window, field);
            own.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class FakePlatform(bool shows) : ITextEditMenuPlatform
    {
        public List<(TextBox Box, Rect Anchor)> Shown { get; } = [];
        public List<TextBox> Hidden { get; } = [];

        public bool TryShow(TextBox box, Rect anchor)
        {
            Shown.Add((box, anchor));
            return shows;
        }

        public void Hide(TextBox box) => Hidden.Add(box);
    }

    [AvaloniaFact]
    public void A_Platform_Menu_Shows_In_Place_Of_The_Arc_Surface()
    {
        var platform = new FakePlatform(shows: true);
        TextEditMenu.Platform = platform;
        var field = new TextField { Text = "sk-abc123" };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            PressForMenu(window, field);
            MenuOf(field).IsOpen.ShouldBeFalse(); // the Arc surface stays closed
            platform.Shown.Count.ShouldBe(1);
            platform.Shown[0].Box.ShouldBe(field);
            platform.Shown[0].Anchor.X.ShouldBe(40, 1); // at the finger
            platform.Shown[0].Anchor.Y.ShouldBe(0);
            platform.Shown[0].Anchor.Height.ShouldBe(field.Bounds.Height);

            // From the selection handles (no position): the anchor is the selection, inside the field.
            field.SelectionStart = 0;
            field.SelectionEnd = 3;
            field.RaiseEvent(new ContextRequestedEventArgs());
            platform.Shown.Count.ShouldBe(2);
            var anchor = platform.Shown[1].Anchor;
            anchor.Width.ShouldBeGreaterThan(0);
            anchor.X.ShouldBeGreaterThanOrEqualTo(0);
            anchor.Right.ShouldBeLessThanOrEqualTo(field.Bounds.Width);

            field.Text = "changed";
            platform.Hidden.ShouldContain(field);
        }
        finally
        {
            TextEditMenu.Platform = null;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arc_Surface_Shows_When_The_Platform_Declines()
    {
        var platform = new FakePlatform(shows: false);
        TextEditMenu.Platform = platform;
        var field = new TextField { Text = "abc" };
        var window = Show(new StackPanel { Children = { field } });
        try
        {
            field.Focus();
            PressForMenu(window, field);
            platform.Shown.Count.ShouldBe(1);
            MenuOf(field).IsOpen.ShouldBeTrue();
        }
        finally
        {
            TextEditMenu.Platform = null;
            window.Close();
        }
    }
}
