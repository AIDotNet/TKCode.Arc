---
title: 多区域故障转移
summary: 全局负载均衡器将流量切换到备用区域，并在主区域故障时提升其数据库为主库。
source-hash: eb9336626322
---

<Demo name="AutomaticFailover" />

## 何时使用

- 在基础设施或可靠性页面上说明某个区域故障时服务如何保持可用。
- 架构文档需要展示哪个区域提供流量、哪个数据库为主库时。
- 团队正式执行前，演示故障转移流程。

## 何时不用

- 通知真实服务中断：用 `Alert` 并填写实际的状态文字。
- 只展示一个区域或一台服务器：故事需要两到三个区域，因此改用 `Card` 加纯文字。
- 报告真实的可用性或延迟随时间的变化：用 `LineChart` 或 `Gauge` 并填入实测值。

## 用法

默认情况下故事自动播放：某个区域未通过健康检查，流量与主数据库切换到备用区域，故障区域恢复后作为副本重新加入，流量最终回到原定区域。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiRegionFailover />
</UserControl>
```

### 区域

`Regions` 接受两到三个区域名称。默认值为 `us-east-1`、`eu-west-1` 和 `ap-southeast-1`。少于两个名称的列表会回退为默认值。

<Demo name="TwoRegions" />

### 自行驱动故障

将 `IsAutomatic` 设为 `False`，然后用区域索引调用 `FailRegion` 和 `RecoverRegion`。每次调用都会运行与自动故事相同的状态机。`PrimaryRegion` 是主数据库所在区域的索引，`TrafficRegion` 是接收流量的区域的索引。

<Demo name="DriveItYourself" />

```csharp
var failover = new MultiRegionFailover { IsAutomatic = false };
failover.PrimaryRegionChanged += (_, index) =>
    primaryLabel.Text = $"主库：{MultiRegionFailover.DefaultRegions[index]}";

failButton.Click += (_, _) => failover.FailRegion(0);
recoverButton.Click += (_, _) => failover.RecoverRegion(0);
```

提升完成后会以新主库的索引触发 `PrimaryRegionChanged`。让持有主库的区域故障即可触发该事件。

### 标签

`Labels` 接受一个 `MultiRegionFailoverLabels` 对象，包含图表中所有可见的文字：节点名称、健康状态、标示（`切换中`、`已升为主库`）和步骤说明文字。

```csharp
failover.Labels = new MultiRegionFailoverLabels
{
    Balancer = "全局负载均衡",
    FailingOver = "切换中",
    Promoted = "已升为主库"
};
```

### 播放

`IsPlaying`、`Speed` 和 `Restart()` 与其他插图一样，用于控制自动故事。故事共六步，`StepChanged` 会报告每一步。

```xml
<arc:MultiRegionFailover Speed="2" />
```

### 减弱动效

在减弱动效下，插图是健康部署的静态画面。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: MultiRegionFailover -->

<!-- api: MultiRegionFailoverLabels -->

## 无障碍

- `Label` 是无障碍名称，当前的说明文字作为帮助文本提供。
- 插图可获得焦点，焦点环宽 2px。
- 空格键或 Enter 播放与暂停故事，R 键从头开始播放。
- 说明文字的变化不会被播报。手动故障控件应为真正的按钮，并带有各自的名称，例如“让 us-east-1 故障”。

## 动效

- 故障区域先变为琥珀色，再变为红色，其传输中的数据包会被丢弃。
- 负载均衡器将流量切换到健康区域，并显示“切换中”标示以及切换耗时。
- 备用数据库以“已升为主库”标示被提升，复制从新主库开始。
- 恢复阶段显示该区域作为副本追赶数据，随后自动故事中会进行计划内的回切。
- 减弱动效和关闭动效时，显示健康部署的静态画面，备用路径以虚线绘制。播放按钮会隐藏。

## 相关组件

- `PaymentFlow`：依赖支付服务可用的单笔请求。
- `DataFlow`：将事件存入单一仓库的管线。
- `Alert`：以文字说明真实服务的状态。
