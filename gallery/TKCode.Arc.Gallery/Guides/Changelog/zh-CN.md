---
title: 更新日志
source-hash: d98231ccb287
---

# 更新日志

TKCode.Arc 的所有重要变更，最新的在前。版本遵循[语义化版本](https://semver.org/)。从 1.0.0 起，破坏性 API 变更只在新的主版本中出现；更早的 `-preview` 版本在各发布版本之间可能变更 API。同样的列表也保存在仓库的 `CHANGELOG.md` 中。

每次发布附带的 NuGet 包：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Foundation`（0.3.2 起）、`TKCode.Arc.Fonts` `TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Dock`（0.4.0-preview.2 起）、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners`、`TKCode.Arc.Mobile`、`TKCode.Arc.Terminal`、`TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native`、`TKCode.Arc.Audio.Browser`、`TKCode.Arc.Templates`（0.4.0-preview.6 起）、`TKCode.Arc.Pdf`、`TKCode.Arc.Pdf.Native` 和 `TKCode.Arc.Pdf.Browser`（1.0.0 起），`TKCode.Arc.Video`、`TKCode.Arc.Video.Native` 和 `TKCode.Arc.Video.Browser`（1.1.0-preview.1 起）。

## 1.1.0-preview.5（2026-11-12）

`TKCode.Arc.Special` 的“停靠与标签页”分组被重建（`OrbitMenu`、`LiquidTabBar`、`ShareSheet`、`ActionMorph` 和 `MorphNav` 的 API 有变化，迁移说明见下文；`ToolDock` 是新增的），`AvatarUpload` 移入编辑器分组，应用反馈（#107–#112）带来了 `WindowCaptionButtons`、设置页选项、内容与媒体令牌，以及 `WaterfallPanel` 和 `TilePanel` 的自动尺寸。相对 1.1.0-preview.4 的破坏性变更仅出现在这五个 Special 控件中。安装时使用 `--version 1.1.0-preview.5`（或 `--prerelease`）。

### 新增

- **`WaterfallPanel` 和 `TilePanel` 自行排布同一模板的条目**（[#112](https://github.com/AIDotNet/TKCode.Arc/issues/112)，源自 [#86](https://github.com/AIDotNet/TKCode.Arc/issues/86)）：`WaterfallPanel.ItemSizing="Varied"` 让每个子项获得一个高度（以行数计的宽度），范围在 `MinItemSize` 与 `MaxItemSize` 之间，而不是测量其内容，因此用同一模板生成的卡片仍能组成瀑布墙；未设置的边界跟随列宽。`TilePanel.IsAutoSpan` 让未设置跨度的子项取 1×1、1×2、2×1 或 2×2，放在第一个空格并使较大的瓦片保持分开；设置 `IsAutoFill` 和 `Rows` 时，自动瓦片会铺满整个 `Columns` × `Rows` 网格。两个面板的尺寸都由子项的索引和新属性 `Seed` 决定，布局时从不使用随机值：相同的条目、种子和网格总得到相同的结果，调整大小也不会重新打乱。默认值（`Content` 尺寸、关闭自动跨度）保持现有布局不变。
- **`InlineEdit.ReserveActionSpace`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`False` 去掉单行在文本后为保存和取消按钮保留的 66 px，使输入框紧贴文本，标签或徽章可以跟在名称后面。静止时框外不绘制任何东西（没有铅笔提示、保存中的旋转指示或结果标记）；编辑时按钮覆盖其后的内容，控件绘制在同一面板中其后面兄弟的上方。编辑时不会再恢复这段保留空间，因此按钮出现时文本和标签都不会跳动。多行输入框保留 38 px。默认值（`True`）不变。
- **内容宽度与媒体令牌**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`ContentMaxWidthSM` / `MD` / `LG` / `XL`（640 / 760 / 880 / 1120）限制页面的内容栏（阅读栏或单个表单、设置页、文档页、全宽分区），`MediaSizeSM` / `MD` / `LG` / `XL`（64 / 128 / 192 / 256）设置方形媒体的尺寸（行内缩略图、卡片预览、二维码或封面图、大图预览）。用法为 `{arc:ArcDesignTokenResource ContentMaxWidthMD}`，覆盖方式与其他 ArcDesign 令牌相同。它们追加在 `ArcDesignTokenKind` 末尾，因此现有成员的编号不变。
- **`AvatarSize.LargePlus`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：64 px 的头像，介于 `Large`（48）和 `XLarge`（88）之间，带 `TypeLG` 首字母，状态点为 13 px，距角落向内 2 px。它追加在枚举末尾，因此现有成员的值不变；`AvatarGroup` 将其绘制为 `Large`。
- **`SettingsSection.BadgeTone`**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)，`TKCode.Arc.Blocks`）：用 `BadgeTone` 为分区的导航徽章着色（“需要注意”用 `Warning`，检查失败用 `Danger`）。默认值 `Neutral` 保持现有外观。
- **`SettingsRow.Details` 和 `SettingsRow.Leading`**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)，`TKCode.Arc.Blocks`）：`Details` 接受描述下方的任意内容，例如状态行、错误或一排标签（文字小且偏淡）。`Leading` 接受任意内容以替代图标块，例如 `Avatar`；它保持自己的尺寸，`LabelWidth` 也包含它。`Icon` 不变。
- **`SettingsPage.SelectedValue`**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)，`TKCode.Arc.Blocks`）：新增的 `SettingsSection.Value` 标识一个分区（枚举成员或路由键），页面的双向 `SelectedValue` 即被选分区的 `Value`，因此显示的分区可以绑定到视图模型属性。设置 `SelectedValue` 会选中匹配的分区。没有 `Value` 的分区仍像以前一样报告自身；`SelectedValueBinding` 可用其他方式读取该值。
- **`RadioGroup`、`RadioCards` 和 `ToggleGroup` 的公共条目容器**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)）：`RadioGroupItem`、`RadioCard` 和 `ToggleGroupItem`（以及它们的基类 `TogglesItem`，该类仅供库内派生）现为公共类型。数据绑定的条目与 Avalonia 的 `ListBoxItem` 行为相同：`arc|RadioGroupItem` 上的样式或分组的 `ItemContainerTheme` 可以绑定条目的 `Description`、`Icon`、`Meta`、`IsEnabled` 和 `AutomationProperties.Name`，因此条目不必是 `RadioOption` 或 `ToggleGroupOption` 也能获得这些功能。每个容器都有自己的令牌（`RadioGroupItem.MinRowHeight`、`RadioCard.GlyphSize`，以及 `ToggleGroupItem.PressScale`、`PressDownDuration` 和 `PressUpDuration`，用于原本固定的按下效果）；默认值保持现有外观，现有的 `RadioGroup`、`RadioCards` 和 `ToggleGroup` 令牌不变。
- **`WindowCaptionButtons`**（[#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)）：最小化、最大化/还原和关闭，作为独立的控件，用于内容延伸到顶部的窗口（即 `IsTitleBarVisible="False"` 的 `ArcWindow`）。可以放在你自己的标题栏任意位置：它们操作所在的窗口，窗口最大化时显示还原，跟随 `CanResize`、`CanMaximize` 和 `CanMinimize`，在操作窗口时携带系统角色（Windows 11 上的贴靠布局），并保留本地化的提示文字。`ShowMinimizeButton`、`ShowMaximizeButton` 和 `ShowCloseButton` 隐藏某个按钮，`IsPreview` 只绘制按钮而不操作窗口。`TitleBar` 现在用此控件绘制其标题栏按钮，并在 `CaptionButtons` 为 `System` 或 `None` 时放弃这些角色，使窗口只保留一套按钮。
- **`PageHeader.IsTitleBar`**（[#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)，`TKCode.Arc.Blocks`）：`True` 让页头（而非正文）成为窗口的标题栏：拖动其空白部分、标题或说明会移动窗口，双击会最大化它；面包屑、紧凑标题、操作、元信息行和标签页仍保持各自的点击。在 Windows 上，页头即系统标题栏，这些部分属于用户内容。`False`（默认）不改变任何东西。
- **应用中的 `ArcOverlayHost`**（[#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)，`TKCode.Arc.Core`）：文档不再说应用不需要它。把应用内容放进一个宿主，标题栏放在它旁边，从内容打开的对话框、抽屉或面板只覆盖宿主，永远不覆盖标题栏及其窗口控制按钮（见窗口标题栏按钮页面）。
- **`AvatarUpload` 通过文件的开头字节检查文件，并保存编码后的图片**：所选、拖入或加载的文件，其类型先依据开头字节判定，再看文件名（把 PDF 改名为 `.png` 会被拒绝，名称奇怪的照片则不会），且在大小检查之后进行；即使流无法提供长度，读取也会在 `MaxFileSize` 处停止。新增的 `Accept` 收窄类型（MIME 类型、`image/*` 或扩展名），选择器随之过滤；`LoadAsync(byte[], name)` 接受粘贴的图片或相机拍摄结果。`AvatarUploadRequest` 新增 `Bytes`、`ContentType`、`SuggestedFileName` 和 `OpenStream()`：保存的正方形图片由新的 `OutputFormat` 编码（`Auto` 对 PNG 保持 PNG，对未知类型的图片也按 PNG 处理，对其他图片保存为 JPEG；`Png`、`Jpeg`），`JpegQuality`（默认 92，JPEG 以白色为底）。没有图片时 `DisplayName` 显示首字母，`AllowRemove` 隐藏移除按钮，结果和以四分之一为单位的进度通过实时区域播报给屏幕阅读器，拖过取景区边缘时会有阻力并弹回（保存的裁剪永远不包含拉伸的部分）。默认值不变。文档新增了示例照片和失败情况两个示例。

- **`ToolDock`**（`TKCode.Arc.Special`，停靠与标签页）：用于画布式编辑器的浮动工具或模式停靠栏。选中的工具位于一层色调上，色调从上一个工具弹簧式地滑过来；名称和快捷键提示标签会滑到鼠标或键盘焦点所在的工具上（触摸时从不如此）；分组（`ToolDockItem.Items`）会在停靠栏外长出一个托盘，成员都在一条细线轮廓内；徽章的计数滚动变化，归零时消失；`CanReorder` 让人们通过拖动或 Alt+左/右键移动工具（`Reordered`）。`SelectedValue` 是双向的，`ItemChosen` 报告每次选择；停靠栏作为一个 Tab 键焦点位置，采用工具栏的键盘模型。

### 变更

- **`TilePanel.IsAutoFill` 也会从侧面和下方填补空洞**（[#112](https://github.com/AIDotNet/TKCode.Arc/issues/112)）：瓦片向右和向下生长后，每个仍为空的空洞都会交给周围的瓦片，因此右侧或下方的邻居也可以长过来填补它。瓦片仍然不会移动、重叠或缩小，跨度上限依然有效。
- **`AvatarUpload` 从 `TKCode.Arc.Special` 移到 `TKCode.Arc`**（编辑器分组，在图库中位于 `SignaturePad` 和 `FileDropzone` 之间）：命名空间、类及其 API 都不变，因此代码像以前一样编译，并且 `UseArcControls()` 现在会注册它（`UseArcSpecial()` 不再注册）。它的文档页从 Special › 输入 移到了 组件 › 编辑器。
- **图库中 Special › Navigation 改名为“停靠与标签页”**，其下列出 Share sheet、Action morph、Morph nav、Tool dock、Liquid tab bar 和 Orbit menu，`MagnifyDock` 也从 Playful 移入此处。源文件夹、命名空间和 slug 不变。
- **破坏性变更：`OrbitMenu` 变成按下—拖动—松开的菜单。** 按下触发按钮会打开圆弧；朝某个操作拖动并松开即触发它（指针离开按钮一点距离后，朝向某个操作就已足够），轻点则让圆弧保持打开，以便悬停和点击。一个透镜滑到目标下方，靠近指针的操作会被吸向它，按钮的标记从三个点变为叉号，操作执行后变为对勾；一个药丸形标签指出目标，指针远离所有操作时显示 `CancelText`，然后显示该操作的 `DoneText` 1.4 s，开启 `Announces` 时会向屏幕阅读器播报。新增：`Direction`、`Spread`、`CancelText`、`Announces`、`OrbitMenuAction.DoneText` 和 `Tone`（`OrbitMenuActionTone.Danger`）、`OrbitMenuActionEventArgs.Item`。迁移：`StartAngle` / `SweepAngle` 由 `Direction`（圆弧的中点）和 `Spread`（圆弧的宽度；旧默认值为 `Direction="-90" Spread="180"`）替代；`Glyph`、`ClosesOnInvoke` 和 `OrbitMenuAction.FocusRingBrush` 已移除（圆弧总会收起并确认，请处理 `ActionInvoked`；键盘焦点以透镜显示）；`Radius` 默认值为 104；触发按钮为 56 px，操作为 46 px；操作不再有 Tooltip。
- **破坏性变更：`LiquidTabBar` 用透镜选择。** 选中项是一个药丸，会滑到所选标签处，途中拉伸其宽度而不变形，并以反色绘制其覆盖的内容，因此轮廓图标会变为实心（用 `ActiveGlyph` 绘制另一种）；旧标签的文字立即消失，新标签的文字在透镜到达时才出现；透镜会向悬停的标签倾斜，并可被拖动或轻弹。新增：`Labels`（`Active`、`Always`、`None`）、`LensBackground`、`LensForeground`，以及 `LiquidTabBarItem` 上的 `Value`、`Label`、`Glyph`、`ActiveGlyph`、`Badge`（变化时其数字滚动）和 `BadgeLabel`。迁移：`Viscosity` 已移除；为每一项设置 `Glyph` 和 `Label`（没有图标的项是文字标签）；`SelectedValue` 报告项的 `Value`；鼠标在松开时选中；上/下键不再移动选择；若要保留强调色透镜，把 `LensBackground` / `LensForeground` 设为 `Accent` / `AccentForeground`；`ItemsPanel`、`Padding` 和 `CornerRadius` 不再使用。
- **破坏性变更：`ShareSheet` 从自己的按钮扩展为分享面板。** 同一个形状从按钮弹出，变为一个面板，包含项目标题、链接和“复制”、访问权限列表、带待处理、完成和失败状态的渠道按钮（`ChannelHandler`），以及最近联系人（其头像飞入“发送给”一行）；“发送”运行 `SendHandler`，失败时显示错误并保留面板原样，成功时收缩为带撤销的确认卡片。宽度不超过 520 px 时，面板从底部升起覆盖在变暗的页面上，向下拖动即可关闭（`SheetOnPhones`），`Align` 选择保持不动的那一边。迁移：触发器不再是 `Content`（请设置 `Label` 和 `Glyph`）；`ShowAt`、`Presentation`、`Subtitle`、`Actions`、`ActionInvoked`、`Shared`、`ClosesOnShare` 以及页眉和复制标签相关属性已移除（请改用 `Title`、`Labels`、带 `ChannelHandler` 的 `Channels`，以及 `SendHandler` 或 `Sent`）；`People` 持有 `ShareSheetPerson`；`LinkCopied` 传递 `ShareSheetLinkCopiedEventArgs`。
- **破坏性变更：`ActionMorph` 从按钮变为菜单、表单和勾选。** 56 px 的按钮先长成一个简短的菜单，列出可创建的内容（每行带描述和字母快捷键，高亮会滑动），然后变为所选操作的表单（单行或多行输入框、可选的选项，按 Enter 或 Ctrl/⌘+Enter 提交），最后收回成带绘制对勾的按钮；`ResetAfter`（1.6 s）之后加号返回。`SubmitHandler`（或 `Submitted`）负责保存，空提交会抖动，提交失败则保留草稿并显示错误，每个操作都会记住未发送的文字，`Corner` 决定表面的生长方式。迁移：把每个面板描述为 `Actions` 中的 `ActionMorphAction`（`Title`、`Placeholder`、`IsMultiline`、`Choices`、`SubmitLabel`、`SuccessLabel`、`EmptyHint`），而不是内容；`Glyph`、`Variant`、`CollapsesOnOutsidePress`、`ShowsCloseButton` 和 `ActionMorph.Closes` 已移除；控件在布局中占 56 × 56，并在其相邻控件上方绘制表面，因此应把它放在 `Panel` 中角落位置的最后。
- **破坏性变更：`MorphNav` 是网站导航栏。** 一个表面容纳品牌、带滑动高亮的项目、搜索和一个主要操作；带链接的项目会把导航栏扩展为其面板（链接分为一或两列，可选的功能区如 `MorphNavStory` 和页脚），相邻项目的面板从其一侧滑入，悬停 60 ms 后打开、180 ms 后关闭，搜索从导航栏本身打开（空查询时给出建议，最多六个排序结果），`IsCompact` 让导航栏更紧凑，当导航栏窄于 `CollapseBelow`（720 px）时，各项会折叠为一个菜单项。迁移：`MorphNavSection` 即 `MorphNavItem`（`Header` → `Label`，另加 `Value`）；`MorphNavLink.Text` / `Glyph` / `Tag` 即 `Label` / `Icon` / `Href`；`Title` / `Mark` 即 `BrandName` / `BrandMark`；`CurrentLink` 即 `Current`（项目的 `Value`）；`LinkInvoked` 即 `Navigate`（`MorphNavNavigateEventArgs`）；`IsOpen`、`Open()`、`Close()` 和 `Toggle()` 即 `Open(value)`、`ClosePanel()` 和 `OpenValue`；导航的 `Footer` 移到 `MorphNavItem.Footer`；`PanelWidth` 和 `CloseLabel` 已移除。

### 修复

- **头像状态点尺寸**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：状态点现在按文档所述跟随头像尺寸（`Small` 时为 8 px 加 1.5 px 的圆环，`Medium` 为 10 px，`Large` 为 12 px，`XLarge` 为 14 px），忙碌横条在每种尺寸下都横跨状态点。以前每个头像都绘制 10 px 的中号状态点：旧模板中的局部值遮住了尺寸样式。
- **`InlineEdit` 多行保留空间**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：多行输入框现在按文档为其堆叠的保存和取消按钮保留 38 px，而不是 66 px，因此其文字比之前晚 28 px 才换行。模板自身的内边距曾遮住了多行样式。
- **`OrbitMenu` 首次打开**：菜单第一次打开时，各操作会在圆弧上的位置短暂显示，然后落回触发按钮并再次展开；现在它们只从触发按钮弹出一次。

## 1.1.0-preview.4（2026-11-11）

来自迁移到 Arc 的应用的反馈（#102–#110）：`PageHeader`、`Dialog`、`CommandPalette`、`Avatar`、`EmptyState`、`InlineEdit`、`CodeBlock` 和 `Tooltip` 增加了新选项，并修复了 `Table`、`ToggleGroup`、`SettingsGroup`、Mobile `ListGroup`、模态覆盖层、`Gauge` 和 `ArcWindow` 的问题。与 1.1.0-preview.3 相比没有破坏性变更。安装时使用 `--version 1.1.0-preview.4`（或 `--prerelease`）。

### 新增

- **适合设置页和侧栏的 `PageHeader`**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)，`TKCode.Arc.Blocks`）：`IsHeaderOnly="True"` 只显示页头，没有标签页、分隔线和滚动区，用作页面顶部的标题和操作栏；`Size="Small"` 是紧凑页头（`TypeXL` 标题，周围留白更小）；`PageHeaderAction.IsVisible="False"` 隐藏操作的按钮和菜单行，而不是让它们变淡。
- **`Dialog.ShowHeader`**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)）：设为 `False` 时隐藏标题行（标题、说明、关闭按钮和分隔线），适合自带标题的对话框。`Title` 仍是无障碍名称。
- **由应用提供结果的 `CommandPalette`**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)，`TKCode.Arc.Blocks`）：在面板或对话框上设置 `IsFilteringEnabled="False"`，会按原样列出 `ItemsSource`，适合自己筛选和排序结果的应用。`CommandPaletteDialog` 新增 `Query`（双向）和 `ActiveItem`，在对话框第一次打开前就能读取。从代码设置的查询会把光标放在末尾。
- **更多头像在线状态**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`AvatarStatus` 新增 `Busy`（危险色，圆点上带一条横杠）、`Away`（警告色）和 `Connecting`（以不透明度呼吸的空心圆环，关闭动效时静止）。每种状态都是头像无障碍名称的一部分。
- **`EmptyState.Size`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`Small` 是用于列表栏和卡片的紧凑空状态（32 px 图标块，上下各 16 px，间距更紧凑），在任何窗口宽度下都生效。
- **`InlineEdit.Labels`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`InlineEditLabels` 对象可本地化屏幕阅读器听到的所有文字：“Saving …”和“… saved”播报、保存和取消按钮的名称以及帮助文字。英文默认值不变。
- **代码中的 diff 着色**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：语言为 `diff` 或 `patch` 时，`CodeBlock`、Markdown 代码围栏和富文本编辑器的代码块会把新增行显示为成功色、删除行显示为危险色，文件头和块头显示为次要文字颜色。之前这些行借用了数字色和字符串色。文档中说明了 `text` 和 `plaintext` 显示为纯文本。
- **`Tooltip.ShowOnDisabled`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：在禁用的控件上显示提示，例如说明按钮为何不能使用。禁用的控件收不到指针输入，因此提示改为跟踪指针是否位于控件范围内。之前 Tooltip 文档说这已经可以使用，实际上并不能。

### 修复

- **小尺寸的 `Gauge`**：数字现在随圆环缩放（最大尺寸时为 `Type3XL`），因此在小表盘上也不会超出圆环；位于圆环中心的是数字的墨迹而不是行框，因此数字不再偏上。
- **Windows 上 `ArcWindow` 的上边框**（[#102](https://github.com/AIDotNet/TKCode.Arc/issues/102)）：客户区延伸到标题栏时，DWM 不绘制窗口顶部的边框线，因此窗口在 Windows 上处于正常状态时会自己用 `Border` 颜色画出这条线（`:top-edge` 伪类）。四条边一致；用 `ArcWindow:top-edge /template/ Border#PART_Root` 上的样式可以去掉它。
- **未布局时更换数据源的 Table**（[#103](https://github.com/AIDotNet/TKCode.Arc/issues/103)）：`Table` 在隐藏或不在可视树上时更换了 `ItemsSource`，之后切换主题（例如页面重新显示时）不再抛出 `ArgumentOutOfRangeException`。行会等到下一次布局再读取新的数据源。
- **使用 `ListItem` 模板的 `ListGroup`**（[#104](https://github.com/AIDotNet/TKCode.Arc/issues/104)，`TKCode.Arc.Mobile`）：当 `ItemTemplate` 的根是 `arcm:ListItem` 时，这一行直接作为容器，不再套第二层，因此可以在模板中绑定 `Title`、`Subtitle`、`Command` 等行属性。
- **使用自定义 `SelectedValueBinding` 的 `ToggleGroup`**（[#105](https://github.com/AIDotNet/TKCode.Arc/issues/105)）：`SelectedValues`、`SelectedValue` 和 `ValueChanged` 事件报告的是绑定取出的值而不是项本身，双向绑定的 `SelectedValue` 在控件显示时也不再被覆盖。
- **由 `ItemsSource` 生成行的 `SettingsGroup`**（[#106](https://github.com/AIDotNet/TKCode.Arc/issues/106)，`TKCode.Arc.Blocks`）：第一行上方不再多一条细线，模板生成的行上设置的 `ColumnSpan` 在第一次布局时就生效。
- **逻辑树中的模态内容**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)）：`Dialog`、`Drawer` 或 `BottomSheet` 的内容在 `GetLogicalDescendants()` 中只出现一次（位于覆盖层之下），不再出现两次。

## 1.1.0-preview.3（2026-11-10）

性能更新：应用可以限制持续循环重绘的频率。与 1.1.0-preview.2 相比没有破坏性变更。安装时使用 `--version 1.1.0-preview.3`（或 `--prerelease`）。

### 新增

- **`ArcMotion.MaxFrameRate`**（`TKCode.Arc.Core`）：可继承的附加属性，限制元素之下每个 `LiveLoop`（吉祥物、骨架屏脉冲、闪烁高光、实时指示点）每秒最多推进的次数。每次推进传入自上次以来经过的时间，因此动作速度不变，跳过的帧不绘制任何内容。默认值 0 与之前一样每帧推进。在始终显示的小窗口上设置它，例如 `arc:ArcMotion.MaxFrameRate="24"`，缓慢的装饰动效就不会再以显示器的全帧率重绘窗口。

## 1.1.0-preview.2（2026-11-10）

NativeAOT 修复：使用 `Sidebar` 的应用发布时不再出现裁剪警告。与 1.1.0-preview.1 相比没有破坏性变更。安装时使用 `--version 1.1.0-preview.2`（或 `--prerelease`）。

### 修复

- **NativeAOT 下的 `Sidebar`**（[#100](https://github.com/AIDotNet/TKCode.Arc/issues/100)）：应用使用 `Sidebar` 时，`TKCode.Arc.Blocks` 不再产生裁剪警告（IL2026 / IL3053）。新增的 `LabelMemberBinding`、`KeyMemberBinding`、`IconMemberBinding`、`BadgeMemberBinding`、`IsEnabledMemberBinding` 和 `ChildrenMemberBinding` 用编译绑定映射 `ItemsSource` 的数据，例如 `LabelMemberBinding="{Binding Title, DataType={x:Type local:MenuNode}}"`。字符串形式的 `*Member` 路径仍然可用，但走反射；裁剪和 NativeAOT 应用请改用 `*MemberBinding` 属性。

## 1.1.0-preview.1（2026-11-10）

1.1.0 的首个预览版：`TKCode.Arc` 新增控件，`TKCode.Arc.Special` 新增分组，新增 58 个区块，视频包发布到 NuGet，并且无需等待 Arc 新版即可在 Avalonia 12 内升级。与 1.0.0 相比没有破坏性变更。安装时使用 `--version 1.1.0-preview.1`（或 `--prerelease`）；本版本新增的 API 在 1.1.0 之前仍可能变化。

### 新增

- **视频发布到 NuGet**：发布 `TKCode.Arc.Video`、`TKCode.Arc.Video.Native` 和 `TKCode.Arc.Video.Browser`。`VideoPlayer` 和 `VideoSurface` 在桌面上通过 Media Foundation、AVFoundation 或 GStreamer 播放，在 WebAssembly 中使用浏览器的 video 元素（`UseArcVideo()`、`ArcVideoNative.Use()`、`ArcVideoBrowser.UseAsync()`）。
- **`TreeSelect`**（[#80](https://github.com/AIDotNet/TKCode.Arc/issues/80)）：基于树的选择框，可单选一个节点或勾选多个，支持三态复选框、决定返回哪些节点的 `CheckStrategy`、保留每个匹配项路径的搜索、标签，以及可容纳数千节点的虚拟化行。
- **`Sticky`**（[#84](https://github.com/AIDotNet/TKCode.Arc/issues/84)）：附加属性，让滚动页面中的任意元素滚动到顶部后固定在那里，类似 CSS `position: sticky`，支持偏移、`IsStuck` 状态和 `StuckChanged` 事件。
- **`BackToTop`**（[#67](https://github.com/AIDotNet/TKCode.Arc/issues/67)）：列表或信息流滚动超过阈值后出现的圆形按钮，点击后平滑滚回顶部；可自行放置，也可用 `arc:BackToTop.IsAttached` 附加到角落。
- **更多控件**：`Rating`（半星、清除、只读、自定义形状）、`ToggleGroup` 与 `SlotText`、`RangeHistogramSlider` 与 `RulerSlider`、`OnboardingChecklist`，以及 `SplitBar`（图表）、`MetricTicker`（卡片）和 `PresenceStack`（头像）。
- **特殊组件**（`TKCode.Arc.Special`，仅根据名称独立设计）新增分组：玻璃（`GlassCard`、`GlassMenu`、`GlassSegmented`、`GlassTabBar`）、导航（`MorphNav`、`LiquidTabBar`、`OrbitMenu`、`ActionMorph`、`ShareSheet`）、照片（`ProductGallery`、`LightboxGallery`、`PhotoGrid`、`CoverFlow`）、数据（`BookingPill`、`ActivityRings`、`TimeDial`、`DateReel`）、小组件（`KeycapKeyboard`）、输入（`EmojiPicker`、`AvatarUpload`、`ConfirmPopover`、`UndoToast`）、出版（`MegaMenuMorph`、`TocProgress`、`ShareAccess`），讲解动画 `DnsResolution`、`ModelTraining` 和 `BranchGraph`，以及 `SandText`、`TypewriterTerminal`、`PrizeWheel`、`MagnifyDock` 和 `ImageTrail`。
- **区块**（`TKCode.Arc.Blocks`）：营销区块（`HeroSection`、`FaqSection`、`ContactSection`、`StatsBand`、`BlogGrid`、`ComparisonTable`、`ChangelogFeed`、`LogoMarquee`、`HeroSignup`、`TeamShowcase`、`StudioPerspectives`、`ScrollStory`、`SpotlightGrid`、`FeatureBento`）；AI（`AgentRun`、`ResearchRun`、`AiInlineEdit`、`AgentTasks`、`AgentApprovals`、`AgentRoutines`、`AgentOrchestrator`、`AgentBuilder`、`AgentGallery`、`McpServers`、`KnowledgeBase`、`EvalSuite`、`AiInsightCard`、`AiFeatureLaunch`、`AiSearchBar`、`AiSetupWizard`、`CatchUp`、`SmartScheduler`）；支持（`SupportConversation`、`SupportWidget`、`InboxTriage`、`FeedbackBoard`、`HelpCenter`、`FeatureVoting`、`ActivityFeed`）；商务与金融（`WalletCard`、`SpendAlert`、`InvoiceStudio`、`DeliveryTracker`、`ProductListing`、`ProductDetail`、`SeatMap`）；分析与项目（`CallInsights`、`LeadScoring`、`AccountHealth`、`GoalTracker`、`PivotBuilder`、`TaskBreakdown`）；应用外壳与认证（`DocsSidebar`、`InboxSidebar`、`SelectionToolbar`、`TwoFactorSetup`、`OnboardingSurvey`、`MagicLinkSignIn`）。
- **`Sidebar.ExpandedWidth`**（区块，[#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)）：侧边栏展开时的宽度，折叠动画从这里弹出并弹回。

### 变更

- `GhostMascot`、`CubeMascot`、`AiMascot`、`LiquidTabBar`、`AiSpark` 和 `RadarChart` 每帧的内存分配大幅减少，外观不变。

### 修复

- 各个包不再把 Avalonia 锁定为一个确切版本（[#97](https://github.com/AIDotNet/TKCode.Arc/issues/97)）：现在接受 Avalonia 12.1.5 及任何更高的 12.x 版本，应用可以在 12 版本内升级 Avalonia，无需等待 Arc 发布新版。
- 减少动效时加载指示器不再冻结（[#93](https://github.com/AIDotNet/TKCode.Arc/issues/93)）：旋转图标和流动线条保持静止，改为透明度呼吸。
- `Dialog`、`Drawer`、`BottomSheet` 和 `Tooltip` 的圆角下方不再露出深色直角（[#75](https://github.com/AIDotNet/TKCode.Arc/issues/75)）。
- `Sidebar`（区块）调整大小后可以再次折叠，标题中居中的内容保持居中，内置文字跟随 Arc 语言（[#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)）。请设置 `ExpandedWidth` 而不是 `Width`。
- 指针停留在触发项上时，`MegaMenuMorph` 不再反复打开和关闭。
- `TextFilterBox` 与 `NumberFilterBox` 的输入框不再重叠或被裁切：过长的标签会换行，空间较窄时标签移到输入框上方。
- `Table` 的紧凑手机布局比行内边距更窄时不再抛出 "Invalid Arrange rectangle"，`ChatThread` 气泡跟随主题的圆角风格。
- Windows 图库应用在 21H2 之前的 Windows 10 上可以再次启动（#76）。

## 1.0.0（2026-11-08）

TKCode.Arc 的首个正式版：以原生 Avalonia 12 控件呈现 Arc 的视觉语言和弹簧动效，并提供页面级区块、数据表格、工作流画布、停靠布局、终端、PDF 查看器和音频播放。目标框架为 `net8.0`、`net10.0` 和 `net11.0`，基于 Avalonia 12.1.4（`TKCode.Arc.Terminal` 为 `net10.0` 和 `net11.0`）。

### 稳定性

从这个版本起，Arc 按语义化版本发布，不再带 `-preview` 后缀：破坏性 API 变更只在新的主版本中出现，并在更新日志中列出。所有包以同一版本一起发布，请让它们保持同一版本。安装时不再需要 `--prerelease`。

### 包

以下 21 个包发布到 NuGet：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Foundation`、`TKCode.Arc.Fonts`、`TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts`、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Dock`、`TKCode.Arc.Terminal`、`TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native`、`TKCode.Arc.Audio.Browser`、`TKCode.Arc.Pdf`、`TKCode.Arc.Pdf.Native`、`TKCode.Arc.Pdf.Browser`、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners`、`TKCode.Arc.Mobile`、`TKCode.Arc.Templates`。

### 从 0.4.0-preview.6 升级

对同样发布过 0.4.0-preview.6 且有 `net10.0` 构建的 11 个包（`TKCode.Arc`、`.Core`、`.Foundation`、`.Blocks`、`.AdvancedCharts`、`.Special`、`.Workflow`、`.Dock`、`.Mobile`、`.Terminal`、`.Audio`）做了 API 对比，没有发现破坏性变更。但有一点可能影响你：许多并非一直显示的部件不再放在控件模板中，而是在首次使用时由代码创建。自定义 `ControlTheme` 或查找这些 `PART_` 名称的代码需要处理部件不存在的情况。请从安装命令中去掉 `--prerelease`。

### 新增

- **Gallery 改为产品官网**：首页不再展示组件和项目，改为介绍 TKCode：做什么、规模数字、可以做什么、做事方式、三条命令上手、运行环境和 AI 支持，背景是实时点阵。其余内容放到两个新页面：`/components`（组件、区块、特效、移动端按标签切换，可筛选）和 `/projects`（StreamMark、Jaunt、Spyglass）；StreamMark 另有 `/streammark`。顶栏改为组件、开源项目、文档，右侧改用图标按钮。顶部赞助横条不变。
- **Gallery 的 Web 版与桌面版**：网站说明自身运行在 WebAssembly 中、性能不代表桌面版，并按访客系统优先列出最新版本的安装包。桌面版新增设置菜单，包含外观、语言和关于（版本号、更新检测、开源协议、源代码和第三方声明），首页改为显示更新状态。
- **Gallery 适配手机**：宽度小于 760 像素时侧边栏改为抽屉，文档页和首页使用手机边距，各区块改为纵向排列。
- **特殊组件 › 趣味**（`TKCode.Arc.Special`，参考 Arc Pro 行为与动效的原创设计）：`TicketTear`，一张活动门票，票根随手沿齿孔一孔一孔撕开，然后掉落。Enter、Space 或 `IsTorn` 会自动撕完。
- **PDF**（新包 `TKCode.Arc.Pdf`、`TKCode.Arc.Pdf.Native` 和 `TKCode.Arc.Pdf.Browser`；issue #51）：`PdfViewer` 从文件、字节、流或地址打开 PDF，侧栏带缩略图和目录，支持带计数和高亮的搜索、文字选择和复制、链接（外部链接只接受 `http`、`https` 和 `mailto`）、密码表单或 `PasswordRequested` 事件，以及加载和失败状态。`arc:Pdf.Source` 在普通 `PageViewer` 中只显示页面。桌面上由 PDFium 渲染（`ArcPdfNative.Use()`，在进程内，Windows、Linux 和 macOS 的 x64 与 arm64），浏览器中由 pdf.js 渲染（`await ArcPdfBrowser.UseAsync()`）。没有后端时文档会失败并给出提示，绝不伪造页面。需要了解的限制：没有嵌入中日韩字体的文档依赖本机字体，不支持触屏拖动选择，纯文本网址只有在 PDFium 下才是链接，浏览器包不含 Liberation 字体。文档和示例提供英文、简体中文和繁体中文。
- **`PageViewer`**（`TKCode.Arc`，媒体；issue #51）：缩放、平移并翻阅图片（`ItemsSource`）或按需渲染的页面（`IPageSource`），可连续或单页显示，带适应模式、工具栏，以及让你在页面上自行绘制和接收指针输入的 `PageLayer`。一个自绘表面只绘制可见页。文档和示例提供英文、简体中文和繁体中文。
- **模板**（新包 `TKCode.Arc.Templates`）：`dotnet new install TKCode.Arc.Templates` 后，用 `dotnet new tkcode-arc` 创建桌面应用，或用 `dotnet new tkcode-arc-xplat` 创建同时面向桌面和浏览器（WebAssembly）的应用。提供四种布局（`--layout blank|sidebar|header|dock`），以及这些选项：用 CommunityToolkit.Mvvm 的 MVVM（`--mvvm toolkit`）、依赖注入（`--di`）、保存偏好设置（`--settings`，默认开启：外观、强调色、窗口位置和布局状态在下次启动时恢复）、本地化（`--i18n`：English、简体中文和繁體中文的 `.xlf` 文件加语言切换；`--ui-language` 选择界面文字的语言）、Headless 测试项目（`--tests`）、Arc 标题栏（`--custom-titlebar`）、`--framework` 与 `--accent`。项模板可添加页面（`tkcode-arc-page`）、窗口（`tkcode-arc-window`）、可等待结果的对话框（`tkcode-arc-dialog`），以及带 Token 类和主题文件的 Arc 风格控件（`tkcode-arc-control`）。另有三个模板从完整的应用开始，由 Arc 的 Block 组成，示例数据放在服务接口后面，带 MVVM 和依赖注入：`tkcode-arc-admin`（登录、仪表盘、带搜索、筛选、分页和详情抽屉的客户表格、团队与角色、账单、设置、命令面板）、`tkcode-arc-ai-chat`（对话列表加 `AiChat`，回复由示例 responder 流式生成，可换成你的模型客户端；对话在两次运行之间保存）和 `tkcode-arc-devtool`（停靠布局：文件浏览、编辑器标签、属性表格、构建输出和问题列表，打开的文件随布局恢复）。此外还有 `tkcode-arc-workflow`（步骤面板、可撤销的流程画布、小地图和属性检查器、模拟运行、JSON 导入导出）、`tkcode-arc-projects`（看板、甘特时间线、周日历、发布检查清单）和 `tkcode-arc-analytics`（KPI 下钻、收入、留存分组、用量预测、服务状态）。`tkcode-arc-lib` 用于开始编写自己的控件库：示例控件及其设计 Token 和主题、`Use<Name>()` 注册、Headless 测试和示例应用，可直接 `dotnet pack`。新增的 **模板** 指南列出了全部内容；安装指南新增“从模板开始”一节。
- **`EnumSelector`**（`TKCode.Arc`；issue #56）：将枚举成员列为 `RadioGroup`，`[Flags]` 枚举则列为 `Checkbox`，`SelectedEnum` 支持双向绑定。成员数据来自 Foundation 为标记 `[EnumMetadata]` 的枚举新增的生成器，不使用反射。
- **`FluidTabControl`、`FluidTabItem`**（`TKCode.Arc`；issue #60）：与滚动联动的标签页。选择标签会平滑滚动到对应区段，滚动时会选中锚线所在区段的标签。
- **高级图表**（`TKCode.Arc.AdvancedCharts`）：`Sunburst`、`SankeyFlow`、`FunnelChart`、`RadarChart`（issue #66：多边形网格、可调填充透明度）、`RealtimeStream` 和 `RaceBarChart`。文档和示例提供英文、简体中文和繁体中文。
- **布局面板**（`TKCode.Arc`，新增 Layout 分组）：`WaterfallPanel`（不同高度的卡片排入等宽的列）、`TilePanel`（开始屏幕式磁贴，可跨行跨列并占据第一个空位）和 `CyclicPanel`（首尾无缝循环的列表）。
- **`TransferBox`**：在两个列表之间移动项目，可逐个、按选择或全部移动，支持全选、搜索，并直接修改绑定的集合。
- **`NumberFilterBox`、`TextFilterBox`**：各表示一个筛选条件。数字框有复选框、运算符和值，可设容差；文本框支持包含、开头是、结尾是、等于、正则表达式和区分大小写。
- **Gallery 社区区域**：首页和 README 显示 QQ 群二维码和群号，群号可以复制。

### 变更

- **全库性能优化**：控件中尚未显示的部分（焦点环、Info 徽章层、选择菜单等）改为首次使用时创建，控件很多的表单构建量大幅减少。主题改用伪类而不是属性选择器，每帧绘制的标签复用已排版的 `TextLayout`，表格保留已排版的单元格文本，`TerminalView` 缓存已渲染的行，动效引擎、内容切换、模态呈现、滚动数字、调色板和令牌查找的内存分配也减少了。这些改动的目标是保持动效和外观不变。
- **图片**：`ArcImageHeader` 从文件头读取图片尺寸，远大于显示区域的图片可以直接解码到该尺寸。
- **`GanttTimeline`**：缩放 60 个任务时，每帧分配从约 1.8 MB 降到约 100 KB，视图外的行、条和箭头不再绘制。
- **Gallery 浏览器版启动**：音频和视频模块只在有播放器的页面加载，中文访客在首帧前就取得字体，API 说明在首次显示时按语言加载，侧边栏在页面首次需要时才构建。
- **Gallery**：更换桌面图标；关于页只在预览构建上显示预览徽章。
- `tkcode-arc-lib` 模板的初始版本从 `0.1.0-preview.1` 改为 `0.1.0`。

### 变更

- 依赖升级为 Avalonia 12.1.5 与 StreamMark 0.1.2。

### 修复

- **`ThemeSwitcher`**（issue #74）：过渡开始前不再闪现新主题。现在从主题开始应用的那一刻起，窗口快照就覆盖整个窗口，四种过渡都已修复。
- **`Tooltip`**：作为提示内容传入的控件会显示控件本身，不再显示类型名。
- **`GanttTimeline`**（#72）：在 560 高的区块中，图表不再遮住工具栏和页脚。两个方向的滚动条与图表同步，今日线位于任务条下方，箭头在一天间隔和折叠阶段上走线正确，悬停时高亮行、任务条和箭头。
- **`SankeyFlow`**：粒子避让的标签列表不再因叠加层单独重绘而不断增长。
- **高级图表标签**：`RaceBarChart` 的标签在超越时淡出而不是重叠，`SankeyFlow` 的粒子避开文字，`RadarChart` 的内环在浅色背景上保持可见，`Sunburst` 图例占比始终显示一位小数。
- **`InstallSnippet`**：修复续延未在调度器上运行时高亮会永久挂起的问题。
- **`InkFluid`**：控制模拟节奏，让 UI 线程可以空闲。
- **模板**（#70）：修复生成的 `admin`、`devtool`、`workflow`、`ai-chat`、`projects`、`sidebar` 和 `header` 应用中的布局问题，如文字被裁剪、表格列不对齐、顶栏按钮被拉伸。
- **Gallery 浏览器版**：macOS 浏览器上的中文输入法可以正常组字，此前字母会落入文本框并中断组字。这项修复用模拟组字验证过，尚未在真实的 macOS 输入法上验证。
- **视频**（在仓库中，尚未发布到 NuGet）：Windows 上没有取帧时（如窗口最小化）播放不再停止，`VideoSurface` 正确读取 Media Foundation 纹理的方向。新增 `Stop`、内嵌文本轨道以及 iOS 和 Android 后端。
- **`TextReveal`、`InViewTitle`**：中文、日文和韩文可以在字符之间换行。之前没有空格的标题在窄栏中会被裁切。
- **`TKCode.Arc.Templates` 0.4.0-preview.6 无法安装**：该包发布时模板文件已移动，但指向它们的 `template.json` 没有更新，`dotnet new install` 报错（`MV012: Source ... does not exist`）。请使用此版本或更高版本。
- **Lucide 图标**：路径以移动命令紧接直线开头的 32 个图标（`UserCog`、`FileCog`、`ServerCog`、`Coins`、`Watch`、`ZapOff` 等）首次显示时抛出 “Invalid double value”：生成器把 `16.53.923` 读成了一个数。现在有测试解析每个图标的路径。

### 已知限制

- `TKCode.Arc.Mobile` 在桌面的手机框架中预览，尚未在 Android 或 iOS 真机上验证。
- `TKCode.Arc.Video`、`.Video.Native` 和 `.Video.Browser` 保留在仓库中，在 Windows 和真机验证完成前不发布到 NuGet。
- `PdfViewer`：未嵌入中日韩字体的文档依赖本机字体，不支持触摸拖动选择，纯文本网址只有在 PDFium 下才是链接，浏览器包不含 Liberation 字体。

## 0.4.0-preview.6（2026-11-08）

新增 8 个定价区块、`dotnet new` 模板包、SettingsGroup 多列布局，以及浏览器原生视频呈现。

### 新增

- **区块 › 定价**（`TKCode.Arc.Blocks`）：`PlanComparison`（移植自 Arc：带月付/年付切换的套餐卡片，价格滚动切换，突出推荐套餐并附功能对比表），以及参考 Arc Pro 行为的 7 个原创设计：`PricingCalculator`（按席位滑块与计费切换定价的套餐卡片，推荐徽标滑向合适的套餐）、`UsagePricing`（拖动席位与流量，套餐名变形、预估金额计数到新总额）、`CartDrawer`（商品飞入购物车、数量滚动，侧边抽屉含步进器、免运费进度条与结账）、`CheckoutSummary`（优惠码变为可移除的标签，折扣与税费行滑入，总额滚动，支付按钮经处理中到已支付）、`LiveCheckout`（订阅结账，摘要随每次修改实时重算并展开为收据）、`SeatManager`（头像行、席位用量条、带按比例计费的步进器、确认步骤，以及可重发或撤销并能撤回的邀请）和 `CreditTopUp`（余额与用量图、预设充值包、自定义金额滑块、自动充值，新余额像里程表一样滚入）。文档与示例提供 English、简体中文和繁體中文。
- **模板**（新包 `TKCode.Arc.Templates`）：`dotnet new install TKCode.Arc.Templates` 后运行 `dotnet new tkcode-arc`，支持 `--layout blank|sidebar`、`--mvvm none|toolkit`（用 CommunityToolkit.Mvvm 的 ViewModel 与编译绑定）、`--settings`（默认开启：外观、强调色、窗口位置和侧边栏的收起状态在下次启动时恢复）、`--framework` 与 `--accent`。项模板可添加页面（`tkcode-arc-page`，可带 ViewModel）、窗口（`tkcode-arc-window`）、可等待结果的对话框（`tkcode-arc-dialog`），以及带 Token 类和主题文件的 Arc 风格控件（`tkcode-arc-control`）。安装指南新增“从模板开始”一节。
- `SettingsGroup` 可按固定或自适应列数排列行（`Columns`、`MinColumnWidth`、`ColumnSpacing`、`RowSpacing`、附加属性 `ColumnSpan`），并向行共享 `LabelWidth`、`ContentAlignment` 与 `ContentSpacing`，行可单独覆盖。默认仍为单列布局。
- `VideoPlayer.BrowserPresentation="Native"` 在浏览器中改用浏览器自带的 video 元素：无需拷贝帧，浏览器能播放的格式都可用（例如 Safari 中的 HLS）。此时控制栏位于画面下方，字幕由浏览器显示。
- `VideoPlayer` 的系统全屏在浏览器中也可用（Fullscreen API）；通过窗口管理器或 Esc 退出全屏时，播放器也会一同退出全屏。

### 修复

- Linux 视频（GStreamer）：事件按发生顺序送达 .NET；事件批次已满时丢弃进度事件，而不是状态、错误或结束事件。
- 视频（原生后端）：在其他线程读取帧时加载或释放，不再释放正在读取的帧；上一个媒体的错误保留其错误信息。

## 0.4.0-preview.5（2026-11-08）

新增 14 个 Special 组件：界面（Surfaces）、音视频与加载器；Gallery 页面打开更快。

### 新增

- **Special 组件 › 界面**（`TKCode.Arc.Special`）：`SheetStack`（逐层升起、带景深的下钻面板，下拉关闭，宽屏上变为叠放对话框）、`WalletStack`（拉丝金属、扭索纹黑、磨砂玻璃与彩色的支付卡，可展开、轻扫切换并抽出查看余额与近期交易）、`ControlCenter`（快捷设置磁贴变形为详情，专注计时拨盘与橡皮筋式音量和字号滑条）、`HoloCard`（随指针透视倾斜、带全息箔片、闪粉与眩光并可翻面的卡片）、`CardToArticle`（故事卡片变形为完整文章，下拉缩回）和 `LinkUnfurl`（把粘贴的网址变成链接、显示加载并展开为预览卡片的输入框）。
- **Special 组件 › 音视频**（`TKCode.Arc.Special`）：`VoiceOrb`（会呼吸、泛起涟漪、旋转与脉动的语音模式球体，附 `VoiceTranscript` 实时字幕与 `VoiceControls` 通话控件）、`NowPlaying`（迷你播放条变形为带波形拖动条的完整播放器，附 `NowPlayingLevels`）和 `VoiceRecorder`（录入实时波形、回放拖动，再以语音气泡发送）。音频为模拟数据，经属性与事件接入，包不依赖任何音频库。
- **Special 组件 › 加载器**（`TKCode.Arc.Special`）：`StretchRefresh`（用鼠标、触摸或滚轮下拉刷新）、`MorphLoader`（四笔在圆点、柱条、圆环与方块间变形，并收成对勾或叉号）、`SkeletonMorph` 与 `MorphBlock`（骨架屏逐块伸展为加载完成的内容）、`BlackHole`（把条目卷入奇点的删除过渡，撤销时旋出复原）和 `ChargeSession`（实时充电卡片，可拖动充电上限并显示渐缓的充电曲线）。
- `TokenTextEditor.TryGetTokenBounds` 返回标记块在另一个视觉元素坐标系中的边界。

### 修复

- Gallery：文档页在首次进入视野时才构建示例，截图在看到时才解码，示例多的页面打开时间约减半；导航时侧栏与顶栏的工作量更少。
- 浏览器中的 Gallery：同时请求两次的标志不再显示为首字母，文档文本与截图每次会话只获取一次。

## 0.4.0-preview.4（2026-11-07）

Lucide 图标改为按需编译进应用：应用只携带自己用到的图标。

### 变更

- **Lucide 图标按应用编译**（`TKCode.Arc.Icons.Lucide`）：包不再携带全部 2155 个图标的路径数据，程序集从 655 KB 降到 67 KB（gzip：130 KB 降到 31 KB），应用每用到一个图标约增加 100 到 300 字节。包中附带的源生成器会在 C#（`LucideIconKind.Bell`）和 XAML（`Kind="Bell"`、`{lucide:LucideGeometry Bell}`、`{x:Static lucide:LucideIconKind.Bell}`、样式 Setter）中找到用到的图标，只把它们编译进项目。这样写的图标无需任何改动。
- **运行时才决定的图标需要声明**（破坏性变更）：来自绑定、数据或运行时加载的 XAML 的图标，需要在项目文件中用 `<LucideIcon Include="Bell;arrow-right" />` 声明（PascalCase 或 Lucide 原名均可），或用 `<LucideIconsIncludeAll>true</LucideIconsIncludeAll>` 编译全部图标。未编译进来的图标不绘制任何内容，并记录一条警告，指出需要添加的那一行；`LucideIconData.Get` 对它返回空字符串。新增 `LucideIconData.IsRegistered` 和 `TryGet`。

### 新增

- **视频**（新包 `TKCode.Arc.Video`、`TKCode.Arc.Video.Native`、`TKCode.Arc.Video.Browser`，仓库内，尚未发布到 NuGet；`UseArcVideo()`）：`VideoPlayer` 在画面上叠加 Arc 风格的控制条播放视频（带缓冲范围、章节和悬停时间的进度条，音量、倍速、字幕菜单、全屏、自动隐藏的控制条、键盘快捷键、读屏值文本），并自己绘制 WebVTT / SRT 字幕；`VideoSurface` 只显示画面。帧在合成树内绘制，所以控制条、圆角和变换都能叠在视频上；macOS 与 Windows 上帧以 GPU 图像传递，不经拷贝。解码使用系统播放器（Media Foundation、AVFoundation、GStreamer；`ArcVideoNative.Use()`），浏览器中使用 HTML video 元素（`ArcVideoBrowser.UseAsync()`）；应用不附带任何解码器。没有后端时播放器显示测试画面。内置文案有 en-US、zh-CN、zh-TW。Gallery 新增“视频”分组。
- **Lucide 图标的构建警告**：项目未声明任何图标时，XAML 中每处绑定 `LucideIcon.Kind`（特性、属性元素或样式 Setter）的位置都会报 `ARCL002`，因为生成器无法得知绑定会显示哪些图标；`ARCL001` 报告 `<LucideIcon Include>` 中不是 Lucide 图标的名称。声明图标或设置 `LucideIconsIncludeAll` 后 `ARCL002` 不再出现；`<NoWarn>ARCL002</NoWarn>` 也可关闭它。

## 0.4.0-preview.3（2026-11-06）

新增用于规划项目的 GanttTimeline、由自有数据生成行的 `Sidebar` `ItemsSource`，并修复标记扩展与 ArcWindow 示例的问题。

### 新增

- **GanttTimeline**（Blocks › Projects）：在日历上排布阶段与任务的项目时间线。拖动条形可移动，拖动两端可调整日期（按整天吸附，支持撤销与重置）；任务早于其依赖任务结束时，依赖箭头变红；阶段可折叠为一条汇总条；`Zoom` 在日、周、月之间切换；今日线标出计划进度。编辑通过 `TasksChanged` 报告。键盘：方向键在条形间移动，Alt+方向键移动任务，Alt+Shift+方向键调整结束日期。条形以弹簧动画落到吸附的日期；减少动效时直接跳转。
- **Sidebar `ItemsSource`**：用自有数据生成行（权限列表、服务端返回的菜单），数据变化时随之更新。`LabelMember`、`KeyMember`、`IconMember`、`BadgeMember`、`IsEnabledMember` 与 `ChildrenMember` 映射字段，`TrailingTemplate` 在行尾绘制额外内容（`DataContext` 为你的对象），你的对象保存在 `SidebarItem.Tag` 中。`ItemsSource` 中的 `SidebarItem` 子类原样使用。

### 修复

- **标记扩展**：生成的令牌与文本扩展（`ArcDesignTokenResource` 及本地化文本扩展）的 `ProvideValue` 返回 `object`，可用于任意属性类型。
- **Gallery**：ArcWindow 示例把标题栏预览裁剪在圆角边框内部，而不是边框本身。

## 0.4.0-preview.2（2026-11-06）

新包 `TKCode.Arc.Dock` 提供停靠布局；新增带 Arc 标题栏的 `ArcWindow`；`ResizablePanels` 支持竖直方向；控件更轻；修复弹层内再开弹层的问题和基础主题的问题（issue #35、#38、#39、#40）。

### 新增

- **停靠布局**（新包 `TKCode.Arc.Dock`，`UseArcDock()`；issue #35）：IDE 式的面板布局。面板（`DockPane`）组成标签组，嵌套在水平或竖直分割里，围绕中央的文档区；分割的分隔条就是 `ResizablePanels`。布局树可以写在 XAML 里（`DockSplit`、`DockGroup`、`DockDocumentArea`），也可以在代码里构建 `DockWorkspace`；模型会自动整理：空组移除，只剩一个子项的分割折叠，同方向的分割合并。面板用 `Move(pane, DockTarget)` 或标签菜单移动（关闭、关闭其他、全部关闭、移动到 › 左/右/上/下新组、停靠到 › 左/右/上/下），移动时保留内容实例；可以从标签、菜单或键盘关闭（`PaneClosing` 可取消）。标签放不下时可滚动，并列在溢出菜单里；文档标签显示关闭按钮和已修改圆点。键盘：方向键、Home、End 在标签间移动，Ctrl/⌘+Shift+方向键调整顺序，Delete 和 Ctrl+F4/⌘+W 关闭，Shift+F10 打开菜单。`SaveLayout` 与 `LoadLayout` 以 JSON 读写布局（`tkcode-arc-dock` 第 1 版，手写读写器，不用反射）。内置文案有 en-US、zh-CN、zh-TW。Gallery 新增“停靠布局”分组。
- **停靠布局：拖放与浮动**：沿标签栏拖动标签可调整顺序（其他标签以弹簧让位），拖离标签栏则开始停靠：幽灵标签跟随指针；指针下方组上的罗盘可合并或分割该组，文档区上的指示环停靠在所有文档旁边，边缘按钮沿整个布局停靠，标签栏里的竖线指出标签的落点，预览框在目标之间滑动。拖动标签栏的空白处可移动整个组，Esc 取消。`ShowDockIndicators="False"` 时不显示罗盘，按指针在组内的位置停靠。面板可以用 `Float`、标签菜单（浮动）或拖到布局外浮动，用 `Dock`（回到原处）、菜单、双击标题栏或把窗口拖到罗盘上停回。`FloatingMode`：`Auto`（经典桌面应用用附属窗口，浏览器等其他宿主用布局内的浮动卡片）、`Window`、`Overlay`、`Disabled`。浮动窗口与 `Workspace.Floats` 双向同步，随保存的布局一起恢复，并会被拉回可见屏幕；关闭浮动窗口会关闭其中可关闭的面板，其余面板停回。Wayland 上窗口由系统决定位置，通过菜单停回。新增 `FloatActiveCommand`。
- **停靠布局：自动隐藏、数据源、面板切换、最大化**：`AutoHide`（标签菜单，Shift+Esc）把工具面板收到离其所在组最近的布局边缘的侧栏；点击侧栏按钮，面板以卡片形式滑出覆盖在内容上（宽度与停靠时相同，最多为布局的 70%，内侧边缘可调整大小），点击外部、按 Esc 或焦点离开时滑回；`Pin`（卡片上的图钉、菜单、`Dock`）把它放回原组。自动隐藏的面板随布局保存，也可以在 XAML 里通过 `DockWorkspace.LeftHidden` / `RightHidden` / `TopHidden` / `BottomHidden` 声明。`DocumentsSource` / `ToolsSource` 配合 `DocumentTemplate` / `ToolTemplate` 为每个数据项生成一个面板（`DataContext` 为数据项；可选 `IDockItem` 提供 id 和标题；`DockPane.IdOf`）：添加数据项即打开面板，移除即关闭，关闭标签时会从可写列表中移除该项，保存的布局按 id 恢复这些面板。Ctrl+Tab 按最近使用顺序打开面板切换器（松开 Ctrl 即切换）。双击标签栏空白处（或菜单中的最大化）让一个组铺满布局，直到还原。新增 `AutoHideActiveCommand`、`ShowPaneSwitcherCommand`、`ToggleMaximizeCommand`。
- **ArcWindow**（Extras）：带 Arc 标题栏的桌面窗口，类似 WPF 的 `WindowChrome`（issue #38）。客户区延伸到窗口装饰里，顶部用主题颜色绘制 `TitleBar`，可通过 `LeftContent`、`CenterContent`（放得下时在整个标题栏上居中）和 `RightContent` 放入自己的控件。拖动标题栏移动窗口，双击最大化或还原，拖动边缘调整大小。在 Windows 上标题栏就是系统标题区（系统菜单、Aero Snap），最大化按钮会打开 Windows 11 的贴靠布局；macOS 上保留红绿灯按钮。`CaptionButtons`（`Auto`、`Drawn`、`System`、`None`）决定由谁绘制最小化、最大化和关闭按钮，标题栏的 `Height` 设置向系统申请的标题栏高度。标题栏按钮的提示已本地化（en-US、zh-CN、zh-TW）。

### 变更

- **ResizablePanels**：新增 `Orientation`（默认 `Horizontal`，或 `Vertical`）、`Gap`（窗格之间的间距，默认 1），并支持运行时增删窗格，留下的窗格保持原有尺寸（其他窗格按比例让出空间）。新增“竖直”示例。停靠布局的分割就是用它实现的。
- **控件**：很少用到的模板部件改为首次需要时才创建：键盘焦点环（Button、Checkbox、Switch、TabItem、Select 等）、Tabs 的滚动按钮和 Slider 的数值气泡。Button 的状态改用伪类。500 个按钮的列表分配内存从 61.7 MB 降到 52.7 MB，常驻从 41.7 MB 降到 35.5 MB。文本框、选择器、滑块和开关的动效与 Token 有小幅调整。
- **Gallery（桌面）**：Gallery 窗口改为 `ArcWindow`，原先自带的标题栏类已移除。

### 修复

- **弹层**：在 `ArcPopup` 对话框（原生弹出窗口）里打开 `Select`、提示或其他弹层，不再抛出 "Unable to create IPopupImpl and no overlay layer is found"，而是以原生弹出窗口打开（issue #40）。
- **基础主题**：应用为某个内置类型（例如 `PopupRoot`）提供的 `ControlTheme` 不再关掉整个基础主题，只跳过应用自己设置了样式的类型（issue #39）。
- **Gallery（WebAssembly）**：切换页面不再让网站卡死；WebAssembly 宿主默认不使用 AOT 编译发布（用 `-p:ArcAot=true` 开启）。

## 0.4.0-preview.1（2026-11-03）

新增编辑对象属性的 PropertyGrid、Table 多行单元格、Select 和 ColorPicker 的尺寸、在运行中的应用里加载包，并修复浏览器中 DataGrid 导出失败的问题（issue #30、#34、#36、#37）。

### 新增

- **PropertyGrid**（Extras）：以“标签 + 编辑器”的行编辑一个或多个对象的属性，类似 CAD 或建模软件的属性面板（issue #30）。给类标注 `[PropertyGridModel]`，Foundation 源生成器会在编译期用带类型的委托注册其属性（不使用反射，兼容 AOT 和裁剪），并读取 `[Category]`、`[DisplayName]`、`[Description]`、`[ReadOnly]`、`[Browsable]` 和新的 `[PropertyGridOptions]`。其他类型用 `PropertyGridTypes.Register<T>` 在代码中注册，Avalonia 对象按其已注册的属性描述。内置编辑器是 Arc 的小尺寸字段（`Input`、`NumberField`、`Switch`、`Select`、`ColorPicker`、`DatePicker`）；`Editors` 可按键、属性名或类型加入自定义编辑器。行按类别分组、可搜索过滤、显示选中行的说明，可展开嵌套值（结构体会写回其所有者）和列表（带添加与删除按钮），跟随 `INotifyPropertyChanged`，并支持同时编辑多个对象（`SelectedObjects`，带混合值）。行是 `PropertyGridItem` 控件，带用于样式的伪类。新增生成器诊断 ARCF030–ARCF032。
- **Table**：多行单元格（issue #37）。`DataTableColumn.TextWrapping`（默认 `NoWrap`，单行加省略号）和 `MaxLines` 让列显示换行符并折行长文本；每行的高度取行内最高的换行单元格，以 52 px（紧凑布局 64 px）为下限。设置了 `TextWrapping="Wrap"` 的 `CellTemplate` 列会把行撑到模板的高度。没有换行列的表格保持统一行高，没有额外开销；有换行列时，只在数据、字体或列宽变化时测量换行文本。新增“多行单元格”示例。
- **Select** 和 **ColorPicker**：`Size`（Select 为 `Small`、`Medium`、`Large`；ColorPicker 为 `Small` 和 `Medium`），可与小尺寸文本框对齐。默认仍为 Medium。
- **Foundation**：`ArcRuntime.UsePackages(arc => ...)` 在运行中的应用里启用包，例如浏览器应用在某个页面首次需要时才下载的程序集中的包（issue #34）。新包的设置与启动时相同：注册其令牌标识、控件主题、语言目录、预设和提交回调，并连同它们的令牌（包括主题作用域）重新发布当前主题。已启用的包会被跳过；冲突或缺少翻译会在任何改动之前抛出异常。

### 变更

- **Gallery（WebAssembly）**：Special 组件（`TKCode.Arc.Special`）改为在首个 Special 页面打开时下载，而不是启动时下载：首页少下载 1.2 MB（brotli），之前为 22.2 MB（issue #34）。Gallery 也不再在启动时运行每个已文档化控件的静态构造函数：API 表只在页面显示时才查找其类型和属性。

### 修复

- **DataGrid**：在浏览器中导出 xlsx 或 CSV 不再得到空文件并提示“导出失败”（issue #36）。浏览器的保存文件流只接受异步写入，因此导出内容先在内存中生成，再用 `WriteAsync` 写入。

## 0.3.2-preview.2（2026-10-30）

菜单支持多级子菜单，TreeView 支持行内容模板，内联选项支持绑定，并修复了 Windows 下的构建问题（issue #28、#29、#31、#32）。

### 新增

- **TreeView**：`ItemTemplate` 用你自己的内容排布每行的标签区，例如名称加类型、状态点或工具提示；行本身、填充、缩进、箭头和图标仍由树绘制，树仍然是虚拟化的（issue #28）。`DisabledSelector` 和 `TreeViewItem.IsDisabled` 显示不能被选中的节点；键盘焦点仍可到达它们，所以禁用的文件夹仍然可以展开。`RowHeight` 设置每一行的高度，例如用于两行内容。
- **菜单**：`DropdownMenu`、`ContextMenu` 和任意 `MenuSurface` 支持多级子菜单（issue #29）。在一行内嵌套 `MenuEntry` 元素（`MenuEntry.Items`，或 `MenuEntry.ItemsSource`），或为 `ItemsSource` 数据设置 `ChildrenSelector`。带子菜单的行会显示箭头，单击、按 ArrowRight、Enter 或 Space，或指针在其上停留 100 ms 后，子菜单会在菜单旁打开；指针移向子菜单途中经过其他行时不会关闭它。ArrowLeft 或 Escape 关闭一级，Tab 关闭全部，在任意一级选择一行都会关闭整个菜单并只引发一次 `ItemClicked`。右侧放不下时子菜单在左侧打开，更深的层级也沿用这个方向。新增成员：`MenuEntry.Items`、`MenuEntry.ItemsSource`、`MenuEntry.HasSubmenu`、`:submenu-open` 伪类，以及 `MenuSurface`、`DropdownMenu` 和 `ContextMenu` 上的 `ChildrenSelector`。
- **Core**：`SubmenuSafeZone`（`TKCode.Arc.Primitives`），指针移向已打开子菜单时的路径区域，由菜单和 `ModelMenu` 共用。

### 变更

- **破坏性变更：内联选项类型支持绑定。** `SegmentedOption`、`RadioOption`、`RadioCardOption`、`BillingToggleOption`、`FilterField`、`FilterOption`、`ReactionOption`、`SliderMark`、`GaugeThreshold`、`ActionSwapState`、`Announcement`、`ChatParticipant` 和 `HoverCardStat` 现在派生自 `AvaloniaObject`，属性改为样式属性。在 XAML 中内联书写的选项可以使用 `{Binding ...}` 和 `{DynamicResource ...}`（例如本地化的标签），显示它们的控件也会跟随之后的变化（issue #32）。构造函数、对象初始化器和属性名不变，但基于旧类型编译的代码需要重新编译，并且这些对象必须在 UI 线程上创建和修改。`FilterChip` 仍是不可变的值类型。
- `BillingToggle.DefaultOptions` 每次读取都返回新的选项，各个切换不再共用同一组选项。
- **菜单**：各级子菜单中的行都会在最外层菜单面板上引发 `MenuSurface.ItemClicked`，因此 `DropdownMenu.ItemClicked` 和 `ContextMenu.ItemClicked` 也会报告嵌套的行。

### 修复

- **Windows 构建**：Windows 下 `src/` 里的项目重新被识别为库，`dotnet pack` 能正常产包，XML 文档（Gallery 的 API 表）也会生成；磁盘上残留的生成代码（`GeneratedFiles/`）不会再被重复编译，每个目标框架写入各自的 `GeneratedFiles/<tfm>` 目录（issue #31）。CI 也增加了 Windows 构建。
- **圆角预设**：Analytics 分段切换、KPI 下钻、指标仪表盘、小倍数图、团队成员、来源引用和引力井的圆角改为取自圆角令牌，`t.Corners(...)` 能像其他控件一样把它们改成直角或按比例缩放。
- **用量预测**：关闭动效时，图表在变化后不再持续运行帧循环。

## 0.3.2-preview.1（2026-10-28）

Arc 不再依赖 AtomUI。每个包现在只依赖 Avalonia 和新的 MIT 包 `TKCode.Arc.Foundation`，整条依赖链都采用宽松许可，闭源应用可以把 Arc 以单文件或 NativeAOT 方式发布（issue #25）。应用的入口和主题 API 有变化；控件、控件属性和 XAML（`{arc:ArcDesignTokenResource ...}`、`{arc:XxxTokenResource ...}`）不变。见下文的*从 0.3.1 迁移到 0.3.2*。

### 新增

- **Foundation**（新包 `TKCode.Arc.Foundation`）：主题管理器（`ArcThemeManager`、`ArcTheme`、`ArcThemeState`、`ArcThemeBuilder`）、主题预设（`ArcThemePreset`）、局部主题（`ArcThemeScope`）、包注册（`AppBuilder.UseArc(...)`、`ArcBuilder`）、语言（`ArcLanguage`、`ArcLanguageManager`），以及生成令牌、主题文件和语言目录的源代码生成器。不使用反射，兼容裁剪与 NativeAOT。控件的主题在该类型的控件第一次使用时才加载。
- **ArcImage** 与 `ImageSource`（Core）：在后台加载图片（http、`avares://`、文件、`data:`、位图），带共享缓存，桌面与浏览器均可用。
- **ArcOverlayHost**（Core）：自带覆盖层的面板，在其中打开的对话框、抽屉和底部面板都限制在它的范围内。
- **基础主题**（Core）：Arc 为其模板用到的 Avalonia 控件提供自己的主题（`ScrollViewer`、`ScrollBar`、`ItemsControl`、`PathIcon`、`TextSelectionHandle` 和弹出层宿主）。应用使用 `FluentTheme` 或 `SimpleTheme` 时，它会让位。
- **滚动条**与 `ScrollArea` 一致：细长，指针悬停在视图上或滚动时显示，静止时隐藏。`ScrollViewer.AllowAutoHide="False"` 可让它们一直显示。
- **圆角主题**：`UseArcCorners()` 与 `t.Corners(style)`；现成的 `ArcCornerPresets.Square` 和 `ArcCornerPresets.Half`。
- **菜单**：较长的菜单在 `MaxDropDownHeight` 内滚动。
- **Gallery**：桌面窗口自己绘制标题栏（徽标、标题、置顶按钮；Windows 与 Linux 上还有窗口按钮）。

### 变更

- **不兼容：入口。** `AppBuilder.UseAtomUI(b => b.UseDesktopControls().UseArc())` 改为 `AppBuilder.UseArc(arc => arc.UseArcControls())`。`Application` 类不再需要 `partial`。
- **不兼容：主题 API。** `ThemeConfigBuilder`、`ThemeAlgorithm`、`IThemeManager.ApplyThemeAsync` 和 `WithArc(...)` 由 `ArcTheme.Apply(t => ...)`、`ArcThemeManager.Apply` / `ApplyAsync` 以及 `UseArc` 中的 `WithTheme(...)` 取代。
- **不兼容：Pixel 与 Corners** 改为主题预设：`t.Pixel()` 与 `t.Corners(style)`。`ArcPixelTheme`、`ArcPixelScope`、`WithArcCorners` 和 `ArcCornerScope` 已移除；预设会跳过应用没有注册的包。
- **不兼容：语言**移到 `TKCode.Arc.Localization`（`ArcLanguage`、`ArcLanguageManager`）；`UseLanguages` 在 `ArcBuilder` 上。
- **不兼容：图片与弹出层。** `AsyncImage` 由 `ArcImage` 取代（`IsLoaded` 改为 `IsImageLoaded`）。`ArcPopup` 派生自 Avalonia 的 `Popup`；`OpenMotion` 与 `CloseMotion` 的类型是 `ArcPopupMotion`，`AdjustPlacementAnchor` 去掉了 `ref flipX, ref flipY` 参数，`IsOpen` 会保持为 true 直到退场动效结束。`ArcPopupFrame` 取代 `BaseMotionActor`，`ArcOverlayHost` 取代 `ScopeAwareOverlayLayerPanel`。
- **不兼容：移动端令牌标识**改为 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch` 和 `MobileToast`，不再与桌面控件同名。
- Arc 不再与 AtomUI 控件共享主题；混用两者的应用请留在 0.3.1-preview.6。
- **性能**：`Button` 在第一次进入加载状态时才创建加载圈；`Select` 的值与字段消息只在需要时才排版文字。

### 修复

- **ToastStack**：在堆栈恢复计时器的过程中关闭某个 Toast，不再打断恢复。
- **DataGrid**：筛选列表的搜索框重新显示（之前它没有模板）。
- **PasswordField**：较长的强度标签不再与强度条重叠（#23）。
- **Gallery**：环境变量事件日志中的文本框重新显示（之前它没有模板）。
- **浮层**：菜单、选择器与气泡卡片重新完整显示阴影；之前浮层宿主把阴影裁掉了，圆角看起来是直角。
- **Gallery**：滑动操作的收件箱示例加上了标题、实时计数与头像，与 Arc 一致。

### 从 0.3.1 迁移到 0.3.2

1. 删除所有 `AtomUI.*` 包引用和 `using AtomUI...;`；Arc 会引入它需要的一切。
2. 把 `UseAtomUI(...)` 换成 `UseArc(...)`，并把 `UseArc()`（控件包）改名为 `UseArcControls()`。其他包的注册方法名不变，移到 `UseArc` 的回调中。
3. 把主题请求换成 `ArcTheme.Apply`。一次更改只写要改的部分，状态的其余部分保持不变。
4. `App` 类可以去掉 `partial` 关键字；不再有代码生成到它里面。
5. 使用 .NET 11 SDK 且 `PublishAot` 时，在应用项目中设置 `<UseOptimizedPublish>false</UseOptimizedPublish>`。

```csharp
// 0.3.1
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseAtomUI(builder =>
          {
              builder.UseDesktopControls();
              builder.UseArc();
              builder.UseArcBlocks();
              builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN]);
          });

// 0.3.2
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseArc(arc => arc
              .UseArcControls()
              .UseArcBlocks()
              .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN])
              .WithTheme(t => t.Appearance(ArcAppearance.System)));
```

```csharp
// 0.3.1
var builder = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Blue).MotionMode(ArcMotionLevel.Reduced));
await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(),
                                               ThemeTransitionReason.UserRequest));

// 0.3.2
ArcTheme.Apply(t => t
    .Appearance(ArcAppearance.Dark)
    .Accent(ArcAccentPreset.Blue)
    .MotionLevel(ArcMotionLevel.Reduced));
```

| 0.3.1（AtomUI） | 0.3.2 |
|---|---|
| `AppBuilder.UseAtomUI(b => ...)`、`UseDesktopControls()` | `AppBuilder.UseArc(arc => ...)` |
| `UseArc()`（控件包） | `UseArcControls()` |
| `IAtomUIBuilder` 上的 `UseArcCore`、`UseArcBlocks`、`UseArcAdvancedCharts`、`UseArcSpecial`、`UseArcWorkflow`、`UseArcMobile`、`UseArcPixelTheme`、`UseArcGallery` | `ArcBuilder` 上的同名方法；新增 `UseArcCorners()` |
| `builder.UseLanguages(...)` | `arc.UseLanguages(...)`，参数相同 |
| `partial class App`、`ApplicationStartup.Attach` | 不再需要 |
| `ThemeConfigBuilder`、`WithAlgorithms(ThemeAlgorithm.Dark)` | `t.Appearance(ArcAppearance.Dark)`（或 `Light`、`System`） |
| `IThemeManager.ApplyThemeAsync(new ThemeRequest(...))` | `ArcTheme.Apply(t => ...)`、`ArcThemeManager.Current.Apply` / `ApplyAsync`（请 await，切勿在 UI 线程上阻塞等待） |
| `WithArc(a => a.Accent(preset or color))` | `t.Accent(preset or color)` |
| `WithArc(a => a.Token(name, value))` | `t.Token(name, value)` |
| `WithArc(a => a.ControlToken(catalog, control, name, value))` | `t.Token(identity, name, value)` |
| `WithArc(a => a.MotionMode(level))` | `t.MotionLevel(level)` |
| `WithArcCorners(style, scope)`、`arc.Corners(style, scope)`、`ArcCornerScope` | `t.Corners(style)`、`ArcCornerPresets` |
| `ArcPixelTheme.CreateRequest(...)`、`WithArcPixel(...)`、`ArcPixelScope` | `t.Pixel()`、`ArcPixelPresets`；用 `t.RemovePreset(ArcPixelPresets.Slot)` 回到 Arc |
| `using AtomUI.Localization;` | `using TKCode.Arc.Localization;` |
| `Application.Current.GetLanguageManager()`、`ILanguageManager` | `ArcLanguageManager.Current` |
| `GetLocalizer().Get(kind)`、`Format(kind, args)` | `XxxLangResources.Get(kind)`、`XxxLangResources.Format(kind, args)` |
| `LanguageChanged`、`ChangeLanguage(tag)` | `Changed`、`Set(tag)`（或 `ArcLanguage.Changed`、`ArcLanguage.Set`） |
| `FormattingCulture`、`CurrentLanguage` | `ArcLanguage.Culture`、`ArcLanguage.Current` |
| 按目录覆盖移动端令牌 | 标识 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch`、`MobileToast` |
| `AtomUI.Controls.ImageSource`、`AsyncImage`、`IsLoaded` | `TKCode.Arc.ImageSource`、`ArcImage`、`IsImageLoaded` |
| 基于 AtomUI `Popup` 的 `ArcPopup`、`BaseMotionActor` | 基于 Avalonia `Popup` 的 `ArcPopup`、`ArcPopupFrame`；`OpenMotion` / `CloseMotion` 的类型是 `ArcPopupMotion` |
| `AdjustPlacementAnchor(placement, ref flipX, ref flipY)` | `AdjustPlacementAnchor(placement)` |
| `ScopeAwareOverlayLayerPanel` | `ArcOverlayHost` |
| AtomUI 为 `ScrollViewer`、`ScrollBar`、`ItemsControl` 和弹出层宿主提供的主题 | Arc 的基础主题；滚动条静止时隐藏（`ScrollViewer.AllowAutoHide="False"` 让它们一直显示） |

- **窗口与覆盖层。** 没有了 AtomUI 的窗口主题，普通的 Avalonia `Window` 只有在模板中含有 `EnableOverlayLayer="True"` 的 `VisualLayerManager` 时才有覆盖层。`ThemeSwitcher` 的过渡效果需要它；没有时会直接切换。
- **AtomUI 控件。** 同时使用 AtomUI 控件的应用，不再能让两者共享同一个主题。在这些界面迁移到 Arc 之前，请留在 0.3.1-preview.6。

## 0.3.1-preview.6 (2026-10-28)

### 新增

- **Cascader**(Extras):按层级逐级选择一个节点,如省、市、区。字段、菜单、滑动高亮和清除按钮与 `Select` 一致;父节点的子级在右侧一列中展开,菜单宽度用弹簧过渡到新的宽度。数据通过委托读取(`ChildrenSelector`、`TextSelector`、`ValueSelector`),不使用反射。`SelectedItem`、`SelectedValue` 与 `SelectedPath` 始终保持一致,数据到达之前写入的值会被保留,数据到达后再解析。选项:`LeafOnly`、`ExpandTrigger`、`ShowFullPath`、`PathSeparator`、`IsClearable`、`IsReadOnly`;完整的键盘支持。

## 0.3.1-preview.5 (2026-10-28)

### 新增

- **Audio**(新包 `TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native` 与 `TKCode.Arc.Audio.Browser`):媒体类 Block 使用的 `IAudioPlayer` 抽象,默认静音模拟;桌面经 miniaudio 播放(`ArcAudioNative.Use()`),WebAssembly 经 HTML audio 元素播放(`ArcAudioBrowser.UseAsync()`)。
- **Blocks**:`MediaPlayer`(封面、播放列表、进度条、上一首与下一首、音量与静音)和 `FileUpload`(拖放区、文件列表、进度、校验)。
- **Special**:新增 Cursor 分组,包含 `DotGrid`、`GravityWell` 和 `InkFluid` 三个指针效果。
- **Gallery**:`/demo` 下的 Northstar 仪表盘演示,复刻 uiarc.dev/demo 的十一个页面和登录页;从首页的"查看演示"或顶栏菜单的"演示"进入。

### 修复

- **ArcContentSwap**:控件被切换出去后再次显示不再抛出异常(在 Webhooks 检查面板里切换 Payload 与 Headers 会导致程序崩溃)。
- **Charts**:平均线旁边的坐标刻度保持隐藏,平均值标签不再与它重叠。
- **Webhooks**:Payload 与 Headers 切换后都从第一行开始显示。

## 0.3.1-preview.4 (2026-10-27)

### 新增

- **Terminal**(新包 `TKCode.Arc.Terminal`,net10.0 与 net11.0):`TerminalView` 控件和 libghostty-vt 的托管封装(VT 解析、屏幕状态、选区、输入编码)。原生库随包放在 `runtimes/<rid>/native`,覆盖 Windows、macOS 和 Linux。

## 0.3.1-preview.3 (2026-10-27)

### 新增

- **Blocks**：Analytics、Developer、People、Finance 与 LineCharts 新增约 25 个区块（API 密钥、Webhook、集成、环境变量、团队与角色、指标探索、MRR 瀑布图、同期群留存、分级统计地图、行情终端等），附 Gallery 页面、示例与测试。
- 新增 **RichTextEditor** 与 **ActionSwap** 控件。
- **行动端**：`Dialog`、`Toast`、`ActionSheet`、`Toolbar`、`PullToRefresh`、`Radio` 与 `IconButton`，键盘避让与 Arc 动效。
- **Gallery**：桌面端支持检查更新并设置 macOS Dock 图标；浏览器端通过 HTTP 加载地图数据。

### 变更

- 重做 **Table**、**Editors**、**Selects** 与 **Toggles**。

### 修复

- **TimePicker**、**FilterMenu**：行的空白区域不再响应指针输入 (#20)；**Combobox**：清除后不再打开列表。
- **Password**：显隐选择与输入状态保持一致 (#21)。
- **Gallery**：修复 Windows 上侧边栏搜索框重复提交输入法内容的问题。

## 0.3.1-preview.2 (2026-10-26)

### 新增

- **圆角主题**：新包 `TKCode.Arc.Themes.Corners`（`WithArcCorners(style, scope)`），只通过令牌覆盖为所有 Arc 控件切换直角或缩放圆角；颜色、边框、阴影和动效保持不变。
- **行动端**：`ListGroup` / `ListItem`、`Sheet`、`TextField` 与 `Searchbar`，大标题 `Navbar` 以及更多 `Button` 变体；Gallery 新增列表分组、导航栈与底部面板的页面和示例。
- **Core**：新增更多设计令牌与 `ArcTokens` 辅助方法，用于绑定圆角半径。

### 变更

- **像素主题**：圆角与主题资源改由共享的圆角表重新生成。
- **Blocks**：`WeekCalendar` 使用新的边线令牌绘制边缘。

### 修复

- **Stepper**：当前步骤的光晕不再被裁切 (#19)。

## 0.3.1-preview.1 (2026-10-26)

### 新增

- **移动端**：新包 `TKCode.Arc.Mobile`（`UseArcMobile()`），首批触摸优先控件：`Button`（按压态、44 × 44 触摸目标）、`Switch`、`Page`（页眉、内容与页脚位于安全区内）、`Navbar` 和 `Tabbar`。只依赖 `TKCode.Arc.Core`，有自己的命名空间。尚未在 Android 或 iOS 真机上验证，并且仍会经由 `TKCode.Arc.Core` 间接引入 `Avalonia.Desktop` 与 `AtomUI.Desktop.Controls`。
- **Gallery › Mobile**：新增 Mobile 区，用手机框预览（iPhone 15、Pixel、小屏手机、平板，竖屏与横屏，状态栏、模拟安全区、软键盘占位）。这是桌面上的模拟，不是真机。
- **像素风主题**：新包 `TKCode.Arc.Themes.Pixel`（`UseArcPixelTheme()`）：直角、硬边阴影、受限调色板，明暗两套；像素字体 Silkscreen 与 VT323（SIL OFL）随 `TKCode.Arc.Fonts` 提供（`WithArcPixelFonts()`）。Gallery 的外观面板新增「风格」切换：Arc 或 Pixel。
- **表格 › TreeTable**：带嵌套行、三态选择、保持层级的排序、懒加载子节点与 treegrid 键盘导航的表格。Gallery 用「New」标记新组件，持续 60 天。
- **Core**：边框宽度与圆角的设计 Token（`EdgeThickness`、`FocusEdgeThickness`、`RadiusStep*`、`EdgeThickness*`），主题可以改写它们；所有默认值与原先的取值一致。

### 变更

- **Gallery**（网页）：WebAssembly 宿主重新改为不做提前编译，因为编译版在启动时崩溃。图标地址带上版本号，浏览器会丢弃旧图标。

### 修复

- **Carousel**：计时已耗尽的幻灯片不再不让出线程就再次推进轮播，而是重新开始一个间隔。

## 0.3.0-preview.5 (2026-10-24)

### 新增

- **工作流画布**：新包 `TKCode.Arc.Workflow`（`UseArcWorkflow()`）：`WorkflowCanvas` 显示步骤与连线组成的图并播放运行过程，支持平移缩放、编辑、连线、撤销重做、复制粘贴、节点模板、小地图，500 个步骤的布局约 2 毫秒。另有 `WorkflowNodeCard` 与 `WorkflowZoomControls`。
- **Blocks › 认证**：`SignIn`、`SignupForm`、`LoginSplit`、`LoginCentered`、`LoginImmersive`、`OtpInput`、`OnboardingIllustration` 和 `MultiStepForm`。
- **Core**：`PanZoomTransform`、`WheelRouter` 和 `LayeredGraphLayout`。

### 变更

- **Gallery**（网页）：站点改为 WebAssembly 提前编译，慢速 CPU 上动画更流畅；首次访问下载量更大。

### 修复

- **内存**：许多控件（`TreeView`、`ChatThread`、`Markdown`、图表、`DataGrid`、AI、Projects 和 Special 区块等）在整个生命周期内都监听其显示的数据，使页面一直留在内存中；现在只在位于可视树时监听。`SiteFooter`、`SemanticZoom`、`AgentStatus` 和 Gallery 页面也不再泄漏。
- **菜单**：关闭的面板不再把遮罩留在窗口上。
- **ChatThread**：表情选择器不再向控制台写调试信息。
- **Accordion** 与 **ExpandableCard**：展开时分隔线开销更低，外观不变。
- **Markdown** 与 AI 回答改用 StreamMark `0.1.0`。

## 0.3.0-preview.4 (2026-10-24)

### 新增

- **特别组件 › AI**：`TKCode.Arc.Special` 中的五个智能体界面组件：`AgentStatus`（实时状态胶囊，可展开查看最近的步骤）、`ToolCall` 与 `ToolCallGroup`（显示输入与输出的工具调用行，可批准和重试）、`AgentPlan`（可编辑的计划草稿，批准后成为实时清单）、`AgentTimeline`（在同一时间轴上回放多智能体运行）和 `ContextWindow`（带压缩阈值的 200 格令牌预算网格）。
- **Blocks › 页眉与页脚**：`SiteHeader`、`SiteFooter`、`CtaSection` 和 `NewsletterSignup`，移植自 Arc 的免费区块。
- **Blocks › AI**：模型竞技场（`ModelArena`，两个模型的盲测：流式回答、投票和排行）和文档提取（`DocumentExtraction`，文档旁的表单随模型阅读逐项填写，每个值都链接到其来源文本）。九个 AI 区块现已全部可用。
- **AnnouncementBar**：`DismissalDuration` 让记住的关闭状态在一段时间后失效。

### 变更

- **Gallery**：语言在顶栏有了独立的菜单，首次启动跟随系统语言并会被记住。首页新增友情链接，文档页加载时显示占位内容。

### 修复

- **Combobox**（#9）：聚焦时不再显示深色的环，只把边框变为 `BorderStrong`，与 Arc 一致。
- **Gallery**（#8、#10）：示例不再切掉行内编辑框和焦点环的左侧。
- **Gallery**（#11）：以 NativeAOT 发布的桌面 Gallery 能重新加载文档页和截图。

## 0.3.0-preview.3 (2026-10-23)

### 修复

- **Select**：选项高亮是直角；现在使用主题的选项圆角（14）。
- **Select**：Full 动效级别下，选中后菜单会在字段下方的文字上淡出。现在按文档约定立即关闭；为此 `ArcPopup` 新增了 `IsExitInstant`。Reduced 级别仍然淡出。
- **Combobox、MorphSelect**：在 macOS 上点击输入框可能因 `NSInvalidArgumentException` 导致应用退出。无障碍实时区域不再接收空文本。

## 0.3.0-preview.2 (2026-10-23)

### 新增

- **特别组件**：新包 `TKCode.Arc.Special`（`UseArcSpecial()`）中的五个 AI 动画讲解：`AgentLoop`、`MixtureOfExperts`、`SpeculativeDecoding`、`PromptCache` 和 `ReasoningTree`，基于共用的 `Explainer` 基类。所有数字都由数据计算得出。
- **Blocks**：五个项目区块（`WeekCalendar`、`AvailabilityPicker`、`ProjectBoard`、`ReleaseReadiness`、`KanbanBoard`），六个展示区块（`ActivityTerrain`、`RevenueGlobe`、`CustomerGalaxy`、`SemanticZoom`、`LayoutMorph`、`ExplodedView`），以及首批 AI 区块（模型选择器、智能体记忆、AI 输入框、AI 聊天、AI 回答，以及侧边面板和提示词工作台）。
- **Core**：`ArcSurface.Elevation`（对话框、抽屉、气泡卡片和菜单的内容知道自己位于抬升的界面上）、`RollingDigitsFalloff.None`、新的弹簧、`ArcContentSwap` 模式、`ArcRing`、`WordDiff`、`WordPacer`、`DecoratedTextBlock`，以及另外 23 个 Lucide 图标。
- **Button**：抬升界面上的幽灵按钮现在有清晰可见的悬停和按下填充。
- **特殊输入**：`TokenTextEditor`，令牌作为整体处理的多行编辑器；`MentionInput` 基于它构建。
- **Gallery**：文档站点提供 `llms.txt`、`llms-full.txt`、每个页面的 Markdown 版本，以及供 AI 助手使用的代理技能。

### 变更

- **破坏性变更，仓库迁移**：仓库现在位于 `AIDotNet/TKCode.Arc`，XAML 命名空间随之改变。请把 `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` 改为 `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`（Lucide 命名空间同样处理）。
- **Sidebar**：`ResizeMode` 现在默认为 `Overlay`；设为 `ResizeMode="Push"` 可恢复之前的行为。
- **性能**：动效调度器每帧只推进每个驱动一次，隐藏的循环不再产生开销，缓存模糊效果和文本排版，ActivityTerrain 与 LayoutMorph 大幅提速。
- **Gallery**：WebAssembly 站点在首帧之前少下载约 1.5 MB。

### 修复

- **Tabs**、**PageHeader**、**Breadcrumb**、**ArcContentSwap**、**DataGrid**：模板不再以 `null` 数据上下文构建。
- **Core**：中途离开窗口的内容切换不再恢复为半透明；锚定弹层的阴影重新使用圆角。
- **Sidebar**：从侧栏轨道展开后，内容不再保留第一帧的宽度。
- **Motion**：从 `Transparent` 渐入或渐出填充时不再闪出灰色（`ArcBrushTransition` 使用预乘 alpha 混合）。
- **TagInput**、**Textarea**、**TimePicker**：去掉了粗重的焦点环；移除了 `FieldFrameToken` 的 `FocusRing*` 值。

完整清单见仓库中的 `CHANGELOG.md`。

## 0.3.0-preview.1 (2026-10-22)

### 新增

- **Core**：`ArcZoom.Scale`，一个可继承的附加属性，供缩放窗口内容的应用使用。锚定弹层（菜单、选择器、工具提示、悬停卡片、气泡卡片）绘制在窗口的覆盖层里，位于被缩放的容器之外，因此原先无法跟随缩放；现在它们会按继承到的比例排版。`ArcPopup.Surface` 返回赋给 `Child` 的那个界面元素（弹层现在把它放进一个缩放元素中，因此 `Child` 本身是那个元素）。
- **ExpandableContent**：把 ExpandableText 的截断、切换按钮和动效用在任意内容上，例如可选择的文本、只读编辑器或渲染后的 Markdown。截断停在内容中第一段可见文本的某一行上（`TextBlock`、`SelectableTextBlock`，或实现了新增 `IExpandableLineSource` 的控件）；其他内容按 `Lines × LineHeight` 截断。`ExpandableText` 与 `ExpandableContent` 共用新增的 `ExpandableClamp` 基类。
- **ExpandableText**、**ExpandableContent**：`IsFadeEnabled` 可以关闭最后一行上的渐隐。

### 变更

- AtomUI 锁定为 6.2.8；0.2.0-preview.1 基于 6.2.7 构建。
- **ExpandableText**：折叠时，文本只排版到截断线之后的一行，展开时才完整排版。600 行的文本折叠时测得约 1ms，而不是约 135ms。带有行内控件的文本仍会完整排版，以便截断线以下的链接可以获得焦点。截断现在停在文本自身的行边界上，而不是 `Lines × LineHeight`。
- **ExpandableText**：`Lines`、`IsExpanded`、`MoreLabel`、`LessLabel`、`LineHeight`、`IsOverflowing` 和 `ExpandedChanged` 现在声明在新增的 `ExpandableClamp` 基类上。通过 `ExpandableText` 使用它们的代码不受影响。

### 修复

- **ExpandableText**：随新的数据上下文到来的 `IsExpanded` 变化（例如在回收的项模板中）不再产生动画，也不再滚动页面。
- **Switch**、**Checkbox**：随新的数据上下文到来的值（例如详情视图被复用于下一项时）会立即显示；滑块不再滑动和拉伸，对勾也不再像用户点击过那样画出。
- **Progress**：`Value` 变化但整数百分比不变时，不再重启弹簧，也不再更新读数和无障碍状态。每秒汇报数百次的下载现在每个百分点只更新一次。
- **SegmentedControl** 以及其他带滑动指示条的切换控件：隐藏的控件不再在窗口的每次布局时放置指示条；控件再次显示时才会放置。
- **GlidingIndicator**：描边画笔在帧之间保留，不再每次渲染都新建。
- TreeView 和 JsonViewer：在下次布局之前对行再次做出更改，而第一次更改的行还在关闭过程中，会在 `RowTop` 中抛出 `IndexOutOfRangeException`。面板现在在行变化后立即刷新行的几何信息。

## 0.2.0-preview.1 (2026-10-21)

### 新增

- **目标框架**：库同时面向 `net8.0`，与 `net10.0`、`net11.0` 并列。net8.0 构建需要 StreamMark `0.1.0-alpha.25`，该版本为其包加入了 net8.0。
- **Markdown**（`TKCode.Arc`）：大纲与查找。提供 `GetOutline`、`ScrollToPoint`、`ScrollToAnchor`、`Find` 和 `GetSelectedText`，并有 `IsDocumentReady`、`MatchCount`、`CurrentMatchIndex`、`HasSelection` 属性，以及 `DocumentReady` 和 `SearchChanged` 事件。宿主可以围绕文档搭建大纲侧栏、查找栏和复制操作。
- **RichTextEditor**（`TKCode.Arc`）：`TrySetMarkdown` 导入 Markdown，并返回 `RichTextImportReport`，列出编辑器无法保留的内容（`RichTextImportIssueKind`：标题层级、代码围栏的信息字符串、波浪线围栏、表格等）。
- **DataGrid**（`TKCode.Arc.AdvancedCharts`，仓库内，尚未发布到 NuGet）：单元格渲染器（徽标、进度、链接、图标文字、复选框、操作）、校验规则（必填、范围、最大长度、模式）、条件格式规则、`CellInvoked` 事件、右键菜单事件、CSV 导出选项，以及筛选与布局状态。
- **Blocks**（`TKCode.Arc.Blocks`，仓库内，尚未发布到 NuGet）：命令面板对话框；带值与日期筛选、详情和操作的搜索结果；侧边栏的折叠与调整大小模式；重做的通知中心。

### 变更

- AtomUI 锁定为 6.2.7；0.1.1-preview.1 基于 6.2.6 构建。AtomUI 依赖随 Arc 版本一同变化。
- DataGrid 的 CSV 导出默认会处理电子表格公式（`NeutralizeCsvFormulas`），超过 `MaxPasteCells`（默认 1,000,000 个单元格）的粘贴会被拒绝。
- 在 .NET 8 和 9 上，DataGrid 的自然文本排序使用内置比较器；在 .NET 10 及更高版本上仍使用 `CompareOptions.NumericOrdering`。

### 修复

- `LiveLoop.Start` 在已经在输出帧时提前返回，因此每次指针移动都启动循环的调用方不再每次重新读取门控条件。

## 0.1.1-preview.1 (2026-10-21)

### 新增

- **组件**：Activity、Content、Editors、Illustrations、Media 和 SpecialInputs 分类，以及 ButtonGroup、SplitButton、Card、Reactions、SwipeActions，还有 Color、Date、DateRange 和 Time 选取器。
- **Core**：`MotionTimeline` 与 `LiveLoop` 动效驱动器、`SharedElement` 基础组件、`ArcBlur` 与 `ArcViewport`。
- **Blocks**（`TKCode.Arc.Blocks`）：AppShell 区块（Sidebar、PageHeader、CommandPalette、SearchResults）以及设置区块（SettingsPage、SecuritySettings、BillingOverview、UsageBilling、CancelFlow、SettingsCommand）。
- **AdvancedCharts**（`TKCode.Arc.AdvancedCharts`）：一个新项目，首先提供 DataGrid 控件。
- **图库**
  - 元件页面即其带实时示例的文档页面；每个示例都是一个 `.axaml` 文件，其“代码”标签页显示该源码。
  - 侧边栏新增文档区，按 `Cmd+K` 或 `Ctrl+K` 可在窗口任何位置聚焦搜索。
  - 从 XML 文档生成的 API 表格，附 zh-CN 与 zh-TW 翻译；外壳文本与文档页面提供 en-US、zh-CN 和 zh-TW 版本。
  - 通过 StreamMark `0.1.0-alpha.23` 实现 Markdown 渲染与代码高亮，这是首个可在浏览器中运行的版本。
  - WebAssembly 宿主（`TKCode.Arc.Gallery.Browser`），以及桌面和浏览器宿主的应用图标。
- **发布**
  - 图库的 WebAssembly 镜像在 GitHub 上构建，以 `ghcr.io/atomuicommunity/tkcode-arc-gallery` 发布，并采用 brotli、预压缩文件和浏览器缓存提供服务。
  - 图库桌面应用的发布构建：Windows 与 Linux（未签名），macOS（已签名并完成公证）。
  - 通过 nuget.org 可信发布，从 CI 打包并发布 NuGet 包。
- **工具**：API 文档翻译流程的脚本，以及图标构建器。
- 简体中文版 README 与赞助者部分。

### 变更

- 各分类的共享辅助代码由 Core 的共享基础组件取代。
- 图库页面从按分类编写的 C# 页面迁移为 Markdown 文档页面与 `.axaml` 示例。
- 为许多控件的公共属性和事件补充了 XML `<summary>` 文本；它们会填充图库的 API 表格。

### 修复

- 动效：等待暂停的循环可能让渲染器空闲且帧请求挂起；现在帧会持续产生。
- HoldToConfirm、ConfirmMorph（长按模式）、Tabs 面板与 Carousel 视口在复用裁剪区域时没有使被裁剪的视觉元素失效，因此长按填充不会移动，且增长中的面板或调整大小后的轮播会保留过期的裁剪区域。
- Countdown：静止的滚轮会被绘制为普通字形，使滴答作响的表盘帧时间减半。
- 当 XAML 将 `Minimum` 设得高于当前 `Maximum` 时（例如 `Minimum="360" Maximum="900"`），Slider 与 ElasticSlider 会因 `Math.Clamp` 抛出异常。
- 切换选中的标签页时，Tabs 不再滚动其所在的页面；标签列表会自行显示选中的标签页。
- 图库浏览器宿主：为 WebAssembly 构建原生 Skia，发布时跳过裁剪。

### 从 0.1.0-preview.1 升级

- 请将所有 TKCode.Arc 包同时更新到相同版本。
- 新增的分类与控件除 `UseArc()` 外无需额外设置。区块与 DataGrid 需要各自的注册（`UseArcBlocks()`、`UseArcAdvancedCharts()`）以及项目引用，详见**安装**。

## 0.1.0-preview.1 (2026-10-20)

首个预览版本。
