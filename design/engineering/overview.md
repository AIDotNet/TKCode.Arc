# 工程规范总览

> TKCode.Arc 的编码、测试、Gallery、打包发布、许可与文档校验规范。

| 文档 | 内容 |
|---|---|
| [coding-guidelines.md](coding-guidelines.md) | 继承自 AtomUI 的编码规则与 Arc 的补充规则 |
| [testing.md](testing.md) | 测试分层、Headless 环境、手动帧时钟、测试去留标准 |
| [gallery-app.md](gallery-app.md) | 自建 Gallery 的结构、导航、展示页模式与工具栏 |
| [packaging-and-release.md](packaging-and-release.md) | 包清单、版本与 AtomUI 版本锁、打包内容、消费验证、发布节奏 |
| [licensing.md](licensing.md) | 许可证与第三方声明清单 |
| [docs-verification.md](docs-verification.md) | 设计文档的校验规则与执行方式 |
| [reserved-token-names.md](reserved-token-names.md) | 不可用作 own token 名的 255 个 AtomUI 全局 Token |
| [implementation-guide.md](implementation-guide.md) | 组件实现手册：文件、Token、主题、动效、基元、性能、易用性、测试、已知坑 |

## 协作约定

- 不经明确指令不提交代码（与 AtomUI 仓库的"提交审查强约束"一致）：完成一个任务后先交审查。
- 设计变化先改文档（必要时新增 ADR），再改代码（[ADR 0007](../decisions/0007-docs-before-code.md)）。
- 仓库根目录的 `AGENTS.md` 只放入口与不可协商的约定，细则链接到本目录与 `design/`。
