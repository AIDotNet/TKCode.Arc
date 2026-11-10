---
title: Activity rings
summary: 表示每日目标的同心圆环。每个圆环随弹簧填满，到达目标时合上，超过目标后继续绕圈；旁边的图例数字会滚动。
source-hash: c09dc14af601
---

<Demo name="Preview" />

## 何时使用

`ActivityRings` 用一组同心圆环同时展示几个每日目标，最外圈排在第一个。圆环从 12 点方向顺时针填充，达到目标时合上；超过目标后继续绕圈，新的一圈压在第一圈上面，头部下方带一点阴影，所以“超额完成”一眼就能看出。数值变化时圆环从当前位置随弹簧继续走，旁边的图例写出每个目标的名称，并滚动显示数值。

- **每日目标**：步数、运动分钟、站立小时、喝水、阅读页数。
- **连续打卡与习惯页面**：一排小圆环就能看完一周。
- **可以超额的指标**：常常被超过的销售或学习目标，超出的部分也有意义。

## 何时不用

- **需要精确比较**：圆环靠感觉读；需要比较数值时请用柱状图。
- **超过四个目标**：内圈会变小，不好区分。请用一列进度条。
- **只有一个数值**：普通的进度环或 `Gauge` 更简单。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActivityRings Label="Today's goals">
        <arc:ActivityRing Label="Move" Value="380" Goal="520" Unit="kcal" />
        <arc:ActivityRing Label="Exercise" Value="22" Goal="30" Unit="min" />
        <arc:ActivityRing Label="Stand" Value="9" Goal="12" Unit="hr" />
    </arc:ActivityRings>
</UserControl>
```

`ActivityRings` 在 Special 包中；请在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。每个目标放一个 `ActivityRing`。`Value` 达到 `Goal` 时圆环填满，`Progress` 表示绕了多少（1.4 表示满一圈再多 40%）。没有设置 `Brush` 的圆环按顺序使用主题的系列颜色。

### 超过目标

超过目标后圆环继续绕圈，最多四圈。上面那一圈在头部前方投下一点阴影，所以即使颜色相同也能看出重叠。

<Demo name="Overflow" />

### 不带图例的小圆环

把 `IsLegendVisible` 设为 false，再给较小的 `Diameter` 和 `RingThickness`，就是一个紧凑视图，例如一周的每一天。圆环显示之后，某个数值达到目标时会触发 `GoalReached`；`Replay()` 让圆环从空重新填满。

<Demo name="Week" />

### 最佳实践

**建议**

- 只放两到四个目标，名称要短，比如“Move”。
- 给 `Label` 一个整体名称，比如“Today's goals”：辅助技术会先读它。
- 使用 `Unit`，让图例读作“380 / 520 kcal”。

**避免**

- 给没有自然目标的数值用圆环。
- 使用相近的颜色：相邻的圆环必须容易区分。

## API 参考

<!-- api: ActivityRings -->

<!-- api: ActivityRing -->

## 无障碍

- 整组圆环是一个元素，名称来自 `Label`。它的帮助文本会读出每个目标：“Move: 380 of 520 kcal, 73 percent.”
- 图例行只给眼睛看，不会一行一行地读。
- 某个圆环达到目标时，一条礼貌的状态信息会说明，例如“Move goal reached”（`GoalReachedText`）。
- 圆环不可交互，也不获取焦点。

## 动效

- 第一次显示时，圆环从空开始依次填满，最外圈最先，每圈间隔 90 ms；弹簧大约 0.9 s 停稳，带一点回弹。图例数字同时向上滚动。
- 之后的变化从圆环当前位置随弹簧继续走；中途再次变化时保留速度。
- 只有弹簧运动时才请求帧；圆弧原地更新，每帧不分配内存。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 第一次显示 | 圆环依次填满 | 直接显示数值 | 直接显示数值 |
| 新数值 | 随弹簧到新数值 | 直接跳到 | 直接跳到 |
| 图例数字 | 滚动 | 直接跳到 | 直接跳到 |

## 响应式

- 空间足够时圆环使用 `Diameter`，空间较小时缩小以适应；圆环粗细不变。
- 宽度小于 340 px（`SideLegendMinWidth`）时图例移到圆环下方，大于时放在旁边。
- 图例中的名称过长时以省略号结束，不换行。

## 相关组件

- `Gauge` 和 `DonutChart` 展示一个数值或整体中的占比。
- `UsageMeter` 是带上限的额度条。

| 组件 | 适用场景 |
|---|---|
| `ActivityRings` | 几个每日目标，超出目标也算数 |
| `Gauge` | 刻度上的一个数值 |
| `UsageMeter` | 一个有硬上限的额度 |
