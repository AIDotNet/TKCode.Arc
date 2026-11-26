using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.Editors;

/// <summary>The editor's menus, toolbar, find bar and announcements follow the Arc language (#215).</summary>
public class RichTextEditorLocalizationTests
{
    private static (RichTextEditor Editor, Window Window, RichTextEditorSurface Surface) Show(string html = "<p>one</p>")
    {
        var editor = new RichTextEditor { Width = 480, Profile = RichTextEditorProfile.Docs, Html = html };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = 540;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<RichTextEditorSurface>(editor));
    }

    private static void SetLanguage(LanguageTag tag)
    {
        ArcLanguageManager.Current!.Set(tag);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static Point Center(Control control, Window window) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public void Slash_Filter_Matches_The_Localized_Label_And_The_English_Keywords()
    {
        try
        {
            SetLanguage(LanguageTags.ZhCN);
            RichTextEditorSlashItem.Filter("标题", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.Heading1);
            RichTextEditorSlashItem.Filter("代码", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.CodeBlock);
            RichTextEditorSlashItem.Filter("heading", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.Heading2);
            RichTextEditorSlashItem.Filter("todo", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.TodoList);
            RichTextEditorSlashItem.Filter("分隔", docs: true).Select(i => i.Type).ShouldBe(new[] { RichTextBlockType.Divider });

            SetLanguage(LanguageTags.ZhTW);
            RichTextEditorSlashItem.Filter("標題", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.Heading1);
            RichTextEditorSlashItem.Filter("heading", docs: true).Select(i => i.Type).ShouldContain(RichTextBlockType.Heading1);

            SetLanguage(LanguageTags.EnUS);
            RichTextEditorSlashItem.Filter("heading", docs: true).Select(i => i.Label).ShouldContain("Heading 1");
            RichTextEditorSlashItem.Filter("标题", docs: true).ShouldBeEmpty();
        }
        finally
        {
            SetLanguage(LanguageTags.EnUS);
        }
    }

    [AvaloniaFact]
    public void Slash_Menu_Rows_And_Announcements_Follow_The_Language()
    {
        var (editor, window, _) = Show("<p></p>");
        try
        {
            SetLanguage(LanguageTags.ZhCN);
            editor.FocusEditor();
            Dispatcher.UIThread.RunJobs();
            Type(window, "/");
            var menu = editor.SlashMenu.ShouldNotBeNull();
            menu.IsShown.ShouldBeTrue();
            menu.Items.Select(i => i.Label).ShouldContain("无序列表");
            menu.GetVisualDescendants().OfType<RichTextEditorSlashRow>().Where(r => r.IsVisible).Select(r => r.Label).ShouldContain("待办列表");
            Avalonia.Automation.AutomationProperties.GetName(menu).ShouldBe("内容块");

            Type(window, "引用");
            menu.Items.Select(i => i.Type).ShouldBe(new[] { RichTextBlockType.Quote });
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            editor.LiveRegion!.Message.ShouldBe("已添加引用");

            SetLanguage(LanguageTags.EnUS);
            Type(window, "/");
            menu.Items.Select(i => i.Label).ShouldContain("Bulleted list");
            menu.GetVisualDescendants().OfType<RichTextEditorSlashRow>().Where(r => r.IsVisible).Select(r => r.Label).ShouldContain("To-do list");
        }
        finally
        {
            SetLanguage(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Tips_Names_And_Placeholder_Switch_With_The_Language()
    {
        var (editor, window, _) = Show("<p></p>");
        try
        {
            editor.FocusEditor();
            Dispatcher.UIThread.RunJobs();
            Type(window, "/");
            var toolbar = editor.Toolbar.ShouldNotBeNull();
            Tooltip.GetTip(toolbar.Bold).ShouldBe("Bold (⌘B)");
            editor.DisplayPlaceholder.ShouldBe("Start writing");

            SetLanguage(LanguageTags.ZhCN);
            Tooltip.GetTip(toolbar.Bold).ShouldBe("加粗（⌘B）");
            Tooltip.GetTip(toolbar.Heading1).ShouldBe("标题 1");
            Avalonia.Automation.AutomationProperties.GetName(toolbar.Link).ShouldBe("链接");
            Avalonia.Automation.AutomationProperties.GetName(toolbar).ShouldBe("格式");
            toolbar.LinkBox.PlaceholderText.ShouldBe("粘贴或输入链接");
            Avalonia.Automation.AutomationProperties.GetName(editor).ShouldBe("编辑器");
            editor.DisplayPlaceholder.ShouldBe("开始书写");

            SetLanguage(LanguageTags.ZhTW);
            Tooltip.GetTip(toolbar.Bold).ShouldBe("粗體（⌘B）");
            Tooltip.GetTip(toolbar.Quote).ShouldBe("引用");

            SetLanguage(LanguageTags.EnUS);
            Tooltip.GetTip(toolbar.Bold).ShouldBe("Bold (⌘B)");
            Tooltip.GetTip(toolbar.Heading1).ShouldBe("Heading 1");
            Avalonia.Automation.AutomationProperties.GetName(editor).ShouldBe("Editor");
            editor.Placeholder = "Custom";
            editor.DisplayPlaceholder.ShouldBe("Custom");
        }
        finally
        {
            SetLanguage(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Block_Menu_Follows_The_Language_Each_Time_It_Opens()
    {
        var (editor, window, surface) = Show("<p>one</p>");
        try
        {
            var point = surface.TranslatePoint(new Point(40, surface.HandleY(0)), window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            var grip = Center(editor.BlockHandle!.Grip, window);
            string[] Open()
            {
                window.MouseMove(grip);
                Dispatcher.UIThread.RunJobs();
                window.MouseDown(grip, MouseButton.Left);
                window.MouseUp(grip, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                var headers = editor.BlockHandle.Menu!.Items.OfType<MenuEntry>().Select(e => (string)e.Header!).ToArray();
                editor.BlockHandle.Menu.Close(returnFocus: false);
                Dispatcher.UIThread.RunJobs();
                return headers;
            }
            SetLanguage(LanguageTags.ZhCN);
            var zh = Open();
            zh.ShouldContain("正文");
            zh.ShouldContain("标题 1");
            zh.ShouldContain("代码块");
            zh.ShouldContain("在下方添加内容块");
            zh.ShouldContain("行距：紧凑");
            Tooltip.GetTip(editor.BlockHandle.Grip).ShouldBe("拖动以移动，点击查看操作");

            SetLanguage(LanguageTags.EnUS);
            var en = Open();
            en.ShouldContain("Text");
            en.ShouldContain("Code block");
            en.ShouldContain("Line spacing: Compact");
            en.ShouldNotContain("正文");
        }
        finally
        {
            SetLanguage(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Text_Has_A_Translation_With_The_Same_Placeholders()
    {
        try
        {
            var kinds = Enum.GetValues<RichTextEditorLangResourceKind>();
            kinds.Length.ShouldBeGreaterThan(100);
            var english = new Dictionary<RichTextEditorLangResourceKind, string>();
            SetLanguage(LanguageTags.EnUS);
            foreach (var kind in kinds)
            {
                english[kind] = RichTextEditorLangResources.Get(kind);
                english[kind].ShouldNotBeNullOrEmpty();
            }
            foreach (var tag in new[] { LanguageTags.ZhCN, LanguageTags.ZhTW })
            {
                SetLanguage(tag);
                foreach (var kind in kinds)
                {
                    var text = RichTextEditorLangResources.Get(kind);
                    text.ShouldNotBeNullOrEmpty();
                    if (english[kind] != kind.ToString())
                    {
                        text.ShouldNotBe(kind.ToString());
                    }
                    for (var i = 0; i < 3; i++)
                    {
                        text.Contains("{" + i + "}", StringComparison.Ordinal).ShouldBe(english[kind].Contains("{" + i + "}", StringComparison.Ordinal), $"{kind} in {tag}");
                    }
                }
            }
        }
        finally
        {
            SetLanguage(LanguageTags.EnUS);
        }
    }
}
