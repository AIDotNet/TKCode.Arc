---
title: JSON 查看器
summary: 可折叠的 JSON 树，支持搜索、长数组分页，以及复制值或路径。
source-hash: 99a6f9248630
---

<Demo name="WebhookPayload" />

## 何时使用

`JsonViewer` 把 JSON 文档显示为一棵可以逐个分支展开的树。它用在工程师和客服人员需要阅读、而不是编辑文档的地方：开发者面板、客服控制台、集成日志。查看器自成一体（包括带搜索的工具栏、树和路径栏），因此页面只需提供 JSON，并把控件放进自己的布局中。

- **Webhook 投递检查**：在集成控制台中，开发者打开一个 `invoice.paid` 或 `order.updated` 负载，检查处理程序读取的字段。设置 `ExpandDepth="2"` 后，根节点和 `data` 会展开，人们只需打开需要的分支，例如 `data.customer`。
- **API 响应调试**：在内部工具中，把 `GET /v1/orders` 调用的响应正文显示在产生它的请求旁边。复制某个字段的路径（`root.items[2].sku`），即可得到用于缺陷报告的精确引用。
- **日志与事件详情**：在事件时间线或审计视图中，结构化日志条目的 `metadata` 对象包含随事件变化的字段。搜索可以在整条记录中查找键或值。
- **配置审查**：在设置页或部署页中，运维人员阅读功能开关文档或流水线定义，并从中复制某个值，而不必冒险修改源内容。
- **大型导出预览**：在数据工具中，一个包含数千条记录的数组按每页 50 个子项加载，因此首屏立即出现，其余内容按需加载。

## 何时不用

- **把整个文档当作可复制的源代码**，例如 README 中的片段或客服回复：用 `CodeBlock`，它把文本显示为一个代码块，并提供一个复制操作。
- **人们需要选择的嵌套分类**，例如文件夹、文件或组织架构图：用 `TreeView`，它为选择而设计，且不解析 JSON。
- **结构相同的表格记录**，例如人们按列排序和筛选的发票列表：用 `Table`。JSON 树展示结构，表格用于比较行。
- **不是 JSON 的值**，例如任意 .NET 对象：先把它们转换为 `JsonNode` 或 `JsonElement`。查看器只读取 System.Text.Json 数据。
- **编辑文档**：查看器是只读的。当人们需要修改文本时，使用 `Textarea` 或 `RichTextEditor`。

## 用法

用 `Json` 传入 JSON 文本。数据已经解析完成时，用 `Value` 传入 `JsonNode`，或用 `Element` 传入 `JsonElement`。

```xml
<arc:JsonViewer Json="{Binding ResponseJson}"
                ExpandDepth="2"
                MaxTreeHeight="320" />
```

`JsonViewer` 派生自 Avalonia 的 `TemplatedControl`。它的模板包含工具栏、可滚动的行树、路径栏和解析错误行，并自行绘制焦点框与行操作。主题负责设置这些部件的样式；键盘、搜索、分页和复制行为由控件本身负责。

### 结构

1. **工具栏**：当 `IsSearchable` 为 `True` 或文档超过一行时显示。它包含搜索框、匹配计数、上一个和下一个匹配按钮，以及全部展开和全部折叠按钮。
2. **搜索框**（`SearchPlaceholder`、`Query`）：高 34 px 的胶囊形区域，使用弱化的表面色，带搜索图标；默认占位文字为 "Search keys and values"。
3. **匹配计数**：当前是第 3 个匹配中的第 1 个时显示 `1/3`；没有匹配时显示 `0/0`。它是一个礼貌实时区域。
4. **树**（`MaxTreeHeight`）：行在此高度内滚动，默认为 420 px。每行高 30 px。
5. **行**：键（或数组索引）、冒号和值。字符串使用成功色，数字使用强调色，布尔值使用警告色；键使用前景色。分支以弱化色显示子项数量（`2 items`、`1 key`），折叠时还会显示预览，例如 `{ name, email }`。
6. **行操作**：行右侧的两个图标按钮，分别是复制值和复制路径。它们在悬停和当前行上出现，不在 Tab 顺序中。
7. **显示更多行**：在较长分支的末尾显示 "Show 50 more" 以及隐藏子项的数量。点击后加载下一页，每页 `PageSize` 个子项。
8. **解析错误行**（`ParseError`）：当 `Json` 不是有效的 JSON 时，代替树显示。
9. **路径栏**（`IsPathVisible`）：当前行的路径（等宽字体）、带子项数量的类型标签（`object, 5 keys`），以及复制路径按钮（`IsCopyable`）。查看器较窄时，类型标签隐藏。

