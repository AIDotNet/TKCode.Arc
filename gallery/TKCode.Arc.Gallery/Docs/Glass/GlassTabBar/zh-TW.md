---
title: Glass tab bar
summary: 一個用於應用程式外殼的浮動底部磨砂玻璃索引標籤列，選取的透鏡在標籤之間滑動並拉伸。
source-hash: 27dce94b9c0d
---

<Demo name="Preview" />

## 何時使用

`GlassTabBar` 是手機風格應用程式外殼的主導覽：一條浮在底邊上方、覆蓋在內容之上的磨砂玻璃膠囊，放三到五個目的地。內容在它下面繼續捲動，並模糊地透出來，所以標籤列不會把頁面截斷。每個標籤是圖示加下方的短文字，可以帶一個徽章。選取的標籤位於一塊更亮的玻璃透鏡中，透鏡以前緣先行的方式滑到下一個標籤，途中被拉長，選取的圖示使用強調色。按住並拖曳透鏡，可以在標籤之間來回滑動。

- **手機應用程式的根導覽**：Home、Search、Saved、Profile。
- **窄視窗或手機預覽中的精簡應用程式外殼**。
- **媒體或地圖應用程式**：內容要在標籤列下面一直延伸到底邊。

## 何時不用

- **寬的桌面版面**：使用側邊欄或頂部的 `Tabs`。
- **超過五個目的地**：把其餘的放進「更多」頁面，或使用側邊欄。
- **在一個頁面內切換檢視**：使用 `GlassSegmented` 或 `Tabs`。
- **操作**：標籤是前往某處的；操作請放在工具列或浮動按鈕中。

## 用法

把標籤列放在頁面上方的底部，並把 `arc:GlassBackdrop.Source` 設為捲動的內容，這樣標籤列會模糊經過它下方的那一部分。在內容末尾留出空間（大約標籤列的高度加上它的外邊距），讓最後一項能捲動到標籤列上方。

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Feed}">
    <ScrollViewer>
        <StackPanel Name="Feed" Margin="16,16,16,110"><!-- the page --></StackPanel>
    </ScrollViewer>
    <arc:GlassTabBar Margin="0,0,0,18">
        <arc:GlassTab Content="Home" Icon="{x:Static arc:ArcIcons.House}" />
        <arc:GlassTab Content="Search" Icon="{x:Static arc:ArcIcons.Search}" />
        <arc:GlassTab Content="Saved" Icon="{x:Static arc:ArcIcons.Bookmark}" Badge="" />
        <arc:GlassTab Content="Profile" Icon="{x:Static arc:ArcIcons.User}" />
    </arc:GlassTabBar>
</Panel>
```

總有一個標籤被選取；繫結 `SelectedIndex` 或處理 `SelectionChanged` 來切換頁面。`GlassTabBar` 衍生自 Avalonia 的 `SelectingItemsControl`，位於 Special 套件中：在 `UseArc()` 旁呼叫 `UseArcSpecial()` 註冊。內容捲動時，它相對標籤列移動，所以標籤列下的副本會重新拍下。磨砂效果如何以公開的 Avalonia API 實作，見 Glass card 頁面。

### 只顯示圖示與徽章

`ShowLabels="False"` 得到一條只有圖示、更矮的標籤列；文字仍是各標籤的無障礙名稱。`Badge` 在標籤圖示上加一個標記：空字串是一個圓點（「有新內容」），其他文字是一個小數字（「3」「9+」），null 表示沒有。

<Demo name="IconsOnly" />

### 手勢

| 輸入 | 效果 |
|---|---|
| 點按一個標籤 | 選取它；透鏡滑過去 |
| 按住選取的標籤 | 透鏡鼓起 |
| 從選取的標籤開始拖曳 | 透鏡跟隨手指在標籤列上移動 |
| 拖曳後放開 | 選取離透鏡最近的標籤 |

### 內容建議

- 使用一個詞的文字，以及同一套清楚的線條圖示。
- 只為需要注意的標籤加徽章：新內容用圓點，待處理的事項用數字。
- 每個頁面上保持相同的標籤和順序。

## API 參考

<!-- api: GlassTabBar -->

<!-- api: GlassTab -->

## 無障礙

- 標籤列是一個索引標籤清單，每個標籤以文字為名稱並帶有選取狀態；徽章在名稱之後朗讀（「Saved, new」「Inbox, 3」）。
- 只有選取的標籤在 Tab 順序中；方向鍵同時移動選取和焦點。
- 鍵盤焦點在取得焦點的標籤周圍顯示 2px 的焦點環；指標操作不顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 進入標籤列（選取的標籤）或離開 |
| `Right` / `Down` | 下一個標籤，循環（由右至左的版面中方向相反） |
| `Left` / `Up` | 上一個標籤，循環 |
| `Home` / `End` | 第一個或最後一個標籤 |

## 動效

- 透鏡的前緣以約 0.28 秒、略有彈性的彈簧移動，後緣以約 0.46 秒、更平靜的彈簧移動，所以它朝新標籤拉長，再收回到它的寬度。
- 按住時，透鏡在每一邊鼓起 3px；拖曳時以緊彈簧跟隨指標。
- 圖示和文字顏色在 160ms 內變化。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 選取變化 | 透鏡滑動並拉伸 | 透鏡在新標籤淡出再淡入 | 透鏡立即移動 |
| 按住和拖曳 | 透鏡鼓起並跟隨 | 透鏡跟隨，不鼓起 | 透鏡跟隨，不鼓起 |
| 指標下的光澤 | 跟隨指標 | 無 | 無 |

## 響應式

- 各標籤平分標籤列的寬度，每個至少 64px；標籤列與標籤一樣寬並置中。在很窄的外殼中，設定 `HorizontalAlignment="Stretch"` 和兩側外邊距，讓標籤平分整個寬度。
- 長文字以省略號結尾，而不是把標籤撐寬。
- 調整大小時，透鏡直接落到它的標籤上，不播放動畫。

## 相關元件

- `Tabs`：寬版面中頁面內的檢視。
- `GlassSegmented`：在玻璃上切換檢視。
- `GlassCard` 和 `GlassMenu`：其他玻璃表面。

| 元件 | 適用情境 |
|---|---|
| `GlassTabBar` | 手機風格應用程式外殼的根導覽 |
| `GlassSegmented` | 在一個畫面內切換少量檢視或模式 |
| `Tabs` | 桌面版面中頁面內的檢視 |
