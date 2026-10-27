---
title: 可排序清單
summary: 透過直接拖放整理項目。
source-hash: 5b51862a9d33
---

<Demo name="Priorities" />

## 何時使用

`ReorderableList` 讓使用者把一個簡短的項目清單調整成他們想要的順序。它用於由人手動決定、且順序本身就是資訊的場景：團隊的上線優先順序、佇列中接下來要處理的任務、審核清單的步驟。它放在卡片、側邊面板或設定頁中，並透過 `Reordered` 事件儲存新的順序。

- **規劃檢視中的上線優先順序**：產品負責人在發行評審前為四到五條工作流排序。順序就是答案，因此每項旁邊的拖曳把手比數字輸入框更清楚。
- **媒體或營運主控台中的播放清單與任務佇列**：接下來要播放或處理的項目。使用者把一項上移或下移，其餘項目維持原位。
- **工作流程編輯器中的核准或審核步驟**：一份清單，其順序決定哪位審核人先看到文件，由流程的負責人編輯。
- **儀表板或導覽面板中的固定捷徑**：使用者最常用的項目，依自己的喜好排序，並依帳號儲存。

## 何時不用

- **順序來自欄位的值**，例如依到期日或金額為發票排序：用 `Table`，它依欄位排序，並把資料保留在表格中。
- **對某一列做快速操作**，例如封存、稍後提醒或刪除，且不需要重新排序：用 `SwipeActions`，它透過滑動顯示操作，而不會移動該列。
- **包含數百項的長清單**：列沒有虛擬化，而且每當其他列移動時，每一列都會執行版面配置動畫。應拆分清單，或為 `Table` 配上排序控制項。
- **巢狀項目**，例如資料夾、分類或子工作：用 `TreeView`，它顯示階層，並讓使用者展開每一層。
- **從固定集合中選擇**，使用者只是切換而不排名，例如篩選標籤或頻道：用 `ChipGroup`。這些選項的順序不由使用者決定。

## 用法

每一列都顯示一個把手。拖曳把手即可移動該列。清單透過 `Reordered` 回報新的順序。

```xml
<arc:ReorderableList Label="優先順序"
                     Width="520"
                     ItemsSource="{Binding Priorities}" />
```

`ReorderableList` 衍生自 Avalonia 的 `ItemsControl`，因此 `ItemsSource`、`Items`、`ItemTemplate` 以及容器方法的用法與其他項目控制項相同。`ItemsSource` 接受一個 `ReorderableListEntry` 清單，每一項包含 `Id`、`Title` 和選用的 `Detail`。

```csharp
var priorities = new ObservableCollection<ReorderableListEntry>
{
    new("pricing", "發布定價頁", "行銷部 - 週五截止"),
    new("onboarding", "檢視上線引導流程", "設計 - 3 則未解決的留言"),
    new("billing", "遷移帳單 Webhook", "平台")
};
var list = new ReorderableList { ItemsSource = priorities };
```

### 結構

1. **把手**（握把）：一個 40 px 的方形，中間是 16 px 的垂直把手圖示，距離列的上、下、左邊緣各 7 px，位於淡化的表面色上。它是唯一能開始拖曳的部分。
2. **列**：帶有 Arc 邊框和控制項圓角的表面，高度至少 56 px。列之間相距 8 px。
3. **內容**：項目標題使用 14 px 中等字重，下方是 12 px、淡化文字顏色的詳情列。預設內容來自 `ReorderableListEntry`；`ItemTemplate` 可以取代它。
4. **位置**（右側）：兩位數字，例如 `01`、`02`，12 px，淡化文字顏色。每次移動後更新，並沿該列移動的方向滾入。
5. **播報器**（不可見）：一個隱藏的禮貌區域，朗讀每一步鍵盤和拖曳操作。

### 回應移動

