---
title: 语义缩放
summary: 一张组织结构图，可从团队缩放到项目，再到单个任务，每个层级都显示新的细节，并提供面包屑、小地图、导览，以及视野内各项的数字。
source-hash: eb9644ec5a43
---

<Demo name="Preview" />

## 何时使用

`SemanticZoom` 把整个组织绘制成一张可以缩放进入的地图，缩放时不会丢失你所在的位置。团队是面板，项目是面板内的卡片，任务是卡片上的区块，而任务可以展开为它的详情和清单。每个缩放层级显示的是不同的信息，而不是同一张图放大：在总览中，你看到各团队及其进度，每个任务是一个灰色小标签（被阻塞时为红色）；靠近一些，显示团队负责人和计数，然后是项目标题、健康度和截止日期，再是任务标题和截止日期，最后才是任务的摘要、负责人和清单。面包屑、详情面板和屏幕阅读器文本始终描述视图中心下的内容，因此地图会在你飞行时告诉你身在何处。

- **组合投资组合总览**，用于领导层会议：从整个公司开始，飞入落后的团队，再飞入偏离目标的项目，然后查看哪些任务被阻塞。
- **工作区首页**，每个人看到同一张地图：总览回答“我们进展如何”，缩放回答“我的项目接下来做什么”，无需切换页面。
- **状态讲解**，配合导览：演示时，地图会自行飞过一个团队、一个项目和两个任务。
- **入职视图**，向新成员展示组织如何构成：谁负责哪个团队，每个团队运行哪些项目，谁在处理什么。
- **规划例会**，人们直接在地图上勾选清单项，并观察任务、项目和团队的数字如何更新。

## 何时不用

- **逐项浏览深层层级。** 使用 `TreeView`；它以行的形式显示任意深度，比地图更易于扫读。
- **处理任务本身。** 使用看板或任务列表；地图用于定位，而不是移动工作。
- **按属性聚类的扁平数据。** 使用 `CustomerGalaxy`；语义缩放需要容器的层级结构。
- **跨多个项目的精确比较。** 使用 `DataGrid` 或 `Table`；地图一次只显示一个节点的数字。
- **小卡片或侧边栏。** 地图需要至少 400 px 的高度，且周围要留出空间，以便页面能够滚动（滚轮始终用于缩放地图）。

## 用法

为控件提供 `Organization`：一棵由 `SemanticZoomTeam`、`SemanticZoomProject` 和 `SemanticZoomTask` 组成的树。布局、合计（未完成和被阻塞的任务、最近的截止日期、进度、人员）以及所有标签都由它推导而来，因此你只需描述工作本身。

```xml
<arc:SemanticZoom Header="Network map">
    <arc:SemanticZoom.Resources>
        <arc:SemanticZoomPerson x:Key="Rustam" Name="Rustam Aliyev" Role="Fleet lead" />
        <arc:SemanticZoomPerson x:Key="Hana" Name="Hana Sato" Role="Charging specialist" />
    </arc:SemanticZoom.Resources>
    <arc:SemanticZoomOrganization Title="Harbor Line Transit">
        <arc:SemanticZoomTeam Title="Fleet" Description="Vehicles, charging and everything that wears out." Lead="{StaticResource Rustam}">
            <arc:SemanticZoomProject Title="Depot charging bays" Lead="{StaticResource Hana}" Health="AtRisk">
                <arc:SemanticZoomTask Title="Order chargers" Status="InProgress" Assignee="{StaticResource Hana}" Points="3">
                    <arc:SemanticZoomChecklistItem Text="Get three quotes" IsDone="True" />
                    <arc:SemanticZoomChecklistItem Text="Check the grid connection" />
                </arc:SemanticZoomTask>
            </arc:SemanticZoomProject>
        </arc:SemanticZoomTeam>
    </arc:SemanticZoomOrganization>
</arc:SemanticZoom>
```

`SemanticZoom` 派生自 Avalonia 的 `TemplatedControl`。地图由控件自己绘制（每帧一次，文字和画刷有缓存，静止时不绘制任何内容）；面包屑、按钮、提示卡和详情面板都是普通控件。控件会设置 `:fit`、`:touring`、`:side-panel`、`:compact` 和 `:interacted` 伪类，供样式使用。

