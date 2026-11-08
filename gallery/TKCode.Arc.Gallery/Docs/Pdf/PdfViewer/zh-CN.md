---
title: PDF 查看器
summary: 查看 PDF，带缩略图、目录、搜索、文字选择、链接和密码提示；桌面上由 PDFium 渲染，浏览器中由 pdf.js 渲染。
source-hash: 0f5b8e340c0d
---

<Demo name="Preview" />

## 何时使用

`PdfViewer` 在应用内打开 PDF 文件。它在 `PageViewer` 之上增加了带缩略图和目录的侧栏、搜索、文字选择、链接和密码提示，并有自己的加载和失败状态。

- **业务工具中的合同和发票**：职员阅读文档，搜索姓名，复制一个数字。
- **桌面应用中的手册和报告**：读者通过目录跳转，并沿链接在章节间移动。
- **Web 应用中生成的文件**：用户不离开页面就能检查服务器生成的 PDF。
- **受保护的文档**：读者在查看器中输入密码，或由你的应用提供。

## 何时不用

- **编辑 PDF**：注释、表单填写、数字签名、打印、视图旋转和双页模式都不在此版本内。
- **脚本和 XFA 表单**：文档中的脚本从不运行，也不支持 XFA 表单。
- **图片或你自己渲染的页面**：用主包中的 `PageViewer`，不需要原生库。
- **需要重排的文字**：用 `Markdown`。PDF 页面的版式是固定的，手机上的读者只能缩放和平移。

## 用法

添加 `TKCode.Arc.Pdf` 和一个后端，注册包，并在启动时启用后端：

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcPdf());