`Reordered` 提供該項目、它的舊索引和新索引。當 `ItemsSource` 是 `ObservableCollection` 且 `MovesItems` 為 `True`（預設值）時，清單會直接在該集合中移動項目。把 `MovesItems` 設為 `False`，集合維持不變，由你自己儲存順序。繫結到陣列或其他固定資料來源的清單行為相同：清單觸發 `Reordered`，並保持資料來源不變。

```csharp
list.Reordered += (_, e) => status.Text =
    $"已將 {((ReorderableListEntry)e.Item!).Title} 從第 {e.OldIndex + 1} 位移到第 {e.NewIndex + 1} 位";

list.MoveItem(0, 2);   // 把第一列移到第三位
```

`MoveItem` 以與拖曳相同的滑動動畫移動列，並且同樣會觸發 `Reordered`，因此程式碼移動和人的移動會進入同一個處理常式。

### 自訂項目範本

`ItemTemplate` 取代預設的標題和詳情。`ItemTitleSelector` 提供把手的無障礙名稱和播報中使用的標題；未設定時，清單使用項目的 `Title` 或項目的文字。

<Demo name="CustomItemTemplate" />

```csharp
var colors = new ReorderableList
{
    ItemsSource = new ObservableCollection<string> { "珊瑚色", "藏紅花色", "薄荷色" },
    ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
    ItemTitleSelector = item => item.ToString() ?? string.Empty
};
```

### 長度與捲動

當清單高於你為它分配的空間時，把它放進 `ScrollViewer`。把一列拖到捲動區域的邊緣會使其捲動，因此使用者可以把一列移到清單可見部分之外。

<Demo name="AutoScroll" />

```xml
<ScrollViewer Height="340">
    <arc:ReorderableList ItemsSource="{Binding Tracks}" />
</ScrollViewer>
```

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有集合以及儲存順序的程式碼。使用 CommunityToolkit.Mvvm 時，由 `ObservableObject` 持有集合；當 `MovesItems` 為 `True` 時，清單會移動該集合中的項目。繫結是單向的：清單修改集合，檢視模型讀取它。

```csharp
public partial class LaunchPlanViewModel : ObservableObject
{
    public ObservableCollection<ReorderableListEntry> Priorities { get; } =
    [
        new("pricing", "發布定價頁", "行銷部，週五截止"),
        new("onboarding", "檢視上線引導流程", "設計，3 則未解決的留言"),
        new("billing", "遷移帳單 Webhook", "平台")
    ];

    [ObservableProperty]
    private string _status = "拖曳把手即可調整上線優先順序。";

    [RelayCommand]
    private void SaveOrder()
    {
        // 在這裡儲存順序，例如透過你的 API 用戶端。
        Status = $"已儲存順序：{string.Join(", ", Priorities.Select(p => p.Id))}";
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:ReorderableList Label="上線優先順序"
                         Width="560"
                         HorizontalAlignment="Left"
                         ItemsSource="{Binding Priorities}" />
    <TextBlock Text="{Binding Status}" TextWrapping="Wrap" />
</StackPanel>
```

在程式碼後置檔中把 `Reordered` 轉送給命令，使每次移動都儲存順序：

```csharp
list.Reordered += (_, _) => vm.SaveOrderCommand.Execute(null);
```

`ItemsSource` 是單向的。不要從清單反向繫結它，因為清單不會寫入該屬性；新的順序由集合本身承載。

### 資料結構與效能

清單適用於短集合，成本主要取決於列數。每當其他列移動時，每一列都會被版面配置並執行動畫，而且列沒有虛擬化，因此幾十列的清單移動得很順暢，而幾百列的清單則不然。

- **常見情況使用 `ReorderableListEntry`。** 它帶有 `Id`，供你自己的程式碼在儲存順序時找到該項目；`Title` 為可存取的列命名；`Detail` 是第二行文字。
- **其他資料使用 `ItemTemplate`。** 任何型別的項目都可以透過 `ItemTemplate` 顯示；同時設定 `ItemTitleSelector`，使把手名稱和播報讀出正確的標題。
- **把資料保存在一個集合中。** 清單原地移動項目時，集合必須是清單所繫結的那一個，而不是副本。