### 结构

1. **标题**（`Header`、`Subtitle`、`SubtitleFormat`）：16 px 中等粗细的标题和 13 px 的弱化说明行。没有 `Subtitle` 时，说明行显示“5 个团队，22 个项目，151 个任务。放大以查看更多。”；设为空字符串则隐藏它。
2. **地图**：团队面板位于点阵背景上，点阵间距每放大四倍重复一次，使背景产生纵深感。
3. **面包屑**（`IsBreadcrumbVisible`）：当前层级以上的每一级都是一个按钮，点击即飞回该层级。
4. **导览按钮**（`IsTourButtonVisible`）：启动和停止导览；在窄容器中只显示图标。
5. **提示卡**：指针下（或键盘光标下）的团队、项目或任务的名称、一行状态或描述，以及四个数字。
6. **小地图**（`IsMinimapVisible`）：离开总览后出现；按下或拖动它即可移动视图。
7. **提示语**（`IsHintVisible`）：一行弱化的手势提示，首次交互后淡出。
8. **缩放栏**（`IsZoomControlsVisible`）：缩小、缩放读数、放大，以及显示全部（当全部内容都在视野内时禁用）。
9. **详情面板**（`DetailPanelPlacement`，也可单独使用，即 `SemanticZoomPanel`）：视图中节点的类型和名称、其未完成和被阻塞的任务及最近的截止日期、进度，以及相关人员。

### 数据

| 类型 | 包含内容 |
|---|---|
| `SemanticZoomOrganization` | 根节点：`Title` 及其 `Teams` |
| `SemanticZoomTeam` | `Title`、`Description`（显示在其提示卡中）、`Lead` 及其 `Projects` |
| `SemanticZoomProject` | `Title`、`Description`（用于生成的任务摘要）、`Lead`、`Health`、`Due` 及其 `Tasks` |
| `SemanticZoomTask` | `Title`、`Status`、`Assignee`、`Reviewer`、`Due`、`Points`、`CommentCount`、`Summary` 及其 `Checklist` |
| `SemanticZoomChecklistItem` | `Text` 和 `IsDone` |
| `SemanticZoomPerson` | `Name`、`Role`、可选的 `Avatar` 图片和可选的 `Initials` |

每个节点都以只读属性的形式公开其合计：`OpenTasks`、`BlockedTasks`、`DoneTasks`、`TaskCount`、`NextDue`（未完成任务中最早的截止日期）、`Progress`（任务中已勾选清单项的比例，完成时为 1；项目、团队和组织则对其下方所有任务取平均）、`People`（负责人在前，然后是负责人，不重复）、`ProjectCount`，以及 `NeedAttention`（未按计划进行的项目）。其下方任何内容发生变化时，它们都会立即更新。

没有 `Summary` 的任务会根据其状态、负责人、截止日期和项目生成摘要。没有 `Avatar` 的人员会在弱化的圆形中显示其首字母。

### 移动与缩放

滚动或捏合可以围绕指针缩放（1× 显示全部，默认最高 53×，即 `MaxZoom`），拖动可以平移（轻扫后视图会继续滑行，并在边缘处回弹），点击团队、项目或任务即可飞入。点击你已经处于其中的节点，或其祖先节点，不会有任何反应；下一次点击会进入更深一层或平移到同级。飞行开始时先稍稍缓出，然后缓入，飞行距离越远，耗时越长。

当前节点（`Scope`，双向绑定）不是单独的选中项：它始终是视图中心下、且缩放级别允许的最深节点（团队从 1.1× 起，项目从 2.4× 起，任务从 18× 起）。设置 `Scope` 或调用 `FlyTo` 即可飞向某处；`FlyOut`、`ResetView` 和 `ZoomBy` 涵盖其余操作。`ScopeChanged` 在飞行和平移期间也会触发，并通过 `IsUserInitiated` 区分用户输入与代码或导览的操作。

### 勾选清单项

