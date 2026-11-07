---
title: 停靠布局
summary: "IDE 式布局：带标签的面板在可调整大小的嵌套分割中围绕文档区排布：拖放停靠面板、把面板浮动到独立窗口、把工具自动隐藏到边栏、把文档绑定到集合、用键盘切换面板，并可把布局保存为 JSON。"
source-hash: dbe1ea863e1e
---

<Demo name="Preview" />

## 何时使用

- IDE、编辑器、仪表盘、交易终端和设计工具：多个长期存在的工作区并排摆放，由用户自己排布。
- 文档在中央区域打开、关闭，工具面板停留在四周。
- 用户希望下次打开时还是原来的布局：保存为 JSON 再读回。

## 何时不用

- 只有两三个固定窗格、没人会重新排布：用 `ResizablePanels`。
- 在同一层级的视图之间切换：用 `Tabs`。
- 带侧栏和主区的应用外框：用 App shell 区块。
- 临时出现在页面上方的内容：用 `Popover`、`Drawer` 或 `Dialog`。

## 用法

添加包并在 `UseArc` 中注册：

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcDock());
```

把布局树写成 `DockLayout` 的内容。`DockSplit` 把子节点左右排列（`Horizontal`）或上下排列（`Vertical`），每个子节点的 `Size` 是它的份额。`DockGroup` 是一张标签卡片，每个 `DockPane` 一个标签。`DockDocumentArea` 存放文档，即没有 `PreferredSide` 的面板。每个面板都需要在布局内唯一的 `Id`，保存的布局按它引用面板。

```xml
<arc:DockLayout>
    <arc:DockSplit Orientation="Horizontal">
        <arc:DockGroup Size="22">
            <arc:DockPane Id="explorer" Title="Explorer" Icon="{x:Static arc:ArcIcons.Folder}" PreferredSide="Left">
                <TextBlock Text="Explorer" />
            </arc:DockPane>
        </arc:DockGroup>
        <arc:DockSplit Orientation="Vertical" Size="78">
            <arc:DockDocumentArea Size="70">
                <arc:DockGroup>
                    <arc:DockPane Id="doc:readme" Title="README.md">
                        <TextBlock Text="Readme" />
                    </arc:DockPane>
                </arc:DockGroup>
            </arc:DockDocumentArea>
            <arc:DockGroup Size="30">
                <arc:DockPane Id="terminal" Title="Terminal" PreferredSide="Bottom">
                    <TextBlock Text="Terminal" />
                </arc:DockPane>
            </arc:DockGroup>
        </arc:DockSplit>
    </arc:DockSplit>
