---
title: 穿梭框
summary: 在兩個清單之間移動項目：逐一、依選取批次或全部移動，支援全選與搜尋，並直接修改繫結的集合。
source-hash: 14a94ece2111
---

<Demo name="Basic" />

## 何時使用

`TransferBox` 把兩個清單並排放置：左邊是可選的項目，右邊是已選的項目，中間的按鈕把選取的項目從一個清單移到另一個清單。兩個清單始終可見，使用者能同時看到已經選了什麼、還剩什麼，並且可以用常見的清單選取鍵一次選取多項。移動會直接修改兩個繫結的集合，因此檢視模型始終持有最新結果。

- **角色與權限指派**：管理後台為某個角色授予權限。可用權限在左側，已授予的在右側，列範本在每個名稱下顯示一行說明。
- **專案與團隊成員**：專案設定頁把人員指派到專案中。開啟搜尋後，管理者不必捲動就能在兩百人的團隊中找到「Grace」。
- **表格的可見欄**：在「自訂欄位」對話方塊中，把欄在「隱藏」和「可見」之間移動。可見欄集合就是表格讀取的集合，因此對話方塊套用後移動立即生效。
- **資源分組**：維運工具把伺服器或區域移入一個部署群組，常常用 Shift 或 Ctrl+A 一次移動很多項。
- **從已知清單中選出子集的設定**：通知管道、同步資料夾或啟用的外掛，完整清單很長但有限。

## 何時不用

- **幾個能放在一行裡的選項**：用 `ChipGroup`，它把每個選項顯示為一個標籤，不需要第二個清單。
- **在表單欄位中選取子集**：用 `MultiSelect`，它把選取保留在精簡的欄位中，只在需要時開啟清單。
- **每項都有自己標籤與驗證的「是／否」回答**：用多列 `Checkbox`。
- **已選項目的順序很重要**：已選項目用 `ReorderableList`，因為穿梭框把移入的項目加在最後，不會重新排序。
- **階層資料，例如資料夾或部門**：用帶核取方塊的 `TreeView`；穿梭框只列出平面的項目。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TransferBox SourceHeader="Available languages"
                     TargetHeader="Selected languages"
                     SourceItems="{Binding AvailableLanguages}"
                     TargetItems="{Binding SelectedLanguages}"
                     Height="340" />
</UserControl>
```

`SourceItems` 是左側清單，`TargetItems` 是右側清單。在一個清單中選取列，然後點選指向另一側的箭頭按鈕、按 Enter，或按兩下某一列。選取的項目離開一個集合，依它們在原清單中的順序加到另一個集合的最後。移動的是同一個物件參考，不會複製任何內容。

### 結構

1. **面板**：兩個帶 1 px 邊框、控制項圓角（`RadiusControl`，18 px）與靜止陰影的表面。每個面板至少 180 px 寬（`PanelMinWidth`），兩者平分剩餘寬度。
2. **標題列**：全選框、標題（`SourceHeader`、`TargetHeader`，中等字重）與計數。計數顯示列數；有選取時顯示「已選／列數」。
3. **搜尋欄**（`IsSearchable`）：標題列下方的小型 `SearchField`，篩選所在清單的列。
4. **列**：至少 36 px 高，圓角 12 px，距面板邊緣 6 px。滑鼠停留時填入柔和的表面色；選取的列使用淺強調色填滿，並顯示強強調色的勾號，因此狀態不只依賴顏色。
5. **空狀態**：清單沒有列時置中顯示的淡化文字（`SourceEmptyContent`、`TargetEmptyContent`），搜尋文字隱藏了所有列時顯示 `NoMatchesText`。
6. **移動按鈕**：面板之間 36 px 的方形次要按鈕。設定 `IsMoveAllVisible` 後，在它們的上方與下方出現「全部移動」按鈕。沒有可移動的內容時按鈕為停用狀態。

### 選取與移動

每個清單的選取方式與清單方塊相同：按一下選取一列，Ctrl+按一下加入或移除一列，Shift+按一下選取一個範圍，Ctrl+A 選取整個清單。標題列中的全選框選取所有列；只選取部分列時顯示短橫線；所有列都已選取時再點選則清除選取。若一次只應移動一項，請設定 `SelectionMode="Single"`，此時全選框會隱藏。

```xml
<arc:TransferBox SourceHeader="Hidden columns"
                 TargetHeader="Visible columns"
                 SelectionMode="Single"
                 IsSelectAllVisible="False" />
```

其他列移走時，留下的列保持選取狀態，清單也保持捲動位置。

### 繫結到檢視模型

穿梭框會修改繫結的集合，因此資料歸檢視模型所有，檢視模型隨時可以讀取結果。每一側都使用 `ObservableCollection<T>`（或任何會回報變更的可修改 `IList`）。檢視模型所做的修改，例如載入項目或刪除一項，會立即顯示在清單中。

```csharp
public partial class LanguagesViewModel : ObservableObject
{
    public ObservableCollection<string> AvailableLanguages { get; } =
        ["C#", "C++", "Java", "JavaScript", "Python", "Rust"];