放大到某个任务足够近时，会显示它的清单；点击某一项，或将键盘光标移到该项并按 Enter，即可勾选或取消勾选。勾选最后一个未完成项会将任务标记为完成；取消勾选已完成任务的某一项，则会使其回到审核状态。任务的区块、项目的卡片、团队和详情面板会同时更新，数字也会滚动到新值。`ChecklistItemToggled` 会报告每一次变化。

### 导览

点击导览按钮（或使用 `ToggleTourCommand`、`StartTour`、`IsTourPlaying`）会依次飞过一系列停靠点，并在每个停靠点停留 `TourDwell`（1.5 s）。默认情况下，它会访问第一个团队、其中的一个项目、下一个项目的两个任务、第二个团队及其中的一个项目，最后返回整张地图。设置 `TourStops` 可以定义你自己的路线；将整个组织作为一个停靠点则表示全部内容。任何手动输入都会在当前位置停止导览。

<Demo name="GuidedTour" />

### 布局与容器宽度

地图的高度为控件宽度的 48%，范围在 400 到 580 px 之间（或由 `StageHeight` 指定）。当控件内部宽度超过 900 px 时，详情面板会移到地图旁边（264 px）；否则它位于地图下方，分为两列。在宽度小于 600 px 的容器中，地图高度为 420 px，导览按钮只显示图标，小地图缩小，提示语移到缩放栏上方，面板只使用一列。地图本身的布局从不随宽度改变：窄容器中的地图只是显示得更小。

<Demo name="SidePanel" />

<Demo name="Narrow" />

### 你自己的数据

整棵树既可以像这里一样用 XAML 编写，也可以在代码中构建后赋给 `Organization`。团队会被排入共享同一卡片高度的行中，每一行会拉伸到最宽的那个团队，因此任何团队规模组合都能保持整齐。

<Demo name="YourData" />

### 文字与格式

每个标签、按钮名称和播报都位于 `Texts`（`SemanticZoomTexts`，默认为英文）中：层级和面板标签、状态和健康度名称、地图上绘制的文字、生成的摘要、提示卡的各行、导览和缩放按钮的名称、提示语，以及屏幕阅读器的播报。赋一个新实例即可更改这些文字。日期（如“Sep 23”）和百分比（如“59%”）由 `Formatter`（`ISemanticZoomFormatter`；`SemanticZoomFormatter` 带有 `Culture`）提供。

## API 参考

<!-- api: SemanticZoom -->

<!-- api: SemanticZoomPanel -->

<!-- api: SemanticZoomOrganization -->

<!-- api: SemanticZoomTeam -->

<!-- api: SemanticZoomProject -->

<!-- api: SemanticZoomTask -->

<!-- api: SemanticZoomChecklistItem -->

<!-- api: SemanticZoomPerson -->

## 无障碍

- 地图是一个可获得焦点的元素，角色为“zoomable map”（可缩放地图）。其名称概括组织和视图中的节点（“Map of Harbor Line Transit: 5 teams, 22 projects, 151 tasks. Showing Fleet. …”），其帮助文字列出各按键。
- 一个礼貌式实时区域会在镜头稳定后播报视图中的节点（“Now showing Fleet team: 5 projects, 31 open tasks, 52% done.”）、键盘光标下的项、勾选和取消勾选的清单项（附带任务新的计数和状态），以及导览的开始和停止。
- 一个隐藏表格会列出每个项目，包括其团队、健康度、进度、未完成和被阻塞的任务以及截止日期，使屏幕阅读器无需缩放即可读取整个投资组合。
- 面包屑是一组有名称的按钮；导览按钮会报告自己是否处于按下状态；缩放按钮有名称，适应按钮在全部内容都在视野内时被禁用，并会被 Tab 跳过；面板是一个有名称的分组，其头像堆叠以所有人的名字命名。
- 滚动的数字只读取一次，读取最终值。健康度和状态从不只依赖颜色：提示卡和地图都会以文字说明。

### 键盘