### 加载数据

有三个属性可以加载文档。每个属性都会替换其他属性的文档，因此屏幕上显示的是最后设置的值。

| 属性 | 类型 | 查看器的处理方式 |
|---|---|---|
| `Json` | `string?` | 把文本解析为文档。无效文本会设置 `ParseError`，并让树保持为空。 |
| `Value` | `JsonNode?` | 设置时把节点序列化为文档。之后对节点的修改不会显示；需要再次设置 `Value`。 |
| `Element` | `JsonElement?` | 复制该元素，因此查看器接收之后，它所属的 `JsonDocument` 即可释放。 |

`RootName`（默认 `root`）是每条路径的第一段。`ExpandDepth`（默认 `1`）设置数据到达时展开的层数：`1` 只展开根节点，`2` 同时展开根节点的子项。

如果文本不是有效的 JSON，`ParseError` 会保存错误消息，树保持为空。

#### 已解析的数据

数据已经是 `JsonNode` 时，绑定 `Value`。在代码中用 `JsonNode` 的重载构建 `JsonNode` 值，这种写法对 AOT 是安全的。

```csharp
var items = new JsonArray();
items.Add((JsonNode)new JsonObject { ["id"] = 1, ["sku"] = "SKU-0001", ["inStock"] = true });

viewer.Value = new JsonObject
{
    ["count"] = 1,
    ["items"] = items
};
```

### 展开

`ExpandDepth` 设置首次显示时展开的层级，默认只展开根节点。`ExpandedPaths` 列出已展开的分支；它是双向的，因此视图模型可以保存展开的集合，并在之后恢复。集合变化后会触发 `ExpandedPathsChanged`。

```csharp
viewer.ExpandAll();
viewer.CollapseAll();
viewer.Expand("root.data.lines");
viewer.Collapse("root.data.customer");
```

设置 `Json`、`Value` 或 `Element` 会重新开始：展开集合按 `ExpandDepth` 重建，搜索重新执行，树滚动回顶部。若要在刷新后保留用户所在的位置，请在新文档到达之前读取 `ExpandedPaths`，并在之后重新设置它。

```csharp
viewer.ExpandedPaths = ["root", "root.data", "root.data.lines"];
```

### 搜索

`IsSearchable` 默认开启。`Query` 会按不区分大小写的方式过滤键和基本值，展开包含匹配项的分支，并在 `MatchCount` 中报告匹配数量。计数最多统计到 2,000 个匹配。输入查询时，`CurrentPath` 移动到第一个匹配项；`NextMatch` 和 `PreviousMatch` 按文档顺序逐个切换匹配项。

<Demo name="Search" />

```xml
<arc:JsonViewer Json="{Binding Payload}"
                Query="{Binding SearchText, Mode=TwoWay}"
                SearchPlaceholder="搜索键和值" />
```

```csharp
viewer.NextMatch();
viewer.PreviousMatch();
```

搜索处于活动状态时，如果人们关闭了某个分支，它会保持关闭，直到查询改变。查看器不会在下一次切换匹配时把它重新打开，因此人的选择优先于搜索。

### 长数组

`PageSize` 设置一个分支一次显示的子项数量，默认为 50，最小值为 1。"Show more" 行加载下一页，并报告仍被隐藏的子项数量。在该行上按 Enter 或 Space 同样会加载下一页，`FocusPath` 可以按路径把焦点移到任意一行。

<Demo name="LongArrays" />

```xml
<arc:JsonViewer Json="{Binding LargeCatalog}" PageSize="50" />
```

分页同时适用于数组和对象。页大小为 50 时，对于数千件商品的目录导出，首屏仍然很快。只有当人们需要一次扫视整页时才提高 `PageSize`；一页 500 行通常并不比十页 50 行更容易阅读。

### 路径与复制

每一行都有一个路径，描述其值在文档中的位置。路径以 `RootName` 开头，标识符形式的键使用点号，其他情况使用方括号。

| JSON 中的键 | 路径 |
|---|---|
| `data` | `root.data` |
| `$ok_1` | `root.$ok_1`（由字母、数字、`_` 和 `$` 组成，且不以数字开头的键） |
| `order-id` | `root["order-id"]` |
| `lines` 的第二项 | `root.lines[1]` |

