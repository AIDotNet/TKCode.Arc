---
title: Markdown
summary: 以 Arc 的风格渲染 Markdown 文档与流式回复，代码块使用 Arc 的语法配色高亮。
source-hash: 25cd452aaa53
---

## 何时使用

- 以 Markdown 形式存储的发布说明、帮助文本与文档。
- 来自模型或其他服务的流式回答，需要在文字到达时即时显示。
- 来源不可控的富文本，需要保证标题、列表、表格与链接的外观一致。

## 何时不用

- 带复制按钮的单个代码块：用 `CodeBlock`。
- 需要自身滚动的长文档：将控件包裹在 `ScrollViewer` 中，或在长对话中使用 StreamMark 的 `TranscriptView`。
- 原位编辑 Markdown 源码：用 `Textarea`。

## 用法

静态文本通过 `Text` 设置。文档高度随内容增长，内部没有滚动。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Markdown Text="## 发布说明&#10;&#10;Arc **0.4** 新增 `Markdown`。" />
</UserControl>
```

### 绑定文档

将 `Text` 绑定到视图模型属性。设置该属性会替换当前仍在显示的回复。

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" FontSize="14" BlockSpacing="10" />
```

### 流式回复

每个数据块到达时调用 `Append`，回复结束时调用 `Complete`。文字会按节奏逐步显示，新词以淡入方式出现。`IsStreaming` 在节奏化的待显示内容全部展示之前保持为 `true`。

```csharp
markdown.Append(chunk);   // 词元到达时调用，在 UI 线程上执行
markdown.Complete();      // 回复结束时调用
```

`Complete` 之后再调用 `Append` 会开始新的回复。若由你自己创建流，则可以改为设置 `Stream`，例如交给工作线程通过 `MarkdownStream.Push` 写入。

### 聊天尺寸

消息气泡可使用较小的 `FontSize`。`LineHeight` 默认为字号的 1.65 倍，代码字号随之变化。

```xml
<arc:Markdown FontSize="14"
              Text="已将 **web-42** 部署到 `staging`。&#10;&#10;```sh&#10;kubectl rollout status deploy/web&#10;```" />
```

### 链接

绝对路径的 `http`、`https` 与 `mailto` 链接会通过系统启动器打开。处理 `LinkClicked` 即可接管，并将事件标记为已处理以阻止默认行为。设置 `OpensLinks="False"` 可关闭默认行为。

```csharp
markdown.LinkClicked += (_, e) =>
{
    e.Handled = true;
    OpenInsideApp(e.Url);
};
```

## API 参考

<!-- api: Markdown -->

## 无障碍

- 文字可以选中并复制。链接带下划线，并使用强调色。
- 代码块使用与 `CodeBlock` 相同的配色。颜色用于辅助源文本，从不替代源文本。
- 在流式回复尚未结束时，使用 `IsStreaming` 显示状态提示。

## 动效

- 流式回复中的新文字在 240 毫秒内淡入。节奏控制会将文字分散开来，避免成批涌现。
- 减弱动效时，淡入缩短为 160 毫秒，且只使用不透明度变化。关闭动效时，新文字立即显示，没有淡入。
- 节奏控制在所有动效级别下都生效。它控制文字何时出现，而不是一段动画。

## 相关组件

- `CodeBlock`：单独展示一段代码示例。
- `Textarea`：用户需要编辑多行文本时使用。
