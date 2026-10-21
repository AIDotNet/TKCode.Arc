---
title: 复制按钮
summary: 复制一个值，并立即给出确认反馈。
source-hash: 9aa09db6259f
---

<Demo name="Outline" />

## 何时使用

`CopyButton` 把一个值复制到剪贴板，并在原位确认复制结果：图标变为对勾，标签变为“已复制”，片刻后按钮恢复静止状态。它紧挨着所复制的值，因此人不必选中文字、找到正确的字符，也不必离开正在操作的行。它本身是一个 `Button`，可以用键盘操作，也能放进工具栏以及任何接受按钮的布局中。

- **只显示一次的密钥**：开发者控制台页面上的 API 密钥或 Webhook 签名密钥。人可以把完整的值复制到密码管理器或配置文件中，旁边的掩码文字无需再选中。
- **引导页中的安装命令与代码片段**：文档页或设置向导中的 `npx arc add copy-button` 这类命令。`Plain` 变体让命令保持易读，同时仍提供复制操作。
- **账单或报告中的可分享链接**：账单页面上的付款链接或只读报告 URL，复制后可以粘贴到发给客户的邮件中。
- **密集表格中的标识符**：列表中的订单号、工作区 ID 或物流单号。只显示图标的形式可以为每一行增加一个复制操作，而不会加宽表格。
- **必须精确的值**：连接字符串或 SKU。手动输入，或从换行的文字中选取，都容易出错。

## 何时不用

- **需要以多种格式复制**，例如复制链接、以 Markdown 复制或复制嵌入代码：用 `SplitButton`，让主复制操作仍然只需按一次，其他选项放在它的菜单中。
- **完整的代码示例**，且自带复制控件：用 `CodeBlock`，它的标题栏中已经有一个复制控件。
- **人可能需要编辑或完整阅读的只读字段**：用 `Input` 并设置 `IsReadOnly="True"`，它能让值保持可选中。当一次按下就应复制整个值时，在该字段旁边放一个 `CopyButton`。
- **发送或执行而非复制的操作**，例如“发送邀请”或“轮换密钥”：用 `Button` 或 `ActionButton`，它们的等待与成功状态能描述工作进度。
- **行操作列表，而复制只是众多操作之一**：用 Menus 分类中的 `DropdownMenu`，让复制操作与编辑、重命名、删除放在一起，而不是每一行都占用一个按钮。

## 用法

把 `Value` 设为要写入剪贴板的文本。`Label` 是可见文字，同时也是无障碍名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CopyButton Value="npx arc add copy-button" />
</UserControl>
```

`CopyButton` 派生自 Avalonia 的 `Button`。它保留 `Command`、`Click`、`IsEnabled` 及其他按钮行为，并在点击处理中加入复制：点击时读取值，并等待剪贴板写入完成，因此复制进行期间窗口仍能继续运行。

### 结构

1. **外框**（`Variant`、`Background`、`BorderBrush`）：1 px 边框，使用表面颜色与控件圆角，最小高度为小尺寸控件的高度（36 px）。悬停时外框变为柔和的表面色，边框变为更醒目的颜色。
2. **图标**：16 px 的复制图形。复制后变为成功色的对勾；失败后变为危险色的警告图标。
3. **标签**（`Label`、`CopiedLabel`、`FailedLabel`）：文字。标签单元会预留三个词中最宽的宽度，因此文字变化时按钮宽度保持不变。图标与标签之间的间距为 8 px，两侧的内边距各为 12 px。
4. **焦点环**：2 px 的焦点色环，绘制在外框外侧 4 px 处。只在键盘获得焦点时出现。

### 变体与尺寸

`Variant` 决定外框。`Outline`（默认）绘制表面与边框，让按钮看起来像一个操作。`Plain` 去掉外框，使用次要文字颜色，适合一行代码、密集表格或说明文字，因为带外框的按钮会与内容争抢注意力。

<Demo name="Plain" />

```xml
<arc:CopyButton Value="https://uiarc.dev" IsIconOnly="True" />
<arc:CopyButton Value="#7C5CFF" Label="复制 HEX 值" Variant="Plain" />
```

每个区域只选一种变体。面板中最重要的复制使用 `Outline`，列表中重复出现的或位于代码块中的复制使用 `Plain`。

### 纯图标与标签

`IsIconOnly` 隐藏标签，只留下图标，形成一个 36 px 的方形按钮。`Label` 仍然为辅助技术提供名称，因此纯图标的复制需要一个具体的标签：在有大量行的表格中，“复制订单号”能被准确播报，而“复制”则含糊不清。纯图标按钮宽 36 px，可以放进表格列而不加宽该列。

### 重置时间与事件

按钮在 `ResetAfter` 之后恢复空闲状态，默认为 1.9 秒。复制成功后会触发 `Copied`，复制失败时绝不会触发。`State` 返回 `Idle`、`Copied` 或 `Error`，它是只读的：由控件设置，代码读取。

```csharp
copy.Copied += (_, _) => log.Text = "已复制到剪贴板。";
copy.ResetAfter = TimeSpan.FromSeconds(3);
```

只有当人必须在确认信息消失前读完它时，才应延长 `ResetAfter`，例如复制之后还要在另一个页面上进行下一步。单次复制保留默认值，使控件能尽快为下一次复制做好准备。

### 绑定到视图模型

值通常是视图模型已经持有的数据，因此把 `Value` 单向绑定，让点击时复制的是当时的数据。`Failure` 示例展示了值不存在的情况。

<Demo name="Failure" />

```csharp
public partial class DeveloperKeysViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _apiKey = "ak_live_2026_7f3a9c21";

    [ObservableProperty]
    private string? _installCommand = "npx arc add copy-button";
}
```

```xml
<StackPanel Spacing="8">
    <arc:CopyButton Value="{Binding ApiKey}" Label="复制密钥" />
    <arc:CopyButton Value="{Binding InstallCommand}" Label="复制命令" Variant="Plain" />
