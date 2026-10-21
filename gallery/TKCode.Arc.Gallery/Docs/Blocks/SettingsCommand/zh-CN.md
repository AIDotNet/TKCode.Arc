---
title: 设置命令面板
summary: 用一个搜索框找到任意设置并原地修改：开关翻转、选项轮换、链接打开，全部可用键盘完成。
source-hash: 9b4358b56dc1
---

<Demo name="Inline" />

## 何时使用

- 用键盘快速找到任意设置，可与完整的设置页并存，也可代替设置页使用。
- 立即生效的设置：开关、少量命名的值，以及通往其余设置所在页面的链接。
- 通过快捷键（例如 Ctrl+, 或 ⌘,）在对话框中打开的设置面板。

## 何时不用

- 需要表单、校验或保存步骤的设置：请使用包含 `SettingsGroup` 和 `SettingsRow` 的设置页。
- 选项很多或需要自由输入的设置，例如时区或显示名称：改为链接到一个带有操作的页面。
- 不属于设置的应用命令，例如“新建项目”：请使用命令面板。

## 用法

在面板内编写设置。`SettingsCommandToggle` 为开或关，`SettingsCommandChoice` 依次切换其 `Options`，`SettingsCommandAction` 执行一条命令。`Group` 将设置列在某个标题之下，`Keywords` 添加其他可用来搜到该设置的词。焦点保留在搜索框中：上、下方向键移动高亮，Enter 执行高亮的行，左、右方向键逐个切换选项。底部栏会确认每一次变更。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SettingsCommand MaxWidth="560">
        <arc:SettingsCommandChoice Group="外观" Header="主题" Options="浅色, 深色, 跟随系统" Keywords="深色 浅色" />
        <arc:SettingsCommandToggle Group="外观" Header="紧凑模式" Description="设置行更紧凑，间距更小。" />
        <arc:SettingsCommandAction Group="账户" Header="修改邮箱" IsNavigation="True" />
    </arc:SettingsCommand>
</UserControl>
```

### 搜索

查询的每个词都必须出现在标题、说明、分组或关键词中，不区分大小写。排序依次为：标题以查询开头的行，标题中某个词以查询开头的行，标题包含查询的行，其余的行。同等优先级的行保持声明顺序，分组跟随其最佳的一行排序。标题中匹配的部分保持前景色，其余部分改为次要色。没有匹配时，面板会说明并建议换一个词。

### 在对话框中

在 `Dialog` 中将 `IsFramed` 设为 `False`，使面板不再绘制第二层外框。示例还为它设置了与对话框内边距相同大小的负外边距，使搜索行和底部栏延伸到对话框边缘。查询为空时按下 Escape 会触发 `CloseRequested`。请在此关闭对话框并将事件标记为已处理，使对话框不会再次处理同一个按键。示例还使用 Ctrl+,（Mac 上为 ⌘,）打开对话框，这是设置的常用快捷键。

<Demo name="InDialog" />

```csharp
private void OnCloseRequested(object? sender, RoutedEventArgs e)
{
    e.Handled = true;
    SettingsDialog.Close();
}
```

### 绑定值

`IsChecked` 与 `SelectedOption` 为双向绑定，应用修改值时，各行随之更新。`SettingChanged` 只在面板中的修改发生时引发。其 `Value` 对开关为 `bool`，对选项则为所选的选项。操作会先执行其 `Command`，然后引发 `ItemInvoked`。

<Demo name="Bound" />

```csharp
palette.SettingChanged += (_, e) => Save(e.Item.Header, e.Value);
palette.ItemInvoked += (_, e) => Navigate(e.Item.Header);
```

### 快捷键与操作

`Shortcut` 以键帽的形式显示操作的组合键。用 `mod` 表示命令键，它在 Mac 上是 ⌘，其他平台上是 Ctrl。面板只负责显示快捷键，实际响应由应用处理。`IsNavigation` 会为打开其他页面的操作加上箭头。将 `IsEnabled` 设为 `False`，可显示当前无法修改的设置：该行变暗，键盘会跳过它。

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandAction Header="下载你的数据" Shortcut="mod+shift+e" />
    <arc:SettingsCommandAction Header="两步验证" IsNavigation="True" />
    <arc:SettingsCommandToggle Header="已读回执" Description="已由管理员关闭。" IsEnabled="False" />
</arc:SettingsCommand>
```

