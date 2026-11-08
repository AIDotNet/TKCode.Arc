---
title: 页面查看器
summary: 缩放、平移并翻阅图片或按需渲染的页面。
source-hash: 0dc771826687
---

<Demo name="Overview" />

## 何时使用

`PageViewer` 显示一组页面，让读者在页面之间移动、放大并平移。页面可以是集合中的图片，也可以是 `IPageSource` 在进入视野时才渲染的页面，例如扫描的合同或报告的各页。它只绘制可见页，所以一万页的文档滚动起来和短文档一样轻。

- **档案工具中的扫描文件**：办事员逐页阅读扫描的表格，并放大查看签名。
- **出版工具中的校样**：编辑在付印前以原始大小翻阅版面的各页。
- **由服务器渲染的报告**：分析师滚动浏览一份长报告，报告的各页在需要时才以图片形式到达。
- **在页面上做标记**：审阅者点击页面留下标记，标记由 `PageLayer` 绘制，缩放和滚动时都留在页面上。

## 何时不用

- **浏览一排图片**：用 `Carousel`，它通过拖动在幻灯片之间切换。
- **同一图片的两个版本**：用 `ImageCompare`，它用分隔线对比两者。
- **布局中的一张图片**：用 `ArcImage`。页面查看器带有工具栏和滚动画布，单张图片用不到。
- **需要重排的文字**：用 `Markdown`。页面的布局是固定的，手机上的读者只能缩放和平移。
- **阅读 PDF 文件**：用 `TKCode.Arc.Pdf` 中的 `PdfViewer`，它增加了缩略图、目录、搜索、文字选择、链接和密码提示。只需要页面时，在 `PageViewer` 上设置 `Pdf.Source`。

## 用法

给查看器一个高度和一组图片。`ItemsSource` 接受 `Bitmap` 对象、`ImageSource` 对象，以及字符串或 `Uri` 形式的地址；集合实现 `INotifyCollectionChanged` 时会跟随其变化。

```xml
<arc:PageViewer Height="600" FitMode="Page" />
```

```csharp
viewer.ItemsSource = new[]
{
    "https://example.com/scan-1.png",
    "https://example.com/scan-2.png"
};
viewer.CurrentPageChanged += (_, e) => status.Text = $"Page {e.NewIndex + 1}";
```

`PageViewer` 派生自 Avalonia 的 `TemplatedControl`。它在滚动查看器内的一个表面上自行绘制页面，所以页面不是控件，也没有逐页的模板。

### 结构

1. **工具栏**：页码框和总页数、上一页和下一页、缩小、缩放菜单、放大，以及布局和平移开关。`ToolbarStart` 和 `ToolbarEnd` 在两端添加你自己的内容，`IsToolbarVisible` 可隐藏工具栏。没有页面时，它只显示两端你自己的内容。
2. **画布**：页面背后的表面，使用柔和的表面色。
3. **页面**：带静置阴影的白纸，明暗主题都一样。没有位图的页面显示一个静止的圆环，直到位图到达。
4. **图层**（`Layers`）：在每页之上进行你自己的绘制和指针输入。

### 布局

`LayoutMode` 默认为 `Continuous`，所有页面排成一列滚动。`Single` 只显示当前页；工具栏、`NextPage`、`PreviousPage` 和翻页键负责切换。

<Demo name="SinglePage" />

`CurrentPageIndex` 是视口中线所在的页。它是双向的，从 0 开始，设置它就会跳到该页。`GoToPage` 作用相同，并可以关闭动画。

### 适应与缩放

`FitMode` 让缩放适应视口，并在视口变化时重新计算。默认的 `Width` 让最宽的页面填满宽度，`Height` 让最高的页面填满高度，`Page` 让一整页保持可见。在 `Single` 布局中它们以当前页为准。

<Demo name="FitWidth" />

`Zoom` 为 1 时页面显示为自身大小，并被钳制到 `MinZoom` 和 `MaxZoom` 之间。从外部设置的值会把 `FitMode` 变为 `None`，工具栏和缩放键也一样。`ZoomIn` 和 `ZoomOut` 在 10、25、50、75、100、125、150、200、300、400、600 和 800% 这些档位之间移动。

```csharp
viewer.ZoomIn();
viewer.ResetZoom();
viewer.FitMode = PageFitMode.Page;
```

### 平移

`InteractionMode` 决定在页面上拖动的效果。默认的 `Select` 把拖动交给图层，`Pan` 移动视图。在任何模式下，鼠标中键和按住空格拖动也能平移，手指则通过滚动查看器平移。

<Demo name="PanMode" />

### 按需渲染

当页面不应全部留在内存中时，请实现 `IPageSource`。`RenderAsync` 为可见页及两侧各一页运行，离中心最近的优先。它在 UI 线程上调用，可以在任意线程上完成。请响应取消令牌，因为离开视野的页面会被取消。`TargetSize` 是整页的像素大小，即页面大小乘以缩放再乘以屏幕缩放比例。

<Demo name="OnDemand" />