</arc:DockLayout>
```

面板本身就是内容宿主。移到其他组时是同一个实例换了父级，内容的滚动位置、输入和选中状态都会保留。每个组只有活动面板在可视树中。

### 标签与分割

分割可以任意嵌套，文档区也可以放一个由文档组组成的分割，实现并排编辑。拖动两张卡片之间的缝即可调整大小；分隔条的行为与 `ResizablePanels` 相同（聚焦分隔条后用方向键、Home、End、Enter）。模型会自动整理：最后一个面板离开的组会被删除，只剩一个子节点的分割由该子节点替代，同方向嵌套的分割会合并。文档区即使为空也会保留，此时显示 `EmptyDocumentContent`。

<Demo name="TabsAndSplits" />

文档标签带关闭按钮，悬停、选中或键盘聚焦时显示；`IsModified` 时改为显示圆点，指针移上去才换成关闭按钮。工具标签没有关闭按钮，可以从标签菜单或键盘关闭。标签放不下时，标签条可以用滚轮滚动，右侧的箭头按钮列出全部标签。

### 移动面板

在标签上右键或按 Shift+F10 打开菜单：关闭、关闭其他和全部关闭（文档），移动到 › 左侧、右侧、上方或下方新组，以及停靠到 › 左侧、右侧、顶部或底部（工具；停靠到整个布局的一侧）。在代码中，`Move` 接收一个 `DockTarget`：

```csharp
Layout.Move(terminal, DockTarget.Split(editorGroup, DockPosition.Bottom)); // a new group below, half of the group
Layout.Move(outline, DockTarget.Edge(DockPosition.Right));                // along the right edge, a quarter of the layout
Layout.Move(search, DockTarget.Into(explorerGroup, 0));                   // first tab of a group
Layout.Move(problems, DockTarget.DocumentArea(DockPosition.Left));        // beside the whole document area
```

文档只能留在文档区内，工具只有在 `CanDockAsDocument` 为 true 时才能进入文档区；违反规则的移动返回 false，布局不变。`Open` 添加面板：不指定目标时，文档进入最近活动的文档组，工具加入 `PreferredSide` 相同的工具组，没有则沿该边新建一个组，宽或高为 `PreferredSize` 像素。`Close` 先触发可取消的 `PaneClosing`，再触发 `PaneClosed`；组会选中右侧（或左侧）的标签，被清空的组会消失。

### 拖放停靠

沿标签条拖动标签即可重排，其他标签会让位。把它拖到标签条上方或下方超过 16px，或拖出所在的组，就变成停靠拖拽：标签的影子跟随指针，指针下方的组上出现罗盘，预览框滑到面板将要落下的位置。

- 罗盘中间的按钮把面板作为标签加入该组；四个方向按钮拆分该组，新组占它的一半。
- 在文档区上方时，外面还有一圈四个按钮，停靠到整个文档区的旁边。
- 布局四条边中点的按钮停靠到整个布局的一侧，占布局的四分之一。
- 在标签条上方时，一条竖线标出标签将插入的位置。
- 不在按钮上时，由指针在组内的位置决定：离某条边四分之一以内拆分那一侧，中间则作为标签加入。
- 拖动标签条的空白处可以移动整个组。按 Escape 取消，面板回到原处。

规则与 `Move` 相同：文档只能留在文档区内，工具只有在 `CanDockAsDocument` 时才能进入文档区，组不能落在自己身上，布局 `IsLocked` 时不能拖动。不接受该面板的按钮会变暗。把 `ShowDockIndicators` 设为 false 可以得到不显示罗盘的安静拖拽：指针在组内或靠近布局边缘的位置仍然决定停靠方向，只显示预览框。

<Demo name="DragToDock" />

### 浮动

把标签拖到布局外松手、在标签菜单中选择"浮动"，或调用 `Float`，面板就移到一个浮动窗口里，出现在原来位置附近。窗口属于布局所在的窗口：没有任务栏按钮，始终在所属窗口之上，主题和动效设置也随之变化。它可以像布局一样容纳多个面板和组。要停靠回去，把它的标签或标题栏拖到罗盘按钮或标签条上（窗口跟随指针移动，布局同时显示罗盘），或双击标题栏，或在标签菜单中选择"停靠"。`Dock` 让面板回到原来的位置：原来的组；组已不存在时，回到原来的相邻节点旁边并恢复原来的尺寸。关闭窗口会关闭其中可以关闭的面板，其余的停靠回去。

<Demo name="Floating" />

`FloatingMode` 决定面板如何浮动。`Auto` 在经典桌面应用的桌面窗口中使用真正的窗口，在其他地方（例如浏览器）使用布局内的浮动卡片。`Window` 和 `Overlay` 强制使用其中一种，`Disabled` 让所有面板保持停靠。浮动卡片使用弹层的表面与阴影，拖动标题栏移动，拖动右下角调整大小，始终留在布局范围内，并带有"停靠"和"关闭"按钮。浮动面板是模型的一部分（`Workspace.Floats`，每个 `DockFloat` 有自己的树和 `Bounds`）：在代码中添加一个就会打开它的窗口，保存的布局也会把它们带回来；原来所在的显示器已经拔掉时，窗口会被拉回到某个屏幕上。

```csharp
Layout.Float(properties);                       // a window (or card) near the pane
Layout.Dock(properties);                        // back where it came from
Layout.FloatingMode = DockFloatingMode.Overlay; // cards inside the layout, also on the desktop
```

在 Linux 的 Wayland 下，应用不能决定窗口的位置：拖到布局外松手的面板出现在系统安排的位置，标题栏使用系统的移动方式，浮动窗口只能从菜单或双击标题栏停靠回去。

### 自动隐藏

工具面板可以离开所在的组，收到离该组最近的布局边缘的边栏上：在标签上右键选择"自动隐藏"、在面板内按 Shift+Escape，或调用 `AutoHide`。边栏上显示一个带面板图标和标题的按钮，左右两侧的按钮旋转 90°。点击按钮或在按钮上按 Enter，面板会以卡片形式从该边滑出、覆盖在内容上，宽或高等于它停靠时的尺寸（最多为布局的 70%），焦点随之进入面板。点击外部、按 Escape 或 Shift+Escape、或焦点移到别处时，卡片收回。拖动卡片的内侧边缘可以调整尺寸。卡片标题栏的图钉按钮、菜单中的"固定"或 `Pin` 会把面板放回原来的组；原组已不存在时，在同一侧新建一个组。

<Demo name="AutoHide" />

自动隐藏的面板会随布局一起保存。在 XAML 中，把它们放进工作区的边栏列表（`LeftHidden`、`RightHidden`、`TopHidden`、`BottomHidden`）；从未停靠过的面板按 `PreferredSize` 滑出：

```xml
<arc:DockLayout>
    <arc:DockLayout.Workspace>
        <arc:DockWorkspace>
            <arc:DockSplit Orientation="Horizontal">
                <arc:DockDocumentArea Size="75">
                    <arc:DockGroup>
                        <arc:DockPane Id="doc:readme" Title="README.md">
                            <TextBlock Text="Readme" />
                        </arc:DockPane>
                    </arc:DockGroup>
                </arc:DockDocumentArea>
                <arc:DockGroup Size="25">
                    <arc:DockPane Id="properties" Title="Properties" PreferredSide="Right">
                        <TextBlock Text="Properties" />
                    </arc:DockPane>
                </arc:DockGroup>
            </arc:DockSplit>
            <arc:DockWorkspace.RightHidden>
                <arc:DockPane Id="outline" Title="Outline" PreferredSide="Right" PreferredSize="220">
                    <TextBlock Text="Outline" />
                </arc:DockPane>
            </arc:DockWorkspace.RightHidden>
        </arc:DockWorkspace>
    </arc:DockLayout.Workspace>
