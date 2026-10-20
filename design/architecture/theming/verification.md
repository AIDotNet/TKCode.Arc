# Token 验证

> Token 体系的契约测试、命名冲突检查与视觉对照方法。临时 / 常驻测试的划分遵循 [测试规范](../../engineering/testing.md)。

## 1. 单元测试（Core.Tests，常驻）

| 测试 | 断言 |
|---|---|
| 预设精确匹配 | 8 个预设 seed 在明 / 暗下的全部输出与 [accent 与调色板](accent-and-palette.md) 的表格逐项相等 |
| 暗色派生匹配 | 每个预设 seed 经 `PaletteGenerator` 暗色派生后，`ArcPalette.Resolve` 仍得到该预设 |
| 默认主色映射 | `#1677FF` 及其暗色派生值解析为 neutral |
| OKLCH 推导 | 对 8 个预设使用推导公式（跳过预设表），结果与手调值的 ΔE_OK ≤ 0.03（S0.2 校准完成后启用） |
| 对比度 | 8 个预设 + 64 个随机 seed：AccentForeground / Accent ≥ 4.5，ControlGlyph / ControlOn ≥ 3 |
| series 公式 | 7 个彩色预设的 Series1–4 与参考表一致（允许 ±1 级 sRGB 差异） |
| 色域映射 | 高色度 seed 转换不产生越界分量，结果稳定（相同输入相同输出） |

## 2. 契约测试（Arc.Tests，常驻）

| 测试 | 断言 |
|---|---|
| 命名不冲突 | `ArcDesignToken` 与所有 `XxxToken` 的属性名都不在 AtomUI 6.2.6 的 `SharedTokenKind` 中（从 `AtomUI.Theme.Resources.SharedTokenKind` 枚举读取） |
| 类型允许 | 所有 own token 属性类型都在内置转换器列表中 |
| identity 完整 | `TKCode.Arc` 程序集中每个公开控件类型都有同命名空间的 `XxxToken` |
| 短名唯一 | Core 与 Arc 的控件短名不重复 |
| 主题引用 | 主题文件中引用 Arc Token 的标记扩展只使用 `arc:` 前缀（扫描 `Themes/**/*.axaml`） |
| 明暗刷新 | 在 Headless 应用中切换外观后，`{arc:ArcDesignTokenResource Background}` 的值变化 |
| 作用域 | `ThemeConfigProvider` 子树内外的 `Accent` 不同 |

## 3. Spike（S0.2，临时）

S0.2 的完整通过标准见 [Phase 0](../../roadmap/phase-0.md)。spike 结束后：

- 能独立发现回归的断言并入第 2 节的常驻测试；
- 其余删除，并记录结论。

## 4. 视觉对照

- 参照物：Arc 网站在 neutral 与 violet、明与暗四种组合下的组件截图，例如 Button、Switch、Card、Tooltip。
- 方法：Gallery 中同尺寸渲染，与截图做像素差（容差在 S0.2 中确定）。只用于验收，不作为常驻测试。
- 每次升级 AtomUI 版本时重跑第 1、2 节测试与 S0.2 的关键断言。
