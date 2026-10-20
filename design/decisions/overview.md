# 架构决策记录（ADR）

> 记录 TKCode.Arc 的关键架构决策：背景、结论、备选方案与后果。所有决策均已由项目负责人确认（2026-10-20）。

## 1. 索引

| 编号 | 标题 | 状态 | 一句话结论 |
|---|---|---|---|
| [0001](0001-atomui-dependency.md) | AtomUI 依赖方式 | 已接受 | NuGet + CPM，AtomUI.* `[6.2.6]`、Avalonia* `[12.1.4]` 精确锁定 |
| [0002](0002-independent-controls.md) | 与 AtomUI 控件的关系 | 已接受 | 独立实现，派生自 Avalonia 基类，只用 AtomUI 公开基础设施 |
| [0003](0003-assemblies-and-packages.md) | 程序集与包划分 | 已接受 | Core + Arc + Icons.Lucide + Fonts，共享 Catalog `TKCode.Arc` |
| [0004](0004-naming-and-namespaces.md) | 命名与命名空间 | 已接受 | 无前缀、扁平 `TKCode.Arc`、语义部件 Path 以 `arc.` 开头 |
| [0005](0005-arcdesign-token.md) | 设计 Token 接入方式 | 已接受（待 S0.2 验证） | 载体控件 `ArcDesign` |
| [0006](0006-motion-engine.md) | 动效引擎 | 已接受（待 S0.6 / S0.7 验证） | 自研弹簧引擎 + 可声明的弹簧过渡 |
| [0007](0007-docs-before-code.md) | 文档节奏 | 已接受 | 架构先行，组件规格按阶段编写 |
| [0008](0008-licensing.md) | 许可证 | 已接受 | MIT + 第三方声明 |
| [0009](0009-tfm-and-sdk.md) | 目标框架与 SDK | 已接受（net11 待 S0.1 / S0.5 验证） | `net10.0;net11.0`，SDK 11 RC2 |
| [0010](0010-gallery-app.md) | Gallery 应用 | 已接受 | 自建轻量 Gallery |

## 2. 状态定义

| 状态 | 含义 |
|---|---|
| 提议 | 起草中，尚未确认 |
| 已接受 | 已确认，作为设计依据 |
| 已接受（待验证） | 已确认，但依赖 Phase 0 spike 的结果；spike 失败时重开该 ADR |
| 已替代 | 被新的 ADR 取代，正文顶部注明替代者 |

## 3. 模板

新增 ADR 时复制以下结构，编号递增，文件名为 `NNNN-kebab-title.md`：

```markdown
# NNNN 标题

- 状态：提议 / 已接受 / 已替代（由 NNNN 替代）
- 日期：YYYY-MM-DD

## 背景
为什么需要做这个决策；约束与已知事实（附源码或文档路径）。

## 决策
结论本身，必须可执行、可检查。

## 备选方案
每个备选方案一段：内容、优点、为什么没有选。

## 后果
正面与负面影响、需要的配套措施、验证方式。

## 关联文档
相对链接。
```

## 4. 修改规则

- 已接受的 ADR 不改写结论；结论变化时新建 ADR 并把旧 ADR 标为"已替代"。
- 可以补充"后果"与"关联文档"中的事实修正，并在文末注明日期。