</arc:DockLayout>
```

面板在边栏上时，`State` 为 `AutoHidden`，`IsPinned` 为 false。文档不会自动隐藏；`CanAutoHide` 为 false 的工具保持停靠。浮动的面板不能自动隐藏（先停靠回去），自动隐藏的面板可以浮动；对自动隐藏的面板调用 `Dock` 等于固定它。滑出的卡片不能开始拖拽：先固定面板，或用菜单中的"浮动"和"停靠到"。

### 最大化与面板切换器

双击组的标签条空白处，或在菜单中选择"最大化"，该组会暂时铺满整个布局，直到再次执行同一操作（此时菜单显示"还原"）或激活其他组的面板；布局本身不会改变。Ctrl+Tab 打开面板切换器：按最近使用顺序列出所有面板，并高亮上一个面板。按住 Ctrl 再按 Tab 继续向后移动（Shift+Tab 向前），松开 Ctrl 即切换过去。`ToggleMaximize`、`ToggleMaximizeCommand`、`ShowPaneSwitcherCommand` 和 `AutoHideActiveCommand` 可以从代码或应用菜单完成同样的操作。

### 保存与恢复

`SaveLayout` 把布局写成 JSON：带份额的布局树、活动面板，以及自动隐藏边栏和浮动窗口中的面板。面板只按 `Id` 保存；`LoadLayout` 在布局用过的面板中查找，找不到的交给 `PaneResolver`。未知的 Id 会被跳过，保存的布局中没有提到的面板按 `PreferredSide` 放回。文本不是布局或来自更新的版本时，`LoadLayout` 返回 false 并保持布局不变。在 `LayoutChanged` 时保存即可自动记住布局。

<Demo name="SaveAndRestore" />

```csharp
Layout.LayoutChanged += (_, _) => settings.DockLayout = Layout.SaveLayout();
if (settings.DockLayout is { } saved)
{
    Layout.LoadLayout(saved);
}
```

### 来自集合的文档

把 `DocumentsSource`（以及 `ToolsSource`）绑定到一个集合，每一项都会成为一个面板，面板的 `DataContext` 就是该项。`DocumentTemplate`（`ToolTemplate`）负责生成面板：模板产出 `DockPane` 时直接使用，产出其他内容时包进一个新面板。没有 `Id` 或 `Title` 的面板从数据项取值：数据项实现 `IDockItem` 时取 `IDockItem.DockId` 和 `DockTitle`，否则取 `ToString()`。添加一项会打开它的面板（文档进入最近活动的文档组，工具按 `PreferredSide` 放置，未设置时放在左侧），移除一项会关闭面板，且不触发 `PaneClosing`。用户关闭标签时先触发 `PaneClosing`；没有处理程序取消、且集合是可写列表时，该项会从集合中移除。保存的布局按 Id 恢复这些面板。

<Demo name="Mvvm" />

```xml
<arc:DockLayout DocumentsSource="{Binding OpenFiles}">
    <arc:DockLayout.DocumentTemplate>
        <DataTemplate>
            <arc:DockPane Id="{Binding Path}" Title="{Binding Name}" IsModified="{Binding IsDirty}">
                <TextBlock Text="{Binding Text}" />
            </arc:DockPane>
        </DataTemplate>
    </arc:DockLayout.DocumentTemplate>
    <arc:DockDocumentArea />