### 範例：上線優先順序

產品負責人在評審之前為上線工作流程排序。這個清單決定了整個頁面的內容，因此它上方有標題，旁邊有一行狀態文字確認已儲存的順序。每一列都有一段包含負責人或截止日期的詳情，使位置讀起來像計畫，而不只是一個數字。

![上線優先順序清單，四列有序項目，每列都有把手和位置數字](images/LaunchPriorities.png)

```xml
<StackPanel Width="600" Spacing="14">
    <TextBlock Text="Launch priorities"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Ordered by the launch lead. Drag a grip, or focus one and press Up or Down."
               FontSize="13"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:ReorderableList Label="Launch priorities"
                         Width="560"
                         HorizontalAlignment="Left">
        <arc:ReorderableListEntry Id="pricing"
                                  Title="Ship pricing page"
                                  Detail="Marketing, due Friday" />
        <arc:ReorderableListEntry Id="onboarding"
                                  Title="Review onboarding flow"
                                  Detail="Design, 3 open comments" />
        <arc:ReorderableListEntry Id="billing"
                                  Title="Migrate billing webhooks"
                                  Detail="Platform" />
        <arc:ReorderableListEntry Id="notes"
                                  Title="Write launch notes"
                                  Detail="Docs, draft ready" />
    </arc:ReorderableList>
</StackPanel>
```

設計決策體現在文案和規模上。四列足以展示把手、標題、詳情和位置，而無需捲動，這正是此控制項所適用的清單長度。詳情列寫明負責人和狀態，因此使用者不必打開每一項就能判斷順序是否合理。

### 文案規範

控制項會根據標題產生把手名稱和播報，因此標題承載措辭。

| 不要寫 | 改為 |
|---|---|
| 項目 1、項目 2 | 工作項目的名稱，例如「發布定價頁」 |
| 拖曳手柄 | 把手；其無障礙名稱由標題產生 |
| 重新排序清單 | `Label="Launch priorities"`，即被排序內容的名稱 |
| 優先順序 1：發布定價 | 發布定價頁，位置由清單顯示 |
| 已移動的項目 | 標題，播報中已經包含它 |

- 標題寫成工作項目的名稱，使用句首大寫，前面不加編號。清單會加上位置。
- 標題要夠短，以便在清單寬度內單行顯示；負責人、日期或狀態放入 `Detail`。
- 詳情列中的日期和計數使用阿拉伯數字，例如「週五截止」或「3 則未解決的留言」。

### 最佳做法

**建議**

- 把 `Label` 設為被排序對象的名稱。標籤就是清單的無障礙名稱，螢幕閱讀器使用者會在第一列之前聽到它。
- 在 `Reordered` 中儲存順序，而不是等到視窗關閉時才儲存，這樣關閉視窗不會遺失變更。
- 當清單可能增長時，把它放在設定了高度的 `ScrollViewer` 中，使拖曳能夠越過可見的列。
- 為每個項目提供穩定的 `Id`，使儲存的順序在重新載入和重新命名之後依然有效。
- 在狀態列中顯示結果，讓使用者看到變更已被儲存。

**避免**

- 把清單用於欄位已經定義的順序。想依到期日查看項目的使用者需要可排序的表格，而手動順序會與排序衝突。
- 對包含數百列的清單重新排序。每當其他列移動時，每一列都會執行動畫，清單在失去實用性之前就已失去順暢度。
- 讓整列都可拖曳。只有把手可以開始拖曳，因此文字可以被選取，點擊文字也不會觸發移動。
- 在按鈕處理常式中再儲存一份順序。清單已經在原地移動項目，因此應在 `Reordered` 觸發時直接儲存集合，而不要手動重建順序。

### 常見問題

**清單移動了列，但我的來源清單沒有變化。** `MovesItems` 為 `False`，或者資料來源不是可變的集合。請使用 `ObservableCollection` 並保持 `MovesItems` 為預設值，或在 `Reordered` 中自行儲存順序。

