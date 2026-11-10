---
title: Glass tab bar
summary: 一个用于应用外壳的浮动底部磨砂玻璃标签栏，选中的透镜在标签之间滑动并拉伸。
source-hash: 27dce94b9c0d
---

<Demo name="Preview" />

## 何时使用

`GlassTabBar` 是手机风格应用外壳的主导航：一条浮在底边上方、覆盖在内容之上的磨砂玻璃胶囊，放三到五个目的地。内容在它下面继续滚动，并模糊地透出来，所以标签栏不会把页面截断。每个标签是图标加下方的短文字，可以带一个徽标。选中的标签位于一块更亮的玻璃透镜中，透镜以前沿先行的方式滑到下一个标签，途中被拉长，选中的图标使用强调色。按住并拖动透镜，可以在标签之间来回滑动。

- **手机应用的根导航**：Home、Search、Saved、Profile。
- **窄窗口或手机预览中的紧凑应用外壳**。
- **媒体或地图应用**：内容要在标签栏下面一直延伸到底边。

## 何时不用

- **宽的桌面布局**：使用侧边栏或顶部的 `Tabs`。
- **超过五个目的地**：把其余的放进“更多”页面，或使用侧边栏。
- **在一个页面内切换视图**：使用 `GlassSegmented` 或 `Tabs`。
- **操作**：标签是去往某处的；操作请放在工具栏或浮动按钮中。

## 用法

把标签栏放在页面上方的底部，并把 `arc:GlassBackdrop.Source` 设为滚动的内容，这样标签栏会模糊经过它下方的那一部分。在内容末尾留出空间（大约标签栏的高度加上它的外边距），让最后一项能滚动到标签栏上方。

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Feed}">
    <ScrollViewer>
        <StackPanel Name="Feed" Margin="16,16,16,110"><!-- the page --></StackPanel>
    </ScrollViewer>
    <arc:GlassTabBar Margin="0,0,0,18">
        <arc:GlassTab Content="Home" Icon="{x:Static arc:ArcIcons.House}" />
        <arc:GlassTab Content="Search" Icon="{x:Static arc:ArcIcons.Search}" />
        <arc:GlassTab Content="Saved" Icon="{x:Static arc:ArcIcons.Bookmark}" Badge="" />
        <arc:GlassTab Content="Profile" Icon="{x:Static arc:ArcIcons.User}" />
    </arc:GlassTabBar>
</Panel>
```

总有一个标签被选中；绑定 `SelectedIndex` 或处理 `SelectionChanged` 来切换页面。`GlassTabBar` 派生自 Avalonia 的 `SelectingItemsControl`，位于 Special 包中：在 `UseArc()` 旁调用 `UseArcSpecial()` 注册。内容滚动时，它相对标签栏移动，所以标签栏下的副本会重新拍下。磨砂效果如何用公开的 Avalonia API 实现，见 Glass card 页面。

### 只显示图标与徽标

`ShowLabels="False"` 得到一条只有图标、更矮的标签栏；文字仍是各标签的无障碍名称。`Badge` 在标签图标上加一个标记：空字符串是一个圆点（“有新内容”），其他文字是一个小数字（“3”“9+”），null 表示没有。

<Demo name="IconsOnly" />

### 手势

| 输入 | 效果 |
|---|---|
| 点击或点按一个标签 | 选中它；透镜滑过去 |
| 按住选中的标签 | 透镜鼓起 |
| 从选中的标签开始拖动 | 透镜跟随手指在标签栏上移动 |
| 拖动后松开 | 选中离透镜最近的标签 |

### 内容建议

- 使用一个词的文字，以及同一套清楚的线条图标。
- 只给需要注意的标签加徽标：新内容用圆点，待处理的事项用数字。
- 每个页面上保持相同的标签和顺序。

## API 参考

<!-- api: GlassTabBar -->

<!-- api: GlassTab -->

## 无障碍

- 标签栏是一个标签列表，每个标签以文字为名称并带有选中状态；徽标在名称之后朗读（“Saved, new”“Inbox, 3”）。
- 只有选中的标签在 Tab 顺序中；方向键同时移动选择和焦点。
- 键盘焦点在获得焦点的标签周围显示 2px 的焦点环；指针操作不显示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 进入标签栏（选中的标签）或离开 |
| `Right` / `Down` | 下一个标签，循环（从右到左的布局中方向相反） |
| `Left` / `Up` | 上一个标签，循环 |
| `Home` / `End` | 第一个或最后一个标签 |

## 动效

- 透镜的前沿以约 0.28 秒、略有弹性的弹簧移动，后沿以约 0.46 秒、更平静的弹簧移动，所以它朝新标签拉长，再收回到它的宽度。
- 按住时，透镜在每一边鼓起 3px；拖动时以紧弹簧跟随指针。
- 图标和文字颜色在 160ms 内变化。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选择变化 | 透镜滑动并拉伸 | 透镜在新标签淡出再淡入 | 透镜立即移动 |
| 按住和拖动 | 透镜鼓起并跟随 | 透镜跟随，不鼓起 | 透镜跟随，不鼓起 |
| 指针下的光泽 | 跟随指针 | 无 | 无 |

## 响应式

- 各标签平分标签栏的宽度，每个至少 64px；标签栏与标签一样宽并居中。在很窄的外壳中，设置 `HorizontalAlignment="Stretch"` 和两侧外边距，让标签平分整个宽度。
- 长文字以省略号结尾，而不是把标签撑宽。
- 调整大小时，透镜直接落到它的标签上，不播放动画。

## 相关组件

- `Tabs`：宽布局中页面内的视图。
- `GlassSegmented`：在玻璃上切换视图。
- `GlassCard` 和 `GlassMenu`：其他玻璃表面。

| 组件 | 适用场景 |
|---|---|
| `GlassTabBar` | 手机风格应用外壳的根导航 |
| `GlassSegmented` | 在一个屏幕内切换少量视图或模式 |
| `Tabs` | 桌面布局中页面内的视图 |
