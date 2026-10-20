---
title: 图片对比
summary: 拖动分隔线横跨两张图片，查看变化之处。
source-hash: bc837e17cbef
---

## 何时使用

- 展示同一张图片的前后对比，例如修图、重新设计或清理效果。
- 对比两个结果，由读者决定揭示变化的程度。
- 上下对比：将 `Orientation` 设为 `Vertical`。

## 何时不用

- 浏览多张图片：用 `Carousel`。
- 拆分实时内容，例如应用中的两个面板：用 `ResizablePanels`。
- 并排比较记录：用 `SortableDataTable`。

## 用法

两侧应使用相同的裁剪与尺寸，以便揭示的部分对齐。

```xml
<arc:ImageCompare Width="600" AspectRatio="1.5">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### 标签与外框

`BeforeLabel` 与 `AfterLabel` 为两侧添加说明文字。分隔线经过某个说明时，该说明会淡出。`ShowLabels` 可隐藏两个说明文字。`AspectRatio` 设置外框的宽高比（宽度除以高度），默认值为 1.5（3 / 2）。

```xml
<arc:ImageCompare AspectRatio="1.7777" ShowLabels="False" Position="30" Label="修图预览">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#8AA399" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### 上下对比

```xml
<arc:ImageCompare Orientation="Vertical" BeforeLabel="原图" AfterLabel="调色后">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

运行时修改 `Orientation` 会让分隔线旋转四分之一圈，揭示区域随之移动。

### 图片

`BeforeSource` 与 `AfterSource` 会在每一侧的内容之上绘制图片。图片在后台加载，并从轻微模糊中淡入。如果加载失败，则显示下方的内容。

```csharp
var compare = new ImageCompare
{
    Before = new Border { Background = Brushes.Gray },
    After = new Border { Background = Brushes.SteelBlue },
    BeforeSource = ImageSource.Parse("https://example.com/before.jpg"),
    AfterSource = ImageSource.Parse("https://example.com/after.jpg")
};
```

### 位置

`Position` 是分隔线的位置，以百分比计，从左侧起算；垂直时从顶部起算。它是双向的，默认值为 50。`PositionChanged` 报告已确定的值：拖动时每变化一个整百分点报告一次，松开时再报告一次。

```csharp
compare.PositionChanged += (_, e) => readout.Text = $"位置 {e.Position:0.#}%";
compare.Position = 75;
```

## API 参考

<!-- api: ImageCompare -->

## 无障碍

- `Label` 为分隔线手柄命名，默认值为“前后对比”。
- 手柄是可获得焦点的滑块。其值为后图所占的比例，例如“后图 40%”。
- 方向键每次移动分隔线 1%，按住 Shift 再按方向键移动 10%，PageUp 与 PageDown 每次移动 10%。
- Home 与 End 分别完整显示后图或前图；在手柄上双击会使其回到中间。
- 焦点环在按下按键后出现，而不是在焦点随指针移动时出现。

## 动效

- 拖动时分隔线与指针一比一跟随，越过边缘时产生阻尼，松开后停留在当前位置。拖动时手柄会拉伸成胶囊形状。
- 分隔线经过某个说明时，该说明淡出。按键推向边缘时会产生短促的回弹。
- 减弱动效时，分隔线与手柄直接定位，不使用弹簧或边缘阻尼。关闭动效时立即到达终态。

## 相关组件

- `Carousel`：浏览一组图片或幻灯片，而不是比较两张。
- `CardStack`：逐一浏览条目并对每一项做出决定。
- `Slider`：在没有图片的轨道上选择数值。
