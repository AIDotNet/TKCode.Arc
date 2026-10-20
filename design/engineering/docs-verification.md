# 文档校验

> 设计文档的一致性规则与校验方式。规则由只读脚本自动检查；脚本不能覆盖的内容由人工通读检查。

## 1. 执行方式

- 脚本：`tools/dev/verify_design_docs.py`，用法 `python3 -I tools/dev/verify_design_docs.py design <AtomUI 源码目录>`，只依赖 Python 3 标准库，只读，不修改任何文件。退出码 0 表示无错误（允许警告），1 表示有错误。
- 当前位置：文档期在临时目录中运行。Phase 0 搭建工程时，把它提交为 `scripts/verify-design-docs.py`，并接入 CI 的 docs 作业（见 [目标框架](../architecture/target-frameworks.md#6-ci-要求)）。
- 运行时机：
  - 修改组件总清单、路线图、原语目录或 Token 目录之后；
  - 新增组件规格之后；
  - 每个阶段结束前。

## 2. 自动检查项

| 编号 | 检查 | 规则 |
|---|---|---|
| 1 | 清单 | [组件总清单](../components/overview.md) 恰好 110 行；类名与 slug 唯一；各大类计数按顺序为 10、3、3、7、7、5、6、2、5、3、3、5、6、5、5、3、3、13、6、3、3、4 |
| 2 | 阶段 | 各阶段计数为 2、6、7、19、8、13、17、17、12、9；每个硬依赖都早于依赖方；[路线图](../roadmap/overview.md) 阶段表中列出的组件集合与清单一致 |
| 3 | 原语 | [原语目录](../architecture/motion/primitives-catalog.md) 包含 P1–P30 与 S1–S12；每个原语的使用组件数与落地阶段，和清单"原语"列计算出的结果一致 |
| 4 | Token | [Token 目录](../architecture/theming/token-catalog.md) 中的名称唯一，且不与 AtomUI 全局 Token 重名（从 AtomUI 源码 `src/AtomUI.Core/Theme/DesignTokens/Definitions/` 解析） |
| 5 | 链接 | 所有相对链接指向存在的文件；带锚点的链接指向存在的标题（按 GitHub 的标题锚点规则计算） |
| 6 | 结构 | 每个目录都有 `overview.md`，没有 `index.md`；每个 `overview.md` 都链接了同目录的所有文档与子目录的 overview |
| 7 | 长度 | 单个文件超过 400 行给出警告，提示按职责拆分 |
| 8 | ADR | 每份 ADR 都有状态、日期、背景、决策、备选方案、后果，并列在 [决策索引](../decisions/overview.md) 中 |
| 9 | 大类 overview | 22 份大类 overview 都存在；每个组件都有 `### 4.x 类名（Arc 标题）` 小节，元数据行的 slug 与阶段和清单一致；没有清单之外的组件 |

## 3. 人工检查项

| 检查 | 关注点 |
|---|---|
| 关键约束一致 | `arc.` 语义部件路径、每个控件都有 `XxxToken`、accent 覆盖不写 Algorithm、版本精确锁定、Browser 不在范围内：这些在各文档中的表述一致 |
| 数值来源 | 规格中的尺寸、时长、阈值能追溯到 Arc 的文档或源码 |
| Pro 边界 | 没有引用 Pro 组件的源码或文档内容 |
| 术语 | 与 [术语表](../glossary.md) 一致 |
| 过时结论 | Phase 0 spike 的结论已回写到受影响的 ADR 与架构文档 |

## 4. 组件规格的额外检查（规格开始编写后启用）

- 规格文件位于 `design/components/<大类目录>/<kebab-组件名>.md`，并从大类 overview 链接。
- 元数据（类名、阶段、依赖、原语）与清单一致。
- 动效表中每条都有 Reduced 与 Off 列。
