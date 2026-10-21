---
title: 命令面板
summary: 完整的键盘操作面板，支持搜索、分组结果与快捷键。
source-hash: 0e434befd364
---

<Demo name="Preview" />

## 何时使用

- 面向全产品的操作与导航，用户通过键盘访问。
- 命令分散在多个菜单与页面中的应用，输入一个词比逐层浏览更快。
- 同时显示每个命令快捷键的启动器。

## 何时不用

- 搜索带摘录和筛选条件的文档或记录：使用 `SearchResults`。
- 页头中显示少量匹配结果的搜索框：使用 `ExpandingSearch`。
- 为表单选择一个值：使用 `Combobox`。
- 针对单个对象的少量操作：使用 `DropdownMenu`。

## 用法

命令面板是位于分组命令列表之上的搜索框。添加 `CommandPaletteItem` 子项或设置 `ItemsSource`。输入会筛选列表，方向键和 Enter 则无需鼠标即可执行命令。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CommandPalette MaxWidth="560">
        <arc:CommandPaletteItem Id="dashboard" Label="前往仪表盘" Group="导航" Shortcut="G D" />
        <arc:CommandPaletteItem Id="new-project" Label="创建项目" Description="从空白看板开始" Group="操作" />
        <arc:CommandPaletteItem Id="theme" Label="切换主题" Group="设置" />
    </arc:CommandPalette>
</UserControl>
```

### 命令

每个 `CommandPaletteItem` 都有作为标题的 `Label`，另可选择设置 `Description`、`Icon`、`Shortcut` 与 `Group`。命令会列在其所属分组之下，分组按其首次出现的顺序排列。没有分组的命令会放在 `DefaultGroup`（默认为 “Actions”）之下。`Icon` 接受 24×24 的 Lucide 图形，例如 `ArcIcons` 中的图标，也可以是任意控件；默认图标为 Lucide 的 Command 图形。将 `IsEnabled` 设为 false，可显示当前无法执行的命令。

搜索会匹配标题、描述、分组和 `Keywords`，且不区分大小写。为每个命令设置稳定的 `Id`，这样列表变化时，该命令所在的行会保持原位。

面板宽度为 480 像素或更小时，会隐藏快捷键键帽并收紧内边距。本例的宽度为 400 像素。

<Demo name="InlineItems" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:generic="using:System.Collections.Generic"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CommandPalette>
        <arc:CommandPaletteItem Id="theme" Label="切换主题" Group="设置" Shortcut="T">
            <arc:CommandPaletteItem.Keywords>
                <generic:List x:TypeArguments="x:String">
                    <x:String>dark mode</x:String>
                    <x:String>appearance</x:String>
                </generic:List>
            </arc:CommandPaletteItem.Keywords>
        </arc:CommandPaletteItem>
    </arc:CommandPalette>
</UserControl>
```

### 执行命令

按 Enter 执行高亮的命令。输入后若没有高亮项，按 Enter 会执行排在最前的匹配项。点击会执行指针下方的命令。随后面板清空查询，焦点保留在搜索框中，并触发 `ItemSelected`。它还会以所选项为参数执行 `SelectCommand`，并执行该项自身的 `Command`，参数为其 `CommandParameter`。

```csharp
palette.ItemSelected += (_, e) => Navigate(e.Item.Id);
```

### 数据绑定

将 `ItemsSource` 绑定到 `CommandPaletteItem` 对象的集合。面板会跟随集合及其中各项的变化。保留的行移动到新位置，新增的行淡入，面板外框随之改变高度。绑定 `SelectCommand`，即可用所选项执行视图模型中的命令。

<Demo name="DataBinding" />

```xml
<arc:CommandPalette ItemsSource="{Binding Commands}"
                    SelectCommand="{Binding RunCommand}" />
```

### 按需打开

