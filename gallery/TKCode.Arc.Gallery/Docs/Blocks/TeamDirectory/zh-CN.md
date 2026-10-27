---
title: Team directory
summary: 带搜索、团队筛选的公司通讯录，所选成员的个人资料显示在列表旁，从简介和状态到可复制的电子邮件地址。
source-hash: 5e6a845c52f5
---

<Demo name="Preview" />

## 何时使用

`TeamDirectory` 是同事互相查找的页面。左侧的列表会随着输入和所选团队而缩小范围；右侧完整显示一个人：一段简短的简介、是否在线、所属团队、正在做的工作、所在地点，以及带复制按钮的电子邮件地址。

- **内部人员页面。** 公司或某个部门的所有人，可按姓名、角色、团队或城市搜索。
- **带部门的团队页面。** 团队按钮一键查看各个部门，并显示固定的人数。
- **显示上下文的联系人选择器。** 选择一个人，了解他是谁并复制他的地址，或响应 `PersonSelected` 在别处打开其完整个人资料。

## 何时不用

- **管理成员。** 更改角色、邀请或移除成员属于 Team members 或 Roles and permissions；本 Block 只读。
- **面向公众的团队展示。** 带大幅肖像和简短引言的营销页面需要以图片为主的布局，而不是可搜索的列表。
- **大型人员表格。** 数百行并带排序和多列的场景应使用 `DataGrid`。通讯录在内存中筛选并布局每一行；对于大型组织，请在服务器端筛选，只给本 Block 一页人员。

## 用法

本 Block 派生自 Avalonia 的 `TemplatedControl`。每个人是一个 `TeamDirectoryPerson`，它是一个 `AvaloniaObject`，包含姓名、角色、团队、可用状态、当前工作、带时区的城市、电子邮件地址、简介和可选的肖像（没有肖像时显示首字母）。更改任一字段都会更新该行、个人资料和匹配结果。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TeamDirectory x:Name="Directory">
        <arc:TeamDirectoryPerson Id="lena" Name="Lena Hartmann" Role="Interaction designer" Team="Design"
                                 Focus="Checkout redesign" City="Vienna" TimeZone="CET"
                                 Email="lena@northwind.test"
                                 Bio="Turns rough ideas into flows people can follow." />
        <arc:TeamDirectoryPerson Id="theo" Name="Theo Brandt" Role="Mobile engineer" Team="Engineering"
                                 Availability="Away" City="Manchester" TimeZone="GMT"
                                 Email="theo@northwind.test" />
    </arc:TeamDirectory>
