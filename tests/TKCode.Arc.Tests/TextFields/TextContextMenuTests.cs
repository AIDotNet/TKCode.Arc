using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

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

    private static string? GetClipboardText(Control owner)
    {
        var clipboard = TopLevel.GetTopLevel(owner)!.Clipboard!;
        return clipboard.TryGetTextAsync().GetAwaiter().GetResult();
    }

    private static MenuSurface? OpenSelectable(SelectableTextBlock block)
    {
        block.RaiseEvent(new ContextRequestedEventArgs());
        return TextContextMenu.SurfaceOf(block) is { IsOpen: true } surface ? surface : null;
    }

    private static MenuSurface? RightClickSelectable(Window window, SelectableTextBlock block, double x = 10)
    {
        var point = block.TranslatePoint(new Point(x, block.Bounds.Height / 2), window).ShouldNotBeNull();
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
        return TextContextMenu.SurfaceOf(block) is { IsOpen: true } surface ? surface : null;
    }

    [AvaloniaFact]
    public void SelectableTextBlock_Opens_Default_Menu_Synchronously_With_Copy_And_SelectAll()
    {
        var block = new SelectableTextBlock { Text = "hello world", Width = 200 };
        var window = TestHelpers.Show(block);
        try
        {
            var args = new ContextRequestedEventArgs();
            block.RaiseEvent(args);
            args.Handled.ShouldBeTrue();
            var surface = TextContextMenu.SurfaceOf(block);
            surface.ShouldNotBeNull();
            surface.IsOpen.ShouldBeTrue();
            surface.Variant.ShouldBe(MenuSurfaceVariant.Context);
            surface.ClosesOnViewportChange.ShouldBeTrue();
            surface.FocusReturnTarget.ShouldBeSameAs(block);
            AutomationProperties.GetName(surface).ShouldBe(TextContextMenuLangResources.Get(TextContextMenuLangResourceKind.TextMenu));

            var tags = Entries(surface).Select(e => (string)e.Tag!).ToList();
            tags.ShouldBe(["copy", "select-all"]);
            Entry(surface, "copy")!.IsEnabled.ShouldBeFalse();
            Entry(surface, "select-all")!.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectableTextBlock_Copy_Preserves_Selection_And_Writes_To_Clipboard()
    {
        var block = new SelectableTextBlock
        {
            Text = "hello world",
            Width = 200,
            Background = Avalonia.Media.Brushes.Transparent,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var other = new Button { Content = "Other" };
        var host = new StackPanel { Children = { block, other } };
        var window = TestHelpers.Show(host);
        try
        {
            SetClipboardAsync(block, null).GetAwaiter().GetResult();
            block.Focus();
            block.SelectionStart = 0;
            block.SelectionEnd = 5;
            Dispatcher.UIThread.RunJobs();

            // Pointer right-click inside selection preserves selection and leaves focus in the block.
            var surface = RightClickSelectable(window, block, x: 10);
            surface.ShouldNotBeNull();
            block.SelectedText.ShouldBe("hello");
            var copy = Entry(surface, "copy")!;
            copy.IsEnabled.ShouldBeTrue();

            // Simulate focus moving to the clicked menu entry (as happens on pointer press) before activation.
            copy.Focus();
            Dispatcher.UIThread.RunJobs();
            block.SelectedText.ShouldBe("hello");
            copy.PerformClick();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => GetClipboardText(block) == "hello").ShouldBeTrue();

            // Keyboard open (no position -> MenuOpenFocus.First) also preserves selection across focus transfer.
            SetClipboardAsync(block, null).GetAwaiter().GetResult();
            block.Focus();
            block.SelectionStart = 6;
            block.SelectionEnd = 11;
            Dispatcher.UIThread.RunJobs();

            surface = OpenSelectable(block);
            surface.ShouldNotBeNull();
            Dispatcher.UIThread.RunJobs();
            block.SelectedText.ShouldBe("world");
            copy = Entry(surface, "copy")!;
            copy.IsEnabled.ShouldBeTrue();
            copy.PerformClick();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => GetClipboardText(block) == "world").ShouldBeTrue();

            // Reversed selection (right-to-left drag) and closing without returning focus when focus moved elsewhere.
            SetClipboardAsync(block, null).GetAwaiter().GetResult();
            block.Focus();
            block.SelectionStart = 5;
            block.SelectionEnd = 0;
            Dispatcher.UIThread.RunJobs();

            surface = OpenSelectable(block)!;
            Dispatcher.UIThread.RunJobs();
            block.SelectedText.ShouldBe("hello");
            surface.Close(returnFocus: true);
            Dispatcher.UIThread.RunJobs();
            block.SelectedText.ShouldBe("hello");

            surface = OpenSelectable(block)!;
            Dispatcher.UIThread.RunJobs();
            other.Focus();
            surface.Close(returnFocus: false);
            Dispatcher.UIThread.RunJobs();
            block.SelectedText.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectableTextBlock_SelectAll_Selects_Entire_Text()
    {
        var block = new SelectableTextBlock { Text = "hello world", Width = 200 };
        var window = TestHelpers.Show(block);
        try
        {
            var surface = OpenSelectable(block)!;
            Entry(surface, "select-all")!.PerformClick();
            Dispatcher.UIThread.RunJobs();
            block.SelectionStart.ShouldBe(0);
            block.SelectionEnd.ShouldBe(11);
            block.SelectedText.ShouldBe("hello world");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectableTextBlock_Inlines_Supports_SelectAll_And_Copy()
    {
        var block = new SelectableTextBlock { Width = 200 };
        block.Inlines!.Add(new Run("hello "));
        block.Inlines.Add(new Run("inlines"));
        var window = TestHelpers.Show(block);
        try
        {
            SetClipboardAsync(block, null).GetAwaiter().GetResult();
            string.IsNullOrEmpty(block.Text).ShouldBeTrue();

            var surface = OpenSelectable(block)!;
            Entry(surface, "copy")!.IsEnabled.ShouldBeFalse();
            Entry(surface, "select-all")!.IsEnabled.ShouldBeTrue();
            Entry(surface, "select-all")!.PerformClick();
            Dispatcher.UIThread.RunJobs();
            block.SelectedText.ShouldBe("hello inlines");

            surface = OpenSelectable(block)!;
            Dispatcher.UIThread.RunJobs();
            Entry(surface, "copy")!.IsEnabled.ShouldBeTrue();
            Entry(surface, "copy")!.PerformClick();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => GetClipboardText(block) == "hello inlines").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectableTextBlock_Empty_Disables_Copy_And_SelectAll()
    {
        var empty = new SelectableTextBlock { Text = "", Width = 200 };
        var window = TestHelpers.Show(empty);
        try
        {
            var surface = OpenSelectable(empty)!;
            Entry(surface, "copy")!.IsEnabled.ShouldBeFalse();
            Entry(surface, "select-all")!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }

        var emptyInlines = new SelectableTextBlock { Width = 200 };
        emptyInlines.Inlines!.Add(new Run(""));
        window = TestHelpers.Show(emptyInlines);
        try
        {
            var surface = OpenSelectable(emptyInlines)!;
            Entry(surface, "copy")!.IsEnabled.ShouldBeFalse();
            Entry(surface, "select-all")!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectableTextBlock_Respects_Handled_OptOut_And_Custom_Menus()
    {
        var block = new SelectableTextBlock { Text = "hello", Width = 200 };
        var window = TestHelpers.Show(block);
        try
        {
            var handled = new ContextRequestedEventArgs { Handled = true };
            block.RaiseEvent(handled);
            TextContextMenu.SurfaceOf(block).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var disabled = new SelectableTextBlock { Text = "hello", Width = 200 };
        TextContextMenu.SetIsEnabled(disabled, false);
        window = TestHelpers.Show(disabled);
        try
        {
            var args = new ContextRequestedEventArgs();
            disabled.RaiseEvent(args);
            args.Handled.ShouldBeFalse();
            TextContextMenu.SurfaceOf(disabled).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var inherited = new SelectableTextBlock { Text = "hello", Width = 200 };
        var host = new StackPanel { Children = { inherited } };
        TextContextMenu.SetIsEnabled(host, false);
        window = TestHelpers.Show(host);
        try
        {
            var args = new ContextRequestedEventArgs();
            inherited.RaiseEvent(args);
            args.Handled.ShouldBeFalse();
            TextContextMenu.SurfaceOf(inherited).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var withFlyout = new SelectableTextBlock { Text = "hello", Width = 200, ContextFlyout = new Flyout() };
        window = TestHelpers.Show(withFlyout);
        try
        {
            withFlyout.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            TextContextMenu.SurfaceOf(withFlyout).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var withMenu = new SelectableTextBlock { Text = "hello", Width = 200, ContextMenu = new Avalonia.Controls.ContextMenu() };
        window = TestHelpers.Show(withMenu);
        try
        {
            withMenu.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            TextContextMenu.SurfaceOf(withMenu).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }

        var attached = new SelectableTextBlock { Text = "hello", Width = 200 };
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
    public void SelectableTextBlock_Reopen_Reuses_Surface_And_Follows_Language()
    {
        var block = new SelectableTextBlock { Text = "hello", Width = 200 };
        var window = TestHelpers.Show(block);
        try
        {
            ArcLanguage.Set(LanguageTags.EnUS);
            var first = OpenSelectable(block)!;
            Entries(first).Count.ShouldBe(2);
            Entry(first, "copy")!.Header.ShouldBe("Copy");
            Entry(first, "select-all")!.Header.ShouldBe("Select all");
            first.Close(returnFocus: false);

            ArcLanguage.Set(LanguageTags.ZhCN);
            var second = OpenSelectable(block)!;
            second.ShouldBeSameAs(first);
            Entries(second).Count.ShouldBe(2);
            Entry(second, "copy")!.Header.ShouldBe("复制");
            Entry(second, "select-all")!.Header.ShouldBe("全选");
            AutomationProperties.GetName(second).ShouldBe("文本菜单");
            second.Close(returnFocus: false);

            ArcLanguage.Set(LanguageTags.ZhTW);
            var third = OpenSelectable(block)!;
            third.ShouldBeSameAs(first);
            Entries(third).Count.ShouldBe(2);
            Entry(third, "copy")!.Header.ShouldBe("複製");
            Entry(third, "select-all")!.Header.ShouldBe("全選");
            AutomationProperties.GetName(third).ShouldBe("文字選單");
        }
        finally
        {
            ArcLanguage.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CodeBlock_And_DecoratedTextBlock_Use_Default_Selectable_Menu()
    {
        var code = new CodeBlock { Code = "var x = 1;", Language = "cs" };
        var window = TestHelpers.Show(code);
        try
        {
            SetClipboardAsync(code, null).GetAwaiter().GetResult();
            var presenter = code.TextPresenter.ShouldNotBeNull();
            var surface = OpenSelectable(presenter)!;
            Entries(surface).Select(e => (string)e.Tag!).ShouldBe(["copy", "select-all"]);
            Entry(surface, "select-all")!.PerformClick();
            Dispatcher.UIThread.RunJobs();
            presenter.SelectedText.ShouldBe("var x = 1;");

            surface = OpenSelectable(presenter)!;
            Dispatcher.UIThread.RunJobs();
            Entry(surface, "copy")!.IsEnabled.ShouldBeTrue();
            Entry(surface, "copy")!.PerformClick();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => GetClipboardText(code) == "var x = 1;").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var decorated = new DecoratedTextBlock { Text = "decorated", Width = 200 };
        window = TestHelpers.Show(decorated);
        try
        {
            var surface = OpenSelectable(decorated)!;
            Entries(surface).Select(e => (string)e.Tag!).ShouldBe(["copy", "select-all"]);
        }
        finally
        {
            window.Close();
        }
    }
}
