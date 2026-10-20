# 路线图

> 110 个组件分 10 个阶段（P0–P9）实现。阶段的划分原则、每阶段的组件与新增基础能力、进入与退出标准、里程碑版本与风险登记。组件归属以 [组件总清单](../components/overview.md) 为准。

## 1. 原则

1. **先文档后代码**：每个阶段开始前，先编写并评审本阶段组件的详细规格（[规格模板](../components/spec-template.md)）。
2. **依赖自下而上**：被其他组件依赖的组件先做；Arc 源码中的硬依赖必须排在更早的阶段。
3. **原语随首个使用者落地**：动效原语与服务在首个需要它的组件中实现并验收，后续复用（见 [原语目录](../architecture/motion/primitives-catalog.md)）。
4. **每个阶段都可发布**：阶段结束时组件已在 Gallery 中展示，测试通过，可打包。
5. **先验证再扩张**：P0 用 Button 与 Switch 两个试点组件验证整条链路（Token、动效、注册、打包、AOT、Gallery），通过后才进入 P1。

## 2. 阶段总表

| 阶段 | 主题 | 数量 | 组件（按实现顺序） | 新增的原语与服务 |
|---|---|---|---|---|
| P0 | 基础设施与试点 | 2 | Button、Switch | S1、S2、S3（PresenceHost）、S4、S5、S7；P1、P2、P5、P21、P24、P29；ArcDesign Token；Lucide 图标；字体；Gallery 骨架 |
| P1 | 被依赖的组件 | 6 | Tooltip、Avatar、AnimatedCounter、TextMorph、CopyButton、Calendar | S6、S8（锚定）、S9、S11、S12；P3、P4、P7、P10、P14、P15、P18、P25、P26 |
| P2 | 轻依赖组件 | 7 | ThemeSwitcher、FloatingButtonGroup、AvatarGroup、BillingToggle、MetricCard、CodeBlock、DatePicker | P6、P19、P27、P28；FieldFrame（DatePicker 触发器）；页面过渡辅助（ThemeSwitcher） |
| P3 | 原子组件与简单状态 | 19 | Badge、Checkbox、RadioGroup、RadioCards、SegmentedControl、Progress、Skeleton、EmptyState、Alert、IconMorph、ActionButton、SearchField、Input、Textarea、PasswordField、UsageMeter、TextReveal、InViewTitle、TextShimmer | P16、P20、P23；FieldFrame 的文本输入部分（Input） |
| P4 | 展开与导航 | 8 | Accordion、Tabs、Breadcrumb、Pagination、ScrollArea、ExpandableText、ExpandableCard、Stepper | P9、P30 |
| P5 | 浮层与菜单 | 13 | Popover、HoverCard、DropdownMenu、SplitButton、ButtonGroup、ContextMenu、Select、Dialog、Card、Drawer、BottomSheet、UserMenu、Reactions | S8（模态）、S10；P8、P11、P12；MenuSurface |
| P6 | 输入与选择 | 17 | Combobox、MultiSelect、TimePicker、MorphSelect、PhoneInput、DateRangePicker、ColorPicker、TagInput、ChipGroup、MentionInput、ShortcutRecorder、InlineEdit、NumberField、MoneyInput、PasswordStrength、ExpandingSearch、FilterToolbar | P13 |
| P7 | 手势、物理、计时与反馈 | 17 | Slider、ElasticSlider、ScrubInput、HoldToConfirm、ConfirmMorph、ExpandingButtonGroup、SwipeActions、ResizablePanels、ImageCompare、Carousel、CardStack、ReorderableList、Toast、ToastStack、AnnouncementBar、Countdown、CookieConsent | P22；ToastManager；偏好存储 |
| P8 | 图表 | 12 | Sparkline、Gauge、BarChart、LineChart、DonutChart、ActivityHeatmap、WaffleChart、SlopeChart、BrushChart、Streamgraph、Ridgeline、Treemap | P17；[图表基础](../components/charts/chart-foundation.md) |
| P9 | 数据、动态与重型编辑器 | 9 | SortableDataTable、TreeView、JsonViewer、Timeline、CommentThread、ChatThread、FileDropzone、SignaturePad、RichTextEditor | 文件选择与拖放 |
| | **合计** | **110** | | |

## 3. 依赖约束

| 依赖方（阶段） | 被依赖方（阶段） |
|---|---|
| ThemeSwitcher（P2） | Button（P0） |
| FloatingButtonGroup（P2） | Tooltip（P1） |
| BillingToggle（P2）、MetricCard（P2） | AnimatedCounter（P1） |
| DatePicker（P2） | Calendar（P1） |
| AvatarGroup（P2） | Avatar（P1） |
| CodeBlock（P2） | CopyButton（P1） |
| ColorPicker（P6） | TextMorph（P1） |
| CookieConsent（P7） | Button（P0）、Switch（P0） |

共享表面（软依赖）：

