---
title: 用户菜单
summary: 头像背后的账户、设置、主题与退出登录。在手机上以底部面板的形式打开。
source-hash: 86d8f44034b9
---

<Demo name="TopBar" />

## 何时使用

用户菜单是应用顶栏中唯一的账户入口。它位于头像之后，汇集已登录用户相关的内容：身份信息头部、两到四个账户入口、主题选择、可选的在线状态以及退出登录。它让账户相关内容远离页面工具栏，页面自身的操作保持可见，而账户仍然只需一次点击即可到达。

- **SaaS 管理后台中的账户入口**：管理多个工作区的管理员，其头像之后有个人资料、工作区设置和账单。计划徽标在姓名旁显示“管理员”或“Pro”。
- **团队应用中的在线状态**：值班工程师在应急响应期间把状态设为“忙碌”，头像上的圆点即可告诉同事其状态，无需发送聊天消息。
- **仪表板或设计工具中的主题选择**：在菜单中选择“浅色”“深色”或“跟随系统”。选择之后菜单保持打开，使页面可以当着用户的面切换主题。
- **耗时较长的退出登录**：退出登录时需要在服务端吊销令牌。该行会显示加载指示器，直到任务完成，菜单随后关闭。
- **窄窗口与手机**：宽度低于 640 px 时，同一个菜单以底部面板的形式打开，因此手机上也能找到账户，而无需第二种导航模式。

## 何时不用

- **与账户无关的通用命令列表**：用 `DropdownMenu`，它带有标签触发器，例如报表页面上的“操作”菜单。
- **单独使用、不在账户菜单之内的主题切换**：用 `ThemeSwitcher`，它是与具体用户无关的浅色与深色控件。
- **超过四个入口**：把多出的入口移到一个页面中，再从其中一个入口链接过去。菜单过长就不再是快捷入口了。
- **从列表行或卡片打开的菜单**：对对象的右键操作用 `ContextMenu`，或在可见的触发器上使用 `DropdownMenu`。
- **从列表中选择的表单值**：用 `Select` 或 `Combobox`。用户菜单承载的是命令和两个切换项，而不是为表单提供数值的控件。

## 用法

设置身份属性，并在 `UserMenu.Items` 下声明入口。头像即为触发器。

`UserMenu` 派生自 Avalonia 的 `TemplatedControl`。它的触发器是 Arc `Button`（Ghost，40 px 高的胶囊形），其中包含头像、在线状态圆点和姓名。其面板是 Account 变体的 `MenuSurface`，由控件配置并持有。宽度低于 640 px 时，同一个弹层切换为 Sheet 变体。面板包含由身份属性生成的头部、入口行、主题与状态切换，以及退出登录行。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:UserMenu DisplayName="Ada Lovelace"
                  Email="ada@analytical.dev"
                  Plan="Pro"
                  IsNameVisible="True"
                  SignOutGestureText="⇧⌘Q">
        <arc:UserMenu.Items>
            <arc:MenuEntry Header="个人资料" />
            <arc:MenuEntry Header="设置" InputGestureText="⌘ ," />
            <arc:MenuEntry Header="账单" />
        </arc:UserMenu.Items>
    </arc:UserMenu>
