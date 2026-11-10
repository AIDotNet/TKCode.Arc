---
title: Table of contents progress
summary: 长文章的目录：高亮正在阅读的章节，滚动时沿列表向下填满一条轨道，并可跳到任意章节；在窄屏上它会收成一个进度按钮。
source-hash: 78df3d5880e1
---

<Demo name="Preview" />

## 何时使用

`TocProgress` 放在长文章、指南或条款页面旁边，跟随读者的位置。正在阅读的章节使用完整的文字颜色，上面的章节显示为已读，下面的章节保持柔和。列表左侧有一条细轨道向下填充：它先到达当前章节的那一行，然后随着读者读完这一节在这一行里继续变长，所以列表同时也是一条进度条。标题旁边显示文章已读的百分比。选择一个章节，文章就会滚动到那里。

- **文档和指南**：人们在章节之间跳转，并想知道自己在哪里。
- **长篇阅读**：文章、报告和更新日志，剩下多少内容很重要。
- **政策和条款**：读者在找某一条，需要看到整体结构。

## 何时不用

- **只有一两屏的短页面**：列表没有帮助。
- **读者必须按顺序完成的步骤**：使用 `Stepper`，它显示完成情况，而不是位置。
- **页面之间的导航**：使用 `Tabs` 或侧边导航；这个列表只在一个页面内移动。
- **没有章节的信息流**：显示普通的 `Progress` 进度条，或者什么都不显示。

## 用法

`TocProgress` 位于 Special 包中：在 `UseArc()` 旁边用 `UseArcSpecial()` 注册它。把 `Target` 指向文章的 `ScrollViewer`，并用 `TocProgress.Heading` 标记其中的标题（子章节再加上 `TocProgress.Level` 2 或 3）。列表按文档顺序收集这些标记过的元素。

```xml
<Grid ColumnDefinitions="*,Auto" ColumnSpacing="32" Height="400">
    <ScrollViewer x:Name="Article">
        <StackPanel Spacing="12">
            <TextBlock Text="Install" arc:TocProgress.Heading="Install" FontSize="22" />
            <TextBlock Text="Add the package to your project." TextWrapping="Wrap" />
            <TextBlock Text="Options" arc:TocProgress.Heading="Options" arc:TocProgress.Level="2" FontSize="18" />
            <TextBlock Text="Every option has a sensible default." TextWrapping="Wrap" />
        </StackPanel>
    </ScrollViewer>
    <arc:TocProgress Grid.Column="1" Width="220" Target="{Binding #Article}" />
</Grid>
```

标题越过阅读线后就算到达了这一节，阅读线位于视口顶部往下四分之一处（`ActivationRatio`）。当文章无法再往下滚动时，即使最后一节的标题从没到达阅读线，最后一节也会成为当前章节。`ActiveIndex`、`Progress` 和 `ActiveChanged` 事件报告读者的位置，例如可以用来更新地址栏。

### 手动列出章节

当列表里的文字应当和标题不同，或者标题是在代码中生成的，可以添加 `TocProgressEntry` 子元素。每个条目指定自己的 `Title`、`Level` 和要滚动到的 `Target` 元素。

<Demo name="Entries" />

### 紧凑

`DisplayMode` 为 Compact 时只显示一个胶囊按钮，里面有一个进度环和当前章节的标题；点击后在弹出层中打开列表。默认的 Auto 会在视口比 `ArcViewport.CompactWidth`（640px）窄时切换到它。

<Demo name="Compact" />

### 减少动态效果

在减少动态效果下，轨道和文章直接跳到新位置；高亮、百分比和紧凑标题立即改变。

<Demo name="ReducedMotion" />

### 结构

1. **标题行**：小号中等字重的“On this page”，以及用等宽字体显示的已读百分比。
2. **轨道**：20px 槽位中一条 2px、使用边框颜色的轨道，强调色的填充，当前行旁边更亮的一段，以及填充末端一个 8px、带表面颜色外圈的圆点。
3. **行**：章节标题，最多两行，每深一级向内缩进 14px：未读时柔和，已读时为次要颜色，当前时为完整颜色和中等字重。
4. **紧凑按钮**：40px 的胶囊，带 20px 的进度环、当前标题和一个箭头。

### 内容建议

- 标题保持简短；列表在两行处换行，之后以省略号结束。
- 即使文章层级更多，列表中也最多使用两级；更深的层级会让轨道显得拥挤。
- 在最后一节下面留出足够的空间（文章末尾的内边距），让它的标题能到达阅读线。

## API 参考

<!-- api: TocProgress -->

<!-- api: TocProgressEntry -->

<!-- api: TocProgressLabels -->

<!-- api: TocProgressPosition -->

<!-- api: TocProgressActiveChangedEventArgs -->

## 无障碍

- 控件是一个名为“On this page”（`Labels.Title`）的导航区域。列表是一组链接：每一行以标题命名；当前行的帮助文字是“current section”，已读的行是“read”。进度通过轨道、文字颜色和百分比表达，从不只靠颜色。
- 只有一行在 Tab 顺序中（当前行，或最后获得焦点的行）。
- 紧凑按钮的名称包含当前章节和百分比，例如“Contents, Feeding every day, 42% read”。
- 键盘焦点显示 2px 的焦点环；指针焦点不显示。

### 键盘

| 按键 | 作用 |
|---|---|
| Tab | 进入列表（到当前行）或离开列表 |
| Up / Down | 移到上一行 / 下一行 |
| Home / End | 移到第一行 / 最后一行 |
| Enter / Space | 把文章滚动到该章节 |
| Escape | 关闭紧凑弹出层并回到它的按钮 |

## 动效

- 填充的末端以一个没有回弹、大约 0.3 秒停稳的弹簧跟随阅读位置；读者改变方向时它保留速度，所以快速滚动也不会卡顿。当前段以 snappy 弹簧移到新的一行。
- 选择章节时文章以 smooth 弹簧滚动；整个过程中被选中的行一直高亮，任何滚轮或滚动条操作都会立即接管。
- 紧凑标题用 Arc 的标签切换（旧的 100 毫秒淡出，新的 160 毫秒淡入并上升 2px）。
- 减少动态效果和关闭动效时直接跳到最终状态。

## 响应式

- 列表使用给它的宽度；标题换到两行后以省略号结束。
- Auto 模式下，视口窄于 `ArcViewport.CompactWidth`（640px）时列表变成紧凑按钮，空间足够时再变回来；弹出层宽 300px，并保持在视口内。
- 每行至少 30px 高，紧凑按钮 40px，都适合触摸操作。

## 相关组件

- `Progress`：普通的进度条。
- `Stepper`：显示用户按顺序完成的步骤进度。
- `ScrollArea`：带 Arc 滚动条的滚动容器，可以放文章。
- `Tabs`：在视图之间切换，而不是在一个视图内移动。
