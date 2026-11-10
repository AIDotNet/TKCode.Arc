---
title: 常见问题区块
summary: 以手风琴、主题导航或可搜索并高亮匹配项的列表呈现的常见问题。
source-hash: 40b5de4e9ee5
---

<Demo name="Preview" />

## 何时使用

在大家开口之前就回答那些反复出现的问题。`FaqSection` 接受一组带纯文本答案的问题，并按数量选用合适的布局。

- **定价页或产品页上的短列表**：`Accordion`，一个居中的单栏。
- **能按主题归类的问题**：`Columns`，一列带数量的主题，旁边是所选主题的问题。
- **大家会去搜索的长列表**：`Search`，一个随输入过滤的搜索框，并高亮找到的内容，即使它在未展开的答案里。

## 何时不用

- **不是问题列表的折叠内容**：请使用 `Accordion`。
- **答案需要代码、图片或很长的结构**：请使用文档页面。
- **上千个问题**：搜索在每次按键时在内存中运行，适合几十个问题。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:FaqSection Variant="Search" Title="Questions about billing">
        <arc:FaqSection.Items>
            <arc:FaqItem Question="Can I cancel at any time?" Answer="Yes. Your plan runs until the end of the period." />
            <arc:FaqItem Question="Do you offer refunds?" Answer="Annual plans can be refunded within 30 days." />
        </arc:FaqSection.Items>
    </arc:FaqSection>
</UserControl>
```

`FaqSection` 派生自 `TemplatedControl`。`Items` 为空时显示示例问题，上线前请替换。`Answer` 是纯文本，搜索会在其中匹配和高亮。

### 布局

| `Variant` | 显示内容 | 适合 |
|---|---|---|
| `Accordion` | 标题、问题和联系行排成一个居中的单栏，最宽 720 | 大约十个以内的问题 |
| `Columns` | 标题、主题和联系行，旁边是所选主题的问题 | 带 `Category` 的问题 |
| `Search` | 搜索框和实时计数，下方是一个过滤后的列表 | 长列表 |

### 展开的问题

`OpenItems` 保存展开问题的 id，并且是双向的。默认展开第一个问题，展开另一个时它会收起。设置 `IsMultiple` 可同时展开多个。id 是 `FaqItem.Id`，或者问题的文本。如果问题可能改写，请提供稳定的 `Id`，这样状态不会因修改而丢失。

```csharp
faq.OpenItems = ["refunds", "seats"];
```

<Demo name="OwnQuestions" />

### 主题与搜索

`SelectedCategory` 是 `Columns` 中选中的主题，`Query` 是 `Search` 中的搜索文本，两者都是双向的。没有 `Category` 的问题归入 “General”。

在 `Search` 中，问题的文本或答案匹配时就会留在列表里。只在答案中匹配的未展开问题会显示一小段摘录，让人看到它出现的原因。匹配时忽略大小写和重音：“cafe” 能找到 “Café”。没有匹配项时，列表的位置显示一行说明和一个清除搜索的按钮。`TotalFormat`、`ResultsFormat`、`OneResultFormat`、`NoResultsFormat` 和各标签属性用于翻译这些文字。

<Demo name="Searchable" />

### 联系行

列表下方的这一行提供联系真人的途径。`Contact` 接受 `FaqContact`，包含 `Label`、`Description`，以及 `Href` 或 `Command`。按下时引发 `ContactInvoked`，执行命令，然后打开地址，除非处理程序已将事件标记为已处理。将 `IsContactVisible` 设为 false 可隐藏这一行。

### 最佳实践

**建议**

- 按大家提问的方式写问题，并在第一句话里作答。
- 让答案保持纯文本，这样搜索才能读取。
- 只有每个主题至少有两个问题时才使用 `Columns`。

**避免**

- 把重要内容只放在未展开的答案里。
- 超过大约五个主题。主题栏用于定位，而不是浏览。

## API 参考

<!-- api: FaqSection -->

<!-- api: FaqItem -->

<!-- api: FaqContact -->

<!-- api: FaqContactEventArgs -->

## 无障碍

- 每个问题都是以其文本命名的按钮，帮助文本说明它已展开还是已收起。
- 搜索框有真实的标签，计数行在变化时会被礼貌地播报。
- 被过滤掉的行在不显示时离开 Tab 顺序。

### 键盘

| 按键 | 操作 |
|---|---|
| `Up` / `Down` | 在同一列表的问题之间移动，到两端时循环 |
| `Home` / `End` | 第一个或最后一个问题 |
| `Enter` / `Space` | 展开或收起当前答案 |
| `Tab` / `Shift+Tab` | 在问题、主题、搜索框和联系行之间移动 |

## 动效

- 答案以高度弹簧（0.4 秒，无回弹）展开，文字在 160 ms 内从 2 px 模糊中下移 6 px。收起用时 120 ms。加号以 Arc 的 CSS 弹簧在 580 ms 内转动 45 度。
- 切换主题时，高亮以 morph 弹簧移动，问题沿移动方向切换：进入 16 px，离开 12 px。
- 被过滤的行以同样的高度弹簧收起，其余的行随之上移。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 答案展开 | 高度弹簧、位移和模糊 | 高度立即变化，无位移 | 立即 |
| 切换主题 | 滑动和模糊 | 立即切换 | 立即切换 |
| 搜索过滤 | 行收起 | 行立即关闭 | 行立即关闭 |

## 响应式

- `FaqSection` 跟随自身宽度，而不是窗口宽度。宽度达到 720 px 时，标题使用更大的字号。
- 宽度不超过 760 px（`:narrow`）时，主题栏移到问题上方，主题排成一行并可横向滚动。
- 宽度不超过 480 px（`:compact`）时，内边距收紧，答案使用全部宽度。
- 触控：问题至少 64 px 高，悬停只改变颜色，因此没有内容依赖悬停。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `FaqSection` | 一组问题和纯文本答案 |
| `Accordion` | 不是问题列表的折叠内容 |
| `SearchField` | 搜索常见问题以外的内容 |
| `ContactSection` | 下一步是写信给你 |
