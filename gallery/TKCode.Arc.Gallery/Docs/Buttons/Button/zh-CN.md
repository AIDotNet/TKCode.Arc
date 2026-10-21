---
title: 按钮
summary: 清晰、响应迅速的操作按钮，次要状态保持低调。
source-hash: e419533863ca
---

<Demo name="Variants" />

## 何时使用

- 页面、表单或对话框中的任何单一操作，如保存、继续或取消。
- 文字会原地变化的操作，如从“保存”变为“已保存”，宽度同时以弹簧动画适配。
- 需要加载指示的短时异步操作，按钮在此期间保持键盘焦点。

## 何时不用

- 需要自行呈现等待和成功状态的按钮：用 `ActionButton`。
- 同一操作的两到五个相近选项：用 `SplitButton`。
- 需要用户刻意按住才能执行的破坏性操作：用 Gestures 中的 `HoldToConfirm`。
- 需要保持选中状态的值：用 Toggles 中的 `SegmentedControl`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Content="保存更改" Variant="Primary" />
</UserControl>
```

### 变体

`Variant` 决定视觉权重。每个界面只使用一个 `Primary` 操作。`Danger` 以危险色描边标记破坏性操作。

```xml
<arc:Button Content="保存更改" Variant="Primary" />
<arc:Button Content="取消" Variant="Secondary" />
<arc:Button Content="了解更多" Variant="Ghost" />
<arc:Button Content="删除项目" Variant="Danger" />
```

### 尺寸

`Size` 决定最小高度：`Small` 为 36，`Medium` 为 44（默认值），`Large` 为 50。尺寸不会随窗口宽度变化。

<Demo name="Sizes" />

### 加载与文字变形

设置 `IsLoading` 即可显示加载指示。按钮保持键盘焦点，但在任务结束前会忽略点击和按键。修改 `Content` 即可切换文字：新文字向上浮入，宽度以弹簧动画适配。

<Demo name="LoadingMorph" />

```xml
<arc:Button Content="{Binding SaveLabel}"
            IsLoading="{Binding IsSaving}"
            Variant="Primary" />
```

### 仅图标按钮

仅图标按钮没有可见文字，因此需要提供无障碍名称。

```xml
<arc:Button Size="Small" Variant="Ghost"
            AutomationProperties.Name="关闭">
    <TextBlock Text="×" />
</arc:Button>
```

### 禁用

将 `IsEnabled` 设为 `False`。描边变体保持其形状，因此不可用的操作仍然像按钮。

<Demo name="Disabled" />

## API 参考

<!-- api: Button -->

## 无障碍

- 按钮具有 Button 角色。仅图标按钮需要 `AutomationProperties.Name`。
- 设置 `IsLoading` 后，按钮仍可获得焦点，并向辅助技术报告自身处于忙碌且不可用的状态。
- Enter 和 Space 均可激活按钮。加载期间，点击和按键会被忽略。
- 键盘焦点显示 2px 的焦点环，与按钮相距 2px。指针聚焦时不绘制焦点环。

## 动效

- 按下时内容轻微缩小，松开后以弹簧动画回弹。窄的图标按钮缩放幅度稍大。
- 文字变化时从柔和的模糊中升起，宽度以弹簧动画过渡到新文字的宽度。加载期间，加载指示持续旋转。
- 减弱动效时，移除按下缩放和宽度弹簧。文字与加载指示以短暂的交叉淡化切换，加载指示停止旋转。关闭动效时，文字与状态立即替换。

## 相关组件

- `ActionButton`：用于异步提交，并在按钮上显示等待和成功状态。
- `SplitButton`：用于带有相近备选操作菜单的主要操作。
- `ButtonGroup`：用于将多个相关操作组合在同一个容器中。
- `CopyButton`：用于复制一个值并显示确认状态。
