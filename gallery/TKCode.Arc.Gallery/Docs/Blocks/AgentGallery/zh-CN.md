---
title: 智能体商店
summary: 可浏览的现成智能体列表：分类胶囊、会标出命中文字的搜索、排序开关和自适应的卡片网格；每张卡片都能打开详情视图，列出能力、所需权限和可以试问的提示，并在其中安装（带进度）或卸载。
source-hash: 499e42b0bc8f
---

<Demo name="Preview" />

## 何时使用

智能体商店是一个提供现成智能体的产品的橱窗。人们按分类浏览，按名称或任务搜索，按热度、日期或评分排序，并扫视一个卡片网格：带底色的标志、名称和作者、一句话说明智能体做什么、评分和安装数、两个标签和一个“Install”按钮。点开卡片会在网格的位置显示这个智能体的页面：更长的介绍、它能做什么、它需要访问什么（敏感的访问用图标和文字标出）、可以试问的提示，以及安装按钮：安装时显示进度，装好后显示已安装，并可以移除。

- **供人挑选的目录**：一个工作区可以添加的十几个到几百个智能体、模板或助手。
- **先看再装**：在任何人按下安装之前，详情视图就说明了智能体能访问什么。
- **放置你自己团队做的智能体**：给它们一个分类，它们就和其他智能体放在一起。
- **从选择开始对话**：详情视图里的提示会触发 `PromptChosen`，你可以用这个智能体打开一个对话。

## 何时不用

- **为对话选一个模型**：请用 `ModelPicker`；商店用来添加东西，不是用来切换的。
- **管理已经安装的内容**：带开关的设置列表比橱窗更合适。
- **成千上万项**：卡片会复用，但没有虚拟化；超过几百项时请分页，或在服务器上搜索。
- **创建或编辑智能体**：那是一个包含说明、工具和测试对话的表单，而不是商店。

## 用法

`AgentGallery` 派生自 Avalonia 的 `TemplatedControl`。没有数据时，它会显示一个虚构的目录：六个分类中的十四个智能体，因此可以直接放到页面上。传入你自己的 `Agents`（`AgentGalleryItem` 列表）和 `Categories`（带键和标签的 `AgentGalleryCategory`），并绑定 `InstalledIds`。

```xml
<arc:AgentGallery Agents="{Binding Catalog}"
                  Categories="{Binding Categories}"
                  InstalledIds="{Binding InstalledAgentIds}" />
```

```csharp
gallery.Agents = new[]
{
    new AgentGalleryItem
    {
        Id = "brief-writer", Name = "Brief Writer", Maker = "Your team", IsVerified = true,
        CategoryKey = "writing", Glyph = ArcIcons.PenLine, Tint = AgentGalleryTint.Series2,
        Pitch = "Writes a project brief from a short chat.",
        Rating = 4.7, RatingCount = 120, Installs = 2_400, AddedOn = new DateOnly(2026, 10, 1),
        Tags = ["Briefs", "Planning"],
        Abilities = ["Ask the questions a brief needs", "Write the brief in your template"],
        Permissions = [new AgentGalleryPermission("Read your project folder")],
        SamplePrompts = ["Start a brief for the spring launch"]
    }
};
```

### 结构

- **页头**：标题、副标题和搜索框。
- **分类胶囊**：“All”加上每个分类一个胶囊；选中胶囊的背景会在胶囊之间滑动。
- **数量和排序**：显示了多少个智能体，以及 Popular、Newest、Top rated 排序开关。
- **卡片网格**：按 248 px 能放下多少列就放多少列（最多四列），等宽；每一行和这一行最高的卡片一样高。
- **卡片**：标志（智能体的图标放在由主题颜色着色的方块上）、名称、作者（已验证的作者带一个勾）、Featured 标记、一句话介绍（最多两行）、两个标签、评分和安装数，以及安装按钮。
- **详情视图**：返回按钮、大标志与名称、作者和一行信息、安装操作、介绍、“What it can do”、“Needs access to”和“Try asking”。

### 浏览和搜索

胶囊、搜索和排序可以组合：搜索在选中的分类里进行，匹配名称、作者、介绍、标签和分类名，不区分大小写；名称和介绍中的命中文字会被标出。没有结果时，网格换成空状态，提供 **Clear search**（选了分类时还有 **Show all categories**）。`SelectedCategoryKey`、`SearchText`、`Sort` 和 `OpenAgentId` 都是双向的，所以视图可以保存并恢复。

<Demo name="Filtered" />

### 安装

卡片上的按钮和详情视图里的按钮都会安装智能体。设置 `InstallAsync` 来调用你的后端：运行期间按钮显示转圈和“Installing”；完成后 id 加入 `InstalledIds`，并触发 `AgentInstalled`。如果它抛出异常，卡片显示 **Retry**，详情视图显示错误信息，并触发 `InstallFailed`。没有处理程序时，商店会模拟一次 1.2 秒的安装。已安装的智能体在详情视图中显示 **Uninstall**，它会调用 `UninstallAsync`（或者直接移除 id）。

<Demo name="Install" />