```csharp
public sealed class ReportPages : IPageSource
{
    public int PageCount => 240;

    public event EventHandler? PagesChanged;

    public Size GetPageSize(int pageIndex) => new(612, 792);

    public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        var bitmap = await Task.Run(() => Draw(request), cancellationToken);
        return new PageImage(bitmap);
    }
}
```

- `OwnsBitmap` 为 true 的位图归查看器所有，淘汰后由它释放。`RenderCacheBytes` 限制保留的字节数（默认 192 MB）；可见页正在绘制的位图不会被淘汰。
- 缩放进行中查看器不发出请求，只拉伸已有的位图。缩放停止 120 ms 后请求清晰的位图并替换。
- 超过 16 百万像素的页面先得到一张封顶的整页位图；滚动停止 120 ms 后，再发出一个设置了 `Region` 的请求，只要视口周围的部分。请只绘制目标大小中的这个矩形。
- 返回 null 或抛出异常都会让页面保留占位。在 `PagesChanged` 触发之前，查看器不会再次请求该位图。
- 页数或页面大小变化时，请在 UI 线程上引发 `PagesChanged`。查看器会丢弃位图并重新读取大小。

### 图层

`PageLayer` 在每个可见页上绘制（裁剪到页面）并接收指针输入。图层按列表顺序绘制；指针事件从最后一层往前分发，`Handled` 会停止分发。被处理的按下会把后续拖动留给该图层。

<Demo name="Marks" />

### 移动到某处

`ScrollIntoView` 以最短路径把页面的某部分移入屏幕，已可见时不动，适合搜索结果和链接目标。`PageHitTest` 把控件上的一点换算为页面和该页上的点。

```csharp
viewer.ScrollIntoView(4, new Rect(40, 300, 200, 24));

if (viewer.PageHitTest(e.GetPosition(viewer), out var page, out var point))
{
    status.Text = $"Page {page + 1} at {point.X:0}, {point.Y:0}";
}
```

### 派生

`PageViewer` 不是密封类。其他程序集中的派生查看器通过带 `BasedOn="{StaticResource {x:Type arc:PageViewer}}"` 的控件主题复用模板（`PdfViewer` 就是这样做的），并使用下面这些受保护成员。

| 成员 | 用途 |
|---|---|
| `LeadingPane` | 页面左侧、工具栏下方的侧栏 |
| `ContentOverlay` | 页面之上的内容，例如浮动搜索栏 |
| `OnViewerKeyDown` | 最先看到按键；返回 true 表示接管该键 |
| `OnPageSourceChanged`、`OnPagesChanged` | 数据源被替换，或其页面发生变化 |
| `GetPageBitmap`、`OnPageBitmapReady`、`CancelPageBitmapRequests` | 从同一缓存取位图，用于缩略图等其他视图 |
| `EffectivePageSource` | 当前显示的数据源 |

### 文案规范

- 文档有名称时，用 `AutomationProperties.Name` 给查看器一个无障碍名称，例如“租赁合同”。没有名称时它读作“第 3 页，共 12 页”。
- 当查看器周围的页面没有显示文档标题时，把简短的标题放进 `ToolbarStart`。

| 不要写 | 应写 |
|---|---|
| 文档 | 租赁合同 |
| 图片 1/6 | 第 1 页，共 6 页 |
| 查看器 | 扫描发票 4471 |

### 最佳实践

**建议**

- 给查看器一个高度，或放进能提供高度的父级。如果父级给出无限高度，它会撑到整列页面的高度并把它们全部绘制出来。
- 报告与位图一致的页面大小，这样页面不会在位图到达时改变形状。
- 取消令牌触发时停止工作。快速滚动的读者会把许多页面甩在后面。

**避免**

- 以固定大小渲染。`TargetSize` 才是屏幕在当前缩放下需要的大小，更小的位图会发虚。
- 释放你以 `OwnsBitmap` 为 true 返回的位图。查看器可能还在绘制它。
- 保留 `GetPageBitmap` 返回的位图。每次绘制都重新获取，因为它可能已被淘汰。

### 常见问题

**为什么缩放后页面会短暂发虚？** 缩放进行中查看器拉伸已有的位图。缩放停止 120 ms 后，清晰的位图会替换它。

**为什么稍微滚动时当前页没有变？** 当前页是视口中线所在的页。跳转后，在视图再次移动之前，目标页一直是当前页，即使中线落在另一页上，例如文档的最后一页。

**为什么图片在很高的缩放下不会变清晰？** 图片只有自身的像素，`ImagePageSource` 没有细节层。能以任意大小绘制的 `IPageSource` 则始终清晰。

**为什么暗色主题下页面仍是白色？** 页面就是纸张。查看器不会反转页面的颜色。

## API 参考

<!-- api: PageViewer -->

`PageChangedEventArgs` 是 `CurrentPageChanged` 的事件数据：

| 成员 | 含义 |
|---|---|
| `OldIndex` | 之前的当前页。 |
| `NewIndex` | 现在的当前页。 |

`PageViewer` 还有以下方法：

