# 流式 Markdown 渲染方案

下面是一个**综合示例**，包含 *斜体*、`行内代码`、~~删除线~~ 以及[链接](https://example.com "示例")。
中文软换行不应插入空格，English soft breaks
should become spaces.

## 步骤

1. 解析源码
2. 冻结稳定前缀
   - 嵌套项 A
   - 嵌套项 B

     嵌套段落，属于 B。
3. 只重解析尾部

- [x] 已完成的任务
- [ ] 未完成的任务

> [!NOTE]
> 提示块也要支持，包括 **粗体**。

> 普通引用第一段
>
> 普通引用第二段

```csharp
public static int Add(int a, int b)
{

    return a + b;
}
```

| 名称 | 说明 | 数量 |
|:-----|:----:|-----:|
| Markdig | 解析器 | 1 |
| RaTeX | 公式 | 2 |

行内公式 $E = mc^2$ 与反斜杠公式 \(a^2 + b^2 = c^2\)。

$$
\int_0^1 x^2 \, dx = \frac{1}{3}
$$

\[
\sum_{i=1}^{n} i = \frac{n(n+1)}{2}
\]

---

::: warning
自定义容器第一段

自定义容器第二段
:::

<!--
注释可以跨越

空行
-->

术语
: 定义说明

    缩进代码块
    第二行

最后一段，含有自动链接 https://github.com 和 emoji :smile:。
