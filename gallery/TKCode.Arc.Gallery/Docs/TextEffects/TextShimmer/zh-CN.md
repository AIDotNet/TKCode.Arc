---
title: 文字闪光
summary: 用平缓的光带扫过文字，表示正在进行的工作。
source-hash: eb40bb2519c4
---

## 何时使用

- 应用忙碌时的状态标签，例如"思考中"或"正在生成摘要"。
- 随工作推进而变化的状态行，例如"正在读取 14 个文件"与"正在起草计划"。
- 工作结束后沉淀为完成状态的标签。

## 何时不用

- 页面加载时的占位内容：用 `Skeleton`。
- 可以度量的进度：用 `Progress`，它显示已完成的比例。
- 逐字母变化且没有扫光的标签：用 `TextMorph`。
- 长文本或多行文字：扫光仅适用于一行简短文字。

## 用法

只要 `IsActive` 为 `true`（默认值），扫光就会持续进行。工作完成后将 `IsActive` 设为 `false`，文字随即沉淀为实色。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextShimmer Text="思考中" />
</UserControl>
```

### 更改文字

新的 `Text` 值会在原位升起，同时该行以弹簧方式调整到新宽度。步骤变化期间请保持 `IsActive` 为开启状态。

```csharp
summary.IsActive = true;
summary.Text = "正在起草计划";
```

### 完成

请将 `IsActive` 设为 `false`，而不是移除控件。扫光会平滑退场，文字变为实色，因此该行不会突然跳动。

```csharp
summary.Text = "摘要已就绪";
summary.IsActive = false;
```

### 扫光速度

`SweepDuration` 设置光带扫过文字一次所需的时间。默认值为 1.8 秒。

```xml
<arc:TextShimmer Text="正在为工作区建立索引"
                 SweepDuration="0:0:3" />
```

### 尺寸

文字使用 `TextElement` 的字体属性。较大的字号适用于状态标题。

```xml
<arc:TextShimmer Text="正在生成摘要" FontSize="22" />
```

## API 参考

<!-- api: TextShimmer -->

## 无障碍

- 文字即为控件的名称，并始终保持可读。
- 只要 `IsActive` 为 `true`，控件就会通过 `AutomationProperties.ItemStatus` 报告忙碌状态。
- 扫光仅为装饰，不会被逐字母朗读。
- 控件本身不播报变化。需要播报完成状态时，请在父元素上使用实时区域。
- 本控件不可获得焦点，也没有键盘交互。

## 动效

- 一条文字颜色的柔和光带掠过较暗的底色。每次扫过耗时 `SweepDuration`，随后光带短暂停留，再开始下一次扫过。
- 扫光仅在控件处于激活、接近视口且可见时运行，并从停止的位置继续。
- 新标签从柔和的模糊中升起。
- 将 `IsActive` 设为 `false` 时，光带平滑退场，文字变为实色。
- 减弱动效时停止扫光，标签以普通淡入淡出切换。关闭动效时立即切换标签。

## 相关组件

- `Skeleton`：页面加载时的占位内容。
- `Progress`：可以度量的工作进度。
- `TextMorph`：简短标签的状态切换。