| 按键 | 位置 | 操作 |
|---|---|---|
| `Tab` | 任何位置 | 地图、面包屑、导览按钮、缩放按钮 |
| `ArrowLeft` / `ArrowRight` / `ArrowUp` / `ArrowDown` | 地图 | 在当前节点的子节点和同级节点中，将光标移到该方向上最近的项（在任务中，移到其清单项）；第一次按下会选中第一个子节点 |
| `Enter`、`Space` | 地图 | 飞入光标所在的项，或勾选其下的清单项 |
| `Escape` | 地图 | 停止导览；清除光标；再按一次则缩小一级 |
| `Backspace` | 地图 | 缩小一级 |
| `+`、`=` / `−` | 地图 | 以中心为基准放大或缩小 1.5 倍 |
| `0`、`Home` | 地图 | 显示整个组织 |

## 动效

- 飞行沿平滑的缩放与平移路径进行（先稍稍缩小，移动，再放大），采用缓入缓出曲线，耗时 460 ms 加上每单位路径长度 380 ms：飞入团队约需 0.6 s，从项目飞入任务约需 1.1 s。
- 滚轮、捏合、按钮和按键缩放遵循临界阻尼弹簧（约 0.2 s 稳定），并保持指针下的点不动。缩放超出限制时会稍微拉伸后回弹；拖动超出边缘时会拉伸，松开后回弹；轻扫后视图会继续滑行，并在约一秒内逐渐减速。
- 每个细节层级都会在按屏幕尺寸划分的区间内淡入，并在缩放跳变时于约 140 ms 内缓动过渡。视野外的节点会变暗（团队至 42%，相邻的项目和任务至 50%）；悬停轮廓在约 0.1 s 内淡入。
- 提示卡会淡入并从 96% 放大；离开总览时，小地图会上升出现；首次交互后，提示语淡出；面包屑淡入（240 ms）和淡出（120 ms）；面板的名称交叉淡化，数字逐位滚动；进度条平滑过渡到新宽度；新头像淡入。
- 导览在每个停靠点等待 1.5 s。在减少动效时，镜头改为直接跳到每个停靠点，每个停靠点停留 2.4 s。
- 减少动效会立即完成每一次飞行、缩放、滑行、变暗和淡入，并取消界面过渡；关闭动效的效果相同。勾选清单项和合计在每个层级都会即时更新。

| 变化 | 完整 | 减少动效 | 关闭 |
|---|---|---|---|
| 飞向节点、返回、适应全部 | 缩放与平移路径，460 + 380·S ms | 立即完成 | 立即 |
| 滚轮、捏合、按钮或按键缩放 | 弹簧，约 0.2 s，锚定 | 立即完成 | 立即 |
| 拖过边缘后松开 | 橡皮筋效果，回弹 | 橡皮筋效果，落在边界内 | 橡皮筋效果，落在边界内 |
| 轻扫 | 继续滑行（τ ≈ 0.27 s） | 停止 | 停止 |
| 细节层级 | 按屏幕尺寸淡入，140 ms 缓动 | 按屏幕尺寸 | 按屏幕尺寸 |
| 变暗与悬停轮廓 | 缓动（140 ms / 90 ms） | 立即 | 立即 |
| 提示卡 | 淡入并从 96% 放大 | 淡入 | 立即 |
| 小地图 | 淡入并上升 6 px | 出现 | 立即 |
| 面包屑 | 淡入 240 ms，淡出 120 ms | 立即切换 | 立即 |
| 面板数字与进度条 | 数字滚动，进度条缓动（580 ms） | 立即切换 | 立即 |
| 导览 | 飞行，每个停靠点停留 1.5 s | 跳转，每个停靠点停留 2.4 s | 跳转，每个停靠点停留 2.4 s |

## 相关组件

- `TreeView`，用于以行的形式浏览任意层级。
- `CustomerGalaxy`，用于按属性分簇的扁平数据。
- `DataGrid` 和 `Table`，用于并排比较项目。
- `Breadcrumb`，用于地图之外的路径。

| 组件 | 适用场景 |
|---|---|
| `TreeView` | 人们展开和收起深层层级，以找到某一项 |
| `CustomerGalaxy` | 数千个项目按属性分组，而非嵌套 |
| `DataGrid` | 人们需要阅读、排序和编辑项目本身 |
| `MetricCard` | 仪表板磁贴显示一个数字及其变化 |
