# 本地化与格式化

> Arc 组件的内置文案如何本地化（AtomUI 语言目录 + XLIFF），以及数字、货币、日期的格式化服务（S11）。

## 1. 语言目录

AtomUI 的本地化机制（以 AtomUI 仓库 `src/AtomUI.Desktop.Controls/DatePicker/Localization/` 为例）：

- 每个有内置文案的组件，在其目录下建 `Localization/`：
  - `XxxLangResourceKind.cs`：`[LanguageCatalog] public enum`，成员名即文案 id；
  - `en-US.xlf`、`zh-CN.xlf`、`zh-TW.xlf`：XLIFF 2.1。`file id` 为枚举全名，`unit id` 为成员名。
- 构建时，AtomUI 的本地化 targets 自动收集 `**/Localization/**/*.xlf`，模块 id 取 PackageId 或 AssemblyName。生成器产出 `XxxLangResourceExtension` 与语言模块注册；包入口 `UseArc()` 执行时自动注册语言模块。
- AXAML 用法：`{arc:CopyButtonLangResource Copied}`。C# 用法：`Application.GetLocalizer()?.Get(CopyButtonLangResourceKind.Copied)`。
- 打包时会自动生成 `buildTransitive/<PackageId>.props`，并把 `en-US.xlf` 放入 contentFiles。不要手写这个 props 文件。

Arc 的约定：

| 项 | 约定 |
|---|---|
| 枚举命名空间 | `TKCode.Arc.Localization`（映射到 arc xmlns） |
| 语言 | en-US（源语言，必须完整）、zh-CN、zh-TW；pt-BR 列入计划（AtomUI 自身有 pt-BR 语言包） |
| 文案风格 | 英文遵循 Arc 规则：句子式大小写，按钮用"动词 + 对象"，不用破折号；中文译文简洁、与 AtomUI 现有术语一致 |
| 占位符 | 使用 `CompositeFormat` 语法；所有语言的占位符索引集合必须与英文一致（生成器校验） |

## 2. 覆盖要求（ATOMUIREG014）

- 应用在 `UseLanguages(...)` 中声明的每种非英语语言，都必须被所有已启用模块完整覆盖，否则编译报 ATOMUIREG014。
  - 回退规则：`zh-CN` 可以用 `zh-Hans` 补齐，但不能只靠英文补缺。
- 因此 Arc 每新增一条文案，必须同时提供 zh-CN 与 zh-TW 的译文。
- 应用若声明了 Arc 未覆盖的语言（如 pt-BR），在 Arc 提供语言包之前会编译失败。Arc 的发布说明需写明已覆盖的语言。
- S0.4 验证：删掉 zh-TW 中的一条文案时，消费应用触发 ATOMUIREG014。

## 3. 需要本地化的内容

| 类型 | 示例 |
|---|---|
| 内置按钮与提示文案 | CopyButton 的 "Copy" / "Copied"，PasswordField 的 "Show password" / "Hide password"，ExpandableText 的 "Read more" / "Show less" |
| 无障碍名称 | 图标按钮的默认名称（如关闭按钮 "Close"） |
| 状态与播报 | "3 results"、"Saved" |
| 日历与时间 | 月份与星期名称（优先取自 `CultureInfo`，仅补充 Arc 特有文案，如 "Today"） |
| 国家与地区 | PhoneInput 的国家名称：取自 .NET 区域数据（`RegionInfo`），找不到时用英文 |

应用传入的业务文案（标签、描述、错误）不在 Arc 的本地化范围内。

## 4. 格式化服务（S11）

```csharp
public static class ArcFormatting
{
    public static FormattedParts FormatNumber(double value, NumberFormatOptions options, CultureInfo? culture = null);
    public static FormattedParts FormatCurrency(decimal value, string currencyCode, CultureInfo? culture = null);
    public static string FormatCompact(double value, CultureInfo? culture = null);      // 1.2K、3.4M
    public static string FormatRelativeDate(DateTimeOffset value, DateTimeOffset now, CultureInfo? culture = null);
}
```

- **分段输出**（`FormattedParts`）：数字、分组符、小数点、货币符号、百分号分别成段，对应 Intl 的 `formatToParts`。RollingDigits（P14）只让数字段滚动，符号段用 P2 / P3 切换。
- **缓存**：按（文化, 选项）缓存格式化器，避免每帧创建。
- **数字风格**（Arc copy 规则）："$1,240"、"12.5%"、"3 of 5 seats"。日期风格："Sep 24"、"Tomorrow at 9:00"。
- **文化来源**：默认使用 AtomUI 当前语言对应的 `CultureInfo`，组件也可以单独指定。
- **货币**：MoneyInput 输出最小货币单位（如分）以避免浮点误差；小数位数取自货币代码。
- **电话号码**：PhoneInput 内置约 50 个国家的格式规则（移植自 Arc 的内联数据，MIT），输出 E.164；完整的号码库不在范围内。

## 5. 文化数据与 AOT

- .NET 没有 `Intl.NumberFormat.formatToParts` 的等价物，`FormattedParts` 由 S11 自行切分格式化结果实现。
- Arc 的格式化依赖 .NET 的文化数据（ICU）。使用 Arc 的应用**不能**启用 `InvariantGlobalization`，否则数字、货币、日期都会退化为不变文化。这一要求写入 README 与包说明。
- Windows 10 1903 及以上自带 ICU；NativeAOT 发布时保持默认的 ICU 加载方式。需要 IANA 时区名的场景（例如以时区为选项的示例）同样依赖 ICU。

## 6. 时区与"今天"

- Calendar、DateRangePicker、DatePicker 共享 `ArcToday` 服务：提供当天日期，在本地午夜翻转时通知订阅者（对应 Arc 的 `use-today`）。
- Timeline 的相对时间约每分钟刷新一次（`PausableTimer`）。