`IsPathVisible` 在路径栏中显示当前行的路径，`IsCopyable` 启用复制操作。复制值时得到值的文本：字符串不带引号，分支复制为两个空格缩进的 JSON，其他值复制为字面量。复制路径时得到路径文本。

```csharp
viewer.RootName = "payload";
viewer.Copied += (_, e) => status.Text = $"已复制 {e.Kind}：{e.Path}";
viewer.CurrentRowChanged += (_, e) => current.Text = $"{e.Path} ({e.ValueType})";
```

`CurrentRowChanged` 在某一行成为当前行时触发（通过键盘或点击），并报告其路径、值和 `JsonViewerValueType`。`Copied` 报告复制的类型（`JsonViewerCopyKind.Value` 或 `JsonViewerCopyKind.Path`）、路径和复制的文本。用 `Copied` 生成状态消息或审计记录。

### 窄容器

宽度为 420 px 及以下（`CompactWidth`）时，缩进收紧为 12 px，键只占行的有限部分，以保证值仍然可见，路径栏中的类型标签隐藏。无需写代码，布局会随宽度自动调整。

<Demo name="Narrow" />

### 数据结构与性能

查看器只接受 JSON：文本、`JsonNode` 或 `JsonElement`。它不读取任意 CLR 对象，这让它对提前编译（AOT）和裁剪保持安全。绑定之前，用 `System.Text.Json` 序列化你的模型，或像上面那样手动构建 `JsonNode` 树。

- **行按视口创建。** 树是一个扁平的行列表，查看器只为可见的行创建实体。即使文档包含数千个对象，也可以打开（`ExpandAll`）并保持流畅滚动；分页上限让每个分支保持较小。
- **搜索在 UI 线程上读取整个文档。** 它遍历每一个键和基本值，并在 2,000 个匹配后停止报告。对于几兆字节的负载，请在视图模型的 `Query` 到达查看器之前做防抖处理（等待输入短暂停顿）。
- **值按原样显示。** 查看器不会格式化货币、日期或千位分隔符；请在查看器旁边的标签中自行格式化。

### 绑定到视图模型

在 MVVM 应用中，视图模型保存 JSON 文本和搜索查询，查看器负责显示它们。使用 CommunityToolkit.Mvvm 时，由 `ObservableObject` 保存状态和命令。

```csharp
public interface IWebhookService
{
    Task<string> GetLatestPayloadJsonAsync();
}

public partial class WebhookInspectorViewModel : ObservableObject
{
    private readonly IWebhookService _webhooks;

    public WebhookInspectorViewModel(IWebhookService webhooks)
    {
        _webhooks = webhooks;
    }

    [ObservableProperty]
    private string? _payloadJson;

    [ObservableProperty]
    private string? _query;

    [ObservableProperty]
    private string _status = "选择一个值以查看其内容。";

    [RelayCommand]
    private async Task LoadLatestAsync()
    {
        PayloadJson = await _webhooks.GetLatestPayloadJsonAsync();
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Button Content="加载最新投递"
                Command="{Binding LoadLatestCommand}" />
    <arc:JsonViewer Json="{Binding PayloadJson}"
                    Query="{Binding Query, Mode=TwoWay}"
                    ExpandDepth="2"
                    MaxTreeHeight="360" />
    <TextBlock Text="{Binding Status}" />
</StackPanel>
```

- `Json`、`Value`、`Element`、`RootName`、`ExpandDepth`、`PageSize`、`MaxTreeHeight`、`IsSearchable`、`IsCopyable` 和 `IsPathVisible` 是单向的：视图模型设置它们，查看器不会写回。
- `Query` 是双向的。在查看器的搜索框中输入会更新视图模型，视图模型也可以把 `Query` 设为空字符串来清除搜索。
- `ExpandedPaths` 是双向的，如"展开"一节所述。`CurrentPath` 和 `MatchCount` 是只读的，并随人的移动和搜索而更新。
- `Copied` 和 `CurrentRowChanged` 是事件，不是命令。在代码后置文件中把它们转发给视图模型：

```csharp
viewer.CurrentRowChanged += (_, e) => vm.Status = $"{e.Path} ({e.ValueType})";
viewer.Copied += (_, e) => vm.Status = $"已复制{(e.Kind == JsonViewerCopyKind.Path ? "路径" : "值")}：{e.Path}";
```