    public ObservableCollection<string> SelectedLanguages { get; } = ["Go", "TypeScript"];

    [RelayCommand]
    private Task SaveAsync() => _settings.SaveLanguagesAsync(SelectedLanguages.ToArray());
}
```

集合的規則：

- 兩個集合都必須已設定、可修改（不是唯讀、不是固定大小），並且是不同的物件。否則移動按鈕會停用、命令無法執行，直接呼叫 `MoveToTarget` 會擲回說明原因的 `InvalidOperationException`。
- 移動依位置從後往前移除項目，因此清單中其他位置的相等值（兩個「A」字串，或相等的記錄）不會被誤取。
- 若目標集合拒絕某個項目，例如把 `int` 加入字串集合，穿梭框會撤回已加入的項目，並在重新擲回例外之前把每個項目放回原位。移動不會讓某個項目同時從兩個清單中消失。
- 沒有變更通知的 `List<T>` 也能使用：穿梭框在自己的移動之後重新整理兩個清單，但看不到別處對這種清單所做的修改。

### 物件、文字與範本

項目可以是任意物件。`ItemTextSelector` 提供每個項目的文字：預設列顯示它，搜尋篩選比對它，螢幕閱讀器朗讀它。它是委派，因此不使用反射，也能在修剪下運作。

```csharp
members.ItemTextSelector = item => ((TeamMember)item!).Name;
```

需要更豐富的列時設定 `ItemTemplate`，兩個清單都會使用它。在範本上宣告 `x:DataType` 讓繫結被編譯，並繼續設定 `ItemTextSelector` 以支援搜尋與無障礙。

<Demo name="ItemTemplate" />

### 搜尋與全部移動

`IsSearchable` 為每個清單加入一個搜尋欄。文字在項目文字中任意位置比對，不分大小寫，並可以透過 `SourceSearchText` 與 `TargetSearchText` 雙向繫結。`IsMoveAllVisible` 加入「全部移動」按鈕；它們移動清單目前顯示的所有列，因此有搜尋文字時只移動相符的列。

<Demo name="Search" />

```xml
<arc:TransferBox SourceHeader="Team"
                 TargetHeader="Project members"
                 IsSearchable="True"
                 IsMoveAllVisible="True"
                 SearchPlaceholder="Search people"
                 SourceSearchText="{Binding MemberQuery}"
                 TargetEmptyContent="No one is on the project yet" />
