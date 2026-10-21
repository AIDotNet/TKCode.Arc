---
title: 用户菜单
summary: 头像背后的账户、设置、主题与退出登录。在手机上以底部面板的形式打开。
source-hash: 7890184bb2a3
---

<Demo name="TopBar" />

## 何时使用

- 应用顶栏中唯一的账户入口，位于头像之后。
- 两到四个账户相关的入口，例如个人资料、设置和账单，并附带主题选择与退出登录。
- 团队应用中显示在头像上的在线状态切换。

## 何时不用

- 与账户无关的通用命令列表：用 `DropdownMenu`。
- 不属于账户菜单、单独使用的主题切换：用 `ThemeSwitcher`。
- 入口超过四个：把多出的入口移到一个页面中，再从菜单链接过去。
- 从列表行或卡片打开的菜单：用 `ContextMenu` 或 `DropdownMenu`。

## 用法

设置身份相关属性，并在 `UserMenu.Items` 下声明各个入口。头像即为触发器。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
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

### 主题与状态

`ThemePreference` 可为 `Light`、`Dark` 或 `System`。菜单会报告所选的值并保持打开。主题由应用自行应用。`IsThemeSwitchVisible="False"` 可隐藏主题切换。

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

### 异步退出登录

选中退出登录行时会执行 `SignOutAction`。任务运行期间，该行显示加载指示器，菜单保持打开。任务完成后菜单才会关闭。

```csharp
userMenu.SignOutAction = () => auth.SignOutAsync();
userMenu.SignOutRequested += (_, _) => status.Text = "正在退出登录…";
```

### 照片与数据驱动的入口

`AvatarSource` 用于显示照片。未设置照片，或照片仍在加载时，显示姓名首字母。当 `Items` 不够用时，`ItemsSource` 可以根据数据生成入口。

```xml
<arc:UserMenu DisplayName="{Binding Name}"
              Email="{Binding Email}"
              AvatarSource="{Binding Avatar}"
              ItemsSource="{Binding AccountLinks}" />
```

## API 参考

<!-- api: UserMenu -->

<!-- api: MenuEntry -->

## 无障碍

- 头像触发器会以“账户菜单”加显示名称的方式被播报。显示状态时，状态会追加到名称之后。
- 各行具有菜单项角色。主题与状态切换分别是有名称的单选组。
- 方向键可在主题或状态切换中选择，Enter 或空格键激活某一行。
- Escape 或 Tab 会关闭菜单，并将焦点交回头像。
- 在线状态点同时通过形状和颜色变化表达，不只依赖颜色。

## 动效

- 在宽窗口中，面板从头像处展开，没有回弹过冲；各行依次淡入。
- 一个高亮条在各行之间滑动，主题与状态的滑块滑到所选选项上。
- 宽度低于 640px 时，菜单以底部面板的形式打开，从底边升起。它会跟随向下拖动，拖得足够远时关闭。
- 减弱动效时，面板与切换项使用短促的淡入淡出，高亮直接跳转，并禁用拖动。关闭动效时菜单立即显示。

## 相关组件

- `DropdownMenu`：位于带标签触发器之后的通用命令列表。
- `ThemeSwitcher`：不属于账户菜单的浅色与深色切换。
- `Avatar`：触发器所显示的图片，也可在身份信息头部复用。
- `MenuSurface`：面板与底部面板所基于的共享菜单弹层。