- DropdownMenu 的 `MenuSurface` 被 SplitButton、ButtonGroup（P5）与 SwipeActions（P7）复用；
- Dialog 的模态表面被 Drawer、BottomSheet、Card 快速预览（P5）复用；
- Popover 被 HoverCard（P5）与 Reactions（P5）复用。

同一阶段内按第 2 节的顺序实现。

## 4. 阶段的进入与退出标准

**进入标准**（每个阶段相同）：

1. 上一阶段已满足退出标准。
2. 本阶段全部组件的详细规格已评审通过。
3. 本阶段需要的新原语与服务的 API 已在架构文档中定义。

**退出标准**（每个阶段相同）：

| 项 | 标准 |
|---|---|
| 实现 | 本阶段组件全部实现；总清单状态更新为"已实现" |
| 测试 | 本阶段相关测试通过；临时测试已按 [测试规范](../engineering/testing.md) 处理 |
| 主题 | 明 / 暗 × neutral / 一个彩色 accent 四种组合下的视觉对照通过 |
| 动效 | Full / Reduced / Off 三个等级都验证过；关键动效与 Arc 录屏对照一致 |
| 键盘与可访问性 | 规格中的键盘表全部通过；Peer 名称与播报正确 |
| 构建 | net10.0 与 net11.0 零警告构建；NativeAOT 发布的 Gallery 能运行 |
| Gallery | 每个组件都有展示页 |
| 文档 | 规格中的未决问题已关闭或转入下一阶段；本目录中失效的设计结论已回写 |

P0 另有专门的 spike 清单，见 [phase-0.md](phase-0.md)。

## 5. 里程碑版本

| 版本 | 内容 | 说明 |
|---|---|---|
| 0.1.0-preview | P0–P2（15 个组件） | 基础设施稳定；对外预览 |
| 0.2.0-preview | + P3–P4（42 个组件） | 原子组件、展开与导航 |
| 0.3.0-preview | + P5（55 个组件） | 浮层与菜单 |
| 0.4.0-preview | + P6（72 个组件） | 输入与选择 |
| 0.5.0-preview | + P7（89 个组件） | 手势、物理、计时与反馈 |
| 0.6.0-preview | + P8（101 个组件） | 图表 |
| 1.0.0 | + P9（110 个组件），稳定 API | 在 .NET 11 GA 之后发布 |

每个版本都标注对应的 AtomUI 版本（见 [打包与发布](../engineering/packaging-and-release.md)）。

## 6. 风险登记

| 风险 | 影响 | 可能性 | 应对 |
|---|---|---|---|
| 载体 Token 是推断出的模式，AtomUI 升级可能改变行为 | 高 | 中 | S0.2 验证 + 常驻契约测试；升级 AtomUI 时先跑契约测试；失败则重开 ADR 0005 |
| AtomUI 版本锁定，需要随 AtomUI 发版重建 | 中 | 高 | CI 支持按 AtomUI 版本参数构建；发布说明列出版本对应表 |
| net11 不在 AtomUI 验证范围内；.NET 11 仍为 RC | 中 | 中 | S0.1 / S0.5 验证；GA 后复验；必要时暂时只发布 net10 资产 |
| 动效性能：宽高弹簧、背景模糊、图表形变 | 中 | 中 | S0.6 基准；组件规格中写动效预算；`ReduceTransparency` 退化 |
| RichTextEditor：Avalonia 没有 contentEditable，需要自建块编辑器（选区、IME、撤销、HTML / Markdown 输出） | 高 | 高 | 排在最后（P9）；开工前单独做技术预研并评估范围裁剪 |
| MentionInput 需要在 TextBox 中渲染内联标记 | 中 | 中 | P6 开工前预研：TextBox 叠加层与自定义 TextPresenter 两种方案 |
| AtomUI 公开 API 的破坏性变化 | 中 | 低 | 只用白名单 API（[AtomUI 集成](../architecture/atomui-integration.md)）；升级时检查 AtomUI 发版说明 |
| nuget.org 上 `AtomUI.` 包 ID 前缀可能已被保留 | 中 | 中 | 发布前确认前缀归属；必要时申请共同所有权或调整包 ID（[打包与发布](../engineering/packaging-and-release.md)） |
| Arc 上游变化（组件改版、新增） | 低 | 中 | 以 [站点快照](../components/arc-catalog-snapshot.md) 为基线定期对比 |
| 许可合规（误用 Pro 内容） | 高 | 低 | 只读取免费组件；评审时检查规格来源 |

## 7. 状态字段

组件状态记录在 [组件总清单](../components/overview.md) 的"状态"列：

| 状态 | 含义 |
|---|---|
| 规划中 | 已列入清单，尚无规格 |
| 规格已评审 | 详细规格通过评审，可以开工 |
| 实现中 | 正在开发 |
| 已实现 | 代码、测试、Gallery 完成 |
| 已验收 | 视觉、动效、键盘、可访问性验收通过 |
