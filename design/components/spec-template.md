# 组件规格模板

> 每个组件在其所属阶段开始前，按本模板编写详细规格：`design/components/<大类目录>/<kebab-组件名>.md`，例如 `design/components/toggles/switch.md`。规格评审通过后才开始写代码（[ADR 0007](../decisions/0007-docs-before-code.md)）。

## 使用说明

- 复制下面"模板正文"中的全部内容，替换尖括号里的占位。
- 信息来源：Arc 组件 markdown、注册表源码（TSX + CSS module）、catalog.json；不得参考 Pro 组件。
- 来源冲突时**以注册表源码为准**，并在规格中注明差异。已知的例子：DatePicker 选择后的关闭延迟，文档为 240ms，源码为 300ms；Popover 的退场，文档为 140ms，源码为 fast（160ms）；Tooltip 的 `side`，文档只写 top / bottom，源码也支持 left / right；Calendar 的行数，文档描述 4 / 5 / 6 行高度变化，源码为固定 6 行。
- 数值写到具体数字，并注明来源（例如"button.module.css：padding-inline 16px"）。
- 与架构文档冲突时，先改架构文档或新增 ADR，再写规格。
- 单个规格超过约 400 行时，按职责拆分（例如 `calendar.md` 与 `calendar-keyboard.md`），由主文件链接。

---

## 模板正文

```markdown
# <类名>（<Arc 标题>）

> 一句话定位。

## 1. 元数据

| 项 | 值 |
|---|---|
| 类名 | `TKCode.Arc.<类名>` |
| Arc 组件 | [<Arc 标题>](https://uiarc.dev/components/<slug>)（slug：`<slug>`） |
| 大类 / 代码目录 | <中文大类>（`src/TKCode.Arc/<大类>/<类名>/`） |
| 阶段 | P<n> |
| Arc 依赖 | <无 / 组件列表> |
| 动效原语 | <P 编号列表> |
| 基础服务 | <S 编号列表> |
| 基类 | <确定的 Avalonia 基类或 Arc 基类> |
| AtomUI 参照（仅学习） | <控件名与学习要点> |
| Since | <Arc 版本号> |
| 状态 | 规格草稿 / 规格已评审 |

## 2. Arc 行为摘要
- 适用场景（When to use）。
- 不适用场景（When not to use）及替代组件。
- 关键交互与视觉特征。

## 3. 范围
| 项 | 是否移植 | 说明 |
|---|---|---|
| <Arc 功能> | 是 / 否 / 调整 | <原因，例如桌面端无触觉反馈> |

## 4. 公共 API

### 4.1 属性
| 名称 | 类型 | 默认值 | 说明 | Arc 对应 |
|---|---|---|---|---|
| `<Name>` | `<Type>` | `<Default>` | <说明> | `<arcProp>` |

### 4.2 事件与命令
| 名称 | 类型 | 触发时机 | Arc 对应 |
|---|---|---|---|

### 4.3 附加属性与辅助类型
（枚举、事件参数、数据模型）

## 5. 模板与组成
- 模板结构草图（树形）。
- 模板部件（`PART_*`）及代码中的用途。
- 内部 presenter 及其主题文件。
- 共享基元（来自 Core 或 Arc `Primitives/`）。

## 6. 状态与伪类
| 伪类 / 状态 | 触发条件 | 视觉变化 |
|---|---|---|

## 7. 视觉与 Token 映射
| 部件 | 状态 | 属性 | Token（ArcDesign / 控件） | 浅色 / 深色说明 |
|---|---|---|---|---|
- 尺寸、间距、圆角、字体与 Arc CSS 的对应关系（注明来源）。

## 8. 动效
| 触发 | 目标属性 | 规格（弹簧 / 补间 + Token） | 原语 | Reduced | Off |
|---|---|---|---|---|---|
- 首帧不动画的处理。
- 中途打断与重定向的行为。
- 动效写在 ControlTheme 还是代码中；写在代码中时说明 ControlTheme 不可行的原因。

## 9. 控件 Token（`<类名>Token`）
| 属性 | 类型 | 默认值 | 来源 |
|---|---|---|---|
（没有专属值时写"空 Token，仅用于 identity"）

## 10. 语义部件
| Path（`arc.*`） | SelectorClass | ContractType | 用途 |
|---|---|---|---|
（不公开定制部件时写"无"）

## 11. 键盘交互
| 按键 | 行为 |
|---|---|

## 12. 可访问性
- AutomationPeer 类型与控件模式。
- `AutomationProperties.Name` / `HelpText` / `LiveSetting` 的来源。
- 焦点管理（焦点环、焦点陷阱、焦点归还）。

## 13. 本地化
| 文案 id | en-US | zh-CN | zh-TW |
|---|---|---|---|

## 14. 性能与 AOT
- 动效预算（逐帧布局、活动动效值数量）。
- 大数据量策略（虚拟化、分页）。
- AOT：无反射、显式注册、无运行时 AXAML。

## 15. 测试计划
| 类型 | 内容 | 去留 |
|---|---|---|
| 行为测试 | <断言> | 临时 / 常驻（说明理由） |
| 主题契约 | <断言> | |
| 动效确定性 | <ManualFrameClock 场景> | |
| 视觉对照 | <与 Arc 截图或录屏对比的场景> | 验收用 |

## 16. Gallery 用例
- <示例 1：标题与内容>
- <示例 2>

## 17. 未决问题
| 问题 | 选项 | 建议 | 决定 |
|---|---|---|---|

## 18. 修订记录
| 日期 | 修改 |
|---|---|
```

---

## 评审清单

- [ ] 元数据与 [组件总清单](overview.md) 一致：类名、阶段、依赖、原语。
- [ ] 每条动效都写明 Reduced 与 Off 行为，并符合 [减少动效](../architecture/motion/reduced-motion.md) 的类别策略。
- [ ] 颜色、阴影、圆角只引用 ArcDesign Token；控件 Token 中只有与 accent 无关的派生色，依赖 accent 的派生色已加入 ArcDesignToken。
- [ ] 存在 `<类名>Token`；语义部件 Path 以 `arc.` 开头。
- [ ] 键盘交互覆盖 Arc 文档的 Keyboard interactions 一节。
- [ ] 新增文案提供 en-US、zh-CN、zh-TW。
- [ ] 测试计划写明每个测试的去留理由（[测试规范](../engineering/testing.md)）。
- [ ] Gallery 用例覆盖主要状态与明暗、accent、动效等级的切换。