</UserControl>
```

### 构成

- **标题栏**：标题和人数（始终是总数），以及带清除按钮的搜索框。
- **团队按钮**：先是 All，然后是每个团队及其总人数。一个柔和的胶囊位于被按下的按钮之后，并滑向下一个按钮。
- **列表**：每个匹配项一行：带可用状态圆点的头像、姓名和角色。正在显示个人资料的那一行位于一块高亮之上，高亮会在行与行之间滑动。
- **个人资料**：大头像、姓名和角色、简介，以及 Status、Team、Working on、Location（城市和时区）和 Email（邮件链接和复制按钮）等字段。空字段会被省略。

### 搜索和团队

搜索在每次按键时运行。文本会去除首尾空白，不区分大小写，作为一个整体与姓名、角色、团队和城市比较："lena hart" 能找到 Lena Hartmann，"hartmann lena" 则找不到，而 "operations" 也能找到角色中没有写但团队是它的人。重音符号不会被折叠，所以 "malmo" 找不到 Malmö。设置 `SearchFilter` 可以用不同的方式搜索；它会收到每个人以及输入的原始文本。

填写了 `Teams` 时，团队按钮来自它，否则来自按首次出现顺序排列的人员。按钮上的数字是总数，不随搜索变化。`SelectedTeam`（All 为 null）和 `SearchText` 是双向且相互独立的：更改其中一个会保留另一个。`MatchCount` 和 `VisiblePeople` 说明当前列出了什么。

### 显示的成员

`SelectedPersonId` 保存所选成员的键（即其 `Id`，没有 `Id` 时是其姓名）；只有点选、在某一行上按 Enter 或 Space，或者你的代码才会更改它。`DisplayedPerson` 是正在显示个人资料的人：所选的人在匹配时显示他，否则显示第一个匹配项。把所选的人筛选掉，就会显示第一个匹配项；把他找回来，他又会显示。`PersonSelected` 和 `PersonSelectedCommand` 只报告点选，从不报告回退显示，也不报告对同一个人的第二次点选。`SelectPerson` 从代码中显示某个人，而不触发该事件。

```csharp
directory.PersonSelected += (_, e) => navigator.Open($"/people/{e.Person.Id}");
directory.SelectedTeam = "Engineering";
directory.SelectPerson("jonas");
```

<Demo name="BoundState" />

### 没有匹配项

没有人匹配时，列表会带着搜索文本说明这一点，并提供 Clear filters，它会清空搜索并重新显示所有团队（所选的人会保留）；个人资料区域显示一条简短提示而不是某个人。`ClearFilters()` 在代码中做同样的事。

### 复制电子邮件地址

复制按钮会复制所显示成员的地址，然后在 `CopyResetDelay`（1.8 秒）内显示 Copied（带对勾）或 Retry，之后恢复原状；状态消息会告诉屏幕阅读器结果如何。默认情况下它写入系统剪贴板。设置 `CopyAsync` 可以用其他方式复制（远程会话、审计调用）：出现异常时显示 Retry，`EmailCopied` 会报告每一次尝试，包含该成员、地址和错误。地址本身是一个邮件链接；`EmailLinkClicked` 先触发，设置 `Handled` 可阻止 mailto: 链接打开。

<Demo name="CopyFailure" />

### 窄容器

本 Block 读取的是自身的宽度，而不是窗口的宽度。宽度不超过 640 时，个人资料会收紧，复制按钮只显示图标；不超过 500 时，个人资料移到列表下方，按钮重新显示文字；不超过 400 时，搜索框在标题下独占一行，团队人数被隐藏。

<Demo name="Narrow" />

### 有边框或扁平

`IsFramed`（默认）会绘制一张带 1px 边框和 16 圆角的卡片。设为 false 可把通讯录平铺在页面或面板中。`IsSearchVisible` 和 `IsFilterVisible` 会隐藏搜索框和团队按钮；`Labels` 接受一个 `TeamDirectoryLabels`，其中包含每个词语和屏幕阅读器文本，可用于翻译或改写它们。

<Demo name="Flat" />

## API 参考

<!-- api: TeamDirectory -->

<!-- api: TeamDirectoryPerson -->

<!-- api: TeamDirectoryLabels -->

<!-- api: TeamDirectoryPersonSelectedEventArgs -->

<!-- api: TeamDirectoryEmailCopiedEventArgs -->

<!-- api: TeamDirectoryPersonEventArgs -->

## 无障碍

- **Tab** 从搜索框（文本非空时还包括它的清除按钮）依次移到每个团队按钮、每一行、邮件链接和复制按钮。每一行和每个团队按钮都是独立的停靠点。
- **Enter** 或 **Space** 按下团队按钮、点选一行，或按下复制按钮。
- 在搜索框中按 **Escape** 会清空它并把焦点留在其中；搜索框为空时则交给页面处理。
- 清除按钮会清空搜索并把焦点放回搜索框。如果焦点原本在搜索框中，Clear filters 会把焦点移到搜索框，而不是让焦点随消息一起丢失。
- 团队按钮和行是切换按钮：屏幕阅读器会把被按下的团队和正在显示个人资料的行读作已按下。团队按钮构成一个有名称的组（"Team filter"），每个按钮读出其团队和总人数（"Design, 2"）；列表也有名称（"Directory"）。
- 一行读出姓名和角色；其头像读出姓名以及此人在线还是离开。
- 个人资料区域是一个礼貌（polite）的实时区域，因此在人员变化时会读出新的人；个人资料以姓名和角色命名。
- 复制按钮名为 "Copy email address"，复制后变为 "Email address copied"；结果也会被播报，按钮恢复原状时播报会被清除。
- 每个词语和名称都在 `Labels` 中。

## 动效

| 内容 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 团队胶囊移到被按下的按钮 | 弹簧（视觉时长 .42，无回弹），位置和宽度 | 直接跳到位 | 直接跳到位 |
| 行高亮移到所显示的行 | 同一条弹簧 | 直接跳到位 | 直接跳到位 |
| 匹配项变化时留下的行 | 在同一条弹簧上滑向新位置 | 直接跳到位 | 直接跳到位 |
| 离开的行 | 在原处于 170ms 内淡出，不占空间 | 立即移除 | 立即移除 |
| 新出现的行 | 在原处于 150ms 内淡入 | 直接出现 | 直接出现 |
| 个人资料切换 | 旧的在 180ms 内淡出；新的在 220ms 内淡入并上升 4px | 直接替换 | 直接替换 |
| 复制确认 | 图标在 160ms 内交叉淡化，并在 580ms 的弹簧上缩放；颜色立即变化 | 颜色和图标立即变化 | 立即变化 |
| 悬停和焦点颜色 | 160ms | 立即变化 | 立即变化 |

被打断的滑动会从当前绘制的位置带着速度继续；被打断的个人资料会从已达到的透明度开始淡出，所以快速连续点选永远不会出现跳变。

## 相关

- `Avatar`：带首字母和可用状态圆点的头像。
- `SearchField` 和 `CopyButton`：独立的搜索输入框和复制按钮。
- `GlidingIndicator`：在各项之间滑动的高亮。
