---
title: 代码块
summary: 以清晰的层级呈现代码，并提供便捷的复制入口。
source-hash: 134c97dd9bb2
---

<Demo name="Default" />

## 何时使用

- 文档或设置页面中的源代码片段与安装命令。
- 较长的文件，需要折叠为几行，并提供「显示全部」操作。
- 用户需要复制的代码，文件名能告诉他们代码应放在哪里。

## 何时不用

- 只复制一个值，例如 API 密钥：单独使用 `CopyButton`。
- 用户需要按键名逐级浏览的 JSON：用 `JsonViewer`，它以树形展示并支持搜索。
- 可导航的文件夹与文件名层级：用 `TreeView`。

## 用法

```xml
<arc:CodeBlock FileName="greeting.tsx"
               Language="tsx"
               Code="{Binding Source}" />
```

### 折叠长代码

`IsCollapsible` 会添加一个切换按钮。`MaxLines` 设置代码块折叠时显示的行数。

<Demo name="Collapsible" />

`IsExpanded` 为双向绑定，表示代码块是否展开。

```csharp
block.IsCollapsible = true;
block.MaxLines = 6;
block.IsExpanded = false;
```

### 语言

`Language` 设置语法高亮以及标题栏中的标签。它接受常见名称与别名，例如 `cs`、`ts`、`py`、`sh` 和 `axaml`。没有对应语法的语言会以纯文本显示。

<Demo name="Languages" />

```xml
<arc:CodeBlock FileName="stats.py" Language="py" Code="{Binding PythonSource}" />
<arc:CodeBlock FileName="notes.txt" Language="text" Code="纯文本。" />
```

### 复制与行号

复制按钮位于标题栏，复制的是 `Code`。`ShowLineNumbers` 会添加行号列，代码横向滚动时行号保持不动。

<Demo name="LineNumbers" />

```xml
<arc:CodeBlock FileName="deploy.sh"
               Language="bash"
               ShowLineNumbers="True"
               Code="{Binding Script}" />
```

```csharp
block.Copied += (_, _) => status.Text = "代码已复制";
```

### 标签文字

复制标签与折叠文字都是属性，因此可以直接翻译。

```xml
<arc:CodeBlock Code="{Binding Source}"
               CopyLabel="复制代码片段"
               ShowAllLinesFormat="显示全部 {0} 行"
               ShowFewerLinesLabel="收起部分行" />
```

### 切换文件

同时设置新的 `FileName`、`Language` 和 `Code`，即可就地换入另一个文件。文件名所在的位置会跟随新的宽度，新的代码上浮出现，高度以弹簧动画跟随。

<Demo name="SwitchingFiles" />

### 窄宽度

宽度低于 420px 时，内边距和文字会缩小，复制操作只保留图标。

<Demo name="Narrow" />

## API 参考

<!-- api: CodeBlock -->

## 无障碍

- 代码块是一个带标签的区域。其名称来自 `FileName`，因此页面上的每个代码块都有各自的名称。
- Tab 依次移动到复制按钮、代码区域和展开切换按钮。
- Enter 或 Space 会复制代码或切换展开状态，取决于当前获得焦点的控件。
- 代码区域可以获得焦点，键盘用户可通过方向键滚动较长的行。
- 展开切换按钮会向辅助技术公开其展开状态。复制结果由 `CopyButton` 播报。

## 动效

- 展开或收起时，代码的高度以平滑弹簧变化。
- 新代码会淡入并轻微上升，使更新清晰可见，而不会突然跳动。
- 切换按钮的文字以短暂的模糊切换，箭头随之转动。
- 减弱动效时，高度立即设定，文字原位切换，没有移动。关闭动效时直接跳到最终状态。

## 相关组件

- `CopyButton` 用于复制单个值，例如令牌或 URL。
- `JsonViewer` 用于用户需要搜索和浏览的 JSON。
- `TreeView` 可放在代码块旁边，让所选文件决定显示的代码。