</StackPanel>
```

- `Value` 是单向的。控件从不写回视图模型，因此复制操作不会改变它所复制的数据。
- `null` 值会使复制失败：按钮显示失败状态，宽度不变。应把 `Value` 绑定到真正可用的数据，而不是一个可能被误复制的占位值。
- 让 `Value` 与屏幕上的数据保持一致。像 “ak_live_****” 这样的掩码显示不应作为绑定值，否则人粘贴出来的就是掩码。

### 示例：开发者 API 密钥

一个开发者控制台页面，在同一面板中显示生产密钥（带复制操作）和安装命令。密钥是页面要复制的最重要的值，因此它使用 `Outline` 变体和完整标签。命令是次要的值，因此它使用 `Plain` 变体，配一个纯图标复制。两个值都完整显示，人可以在复制前核对。

![一个包含生产密钥与复制命令行的 API 密钥面板](images/ApiKeys.png)

```xml
<StackPanel Width="620" Spacing="16">
    <TextBlock Text="API keys"
               FontSize="{arc:ArcDesignTokenResource TypeLG}"
               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
            Padding="20">
        <StackPanel Spacing="18">
            <StackPanel Spacing="6">
                <TextBlock Text="Production key"
                           FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Created 14 April 2026. Use it on the server only."
                           TextWrapping="Wrap"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
            <Grid ColumnDefinitions="*,Auto">
                <Border Grid.Column="0"
                        Background="{arc:ArcDesignTokenResource SurfaceMuted}"
                        CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                        Padding="12,9"
                        VerticalAlignment="Center">
                    <TextBlock Text="ak_live_2026_7f3a9c21"
                               FontFamily="{arc:ArcDesignTokenResource MonoFontFamily}"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </Border>
                <arc:CopyButton Grid.Column="1"
                                Label="Copy key"
                                Value="ak_live_2026_7f3a9c21"
                                Margin="12,0,0,0"
                                VerticalAlignment="Center" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource BorderSubtle}" />
            <Grid ColumnDefinitions="Auto,*,Auto">
                <TextBlock Grid.Column="0"
                           Text="Install"
                           Margin="0,0,16,0"
                           VerticalAlignment="Center"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="1"
                           Text="npx arc add copy-button"
                           FontFamily="{arc:ArcDesignTokenResource MonoFontFamily}"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <arc:CopyButton Grid.Column="2"
                                Label="Copy command"
                                Value="npx arc add copy-button"
                                Variant="Plain"
                                IsIconOnly="True"
                                VerticalAlignment="Center" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

密钥是页面要复制的唯一值，因此它有完整标签、表面外框，以及一种等宽字体，使 `0` 与 `O` 等字符易于区分。命令是次要的，因此它的纯图标复制让该行保持紧凑，同时其无障碍名称仍能说明它复制的内容。

### 文案规范

- **标签**：动词加宾语，句首大写，例如“复制密钥”或“复制命令”。为值命名，使按钮脱离上下文仍有意义，屏幕阅读器逐个读取按钮列表时也能分辨。
- **已复制标签**：简短的过去式确认，“已复制”或“密钥已复制”。已复制状态只持续片刻，因此这个词必须一眼就能看懂。
- **失败标签**：说明没有成功的结果，而不是原因。“无法复制”提示人再试一次，或手动选中文字。

| 不要写 | 改为 |
|---|---|
| 点击复制 | 复制密钥 |
| 成功！ | 已复制 |
| 错误 | 无法复制 |
| 复制到剪贴板（40 行的表格中） | 复制订单号 |

