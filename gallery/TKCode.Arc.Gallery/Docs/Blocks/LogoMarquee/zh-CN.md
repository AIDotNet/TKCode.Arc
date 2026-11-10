---
title: Logo marquee
summary: 一排安静、持续移动的标志，附带暂停控件。
source-hash: 92e7516e9873
---

<Demo name="Preview" />

## 何时使用

使用 `LogoMarquee` 在落地页上展示客户或集成标志条，或用于带标题和一行说明的社会证明区块。

## 何时不用

- **标志需要说明文字或连接操作**：使用 `Integrations` 区块。
- **内容需要用户手动浏览**：使用 `Carousel`。
- **没有展示许可的标志**：默认标志都是虚构的；只替换为你有权使用的标志。

## 用法

```xml
<arc:LogoMarquee Title="Trusted by product teams"
                 Description="Teams of every size ship with us." />
```

### 色调

`Tone` 可取 `Color`（每个标志使用自身颜色，名称以全对比度显示）或 `Mono`（所有标志与名称都使用次要文字颜色）。

<Demo name="Mono" />

### 自定义标志

`Brands` 接受 `LogoMarqueeBrand` 项。`Icon` 是任意坐标系下的填充 `Geometry`，会缩放到合适大小。`Color` 在 `Color` 色调下为其着色。`ColorIcon` 是全彩 `IImage`，在 `Color` 色调下取代 `Icon`。

<Demo name="CustomMarks" />

```csharp
marquee.Brands =
[
    new LogoMarqueeBrand { Name = "Acme", Icon = Geometry.Parse("M12 2 22 22H2z"), Color = Color.Parse("#3C9D6B") },
    new LogoMarqueeBrand { Name = "Globex", ColorIcon = myDrawingImage, Icon = fallbackShape }
];
```

### 暂停

标志条下方的控件切换 `IsPaused`，该属性也可以绑定。所有文字都是带英文默认值的属性。

## API 参考

<!-- api: LogoMarquee -->

## 无障碍

- 标志条只向辅助技术暴露一次，名称取自 `RegionLabel`，各标志名称作为帮助文本；重复的副本不暴露。
- 暂停控件有操作名称（`PauseActionName`、`PlayActionName`），暂停时实时区域会读出 `PausedAnnouncement`。
- 动效低于完整级别时，标志条完全不移动。

## 动效

- 标志条以线性速度在 39 秒内移动一个列表的宽度，无缝循环。鼠标或触控笔停留其上时保持静止。
- 每帧只在 `LiveLoop` 上移动一个变换，离开视口即暂停，不重绘也不分配内存。
- 减弱动效与关闭动效时，标志条停止移动，每个标志只绘制一次并居中换行，同时隐藏暂停控件。

## 响应式

- 区块跟随自身宽度。低于 640 px（`:narrow`）时标题与说明上下堆叠，标志从 22 px 缩小到 20 px，间距从 56 px 收紧到 40 px。
- 标志条两侧各淡出 7%，窄宽度下标志不会被生硬截断。

## 相关组件

- `Carousel`：需要手动切换的幻灯片。
- `Integrations` 区块：带说明和操作的标志。