| 方法 | 含义 |
|---|---|
| `GoToPage(index, animate)` | 跳到某页，超范围时钳制。 |
| `NextPage()`、`PreviousPage()` | 跳到下一页或上一页。 |
| `ZoomIn()`、`ZoomOut()` | 以视口中心为锚，缩放到下一档或上一档。 |
| `ResetZoom()` | 以视口中心为锚，缩放到 100%。 |
| `ScrollIntoView(pageIndex, pageRegion, animate)` | 把页面的某部分移入屏幕；已可见时不动。 |
| `PageHitTest(pointInViewer, out pageIndex, out pointInPage)` | 查找控件上某点下的页面；在画布或工具栏上返回 false。 |

`IPageSource` 是按需页面的来源，`ImagePageSource` 是 `ItemsSource` 背后的实现：

| 成员 | 含义 |
|---|---|
| `PageCount` | 页数。 |
| `GetPageSize(pageIndex)` | 页面大小，单位为页面 DIP：缩放为 1 时按此大小显示。 |
| `RenderAsync(request, cancellationToken)` | 渲染一页或其中一部分；返回 `PageImage`，暂时没有内容时返回 null。 |
| `PagesChanged` | 页数或页面大小发生变化；在 UI 线程上引发。 |

`PageRenderRequest` 有 `PageIndex`、`TargetSize`（整页的像素大小）和 `Region`（null 表示整页，否则是目标大小中的一个矩形）。`PageImage` 有 `Bitmap` 和 `OwnsBitmap`。

`PageLayer` 在页面之上绘制：

| 成员 | 含义 |
|---|---|
| `Render(context, page)` | 在一个可见页上绘制。`page` 有 `PageIndex`、`PageBounds` 和 `Scale`。 |
| `OnPointerPressed(e)`、`OnPointerMoved(e)`、`OnPointerReleased(e)` | 页面上的指针输入。`e` 有 `PageIndex`、`PointInPage`、`Modifiers`、`ClickCount`、`PointerType` 和 `Handled`。除非图层处理了按下，手指会滚动视图，所以接管拖动前先检查 `PointerType`。 |
| `OnPointerDragCancelled()` | 图层持有的拖动没有经过释放就结束了：捕获丢失，或查看器离开了可视树。请撤销拖动已开始的操作。 |
| `GetCursor(pageIndex, pointInPage)` | 某点的光标；null 表示交给下面的图层。 |
| `Invalidate()` | 请求重新绘制图层。 |

## 无障碍

- 对辅助技术而言，视口是一个文档。除非 `AutomationProperties.Name` 另有设置，否则它的名称是“第 3 页，共 12 页”，值模式读到相同的文字。当前页变化时会更新。
- 视口可获得键盘焦点，从键盘获得焦点时显示焦点环。
- 每个工具栏按钮都有当前语言（英文、简体中文、繁体中文）的名称和提示。

### 键盘

视口有焦点时按键生效。工具栏有焦点时缩放键同样有效。

| 按键 | 操作 |
|---|---|
| 方向键 | 滚动 40 个单位 |
| `PageUp` / `PageDown` | 滚动一屏；在 `Single` 布局中翻页 |
| `Home` / `End` | 跳到第一页或最后一页 |
| `Ctrl` 或 `Cmd` + `=` / `-` / `0` | 放大、缩小或回到 100% |
| 按住 `Space` 并拖动 | 平移 |
| 在页码框中按 `Enter` | 跳到该页，超范围时钳制 |

## 动效

- 工具栏和缩放键以视口中心为锚，用干脆的弹簧改变缩放。`Zoom` 立即保存目标值。
- `GoToPage` 和 `ScrollIntoView` 用平滑的弹簧移动视图。移动期间 `CurrentPageIndex` 保持目标值，读者的任何滚动都会停止弹簧。
- 滚轮和捏合跟手，没有动画。更清晰的位图直接替换旧位图，文字不会闪动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 工具栏或按键缩放 | 弹簧 | 立即 | 立即 |
| `GoToPage`、`ScrollIntoView` | 弹簧 | 立即 | 立即 |
| 滚轮或捏合缩放 | 跟手 | 跟手 | 跟手 |
| 工具栏按钮背景 | 缓动 | 立即 | 无 |

## 响应式

- 查看器没有自身尺寸：给它一个高度，它会填满得到的宽度。
- 宽度小于 640 时，页面周围的画布内边距从 24 缩小到 8，`FitMode` 会把多出的空间给页面。
- 宽度小于 520 时，工具栏只保留页码框、总页数和缩放控件。
- 触屏上手指滚动，捏合以手指为锚缩放。没有任何功能依赖悬停。

## 相关组件

- `PdfViewer` 用于 PDF 文件，带侧栏、搜索、选择和链接。
- `Carousel` 用于一排图片或幻灯片，一次显示一张。
- `ImageCompare` 用于在分隔线下对比同一图片的两个版本。
- `ArcImage` 用于布局中的一张图片。

| 组件 | 何时选择 |
|---|---|
| `PdfViewer` | 文档是 PDF 文件 |
| `Carousel` | 读者通过拖动翻阅幻灯片 |
| `ImageCompare` | 对比同一图片的两个版本 |
| `ArcImage` | 布局中只有一张图片 |
| `Markdown` | 文字应随宽度重排 |
