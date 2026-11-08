---
title: 列舉選擇器
summary: 從列舉的成員中選擇：一般列舉顯示為單選項，[Flags] 列舉顯示為按位元組合的核取方塊，成員資訊在編譯期產生，不使用反射。
source-hash: 061ff55675fd
---

<Demo name="Basic" />

## 何時使用

`EnumSelector` 把一個列舉變成一組可見的選項。指定列舉並繫結 `SelectedEnum`，它會列出成員、顯示它們的 `[Description]` 文字，並讓值與選項保持一致。一般列舉顯示為 `RadioGroup`，`[Flags]` 列舉顯示為一組 `Checkbox`，勾選的成員按位元組合。

- **設定頁**：主題、密度或預設檢視，檢視模型裡本來就是一個列舉。
- **權限與能力**：讀取、寫入、執行這樣的 `[Flags]` 列舉，包括 `None` 成員和 `ReadWrite` 這樣的組合成員。
- **篩選與狀態**：從一個簡短、固定的清單中選擇狀態或優先順序。

## 何時不用

- **很長的清單**：超過大約八個成員會佔用太多空間，改用 `Select` 或 `Combobox`。
- **選項不是列舉**：來自資料的選項請用 `RadioGroup`、`ChipGroup` 或 `MultiSelect`。
- **帶說明、價格或圖示的選項**：使用 `RadioCards`，或帶 `RadioOption` 的 `RadioGroup`。
- **立即生效的開關**：單一 `bool` 用 `Switch` 或 `Checkbox`。

## 用法

為列舉加上 `[EnumMetadata]`。隨 TKCode.Arc.Foundation 一起發佈的產生器會在編譯期讀取它，並在組件載入時把成員、`[Description]`（或 `[Display(Name = ...)]`）文字和 `[Flags]` 標記註冊到 `EnumTypes`。執行時不使用反射，因此選擇器相容 Native AOT 與修剪。

```csharp
using System.ComponentModel;
using TKCode.Arc;

[EnumMetadata]
public enum Priority
{
    [Description("Low")] Low,
    [Description("Medium")] Medium,
    [Description("High")] High,
    [Description("Critical")] Critical
}
```

接著設定 `EnumType` 並繫結 `SelectedEnum`（預設為雙向繫結）：

```xml
<arc:EnumSelector xmlns="https://github.com/avaloniaui"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                  xmlns:demos="using:TKCode.Arc.Gallery.Demos"
                  Header="Priority"
                  EnumType="{x:Type demos:DemoPriority}"
                  SelectedEnum="{x:Static demos:DemoPriority.Medium}"
                  Columns="2" />
```

設定了 `SelectedEnum` 時可以省略 `EnumType`：選擇器會列出這個值的型別。標記了 `[Browsable(false)]` 的成員不列出；兩個成員值相同時只列出第一個。沒有標記（也沒有在程式碼中註冊）的列舉不顯示任何選項。

### 旗標列舉

`[Flags]` 列舉顯示為核取方塊。勾選一個成員會設定它的位元，取消勾選會清除它的位元，因此：

- 勾選 `ReadWrite` 這樣的組合成員會同時勾選 `Read` 和 `Write`，取消勾選則把兩者都清除；
- 組合成員的位元只設定了一部分時顯示短橫（不確定狀態），點擊它會補齊其餘的位元；
- `None` 這樣的零值成員只在沒有任何位元時才處於勾選狀態，勾選它會清除所有旗標，勾選其他任何成員都會取消它；
- 沒有被任何列出成員命名的位元會被保留。

`byte`、`short`、`int`、`long` 及其無號型別都能正確處理，包括符號位元和 `ulong` 的最高位元。

<Demo name="Flags" />

### 顯示模式

`DisplayMode` 預設為 `Auto`：一般列舉顯示為單選項，`[Flags]` 列舉顯示為核取方塊。`EffectiveDisplayMode` 是實際使用的模式，`HasFlags` 表示列舉是否帶 `[Flags]`。

- `Radio` 對任何列舉都顯示單選項。此時 `[Flags]` 列舉一次只取一個成員，組合成員也算一個。
- `Check` 對任何列舉都顯示核取方塊。一般列舉仍然只取一個成員：勾選一個會取消其他的，因此值永遠不會是列舉沒有定義的組合。

<Demo name="DisplayModes" />

### 顯示名稱

`UseDisplayNames`（預設為 true）顯示每個成員的顯示文字；設為 `False` 則顯示程式碼中的名稱。文字立即切換，已選項保持不變。

<Demo name="DisplayNames" />

