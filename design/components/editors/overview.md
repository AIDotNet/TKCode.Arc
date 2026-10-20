# 编辑器（editors）

> 编辑器在 Arc 中承载自由形态的内容创作：富文本、手写签名与文件拖放；每个组件都维护自己的输入模型（文档、笔迹、文件队列），而不只是一个值。

## 1. 大类定位
- 解决：结构化文本（标题、列表、引用、代码与链接）的撰写及 HTML / Markdown 输出；带压感的手写签名、回放与导出；文件的选择、拖放、粘贴与逐文件上传进度。
- 边界：纯文本用 Textarea，只需 @ 提及用 MentionInput，单行原地编辑用 InlineEdit（均属 TextFields / SpecialInputs）；展示代码用 CodeBlock（Tables）；只需郑重确认而不需要签名产物时用 HoldToConfirm（Gestures）。Avatar upload 属 Pro，不在范围。
- 共同特征：组件内部持有历史或列表状态（RichTextEditor 300 步撤销、SignaturePad 60 步、FileDropzone 的逐行状态），每个操作结果都经礼貌播报宣告；动效服务于"确认结果"（工具栏形变、擦除、行进出），而不是装饰。
- 移植难度最高的大类：三者都依赖浏览器原生能力（contentEditable、Pointer Events 的合并事件与压感、拖放与剪贴板文件），Avalonia 侧需要显式替代，因此全部排在 P9。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 富文本编辑器 | `RichTextEditor` | [rich-text-editor](https://uiarc.dev/components/rich-text-editor) | P9 | 无 | TemplatedControl（块编辑器） | P6 P7 P9 P10 P25 |
| 签名板 | `SignaturePad` | [signature-pad](https://uiarc.dev/components/signature-pad) | P9 | 无 | Control（自绘） | P2 P7 P15 P29 |
| 文件拖放区 | `FileDropzone` | [file-dropzone](https://uiarc.dev/components/file-dropzone) | P9 | 无 | TemplatedControl | P6 P13 P15 P19 P21 P23 P27 |

## 3. 共享约定
- **修饰键**：Arc 的 Cmd / Ctrl（Mod）统一映射到 PlatformHotkeyConfiguration.CommandModifiers（macOS 为 Meta，其他平台为 Control）；撤销 Mod+Z，重做 Mod+Shift+Z 与 Mod+Y，RichTextEditor 与 SignaturePad 一致。
- **自有历史**：撤销栈由组件自己维护，不使用平台撤销；RichTextEditor 把可撤销状态暴露给外部工具栏（Arc 的 onHistoryChange）。
- **结果播报**：操作结果写入 LiveSetting=Polite 的播报元素（RichTextEditor："Formatted"、"Link added"、"Undone"；SignaturePad：笔画增加、撤销、重做、清除、回放、导出；FileDropzone：添加、完成、失败）；拒绝类错误（类型、大小、数量）用 Assertive。文案全部走 xlf。
- **工具按钮不夺焦**：工具栏与选项按钮按下时不抢焦点，以保留文本选区或画板焦点（Arc 用 mousedown preventDefault；Avalonia 中设 Focusable=false，并在 PointerPressed 中标记 Handled）。
- **图标按钮**：Lucide 图标 15–17px、描边 1.75，必须有 AutomationProperties.Name 与 ToolTip（Arc 的 title 带快捷键提示，如 "Bold (⌘B)"）。
- **指针与触摸**：绘制与拖动区域按下即捕获指针，阻止外层 ScrollViewer 的触摸滚动（对应 Arc 的 touch-action: none）；悬停样式只对鼠标指针生效。
- **动效等级**：Reduced 去掉位移、缩放、擦除与模糊，换成短淡入淡出，进度变化立即到位；Off 立即到终态。

## 4. 组件

### 4.1 RichTextEditor（Rich text editor）
`slug: rich-text-editor` · 阶段 P9 · Arc 依赖：无 · 建议基类：TemplatedControl（块编辑器）

**Arc 行为摘要**：基于 contentEditable 的轻量编辑器：输入时的 Markdown 快捷方式、随选区出现并可形变为链接输入框的浮动工具栏、"/" 块菜单、自有撤销历史，输出干净的 HTML 与 Markdown。适合评论、笔记、帖子与中短篇文档。块只有 p、h1–h3、ul / ol（可嵌套）、blockquote、pre、hr，行内只有 strong、em、s、code、a；不支持图片、表格与下划线。纯文本用 Textarea，只需提及用 MentionInput，单行原地编辑用 InlineEdit，展示代码用 CodeBlock。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` | `string`（HTML），加载时净化 | `Html`（string，双向；仅当与上次输出不同时才重建文档，回填 ContentChanged 的 Html 是安全的） |
| `defaultMarkdown` | `string` | `Markdown`（string?，未给 Html 时作为初始内容） |
| `onChange` | `(RichTextValue) => void`，每次编辑后 | `ContentChanged` 事件（RichTextValue：Html、Markdown、Text、IsEmpty） |
| `onHistoryChange` | `{ canUndo, canRedo }` | `CanUndo` / `CanRedo`（bool，只读）+ `UndoCommand` / `RedoCommand` |
| `placeholder` / `blockHint` | `"Start writing"` / `"Type / for blocks"` | `Placeholder` / `BlockHint`（string） |
| `readOnly` | `false`，关闭编辑、工具栏与快捷键 | `IsReadOnly`（bool） |
| `autoFocus` | `false`，挂载时聚焦并把光标放到末尾 | `IsAutoFocusEnabled`（bool） |
| `aria-label` | `"Editor"` | `AutomationProperties.Name`（默认 "Editor"） |

命令式句柄映射为公开方法 `GetHtml()`、`GetMarkdown()`、`GetText()`、`SetHtml()`、`SetMarkdown()`、`Clear()`、`Undo()`、`Redo()`（Set 与 Clear 各为一个撤销步）；工具函数 markdownToHtml、htmlToMarkdown、sanitizeHtml、normalizeUrl 放入静态类 `RichTextConvert`。

**交互与键盘**：
- 行内：Mod+B 粗体、Mod+I 斜体、Mod+Shift+X 删除线、Mod+E 行内代码（限单块内）、Mod+K 为选区或光标处的链接打开链接框；Mod+U（下划线）被拦截。
- Markdown 快捷方式在完成字符输入后立即转换：段落行首 `# `、`## `、`### `、`- ` / `* ` / `+ `、`1. ` / `1) `、`> ` 转为块，`---` 转为分隔线，` ``` ` 转为代码块；`**x**` / `__x__`、`*x*` / `_x_`、`~~x~~`、`` `x` `` 转为行内标记（Arc 在标记后插零宽空格让下一次输入落在标记外，模型实现可改用"光标处标记状态"）。
- "/"（行首或空格后）打开块菜单：输入过滤，ArrowUp / ArrowDown 移动，Enter 或 Tab 选择，Escape 关闭。
- Tab / Shift+Tab 缩进 / 反缩进列表项，代码块内 Tab 插入两个空格；Enter 新块、Shift+Enter 换行，空列表项或引用中 Enter 退出，代码块末尾连续两次 Enter 退出；块首 Backspace 把标题、引用、代码块、列表项变回段落或反缩进，分隔线后 Backspace 删除分隔线。
- Alt+F10 把焦点移入打开中的浮动工具栏，ArrowLeft / ArrowRight 在按钮间移动，Escape 回到正文。粘贴经过净化，形似 Markdown 的纯文本会被转换。

**可访问性**：
- 编辑面为可编辑文本 peer（Edit 类型、多行、只读状态、Name 默认 "Editor"）；块菜单为 List，经活动项与编辑面关联；浮动工具栏为 ToolBar，格式按钮为切换按钮（IToggleProvider），ToolTip 显示快捷键。
- 结果播报："Formatted"、"Link added"、"Undone" 等写入 Polite 播报元素。
- 明确降级：Avalonia 12.1.4 没有 ITextProvider / ITextRangeProvider，读屏无法在自绘编辑器内按字符、词、行导航或读出选区，只能经 IValueProvider 读整段纯文本；需在规格中记录为与 Arc 的差异。

**动效**：
- 工具栏随选区滑行（GLIDE：0.3s、bounce 0.08），表面在"格式面"与"链接面"之间按 morph 时长（0.42s、bounce 0.12）形变尺寸（P9）；两个面以 ±18px 横移 + blur subtle 互相滑过（入 0.2s + enter、延迟 0.03s，出 0.12s + standard）（P25）。
- 块菜单从 "/" 处弹出（P10），高亮在行间滑行（P7），筛选结果变化时高度以 smooth 时长（0.4s、无回弹）的弹簧变化（P6）。
- Reduced：工具栏位置与尺寸直接跳变，两个面交叉淡化，菜单只淡入淡出、无缩放与位移。

**Avalonia 实现要点**：Arc 依赖的浏览器能力在 Avalonia 中都要自建：

| Arc 依赖的浏览器能力 | Avalonia 12.1.4 现状 | TKCode.Arc 方案 |
|---|---|---|
| contentEditable 富文本 DOM | 没有；TextBox 只编辑纯文本，SelectableTextBlock 支持 Inline 但只读 | 自建块文档模型（Paragraph、Heading1–3、ListItem（层级、有序）、Quote、CodeBlock、Divider；行内 Bold、Italic、Strike、Code、Link(href)），每块一个 TextLayout 自绘 |
| Selection / Range 与光标 | 没有跨元素选区 | 文档坐标（块索引 + 字符偏移）的选区，自绘光标闪烁与跨块选区高亮；命中与矩形用 TextLayout.HitTestPoint / HitTestTextPosition / HitTestTextRange |
| beforeinput 与 IME 组合输入 | 提供 TextInputMethodClient，经 TextInputMethodClientRequested 交给控件 | 自定义 TextInputMethodClient：SurroundingText、光标矩形、预编辑文本与下划线渲染；组合输入期间不触发快捷键与 Markdown 规则 |
| execCommand（加粗、链接等） | 没有 | 文档模型上的命令（切换标记、设块类型、缩进、设链接），全部进入自有撤销栈 |
| 剪贴板 HTML / 纯文本 | IClipboard.SetDataAsync / TryGetDataAsync + DataTransfer | 复制同时写 HTML 与纯文本；粘贴的 HTML 经 C# 白名单净化（没有 DOMParser，需自写或引入 HTML 解析器） |
| 拼写检查、系统自动更正 | 没有 | v1 不提供，记为与 Arc（浏览器默认）的差异 |

- 工具栏与块菜单是以"选区矩形"为虚拟锚点的 S8 锚定浮层：靠近滚动或裁剪祖先的边缘时上下翻转，水平方向夹在编辑器宽度内；编辑器宽 < 330px 时工具栏隐藏标题与引用按钮、只留行内标记。
- 撤销：最多 300 个快照，1 秒内的连续输入合并；对不可变文档模型做快照，比 Arc 的 HTML 快照便宜。选区变化合并到每帧一次（S1 帧时钟）。
- 输出：每次编辑从模型序列化 HTML（规范标签，只保留安全 href：http、https、mailto、tel、相对路径与 # 锚点）、Markdown 与纯文本（每块一行）；导入 Markdown 时 h4–h6 降为 h3；normalizeUrl 给裸域名补 https、给邮箱补 mailto。
- 服务：S1、S2、S3、S5（工具栏面尺寸）、S7、S8、S9（块菜单）。

**AtomUI 参考**：—（AtomUI 无对应控件）。

**风险与未决**：
- 工作量与平台差异全库最大：需在 P9 之前做 spike，验证三平台的中文 / 日文输入法、跨块选区、长文档的命中测试与重排性能。AvaloniaEdit 面向代码编辑（单一文本缓冲），可参考其光标与 IME 处理，但不提供块级富文本模型。
- 读屏降级（无 Text 模式）与双向文本（RTL）中的光标移动范围需要在规格中写明支持边界。
- Markdown / HTML 转换：自写子集解析器（逐条对齐 Arc 行为）还是引入 Markdig、AngleSharp 等依赖（许可、AOT 裁剪），需要 ADR 级决定。

### 4.2 SignaturePad（Signature pad）
`slug: signature-pad` · 阶段 P9 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：带真实墨迹的签名区：笔压决定线宽（鼠标与手指按速度模拟，快则细），笔画两端收锋；支持撤销 / 重做、擦除式清除、按原节奏回放，以及导出裁剪到墨迹的 PNG 或 SVG。用于合同、签收确认与知情同意；只需输入姓名时用 Input，只需郑重确认而不需要签名产物时用 HoldToConfirm。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `signer` | `string` | `Signer`（string?，印在基线下方） |
| `hint` | `"Sign here"` | `Hint`（string） |
| `defaultColor` | `"black" \| "blue" \| "violet"`，`"black"` | `InkColor`（enum SignatureInkColor：Black / Blue / Violet） |
| `defaultWidth` | `"fine" \| "medium" \| "bold"`，`"medium"` | `InkWidth`（enum SignatureInkWidth；笔宽 3.4 / 5.6 / 8.4 画板单位） |
| `onChange` | `(InkStroke[]) => void`，含撤销、重做、清除 | `StrokesChanged` 事件 + `Strokes`（`IReadOnlyList<InkStroke>`，只读） |
| `fileName` | `"signature"`，不含扩展名 | `FileName`（string） |
| `label` | `"Signature pad"` | `AutomationProperties.Name`（默认 "Signature pad"） |

Arc 的 signatureToSvg / signatureToPng（scale 默认 3）映射为静态 `SignatureExport.ToSvg(strokes)` / `ToPngAsync(strokes, scale)`；Arc 组件是非受控的（换 key 重挂载来重置），Avalonia 侧以 `Clear()` 方法替代重挂载。

**交互与键盘**：
- 画板聚焦时 Mod+Z 撤销，Shift+Mod+Z / Mod+Y 重做；Delete / Backspace 以擦除动画清除，可撤销恢复。
- Escape 停止进行中的回放；墨色与笔宽各为一个单选组，组内用方向键切换。
- 工具栏：撤销、重做、回放（播放 / 停止图标切换）、导出 PNG / SVG、清除。

**可访问性**：
- 画板可聚焦，peer 为 Image 类型，GetLocalizedControlTypeCore 返回 "signature pad"，Name 含笔画数；快捷键说明放在 HelpText（对应 aria-describedby 指向的隐藏段落）。
- 墨色、笔宽为 roving tab stop 的单选组；图标按钮有 Name 与 ToolTip。
- 笔画增加、撤销、重做、清除、回放与导出结果写入 Polite 播报。

**动效**：
- 清除：刮板边缘横扫画板，同时以裁剪擦除墨迹，0.56s（P29）。
- 回放：按原始节奏逐笔重画并显示笔尖圆点（35% 不透明），笔画间隔夹在 90–260ms，整体压缩到约 3.2s（P15）。
- 提示文字从 blur 中淡入上移（standard + enter），退出 fast + standard（P2）；墨色 / 笔宽的选中高亮以共享布局弹簧 morph 滑行（P7）。
- 回放 / 停止图标切换 scale 0.6 → 1，走 snappy。
- Reduced：去掉擦除与模糊，使用短淡入淡出。

**Avalonia 实现要点**：
- 自绘：画板坐标固定为 600 × 260 单位，MeasureOverride 保持 600 : 260 宽高比，笔画随尺寸缩放；Render 中每笔绘制一个填充的 StreamGeometry 轮廓（类 perfect-freehand：streamline 0.42、thinning 0.62，无压力时按速度模拟，两端收锋，急转处圆角连接）。
- 输入：按下时捕获指针；PointerMoved 中用 GetIntermediatePoints 取合并点（对应 coalesced events），笔输入读 PointerPointProperties.Pressure；当前笔画只 InvalidateVisual，不触发布局。
- 历史保留最近 60 个状态；回放用 S1 帧时钟逐帧重绘可见笔画，长签名回放更耗时。
- 导出：SVG 字符串使用固定印刷墨色（#17171a、#1d3fae、#5b35c8）；PNG 用 RenderTargetBitmap 以 600 × 260 的 3 倍分辨率绘制、透明底、裁剪到墨迹；保存经 TopLevel.StorageProvider 的保存对话框，默认文件名取 FileName。
- 屏幕墨色是主题相关的 Own Token（黑 = Foreground，蓝 / 紫在明暗下各一份），与导出墨色分开；画板 RadiusPanel + 1px Border，基线为 1.5px BorderStrong 虚线，位于 72% 高度、左右各缩进 7%。
- 服务：S1、S3、S7、S9（单选组）。

**AtomUI 参考**：—（AtomUI 无对应控件）。

**风险与未决**：
- 三平台笔压支持程度不同（Windows Ink、macOS 数位板、Linux），无压力数据时统一走速度模拟，需实机验证手感。
- 存储格式：Arc 存 InkStroke[]（600 × 260 坐标、压力、时间戳），可回放；Avalonia 侧的序列化格式（JSON 字段名）是否与 Arc 保持一致以便跨端回放，需在规格中决定。

### 4.3 FileDropzone（File dropzone）
`slug: file-dropzone` · 阶段 P9 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：宽大的拖放目标兼文件选择器，在下方（或目标边缘内）列出文件，可通过 onUpload 逐个上传，显示进度、重试与移除。用于附件与上传；只需系统文件对话框、不需要列表时直接调用文件选择（Arc 中对应原生 file input），空页面中拖放是唯一动作时用 EmptyState 给出更大的号召。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `accept` | `string`（".pdf"、"image/*"），其他类型以错误拒绝 | `Accept`（string，同语义） |
| `multiple` / `maxFiles` | `true` / `5` | `AllowMultiple`（bool，true）/ `MaxFiles`（int，5） |
| `maxSize` | `number`（字节），超出则失败且不上传 | `MaxFileSize`（long?） |
| `onFilesChange` | `(File[]) => void` | `FilesChanged` 事件（`IReadOnlyList<IStorageFile>`） |
| `defaultItems` | `FileDropzoneItem[]`，挂载时已有的行不播放入场 | `Items`（`IList<FileDropzoneItem>`） |
| `onUpload` | `(item, { onProgress, signal }) => Promise<void>` | `UploadHandler`（`Func<FileDropzoneItem, IProgress<double>, CancellationToken, Task>?`） |
| `label` / `description` / `note` / `dropLabel` | `"Add files"` / `"Drop files here or choose from your device"` / 自动 / 无 | `Label` / `Description` / `Note` / `DropLabel`（string） |
| `listPlacement` / `compactAt` | `"below" \| "inside"`，`"below"` / 无 | `ListPlacement`（enum：Below / Inside）/ `CompactAt`（int?，达到后提示折叠为细条） |

FileDropzoneItem 映射为同名类：Id、Name、Size、Status（Uploading / Uploaded / Failed）、Progress、Error、IsRetryable、File、Preview。

**交互与键盘**：
- 拖放目标是原生按钮：Enter / Space 打开系统文件对话框；整个目标是一个点击区，触摸设备不拖放也能用。
- 文件列表中 ArrowUp / ArrowDown 把焦点移到相邻行的同一动作（移除或重试），Delete / Backspace 移除聚焦行；移除后焦点移到相邻行。
- Mod+V 粘贴剪贴板中的文件（如截图），条件是指针悬停在拖放区上或焦点在其内，文本框内忽略。
- 被拒绝（类型、大小、超过 MaxFiles）时目标抖动并显示错误；移除上传中的行会取消其上传。

**可访问性**：
- 目标为 Button 类型（Arc 中移出 Tab 顺序的隐藏 file input 在 Avalonia 中由 StorageProvider 取代，无需对应元素）；上传中的行暴露 ProgressBar（IRangeValueProvider，0–100），移除 / 重试按钮的 Name 含文件名。
- 添加、完成、失败写入 Polite 播报，拒绝错误用 Assertive。
- 文件名与失败原因单行省略，完整文本放在 ToolTip 与 Name 中。

**动效**：
- 文件悬停时三张纸图标上抬 3px，虚线边（dash 3.5 / 3.5）收拢为实线并变为 Accent 40% 混色，目标以 2.2s 周期"呼吸"，指针附近出现内部光晕与边缘高光（P27、P15）。
- 行进入：从 y −22、scale 0.94、blur soft 落位，高度以 smooth 展开，新批次最多交错 8 步；移除时高度以 visualDuration 0.3 的 smooth 收起（P19、P6）。
- 进度条以 smooth 弹簧追随目标百分比，完成后对勾以路径描绘出现（P23、P15）；失败行左右衰减抖动（x 0、−7、6、−4、3、−1.5、0，0.42s）。
- Reduced：高度与上抬改为短淡入淡出，进度变化立即到位。

**Avalonia 实现要点**：
- 三种来源统一为 IStorageFile 列表再进入同一校验管线：拖放用 DragDrop.AllowDrop 与 DragEnter / DragOver / DragLeave / Drop 事件，从 DataTransfer 读取文件项；选择用 TopLevel.StorageProvider.OpenFilePickerAsync（由 Accept 生成文件类型过滤）；粘贴用 IClipboard.TryGetDataAsync 读取文件格式。
- 虚线边自绘为矩形描边，dash 周期按实测周长取整，接缝不可见；圆角在 RadiusSurface（34）与折叠后的 RadiusPanel（26）之间过渡时逐帧重算（S5）。
- 图片预览用 AtomUI 的 ImageLoader / AsyncImage 生成缩略图，行移除或控件卸载时释放；加载失败回落到按扩展名选择的类型图标。
- 列表不虚拟化，MaxFiles 默认 5；容器宽 < 440px 时重试按钮只留图标，< 300px 时隐藏文件图标（S5）。
- 服务：S1、S2、S3、S5、S9。

**AtomUI 参考**：Upload、UploadDropZone（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 主数据未列 P21：拖入悬停时目标的 2.2s 呼吸是持续循环，Reduced 下必须停止；失败抖动应归入 P27 还是 P13，需原语目录确认。
- Accept 中的 MIME 通配（"image/*"）到桌面文件对话框过滤器的映射，以及剪贴板文件格式在三平台上的可用性，需要实机验证。
- 复核结论（汇总评审）：新增 P13、P21，已同步到[组件总清单](../overview.md)。

## 5. 实现顺序与依赖
- 三者都在 P9（数据、活动与重型编辑器），Arc 依赖均为空；建议顺序 FileDropzone → SignaturePad → RichTextEditor，风险递增。
- **FileDropzone**：复用 Progress（P3）首次落地的 P23、DatePicker（P2）的 P19、CopyButton（P1）的 P15；需要拖放、StorageProvider、剪贴板文件读取与 ImageLoader 的封装。
- **SignaturePad**：P29（Button，P0）、P15（CopyButton，P1）、P7（Calendar，P1）已就绪；新增的是自绘输入层（合并点、压感）与 RenderTargetBitmap 导出。
- **RichTextEditor**：路线图风险登记中单列的风险项，应在 P9 之前完成独立 spike（块模型 + TextLayout 自绘 + IME + 跨块选区 + 剪贴板），通过后再写详细规格；P9 由 ExpandableCard（P4）、P6 / P7 / P25 由 Calendar（P1）、P10 由 Tooltip（P1）先行落地；工具栏与块菜单复用 S8 锚定浮层，菜单表面风格与 DropdownMenu（P5）一致。

## 6. 相关文档
- [组件总清单](../overview.md)
- [组件规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md)
- [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)
- [字段通用结构](../../architecture/forms-and-fields.md)
- [路线图](../../roadmap/overview.md)
- Arc 原页面见第 2 节表格中的链接。
