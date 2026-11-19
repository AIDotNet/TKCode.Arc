# StreamMark 演示

StreamMark 是面向 AI 对话的**高性能流式 Markdown** 渲染库：*原生绘制*、`NativeAOT`、~~WebView~~ 不需要。

## 数学公式

行内公式 $E = mc^2$，反斜杠写法 \(\alpha + \beta = \gamma\)，以及块级公式：

$$
\int_{-\infty}^{\infty} e^{-x^2}\,dx = \sqrt{\pi}
$$

\[
\begin{aligned}
\nabla \cdot \mathbf{E} &= \frac{\rho}{\varepsilon_0} \\
\nabla \times \mathbf{B} &= \mu_0 \mathbf{J} + \mu_0 \varepsilon_0 \frac{\partial \mathbf{E}}{\partial t}
\end{aligned}
\]

化学方程式：$\ce{2H2 + O2 -> 2H2O}$

## 代码

```csharp
public static async Task<int> CountAsync(IAsyncEnumerable<string> lines)
{
    var count = 0;
    await foreach (var line in lines)
    {
        if (!string.IsNullOrWhiteSpace(line)) count++;
    }
    return count;
}
```

```python
def fib(n: int) -> int:
    a, b = 0, 1
    for _ in range(n):
        a, b = b, a + b
    return a
```

## 图表

```mermaid
flowchart LR
    A[网络 token] --> B[节奏控制]
    B --> C[流式文档]
    C --> D{可以冻结?}
    D -->|是| E[冻结片段]
    D -->|否| F[尾部重解析]
```

## 表格与列表

| 能力 | 状态 | 备注 |
|:-----|:----:|-----:|
| 流式解析 | ✅ | 冻结前缀 + 尾部 |
| 公式 | ✅ | RaTeX |
| 图表 | ✅ | Mermaid |

- [x] 跨消息选区
- [x] 虚拟化长会话
- [ ] 更多主题

> [!TIP]
> 选中任意文本后按 ⌘C 复制纯文本，⌘⇧C 复制 Markdown 源码。

## 组件

快捷键 <kbd>⌘</kbd>+<kbd>K</kbd>，H<sub>2</sub>O，E = mc<sup>2</sup>，<mark>高亮文字</mark>，引用来源<Citation id="1" />。

<details>
<summary>展开查看更多</summary>

折叠块里的内容可以包含 **Markdown**：

- 列表
- `代码`
</details>

## HTML

<div align="center">
  <h3>居中的 HTML 标题</h3>
  <p>支持 <strong>常用标签</strong>、<span style="color: #1677ff">颜色</span>、<span style="font-size: 18px">字号</span> 与 <a href="https://github.com/239573049/StreamMark">链接</a>。</p>
</div>

<div style="margin-top: 8px; padding: 10px 14px; border: 1px solid #8884; border-radius: 8px">

盒子里也可以写 **Markdown**（与 HTML 之间用空行隔开）。

</div>

脚注示例[^1]。

[^1]: 脚注内容会显示在文末。