</UserControl>
```

### 结构

1. **触发器**（模板中的 Ghost `arc:Button`）：40 px 高的胶囊，四周内边距为 4 px。显示姓名时，右侧内边距为 10 px。按下时它从不缩放；按下与打开只改变其背景色。
2. **头像**（`DisplayName`、`AvatarSource`）：32 px 的头像。设置了 `AvatarSource` 时显示照片；照片加载期间或加载失败时，显示姓名首字母。
3. **在线状态圆点**（`Status`、`IsStatusVisible`）：位于头像右下角的 11 px 圆点，外圈有 2 px 的触发器背景色描边。“在线”为实心的成功色圆点，“忙碌”为带横条的危险色圆点，“离开”为中空的警告色圆点，因此即使不依赖颜色也能分辨状态。
4. **姓名**（`DisplayName`、`IsNameVisible`）：中等字重，最宽 176 px，超出时以省略号截断。只有当 `IsNameVisible` 为 `True` 且窗口宽度至少 640 px 时，它才会显示在头像旁边。
5. **箭头**：16 px，使用弱化文字颜色。它与姓名一同显示，菜单打开时旋转 180 度。
6. **面板**（`PanelAlignment`）：账户面板宽 248 px（15.5 rem），与头像相距 8 px。默认对齐为 `End`，使面板与触发器的右边缘对齐。
7. **身份头部**：34 px 的头像（显示状态时带有在线状态圆点）、姓名及其旁边的 `Plan` 徽章，以及姓名下方的 `Email`。过长的姓名、计划或邮箱会以省略号结束，而不会让面板变宽。
8. **入口**（`Items` 或 `ItemsSource`）：32 px 高的行，文字 13 px，每行可带可选图标，末端为可选的快捷键提示。
9. **主题切换**（`ThemePreference`、`IsThemeSwitchVisible`）：一行“主题”，含三个分段：浅色（太阳图标）、深色（月亮图标）和跟随系统（显示器图标）。在面板中高 36 px，在底部面板中高 52 px。
10. **状态切换**（`Status`、`IsStatusVisible`）：一行“状态”，含三个分段：在线、忙碌和离开，高度与主题切换相同。
11. **退出登录行**：LogOut 图标、“退出登录”文字，以及 `SignOutGestureText` 提示。退出登录任务运行时，文字变为“正在退出登录”，图标变为加载指示器。
12. **分隔线**：入口、切换项与退出登录行之间的线条。

### 选择布局

- 在顶栏较宽、姓名有助于确认账户的场合，显示姓名（`IsNameVisible="True"`）。在较窄的工具栏中则隐藏姓名，仅头像就已足够。
- 在支持两种主题的应用中，保持 `IsThemeSwitchVisible` 的默认值 `True`。若应用只有一种主题，则设为 `False`，使菜单不提供无效的选择。
- 只在团队成员会设置和查看状态的应用中显示在线状态圆点（`IsStatusVisible="True"`）。没有人设置的圆点会给每个头像增加干扰。
- 当头像位于窗口左边缘时使用 `PanelAlignment="Start"`，使面板朝内容方向展开。

### 主题与状态

`ThemePreference` 可为 `Light`、`Dark` 或 `System`。菜单会报告所选的值并保持打开。主题由应用自行应用。`IsThemeSwitchVisible="False"` 会隐藏主题切换。

<Demo name="Presence" />

```xml
<arc:UserMenu DisplayName="Grace Hopper"
              Email="grace@compiler.dev"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              Status="{Binding Status, Mode=TwoWay}"
              IsStatusVisible="True" />
```

```csharp
userMenu.ThemePreferenceChanged += (_, e) => ApplyTheme(e.ThemePreference);
userMenu.StatusChanged += (_, e) => status.Text = $"状态：{e.Status}";
```

`Status` 与 `ThemePreference` 为双向绑定，因此视图模型始终持有当前的选择。这些事件只在用户于菜单中作出选择时触发。使用它们来应用主题或把状态发送到服务端，因为菜单自身两者都不做。

### 异步退出登录

选中退出登录行时会执行 `SignOutAction`。任务运行期间，该行显示加载指示器，菜单保持打开。任务完成后菜单才会关闭。

```csharp
userMenu.SignOutAction = () => auth.SignOutAsync();
userMenu.SignOutRequested += (_, _) => status.Text = "正在退出登录…";
```

`SignOutRequested` 在动作执行之前触发，因此适合在此显示状态文字或清除本地缓存。如果动作抛出异常，菜单仍会停止等待并关闭；失败信息应由动作自行报告，因为菜单不会显示它。未设置动作，或任务已经完成时，菜单会立即关闭，并把焦点交还头像。

### 照片与数据驱动的入口

`AvatarSource` 用于显示照片。未设置照片，或照片仍在加载时，显示姓名首字母。当 `Items` 不够用时，`ItemsSource` 可根据数据生成入口。

```xml
<arc:UserMenu DisplayName="{Binding Name}"
              Email="{Binding Email}"
              AvatarSource="{Binding Avatar}"
              ItemsSource="{Binding AccountLinks}" />
