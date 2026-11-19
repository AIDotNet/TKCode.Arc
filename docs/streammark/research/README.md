# 调研报告

2026-10-16 构建 StreamMark 之前做的五路调研，结论汇总在 [../decisions.md](../decisions.md)。
报告保留原文；文中的 `<research-scratch>/` 指调研时的临时克隆与基准目录，不在本仓库内。

| 报告 | 内容 |
|---|---|
| [01-streamdown.md](01-streamdown.md) | vercel/streamdown（remend、块切分、记忆化、Shiki、动画）与 @lobehub/streamdown（冻结尾部、平滑、渐显），以及 smd、Incremark |
| [02-dotnet-parsing.md](02-dotnet-parsing.md) | Markdig 1.4 的 AST、扩展、AOT 实测与流式陷阱；TextMateSharp 增量高亮实测 |
| [03-avalonia-rendering.md](03-avalonia-rendering.md) | 现有 Avalonia Markdown 库、Avalonia 12 文本栈成本、虚拟化、渲染管线、AOT 与主题 |
| [04-math-mermaid.md](04-math-mermaid.md) | CSharpMath / XAML-Math / RaTeX / MicroTeX / Jint 对比实测；Mermaider、merman |
| [05-native-streaming.md](05-native-streaming.md) | Zed、Codex CLI、Gemini CLI、JetBrains、gpt_markdown、Textual 的流式渲染做法与节奏控制算法 |