### 最佳实践

**推荐**

- 在 `Label` 中写明值的名称（“复制密钥”“复制邀请链接”），因为即使图标隐藏了文字，标签也是无障碍名称。列表中重复出现的通用“复制”让屏幕阅读器无法区分各行。
- 复制人需要的完整值，而不是掩码显示。粘贴出来的掩码就是一张本可避免的支持工单。
- 在密集行和代码中使用 `Plain`，面板中最重要的复制使用 `Outline`。每个面板只保留一个带外框的复制，主要操作才清晰。
- 单次复制保留默认的 `ResetAfter`，使控件在人继续操作前回到标签状态。

**避免**

- 在数据可能缺失时绑定 `Value` 却不处理失败。`null` 值会显示失败状态，人需要一个再试一次的理由，而不是一个毫无反应的按钮。
- 把复制与破坏性操作合并在一个按钮中，例如“复制并轮换密钥”。复制是安全的，因此应把轮换放在有确认的独立控件中。
- 一行中堆叠多个带外框的复制按钮。视觉上它们会被看作同等重要的多个操作；次要的复制改用 `Plain`。
- 依赖 `Copied` 事件来显示消息。按钮已经在原位确认了复制；再显示一条消息只是重复同一信号，还会与页面争抢注意力。

### 常见问题

**为什么设置了 `Value` 按钮仍显示失败状态？** 当剪贴板拒绝写入时，点击会报告失败，因此失败状态并非仅由值引起。显示失败标签，并让人从可选中的字段中手动复制文本。

**复制失败时会触发 `Copied` 吗？** 不会。该事件只在复制成功后触发。用 `State` 读取上一次点击的结果，包括 `Error`。

**按钮能复制人点击时才生成的值吗？** 仅靠 `Value` 不行。值是在点击时读取的，因此要让它与屏幕上的数据保持同步。长的或远程获取的值应在页面显示按钮之前生成，再把结果绑定到 `Value`。

**如何在窄屏幕上只显示图标？** 在布局变窄时从代码中设置 `IsIconOnly`，例如在 `SizeChanged` 处理程序中。标签仍是无障碍名称，因此要保持它的具体性。

## API 参考

<!-- api: CopyButton -->

## 无障碍

- 无障碍名称始终是 `Label`，即使按钮只显示图标。
- 一个隐藏的礼貌状态区域会播报结果，例如“复制密钥：已复制”或“复制密钥：无法复制”。
- Enter 与 Space 执行复制。
- 标签单元保持其最宽状态的宽度，因此按钮周围的布局不会移动。
- 键盘焦点会显示焦点环。指针聚焦时不绘制焦点环。
- 禁用的按钮降至一半透明度。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到下一个或上一个可聚焦控件，包括复制按钮 |
| `Enter` 或 `Space` | 复制该值；显示已复制或失败状态，`ResetAfter` 之后恢复静止 |

## 动效

- 图标以柔和的模糊切换，并以缓慢、近乎临界阻尼的弹簧缩放进入。对勾会自行绘制出来。
- 只有标签中变化的字母会移动，恢复静止时按相反方向播放。
- 按下时内容轻微缩小，松开时以弹簧弹回。
- 减弱动效时，图标与文字使用瞬时交叉淡化，跳过描边绘制与按下缩放。关闭动效时立即替换图标与标签。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 已复制或失败 | 图标以柔和模糊切换，对勾自行绘制 | 图标与标签瞬时交叉淡化；对勾不绘制 | 立即 |
| 标签变化 | 只有变化的字母移动，静止时反向回位 | 瞬时交叉淡化 | 立即 |
| 按下 | 内容缩放至 0.97（纯图标时为 0.96），松开时弹回 | 无按下缩放 | 无按下缩放 |
| 悬停 | 外框交叉淡化为柔和的表面色与更醒目的边框 | 瞬时 | 立即 |

## 相关组件

- `SplitButton`：复制只是多个操作之一时使用，例如“复制页面”，并提供“复制链接”和“以 Markdown 复制”。
- `ActionButton`：异步提交，带有等待与成功状态。
- `Button`：没有剪贴板结果的普通操作。
- `CodeBlock`：已包含复制控件的代码示例。

| 组件 | 适用场景 |
|---|---|
| `SplitButton` | 复制是主要操作，其他选项是同一个值的格式或变体 |
| `CodeBlock` | 被复制的文本是代码示例，且已自带复制控件 |
| `Input`（`IsReadOnly`） | 值必须保持可选中、可完整阅读，旁边有复制操作 |
| `ActionButton` | 操作需要发送或保存内容，并需要等待与成功状态 |
| `DropdownMenu` | 复制是多个行命令之一，适合放在菜单中 |