```

每个生成的行显示该条目；若设置了 `ItemTemplate`，则显示模板的结果。`ItemClicked` 通过 `e.Item` 报告所在行及其数据项，因此一个处理程序即可把每个入口导航到对应的页面。

### 绑定到视图模型

视图模型持有身份、主题、状态与退出登录逻辑。菜单负责显示它们，并通过双向属性和事件回报。把账户入口作为数据保存在视图模型中，使菜单与导航保持一致。

```csharp
public partial class AccountViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public AccountViewModel(IAuthService auth) => _auth = auth;

    [ObservableProperty]
    private string _displayName = "Ada Lovelace";

    [ObservableProperty]
    private string _email = "ada@analytical.dev";

    [ObservableProperty]
    private UserMenuThemePreference _themePreference = UserMenuThemePreference.System;

    [ObservableProperty]
    private UserMenuStatus _status = UserMenuStatus.Available;

    [RelayCommand]
    private Task SignOutAsync() => _auth.SignOutAsync();
}
```

```xml
<arc:UserMenu DisplayName="{Binding DisplayName}"
              Email="{Binding Email}"
              IsNameVisible="True"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              IsStatusVisible="True"
              Status="{Binding Status, Mode=TwoWay}" />
```

```csharp
userMenu.SignOutAction = () => viewModel.SignOutCommand.ExecuteAsync(null);
```

- `ThemePreference` 与 `Status` 为双向绑定，因此在菜单中选择的主题会传到视图模型，而在代码中设置的状态会反映到圆点和切换项上。
- `DisplayName` 与 `Email` 为单向绑定：资料更新后视图模型修改它们，头部随之更新。
- 使用 `ThemePreferenceChanged` 应用主题，使用 `StatusChanged` 保存状态。双向值只记录选择；应用真正据此执行操作的地方是事件。
- 退出登录命令是一个 `Func<Task>`，因此应在代码中赋值，或把命令包装在返回 `Task` 的方法中。

### 焦点与关闭

指针打开菜单时，焦点位于面板上，且不设高亮行。键盘打开时，按 Enter、Space 或 ArrowDown 会高亮第一行，按 ArrowUp 则高亮最后一行。页面加载时若以 `IsOpen` 设置为启动即打开的菜单，不会获得焦点，也不会滚动页面。

选择某个入口会关闭菜单。主题与状态切换会让菜单保持打开，使用户可以在一次访问中切换两者。退出登录行会让菜单保持打开并显示加载指示器，直到任务结束后再关闭它。外部按下会关闭菜单；在底部面板中，按下遮罩同样会关闭。

窗口跨过 640 px 断点时，若菜单正处于打开状态，菜单会直接关闭，而不是把面板转换为底部面板，用户需要在新的形态下重新打开它。Escape 和 Tab 会关闭菜单，并把焦点交还头像。

### 示例：运维控制台顶栏

应急控制台的顶栏显示值班工程师的账户。控制台是宽窗口，因此显示姓名；在线状态圆点显示“忙碌”，让同事知道工程师正在处理事件。计划徽章显示“管理员”，菜单包含三个入口，退出登录位于最后并带有快捷键提示。主题切换保持默认值，因为响应人员常在暗色环境中工作，需要无需进入设置页即可切换。

```xml
<Border Padding="16,8"
        CornerRadius="18"
        BorderThickness="1"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <DockPanel>
        <TextBlock DockPanel.Dock="Left"
                   Text="事件控制台"
                   FontSize="15"
                   FontWeight="Medium"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:UserMenu DockPanel.Dock="Right"
                      DisplayName="Grace Hopper"
                      Email="grace@compiler.dev"
                      Plan="管理员"
                      IsNameVisible="True"
                      IsStatusVisible="True"
                      Status="Busy"
                      SignOutGestureText="⇧⌘Q">
            <arc:MenuEntry Header="个人资料" Icon="{x:Static arc:ArcIcons.User}" />
            <arc:MenuEntry Header="值班排期" Icon="{x:Static arc:ArcIcons.Clock}" />
            <arc:MenuEntry Header="账单" Icon="{x:Static arc:ArcIcons.CreditCard}" />
        </arc:UserMenu>
    </DockPanel>
