---
title: 搜尋結果
summary: 跨多種內容類型搜尋，提供帶即時數量的分面標籤頁，結果依類型分組並醒目提示符合的內容，支援鍵盤導覽。
source-hash: ca0d8f0e89c8
---

<Demo name="Preview" />

## 何時使用

- 工作區、說明中心或管理工具中的搜尋頁面，結果分為多種類型。
- 使用者需要將結果縮小到某一類，例如文件、問題、檔案或人員，並查看每一類的數量。
- 結果需要一些脈絡：項目所在的位置、符合處前後的文字，以及最後更新時間等簡短備註。

## 何時不用

- 在應用程式任何位置跳到頁面或執行命令：使用指令面板區塊。
- 頁首或工具列中的精簡搜尋：使用 `ExpandingSearch`。
- 篩選頁面上已有的清單：在清單旁使用 `SearchField`。

## 用法

為控制項設定項目與查詢詞。查詢中的每個字詞都必須符合某個項目的標題、摘錄、位置或關鍵字。分面列會列出“全部”與每一種類型，並附上各自的結果數，清單則依類型將結果分組。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchResults Height="480" Query="發票" Label="搜尋財務">
        <arc:SearchResultItem Title="發票範本"
                              Kind="文件"
                              Location="財務 / 範本"
                              Meta="1 週前更新"
                              Snippet="每位客戶的發票都使用此範本。"
                              Icon="{x:Static arc:ArcIcons.FileText}" />
        <arc:SearchResultItem Title="發票合計與報表不符"
                              Kind="問題"
                              Location="FIN-88 · 報表"
                              Meta="待處理"
                              Icon="{x:Static arc:ArcIcons.CircleDot}" />
    </arc:SearchResults>
</UserControl>
```

### 項目

直接在標記中加入 `SearchResultItem` 物件，或將 `ItemsSource` 設為一個清單。清單為可觀察集合時，結果會跟著其變化。`Kind` 決定所屬的分面與群組；沒有類型的項目會列在 `OtherLabel` 之下。`Icon` 接受 Lucide 圖形或任何控制項，例如用於人員的 `Avatar`。`Keywords` 是額外的關鍵字，同樣可以搜到該項目；`Tag` 可以是任何你想取回的值。

```csharp
search.ItemsSource = new ObservableCollection<SearchResultItem>
{
    new() { Id = "q3", Title = "Q3 規劃筆記", Kind = "文件", Location = "產品 / 規劃", Snippet = notes },
    new() { Id = "fn-482", Title = "睡眠後同步停滯", Kind = "問題", Keywords = ["FN-482"] },
    new() { Id = "maya", Title = "Maya Lindqvist", Kind = "人員", Icon = new Avatar { DisplayName = "Maya Lindqvist", Size = AvatarSize.Small } }
};
```

若直接修改了某個項目，請呼叫 `Refresh()` 重新搜尋。

### 匹配、排序與醒目提示

標題以第一個字詞開頭的結果排在最前，接著是標題中某個字詞以它開頭的結果，再來是標題包含它的結果，其餘結果排在最後。排名相同的結果保持項目的原有順序。標題與摘錄中的每處符合都會被標示。較長的摘錄會裁切成一段範圍，該範圍從第一處符合之前稍早的位置開始，被裁切的兩端都會顯示省略號。

### 分面與群組

`SelectedKind` 為目前的分面，若為 null 則表示“全部”。它是雙向的，因此可以繫結到路由或檢視模型。在“全部”下，每種類型會顯示前 `GroupPreviewCount` 筆結果，以及一個開啟該分面的“查看全部”連結。單一分面會顯示 `PageSize` 筆結果與一個“顯示更多”按鈕。對於目前查詢沒有結果的分面，會保留在原位並以淡化的文字顯示。

<Demo name="FacetsAndEvents" />

### 開啟結果

按下 Enter 或點擊即可開啟結果：會引發 `ItemInvoked`，並以該項目執行 `InvokeCommand`。`ActiveItem` 是鍵盤游標所在的結果。`IsEnabled` 設為 false 的結果會變淡、略過游標，且無法開啟。

```csharp
search.ItemInvoked += (_, e) => Navigate(e.Item.Id);
```

```xml
<arc:SearchResults ItemsSource="{Binding Results}"
                   Query="{Binding Query}"
                   SelectedKind="{Binding Kind}"
                   InvokeCommand="{Binding OpenCommand}" />
