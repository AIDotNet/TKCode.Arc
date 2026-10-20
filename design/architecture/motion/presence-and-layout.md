# Presence 与布局动画

> 退场保活（Presence）、列表进出与重排（FLIP）、共享元素飞行、尺寸弹簧四类编排服务。命名空间 `TKCode.Arc.Motion.Presence` 与 `TKCode.Arc.Motion.Layout`。

## 1. Presence（S3）

对应 Motion 的 `AnimatePresence`：元素被移除时，先播放退场动画，结束后才真正从可视树移除。

### 1.1 类型

| 类型 | 用途 |
|---|---|
| `PresenceHost` | 单内容宿主：`Content` 改变或置空时，旧内容播放退场，新内容播放进场（交叉淡化、模糊切换、方向滚动） |
| `PresenceItemsControl` | 列表宿主：维护一份包含"正在退场项"的显示列表，退场结束后才移除 |
| `PresenceTransition`（抽象） | 描述进场 / 退场：`Enter(Visual, MotionLevel)`、`Exit(Visual, MotionLevel)`，返回 Task |
| 内置转场 | `FadeTransition`、`BlurSwapTransition`（P2）、`DirectionalRollTransition`（P3）、`PopInTransition`（P26）、`DirectionalSlideTransition`（P25）、`AnchoredScaleTransition`（P10） |

### 1.2 规则

- **初始不动画**：首次显示的内容直接呈现，对应 Motion 的 `initial={false}`。
- **退场快于进场**：退场默认 `DurationExit` + `EaseExit`；进场用 `DurationStandard` + `EaseEnter`，或对应的弹簧。
- **退出脱离布局**：模糊切换中退场的旧内容不再占据布局宽度（Arc：the outgoing element is popped out of flow），新内容决定宿主尺寸；宽度变化交给 P5。
- **可打断**：退场中的元素若被重新加入，从当前状态反向进场，不重新创建。
- **键稳定**：列表项按数据项身份（`IItemKey` 或引用）跟踪，不按索引跟踪。
- **焦点**：退场中的元素不可聚焦、不可命中；若它持有焦点，焦点先移到新内容或宿主。

## 2. FLIP 布局动画（LayoutAnimator）

对应 Motion 的 `layout` / `layout="position"`：位置变化由布局引起时，用变换平滑过渡。

### 2.1 流程

1. **First**：布局变化前，记录被跟踪子元素相对容器的位置（`TranslatePoint`），尺寸可选。
2. **Last**：布局完成后（容器的 `ArrangeOverride` 末尾，或 `LayoutUpdated`），读取新位置。
3. **Invert**：对每个元素施加反向位移（RenderTransform），让它看起来还在原处。
4. **Play**：用 `MotionValue<Transform2D>` 把反向位移弹回恒等变换（默认 morph）。

### 2.2 规则

- 默认只动画位置（Arc 多数场景是 `layout="position"`）。需要尺寸时启用缩放修正，并对子内容做反向缩放，避免文字被拉伸。
- 被动的尺寸变化（窗口缩放、字体加载）不触发 FLIP，只有数据变化（增删、排序、展开）触发。这与 Arc 的"只在内容变化后 700ms 内动画"规则一致。
- 与 `PresenceItemsControl` 配合：新增项用 P26 弹出或淡入，删除项退场，其余项用 FLIP 补位（P19）。
- 交错：列表进场按 `StaggerItem` 依次延迟，总时长不超过约 0.4s；超过的项不再延迟。

## 3. 共享元素（SharedElementScope）

对应 Motion 的 `layoutId`：同一作用域内，带相同 `MotionId` 的元素在一处消失、另一处出现时，视觉上从旧位置飞到新位置。

```xml
<arc:SharedElementScope>
  <!-- 列表中的卡片缩略图 -->
  <Border arc:SharedElement.MotionId="{Binding Id}" />
  <!-- 详情面板中的大图（出现时从缩略图位置飞入） -->
</arc:SharedElementScope>
```

- 作用域记录最近消失元素的位置与尺寸。同 id 元素出现后，以旧位置为起点，用 `MotionValue<Rect>` 动画到新位置；按尺寸比例变换内容，结束后清除变换。
- 用于 P8：MorphSelect 的选中项飞入触发器、DateRangePicker 的标签飞入触发器、Card 快速预览、ChatThread 的已读头像、CardStack 撤销。
- 列表内单一高亮在项之间滑动（P7）**不使用**共享元素，而是由一个独立的指示器元素在容器内移动（见 [原语目录](primitives-catalog.md) 的 GlidingIndicator），实现更简单、开销更小。

## 4. 尺寸弹簧（SpringSizeDecorator）

对应 Arc 的"宽度随内容弹簧"（P5）与"高度展开收起"（P6）。

```xml
<arc:SpringSizeDecorator Axis="Width" Spring="Morph" AnimateOn="ContentChange">
  <TextBlock Text="{Binding Label}" />
</arc:SpringSizeDecorator>
```

| 属性 | 说明 |
|---|---|
| `Axis` | `Width` / `Height` / `Both` |
| `Spring` | 增长用 morph；收缩可单独指定（如 ConfirmMorph 收缩用 smooth） |
| `AnimateOn` | `ContentChange`（默认，内容变化后 700ms 内的尺寸变化才动画，其余直接跳变）或 `Always` |
| `ClipToBounds` | 动画期间裁剪内容 |
| `IsExpanded` | 高度模式下用于展开收起：从 0 到内容的期望高度；内容按最终宽度布局 |

规则：

- 每帧在 `MeasureOverride` 中返回当前动画尺寸，会触发布局。只在"尺寸就是信息"时使用（Arc 规则 1）。
- 高度展开时，内容可选地从 6–8px 位移与 subtle 模糊中进入；折叠用 smooth（常用 0.28–0.36s）或 `DurationExit` + `EaseExit`。
- 中途反向（展开中又折叠）从当前高度与速度继续。
- 等级为 Reduced 或 Off 时直接跳到终态尺寸；Reduced 下内容用短淡入。

## 5. 与其他服务的关系

| 服务 | 依赖 |
|---|---|
| `PresenceHost` / `PresenceItemsControl` | `MotionValue<T>`、动效等级 |
| `LayoutAnimator` | `MotionValue<Transform2D>`、S5 尺寸观察（判断被动尺寸变化） |
| `SharedElementScope` | `MotionValue<Rect>`、浮层（跨容器飞行时需要在顶层绘制） |
| `SpringSizeDecorator` | `MotionValue<double>`、S5 尺寸观察 |