**能從列的文字開始拖曳嗎？** 不能。只有把手可以開始拖曳，因此列中的文字可以被選取和複製。使用鍵盤的使用者可以聚焦把手，然後用方向鍵移動。

**為什麼第一列按「上」沒有反應？** 鍵盤移動在清單兩端停止，焦點保留在把手上。列不會繞到另一端；在選取一列時使用 Home 或 End，即可跳到第一位或最後一位。

**如何從程式碼移動項目？** 呼叫 `MoveItem(from, to)`。它使用與拖曳相同的滑動動畫，並會觸發 `Reordered`，因此儲存順序的處理常式也會對程式碼移動生效。

**鍵盤移動之後，焦點會留在該列上嗎？** 會。焦點保留在被移動列的把手上，因此下一次按鍵會再次移動同一列。Tab 像對待其他控制項一樣離開清單。

## API 參考

<!-- api: ReorderableList -->

<!-- api: ReorderableListEntry -->

## 無障礙

- 每個把手都作為按鈕公開。它的名稱包含項目標題及其位置，例如 "Reorder Ship pricing page. Position 1 of 4. Use arrow keys."，並且每次移動後名稱都會更新。
- 清單由 `Label` 命名，預設為 "Reorder items"。請把它設為被排序對象的名稱。
- 只有把手可以取得焦點用於拖曳。列中的文字是一般的、可選取的文字區域。
- 每一步都在禮貌區域中播報：「"Picked up Ship pricing page. Position 1 of 4. Use the arrow keys to move, Space to drop, Escape to cancel."」，然後是 "Ship pricing page dropped at position 3 of 4."
- 鍵盤移動之後，焦點保留在被移動列的把手上，因此下一次按鍵會作用於同一列。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Up` / `Down`（在把手上） | 立即把該列上移或下移一位；焦點保留在其把手上 |
| `Space` / `Enter`（在把手上） | 拿起該列；之後方向鍵會移動它，清單同時預覽位置 |
| `Up` / `Down`（已拿起的列） | 把被拿起的列移動一位 |
| `Home` / `End`（已拿起的列） | 把被拿起的列移到第一位或最後一位 |
| `Space` / `Enter`（已拿起的列） | 在目前位置放下該列 |
| `Escape`（已拿起的列） | 取消：該列回到原來的位置 |
| 在把手上按指標拖曳 | 把該列移到新位置；被經過的列讓出空間 |

## 動態效果

- 按下把手時，該列會輕微抬起。放開後，它以一個短促的彈簧回彈到原位。
- 拖曳時，該列跟隨指標移動，被經過的列則以彈簧滑開。
- 位置數字沿該列移動的方向捲動。
- 減少動態效果時，列仍跟隨指標，但落下時直接到位且不抬起。關閉動態效果時的行為與減少相同。
- 把列拖到捲動區域的邊緣時，該區域會捲動。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 按下把手 | 該列抬起，並以靈敏的彈簧縮放到 1.02 | 該列不抬起 | 該列不抬起 |
| 拖曳 | 該列跟隨指標，被經過的列以彈簧滑開 | 該列跟隨指標，落下時立即到位 | 該列跟隨指標，落下時立即到位 |
| 放下 | 該列以快速彈簧落定，並保留放開時的速度 | 該列立即到位 | 該列立即到位 |

## 相關元件

- `Table`：當順序來自欄位的值，而不是使用者的選擇時使用。
- `SwipeActions`：對某一列做快速操作，而不重新排序它。

| 元件 | 適用情境 |
|---|---|
| `Table` | 順序來自某一欄中的值，使用者依該欄排序 |
| `SwipeActions` | 某一列需要封存或稍後提醒等快速操作，而不是新的位置 |
| `TreeView` | 項目是巢狀的，使用者展開資料夾或分類來尋找它們 |
| `ChipGroup` | 使用者從固定的選項集合中選擇，而順序不由他們決定 |