### 文本

内置文本来自 `Labels`。替换整个对象即可翻译。

```csharp
palette.Labels = new SettingsCommandLabels
{
    Placeholder = "Instellingen zoeken",
    NoResults = query => $"Geen instellingen voor “{query}”",
    TurnedOn = header => $"{header} aan",
    TurnedOff = header => $"{header} uit",
};
```

## API 参考

<!-- api: SettingsCommand -->
<!-- api: SettingsCommandToggle -->
<!-- api: SettingsCommandChoice -->
<!-- api: SettingsCommandAction -->

### SettingsCommandLabels

| 属性 | 类型 | 说明 |
|---|---|---|
| `Placeholder` | `string` | 搜索框的占位文字：“搜索设置”。 |
| `SearchName` | `string` | 搜索框的无障碍名称：“搜索设置”。 |
| `ResultsName` | `string` | 结果列表的无障碍名称：“设置”。 |
| `NoResults` | `Func<string, string>` | 没有匹配时的提示：“没有匹配‘主题’的设置”。 |
| `NoResultsHint` | `string` | 其下方的提示：“换一个词再试，例如主题或邮箱。” |
| `Navigate`, `Toggle`, `Change`, `Open`, `Close` | `string` | 底部栏中按键旁的操作提示。 |
| `EscapeKey` | `string` | Escape 键帽的文字：“Esc”。 |
| `TurnedOn`, `TurnedOff` | `Func<string, string>` | 开关变化后的提示：“紧凑模式已开启”。 |
| `ChangedTo` | `Func<string, string, string>` | 选项变化后的提示：“主题：深色”。 |
| `Ungrouped` | `string` | 未设分组的设置所用的分组标题，在其他设置有分组时显示：“设置”。 |
| `On`, `Off` | `string` | 辅助技术朗读开关状态时使用的文字。 |
| `RowState` | `Func<string, string, string>` | 行的标题与状态：“深色模式，开启”。 |
| `PreviousOption`, `NextOption` | `string` | 选项值两侧箭头的名称。 |

### 事件参数

| 类型 | 属性 | 说明 |
|---|---|---|
| `SettingsCommandChangedEventArgs` | `Item` | 发生变化的设置。 |
| `SettingsCommandChangedEventArgs` | `Value` | 新值：开关为 `bool`，选项为所选的选项。 |
| `SettingsCommandItemEventArgs` | `Item` | 被调用的操作。 |

## 无障碍

- 搜索框名为“搜索设置”。其帮助文字会跟随高亮的行变化，例如“紧凑模式，关闭”或“主题，深色”。
- 结果是名为“设置”的列表，每一行都是以其标题命名的列表项。
- 键盘焦点始终留在搜索框中。上、下方向键移动高亮并跳过已禁用的行，Page Up 与 Page Down 各移动五行。列表不循环。
- Enter 会切换开关、将选项移到下一个值，或执行操作。左、右方向键逐个切换选项且不循环。其他情况下，左、右方向键只移动插入符。
- Escape 会清空查询。查询为空时按下 Escape 会触发 `CloseRequested`。
- 变化提示是礼貌的实时区域，因此屏幕阅读器会朗读“紧凑模式已开启”。
- 开关、选项旁的箭头和键帽都无法获得焦点。点击任一行的效果与按 Enter 相同，键盘焦点仍保留在搜索框中。

## 动效

- 高亮会以灵敏的弹簧在各行之间滑动。结果变化时，高亮会立即移动。
- 列表平滑滚动，以保持高亮行及其分组标题可见。
- 选项的值会按变化方向上滚或下滚，开关的滑块也会滑动。
- 变化提示会伴随柔和的模糊上移出现，并在 2.5 秒后淡出。
- 减少动效时，提示与值淡入淡出，高亮立即移动。关闭动效时，每项变化都立即显示。

## 相关组件

- `SettingsPage`：带有分区导航和保存条的完整设置页。
- `SecuritySettings`、`BillingOverview`、`UsageBilling` 与 `CancelFlow`，即其他设置区块。
- `Switch`：设置行中的开关控件；`Dialog`：通过快捷键承载面板的对话框。
- `SearchField` 与 `Select`：用于表单中单独的搜索或选择。
