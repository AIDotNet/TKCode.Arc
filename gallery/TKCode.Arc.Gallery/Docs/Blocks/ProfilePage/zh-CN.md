---
title: Profile page
summary: 一个人的个人资料页：封面与头像、带统计数据的简介栏、Activity 与 Projects 标签页，以及保存后字段会亮一下的编辑面板。
source-hash: b37b9dc41572
---

<Demo name="Preview" />

## 何时使用

`ProfilePage` 是用户打开自己或他人的个人资料时看到的页面。顶部是一张宽幅封面，圆形头像压在封面的下边缘；姓名、用户名和一句话介绍紧随其后，右侧是 "Share" 和 "Edit profile" 按钮。下方是一个窄的简介栏，包含个人简介、所在地、网站、加入时间以及最多三个数字，其余空间由两个标签页占据：Activity 是按分组列出此人近期动态的列表，Projects 是访客可以加星标的卡片网格。

"Edit profile" 会让一个编辑面板在区块内从右侧滑入。它可以编辑姓名、一句话介绍、所在地、网站和个人简介，并对它们进行检查；保存时，新值会写回页面，每个发生变化的字段会亮一下，让读者看到哪里变了。

- **产品中成员自己的个人资料。** 用户看到的就是别人看到的内容，并且可以就地修改自己的资料，无需单独的设置页。
- **团队成员的页面。** 关闭编辑（`IsEditable="False"`），同样的布局就成为一个关于同事的只读页面，展示其近期工作和项目。
- **创作者或作品集页面。** 项目卡片带有封面、协作者和星标数，足以作为公开展示页。

## 何时不使用

- **账户设置。** 密码、安全、通知和账单应放在 `SettingsPage`、`SecuritySettings` 和 `BillingOverview` 中。本区块只编辑公开信息。
- **同时展示很多人。** 浏览或搜索团队请使用团队通讯录区块或表格；本区块只针对一个人。
- **营销用的团队介绍区。** 介绍团队的落地页需要在网格中放置肖像和简短介绍，而不是标签页和编辑面板。
- **很长的动态流。** Activity 标签页只是简短的摘要（每组只有寥寥几行）。无尽的历史记录请使用时间线或分页列表。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。给它一个 `ProfileDetails`，再填充三个集合；头像和封面由你提供（没有头像时区块显示姓名首字母，没有封面时显示柔和的表面色或你设置的 `CoverBrush`）。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ProfilePage ProfileUrl="https://fieldnote.app/@rowan">
        <arc:ProfilePage.Profile>
            <arc:ProfileDetails Name="Rowan Ellery"
                                Handle="rowan"
                                Headline="Product designer at Fieldnote"
                                Location="Bergen, Norway"
                                Website="rowanellery.studio"
                                JoinedText="June 2020"
                                Bio="I design tools for field researchers and care about honest defaults and quiet motion." />
        </arc:ProfilePage.Profile>
        <arc:ProfilePage.Stats>
            <arc:ProfileStat Label="Followers" Value="3107" />
            <arc:ProfileStat Label="Following" Value="212" />
            <arc:ProfileStat Label="Shipped" Value="41" />
        </arc:ProfilePage.Stats>
        <arc:ProfilePage.Activity>
            <arc:ProfileActivity Kind="Shipped" Action="shipped" Target="Color tokens v3" TimeText="3h"
                                 Group="This week" Detail="New contrast pairs for charts and a calmer dark palette." />
            <arc:ProfileActivity Kind="Reviewed" Action="approved" Target="Compact table mode" TimeText="Wed" Group="This week" />
        </arc:ProfilePage.Activity>
    </arc:ProfilePage>