要在页面上方打开面板，请将它放入浮层中。设置 `AutoFocus`，使面板出现时搜索框获得焦点；设置 `ShowCloseButton` 以显示 “Esc” 按钮。在空的搜索框中按 Escape，或点击 “Esc” 按钮，都会触发 `CloseRequested`；请在该事件中关闭浮层，并在 `ItemSelected` 之后同样关闭。本演示使用 Core 的 `ModalPresence`，它会添加遮罩、将焦点保持在浮层内，并在关闭后把焦点还给打开它的按钮。面板自带进场动效，因此模态层只做淡入。

<Demo name="OnDemand" />

```csharp
var palette = new CommandPalette { ItemsSource = commands, AutoFocus = true, ShowCloseButton = true, MaxWidth = 560 };
var modal = new ModalPresence(palette, anchor: this) { Entrance = ModalEntrance.Fade };
palette.CloseRequested += (_, _) => modal.Close();
palette.ItemSelected += (_, e) => modal.Close(DismissReason.Action);
modal.Open();
```

### 键盘快捷键

在 macOS 上按 Cmd+K，其他平台按 Ctrl+K，即可从窗口中的任何位置将焦点移到搜索框。面板会先于窗口处理该按键，因此在面板显示期间，它会优先于其他 Cmd/Ctrl+K 处理程序。当一个页面显示多个面板时，请在除其中一个之外的所有面板上将 `IsShortcutEnabled` 设为 false。`ShortcutHint` 用于修改搜索行末尾的键帽。

```xml
<arc:CommandPalette IsShortcutEnabled="False" />
<arc:CommandPalette ShortcutHint="Ctrl K" />
```

### 文本

所有文字都有英文默认值。`PlaceholderText` 是空搜索框中的提示，`Label` 为搜索框命名。`EmptyTitle` 与 `EmptyDescription` 用于填充空状态。`ClearButtonLabel`、`CloseButtonLabel` 与 `ResultsLabel` 分别为清除按钮、关闭按钮和供辅助技术使用的列表命名。

```xml
<arc:CommandPalette PlaceholderText="跳转到页面"
                    EmptyTitle="未找到页面"
                    EmptyDescription="请检查拼写，或尝试其他词语。" />
```

## API 参考

<!-- api: CommandPalette -->

<!-- api: CommandPaletteItem -->

## 无障碍

- 搜索框的无障碍名称由 `Label` 提供。结果列表的名称由 `ResultsLabel` 提供，每一行则以其标题和描述命名。
- 高亮的行会向辅助技术报告自己为已选中状态，搜索框的帮助文字即为 `Label`。
- ArrowDown 与 ArrowUp 移动高亮，并在第一个和最后一个命令处停止。Home 与 End 直接跳到它们。已禁用的命令会被跳过。
- Enter 执行高亮的命令；输入后则执行排在最前的匹配项。Escape 清空查询，在空的搜索框中则触发 `CloseRequested`。
- Cmd/Ctrl+K 将焦点移到搜索框。清除按钮与 “Esc” 按钮可通过 Tab 键到达，并显示焦点环。
- 空状态为纯文字，因此屏幕阅读器可以读出。

## 动效

- 面板以淡入方式进入，并从稍小的缩放比例上移 6 像素。
- 只有一个高亮会在各行之间移动：方向键使用短促的滑动，指针则使用快速弹簧动画。高亮首次出现的位置会淡入，没有任何行被高亮时则淡出。
- 输入时，保留的行平滑滑动到新位置，离开的行淡出，新增的行淡入。面板外框的高度通过平滑的弹簧动画跟随列表变化。若行需要移动超过六个位置，则直接一步到位。
- 结果先淡出，空状态再升起出现，因此外框只改变一次高度。
- 清除按钮从较小的缩放比例和柔和的模糊中弹入。
- 动效等级为 Reduced 时，只保留淡入淡出。Off 时，所有变化立即生效。

## 相关组件

- `SearchResults`：用于带筛选与摘录的完整搜索页面。
- `ExpandingSearch`：用于页头中的紧凑搜索框。
- `DropdownMenu`：用于单个对象的操作。
- `Combobox`：用于在表单中选择一个值。
