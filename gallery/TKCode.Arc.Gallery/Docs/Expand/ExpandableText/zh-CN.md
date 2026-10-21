---
title: 可展开文本
summary: 长文本截断为几行，底部带柔和渐隐；点击“阅读更多”平滑展开，点击“收起”折回。仅在文本溢出时显示切换按钮。
source-hash: 5b80f12913ab
---

<Demo name="Default" />

## 何时使用

- 评论、评价或个人简介，若全文显示会让列表中的卡片高度参差不齐。
- 固定宽度列中的长文本，读者可以选择是否阅读其余部分。
- 阅读全文并非必需的文字，例如商品描述。

## 何时不用

- 必须完整显示的文字，例如法律条款或错误详情。不要截断它们。
- 一组带标题的分节或问题：用 `Accordion`。
- 隐藏在摘要之后的结构化内容：用 `ExpandableCard`。
- 需要保持固定高度并带滚动条的文字：用 `ScrollArea`。

## 用法

用 `Text` 设置内容。文本会被截断为 `Lines` 行，默认为 3 行。只有当文本超出截断范围时，切换按钮才会出现。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandableText Text="我们在一个下午内就把整个设计团队迁移到了新的工作区。导入过程保留了每一条评论和每个版本。"
                        MaxWidth="520"
                        HorizontalAlignment="Left" />
</UserControl>
```

### 行数

`Lines` 设置文本折叠时显示的行数。评论等高密度列表使用 2 行；个人简介与商品描述使用 3 或 4 行。

<Demo name="TwoLines" />

```xml
<arc:ExpandableText Lines="2"
                    Text="{Binding CommentText}"
                    MaxWidth="420" />
```

### 标签

`MoreLabel` 与 `LessLabel` 设置切换按钮的文字。默认值为 `阅读更多` 和 `收起`。

```xml
<arc:ExpandableText MoreLabel="查看完整简介"
                    LessLabel="收起"
                    Lines="4"
                    Text="{Binding Bio}" />
```

### 绑定展开状态

`IsExpanded` 为双向绑定。可以将其绑定到视图模型属性，也可以在代码中设置。文本展开或收起时会触发 `ExpandedChanged`。

<Demo name="Controlled" />

```xml
<arc:ExpandableText Text="{Binding Bio}"
                    IsExpanded="{Binding BioExpanded, Mode=TwoWay}" />
```

### 文本能完整显示

只有当文本确实溢出时，切换按钮才会出现，并且会随宽度和字体的变化而更新。

<Demo name="Fits" />

## API 参考

<!-- api: ExpandableText -->

## 无障碍

- 完整文本始终位于无障碍树中。屏幕阅读器无需使用切换按钮即可读取全部内容。
- 切换按钮是一个按钮，遵循展开与收起模式。其名称为当前标签，即 `MoreLabel` 或 `LessLabel`。
- 截断部分中的链接可以通过键盘获得焦点。聚焦到位于截断线以下的隐藏链接时，文本会展开。
- 切换按钮支持 Enter 与空格键。

## 动效

- 高度、末行的渐隐与箭头沿同一条曲线一起运动。它们缓动收尾，不会过冲。
- 展开约需半秒，收起约需 0.4 秒。距离较长时耗时略长。
- 文本展开时，末行的渐隐随之消散。新出现的行穿过柔和的边缘升起。
- 动画进行中再次点击，会从当前高度反向运动。
- 减弱动效时，文本立即展开或收起。当文本在页面下方收起时，会将文本开头重新滚入视野。

## 相关组件

- `Accordion`：用于多个带标题的分节，在原位展开。
- `ExpandableCard`：用于带隐藏详细信息的摘要卡片。
- `ScrollArea`：用于必须保持固定高度的内容。
