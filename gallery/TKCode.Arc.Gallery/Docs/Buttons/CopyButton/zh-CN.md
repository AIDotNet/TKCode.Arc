---
title: 复制按钮
summary: 复制一个值，并立即给出确认反馈。
source-hash: 15f86ad86384
---

<Demo name="Outline" />

## 何时使用

- 用户经常复制的值，例如 API 密钥、安装命令或链接。
- 代码片段和行内值，确认反馈应出现在原位。
- 空间紧凑、只需图标即可完成复制的行。

## 何时不用

- 除复制值外还需要其他格式，例如复制链接或以 Markdown 复制：用 `SplitButton`。
- 完整的代码示例，且自带复制控件：用 `CodeBlock`，它已经包含一个复制控件。

## 用法

把 `Value` 设为要写入剪贴板的文本。`Label` 是可见文字，同时也是无障碍名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CopyButton Value="npx arc add copy-button" />
</UserControl>
```

### 自定义标签

`Label` 设置空闲状态的文字。`CopiedLabel` 与 `FailedLabel` 设置复制后显示的文字。标签区域会预留三者中最宽的宽度，因此按钮的宽度永远不会变化。

```xml
<arc:CopyButton Value="sk_live_51H..."
                Label="复制密钥"
                CopiedLabel="密钥已复制"
                FailedLabel="无法复制" />
```

### 纯图标与简约样式

`IsIconOnly` 会隐藏标签，但标签仍作为无障碍名称。`Variant="Plain"` 去掉外框，适合紧凑的行和代码块。

<Demo name="Plain" />

```xml
<arc:CopyButton Value="https://uiarc.dev" IsIconOnly="True" />
<arc:CopyButton Value="#7C5CFF" Label="复制 HEX 值" Variant="Plain" />
```

### 绑定值

把 `Value` 绑定到应被复制的文本。当值为 `null` 时复制会失败，按钮显示失败状态。

<Demo name="Failure" />

```xml
<arc:CopyButton Value="{Binding InstallCommand}" Label="复制命令" />
```

### 重置时间与事件

按钮会在 `ResetAfter` 之后恢复空闲状态，默认为 1.9 秒。复制成功后会触发 `Copied`。`State` 的取值为 `Idle`、`Copied` 或 `Error`。

```csharp
copy.Copied += (_, _) => log.Text = "已复制到剪贴板。";
copy.ResetAfter = TimeSpan.FromSeconds(3);
```

## API 参考

<!-- api: CopyButton -->

## 无障碍

- 无障碍名称始终是 `Label`，即使按钮只显示图标。
- 一个隐藏的礼貌状态区域会播报结果，例如"复制密钥：已复制"或"复制密钥：无法复制"。
- Enter 与 Space 执行复制。
- 标签区域保留其最宽状态的宽度，因此按钮周围的布局不会移动。
- 键盘焦点显示标准的焦点环；指针聚焦时不绘制焦点环。

## 动效

- 图标以柔和的模糊切换，并以缓慢、接近临界阻尼的弹簧缩放进入。对勾会自行绘制出来。
- 只有标签中变化的字母会移动；按钮恢复空闲时，这些动画会反向播放。
- 按下时内容会轻微缩小，松开时以弹簧弹回。
- 减弱动效时，图标与文字使用瞬时交叉淡化，跳过描边绘制和按下缩放。关闭动效时立即替换图标与标签。

## 相关组件

- `SplitButton`：复制只是多个操作之一时使用，例如复制页面，并提供复制链接与以 Markdown 复制。
- `ActionButton`：异步提交，带有等待与成功状态。
- `Button`：没有剪贴板结果的普通操作。
- `CodeBlock`：已包含复制控件的代码示例。
