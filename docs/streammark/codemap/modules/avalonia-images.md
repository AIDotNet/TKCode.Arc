---
module: avalonia-images
sources: ["src/TKCode.Arc.StreamMark/Avalonia/Images/**", "src/TKCode.Arc.StreamMark/Avalonia/Rendering/ImageSlot.cs", "src/TKCode.Arc.StreamMark/Avalonia/Rendering/ImageViewer.cs"]
commit: 44a8f5a
updated: 2026-10-16
---
# avalonia-images（图片：策略 / 获取器 / 两级缓存 / 解码 / 布局槽位 / 查看器）

按"规范化地址 → 内存缓存 → 同一请求合并 → 磁盘缓存（新鲜直接用，过期条件请求重验证）→ 获取器取原始字节 → 后台按宽度档位解码 → 写回两级缓存"的管线加载一张图片，视图侧用 `ImageSlot` 持有句柄、用 `ImageRun` 参与 `TextLayout` 排版（占位骨架 / 失败重试 / 需要同意标签），点击已加载图片在全屏 `ImageViewer` 里查看。
**不负责：** Markdown 与 HTML 的解析、`InlineObject(Image)` 的产生（`core-parsing-and-model`，`MarkdigMapper.cs:442`、`HtmlMapper.cs:426`）；`DrawableTextRun` 基类、基线对齐与保留行高的重排（`avalonia-text-rendering`，`Rendering/InlineRuns.cs:69`、`TextSegmentView.cs:303`）；视图回收与 `OnDetachedFromVisualTree` 的调用时机（`avalonia-transcript`，`TranscriptPanel.cs`）；流式未写完图片地址的占位符替换（`core-streaming` 对应的 `StreamMark.Core/Remend`，`Remend.cs:87` 定义 `IncompleteImageUrl`）；相对地址的基准来源（宿主附加属性，`Extensions/MarkdownServices.cs:46`）；公钥固定的宿主实现 `SvgImageRasterizer`（`StreamMark.Mermaid`，`SvgImageRasterizer.cs:13`，本页只描述接口约定）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `ImagePipeline` | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImagePipeline.cs:52` | 唯一的对外入口：构造即建内存缓存（有目录时再建磁盘缓存），`LoadAsync` / `TryGetCached` / `TryGetPixelSize` / `Policy` / `Approve` / `ForgetFailure` / `Dispose` |
| `ImagePipelineOptions` | `ImagePolicy.cs` 之外的同文件 `ImagePipeline.cs:7` | 宿主配置：`Policy`、`MemoryBudgetBytes`、`NegativeCacheDuration`、`DiskCacheDirectory`（null 即不启用磁盘）、`Fetchers`、`HttpHandler`、`SvgRasterizer`、`TimeProvider`、`DecodeDensity`（文档内解码密度上限，`ImagePipeline.DecodeDensity` 可运行时修改）、`DiskThumbnails` / `ThumbnailEncoder`（磁盘缩略图，默认开启；编码器为 null 时用 Avalonia 存 PNG，`StreamMark.Mermaid.WebPImageEncoder` 存 WebP） |
| `IImageFetcher` | `src/TKCode.Arc.StreamMark/Avalonia/Images/IImageFetcher.cs:8` | 协议扩展点：`CanFetch(scheme)` / `IsRemote` / `FetchAsync`；内置 `DataUriFetcher`、`HttpsFetcher`、`FileFetcher`，宿主注册的排在前面（`ImagePipeline.cs:82`） |
| `ImagePolicy` | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImagePolicy.cs:11` | 不可变策略记录：`CheckUri`（协议 + 域名）、`RequiresConsent`、`AllowedFileRoots`、`MaxBytes` / `Timeout` / `MaxRedirects` / `UserAgent` |
| `ImageMemoryCache` | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImageMemoryCache.cs:53` | 已解码位图的内存缓存（LRU + 字节预算 + 负缓存 + 记住原始像素尺寸） |
| `ImageDiskCache` | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImageDiskCache.cs:60` | 原始字节 + 元数据的两文件磁盘缓存，容量 LRU 清理，可多进程共用 |
| `ImageSlot` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/ImageSlot.cs:12` | 一个视图中一张图的加载状态与句柄；`Ensure` / `Retry` / `Approve` / `Dispose` |
| `ImageRun` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/InlineRuns.cs:69` | 行内图片 run：按自然尺寸 / `width` / `height` 属性算尺寸，加载中画骨架占位，其它状态画标签 |
| `ImageViewer` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/ImageViewer.cs:14` | 全屏查看器（`Show` 静态入口，覆盖层显示，缩放 / 平移 / Esc 关闭） |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `ImagePipeline.LoadAsync(url, decodeWidth)` | `ImagePipeline.cs:190` | 全流程；返回的 `Loaded` 结果持有句柄，调用方负责释放；取消只影响本次等待，底层获取会跑完并写缓存（第 187 行注释、`GetBytesAsync` 第 385 行） |
| `ImagePipeline.TryGetCached` / `TryGetPixelSize` | `ImagePipeline.cs:137,155` | 同步、不联网：前者查内存命中（渲染首帧直接显示），后者按"内存记录 → `data:` 文件头 → 磁盘元数据"给占位宽高比 |
| `ImagePipeline.GetDecodeBucket` / `NormalizeUrl` | `ImagePipeline.cs:126,132` | 宽度档位（256px 向上取整，0 = 原始）；地址规范化（去片段、小写、去默认端口，`data:` 原样保留，`TryNormalize` 第 494 行） |
| `HttpsFetcher` | `Images/HttpsFetcher.cs:12` | 手动跟重定向并逐跳 `CheckUri`、`Content-Type` 必须 `image/*`、边读边限长、条件请求、`IDisposable`（持有 `HttpClient`） |
| `FileFetcher` | `Images/FileFetcher.cs:7` | 只读 `AllowedFileRoots` 下（逐级解析符号链接后）的文件，拒绝远程共享；`ContentTypeFromExtension` 第 125 行 |
| `DataUriFetcher` | `Images/DataUriFetcher.cs:4` | 纯内存解析 `data:image/*[;base64],…`，先按长度粗判上限再解码 |
| `ImageDecoder.Sniff` / `TryReadSize` / `Decode` / `Bucket` | `Images/ImageDecoder.cs:26,71,182,176` | 头部识别格式、只读文件头取像素尺寸（PNG/GIF/BMP/WebP/JPEG，SVG 只看 `Content-Type` 或前 512 字节文本）、按档位 `Bitmap.DecodeToWidth` |
| `ImageFetchResult` / `ImageCacheHeaders` / `ImageFetchStatus` | `Images/IImageFetcher.cs:112,56,43` | 获取结果四态（`Ok` / `NotModified` / `Failed` / `Blocked`）与缓存头（`ExplicitFreshness` 第 83 行算新鲜期） |
| `ImageLease` | `Images/ImageMemoryCache.cs:10` | 引用计数句柄：`Image` / `PixelSize` / `DecodedSize` / `AddRef`；最后一个句柄释放且条目已淘汰时才真正释放位图 |
| `ImageDiskCacheEntry` | `Images/ImageDiskCache.cs:9` | `<sha256>.json` 元数据：`Url` / `ETag` / `LastModified` / `ExpiresUtc` / `FreshnessSeconds` / `MustRevalidate` / `PixelWidth/Height` / `LastAccessUtc` / `Size` |
| `ImageViewer.FitScale` / `ImageRect` / `OnPointerWheelChanged` | `Rendering/ImageViewer.cs:60,73,107` | 适配缩放、居中矩形、以指针为中心缩放（夹在 `FitScale*0.25` 与 8 之间） |
| `ImageSlot.Ensure` / `Load` / `Release` | `Rendering/ImageSlot.cs:41,66,126` | 地址 / 档位未变则直接返回；否则先同步查缓存，未命中就异步加载并用 `_generation` 丢弃过期结果 |

## Dependencies
- Uses: `core-parsing-and-model` via `InlineObject(InlineObjectKind.Image, src, alt, title)` 与 `Attributes`（`width` / `height` 已由解析层从 `style` 规范化）— 消费点 `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:885-892`，`DeclaredSize` 第 907 行；模型定义 `src/TKCode.Arc.StreamMark/Core/Model/InlineContent.cs:89`
- Uses: `core-parsing-and-model` via `Remend.IncompleteImageUrl`（流式中地址未写完 → 传 `null` slot 的骨架占位）— `Rendering/TextSegmentView.cs:886`，常量 `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:87`
- Uses: `avalonia-text-rendering` via `DrawableTextRun`（`Size` / `Baseline` / `Draw`）— `Rendering/InlineRuns.cs:69`；`RenderContext.ImageBaseUri` 每帧传入 `SlotFor` — `Rendering/RenderContext.cs:33`、`TextSegmentView.cs:383`
- Uses: `avalonia-transcript` 无关方向反之：`ImageSlot` 由视图持有，视图被回收即 `Dispose` — `Rendering/TextSegmentView.cs:232-248`
- Uses: `MarkdownServices`（附加属性承载管线与基准） via `Services.Images` / `ResolveImageUrl` — `Extensions/MarkdownServices.cs:40,54`
- Uses: `StreamMark.Mermaid` via `ISvgRasterizer`（接口在 `ImagePipeline.cs:38`，实现 `src/TKCode.Arc.StreamMark.Mermaid/SvgImageRasterizer.cs:13`；不注入时 SVG 直接失败 `ImagePipeline.cs:347-350`）
- Used by: `avalonia-text-rendering` via `ImageRun` 作为 `ITextSource` 的嵌入对象 — `Rendering/TextSegmentView.cs:889,892`
- Used by: `tests` via `ImagePipeline` / `ImageMemoryCache` / `ImageDiskCache` / `ImageSlot` 诊断计数 — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Images/ImagePipelineTests.cs:39`、`Rendering/TextSegmentView.cs:375-377`

## State it owns
- **内存缓存键与生命周期**（`ImageMemoryCache.cs:49-52` 注释）：键 =（规范化地址，解码宽度档位），同地址可有多个档位条目；字节按 `宽×高×4` 估算（`Entry.Bytes` 第 415 行），超预算时从 LRU 尾部淘汰**未被句柄引用**的条目（`TrimLocked` 第 317 行），默认 96MB（第 56 行）。`TryAcquire` 允许"原始尺寸或更大档位"满足更小请求（第 129 行）。
- **负缓存**：失败地址记 `(原因, 到期时间)`，默认 5 分钟（第 64、75 行）；`ImagePipeline.LoadCoreAsync` 只在 `ImageFetchStatus.Failed` 且原因非 `canceled` 时写入（`ImagePipeline.cs:335-338`），`Blocked` 不进负缓存（`IImageFetcher.cs:49-52`）。
- **记忆的原始像素尺寸**：`_sizes`（上限 8192，满了整体清空，`RememberSize` 第 375 行）；`Clear()` 会一并清掉。
- **磁盘缩略图**：缩小解码（档位 > 0）或栅格化 SVG 之后，后台把结果编码写进同一个磁盘缓存，键为 `thumbnail:<档位>:<地址>`，元数据 `SourceVersion` 记录原图版本（本地文件 = 大小 + 修改时间，且先经 `FileFetcher.ResolveAllowedPath` 按策略解析；远程 = 原图缓存条目新鲜时的大小 + ETag + Last-Modified；`data:` 与自定义获取器不做）。`LoadCoreAsync` 在取原始字节**之前**先查缩略图，命中则不读、不解码原图（`ThumbnailHits` 计数）；版本不一致或解码失败即视为未命中。缩略图与原图共用容量上限与 LRU 清理。
- **磁盘缓存**：文件名主干 = `SHA256(地址)` 小写十六进制（`HashKey` 第 102 行），`<hash>.bin` + `<hash>.json`；写入先临时文件再覆盖改名（`WriteAtomicAsync` 第 347 行），元数据版本或地址不符即删掉整对文件（`ReadMetadata` 第 331 行）；超过容量（默认 512MB）在后台按 `LastAccessUtc` LRU 清理，构造时也清一次（第 92 行），并清理 10 分钟以上的 `.tmp` 与 1 分钟以上的孤立 `.bin`（第 458-465 行）。它有**自己的时钟**（`ImagePipelineOptions.TimeProvider` 注入）。
- **进程共享默认管线**：`MarkdownServices.s_defaultImages`（`MarkdownServices.cs:31,40`）是静态字段，`Default` 服务的 `Images` 惰性创建并全进程共用——**只有内存缓存**（构造时不传 `DiskCacheDirectory`），没有所有者、不释放，也不会随宿主设置页变化。宿主自建 `ImagePipeline` 时由宿主负责 `Dispose`（注释见 `MarkdownServices.cs:33-36`）。
- **视图侧句柄**：`ImageSlot._lease`（`ImageSlot.cs:16`）在 `Release` 时归还（第 126 行）；`TextSegmentView` 的 `_images` 字典按解析后的地址缓存 slot，`OnDetachedFromVisualTree` 里全部 `Dispose` 并清空（`TextSegmentView.cs:238-248`），重新挂载时重新取得（一般命中内存缓存）；`_usedImages` + `ReleaseUnusedImages`（第 379、397 行）只在整轮重建时释放本轮没用到的地址。
- **派生副本**：`ImageSlot.PixelSize` 是"缓存元数据里已知的自然尺寸"（`ImageSlot.cs:33-34`、`Ensure` 第 62 行），可与 `Lease.PixelSize` 不同（占位时先有、加载后以 lease 为准，第 94 行）；`ImagePipeline` 的 6 个 `internal` 计数器（`ImagePipeline.cs:99-104`）与 `ImageDiskCache._approxBytes`（第 73 行）都是估算 / 诊断值。
- **释放责任**：`ImageLoadResult` 与 `ImageLease` 都 `IDisposable`（`ImageLoadResult.cs:54`、`ImageMemoryCache.cs:39`），成功路径的句柄归调用方；管线内部只在等待者归零后释放自己那份 master 引用（`ImagePipeline.cs:259-272`）。

## Contracts it exposes
- **`IImageFetcher`**（`IImageFetcher.cs:8`）：实现必须线程安全；`FetchAsync` 失败返回 `Failed` / 策略拒绝返回 `Blocked`，**不抛**（调用方取消才抛 `OperationCanceledException`，第 19-20 行）；`IsRemote` 同时决定"是否受同意约束"和"是否落磁盘缓存"——`ImagePipeline.FetchBytesAsync` 里 `var disk = fetcher.IsRemote ? Disk : null`（`ImagePipeline.cs:421`，注释也写在 `IImageFetcher.cs:13-16`）。
- **`ImagePolicy` 允许的协议与文件根**：`CheckUri`（`ImagePolicy.cs:50`）拒绝相对地址、默认拒绝 `http`、`file:` 直接放行（协议层面），域名按 `BlockedHosts` 优先、`AllowedHosts` 通配（`*.x.com` / `.x.com` 只匹配子域，第 104-130 行）；本地文件**必须**由载荷再检查一次：`FileFetcher.ResolveAllowedPath` + `ImagePipeline.LoadAsync` 里的 `AllowedFileRoots.Count == 0 → Blocked("file access not allowed")`（`ImagePipeline.cs:208-211`）。
- **`MarkdownServices.ImageBaseUri` 与 `ImagePipeline` 的关系**：`ImageBaseUri` 只做**地址解析**，相对地址在进入管线之前由 `MarkdownServices.ResolveImageUrl` 拼成绝对地址（rooted 路径用 `new Uri(Path.GetFullPath(...))` 转 `file:`；没有基准时原样返回相对地址，随后被管线判 `invalid url` 而失败）。它**不会**放行本地文件——放行只能靠 `ImagePolicy.AllowedFileRoots`，这一点写在宿主机面向的注释里（`MarkdownServices.cs:42-45`）。
- **尺寸与对齐约定**：显示尺寸像浏览器一样 1 像素 = 1 DIP（`ImageRun` 注释 `InlineRuns.cs:83-84`）；`width` / `height` 属性优先，只给宽度时按自然宽高比推高（有自然尺寸时）；未加载但已知自然尺寸时**先按同样尺寸画骨架**，加载完成不跳动；两个方向都被限制在可用宽度内（`maxWidth`，第 91 行）。`Baseline` 在有标签时取标签首行基线，否则取整个高度（第 110 行），因此图片整体坐在基线上、行高随之增长；`TextSegmentView` 只有在"行内有更高对象"时才丢掉固定行高重排（`TextSegmentView.cs:302-306`）。
- **解码宽度**：`SlotFor` 用 `显示宽度 × TopLevel.RenderScaling` 作为物理像素宽度（无声明宽度、无限宽时兜底 1024），再向上取 256 的档位（`TextSegmentView.cs:390-393`）；`ImagePipeline.DecodeDensity` 有值时渲染缩放取 `min(RenderScaling, DecodeDensity)`（宿主压缩画质用）。点击图片的默认查看器（`TextSegmentView.ShowViewer`）按原始尺寸打开，先画缩小图并持有一个句柄，再 `LoadAsync(url, 0)` 换成原图；关闭时 `ImageLease.Evict()` 把原图移出内存缓存，不让它常驻。

## How to extend
- To add 一种协议 / 获取器：写一个 `IImageFetcher`（`CanFetch` 返回小写协议名、`IsRemote` 明确本地还是远程）→ 通过 `ImagePipelineOptions.Fetchers` 注册（`ImagePipeline.cs:25,82`，注册项排在 `data:` / `https` / `file` 之前）→ 若要求同意或落盘，必须把 `IsRemote` 设为 `true`，否则 `ImagePipeline.FetchBytesAsync` 不会给它磁盘缓存（`ImagePipeline.cs:421`），`RequiresConsent` 也不会触发（`ImagePipeline.cs:222`）。漏掉注册的表现是 `LoadAsync` 直接返回 `Blocked("unsupported scheme: …")`（`ImagePipeline.cs:199-202`）。参考测试 `ImagePipelineTests.Custom_scheme_fetcher_is_used`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Images/ImagePipelineTests.cs:601`）——它用一个匿名 `LambdaFetcher("app", …)`（该类型在测试项目里，本页未在 `Images/**` 中找到实现：(unverified)）。
- To add 一种解码格式：先在 `ImageFormat` 加枚举值并在 `Sniff` 里加头部判定（`ImageDecoder.cs:8,26`）——这一步决定磁盘缓存是否愿意落盘（`ImagePipeline.cs:467` 对 `Unknown` / `Svg` 不建条目）→ 再在 `TryReadSize` 加分支（第 71 行，占位宽高比依赖它，失败只是占位不准）→ 最后确认 `ImageDecoder.Decode` 能解（第 182 行，走 `Bitmap` / `Bitmap.DecodeToWidth`，解码失败会 `AddFailure("decode failed")` 并删掉磁盘副本，`ImagePipeline.cs:362-370`）。漏掉第 2 步的表现是：图能显示，但加载前没有正确占位、也没有磁盘缓存；矢量 / 需要专用栅格化的格式参考 SVG 的走法（`ISvgRasterizer`，`ImagePipeline.cs:38,345-357`）。
- To add 宿主接入带磁盘缓存的管线（三步）：`new ImagePipeline(new ImagePipelineOptions { DiskCacheDirectory = …, Policy = ImagePolicy.Default with { AllowedFileRoots = [工作区] }, SvgRasterizer = … })`（`ImagePipeline.cs:71`）→ 放进 `new MarkdownServices { Images = pipeline }` 并 `MarkdownServices.SetServices(root, services)`（`MarkdownServices.cs:40`）→ 在**同一个 root** 上 `MarkdownServices.SetImageBaseUri(root, 工作区)`（`MarkdownServices.cs:46-51`）。漏掉第 3 步的表现是相对路径原样进管线 → `invalid url` → 整片图片显示失败标签（测试 `RelativeImage_WithoutBaseUri_FailsInsteadOfHanging`，`ImageRenderingTests.cs:107`）；另外宿主必须自己 `using` / `Dispose` 这个 pipeline（`ImagePipeline.Dispose` 会取消在途请求并释放内存缓存与内置 `HttpsFetcher`，`ImagePipeline.cs:275-284`），并可用 `Disk.ClearAsync` / `Disk.GetSizeAsync` 做设置页（第 96 行注释）。

## Invariants and traps
- **默认管线只有内存缓存**：`MarkdownServices` 的共享默认实例不带磁盘缓存、没人释放；要磁盘缓存与本地文件必须宿主自建并负责释放 — `MarkdownServices.cs:31,40`、`ImagePipeline.cs:77-80,275`。
- **本地文件必须显式放行**：`AllowedFileRoots` 为空时一切 `file:` 都被拒绝（哪怕 `CheckUri` 对 `file:` 返回 null），且路径要先规范化再逐级解析符号链接比较前缀，因此"允许工作区"不会顺带允许工作区外的软链接目标 — `ImagePipeline.cs:208-211`、`FileFetcher.cs:57-95`。
- **`ImageBaseUri` 是 `inherits: true` 的附加属性**：值从窗口 / 会话视图继承到 `TextSegmentView`，并在成员变化时作废渲染上下文（`MarkdownView` 的类处理器 `MarkdownView.cs:45`、`TranscriptPanel.ContextFor` 比较后重建 `TranscriptPanel.cs:231-237`）；不设它时相对地址不会被解析，直接失败。
- **流式过程中的占位尺寸决定行高与滚动**：`IncompleteImageUrl` 或尚未取到尺寸时画的是标签 / 骨架（尺寸未知 → 由标签或 alt 决定），一旦 `TryGetPixelSize` 能给出自然尺寸（磁盘元数据 / `data:` 文件头）就按宽高比占位，`Load` 完成后再 `_changed()` → `OnResourceLoaded` → `_runCache.Invalidate()` + 全量重排（`TextSegmentView.cs:359-364`）；这段重排会改变行高，虚拟化下表现为滚动位置 / 滚动条长度变化（`avalonia-transcript` 的锚定机制负责兜住，见 `TranscriptPanel.cs:355,372`）。
- **`[![…](…)](…)` 的点击行为分两层**：解析层给图片的占位字符多加一个 `InlineStyle.Link` 区间（`MarkdigMapper.cs:603-606`，HTML 侧 `HtmlMapper.cs:449-452`），渲染层在 `OnPointerReleased` 里按**状态**分派——`Loaded` 且 `LinkAt(position) >= 0` 时发 `LinkClicked` 打开链接，其余成功情况发 `ImageClicked` 并在未被标记 `Handled` 时开 `ImageViewer`；失败 / 需要同意时分别 `Retry` / `Approve`（`TextSegmentView.cs:637-667`）。因此"徽章图片点开链接"不需要图片子系统做任何事，反而是图片自己的查看器被让位。
- **句柄不在手时位图可能已被释放**：`ImageMemoryCache` 只保证"被 `ImageLease` 引用"的条目不被释放，`Clear()` / `Dispose()` 之后仍持有的 lease 才让位图活到最后一次 `Dispose`（第 292-315 行）；`ImageSlot.Image` 在 `Release` 之后仍是旧引用，所以 `Dispose` 后不应再用（`ImageMemoryCache.cs:20` 注释、`ImageSlot.cs:126-131`）。
- **取消不取消共享获取**：同一地址（同档位）的并发 `LoadAsync` 合并到一次请求；某个等待者取消只让它自己收到异常，底层获取继续完成并写缓存 — `ImagePipeline.cs:187,231-272`，测试 `Cancelled_waiter_does_not_cancel_shared_fetch`（`ImagePipelineTests.cs:227`）。不同档位也会共享同一次字节获取（`_bytesInflight`，第 385 行）。
- **`ImageSlot.Ensure` 的短路条件容易误读**：`Status is not null && Status != Loaded` 表示"已失败 / 需同意 / 被拒绝"时，只要 pipeline 与地址不变就直接返回，不重新请求——重试必须走 `Retry()` 或 `Approve()`（`ImageSlot.cs:44,101,114`）。
- **`decodeWidth` 是物理像素**：传逻辑宽度会得到过小的档位，在高分屏上表现为图片发虚；反向传过大会浪费内存 — `TextSegmentView.cs:390-393`、`ImagePipeline.GetDecodeBucket` 文档注释 `ImagePipeline.cs:125`。
- **SVG 只在注入栅格化器时可用**，且 `ImageDecoder.Sniff` 靠 `Content-Type` 或前 512 字节文本识别（`ImageDecoder.cs:52-66`）；未注入时返回 `Failed("svg not supported")` 并进负缓存（`ImagePipeline.cs:345-351`），磁盘缓存也不为 SVG 建条目（第 467 行）。
- **磁盘缓存的新鲜度默认 7 天**：响应没有任何缓存头时用 `DefaultFreshness`（`ImageDiskCache.cs:66,257`）；过期后用 ETag / Last-Modified 条件请求，304 时刷新元数据，重新验证失败且没有 `must-revalidate` 时继续用旧内容（`ImagePipeline.cs:426-457,485-489`）。

## Tests
- 位置：`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Images/`，4 个文件：`ImagePipelineTests.cs`（23 个用例：网络→内存→磁盘、HTTP 缓存语义、请求合并、策略与同意、失败负缓存、档位解码、data: URI、SVG 不支持、自定义协议）、`ImageCacheTests.cs`（11 个用例：LRU 与租约保护、负缓存过期、磁盘清理与损坏恢复、`FileFetcher` 越界、头部尺寸嗅探）、`ImageRenderingTests.cs`（3 个用例：`ResolveImageUrl`、工作区相对路径 PNG+SVG 渲染、无基准地址时失败而不挂起）、`ImageTestSupport.cs`（`TestPng.Create` 自造可解码 PNG、`TestImageServer`、`TempDir`、`ManualTimeProvider`）。
- 命名模式：类名 `<主题>Tests`，方法用"行为_期望"的下划线句子（如 `Lease_outlives_eviction_from_pipeline_memory`、`Network_then_memory_then_disk_across_pipelines`），方法体包在 `await HeadlessApp.Run(async () => { … })` 里在界面线程跑（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:55`），渲染类测试用 `HeadlessApp.Settle()` 推进帧并可用 `HeadlessApp.Save(window, "…")` 落截图。
- 只跑一个类 / 文件：`dotnet test TKCode.Arc.slnx --filter "FullyQualifiedName~Images.ImagePipelineTests"`（同理替换 `ImageCacheTests` / `ImageRenderingTests`）；整个模块 `--filter "FullyQualifiedName~Tests.Images"`。仓库既有命令是 `dotnet test TKCode.Arc.slnx`（`README.md:91`）。未运行验证：(unverified)。

## Related flows
- [raw-html-render](../flows/raw-html-render.md)
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [transcript-virtualization-scroll](../flows/transcript-virtualization-scroll.md)