</arc:DockLayout>
```

活动文档是 `ActivePane.DataContext`；`ActivePaneChanged` 会在它变化时通知你。

### 锁定

`IsLocked` 让所有面板固定不动：标签不能关闭、移动、浮动、自动隐藏或拖动，菜单命令不可用。分隔条仍然可以调整，自动隐藏的面板仍然可以滑出，组也仍然可以最大化。

<Demo name="Locked" />

## API 参考

<!-- api: DockLayout -->

<!-- api: DockPane -->

<!-- api: DockSplit -->

<!-- api: DockGroup -->

<!-- api: DockDocumentArea -->

## 无障碍

- 布局读作名为"停靠布局"的组，除非用 `AutomationProperties.Name` 另行命名。每个组读作标签列表，每个标签读作以面板命名的标签项；修改过的文档会加上"已修改"。文档标签的关闭按钮名为"关闭 README.md"。
- 布局变化会礼貌播报："Explorer 已停靠到 Terminal 右侧"、"Search 已停靠到下边缘"、"README.md，第 2 个标签，共 2 个"、"Terminal 已移到新窗口"、"README.md 已关闭"。
- 罗盘按钮的名称为"停靠到左侧"、"停靠到右侧"、"停靠到顶部"、"停靠到底部"和"作为标签停靠"。
- 边栏按钮读作以面板命名的按钮，帮助文本为"已自动隐藏，按 Enter 显示"；滑出的卡片以面板命名，面板切换器读作列表。自动隐藏也会播报："Explorer 已自动隐藏"。
- 指针能对面板做的每件事都有对应的菜单命令（移动到、停靠到、浮动、停靠），菜单可以用键盘打开。

### 键盘

| 按键 | 作用 |
| --- | --- |
| Left / Right、Home / End | 在标签上：移到组内上一个、下一个、第一个或最后一个标签并显示其面板。 |
| Ctrl / ⌘ + Shift + Left / Right | 在标签上：把标签向左或向右移动一位。 |
| Delete | 在标签上：关闭其面板（可关闭时）。 |
| Ctrl + F4、⌘ + W | 在标签上或面板内：关闭活动面板。 |
| Shift + F10、Menu 键 | 在标签上：打开菜单并高亮第一行。 |
| 方向键、Home、End、Enter | 在分隔条上：调整大小，与 `ResizablePanels` 相同。 |
| Ctrl + Tab、Ctrl + Shift + Tab | 布局内任何位置：打开面板切换器并高亮上一个（或最久未用的）面板；按住 Ctrl 时 Tab 继续移动，松开 Ctrl 即切换。 |
| Up / Down、Enter、Escape | 在面板切换器中：移动高亮、切换，或不切换直接关闭。 |
| Shift + Escape | 在工具面板内：自动隐藏它。在滑出的面板内：把它收回。 |
| Enter、Space | 在边栏按钮上：滑出或收回其面板。 |
| Escape | 在滑出的面板内：收回它；焦点回到它的边栏按钮。 |
| Escape | 拖动面板时：取消并放回原处。 |

面板在拥有焦点时被关闭，焦点会移到接替它的面板的标签上。

## 动效

- 选中标签的底色在标签之间滑动，弹簧与 `Tabs` 相同；所在组包含活动面板时换成强调色。
- 标签文字和操作按钮的颜色在标准时长内渐变。
- 分隔条的跟手与落定与 `ResizablePanels` 相同。
- 沿标签条拖动标签时，其他标签以 snappy 弹簧让位，松手后标签弹回自己的位置。
- 预览框在目标之间以 snappy 弹簧滑动；罗盘在 120ms 内淡入，同时从 92% 放大。
- 浮动卡片像弹层一样进场，从 97% 淡入。拖拽影子和浮动窗口直接跟随指针。
- 自动隐藏的面板以平滑弹簧滑出，180ms 内收回。
- 通过命令移动的面板直接换位；布局静止时没有任何循环动画。
- 减弱动效：选中底色、标签和预览框直接跳到位，不滑动；罗盘和浮动卡片只淡入淡出；自动隐藏的面板改为在 120ms 内淡入淡出，不再滑动。

## 响应式

- `DockLayout` 在任何宽度下都不改变形态：没有断点，也没有紧凑布局。分割区共享所给的宽度。每个窗格低于其 `MinSize`（默认 120 px）时会增加阻力；当布局比所有最小值之和还窄时，最小值按比例缩小，窗格变窄。
- 它按自身宽度而不是窗口宽度测量，所以手机预览或窄侧边栏中的布局与手机上一样被挤压。
- 浮动窗口至少 320 px 宽、240 px 高；自动隐藏的窗格滑出覆盖内容时，最大为布局尺寸的 70%。
- 标签放不下时，标签条可用滚轮滚动，箭头按钮列出全部标签。文档标签的关闭按钮只在悬停和选中的标签上显示；标签菜单通过右键或 Shift+F10 打开，所以不要依赖触屏关闭未选中的标签。
- 在手机上用 `Tabs` 一次只显示一个视图，代替分割布局；说明页建议它用于同一层级的视图切换。

## 相关组件

- `ResizablePanels`：固定的一行或一列可调整大小的窗格。
- `Tabs`：在同一层级的视图之间切换。
- `ArcWindow`：带 Arc 标题栏的桌面窗口。
