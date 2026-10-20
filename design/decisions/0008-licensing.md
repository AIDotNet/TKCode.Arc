# 0008 许可证：MIT

- 状态：已接受
- 日期：2026-10-20

## 背景

- Arc 免费组件以 MIT 许可发布：Copyright (c) 2026 Elia Kuratli。要求保留版权声明与许可文本。Pro 组件为商业许可，禁止重新分发其源码。
- AtomUI 以 LGPL-3.0 发布。链接其已发布的二进制可以商用；修改 AtomUI 源码则需开源或购买商业许可。
- Arc 的视觉依赖：
  - Lucide 图标（ISC 许可）；
  - Geist 字体（SIL OFL 1.1）；
  - Inter 字体（SIL OFL 1.1；Avalonia.Fonts.Inter 包内已包含）。

## 决策

1. TKCode.Arc 的源码与包以 **MIT** 许可发布。
2. 仓库根目录提供 `LICENSE`（MIT）与 `THIRD-PARTY-NOTICES.md`，后者至少包含：
   - Arc 的 MIT 版权声明与许可全文（TKCode.Arc 移植了其免费组件的设计与行为）；
   - Lucide 的 ISC 声明（图标包与 Core 内置字形）；
   - Geist 与 Inter 的 OFL 声明（字体包）；
   - 对 AtomUI（LGPL-3.0）与 Avalonia（MIT）的依赖说明。
3. NuGet 包携带 `LICENSE` 与对应的第三方声明。字体包与图标包各自携带其上游许可文件。
4. 不抓取、不复刻 Arc Pro 组件的源码、样式、文档或资源。

## 备选方案

- **LGPL-3.0（与 AtomUI 一致）**：与 AtomUI 生态一致；但对使用者的约束比 Arc 原版严格。未选。
- **暂不确定**：会让打包与声明工作悬而未决。未选。

## 后果

- 正面：许可最宽松，与 Arc 免费组件一致，便于社区采用。
- 负面：应用依赖链上仍有 AtomUI 的 LGPL 义务；Arc 的文档需要说明这一点。
- 配套：[许可与声明](../engineering/licensing.md) 维护声明清单与打包检查项。

## 关联文档

- [许可与声明](../engineering/licensing.md)
- [打包与发布](../engineering/packaging-and-release.md)
