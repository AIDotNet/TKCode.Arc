---
title: Emoji picker
summary: 按名称查找表情，或浏览每个分类；最近用过的表情排在最前，人物和手势可以选择肤色，表情网格可以完全用键盘操作。
source-hash: e2e8bd91b485
---

<Demo name="Preview" />

## 何时使用

`EmojiPicker` 是一次选择一个表情的面板。顶部的搜索框会随着输入按名称和关键词筛选；下面是一排分类图标，可以跳到各个 Unicode 分组；网格显示所有表情，最近选过的表情单独放在最上面的一节。搜索框旁边的手势按钮为所有带肤色的人物和手势设置肤色。网格可以完全用键盘操作，只绘制可见的行；底部一行显示指针或键盘所在表情的名称。

- **聊天或评论中的回应和回复**：从消息框旁边的笑脸按钮打开。
- **状态和个人资料字段**：状态消息或工作区名称前面放一个表情。
- **标签和标记**：用表情作为频道、项目或保存视图的图标。

## 何时不用

- **固定的几个回应**：直接把这几个显示为按钮；为一个点赞打开完整的选择器是绕远路。
- **为界面挑选图标**：表情使用系统字体，在每个系统上看起来都不同；结果必须处处一致时，用图标集。
- **输入可能包含表情的文字**：系统自带的表情键盘在所有文本框中都能用；把选择器作为快捷方式提供，而不是唯一的方式。

## 用法

把选择器放在用户够得到的地方，通常是消息框旁边的 `Popover` 中，并处理 `EmojiSelected`。`e.Emoji` 是要插入的文字，已经应用了所选的肤色。

```xml
<arc:EmojiPicker MaxRecent="16" />
```

```csharp
picker.EmojiSelected += (_, e) => message.Text += e.Emoji;
```

`EmojiPicker` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。

### 放在弹出面板中

选择器自带一个凸起的面板。放在本身已经是面板的 `Popover` 中时，把 `Background` 设为 `Transparent`，`BorderThickness` 和 `Padding` 设为 0。弹出面板打开时焦点进入搜索框，用户可以直接输入；在 `EmojiSelected` 中关闭弹出面板。

<Demo name="InComposer" />

### 最近使用

每次选择都会把表情（不含肤色）移到 `Recent` 的最前面，最多 `MaxRecent`（24）个。这一节显示的是选择器上次显示时的列表，所以用户挑选时表情不会移动。填充 `Recent` 可以恢复保存的列表，监听它的 `CollectionChanged` 可以保存列表；把 `MaxRecent` 设为 0 可以隐藏这一节。

```csharp
foreach (var emoji in settings.RecentEmoji)
{
    picker.Recent.Add(emoji);
}
picker.Recent.CollectionChanged += (_, _) => settings.RecentEmoji = picker.Recent.ToList();
```

### 肤色

`SkinTone` 为所有带肤色的表情应用五种肤色之一，或默认的黄色；其他表情不变。它是双向的，所以选择器中的手势按钮和你自己的设置保持一致。

<Demo name="SkinTone" />

### 搜索

搜索匹配词的开头：“hea”找到所有爱心，“red hea”找到红色爱心。名称中的匹配排在前面，然后是 CLDR 关键词中的匹配（“happy”“lol”）。`SearchText` 是双向的，页面可以带着查询词打开选择器。在搜索框中按下方向键进入结果，按 Enter 选择第一个结果。

### 表情版本

表情数据来自 Unicode 16.0，但图形来自系统的表情字体。`MaxEmojiVersion`（默认 15.0）会排除更新的表情，所以缺少它们的系统不会显示空白方框。只面向较新系统时可以调高。

### 结构

1. **搜索**（`IsSearchVisible`）：搜索框和肤色手势按钮。
2. **分类**：每节一个图标；网格滚动时，可见分节下方的短条会滑过去。
3. **网格**：分节标题（顶部的标题保持固定）和一行行 36px 的格子。
4. **预览**（`IsPreviewVisible`）：指针或键盘所在的表情，放大显示，并带有名称。

### 文案建议

| 不要写 | 改为 |
|---|---|
| Emoji | Add emoji（触发按钮的无障碍名称） |
| Type to search for an emoji... | Search emoji |

## API 参考

<!-- api: EmojiPicker -->

<!-- api: EmojiSelectedEventArgs -->

<!-- api: EmojiPickerLabels -->

枚举：

| 类型 | 取值 |
|---|---|
| `EmojiCategory` | `Recent`、`Smileys`、`People`、`Nature`、`Food`、`Travel`、`Activities`、`Objects`、`Symbols`、`Flags` |
| `EmojiSkinTone` | `Default`、`Light`、`MediumLight`、`Medium`、`MediumDark`、`Dark` |

## 无障碍

- 搜索框以 `Labels.Search` 命名；每次变化后报告匹配的数量（“12 emoji found”）或没有匹配。
- 分类条是一个 Tab 停靠点，以有焦点的分类命名。左右方向键在分类之间移动，并让网格跳过去；Home 和 End 到第一个和最后一个；Enter 或 Space 跳到有焦点的分类。
- 网格是一个 Tab 停靠点。它的无障碍名称是当前表情的名称，帮助文本说明所在分节和位置（“Smileys and emotion, 3 of 168”）。
- 肤色手势按钮名为“Skin tone: Medium”这样的文字；它会展开成一排六种肤色。

### 键盘

| 按键 | 位置 | 作用 |
|---|---|---|
| 输入文字 | 搜索 | 筛选网格 |
| `Down` | 搜索 | 进入网格 |
| `Enter` | 搜索 | 选择第一个结果 |
| `Escape` | 搜索 | 清空搜索 |
| 方向键 | 网格 | 移动当前表情，可以跨过分节标题 |
| `Home` / `End` | 网格 | 本行的第一个或最后一个表情 |
| `Ctrl` + `Home` / `End` | 网格 | 所有表情中的第一个或最后一个 |
| `Page Up` / `Page Down` | 网格 | 移动一屏的行 |
| `Enter` / `Space` | 网格 | 选择当前表情 |
| 字母 | 网格 | 继续在搜索框中输入 |
| `Left` / `Right` | 肤色（展开时） | 在肤色之间移动；`Enter` 选择，`Escape` 收起 |

## 动效

- 选择分类后，网格以平滑的弹簧动画滚动过去；滚轮或拖动会立即接手。
- 分类下方的短条以灵敏的弹簧动画滑到可见的分节。
- 肤色行以宽度弹簧动画展开和收起。
- 搜索时，分类条在 160ms 内淡到一半不透明度。
- 减少动效时跳转和短条移动立即完成，淡出为 120ms；关闭动效时一切立即完成。

## 响应式

- 选择器默认宽 352px；网格在可用宽度内放下尽可能多的 36px 列，并居中排列。
- 网格高 288px，可以滚动；只绘制可见的行，每个表情的文字布局在它第一次显示时才创建。
- 放在较窄的弹出面板中时，设置合适的 `Width`；分类会铺满可用的宽度。

## 相关组件

- `Popover`：从按钮打开选择器。
- `MentionInput` 和 `TokenTextEditor`：带有行内标记的文本输入。

| 组件 | 适用场景 |
|---|---|
| `EmojiPicker` | 用户按名称或浏览来选择任意表情 |
| `Popover` | 显示几个你自己固定的回应 |
| `SegmentedControl` | 几个类似表情的选项中的一个是一项设置 |
