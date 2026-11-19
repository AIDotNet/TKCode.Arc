using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class TaskInteractionTests
{
    private const string Source = "- [ ] first\n- [x] second\n- plain\n";

    private static async Task<(Window Window, MarkdownView View, List<TaskToggleRequestedEventArgs> Requests)> Show(string markdown, bool interactive)
    {
        var view = new MarkdownView { Markdown = markdown, Margin = new(12), IsTaskInteractionEnabled = interactive };
        var requests = new List<TaskToggleRequestedEventArgs>();
        view.TaskToggleRequested += (_, e) => requests.Add(e);
        var window = new Window { Width = 520, Height = 420, Content = view, Background = Brushes.Black };
        window.Show();
        await HeadlessApp.Settle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
        return (window, view, requests);
    }

    private static List<ListItemBlockView> TaskItems(MarkdownView view) =>
        view.GetVisualDescendants().OfType<ListItemBlockView>().Where(i => i.Task is not null).ToList();

    // 复选框中心（窗口坐标）：列表项正文起点往左，扣掉复选框右侧的间距和半个复选框
    private static Point BoxCenter(Window window, MarkdownView view, ListItemBlockView item)
    {
        var style = MarkdownStyle.Resolve(view);
        var segment = item.Segments().First();
        var origin = segment.View.TranslatePoint(default, item)!.Value;
        var line = segment.GetCharacterBounds(0);
        var box = Math.Round(style.FontSize * 0.95);
        return item.TranslatePoint(new Point(origin.X - style.FontSize * 0.45 - box / 2, origin.Y + line.Top + line.Height / 2), window)!.Value;
    }

    private static Point TextPoint(Window window, ListItemBlockView item)
    {
        var segment = item.Segments().First();
        var line = segment.GetCharacterBounds(2);
        return segment.View.TranslatePoint(new Point(line.Left + 1, line.Top + line.Height / 2), window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [Fact]
    public async Task ReadOnlyByDefault_ClicksAndKeysRaiseNothing()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: false);
            Assert.False(view.IsTaskInteractionEnabled);
            var items = TaskItems(view);
            Assert.Equal(2, items.Count);

            Click(window, BoxCenter(window, view, items[0]));
            Assert.False(items[0].Focusable);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Assert.Empty(requests);
            window.Close();
        });
    }

    [Fact]
    public async Task Click_RaisesRequestWithMarkerStateAndVersion_AndLeavesRenderedStateAlone()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: true);
            var items = TaskItems(view);

            Click(window, BoxCenter(window, view, items[0]));
            var request = Assert.Single(requests);
            Assert.False(request.IsChecked);
            Assert.True(request.RequestedChecked);
            Assert.Equal("[ ]", Source.Substring(request.Marker.Start, request.Marker.Length));
            Assert.Equal(Source.IndexOf("[ ] first", StringComparison.Ordinal), request.Marker.Start);
            Assert.Equal(view.DocumentId, request.DocumentId);
            Assert.Equal(view.Snapshot!.Version, request.Version);
            Assert.True(view.IsCurrent(request));
            Assert.Equal("- [x] first\n- [x] second\n- plain\n", request.ApplyTo(Source));

            // 宿主没有改原文（相当于拒绝）：视图的勾选状态不变
            await HeadlessApp.Settle();
            Assert.Equal(new bool?[] { false, true }, TaskItems(view).Select(i => i.Task).ToArray());

            // 已勾选的项请求取消
            Click(window, BoxCenter(window, view, items[1]));
            Assert.Equal(2, requests.Count);
            Assert.True(requests[1].IsChecked);
            Assert.Equal("- [ ] first\n- [ ] second\n- plain\n", requests[1].ApplyTo(Source));
            window.Close();
        });
    }

    [Fact]
    public async Task ClickingTaskText_SelectsTextInsteadOfRaisingRequest()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: true);
            var items = TaskItems(view);

            Click(window, TextPoint(window, items[1]));
            Assert.Empty(requests);
            Assert.NotNull(view.Selection.Anchor);
            window.Close();
        });
    }

    [Fact]
    public async Task NestedOrderedAndDuplicateTasks_IdentifyTheClickedMarker()
    {
        await HeadlessApp.Run(async () =>
        {
            const string text = "1. [ ] same\n2. [x] other\n\n- [ ] parent\n  - [ ] same\n    - [x] same\n\n- [ ] same\n";
            var (window, view, requests) = await Show(text, interactive: true);
            var items = TaskItems(view);
            Assert.Equal(6, items.Count);

            // 依次点每一项，请求里的标记位置必须就是那一项自己的 "[…]"
            var expected = new List<int>();
            for (var from = 0; (from = text.IndexOf("[", from, StringComparison.Ordinal)) >= 0; from++)
            {
                expected.Add(from);
            }
            Assert.Equal(6, expected.Count);
            foreach (var item in items)
            {
                Click(window, BoxCenter(window, view, item));
            }
            Assert.Equal(expected, requests.Select(r => r.Marker.Start).ToList());
            Assert.All(requests, r => Assert.StartsWith("[", text.Substring(r.Marker.Start, r.Marker.Length)));
            window.Close();
        });
    }

    [Fact]
    public async Task ApplyTo_ChangesOnlyTheMarkerCharacter_AndKeepsIndentAndCrlf()
    {
        await HeadlessApp.Run(async () =>
        {
            const string text = "intro\r\n\r\n- [ ] top\r\n    - [x] nested 中文 😀\r\n- [ ] last";
            var (window, view, requests) = await Show(text, interactive: true);
            var items = TaskItems(view);

            Click(window, BoxCenter(window, view, items[1]));
            var request = Assert.Single(requests);
            var changed = request.ApplyTo(text)!;
            Assert.Equal(text.Length, changed.Length);
            var differences = Enumerable.Range(0, text.Length).Where(i => text[i] != changed[i]).ToList();
            Assert.Equal([request.Marker.Start + 1], differences);
            Assert.Equal('x', text[differences[0]]);
            Assert.Equal(' ', changed[differences[0]]);
            Assert.Contains("\r\n    - [ ] nested 中文 😀\r\n", changed);

            // 宿主把新文本交回视图：只有这一项的状态变了
            view.Markdown = changed;
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.Equal(new bool?[] { false, false, false }, TaskItems(view).Select(i => i.Task).ToArray());
            window.Close();
        });
    }

    [Fact]
    public void ApplyTo_RejectsSourceThatNoLongerMatches()
    {
        var request = new TaskToggleRequestedEventArgs(1, 1, new SourceRange(2, 3), isChecked: false);
        Assert.Equal("- [x] a", request.ApplyTo("- [ ] a"));
        Assert.Null(request.ApplyTo("- [x] a"));
        Assert.Null(request.ApplyTo("- (x) a"));
        Assert.Null(request.ApplyTo("- ["));
        Assert.Equal("- [ ] a", new TaskToggleRequestedEventArgs(1, 1, new SourceRange(2, 3), isChecked: true).ApplyTo("- [X] a"));
    }

    [Fact]
    public async Task Keyboard_SpaceAndEnter_ActivateTheFocusedTask()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: true);
            var items = TaskItems(view);
            Assert.All(items, i => Assert.True(i.Focusable));

            Assert.True(items[1].Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Assert.Equal(2, requests.Count);
            Assert.All(requests, r => Assert.Equal(Source.IndexOf("[x]", StringComparison.Ordinal), r.Marker.Start));

            // 其他键不触发
            window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, "a");
            Assert.Equal(2, requests.Count);
            window.Close();
        });
    }

    [Fact]
    public async Task Automation_ExposesCheckBoxStateAndToggle()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: true);
            var items = TaskItems(view);

            var peer = ControlAutomationPeer.CreatePeerForElement(items[0]);
            Assert.Equal(AutomationControlType.CheckBox, peer.GetAutomationControlType());
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            Assert.Equal(ToggleState.On, ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(items[1])).ToggleState);
            Assert.Equal("first", peer.GetName());

            toggle.Toggle();
            var request = Assert.Single(requests);
            Assert.Equal(Source.IndexOf("[ ]", StringComparison.Ordinal), request.Marker.Start);
            window.Close();
        });
    }

    [Fact]
    public async Task StaleRequests_AreDetectedByDocumentAndVersion()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: true);
            Click(window, BoxCenter(window, view, TaskItems(view)[0]));
            var request = Assert.Single(requests);
            Assert.True(view.IsCurrent(request));

            // 宿主还在处理时文档被换掉：请求过期
            view.Markdown = "- [ ] other\n";
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.False(view.IsCurrent(request));
            window.Close();
        });
    }

    [Fact]
    public async Task WhileStreaming_ClicksAreIgnored_UntilTheDocumentCompletes()
    {
        await HeadlessApp.Run(async () =>
        {
            var document = new StreamingDocument();
            document.Append("- [ ] one\n- [x] two\n\nstill writing");
            var view = new MarkdownView { Document = document, Margin = new(12), IsTaskInteractionEnabled = true };
            var requests = new List<TaskToggleRequestedEventArgs>();
            view.TaskToggleRequested += (_, e) => requests.Add(e);
            var window = new Window { Width = 520, Height = 420, Content = view, Background = Brushes.Black };
            window.Show();
            await document.WhenIdle();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var items = TaskItems(view);
            Assert.Equal(2, items.Count);
            Click(window, BoxCenter(window, view, items[0]));
            Assert.Empty(requests);

            document.Complete();
            await document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            Click(window, BoxCenter(window, view, TaskItems(view)[0]));
            Assert.Single(requests);
            window.Close();
        });
    }

    [Fact]
    public async Task EnablingInteractionLater_MakesExistingCheckboxesClickable()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view, requests) = await Show(Source, interactive: false);
            Click(window, BoxCenter(window, view, TaskItems(view)[0]));
            Assert.Empty(requests);

            view.IsTaskInteractionEnabled = true;
            await HeadlessApp.Settle();
            Click(window, BoxCenter(window, view, TaskItems(view)[0]));
            Assert.Single(requests);

            view.IsTaskInteractionEnabled = false;
            await HeadlessApp.Settle();
            Click(window, BoxCenter(window, view, TaskItems(view)[0]));
            Assert.Single(requests);
            window.Close();
        });
    }
}