</Border>
```

### 文案规范

菜单简短，一眼即可读完。入口是它所打开页面的名词，切换项与退出登录行则使用应用其他地方通用的用词。

- 入口以其页面命名，例如“账单”“工作区设置”。保持在约 24 个字符以内。
- 退出登录行使用“退出登录”，由动词及其补语构成，标签中不加感叹号，也不加确认文字。
- 计划徽章保持一个词，例如“Pro”或“管理员”；它是徽章，不是句子。
- 三种状态按原样使用。菜单提供“在线”“忙碌”和“离开”；“离线”不是其中之一。
- 主题选项写成单个词：浅色、深色、跟随系统。

| 不要写 | 改为 |
|---|---|
| 登出 | 退出登录 |
| 我的账户设置 | 个人资料 |
| 暗黑模式 | 深色 |
| 离线 | 离开 |
| 立即退出！ | 退出登录 |

### 最佳实践

**推荐**

- 入口保持在两到四个。第五个入口应放在页面中，并从其中一个入口链接过去。
- 退出登录始终放在最后一行。布局本身已将它与其他行分开，不要在它下面再添加行。
- 只在团队成员会设置和查看状态的应用中显示在线状态圆点。没有人设置的圆点会给每个头像增加干扰。
- 在 `ThemePreferenceChanged` 中应用主题，使选择在菜单仍然打开时即刻生效。
- 让 `Email` 与 `Plan` 使用合适长度的值。过长的值会被截断，因此简短的姓名与计划更易读。

**避免**

- 在菜单中放置长篇的设置表单。表单应放在 `Drawer` 或页面中；菜单只用于选择和导航。
- 放置很长的账户或工作区列表。切换工作区应使用 `Select` 或页面，因为菜单只能舒适地容纳少数几行。
- 围绕菜单中的某个设置来设计页面。断点固定为 640 px，因此页面要同时为面板和底部面板而设计。
- 执行退出登录操作却不给出反馈。未完成的任务会显示加载指示器；瞬间完成的任务则直接关闭菜单，不显示指示器。
- 在菜单中添加自定义切换项。API 只提供主题与状态切换；其他选择应放在 `DropdownMenu` 或页面中。

### 常见问题

**为什么我的窄窗口上不显示姓名？** 宽度低于 640 px 时，触发器只显示头像，菜单以底部面板的形式打开。`CompactBreakpoint` 是固定为 640 的常量，因此应为窄窗口中只显示头像的触发器规划布局。

**为什么在线状态圆点不见了？** `IsStatusVisible` 默认为 `False`。将其设为 `True` 即可显示圆点和状态切换。

**如何隐藏主题选择？** 设置 `IsThemeSwitchVisible="False"`。主题行随即从面板中移除，应用可在其他位置提供自己的主题控件。

**退出登录的加载指示器如何出现？** 从 `SignOutAction` 返回一个未完成的 `Task`。该行会显示加载指示器和“正在退出登录”文字，直到任务完成，然后菜单关闭。

**能否在窄窗口中显示姓名和箭头？** 不能。姓名和箭头只在宽度至少 640 px 的窗口中显示，底部面板只保留头像。应围绕这一点规划页面布局，因为菜单没有将断点作为可设置项公开。

## API 参考

<!-- api: UserMenu -->

<!-- api: MenuEntry -->

菜单报告的两种选择类型：

| 类型 | 取值 | 含义 |
|---|---|---|
| `UserMenuThemePreference` | `Light`、`Dark`、`System` | 用户所选的主题；默认为 `System` |
| `UserMenuStatus` | `Available`、`Busy`、`Away` | 圆点与状态切换所显示的在线状态；默认为 `Available` |

## 无障碍

- 头像触发器会以“账户菜单”加显示名称的方式被播报。显示状态时，状态会追加到名称之后。
- 各行具有菜单项角色。主题与状态切换分别是有名称的分组，每个选项都可被选中。
- 方向键可在主题或状态切换中选择，Enter 或空格键激活某一行。
- Escape 或 Tab 会关闭菜单，并将焦点交还头像。
- 在线状态圆点在颜色之外还会改变形状，因此不只依赖颜色。
- 退出登录任务运行期间，退出登录行会报告自身处于忙碌状态。
- 为账户入口提供说明其去向的 `Header`，例如“账单”或“工作区设置”。屏幕阅读器通过各行的标题来朗读它们。

### 键盘

| 按键 | 操作 |
|---|---|
| 头像上的 `Enter`、`Space` 或 `ArrowDown` | 打开菜单，并高亮第一行 |
| 头像上的 `ArrowUp` | 打开菜单，并高亮最后一行 |
| 在头像上点击或轻触 | 切换菜单 |
| `ArrowDown` / `ArrowUp` | 将高亮移到下一行或上一行；列表循环，并跳过禁用的行 |
| `Home` / `End` | 将高亮移到第一行或最后一行 |
| 字母键 | 输入首字母，移动到文字以所输字母开头的行，例如 `S` 定位到“退出登录” |
| 切换项上的 `ArrowLeft` / `ArrowRight` | 选择上一个或下一个选项，到头后回绕；菜单保持打开 |
| `Enter` / `Space` | 激活某个入口或退出登录行；在切换行上无效 |
| `Escape` / `Tab` | 关闭菜单，并将焦点交还头像 |

## 动效

- 在宽窗口中，面板从头像处展开，没有回弹过冲；各行依次淡入。
- 一个高亮条在各行之间滑动，主题与状态的滑块滑到所选选项上。
- 宽度低于 640px 时，菜单以底部面板的形式打开，从底边升起。它会跟随向下拖动，拖得足够远时关闭。
- 减弱动效时，面板与切换项使用短促的淡入，高亮直接跳转，并禁用拖动。关闭动效时菜单立即显示。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 宽面板打开 | 从头像处以平滑弹簧从 0.94 展开，无过冲，随后各行以 16 ms 间隔淡入 | 短促淡入 | 立即 |
| 宽面板关闭 | 在 120 ms 内缩放至 0.97 并淡出 | 短促淡入 | 立即 |
| 高亮移动 | 指针与键盘操作均在行之间滑动 | 直接跳转 | 立即 |
| 切换滑块移动 | 以 580 ms 的弹簧滑到所选选项 | 短促淡入 | 立即 |
| 底部面板打开（低于 640 px） | 从底边以平滑弹簧升起；各行在 60 ms 后以 20 ms 间隔进入 | 短促淡入 | 立即 |
| 底部面板拖动 | 跟随向下拖动；超过 80 px 或快速轻扫时关闭 | 禁用拖动 | 立即 |
| 箭头 | 随菜单打开以弹簧旋转 180 度 | 立即转向 | 立即 |

## 相关组件

- `DropdownMenu`：带标签触发器之后的通用命令列表。
- `ThemeSwitcher`：独立的浅色与深色切换，不属于账户菜单。
- `Avatar`：触发器所显示的图片，也可在身份头部中复用。
- `MenuSurface`：面板与底部面板所基于的共享菜单弹层。

| 组件 | 适用场景 |
|---|---|
| `DropdownMenu` | 命令与账户无关，需要带标签的触发器 |
| `ThemeSwitcher` | 浅色与深色切换单独存在，不属于账户 |
| `Avatar` | 在列表或头部中用图片或首字母标识一个人 |
| `ContextMenu` | 操作属于单个对象，并在指针处打开 |
| `Drawer` | 账户设置需要带表单的完整侧边面板，而非简短菜单 |
| `Dialog` | 退出登录或更改账户前需要先确认 |