```

### 命令、方法與事件

穿梭框把移動公開為命令，因此你自己的按鈕、選單項目或快速鍵都可以驅動它。命令的狀態跟隨選取與集合，繫結的按鈕會自動啟用與停用。

| 命令 | 方法 | 移動 |
|---|---|---|
| `MoveToTargetCommand` | `MoveToTarget()` | 左側清單中選取的列 |
| `MoveToSourceCommand` | `MoveToSource()` | 右側清單中選取的列 |
| `MoveAllToTargetCommand` | `MoveAllToTarget()` | 左側清單顯示的所有列 |
| `MoveAllToSourceCommand` | `MoveAllToSource()` | 右側清單顯示的所有列 |

這些方法回傳移動的項目數。`GetSelectedItems`、`SelectAll` 與 `ClearSelection` 讀取與修改一側的選取，`SourceSelectionCount`、`TargetSelectionCount`、`CanMoveToTarget` 與 `CanMoveToSource` 可以繫結。每次移動只引發一次 `ItemsTransferred`，帶有方向 `Direction` 與移動的項目 `Items`，不論一次移動了多少項。

<Demo name="Commands" />

```csharp
box.ItemsTransferred += (_, e) =>
{
    var verb = e.Direction == TransferDirection.ToTarget ? "Granted" : "Revoked";
    _audit.Write($"{verb}: {string.Join(", ", e.Items)}");
};
```

### 資料結構與效能

每個清單都是虛擬化的清單方塊，只建立可見的列，因此包含數千項的清單也能流暢捲動。搜尋文字每變更一次，就走訪一次整個集合。移動在原位修改集合，清單逐列更新，不會整體重建。

### 文案規範

- **標題**：依兩側所裝的內容命名，使用句子式大小寫的名詞：「Available permissions」與「Granted to editors」，而不是「左」「右」或「來源」「目標」。
- **空狀態**：說明空清單對目前工作代表什麼，例如「No one is on the project yet」，而不只是「無資料」。
- **搜尋預留位置文字**：說明搜尋的是什麼，例如「Search people」。
- **你自己按鈕的標籤**：動詞加受詞，例如「Show column」或「Grant permission」。

| 不要寫 | 改寫為 |
|---|---|
| Source | Available permissions |
| Target | Granted to editors |
| No data | No one is on the project yet |
| Search... | Search people |
| Move | Grant permission |

### 最佳做法

**建議**

- 把兩側繫結到檢視模型擁有的集合，並在檢視模型中儲存，而不是在控制項中。
- 物件項目即使有 `ItemTemplate` 也設定 `ItemTextSelector`，讓搜尋與螢幕閱讀器有文字可用。
- 清單可能裝下超出面板高度的項目時，開啟搜尋。
- 給穿梭框一個高度，或讓父層給它高度；清單在其中捲動。

**避免**

- 唯讀集合或陣列：穿梭框無法在其中移動項目，按鈕會保持停用。
- 把兩側繫結到同一個集合。
- 只為兩三個選項使用穿梭框；`ChipGroup` 或核取方塊呈現起來更省力。
- 依賴已選項目的順序；移入的項目加在最後。

### 常見問題

**為什麼移動按鈕是停用的？** 按鈕需要所在清單中有選取（「全部移動」需要有列），穿梭框必須為啟用狀態，兩個集合都必須已設定、可修改且不同。可以檢查 `CanMoveToTarget` 與 `CanMoveToSource`。

**能否依排序位置插入移入的項目？** 穿梭框把它們加在最後。若順序重要，請處理 `ItemsTransferred`，在檢視模型中排序集合。

**是否支援 `DisplayMemberPath`？** 不支援。字串路徑需要反射；文字用 `ItemTextSelector`，編譯繫結用帶 `x:DataType` 的 `ItemTemplate`。

**按住按鈕能否連續移動？** 不能。每次按下只移動一次目前的選取；需要移動很多項時用 Ctrl+A 或「全部移動」按鈕。

## API 參考

<!-- api: TransferBox -->

## 無障礙

- 每個清單都是以標題文字（`SourceHeader`、`TargetHeader`）命名的清單方塊，每一列都是清單項目，名稱取自 `ItemTextSelector` 提供的文字或範本中的文字。
- 全選框是核取方塊，名稱為「Select all: 」加清單標題；只選取部分列時為混合狀態。
- 移動按鈕的無障礙名稱與工具提示來自 `MoveToTargetLabel`、`MoveToSourceLabel`、`MoveAllToTargetLabel` 與 `MoveAllToSourceLabel`；你的標題不是「available」和「selected」時請設定它們。
- 選取的列除了強調色填滿還顯示勾號，因此選取狀態不依賴顏色。
- 以鍵盤取得焦點的列顯示焦點環顏色的焦點環。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在全選框、搜尋欄、清單與移動按鈕之間移動 |
| `Up` / `Down`、`Home` / `End`、`PageUp` / `PageDown` | 在清單的列之間移動 |
| `Shift` + 方向鍵 | 擴展選取 |
| `Ctrl+A` | 選取清單的所有列 |
| `Enter` | 把選取的列移到另一個清單 |
| 按鈕上的 `Space` 或 `Enter` | 執行移動 |

## 動態效果

- 列的滑鼠停留與選取填色交叉淡變，列被選取時勾號淡入。
- 移動的列立即離開與加入清單，清單不會滑動。
- 按鈕按下與放開時使用 Arc 按鈕的彈簧。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 滑鼠停留或選取填色 | 160 ms 淡變 | 120 ms 淡變 | 立即 |
| 選取的勾號 | 160 ms 淡變 | 立即 | 立即 |
| 列被移動 | 立即 | 立即 | 立即 |

## 響應式

- 兩個面板平分穿梭框的寬度，每個至少 180 px，移動按鈕位於兩者之間。請把穿梭框放在至少約 440 px 寬的容器中；在更窄的螢幕上，改為每一步顯示一個清單（一個清單加一個「選擇」面板）。
- 除非設定了 `Height` 或父層拉伸它，穿梭框高 320 px；清單在面板內捲動。
- 過長的項目文字在列內以省略號截斷。
- 沒有動作依賴滑鼠停留：點一下選取一列，點兩下移動它。

## 相關元件

- 在表單欄位中選取子集用 `MultiSelect`，同時呈現少量選項用 `ChipGroup`。
- 已選項目的順序重要時用 `ReorderableList`，階層中的選擇用 `TreeView`。

| 元件 | 何時選擇 |
|---|---|
| `MultiSelect` | 選取的值必須放進精簡的欄位 |
| `ChipGroup` | 少量選項能放在一兩行內 |
| `Checkbox` | 表單中每個選項都是「是／否」回答 |
| `ReorderableList` | 使用者要排列已選項目的順序 |
| `TreeView` | 項目構成階層 |