```

### 伺服器端搜尋

當搜尋由伺服器執行時，將 `IsFilteringEnabled` 設為 false。此時項目會依你提供的順序顯示，並依類型計數，查詢中的字詞仍會被醒目提示。請求執行期間設定 `IsLoading`：骨架列會取代結果的位置，並保留原本的高度。

<Demo name="ServerSearch" />

### 無結果

沒有任何符合項時，空狀態會顯示查詢內容。若目前的分面為空，而其他分面不為空，會指出結果最多的類型，並提供“搜尋全部”。`Suggestions` 用來加入建議的查詢；選取其中一項會將它填入搜尋欄位。

<Demo name="NoResults" />

### 窄版配置

寬度低於 560 像素時，外框會收緊，每個結果會把備註移到位置列中。分面列會在其軌道內捲動，並在仍有分面被隱藏的一側邊緣淡出。

<Demo name="Narrow" />

### 文字

每段文字都有英文預設值，並可透過一個屬性變更。格式使用 `{0}` 代表數量或查詢內容。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchResults Label="搜尋說明中心"
                       PlaceholderText="搜尋文章"
                       AllLabel="全部"
                       ResultsFormat="{}{0} 篇文章"
                       OneResultText="1 篇文章"
                       NoResultsText="找不到文章"
                       EmptyTitleFormat="找不到“{0}”"
                       SuggestionsLabel="試試搜尋" />
</UserControl>
```

## API 參考

<!-- api: SearchResults -->

<!-- api: SearchResultItem -->

## 無障礙

- 此控制項是搜尋地標，名稱由 `Label` 提供，同時也為搜尋欄位命名。
- 分面為標籤頁清單。只有選取的分面可透過 Tab 鍵取得焦點。ArrowLeft 與 ArrowRight 在分面之間移動並選取它們，Home 與 End 跳到第一個與最後一個。每個分面的無障礙名稱包含其類型與數量。
- 結果為清單，游標所在的結果即為其選取項目。每個結果的無障礙名稱包含標題、類型與位置。分組標題為三級標題。
- 在搜尋欄位中，ArrowDown 移入結果，Enter 開啟游標所在的結果，若沒有游標則開啟第一個結果。在結果中，ArrowUp、ArrowDown、Home、End、PageUp 與 PageDown 移動游標；在第一個結果上按 ArrowUp 會返回搜尋欄位；Enter 或 Space 開啟結果，Escape 返回搜尋欄位。
- 在搜尋欄位中按 Escape 會清除查詢。焦點位於控制項內時，Ctrl+F（macOS 上為 Cmd+F）或 `/` 會將焦點移到搜尋欄位；在結果中輸入則會接續在搜尋欄位中輸入查詢。
- 統計列是禮貌式即時區域，結果數量變更時會被播報。

## 動態效果

- 游標在結果之間滑動：跟隨指標時使用彈簧動畫，跟隨方向鍵時使用 70 毫秒的補間動畫。游標首次出現時淡入，被清除時淡出。
- 新結果會淡入並上移 4 像素，每個結果相隔 35 毫秒。已經顯示的結果保持不動。
- 分面數量會滾動到新的數值，選取分面的區塊會在變形彈簧上於各分面之間滑動。
- 統計文字交叉淡化，結果、骨架列與空狀態會相互淡入淡出。
- 動態效果等級為 Reduced 時保留淡入淡出，但取消所有位移：游標與分面區塊直接跳轉，數量立即變更，結果只淡入。等級為 Off 時，所有變更會立即套用。

## 相關元件

- 指令面板區塊：用於在應用程式任何位置執行操作與導覽。
- `ExpandingSearch`：用於頁首中的精簡搜尋。
- `SearchField`：用於篩選頁面上的清單。
- `EmptyState` 與 `Skeleton`：本區塊分別用於無結果與載入狀態。
