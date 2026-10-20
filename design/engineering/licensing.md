# 许可与声明

> TKCode.Arc 的许可证与第三方声明清单，以及打包、评审时的检查项。决策见 [ADR 0008](../decisions/0008-licensing.md)。

## 1. 本项目

- 许可证：MIT。
- 仓库根目录的 `LICENSE` 为 MIT 全文，版权人在首次发布前确定。

## 2. 第三方清单

| 来源 | 许可 | 使用方式 | 声明位置 |
|---|---|---|---|
| Arc 免费组件（uiarc.dev） | MIT，Copyright (c) 2026 Elia Kuratli | 移植组件的设计、行为与少量数据（如国家电话格式、密码强度规则） | `THIRD-PARTY-NOTICES.md` 全文；`TKCode.Arc`、`TKCode.Arc.Core` 包 |
| Lucide | ISC | 图标几何数据 | `THIRD-PARTY-NOTICES.md`；`TKCode.Arc.Icons.Lucide` 与 `TKCode.Arc.Core`（内置字形）包 |
| Geist | SIL OFL 1.1 | 字体文件 | `THIRD-PARTY-NOTICES.md`；`TKCode.Arc.Fonts` 包内携带 OFL 全文 |
| Inter | SIL OFL 1.1 | 通过 `Avalonia.Fonts.Inter` 依赖使用，不重新分发 | `THIRD-PARTY-NOTICES.md` 中说明 |
| AtomUI | LGPL-3.0 | NuGet 依赖，链接已发布的二进制 | README 与 `THIRD-PARTY-NOTICES.md` 中说明依赖关系与应用侧义务 |
| Avalonia | MIT | NuGet 依赖 | `THIRD-PARTY-NOTICES.md` 中说明 |

## 3. Arc Pro 的边界

- 不抓取、不复刻 Arc Pro 组件的源码、样式、文档与资源。Pro 组件清单见 [站点快照](../components/arc-catalog-snapshot.md#2-侧边栏统计)。
- 规格编写与代码评审时，确认参考来源都是免费组件的公开页面或注册表。
- 即使 Pro 组件与某个免费组件相似（例如 Presence stack 与 AvatarGroup），也只按免费组件的公开资料实现。

## 4. 检查项

| 时机 | 检查 |
|---|---|
| 新增依赖 | 在第 2 节登记许可证与声明位置 |
| 移植 Arc 数据或算法 | 文件头注明来源组件 |
| 打包 | 每个包都携带 `LICENSE` 与对应的第三方声明；字体包携带 OFL 全文 |
| 发布 | README 说明 AtomUI 的 LGPL 依赖 |