</UserControl>
```

### 结构

- **封面**：宽页面上高 240（宽度低于 1000 时为 200，低于 640 时为 128）。`ProfileDetails.Cover` 绘制在 `ProfileDetails.CoverBrush` 之上。
- **身份区**：104 的头像，带 4px 的表面色描边，一半压在封面上；右侧是 "Share" 和 "Edit profile"；下方是姓名（28px 展示字体）以及 "@handle · headline"。
- **简介**：个人简介为一个段落，然后是所在地、网站（点击后在浏览器中打开的链接）和 "Joined …"，值为空的行会被隐藏；其下用一条细线隔开，是三等分列的统计数据。
- **标签页**：Activity 和 Projects 及其数量；2px 的下划线标出当前选中的标签页。宽度低于 760 时，简介块移到标签页上方。
- **编辑面板**：宽 420，位于区块右侧，下面有遮罩；无论页面滚动到哪里，它都覆盖区块的可见部分。宽度低于 420 时它占满整个区块。

### Activity

每个 `ProfileActivity` 读作 "{名} {Action} **{Target}**"，可带一行可选的 `Detail`，末尾是 `TimeText`。名取自 `ProfileDetails.Name` 的第一个词，所以改名后各行会随之变化。各行按 `Group` 标题分组，分组顺序按各组首次出现的先后。`Kind` 决定图标：`Shipped` 为绿色火箭，`Starred` 为琥珀色星标，`Commented`、`Created` 和 `Reviewed` 为柔和色图标。

### Projects 与星标

每个 `ProfileProject` 是一张卡片，包含 96px 的封面、标题和 `Visibility`（Public 或 Private）、描述、以重叠头像形式显示的 `Collaborators`、`UpdatedText` 以及一个星标按钮。点按星标会切换 `IsStarred`，并在条目本身上将 `StarCount` 加一或减一（两者都是双向的），然后引发 `StarChanged`，携带该项目、新状态和数量，以便你的代码通知服务器。卡片分两列排布，宽度低于 560 时为一列。

### 控制标签页

`Tab` 是双向的。可以绑定它，把标签页保存在你自己的状态里（例如地址中），也可以通过其他按钮设置它；无论变化来自点击标签页、方向键还是绑定，`TabChanged` 都会报告每一次变化，并带上旧标签页和新标签页。

<Demo name="ControlledTab" />

### 保存

`SaveHandler` 接收编辑后的 `ProfileDetails`（已去除首尾空白，用户名、图片和加入时间沿用原值）并返回一个任务。任务运行期间，"Save" 按钮显示转圈和 "Saving"；字段、"Cancel" 和关闭按钮仍可使用。完成后，`Profile` 会被新对象替换，面板关闭，每个去除空白后的值发生变化的字段会亮一下，`Saved` 会报告之前的资料、新的资料和 `ChangedFields`。没有处理程序时，区块会模拟一次保存，在 `SimulatedSaveDelay`（700ms）之后成功。

在处理程序中抛出异常可以让面板保持打开：异常的消息会显示在面板按钮上方的红色提示条中，会被播报，并引发 `SaveFailed`。草稿保持原样，因此用户可以再试一次。

```csharp
var http = new HttpClient();
page.SaveHandler = async profile =>
{
    var json = JsonSerializer.Serialize(new { profile.Name, profile.Headline, profile.Location, profile.Website, profile.Bio });
    var body = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await http.PutAsync("https://api.example.com/me", body);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("We could not save your profile. Try again in a moment.");
    }
};
```

<Demo name="SaveError" />

保存进行中关闭面板并不会取消保存：完成时值仍会写回，失败则会被播报并通过 `SaveFailed` 报告。

### 面板中的规则

- **Name** 为必填：必须包含空格以外的内容。
- **Website** 为选填。填写时，它必须像一个网址：可选的小写 `http://` 或 `https://`，至少两段以点分隔、由字母、数字、`-` 或 `_` 组成的部分（不含端口），以及可选的以 `/` 开头且不含空格的路径。`example.com`、`www.example.com/work` 和 `https://example.com/p?q=1` 可以通过；`example`、`localhost:3000` 和 `ftp://example.com` 不行。
- **Bio** 最多 `BioMaxLength` 个字符（200）；标签旁的计数器显示还剩多少。

错误在第一次尝试保存之前保持隐藏，之后随每次按键更新。草稿与资料不同之前，"Save" 保持禁用。

### 分享

"Share" 会复制 `ProfileUrl`（未设置时复制 "https://example.com/@{handle}"），按钮会在 `ShareFeedbackDuration`（1800ms）内显示 "Copied" 或 "Failed"，且宽度不变。`CopyHandler` 可以取代系统剪贴板，例如用于添加统计跟踪或测试失败的情形；返回 false 或抛出异常会显示 "Failed"。`Shared` 报告结果。

### 只读个人资料