要翻譯這些文字，請指定一個語言目錄。`Priority` 的成員 `Low` 的文字就是目錄成員 `PriorityLow`（`KeyPrefix` 加成員名稱；前綴預設是列舉名稱），並隨 `ArcLanguage.Set` 切換。目錄中沒有對應項目的成員保留它的 `[Description]`，建置時會發出警告（ARCF037）。

```csharp
using TKCode.Arc;
using TKCode.Arc.Localization;

[LanguageCatalog]
public enum AppLangResourceKind
{
    PriorityLow,
    PriorityMedium,
    PriorityHigh,
    PriorityCritical
}

[EnumMetadata(LanguageCatalog = typeof(AppLangResourceKind))]
public enum Priority { Low, Medium, High, Critical }
```

### 版面配置

選項放在一個均勻格線中。`Columns` 設定欄數，`Rows` 設定列數；只設定其中一個時另一個自動計算，兩個都不設定時選項排成一欄。`RowSpacing` 和 `ColumnSpacing` 設定間距（單選項為 8 和 16，核取方塊為 0 和 16，因為核取方塊 44 px 的點擊區域已經拉開了距離）。其他排列方式可以為 `ItemsPanel` 指定自己的面板，例如依可用寬度換行的 `WrapPanel`。

<Demo name="Layout" />

其他組件中的列舉（例如上面的 `DayOfWeek`）以專案中的組件屬性來描述：

```csharp
[assembly: TKCode.Arc.EnumMetadata(typeof(System.DayOfWeek))]
```

### 繫結到檢視模型

`SelectedEnum` 的型別是 `object?`，因此可以繫結到任何列舉型別的屬性，包括可為 null 的型別。選擇會寫入屬性，檢視模型的新值（包括組合旗標）會更新選項。設為 null 會清除選擇。`SelectedEnumChanged` 會回報每次變化的舊值和新值。

<Demo name="Binding" />

### 自訂標籤

`ItemTemplate` 根據 `EnumSelectorItem` 建立每個選項的標籤：`Text` 是顯示的文字，`Name` 是成員名稱，`Value` 是列舉值，`Member` 是完整的成員描述。

<Demo name="ItemTemplate" />

### 在程式碼中註冊列舉

`EnumTypes.Register` 接受手寫的描述，例如來自無法加標記、又不想在組件屬性中列出的程式庫的列舉：

```csharp
EnumTypes.Register(new EnumTypeInfo(typeof(Size), isFlags: false,
    toBits: static v => (ulong)(int)(Size)v,
    fromBits: static b => (Size)(int)b,
    members:
    [
        new EnumMemberInfo("Small", Size.Small, 0) { DisplayName = "S" },
        new EnumMemberInfo("Large", Size.Large, 1) { DisplayName = "L" }
    ]));
```

## API 參考

<!-- api: EnumSelector -->

<!-- api: EnumSelectorItem -->

<!-- api: EnumSelectorDisplayMode -->

<!-- api: EnumSelectorChangedEventArgs -->

<!-- api: EnumMetadataAttribute -->

<!-- api: EnumTypes -->

## 無障礙

- 選擇器是一個群組；字串型別的 `Header` 是它的無障礙名稱，也是內部單選群組的名稱。
- 單選項的行為與 `RadioGroup` 相同：整組只佔一個 Tab 停駐點，方向鍵、Home 和 End 移動選擇。
- 核取方塊的行為與 `Checkbox` 相同：每個都是一個 Tab 停駐點，空白鍵切換；部分設定的組合成員回報混合狀態。

## 動態效果

- 選擇器本身不加入動態效果：單選醒目提示的滑動和圓點的彈出與 `RadioGroup` 相同，勾號的繪製和變為短橫與 `Checkbox` 相同。
- 來自檢視模型的值以同樣的方式移動選項；減少動態效果或關閉動態效果時遵循這些控制項的規則。

## 響應式

- `EnumSelector` 沒有斷點。均勻格線把得到的寬度平均分給各欄，比欄寬更長的標籤會換行。
- 在窄螢幕上請減少 `Columns`，或把 `WrapPanel` 用作 `ItemsPanel`，讓選項流到新的一行。
- 單選列和核取方塊的點擊區域保持適合觸控的高度（核取方塊為 44 px）。

## 相關元件

- `RadioGroup`：從自己列出的選項中選擇一個。
- `Checkbox`：單一的二元選擇。
- `ChipGroup`：以標籤形式選擇篩選條件。
- `Select`：在選單中從較長的清單中選擇。
- `PropertyGrid`：與物件的其他屬性一起編輯列舉屬性，同樣使用編譯期產生的描述。
