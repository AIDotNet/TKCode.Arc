---
name: svgia-version-pin
description: Svg.Skia 钉在 5.1.1，托管依赖覆盖到 5.2.3，Svg.Animation 必须保持 5.1.1
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark.Mermaid/StreamMark.Mermaid.csproj, Directory.Packages.props, docs/streammark/decisions.md]
---
`StreamMark.Mermaid` 用 `VersionOverride` 把 Svg.Skia 钉在 5.1.1，同时把 Svg.Model / Svg.Custom / ShimSkiaSharp / Svg.SceneGraph 覆盖到 5.2.3，`Svg.Animation` 例外地保持 5.1.1。

**Why:** 5.1.1 与 Avalonia 12.1 使用同一个 SkiaSharp 3 原生库，而 5.2.x 的 Svg.Skia 需要 SkiaSharp 4；Svg.Model 5.1.1 还会产生两个 IL2026。5.2.x 的 Svg.Animation 依赖 SkiaSharp 4，所以不能跟着升。这一点是 CI 的 Linux 任务发现的。
**How to apply:** 不要为了「版本统一」删掉这些 override；Avalonia 升级到 SkiaSharp 4 之后才能一起升到 5.2+，那时也要撤掉 Svg.Animation 的例外。
