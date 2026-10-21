---
title: Cookie 同意
summary: 同意条可展开为偏好设置，并收拢为重新打开按钮。
source-hash: 46fd1a9f2206
---

<Demo name="ConsentBar" />

## 何时使用

- 需要访客接受或拒绝 Cookie 与追踪的营销网站或应用。
- 按类别征求同意：可选类别默认关闭，访客之后可以更改选择。
- 提供重新打开按钮，让访客在保存选择后仍能修改。

## 何时不用

- 不需要任何选择的全站通知：用 `AnnouncementBar`。
- 无需选择、会自行消失的消息：用 `Toast`。
- 必须等用户回答才能继续浏览页面的决定：用 `Dialog`。
- 页面某个区域内的普通消息：用 `Alert`。

## 用法

将控件放在页面之上，例如作为窗口根 `Grid` 的最后一个子元素。该控件是一个图层：除自身表面外，不接收指针输入。未设置类别时，使用 Arc 的四个默认类别：严格必要、偏好设置、分析与营销。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- 页面内容放在这里。 -->
        <arc:CookieConsent StorageKey="site-consent" Version="1" />
    </Grid>
</UserControl>
```

### 类别与政策

设置 `Categories` 以定义类别。`IsRequired` 使某个类别始终开启，`IsEnabledByDefault` 设置可选类别的初始状态。`Policy` 会在同意条与面板中添加指向完整政策的链接。

```xml
<arc:CookieConsent StorageKey="site-consent" Version="2">
    <arc:CookieConsent.Categories>
        <arc:CookieCategory Id="necessary" Label="严格必要"
                            Description="网站正常运行所需。"
                            IsRequired="True" IsEnabledByDefault="True" />
        <arc:CookieCategory Id="analytics" Label="分析"
                            Description="帮助我们了解哪些页面有用。" />
    </arc:CookieConsent.Categories>
    <arc:CookieConsent.Policy>
        <arc:CookieConsentPolicy Label="Cookie 政策" Href="https://example.com/cookies" />
    </arc:CookieConsent.Policy>
</arc:CookieConsent>
```

类别或政策变化时请修改 `Version`。版本不同的已保存选择会被忽略，访客将再次被询问。

### 响应选择

访客保存、接受、拒绝或重置选择时会引发 `ConsentChanged`。`Consent` 将每个类别 id 映射为 `True` 或 `False`；访客尚未决定时为 `null`。

```csharp
consent.ConsentChanged += (_, e) =>
{
    if (e.Consent is not null && e.Consent.TryGetValue("analytics", out var allowed))
    {
        Analytics.Enabled = allowed;
    }
};
```

### 读取已保存的选择

在控件显示之前、启动时读取已保存的选择，即可为已经做出决定的访客跳过同意条。

```csharp
var record = CookieConsent.Read("site-consent", "2");
if (record is not null)
{
    Analytics.Enabled = record.Value.TryGetValue("analytics", out var allowed) && allowed;
}
```

### 位置与重新打开按钮

`Position` 将同意条设置在底部、左下或右下。保存选择后，`IsReopenButtonVisible` 会保留 44px 的按钮。`OpenPreferences` 可从其他控件（例如页脚链接）打开面板。

```xml
<arc:CookieConsent Position="BottomRight" IsReopenButtonVisible="True" />
```

```csharp
settingsLink.Click += (_, _) => consent.OpenPreferences();
```

## API 参考

<!-- api: CookieConsent -->

<!-- api: CookieCategory -->

## 无障碍

- 同意条与面板都是非模态对话框。二者均以标题命名，其消息或描述作为辅助文字。
- 打开面板时焦点移到其标题。保存面板后焦点移到重新打开按钮。
- 每个类别的开关以类别标签命名，描述作为辅助文字。必需类别以 `Always on` 文字代替开关。
- Tab 在按钮与开关之间移动。空格键切换当前焦点开关，点击类别名称也会切换。
- Escape 关闭面板且不保存，焦点返回同意条。
- 全部拒绝与全部接受权重相同，并排放置。

## 动效

- 同意条、面板与重新打开按钮是同一个表面，它会改变位置、宽度、高度与圆角。
- 每个面板内容从短暂的模糊中上升几像素出现。离场的面板淡出并略微下沉。
- 同意条以轻微缩放从底边进入。
- 减弱动效时，表面在各形态之间直接跳转，内容只做淡入。关闭动效时，每次变化立即显示。

## 相关组件

- `AnnouncementBar`：不需要用户选择的全站通知。
- `Dialog`：必须等用户回答才能继续浏览页面的决定。
- `Toast`：会自行关闭的简短确认。
