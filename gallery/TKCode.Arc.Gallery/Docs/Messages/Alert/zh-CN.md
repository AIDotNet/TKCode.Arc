---
title: 警告提示
summary: 持续显示的消息，帮助用户恢复或继续操作。
source-hash: 1e6aba0fce9e
---

## 何时使用

- 页面或表单中的行内通知，例如即将过期的卡片或未完成的域名验证。
- 随时间变化的状态。保持同一个 `Alert`，改变它的语气、标题和内容即可。
- 用户可以关闭的通知，关闭后其下方的空间也随之收起。

## 何时不用

- 对已完成操作的简短确认：用 `Toast`。
- 带类型、操作和堆叠的应用通知：用 `ToastStack`。
- 用户必须先作出回应才能继续的消息：用 `Dialog`。
- 没有内容可显示的页面或列表：用 `EmptyState`。

## 用法

基础用法需要设置 `Title` 和 `Content`。默认的 `Tone` 为 `Info`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Alert Title="你的银行卡下个月到期"
               Content="更新付款方式，以保持套餐有效。" />
</UserControl>
```

### 语气

`Tone` 决定图标及其颜色。`Danger` 以强提示方式播报，其他语气以礼貌方式播报。

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="Success" Title="域名已验证"
               Content="发往 acme.dev 的邮件现在可以正常送达。" />
    <arc:Alert Tone="Warning" Title="存储空间即将用尽"
               Content="请释放空间或升级，否则上传将暂停。" />
    <arc:Alert Tone="Danger" Title="付款失败"
               Content="无法扣除尾号 4242 的银行卡付款。" />
</StackPanel>
```

### 原位变形

状态变化时保留同一个实例，只修改其属性。图标会切换，新文字向上浮现，高度随弹簧动画变化。若改为替换控件，则会跳过这些过渡效果。

```csharp
var status = new Alert { Title = "可以同步了", Content = "更改已保存在此设备上。" };

async void OnSave(object? sender, RoutedEventArgs e)
{
    status.Tone = AlertTone.Info;
    status.Title = "正在保存更改";
    status.Content = "正在将 3 个文件上传到工作区。";

    await UploadAsync();

    status.Tone = AlertTone.Success;
    status.Title = "所有更改已保存";
    status.Content = "工作区中的所有人现在都能看到最新版本。";
}
```

### 关闭

设置 `IsDismissible` 即可显示关闭按钮。按下后警告收起，然后触发 `Dismissed`。将 `IsOpen` 重新设为 `True` 可再次显示警告。

```xml
<arc:Alert Tone="Warning"
           Title="周日计划维护"
           Content="02:00 至 03:00（UTC）期间仪表盘为只读。"
           IsDismissible="True" />
```

```csharp
var notice = new Alert { Tone = AlertTone.Warning, IsDismissible = true, Title = "周日计划维护" };
notice.Dismissed += (_, _) => SaveDismissedState();

// 稍后再次显示该警告。
notice.IsOpen = true;
```

### 自定义内容

非文本的 `Content` 会显示在标题下方。文本内容会应用详情样式；其他内容按原样显示。

```xml
<arc:Alert Title="有新版本可用">
    <arc:Alert.Content>
        <arc:Button Content="重新加载" Size="Small" Variant="Secondary" />
    </arc:Alert.Content>
</arc:Alert>
```

## API 参考

<!-- api: Alert -->

## 无障碍

- `Title` 为警告命名，`Content` 作为其辅助文字被读取。标题变化后会重新播报。
- `Danger` 警告以强提示方式播报；其他语气以礼貌方式播报。
- 关闭按钮的名称为 `关闭：{Title}`，让用户听到它关闭的是哪一条警告。
- 语气图标不向辅助技术暴露，语气通过标题和内容传达。
- 关闭按钮是普通按钮：Tab 可以聚焦，Enter 或空格键可以激活。

## 动效

- 显示和隐藏警告时，高度以平滑弹簧展开或收起，下方内容随之上移或下移。
- 语气变化时，图标以短促的缩放加模糊切换；变化的文字向上浮现并变清晰。
- 减弱动效时，去掉高度动画和位移。文字与图标只做淡入淡出，显示或隐藏立即完成。
- 关闭动效时直接到达最终状态。

## 相关组件

- `Toast`：已完成操作的简短确认。
- `ToastStack`：排队、更新并堆叠显示的应用通知。
- `AnnouncementBar`：网站顶部轮播的横幅。
- `EmptyState`：完全没有内容可显示时使用。
