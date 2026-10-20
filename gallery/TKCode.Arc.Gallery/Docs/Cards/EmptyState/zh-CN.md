---
title: 空状态
summary: 暂无内容时，给出清晰的下一步操作。
source-hash: 3cddac5cfe97
---

## 何时使用

- 空列表、空文件夹或首次使用的页面，且下一步很明确。
- 搜索或筛选没有结果时，提供修正的操作。
- 原位变化的状态，例如“暂无项目”变为“项目已创建”。

## 何时不用

- 数据仍在加载：用 `Skeleton`。
- 已有内容的页面出现错误：用 `Alert`。
- 卡片形态的内容或操作分组：用 `Card`。

## 用法

`Title` 与 `Description` 为必填项。需要行动号召时添加 `Action`。默认图标为文件夹。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:EmptyState Label="项目"
                    Title="暂无项目"
                    Description="项目会把你的文件、成员和讨论集中保存在一处。">
        <arc:EmptyState.Action>
            <arc:Button Content="新建项目" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</UserControl>
```

### 图标

`Icon` 可接受一个控件，或一份 24 × 24 的 Lucide 几何图形，例如 `ArcIcons` 中的某个图标。图标变化时，旧图标淡出，新图标弹入。

```xml
<arc:EmptyState Title="没有结果"
                Description="检查拼写，或尝试更宽泛的搜索词。"
                Icon="{Binding SearchIcon}" />
```

```csharp
empty.Icon = ArcIcons.Search;
```

### 操作区

`Action` 可接受一个按钮，也可接受包含多个按钮的面板。该区域在描述下方居中排列。

```xml
<arc:EmptyState Title="没有与“季度计划”相关的结果"
                Description="检查拼写，或尝试更宽泛的搜索词。"
                Icon="{Binding SearchIcon}">
    <arc:EmptyState.Action>
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Button Content="清除搜索" Variant="Secondary" />
            <arc:Button Content="浏览全部" Variant="Ghost" />
        </StackPanel>
    </arc:EmptyState.Action>
</arc:EmptyState>
```

### 状态切换

保持控件挂载，只修改其属性即可。标题与描述会滚动切换为新文案，高度以弹簧方式调整以适应新内容。将 `Action` 设为 `null` 可移除按钮。

```csharp
empty.Icon = ArcIcons.Check;
empty.Title = "项目已创建";
empty.Description = "准备好后，邀请团队成员或开始添加文件吧。";
empty.Action = null;
```

## API 参考

<!-- api: EmptyState -->

## 无障碍

- 该控件是一个由 `Label` 命名的区域地标。页面上每个空状态都应设置 `Label`。
- 标题为三级标题。描述紧随其后，位于阅读顺序中。
- 图标不向辅助技术公开，含义由文字承载。
- `Action` 中的按钮保留各自的焦点与键盘行为。

## 动效

- 新标题或描述带着柔和的模糊从下方升起，旧文字向上淡出。
- 文字区域以平滑弹簧改变高度，以适应新文案。
- 新的 `Icon` 弹入；图标区域首次出现时淡入并缩放。
- 减弱动效时，文字直接淡入切换，高度即时变化；图标区域不做入场动画。关闭动效时所有变化立即生效。

## 相关组件

- `Skeleton`：内容仍在加载时使用。
- `Alert`：已显示内容的页面中出现错误或提示时使用。
- `Card`：需要包含媒体、所有者与操作的网格项时使用。
