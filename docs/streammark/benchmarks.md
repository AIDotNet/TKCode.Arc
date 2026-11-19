# 基准测试

`dotnet run -c Release --project tools/streammark/Benchmarks -- --filter '*' --job short`
（macOS arm64，.NET SDK 10.0.301，2026-10-16，BenchmarkDotNet ShortRun，误差较大，仅看量级）

## 流式解析：20KB 聊天式文档，按 4 字符一个 token 流完（约 5100 次更新）

| 方案 | 总耗时 | 总分配 |
|---|---:|---:|
| 每个 token 整篇重解析（remend + Markdig） | 1,270 ms | 1,854 MB |
| StreamMark（冻结前缀 + 只解析尾部，含最终整篇重解析） | 36 ms | 21 MB |

StreamMark 快约 35 倍，分配少约 87 倍。平均每次更新约 7µs。
调研中的"只解析尾部 5ms / 11MB"只计 Markdig 解析；这里还包含 remend、块模型映射、切分器和流结束时的整篇重解析。

实际使用时 `StreamPacer` 把提交频率限制在每帧最多一次（约 60 次/秒），远低于这里的每 token 一次。

## remend：约 58k 字符的单个尾部块

| 方法 | 平均耗时 | 分配 |
|---|---:|---:|
| `Remend.Complete` | 194 µs | 115 KB |

亚毫秒级，与 vercel 线性扫描器的结论一致（其 changelog：915ms → 0.4ms）。

## 视图层（无头 Skia，Debug 测试构建，`VirtualizationScaleTests`）

| 场景 | 结果 |
|---|---|
| 单条消息 10 万个顶层块：首次应用快照 + 布局 | 50 ms，实例化 ≤ 120 行 |
| 在 10 万块中任意跳转滚动（每次布局） | 约 7 ms |
| 10 万块之后继续流式追加（每帧应用 + 布局） | 约 3 ms |

## 扩展（NativeAOT，子代理实测）

| 扩展 | 结果 |
|---|---|
| TextMate：300 行 C# 逐行流式 | 约 88 µs / 行（JIT 约 136 µs） |
| TextMate：C# 语法冷启动 | 9–12 ms（新二进制首次启动约 386 ms，启动时后台预热） |
| Mermaid：5–6 节点流程图 | 约 1.2 ms（进程内首次渲染额外 50–90 ms） |
| RaTeX：排版 | 亚毫秒级，结果按 (latex, 显示模式) 缓存 |

## 第二阶段（无头 Skia，Debug 测试构建）

| 场景 | 结果 |
|---|---|
| 流式追加约 2700 字的段落（每次 12 字）的累计排版耗时 | 不用整形缓存 535 ms → 使用 `TextRunCache` 265 ms（约 2 倍；alpha.10 修正失效范围后重测。此前的 4.3 倍是错误失效的结果，见 decisions） |
| 3000 行代码块（24 段 × 128 行）在会话中任意跳转滚动 | 约 4–7 ms / 次，实例化 ≤ 12 行 |

## 流式 CPU（真实窗口，NativeAOT）

`tools/streammark/cpu-bench.sh osx-arm64`（`tools/streammark/CpuHarness`；MacBook Air M 系列，2560×1600，Metal，会话含 10 条历史消息，320 字/秒持续流式，预热 3 秒后统计 12 秒；数值是进程 CPU 占一个核心的百分比，误差约 ±1.5）

| 场景 | 改前（alpha.15） | 改后（alpha.16） |
|---|---:|---:|
| 空闲 | 0.0% | 0.0% |
| 聊天式文档 | 7.1–7.3% | 7.2–8.7% |
| 聊天式文档，关闭渐显 | 5.5% | 5.7–8.0% |
| 单段约 9KB 的长段落 | 34–39%（分配 36MB/s，12 秒 54 次 GC） | 8.4%（分配 4.2MB/s，8 次 GC） |
| 长段落，关闭渐显 | 21.4% | 6.1% |
| 400 行代码块 | 15.3% | 14.3% |
| 聊天式文档，区域脏矩形 | — | 7.1%（无明显收益，未启用） |

改前的长段落成本来自每次追加整段重排与渐显每帧两遍整段绘制；改后逐行排版、渐显在渲染线程，长短段落基本持平。剩余成本主要在渲染线程：贴底跟随时每出现新行整个视口重绘一次。

无头单测 `IncrementalLayoutTests.AppendCost_DoesNotGrowWithParagraphLength`：每次追加的排版中位数 1KB 段落 0.012ms、8KB 段落 0.015ms（整段重建时约为 3KB 0.6ms，随长度线性增长）。