### 示例：Webhook 投递检查器

开发者从集成控制台打开一次投递。面板在树的上方显示事件名称及其时间。根节点及其第一层已展开，因此 customer 和行项目一眼可见；嵌套的 `metadata` 对象在开发者展开之前保持关闭。树的高度限制为 340 px，使面板可以与投递列表并排放置。

![Webhook 投递负载，根节点及其第一层已展开，并带有搜索工具栏](images/WebhookInspector.png)

```xml
<StackPanel Width="680" Spacing="12">
    <TextBlock Text="Delivery payload"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="invoice.paid  ·  evt_1Q2w3E4r5T  ·  delivered 2026-04-12 09:14 UTC"
               FontSize="13"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:JsonViewer ExpandDepth="2" MaxTreeHeight="340">
        <arc:JsonViewer.Json>{
  "id": "evt_1Q2w3E4r5T",
  "type": "invoice.paid",
  "created": 1775985240,
  "livemode": false,
  "data": {
    "customer": {
      "id": "cus_9s8d7f6g",
      "name": "Ada Lovelace",
      "email": "ada@example.com"
    },
    "lines": [
      { "description": "Pro plan", "amount": 4800, "quantity": 1 },
      { "description": "Additional seats", "amount": 1200, "quantity": 4 }
    ],
    "total": 9600,
    "currency": "usd",
    "discount": null,
    "metadata": {
      "order-id": "A-1042",
      "note": "Renewed early to keep the founding-member price."
    }
  }
}</arc:JsonViewer.Json>
    </arc:JsonViewer>
</StackPanel>
```

设计决策体现在数据和深度上。`ExpandDepth="2"` 展开根节点及其直接子项，因此 `data` 对象是展开的，开发者首先检查的字段位于下一层。`customer`、`lines` 和 `metadata` 分支保持关闭，每个分支的行上都有预览（`{ id, name, email }`、`[ … ]`），因此树读起来像一份摘要，直到有人需要细节为止。工具栏中的搜索覆盖了常见情况：在较长的负载中查找 `customer` 或 `amount`。

### 文案规范

查看器中的大部分文字来自控件本身（占位文字、计数、复制消息）。它周围的文字，即标题和状态，由你自己编写。

| 不要写 | 改为 |
|---|---|
| JSON | 投递负载、响应正文或事件数据 |
| 输出 | POST /v1/invoices 的响应 |
| 搜索... | Search keys and values（默认占位文字） |
| 根节点 | 团队文档中使用的名称，用 `RootName` 设置，例如 `event` |
| 已复制！ | 已复制 root.data.customer 的值（根据 `Copied` 事件生成的状态行） |

- 用文档的来源和事件为它命名，让支持工程师知道屏幕上是哪个负载。
- 标题和状态文字使用句子式大小写；你编写的文字中的计数使用阿拉伯数字。

### 最佳实践

**推荐**

- 把 `ExpandDepth` 设为能回答常见问题的层级，通常是 `2`。默认层级过深会让每个文档看起来都很繁杂，并掩盖人们要找的字段。
- 对于外部服务返回的数组，保持 `PageSize` 为默认的 50。较小的页面让每个分支足够短、便于扫视，而 "Show more" 行会说明还剩多少项。
- 对于几兆字节的负载，对 `Query` 做防抖处理，因为搜索会在 UI 线程上遍历整个文档。

**避免**

- 把 `Value` 绑定到 `JsonNode` 之后又原地修改该节点。查看器在设置时会复制节点，因此树不会更新；请用新节点再次赋值 `Value`。
- 默认对大型文档调用 `ExpandAll`。它会打开每个分支，包含数千个对象的负载会显示数千行需要扫视。
- 在共享屏幕上显示包含机密或个人数据的负载而不先做遮罩。查看器会显示它收到的每一个值。

### 常见问题

**修改 `JsonNode` 之后，树没有变化。** 设置 `Value` 时会序列化节点，之后对节点的修改不会被察觉。请用更新后的节点再次设置 `Value`，或构建一个新的 `JsonObject`。

**`Json`、`Value` 和 `Element` 该用哪一个？** 数据以文本形式到达时（例如 HTTP 响应正文）使用 `Json`。在代码中构建或解析了 `JsonNode` 时使用 `Value`。已经持有 `JsonElement` 时使用 `Element`；查看器会复制它，因此之后你可以释放 `JsonDocument`。