设置 `IsEditable="False"` 后，"Edit profile" 按钮消失，`OpenEditor` 不执行任何操作；其余功能，包括 "Share" 和星标，仍然可用。

<Demo name="ReadOnly" />

### 窄容器与高度

布局跟随区块自身的宽度，而不是窗口的宽度：封面在低于 1000 时缩小，各列在低于 760 时堆叠，头部在低于 640 时收紧（128 的封面、84 的头像和 22px 的姓名），卡片在低于 560 时变为一列，面板中的 Location 和 Website 字段在低于 420 时堆叠。区块高度为 `PageHeight`（760），父级提供的空间更小时则更矮，并在内部滚动。`IsFramed="False"` 会去掉边框和圆角，适合整个窗口的页面。

<Demo name="Narrow" />

### 文本

区块显示或播报的每个词都在 `Labels` 中，它是一个带英文默认值的 `ProfilePageLabels` 对象。替换整个对象即可翻译；包含数值的文本（简介计数器、"Joined …"、星标按钮的名称）是函数，因此语序可以改变。

## API 参考

<!-- api: ProfilePage -->

<!-- api: ProfileDetails -->

<!-- api: ProfileStat -->

<!-- api: ProfileActivity -->

<!-- api: ProfileProject -->

<!-- api: ProfileCollaborator -->

<!-- api: ProfilePageLabels -->

## 无障碍

键盘：

- **Tab** 依次移动到 "Share"、"Edit profile"、网站链接、选中的标签页和 Projects 标签页中的星标按钮。
- 在标签页上按 **Left / Right** 会切换到另一个标签页并立即显示，首尾循环。
- **Enter / Space** 按下获得焦点的按钮或星标。
- 在面板中，焦点从 Name 开始，**Tab / Shift+Tab** 只在面板内移动。在单行字段中按 **Enter** 会保存；在个人简介中则换行。**Escape** 不保存并关闭面板，焦点返回 "Edit profile"。

屏幕阅读器：

- 简介栏、标签页栏和标签页行都有名称（"About"、"Profile content"、"Profile sections"）。标签页是标签页项；只有选中的那个是 Tab 停靠点。图标以及用户名与一句话介绍之间的圆点对屏幕阅读器隐藏。
- "Share" 按钮是礼貌的实时区域：它的新名称（"Copied"、"Failed"）会被朗读，结果也会被播报。
- 每个星标按钮以项目及其数量命名（"Star Signup study, 34 stars"，加星标后为 "Unstar …"）。
- 面板以其标题命名。每个字段以其标签命名；错误作为字段的帮助文本给出。保存失败显示在强制播报的实时区域并被播报；保存成功则播报 "Profile saved"。
- 键盘焦点环为 2px，位于元素外侧；由指针移动的焦点不显示焦点环。

## 动效

- **标签页**：下划线沿临界阻尼弹簧（Smooth，约 0.9 秒）滑向选中的标签页，位置和宽度一起变化。旧内容在 120ms 内淡出，然后新内容淡入并在 240ms 内上升 6px；新内容就位后，面板高度沿同一弹簧跟随变化。中途切回时，旧内容会重新淡入。
- **编辑面板**：从其完整宽度处沿 Smooth 弹簧滑入，同时遮罩在 240ms 内淡入；离开时使用更快的临界阻尼弹簧（约 250ms）。
- **已保存的字段**：每个发生变化的字段会得到一层向外延伸 3px 的强调色浅底，保持 480ms，再在 1120ms 内淡出。
- **星标**：加星标时，星标从 60% 开始在快速弹簧上弹出；数量立即变化。取消星标没有动画。
- **标签文字**："Share" 和 "Save" 以短促的淡入淡出、2px 模糊和 3px 上升切换标签，宽度不变。
- **减少动效**：下划线和内容立即切换，面板改为淡入（160ms）和淡出而不是滑动，标签文字立即切换，星标不弹出。保存时的亮起效果保留（没有任何东西移动）。**关闭**则立即应用每一次变化，并跳过亮起效果。

## 相关组件

- `SettingsPage` 和 `SecuritySettings`，用于账户设置。
- `Avatar` 和 `AvatarGroup`，用于其他位置的头像。
- `Tabs`，用于本区块之外的标签页。
- `Drawer`，用于窗口级的侧边面板。