```csharp
gallery.InstallAsync = async (agent, cancellation) =>
{
    await api.InstallAgentAsync(workspaceId, agent.Id, cancellation);
};
```

### 打开智能体

点击卡片、按 Enter 或空格会打开它的详情视图；`Open("agent-id")` 在代码中做同样的事，`AgentOpened` 会报告它。返回按钮、Escape 或 `CloseDetail()` 回到网格，并把焦点放回这张卡片。打开智能体时在搜索框里输入，也会回到网格。

### 窄容器

商店按自身的宽度变化。宽度小于 600 px 时，搜索框移到标题下方，内边距收紧，网格只保留放得下的列数。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（`AgentGalleryLabels`）；需要格式化的文字是函数，所以词序可以改变。控件上的 `Title` 和 `Subtitle` 优先于文案对象；把 `Subtitle` 设为空字符串会隐藏这一行。

### 内容建议

- **一句话介绍**：一句以动词开头的话，说明智能体做什么（“Sorts new mail into folders”）。
- **能力**：三到五个短句，每一句都是人们可以提出的一件事。
- **权限**：写出数据和动作（“Read your mail”），在说明里写原因，并把任何会发送、修改或删除内容的权限标为敏感。
- **标志**：选择一个说明任务的图标和一个主题颜色；商店从不显示品牌颜色。

## API 参考

<!-- api: AgentGallery -->

<!-- api: AgentGalleryItem -->

<!-- api: AgentGalleryCategory -->

<!-- api: AgentGalleryPermission -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `AgentOpened` | 打开一个智能体的详情视图 | `Agent` |
| `AgentInstalled` | 安装完成 | `Agent` |
| `AgentUninstalled` | 智能体被移除 | `Agent` |
| `InstallFailed` | `InstallAsync` 抛出异常 | `Agent`、`Message` |
| `PromptChosen` | 按下“Try asking”中的一个提示 | `Agent`、`Prompt` |

## 无障碍

- 商店是一个区域；标题和智能体名称是标题，详情中的各部分是低一级的标题。
- 每张卡片都是一个按钮，名称包含智能体、作者和安装状态（“Inbox Sorter by Tidewater Labs, Installed”），一句话介绍作为帮助文字。安装按钮的名称是“Install Inbox Sorter”。
- 只有一张卡片是 Tab 停靠点，后面跟着它的安装按钮；方向键在卡片之间移动，所以页面不会每张卡片都占一次 Tab。
- 一个礼貌的实时区域会播报筛选或搜索显示了多少个智能体，以及安装何时开始、完成或失败。
- 敏感权限带有警告图标和“Sensitive”一词；安装失败有图标和一句话。任何状态都不只依靠颜色。
- 在详情视图中安装后，焦点移到 Uninstall；移除后再回到 Install。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 搜索框、选中的分类胶囊、排序开关、当前卡片、它的安装按钮 |
| 搜索框中的 `ArrowDown` | 移到当前卡片 |
| 卡片上的 `ArrowLeft` / `ArrowRight` | 上一张或下一张卡片 |
| 卡片上的 `ArrowUp` / `ArrowDown` | 上方或下方的卡片 |
| 卡片上的 `Home` / `End` | 第一张或最后一张卡片 |
| 卡片上的 `Enter` 或空格 | 打开它的详情视图 |
| 详情视图中的 `Escape` | 回到网格，焦点在这张卡片上 |
| 搜索框中的 `Escape` | 清空搜索 |

## 动效

- 筛选、搜索或排序之后，卡片在 160 ms 内从 25% 的不透明度淡入（只有透明度，没有位移）。
- 详情视图在 240 ms 内淡入并上升 8 px；离开时网格立即出现。
- 悬停卡片时，背景着色、边框加深，用时 160 ms；选中的分类沿弹簧滑动。
- 安装按钮安静地切换文字，安装期间显示转圈。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 筛选后的卡片 | 160 ms 内从 25% 淡入 | 同样的淡入 | 立即 |
| 打开详情视图 | 淡入并上升 8 px | 只淡入，120 ms | 立即 |
| 悬停卡片 | 160 ms 着色 | 立即 | 立即 |
| 分类胶囊 | 沿弹簧滑动 | 跳到位 | 立即 |

## 响应式

- 商店按自身的宽度变化。列数：按 248 px 加 12 px 间距能放下多少列就放多少列，最多四列。
- 宽度小于 600 px（`:compact`）时，搜索框移到标题下方并拉伸，两侧内边距从 20 降到 12。
- 分类胶囊放不下时在自己的栏里横向滚动；排序开关也会在自身内部滚动。
- 在详情视图中，安装操作换行到名称下方，文字列最宽 720 px。
- 外框最宽 1120 px 并居中；给它页面的宽度，让它向下增长。

## 相关组件

- `ModelPicker`：选择智能体使用的模型。
- `AgentMemory`：已安装的助手记住了什么。
- `PromptStudio`：编写并测试提示。

| 组件 | 何时选择 |
|---|---|
| `ModelPicker` | 人们在几个模型之间切换 |
| `SearchResults` | 结果列表是文档，而不是要安装的东西 |
| `PlanComparison` | 人们并排比较几个套餐 |
