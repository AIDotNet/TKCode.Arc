---
title: 屬性面板
summary: 以「標籤 + 編輯器」的列編輯一個或多個物件的屬性，依分類分組，支援搜尋、巢狀值和清單。
source-hash: 6491735f049a
---

<Demo name="Basic" />

## 何時使用

`PropertyGrid` 像設計、建模軟體的「性質」面板那樣編輯物件的屬性：每個屬性一列，左邊是名稱，右邊是編輯器；各列依分類分組，選取列的說明顯示在下方。屬性來自物件本身、而不是手動排版的表單時使用它。

- **CAD 或建模軟體的性質面板**：選取一段圓弧，編輯它的圖層、顏色、圓心和半徑；選取多個圖形，編輯它們共有的屬性。
- **設定與組態編輯器**：附有分類和說明的設定類別，不寫表單就能得到編輯介面。
- **視覺化設計工具**：編輯畫布上某個控制項的 Avalonia 屬性。

## 何時不用

- **只填寫一次的表單**（註冊、結帳）：自行排版 `Input`、`Select` 等欄位，把標籤、說明文字和驗證放在合適的位置。
- **欄位相同的大量資料**：使用 `Table` 或 `DataGrid`，方便跨列比較。
- **只有幾個選項**：在 `Card` 裡放幾個 `Switch` 或 `Select`，比屬性面板更清楚。

## 用法

為類別加上 `[PropertyGridModel]`，再把物件交給屬性面板。Foundation 產生器（隨 `TKCode.Arc.Foundation` 發佈）在編譯時讀取這個類別，為每個公開屬性註冊強型別的讀寫委派，執行時不使用反映，因此支援 Native AOT 和修剪。

```csharp
using System.ComponentModel;
using TKCode.Arc;

[PropertyGridModel]
public sealed class Arc
{
    [Category("General"), Description("The layer the shape belongs to.")]
    public string Layer { get; set; } = "Walls";

    [Category("General"), DisplayName("Line type")]
    public LineType LineType { get; set; }

    [Category("Geometry"), PropertyGridOptions(Minimum = 0, FractionDigits = 4)]
    public double Radius { get; set; } = 1624.1491;

    [Category("Geometry")]
    public double Area => Math.PI * Radius * Radius;   // no setter: read-only
}

properties.SelectedObject = new Arc();
```

屬性面板讀取 `System.ComponentModel` 的 `[Category]`、`[DisplayName]`、`[Description]`、`[ReadOnly]` 和 `[Browsable(false)]`，以及 DataAnnotations 的 `[Display(Name, GroupName, Description)]`。`[PropertyGridOptions]` 提供屬性面板自己的選項：`Order`，數字編輯器的 `Minimum`、`Maximum`、`Step` 和 `FractionDigits`，用於自訂編輯器的 `EditorKey`，列的樣式類別 `Classes`，以及 `Expand`。

每種型別使用 Small 尺寸的 Arc 欄位作為編輯器：文字用 `Input`，數字用 `NumberField`，`bool` 用 `Switch`，列舉用 `Select`（成員上的 `[Description]` 是顯示文字），`Color` 用 `ColorPicker`，`DateOnly`、`DateTime` 和 `DateTimeOffset` 用 `DatePicker`。唯讀屬性以文字顯示。文字在按 Enter 或焦點離開時送出，按 Escape 還原原值。物件觸發 `PropertyChanged` 時各列會重新讀取，所以由其他屬性算出的值（上例的面積）會隨每次編輯更新；不發出通知的物件請呼叫 `Refresh()`。

### 多個物件

`SelectedObjects` 同時編輯多個物件。各列是它們共有的屬性（名稱和型別都相同）；取值不一致時顯示 `MixedValueText`（「Varies」），編輯時寫入所有物件。像 CAD 的性質面板一樣，可以在屬性面板上方放一個選擇器，把選取範圍縮小到某一類圖形。

<Demo name="MultiSelect" />

### 巢狀值和清單

屬性的型別也標了 `[PropertyGridModel]`（或在程式碼中註冊過）時，該列帶有展開箭頭，可以展開成它自己的列。結構會被複製、修改，再逐層寫回它的擁有者，直到選取的物件，所以 `Point` 這類屬性和其他屬性一樣編輯。清單（`IList`，包括陣列）展開成 `[0]`、`[1]`……列；元素本身是模型時還能繼續展開。清單可增長、且產生器能建立元素（實值型別、字串，或有公開無參數建構函式的類別）時，清單列有新增按鈕，每個元素有移除按鈕。

<Demo name="Lists" />

### 自訂編輯器和樣式

`Editors` 為部分列替換內建編輯器。`PropertyGridEditor` 先依 `EditorKey` 比對（以 `[PropertyGridOptions(EditorKey = "...")]` 設定），再依 `PropertyName`，最後依 `PropertyType`。範本的資料內容是 `PropertyGridRow`：繫結 `Value`、`IsReadOnly`、`IsMixed` 和 `DisplayText`。

