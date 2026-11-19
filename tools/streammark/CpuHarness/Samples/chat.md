好的，我来分析一下这个问题。整体思路是先定位瓶颈，再逐个击破：流式渲染里最常见的开销来自**重复排版**、**重复整形**和**每帧重绘**，而不是解析本身。下面按模块逐一说明，并给出可以直接落地的修改建议。

## 1. 现状梳理

当前的数据流大致是：网络分片到达后进入节奏控制器，节奏控制器每帧放出若干字符，追加到只追加的源码缓冲区；后台线程负责切分与解析，界面线程每帧最多应用一次快照差异。The pipeline itself is reasonably efficient: parsing only touches the unfrozen tail, and the diff skips the shared frozen prefix, so the per-update parse cost stays in the low microseconds even for long documents.

但是在视图层，情况就不一样了。每次追加文本时，末尾段落的 `TextLayout` 会被整体丢弃并重建，这意味着整段文字都要重新做字形整形（shaping）和断行（line breaking）。对于一个两三千字的长段落，这个成本在每秒六十次的频率下会非常可观。

- 解析：后台单线程合并，**中间版本跳过**，基本不是瓶颈
- 差异：按块 key 归并，冻结前缀直接跳过
- 排版：末尾段落每帧整体重建，`O(段落长度)`
- 绘制：渐显效果每帧把整个段落画两遍，并且带不透明度蒙版
- 动画：只要还有字符在渐显，就持续请求下一帧

### 1.1 关键路径上的函数

```csharp
public override void Render(DrawingContext context)
{
    var streaming = IsDocumentTail ? _context.Streaming : null;
    var fading = streaming is not null ? streaming.GetFadingTail(StreamClock.Now) : (0, 1.0, 1.0);
    if (fading.Item1 > 0 && _content.Text.Length > 0)
    {
        DrawWithFade(context, layout, fading.Item1, fading.Item2, fading.Item3);
        RequestNextFrame();
    }
    else
    {
        layout.Draw(context, default);
    }
}

private void DrawWithFade(DrawingContext context, TextLayout layout, int count, double oldest, double newest)
{
    var start = Math.Max(0, _content.Text.Length - count);
    var first = layout.HitTestTextPosition(start);
    using (context.PushGeometryClip(solidGeometry))
    {
        layout.Draw(context, default);
    }
    using (context.PushGeometryClip(fadeGeometry))
    using (context.PushOpacityMask(mask, bounds))
    {
        layout.Draw(context, default);
    }
}
```

上面这段代码里，`PushOpacityMask` 在 Skia 后端会触发一次离屏图层（`SaveLayer`），图层大小等于整个段落的边界。也就是说，每一帧都要分配并合成一块和段落一样大的离屏缓冲。

## 2. 测量方法

| 场景 | 字符速率 | 渐显 | 平滑 | 说明 |
|---|---:|:---:|:---:|---|
| 基线 | 320 字/秒 | 开 | 开 | 与真实模型接近 |
| 关闭渐显 | 320 字/秒 | 关 | 开 | 隔离绘制开销 |
| 关闭平滑 | 320 字/秒 | 开 | 关 | 隔离节奏控制器 |
| 长段落 | 320 字/秒 | 开 | 开 | 单段 3000 字 |
| 长代码 | 320 字/秒 | 开 | 开 | 400 行代码块 |

测量时只看稳态：先预热三秒，再统计十五秒内的进程 CPU 时间和界面线程 CPU 时间。The remaining time is attributed to the render thread, the parse worker and the GC. 为了避免干扰，测试程序本身不注入任何输入事件，也不截屏。

## 3. 优化方向

### 3.1 增量排版

最直接的收益来自**只为变化的那一行重新排版**。Avalonia 的 `TextFormatter.FormatLine` 可以从任意文本位置开始格式化一行，因此可以缓存已经稳定的行（最后一个换行点之前的行），每次追加只重新格式化最后一两行。这样每帧的排版成本从 `O(段落长度)` 降到 `O(最后一行长度)`。

1. 记录每一行的起始位置和 `TextLineBreak`
2. 追加时找到差异点所在的行
3. 从该行开始重新格式化，直到行的起点与旧结果重新对齐或者文本结束
4. 高度只在行数变化时才改变，避免触发父级重新测量

### 3.2 渐显改为合成层动画

渐显效果完全可以交给渲染线程。把末尾新出现的字形单独作为一个合成视觉对象（composition visual），用不透明度动画淡入，界面线程就不需要每帧重新录制绘制指令。另一个更简单的办法是：只对最后一行使用渐变画刷作为前景色，而不是对整个段落做不透明度蒙版。

> 注意：渐显的视觉效果要保持一致。新字符从透明到不透明的过渡时间是 220 毫秒，最旧那批和最新那批之间用线性渐变连接。如果改成按行处理，跨行的情况需要分别计算两段渐变。

### 3.3 降低提交频率

节奏控制器目前每 16 毫秒放出一次文本，和显示器刷新并不同步。在 120Hz 的屏幕上，渲染循环和计时器会互相错位，导致某些帧做了两次排版。把放出逻辑挂到 `RequestAnimationFrame` 上，可以保证每个显示帧最多一次排版、一次绘制。

```python
def schedule(frame_time, backlog, rate):
    # 每帧最多放出一次；积压越多放得越快
    count = max(rate * frame_time, backlog / TARGET_LAG)
    return min(int(count), backlog)
```

### 3.4 代码块

代码块已经按行缓存排版结果，并且高亮在后台增量进行，所以流式代码块的成本主要在高亮回调触发的重绘上。可以考虑把高亮结果合并到下一帧统一应用，而不是每完成一行就失效一次。

```json
{
  "pacing": "Balanced",
  "fadeDuration": 220,
  "maxLag": 400,
  "bypassThreshold": 4096,
  "skipCompletedBlocks": false
}
```

## 4. 预期收益

综合以上几项，稳态 CPU 应该能够明显下降：排版成本从整段变为单行，绘制从两遍整段加离屏图层变为一遍整段加一行渐变，提交频率与显示器同步。Long answers with several thousand characters per paragraph benefit the most, because their cost currently grows linearly with paragraph length on every single frame.

最后提醒一点：任何优化都要用同一套测量方法前后对比，并且同时检查视觉效果没有退化——渐显、光标、选区和复制都要保持原样。
