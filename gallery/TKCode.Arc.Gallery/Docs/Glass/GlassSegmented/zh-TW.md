---
title: Glass segmented
summary: 一個磨砂玻璃上的分段控制項：選取的透鏡在分段之間滑動時會拉伸，也可以直接拖曳。
source-hash: cc5d6140595f
---

<Demo name="Preview" />

## 何時使用

`GlassSegmented` 在一條浮在圖片或彩色頁首上的磨砂玻璃軌道上，切換兩到五個相關的檢視或模式。選取的分段位於一塊更亮的玻璃透鏡中。選取變化時透鏡滑過去：前緣先走，後緣跟上，所以它在途中被拉長，落下時再收攏，像一滴液體。按住選取的分段，透鏡會在手指下微微鼓起；拖曳它，它就跟著走，放開時選取它所在的分段。

- **圖表或地圖上的時間範圍**：Day、Week、Month、Year。
- **媒體頁首上的檢視切換**：照片與相簿、地圖與清單。
- **玻璃工具列上的模式**：檢視器裡的淺色、深色和螢幕。

## 何時不用

- **一般頁面上**：使用更輕的 `SegmentedControl`。
- **超過五個選項，或文字較長**：使用 `Tabs` 或 `Select`。
- **開啟或關閉一件事**：使用 `Switch`。
- **應用程式的頂層導覽**：使用 `GlassTabBar` 或 `Tabs`。

## 用法

直接寫出 `GlassSegment` 分段（文字和可選的 `Icon`），或為控制項設定 `ItemsSource`。總有一個分段被選取；讀取或繫結 `SelectedIndex` 或 `SelectedItem`，並回應 `SelectionChanged`。

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassSegmented SelectedIndex="1" HorizontalAlignment="Center">
        <arc:GlassSegment Content="Day" />
        <arc:GlassSegment Content="Week" />
        <arc:GlassSegment Content="Month" />
    </arc:GlassSegmented>
</Panel>
```

`GlassSegmented` 衍生自 Avalonia 的 `SelectingItemsControl`，位於 Special 套件中：在 `UseArc()` 旁呼叫 `UseArcSpecial()` 註冊。磨砂效果與所有玻璃表面相同：它如何以公開的 Avalonia API 實作，見 Glass card 頁面。

### 圖示與停用的分段

`Icon` 接受任何 24×24 的描邊圖形，例如 `ArcIcons` 中的圖示。停用的分段不能被選取；方向鍵會略過它，透鏡也不會停在上面。

<Demo name="Icons" />

### 來自資料的選項

`ItemsSource` 中的項目顯示為它們的文字。

<Demo name="Bound" />

### 手勢

| 輸入 | 效果 |
|---|---|
| 點按一個分段 | 選取它；透鏡滑過去 |
| 按住選取的分段 | 透鏡鼓起 |
| 從選取的分段開始拖曳 | 透鏡沿軌道跟隨，在寬度不同的分段之間改變寬度 |
| 拖曳後放開 | 選取離透鏡最近的分段 |

### 內容建議

- 每個分段的文字只用一個短詞，並讓各分段長度相近。
- 要嘛每個分段都有圖示，要嘛都沒有。
- 把控制項放在背景較平靜的位置；軌道下面細節太多會讓文字難以閱讀。

## API 參考

<!-- api: GlassSegmented -->

<!-- api: GlassSegment -->

## 無障礙

- 控制項是一個索引標籤清單，每個分段是一個索引標籤，以文字為名稱並帶有選取狀態；螢幕閱讀器可以直接選取一個分段。
- 只有選取的分段在 Tab 順序中；方向鍵同時移動選取和焦點。
- 鍵盤焦點在取得焦點的分段周圍顯示 2px 的焦點環；指標操作不顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 進入控制項（選取的分段）或離開 |
| `Right` / `Down` | 下一個分段，循環（由右至左的版面中方向相反） |
| `Left` / `Up` | 上一個分段，循環 |
| `Home` / `End` | 第一個或最後一個分段 |

## 動效

- 透鏡的前緣以約 0.28 秒、略有彈性的彈簧移動，後緣以約 0.46 秒、更平靜的彈簧移動，所以透鏡朝新分段拉長，再收回到它的寬度。
- 按住時，透鏡以靈敏彈簧在每一邊鼓起 2px；拖曳時以約 0.18 秒的緊彈簧跟隨指標。
- 文字顏色在 160ms 內變化。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 選取變化 | 透鏡滑動並拉伸 | 透鏡在新分段淡出再淡入 | 透鏡立即移動 |
| 按住和拖曳 | 透鏡鼓起並跟隨 | 透鏡跟隨，不鼓起 | 透鏡跟隨，不鼓起 |
| 文字顏色 | 160ms | 120ms | 立即 |

## 響應式

- 控制項與它的分段一樣寬，不會捲動。保持最多五個短選項，或在窄版面中改用 `Select`。
- 調整大小時，透鏡直接落到它的分段上，不播放動畫。
- 觸控與滑鼠相同：點按選取，按住並拖曳選取的分段來移動透鏡。

## 相關元件

- `SegmentedControl`：一般表面上的同類選擇。
- `GlassTabBar`：玻璃上的應用程式導覽。
- `GlassCard` 和 `GlassMenu`：其他玻璃表面。

| 元件 | 適用情境 |
|---|---|
| `GlassSegmented` | 在圖片或彩色頁首上切換少量檢視或模式 |
| `SegmentedControl` | 一般頁面上的同類選擇 |
| `Tabs` | 檢視較多，或檢視名稱較長 |
