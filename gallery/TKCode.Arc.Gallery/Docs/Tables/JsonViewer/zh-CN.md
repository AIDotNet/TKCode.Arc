---
title: JSON 查看器
summary: 可折叠的 JSON 树，支持搜索、长数组分页，以及复制值或路径。
source-hash: 5fc22f22ccd6
---

<Demo name="WebhookPayload" />

## 何时使用

- 检查 API 响应、Webhook 负载、日志条目或配置文件。
- 文档较大，用户需要搜索键或值，并只展开所需的分支。
- 展示服务数据的开发者工具与调试面板。

## 何时不用

- 将整个 JSON 文档作为可复制的源代码展示：用 `CodeBlock`。
- 用户需要选择的文件夹、文件或其他嵌套分类：用 `TreeView`。
- 非 JSON 的值，例如任意 .NET 对象：请先将其转换为 `JsonNode` 或 `JsonElement`。

## 用法

使用 `Json` 传入 JSON 文本。如果数据已经解析，可用 `Value` 传入 `JsonNode`，或用 `Element` 传入 `JsonElement`。

```xml
<arc:JsonViewer Json="{Binding ResponseJson}"
                ExpandDepth="2"
                MaxTreeHeight="320" />
```

### 已解析的数据

如果数据已经是 `JsonNode`，请绑定 `Value`。在代码中请使用 `JsonNode` 的重载来构建 `JsonNode` 值，这种方式对 AOT 是安全的。

```csharp
var items = new JsonArray();
items.Add((JsonNode)new JsonObject { ["id"] = 1, ["sku"] = "SKU-0001", ["inStock"] = true });

viewer.Value = new JsonObject
{
    ["count"] = 1,
    ["items"] = items
};
```

如果文本不是有效的 JSON，`ParseError` 会保存错误信息。

### 展开

`ExpandDepth` 设置初始时展开的层级数。默认仅展开根节点。`ExpandedPaths` 列出已展开的分支，可在代码中读取与设置。

```csharp
viewer.ExpandAll();
viewer.CollapseAll();
viewer.Expand("root.data.lines");
viewer.Collapse("root.data.customer");
```

### 搜索

`IsSearchable` 默认开启。`Query` 会筛选键与值，展开包含匹配项的分支，并在 `MatchCount` 中报告匹配数量。

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

### 长数组

`PageSize` 设置分支一次显示的子项数量。「显示更多」行会加载下一页。

<Demo name="LongArrays" />

```xml
<arc:JsonViewer Json="{Binding LargeCatalog}" PageSize="50" />
```

### 路径与复制

`RootName` 是每条路径的前缀，例如 `root.users[0].name`。`IsPathVisible` 显示路径栏，`IsCopyable` 启用复制功能。`CurrentRowChanged` 报告获得焦点的行，`Copied` 报告值或路径已被复制。

```csharp
viewer.RootName = "payload";
viewer.Copied += (_, e) => status.Text = $"已复制 {e.Kind}，位于 {e.Path}";
viewer.CurrentRowChanged += (_, e) => current.Text = $"{e.Path} ({e.ValueType})";
```

### 窄宽度

宽度为 420px 及以下时，缩进会收紧，路径栏不再显示类型。

<Demo name="Narrow" />

## API 参考

<!-- api: JsonViewer -->

## 无障碍

- 树遵循 WAI-ARIA 树形模式：每一行都会公开其层级、在同级中的位置，以及展开或选中状态。
- Tab 顺序中只有一行。朗读某一行时，会读出其键与值，或其类型与项数。
- 方向键、Home 与 End 在各行之间移动。Enter 或 Space 会展开分支或加载下一页。
- 按 Slash 将焦点移到搜索框。Enter 与 Shift + Enter 逐个切换匹配项，Escape 清除搜索。
- 匹配数量位于礼貌区域中。复制结果会在状态区域中播报。
- 行内复制按钮不在 Tab 顺序中。使用 Ctrl/Cmd + C 复制当前值，加上 Shift 则复制路径。

## 动效

- 分支展开时，每一行的高度以平滑弹簧展开。加载分页与全部展开使用相同的动效。
- 当前行的高亮在各行之间滑动，树会在高亮稳定后滚动到该行。
- 复制图标以轻微缩放与模糊转变为对勾或叉号。
- 减弱动效时，行的变化立即生效，高亮与滚动直接移到目标位置。关闭动效时直接跳到最终状态。

## 相关组件

- `TreeView`：用于用户选择而非检查的嵌套记录。
- `CodeBlock`：将整个 JSON 展示为可复制的源代码。
- `CopyButton`：复制树之外的单个值。
