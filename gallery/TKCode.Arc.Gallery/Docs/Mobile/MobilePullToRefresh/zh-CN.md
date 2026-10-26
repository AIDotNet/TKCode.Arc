---
title: 移动端下拉刷新
summary: 在可滚动内容的顶部下拉即可刷新，带橡皮筋阻尼、加载圈和弹簧回弹。
source-hash: 6f5a4c7796c7
---

<Demo name="Basic" />

## 何时使用

`PullToRefresh` 包住滚动的列表或页面，让人用早已熟悉的手势请求最新数据。

- **信息流和收件箱**：消息、时间线和订单列表，内容在服务器上会变。
- **任何有重新加载操作的列表**：用按钮会显得杂乱的时候。

实时示例在手机外框里运行，那是用鼠标驱动的桌面模拟；真机上的触摸表现，以及它与平台自带过度滚动的配合，这里没有验证。示例用定时器结束刷新，就像一次请求那样。

## 何时不用

- **不能滚动、也不需要重新加载的内容**：没有可拉的东西。
- **应该自动到达的更新**：在后台刷新并展示结果。
- **横向列表**：下拉只在纵向。

## 用法

把滚动内容放进去。内容在顶部时向下拉，内容会带着阻尼跟手，箭头随下拉接近 `PullThreshold`（默认 64 px）而翻转。越过阈值松手会触发 `RefreshRequested`；提前松手，内容弹回。

```xml
<arcm:PullToRefresh Command="{Binding ReloadCommand}">
    <ScrollViewer>
        <!-- the list -->
    </ScrollViewer>
</arcm:PullToRefresh>
```

刷新会带着加载圈一直进行，直到 `IsRefreshing` 再次为 false。可以绑定 `IsRefreshing`（它是双向的），也可以从事件数据里取一个延迟（deferral），工作完成时释放它。

```csharp
refresher.RefreshRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    await LoadAsync();
};
```

也可以在代码里设置 `IsRefreshing`，不经下拉直接显示加载圈，此时不会触发 `RefreshRequested`。

### 穿透

手势开始时内部的 `ScrollViewer` 已经滚动过、向上，或主要是横向，就交给内容处理，所以滚动长列表不会和下拉打架。刷新进行时不能再下拉。

### Token

`PullToRefreshToken` 保存几何：`IndicatorExtent`（56，也是刷新时内容停靠的位置）、`ArrowSize`（20）、`SpinnerSize`（22）和 `SpinnerThickness`（2）。颜色来自 ArcDesign token。

## API 参考

<!-- api: MobilePullToRefresh -->

## 无障碍

刷新是一种手势，所以在要紧的地方要给出另一种重新加载的办法（菜单项或按钮）。`RefreshRequested` 和 `Command` 在松手时运行，按住时不运行。

## 动效

内容按橡皮筋曲线跟手：起初跟得上，越往下阻力越大。松手时，手指的速度交给弹簧，内容平滑地停在指示器上或回到顶部。新的下拉能在内容飞行中接住它。下拉达到阈值时箭头翻转，刷新时由加载圈取代。减弱动效时，内容不随手指拉伸；刷新照常进行，内容直接跳转。

## 相关组件

- `MobileListGroup`：通常的内容。
- `MobilePage`：承载被刷新的屏幕。
- `MobileNavbar`：它上方的栏。
