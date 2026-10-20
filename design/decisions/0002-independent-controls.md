# 0002 与 AtomUI 控件的关系：独立实现

- 状态：已接受
- 日期：2026-10-20

## 背景

- Arc 的视觉语言与 Ant Design 差异很大：
  - 中性色基调、大圆角（18 / 26 / 34px）、44px 默认控件高度。
  - 键盘焦点才显示焦点环；大量可打断的弹簧动效。
- AtomUI 已有许多同名或相近的控件（Button、Select、Slider、Calendar、DatePicker、Drawer、Dialog…），但复用存在硬限制：
  - 许多主题相关属性与部件是 `internal`。例如 `AbstractToggleSwitch.KnobRectProperty`、`AbstractSegmented.SelectedThumbBgProperty`、`WaveSpiritDecorator`、`InputControlFrame`、`FeedbackStack/*`。
  - `InternalsVisibleTo` 只开放给 AtomUI 自己的 Extras / DataGrid / ColorPicker 与测试程序集。
  - Token 按控件的精确类型解析，未注册的子类会抛异常；own token 不沿控件继承链继承。
  - AtomUI 已导出的默认主题 key（如 `{x:Type atom:Button}`）不能被再次导出，冻结时会报冲突。
  - AtomUI 控件的代码逻辑与其模板强绑定：波纹、变体画刷计算、固定时长动画。
- AtomUI 的公开基础设施可以直接复用：
  - 主题管理与 Token 体系、包注册、MotionScene。
  - `Popup`、`ScopeAwareOverlayLayer` / `ScopeAwareAdornerLayer`。
  - `Icon`、`ImageLoader` / `AsyncImage`、本地化。

## 决策

1. Arc 控件派生自 **Avalonia** 基类，如 `Button`、`ToggleButton`、`TemplatedControl`、`ContentControl`、`ItemsControl`、`SelectingItemsControl`、`RangeBase`、`TextBox`、`ScrollViewer`、`TreeView`。
   - 模板、Token 与动效全部由 Arc 定义。
   - Arc 控件之间可以继承，例如 `ActionButton : TKCode.Arc.Button`；子类仍需自己的 Token 与主题。
2. 只使用 AtomUI 的**公开** API，白名单与黑名单维护在 [AtomUI 集成](../architecture/atomui-integration.md) 中。
3. AtomUI 的同类控件只作为行为与边界的学习参考，不继承、不包装、不在模板里嵌套使用。
4. 复杂行为（日历计算、颜色空间换算、树形导航等）在 Arc 内自行实现，或复用 Avalonia 的非视觉逻辑。

## 备选方案

- **继承 AtomUI 控件并替换主题**（如 `Switch : ToggleSwitch`）：被 internal 属性、精确类型 Token 解析和默认主题 key 冲突阻断，而且会把 Ant 的 API 与动画逻辑带进 Arc。未选。
- **在 Arc 控件内部包裹 AtomUI 控件**，只用公开的 Semantic Part Style 微调：开发快，但视觉与动效达不到 Arc 原版的程度，嵌套控件的焦点与键盘行为也难统一。未选。

## 后果

- 正面：
  - Arc 的视觉与动效完全可控，不受 AtomUI 内部重构影响。
  - API 按 Arc 的语义设计，没有 Ant 的属性泄漏。
- 负面：
  - 工作量更大：日期、颜色、树、表格等复杂控件要在 Arc 内重新实现。
  - 无法加入 AtomUI 的 `CompactSpace`，因为 `ICompactSpaceAware` 是 internal。
- 配套：
  - 每个组件规格中的"AtomUI 参照"一栏只写学习要点。
  - [编码规范](../engineering/coding-guidelines.md) 禁止引用 AtomUI 内部类型或通过反射访问 AtomUI 的 internal 成员。

## 关联文档

- [AtomUI 集成](../architecture/atomui-integration.md)
- [组件总清单](../components/overview.md)
