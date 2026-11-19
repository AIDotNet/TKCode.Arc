# StreamMark 记忆

本目录的索引：一个条目一行。依赖某个条目之前先打开它。
格式与规则见 [README.md](README.md)。个人记忆不放在这里。

## Project
- [调研五路并列，被否决的方案要留理由](research-decisions-recorded.md) — 为什么每个依赖都是「实测过的选择」，以及改选时该看哪里
- [平台只做桌面，移动端零投入](desktop-only-scope.md) — 别为 iOS / Android 加分支
- [强调扩展只启用删除线](strikethrough-only-emphasis.md) — 上标/下标/插入/高亮的误触发证据
- [remend 只在尾部补全，冻结片段与整篇只做转义修正](remend-tail-only.md) — 「完整文本不该被补全」的取舍
- [revert 掉 TCode 特有形状，那不算「StreamMark 不支持的 GFM」](tcode-shaped-revert.md) — 排查渲染异常前先看它
- [Svg.Skia 5.1.1 + 托管依赖 5.2.3，Svg.Animation 必须保持 5.1.1](svgia-version-pin.md) — 升级 Avalonia 之前别动
- [TextRunCache 的失效范围：从段落起点，不是差异点](textruncache-invalidation.md) — alpha.8/9 每次都变硬换行的原因
- [代码块分段的横向滚动各段独立是已知限制](chunk-horizontal-scroll-limit.md) — 别当成待修的 bug
- [主题要用资源别名而不是画刷](host-resource-alias.md) — 否则宿主 token 下文字透明
- [原生库有两条分发路径](native-library-two-paths.md) — 交叉发布时复制错架构的来历
- [Rust 1.100 起 win-arm64 链接缺 _Unwind_*，发布构建固定 Rust](rust-gnullvm-unwind.md) — 升级 Rust 前先编 win-arm64
- [RaTeX 的本地补丁应提交上游](ratex-fbox-upstream-patch.md) — 上游修复后要移除补丁并重新接线
- [StreamPacer.GetRevealTime 还没有生产消费者](pacer-reveal-time-unwired.md) — 排查渐显不生效时的第一个怀疑对象
- [CI runner 的字体环境与本机不同](ci-font-environments.md) — Linux 只有 DejaVu、macOS 苹方 600 加载成 Medium；字体断言按真实加载结果
- [回退字体曾依赖取 run 的起点与匹配历史](fallback-font-position-history.md) — Windows 上流式断行与整段排版不一致的原因（alpha.19 修复）
- [差异视图正在加入（2026-10-16）](diff-view-in-flight.md) — 当前在飞的特性，以及它会收紧哪些不变量
- [Mermaid 配色默认复刻 mermaid.js，宿主半透明色必须叠底色](mermaid-theme-mermaidjs.md) — 白底白字的来历、`DataPalette` 的蓝色陷阱、高图不再封顶 900px
- [LongCodeTests 的 20ms 计时断言本机偶发失败](longcode-jump-timing-flaky.md) — 全量里只有它失败时先单独重跑，别当成回归
- [表格操作按钮在网格外的操作条里](table-actions-outside-grid.md) — 复制按钮曾盖在表头上，负坐标 / Popup 点不中

## Reference
- [上游与依赖的出处、许可和调研材料](upstream-and-license-references.md) — 改依赖或许可文本之前先读它
