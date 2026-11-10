---
title: Team showcase
summary: 团队介绍区块：生成的头像、姓名和职位，悬停、聚焦或轻点时在头像上显示简介，按部门筛选时网格会滑到新位置。
source-hash: b70414df9b0e
---

<Demo name="Preview" />

## 何时使用

`TeamShowcase` 在关于我们、招聘或公司页面上介绍产品背后的人。每个人是一张卡片，上面是方形头像、姓名和职位；指针移到卡片上、用键盘聚焦或轻点时，头像下半部分会浮出一段简短的介绍和所在城市。没有照片的人会得到一个生成的头像：用主题的图表颜色画出的头肩人像，由姓名决定，所以同一个人在哪里都长一样。网格上方的分段筛选一次只显示一个部门：其他人淡出，留下的卡片滑到新位置。

- **关于我们页面**，团队有五到三十人。
- **招聘页面**，按部门展示候选人将来的同事。
- **项目或工作室页面**，列出参与制作的人。

## 何时不用

- **可搜索、带联系方式的公司通讯录**：请使用 `TeamDirectory`。
- **在应用中管理成员和角色**：请使用 `TeamMembers` 或 `RolesPermissions`。
- **客户评价**：请使用 `StudioPerspectives`。

## 用法

```xml
<arc:TeamShowcase Title="The people behind Northwind" MaxColumns="4">
    <arc:TeamShowcaseMember Name="Mara Okonjo" Role="Head of Design" Department="Design" Location="Lisbon" Bio="Draws the first version of everything on paper." />
    <arc:TeamShowcaseMember Name="Teo Lindgren" Role="Staff Engineer" Department="Engineering" Location="Stockholm" Bio="Owns the sync engine." />
</arc:TeamShowcase>
```

没有成员时区块显示一个虚构的十人团队；把 `ShowsSampleTeam` 设为 false 则显示空网格。成员的 `Image` 会替换生成的头像，`Seed` 可换一个人像。

### 自己的团队

<Demo name="CustomTeam" />

部门来自成员，按首次出现的顺序排列；至少有两个部门时才显示筛选（`IsFilterVisible` 可隐藏它）。`SelectedDepartment` 是双向的：null 表示显示全部。`MemberInvoked` 报告在卡片上的点击、轻点、Enter 或 Space，同时会固定显示它的简介。

### 列数

只要每列不窄于 `MinColumnWidth`（200），网格就会增加列，最多 `MaxColumns`（4）列。

## API 参考

<!-- api: TeamShowcase -->

## 无障碍

- 区块以标题命名，标题是二级标题。
- 每张卡片都可聚焦，以人名命名；帮助文本依次读出职位、部门、简介和城市。
- 筛选变化后会播报显示的人数。被筛掉的卡片如果有焦点，焦点会移到筛选上。
- 头像只作装饰，对读屏隐藏。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 移到筛选，再依次经过各张卡片。 |
| 方向键 | 按行列在卡片之间移动。 |
| `Home` / `End` | 第一张或最后一张卡片。 |
| `Enter` / `Space` | 固定或取消固定简介。 |
| `Escape` | 取消固定简介。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 简介出现 | 160 毫秒淡入并以弹簧上升 8 像素；头像放大 3% | 淡入 | 立即 |
| 筛选移除卡片 | 160 毫秒淡出 | 淡出 | 立即 |
| 留下的卡片移动 | 以弹簧滑到新位置 | 直接跳到 | 直接跳到 |
| 筛选带回卡片 | 淡入并略微上升，每张间隔 35 毫秒 | 淡入 | 立即 |

## 响应式

- 断点按区块自身的宽度计算：小于 720（`:compact`）时标题变小、间距收紧；小于 480（`:narrow`）时边距再收紧，筛选占满宽度并可横向滚动。
- 列数随宽度变化（`MinColumnWidth`、`MaxColumns`）；在手机上是一到两列。
- 轻点也能显示简介，所以触屏同样可以阅读。

## 相关组件

- `TeamDirectory`：带个人资料的可搜索通讯录。
- `StudioPerspectives`：客户评价。
- `SegmentedControl`：部门筛选就是它。