ArcPdfNative.Use();              // desktop: TKCode.Arc.Pdf.Native
await ArcPdfBrowser.UseAsync();  // browser: TKCode.Arc.Pdf.Browser
```

把 `Source` 设为绝对地址或路径。`http`、`https` 和 `avares` 地址都可以，文件路径也可以。在代码中，`PdfSource` 还能打开字节和流。

```xml
<arc:PdfViewer Source="avares://MyApp/Assets/report.pdf" Height="600" />
```

```csharp
viewer.Source = PdfSource.FromFile(path);
viewer.Source = PdfSource.FromBytes(bytes);
viewer.Source = PdfSource.FromStream(() => File.OpenRead(path));
viewer.DocumentOpened += (_, e) => status.Text = e.Document.Info.Title;
viewer.DocumentFailed += (_, e) => status.Text = e.Error.Kind.ToString();
```

`PdfViewer` 派生自 `PageViewer`，所以缩放、布局、平移、`CurrentPageIndex` 和工具栏的用法相同。`Status` 表示当前状态：`Empty`、`Loading`、`Ready`、`PasswordRequired` 或 `Failed`。没有显示文档时，工具栏隐藏页码、缩放、侧栏和搜索控件；已打开的侧栏或搜索框会随下一个文档回来。

`Document` 显示你自己打开的 `PdfDocument`。它仍归你所有，查看器从不释放它；两者都设置时 `Source` 优先。`SimulatedPdfDocument` 提供生成的页面，用于测试和设计预览。

### 结构

1. **工具栏**：`PageViewer` 的工具栏，开头是侧栏按钮，末尾是搜索按钮。
2. **侧栏**：缩略图和目录，分两个页签。没有目录的文档只显示缩略图。
3. **页面**：白纸，亮色和暗色主题下都一样。查看器不反转页面颜色。
4. **搜索框**：浮在右上角，带计数、上一处和下一处、匹配大小写、整词和关闭。
5. **状态覆盖层**：打开时的加载动画、失败后的原因，以及密码表单。

### 侧栏

`IsSidebarOpen` 显示侧栏，`SidebarMode` 选择 `Thumbnails` 或 `Outline`；两者都是双向绑定。缩略图从与页面相同的缓存中按需渲染，点击目录项会跳到对应位置。

<Demo name="Sidebar" />

### 搜索

`Ctrl` 或 `Cmd` + `F` 打开搜索框。`SearchText` 在停止输入 300 毫秒后开始搜索，从当前页向后并环绕。每页完成就上报，所以 `IsSearching` 为 true 时 `SearchMatchCount` 不断增长；有了第一个结果，查看器就跳过去。`IsMatchCase` 和 `IsWholeWord` 缩小搜索范围，中文、日文和韩文没有整词匹配。`SearchNext`、`SearchPrevious` 和双向的 `CurrentSearchMatch` 在结果之间移动。

<Demo name="Search" />

### 选择文字

在文字上拖动即可选择，双击选词，三击选行。Shift 加点击扩展选区，选区可以跨页。`SelectedText` 保存所选文字；`Ctrl` 或 `Cmd` + `C` 和右键菜单可以复制，`CopySelection`、`SelectAll` 和 `ClearSelection` 在代码中完成相同操作。`IsTextSelectionEnabled` 关闭以上全部功能。

此版本的文字选择不支持触屏拖动：手指滚动页面，双击选词。

### 链接

点击链接会引发 `LinkActivated`；设置 `Handled` 可改做别的事。否则文档内部的链接跳到对应页面。外部链接只对 `http`、`https` 和 `mailto` 打开；其他协议（如 `file:` 或 `javascript:`）一律不打开，因为文档不可信。`ExternalLinkBehavior="None"` 把外部链接交给你的处理程序。

```csharp
viewer.LinkActivated += (_, e) =>
{
    if (e.Link.Uri is { } address)
    {
        e.Handled = true;
        ConfirmAndOpen(address);
    }
};
```

### 加密文档

带密码的文档会显示表单，密码错误时表单抖动并给出提示。要由应用回答，请处理 `PasswordRequested`：设置 `Password`，或设置 `Cancel` 放弃；需要稍后回答时取一个 deferral。`IsRetry` 表示上一个密码是错的。查看器只在打开期间保留密码，也从不写入日志。

这个样例的密码是 `arc-sample`：

<Demo name="Locked" />

```csharp
viewer.PasswordRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    e.Password = await vault.GetPasswordAsync();
};
```

### 只有页面

`Pdf.Source` 在普通 `PageViewer` 中打开 PDF，只有页面和缩放，没有上面那些工具。这里没有密码表单和失败覆盖层：`Pdf.Error` 说明文档未能打开的原因，需要密码也在其中。

<Demo name="PagesOnly" />

### 平台与包

| 包 | 内容 | 大小 | 入口 |
|---|---|---|---|
| `TKCode.Arc.Pdf` | `PdfViewer`、`Pdf.Source`、后端接口 | 0.3 MB | `UseArcPdf()` |
| `TKCode.Arc.Pdf.Native` | win、linux、osx 的 x64 和 arm64 版 PDFium | 包 22 MB；单平台应用中约 7.5 MB | `ArcPdfNative.Use()` |
| `TKCode.Arc.Pdf.Browser` | pdf.js、其 worker 和数据文件 | 包 2 MB；服务器上 4 MB，按需获取 | `await ArcPdfBrowser.UseAsync()` |

两个入口在无法加载时都返回 false，后端保持原样。没有后端时，文档以 `BackendUnavailable` 失败，查看器会说明缺少哪个调用，绝不显示假页面。

`ArcPdfBrowser.UseAsync` 从 `_content/TKCode.Arc.Pdf.Browser/pdfjs/` 导入 pdf.js，所以宿主必须提供包的静态 Web 资源，并为 `.mjs` 文件设置 JavaScript 类型。要从别处提供这些文件，请传入地址。

需要预先考虑的限制：

- **字体**：没有嵌入中文、日文或韩文字体的文档依赖本机字体。在没有字体的 Linux 机器上，这些文字显示为空白。
- **浏览器中的字体**：Liberation 字体因许可证原因不随浏览器包提供，所以未嵌入 Helvetica 或 Arial 的文档会回退到本地字体或浏览器的无衬线字体，字宽会不同。
- **触屏**：此版本的 `IsTextSelectionEnabled` 不支持触屏拖动选择。
- **文字中的网址**：纯文本写的网址只有在 PDFium 下才算链接。在浏览器中，只有真正的链接注释才是链接。
- **浏览器中的选择**：pdf.js 按文本段报告文字，查看器按宽度比例拆分每一段，所以选区可能与字形略有偏差。
- **不可信文件**：PDFium 在你的进程内解析文件。请保持更新，也不要把它当作处理陌生文件的沙箱。
- **离开可视树**：查看器离开可视树时会关闭从 `Source` 打开的文档，回来时重新打开，加密文档会再次询问密码。一步之内把查看器移到另一个父元素则文档保持打开。

### 内容指南

- 页面知道文档名称时，用 `AutomationProperties.Name` 给查看器一个无障碍名称。
- 文档可能没有标题时，给 `PdfSource` 设置 `DisplayName`。`Title` 会回退到它。
- 在读者无法离开的流程中，`DocumentFailed` 触发时要说明下一步怎么做。

| 不要写 | 应该写 |
|---|---|
| 错误 | 无法打开此文件 |
| PDF 文档 | 租赁协议 |

### 最佳实践

**推荐**

- 给查看器一个高度，或放在有高度的父元素中。在高度无限的父元素中，它会增长到整列页面的高度。
- 在打开第一个文档之前启用后端。
- 文档中的链接需要读者确认时，处理 `LinkActivated`。

**避免**

- 释放查看器从 `Source` 打开的文档。它归查看器所有。
- 在 `PasswordRequested` 之后仍把密码留在视图模型中。
- 把查看器当作安全边界。它检查链接协议，也不运行脚本，但文件仍在你的进程内解析。

### 常见问题

**为什么查看器说没有设置渲染器？** `ArcPdf.Backend` 还是默认值。请调用 `ArcPdfNative.Use()` 或 `await ArcPdfBrowser.UseAsync()`，并确认引用了对应平台的包。

**为什么中文显示为空白？** 文档没有嵌入字体，而本机没有合适的字体。见上面的限制。

**为什么又询问密码？** 查看器离开了可视树，这会关闭文档并忘记密码。

**为什么暗色主题下页面是白的？** 页面是纸。查看器不反转页面颜色。

## API 参考

<!-- api: PdfViewer -->

`Pdf` 的附加属性，用于普通 `PageViewer`：

<!-- api: Pdf -->

`PdfViewer` 还有这些方法：

| 方法 | 含义 |
|---|---|
| `SearchNext()`、`SearchPrevious()` | 跳到下一处或上一处结果，到头后环绕。 |
| `ClearSearch()` | 结束搜索并清空 `SearchText`。 |
| `SelectAll()`、`ClearSelection()`、`CopySelection()` | 选择当前页、清除选区或复制选区。 |

`PdfSource` 说明文档的来源：

| 成员 | 含义 |
|---|---|
| `FromFile(path)` | 文件。不适用于浏览器，那里没有可用的文件系统。 |
| `FromBytes(data)` | 内存中的字节。 |
| `FromStream(open)` | 可定位的流；每次打开调用一次 `open`。 |
| `FromUri(uri)` | `http` 或 `https`，或表示应用资源的 `avares`。 |
| `Parse(text)` | 绝对地址或路径，XAML 用的就是它。 |
| `DisplayName` | 文档没有标题时显示的名称。 |

`PdfDocument` 是已打开的文档：一个 `IPageSource`，带 `Info`、`GetPageLabel`、`GetOutlineAsync`、`GetPageTextAsync` 和 `GetLinksAsync`。`ArcPdf.Backend` 是打开文档的 `IPdfBackend`；`PdfException.Kind` 是 `PdfErrorKind`：`BackendUnavailable`、`NotFound`、`Network`、`Corrupt`、`Unsupported`、`PasswordRequired` 或 `Aborted`。

## 无障碍

- 视口对辅助技术来说是一个文档，名称为“Page 3 of 12”，除非 `AutomationProperties.Name` 设置了名称。屏幕阅读器可以通过帮助文本读取当前页的文字；此版本没有完整的文本模式。
- 工具栏和搜索框的每个按钮都有当前语言（英语、简体中文、繁体中文）的名称和工具提示。
- 搜索结果数以礼貌方式播报。
- 密码表单出现时获得焦点，Enter 和 Escape 可以作答。
- 没有显示文档时，工具栏的控件隐藏，不在 Tab 顺序中。

### 键盘

视口有焦点时按键生效。

| 按键 | 操作 |
|---|---|
| 方向键 | 滚动 40 个单位 |
| `PageUp` / `PageDown` | 滚动一屏；在 `Single` 布局中翻页 |
| `Home` / `End` | 跳到第一页或最后一页 |
| `Ctrl` 或 `Cmd` + `=` / `-` / `0` | 放大、缩小或回到 100% |
| 按住 `Space` 并拖动 | 平移 |
| `Ctrl` 或 `Cmd` + `F` | 打开搜索并聚焦 |
| `Enter` / `Shift` + `Enter`、`F3` / `Shift` + `F3` | 下一处或上一处结果 |
| `Escape` | 关闭搜索；否则清除选区 |
| `Ctrl` 或 `Cmd` + `C` / `A` | 复制，或选择当前页 |
| 页码框中按 `Enter` | 跳到该页，超出范围时钳制 |

## 动效

- 侧栏打开时宽度沿平滑弹簧变化；在窄查看器中，它滑入并浮在页面上。
- 搜索框淡入并落到位。
- 密码错误时表单抖动。
- 缩放和翻页与 `PageViewer` 相同。更清晰的位图直接替换旧位图，文字不会闪动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 侧栏开合 | 弹簧或滑动 | 淡入淡出 | 立即 |
| 搜索框开合 | 淡入并下落 | 淡入淡出 | 立即 |
| 密码错误 | 抖动 | 只有错误文字 | 只有错误文字 |
| 工具栏缩放、`GoToPage` | 弹簧 | 立即 | 立即 |

## 响应式

- 查看器没有自身大小：给它一个高度，它会填满得到的宽度。
- 宽度小于 640 时，侧栏浮在页面上，不再占用页面的宽度。
- 宽度小于 520 时，工具栏只保留页码框、总页数、缩放控件和两端的按钮。
- 在触屏上，手指滚动，捏合以手指为中心缩放，双击选词。没有依赖悬停的功能。

## 相关组件

- `PageViewer` 用于图片或你自己渲染的页面。
- `VideoPlayer` 是同类的包，每个平台有单独的后端。
- `Markdown` 用于需要重排的文档。

| 组件 | 何时选择 |
|---|---|
| `PageViewer` | 页面是图片，或由你的代码渲染 |
| `Markdown` | 文字应随宽度重排 |
| `ArcImage` | 布局中的单张图片 |