**刷新数据后，树为什么会收起？** 每个新文档都从 `ExpandDepth` 开始，因此人们展开的分支会再次关闭。在刷新之前保存 `ExpandedPaths`，并在新文档加载后重新设置它。

**如何用键盘复制值？** 用方向键选中行，然后按 Ctrl+C（macOS 上为 Cmd+C）复制它的值，或按 Ctrl+Shift+C（Cmd+Shift+C）复制它的路径。行操作按钮不在 Tab 顺序中，因此键盘快捷键是更可靠的途径。

## API 参考

<!-- api: JsonViewer -->

## 无障碍

- 树遵循 WAI-ARIA 树形模式：行面板暴露为树，每一行暴露其层级、在同级中的位置，以及展开或选中状态。
- 只有一行在 Tab 顺序中，即当前行。方向键、Home 和 End 在行之间移动焦点。
- 一行的朗读内容包含它的键和值，例如 `id: evt_1`；或它的键、类型和子项数量，例如 `lines, array, 2 items`。"Show more" 行朗读为 `Show 50 more, 70 hidden`。
- 搜索框的名称为 "Search JSON"。匹配计数是礼貌实时区域，因此计数的每次变化都会被播报，而焦点不会移动。
- 复制结果在礼貌状态区域中播报："Copied value of root.customer" 或 "Copy failed"。路径栏的复制按钮会把名称改为 "Copied" 或 "Copy failed"。
- 行操作按钮由控件绘制，不在 Tab 顺序中，通过 Ctrl+C 或 Ctrl+Shift+C 使用。路径栏的复制按钮和工具栏按钮是普通的 Tab 停靠点，名称分别为 "Expand all"、"Collapse all"、"Previous match"、"Next match" 和 "Copy path"。
- 悬停只改变行的填充色；当前行由高亮和路径栏标示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Up` / `Down` | 移到上一个或下一个可见行 |
| `Home` / `End` | 移到第一行（根节点）或最后一个可见行 |
| `Right` | 展开已关闭的分支；在已展开的分支上，移到它的第一个子项 |
| `Left` | 关闭已展开的分支；否则移到父行 |
| `Enter` / `Space` | 展开或关闭分支；在 "Show more" 行上，加载下一页 |
| `Ctrl+C` / `Cmd+C` | 复制当前行的值 |
| `Ctrl+Shift+C` / `Cmd+Shift+C` | 复制当前行的路径 |
| `/` | 聚焦搜索框并选中其中的文字 |
| `Enter` / `Shift+Enter`（在搜索框中） | 跳到下一个或上一个匹配 |
| `Escape`（在搜索框中） | 查询不为空时清除查询 |
| `Down`（在搜索框中） | 把焦点移入树，停在当前行 |
| `Tab` / `Shift+Tab` | 离开树或返回树；树只占一个 Tab 停靠点 |

## 动效

- 分支展开时，每一行都以平滑弹簧展开高度。加载一页和全部展开使用相同的动效。
- 当前行的高亮在行之间滑动；树会在行稳定后滚动到该行。鼠标滚轮的移动会停止仍在进行的滚动。
- 复制图标会以轻微的缩放和模糊变为对勾或叉号。
- 减弱动效时，行的变化立即生效，高亮和滚动直接移动到目标位置。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 分支展开或收起，或加载一页 | 每一行以平滑弹簧展开高度 | 行立即变化 | 立即完成 |
| 当前行移动 | 高亮在行之间滑动 | 高亮直接移动 | 立即完成 |
| 树滚动到当前行 | 弹簧式滚动到行稳定后的位置 | 直接跳转 | 立即完成 |
| 树中可见行超过 400 行 | 行立即变化 | 行立即变化 | 立即完成 |

## 相关组件

- `CodeBlock` 把整个 JSON 显示为可复制的源代码，提供一个复制操作。
- `TreeView` 用于人们选择而非检查的嵌套记录。
- `Table` 用于人们按列排序和筛选的同一结构记录。
- `CopyButton` 在树之外复制单个值。

| 组件 | 适用场景 |
|---|---|
| `CodeBlock` | JSON 是人们以文本形式复制的代码片段，例如示例请求 |
| `TreeView` | 嵌套项是人们选择的文件夹、文件或分类 |
| `Table` | 记录结构相同，人们按列比较它们 |
| `CopyButton` | 需要在旁边提供复制操作的单个值，例如 ID 或键 |