每一列是一個 `PropertyGridItem` 控制項。可以依虛擬類別（`:readonly`、`:mixed`、`:expanded`、`:nested`、`:list-item`、`:invalid`、`:selected`）、依 `PropertyName`，或依 `[PropertyGridOptions(Classes = "...")]` 加上的樣式類別設定樣式。

<Demo name="CustomEditors" />

### 在程式碼中註冊型別

無法加上屬性標記的型別（如 `System.Numerics.Vector3` 或其他函式庫的型別）在啟動時註冊一次。`Set` 寫入類別；`Update` 傳回結構或不可變型別修改後的副本，由屬性面板寫回。

```csharp
PropertyGridTypes.Register<Vector3>(t =>
{
    t.Property("X", v => v.X).Update((v, x) => { v.X = x; return v; });
    t.Property("Y", v => v.Y).Update((v, y) => { v.Y = y; return v; });
    t.Property("Z", v => v.Z).Update((v, z) => { v.Z = z; return v; });
    t.Summary(v => $"{v.X:0.##}, {v.Y:0.##}, {v.Z:0.##}");
});
```

### Avalonia 控制項

Avalonia 物件不需要註冊：它的 styled 和 direct 屬性會被列出，依宣告它們的型別分組。以 `PropertyFilter` 保留要顯示的屬性。

<Demo name="Controls" />

### 版面選項

`IsCategorized` 在分類標題和依字母排序的清單之間切換，`IsSearchVisible` 和 `IsDescriptionVisible` 顯示或隱藏搜尋框和說明區，`IsReadOnly` 讓所有值都以文字顯示。拖曳標籤和編輯器之間的分隔線可以改變 `LabelWidth`。

<Demo name="Options" />

### 變更和錯誤

每次編輯後都會觸發 `PropertyValueChanged`，引數包含該列、各物件原本的值和寫入的值（新增或移除元素後，是清單所在的列和清單本身）。setter 擲回例外或文字無法轉換時，值保持不變，該列變為 `:invalid`，`PropertyGridRow.ErrorMessage` 說明原因。

```csharp
properties.PropertyValueChanged += (_, e) =>
    undo.Push(e.Row.Path, e.OldValues, e.NewValue);
```

## API 參考

<!-- api: PropertyGrid -->

<!-- api: PropertyGridItem -->

<!-- api: PropertyGridCategoryItem -->

<!-- api: PropertyGridEditor -->

<!-- api: PropertyGridOptionsAttribute -->

## 協助工具

- 每一列以標籤命名，以屬性說明作為描述；可展開的列會回報是否已展開，每個編輯器以所在列的標籤命名。
- 清單的新增和移除按鈕分別以 `AddItemText` 和 `RemoveItemText` 命名。
- 寫入失敗時，標籤變為危險色，並以帶有 `ErrorMessage` 的提示顯示原因，而不只依賴顏色。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序在列、分類標題和編輯器之間移動。 |
| ArrowDown / ArrowUp | 在列或分類標題上：移到下一個或上一個。 |
| Home / End | 在列或分類標題上：移到第一個或最後一個。 |
| ArrowRight | 展開列或分類；在已展開的列上，移到第一個子列。 |
| ArrowLeft | 收合列或分類；在已收合的列上，移到父列或所屬分類。 |
| Enter / F2 | 在列上：進入它的編輯器。在分類標題上，Enter 和 Space 收合或展開。 |
| Escape | 在編輯器中：還原正在輸入的值，然後回到所在列。 |

## 動態效果

- 列和分類的箭頭在展開或收合時以 Arc 的彈簧轉動；滑鼠停留和選取的底色在 120 ms 內淡入。
- 編輯器保留各自的動態效果（數字輸入框的數字、選擇器的選單、取色器的面板）。
- 減少動態效果時箭頭直接轉動、不使用彈簧；關閉動態效果時沒有任何動畫。

## 響應式

- `PropertyGrid` 沒有斷點。標籤欄寬為 `LabelWidth`（預設 140 px），編輯器佔用其餘寬度；標籤欄不會把編輯器壓到 96 px 以下，標籤欄本身也不會窄於 64 px。
- 它依自身寬度（`Bounds.Width`）計算，而不是視窗寬度；`MinWidth` 為 240 px。
- 標籤與值以省略號截斷（`CharacterEllipsis`），關閉橫向捲動，說明區最多兩行（`MaxLines`），因此很長的值不會把表格橫向推開。
- 列高為 40 px（`RowHeight`）。沒有任何功能依賴懸停，但集合列上的新增與刪除按鈕是 28 px 見方（`ActionSize`）。在手機上請調低 `LabelWidth`（最低 64 px），為編輯器留出更多空間。

## 相關元件

- `Input`、`NumberField`、`Switch`、`Select`、`ColorPicker` 和 `DatePicker`：內建編輯器。
- `TreeTable` 和 `TreeView`：呈現資料的階層，而不是單一物件的屬性。
- `Accordion`：在可展開收合的標題下分組內容，與屬性面板的分類類似。
