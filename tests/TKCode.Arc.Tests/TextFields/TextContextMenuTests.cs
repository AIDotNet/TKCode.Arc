using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.TextFields;

public class TextContextMenuTests
{
    private static async Task SetClipboardAsync(Control owner, string? text)
    {
        var clipboard = TopLevel.GetTopLevel(owner)!.Clipboard!;
        if (text is null)
        {
            await clipboard.ClearAsync();
        }
        else
        {
            await clipboard.SetTextAsync(text);
        }
    }

    // The menu opens after an async clipboard probe; pump until it is open (or give up).
    private static MenuSurface? Open(FieldTextBox box)
    {
        box.RaiseEvent(new ContextRequestedEventArgs());
        for (var i = 0; i < 100 && TextContextMenu.SurfaceOf(box)?.IsOpen != true; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        return TextContextMenu.SurfaceOf(box) is { IsOpen: true } surface ? surface : null;
    }

    private static List<MenuEntry> Entries(MenuSurface surface) => surface.Items.OfType<MenuEntry>().ToList();

    private static MenuEntry? Entry(MenuSurface surface, string tag) => Entries(surface).FirstOrDefault(e => (e.Tag as string) == tag);

    [AvaloniaFact]
    public void Input_Opens_Default_Menu_With_Cut_Copy_Paste_SelectAll()
    {
        var box = new Input { Text = "hello", Width = 200 };
        var window = TestHelpers.Show(box);
        try
        {
            SetClipboardAsync(box, "clip").GetAwaiter().GetResult();
            var surface = Open(box);
            surface.ShouldNotBeNull();
            var tags = Entries(surface).Select(e => (string)e.Tag!).ToList();
            tags.ShouldContain("cut");
            tags.ShouldContain("copy");
            tags.ShouldContain("paste");
            tags.ShouldContain("select-all");
            Entry(surface, "paste")!.IsEnabled.ShouldBeTrue();
            Entry(surface, "select-all")!.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cut_And_Copy_Need_A_Selection()
    {
        var box = new Input { Text = "hello", Width = 200 };
        var window = TestHelpers.Show(box);
        try
        {
            var surface = Open(box)!;
            Entry(surface, "cut")!.IsEnabled.ShouldBeFalse();
            Entry(surface, "copy")!.IsEnabled.ShouldBeFalse();
            surface.Close(returnFocus: false);
            box.SelectAll();
            surface = Open(box)!;
            Entry(surface, "cut")!.IsEnabled.ShouldBeTrue();
            Entry(surface, "copy")!.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Is_Disabled_With_An_Empty_Clipboard()
    {
        var box = new Input { Text = "hello", Width = 200 };
        var window = TestHelpers.Show(box);
        try
        {
            SetClipboardAsync(box, null).GetAwaiter().GetResult();
            Entry(Open(box)!, "paste")!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PasswordField_Never_Allows_Cut_Or_Copy()
    {
        var box = new PasswordField { Text = "secret", Width = 200 };
        var window = TestHelpers.Show(box);
        try
        {
            box.SelectAll();
            var surface = Open(box)!;
            Entry(surface, "cut")!.IsEnabled.ShouldBeFalse();
            Entry(surface, "copy")!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReadOnly_Has_No_Cut_Or_Paste()
    {
        var box = new Input { Text = "hello", IsReadOnly = true, Width = 200 };
        var window = TestHelpers.Show(box);
        try
        {
            box.SelectAll();
            var surface = Open(box)!;
            Entry(surface, "paste").ShouldBeNull();
            Entry(surface, "cut").ShouldBeNull();
            Entry(surface, "copy")!.IsEnabled.ShouldBeTrue();
            Entry(surface, "select-all").ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_ContextMenu_Or_Flyout_Suppresses_The_Default()
    {
        var box = new Textarea { Text = "hello", Width = 200, ContextFlyout = new Flyout() };
        var window = TestHelpers.Show(box);
        try
        {
            box.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            TextContextMenu.SurfaceOf(box).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var attached = new Input { Text = "hello", Width = 200 };
        var custom = new MenuSurface();
        custom.Items.Add(new MenuEntry { Header = "Mine" });
        ContextMenu.SetMenu(attached, custom);
        window = TestHelpers.Show(attached);
        try
        {
            attached.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            custom.IsOpen.ShouldBeTrue();
            TextContextMenu.SurfaceOf(attached).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsDefaultContextMenuEnabled_False_Suppresses_The_Default_And_Inherits()
    {
        var box = new Input { Text = "hello", Width = 200, IsDefaultContextMenuEnabled = false };
        var window = TestHelpers.Show(box);
        try
        {
            box.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            TextContextMenu.SurfaceOf(box).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var inner = new SearchField { Text = "x", Width = 200 };
        var host = new StackPanel { Children = { inner } };
        TextContextMenu.SetIsEnabled(host, false);
        window = TestHelpers.Show(host);
        try
        {
            inner.IsDefaultContextMenuEnabled.ShouldBeFalse();
            inner.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            TextContextMenu.SurfaceOf(inner).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Follow_The_Language()
    {
        var box = new Input { Text = "hello", Width = 200 };
        var window = TestHelpers.Show(box);
        try
        {
            ArcLanguage.Set(LanguageTags.ZhCN);
            var surface = Open(box)!;
            Entry(surface, "cut")!.Header.ShouldBe("剪切");
            Entry(surface, "copy")!.Header.ShouldBe("复制");
            Entry(surface, "paste")!.Header.ShouldBe("粘贴");
            Entry(surface, "select-all")!.Header.ShouldBe("全选");
            TextContextMenuLangResources.Get(TextContextMenuLangResourceKind.Undo).ShouldBe("撤销");
            ArcLanguage.Set(LanguageTags.ZhTW);
            TextContextMenuLangResources.Get(TextContextMenuLangResourceKind.Cut).ShouldBe("剪下");
            TextContextMenuLangResources.Get(TextContextMenuLangResourceKind.Redo).ShouldBe("取消復原");
        }
        finally
        {
            ArcLanguage.Set(LanguageTags.EnUS);
            window.Close();
        }
    }
}
