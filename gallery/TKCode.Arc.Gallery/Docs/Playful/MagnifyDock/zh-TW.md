---
title: Magnify dock
summary: 一個應用程式停駐列：圖示在指標下像透鏡一樣放大，把鄰居推開，顯示名稱，啟動時還會彈跳。
source-hash: 53aa2e91e84b
---

<Demo name="Preview" />

## 何時使用

`MagnifyDock` 是架子上的一排應用圖示。指標沿著它移動時，指標附近的圖示會像放大鏡一樣變大，正下方的最大，整排會變寬來騰出空間，所以不會有東西重疊。指標下的圖示會在上方顯示它的名稱。點擊圖示即啟動：它會彈跳幾下，一次比一次小，下方的一個圓點表示它正在執行。

- **桌面風格的應用程式啟動器**：用在網頁桌面、展示機台或展示環境裡。
- **作品集與產品頁面**：把工具或專案列成圖示。
- **有趣的工具列**：讓少數幾個常用操作有觸感。

## 何時不用

- **密集的工具列和編輯器**：在指標下移動的圖示更難點中。請用一般工具列。
- **應用程式版面裡的停駐面板**：那是 `DockLayout`。
- **很長的清單**：圖示保持在十來個；更多的話手機放不下。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MagnifyDock Label="Apps">
        <arc:MagnifyDockItem Label="Files" IsRunning="True">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Folder}" Size="24" />
        </arc:MagnifyDockItem>
        <arc:MagnifyDockItem Label="Mail">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Mail}" Size="24" />
        </arc:MagnifyDockItem>
    </arc:MagnifyDock>
</UserControl>
```

`MagnifyDock` 是 Special 套件裡的 `ItemsControl`。在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊這個套件。每一項是一個 `MagnifyDockItem`，它是一個以圖示為內容的按鈕：可以直接寫在裡面，或繫結 `ItemsSource`，用 `ItemTemplate` 提供圖示，並透過 `ItemContainerTheme` 設定每一項的 `Label` 和 `IsRunning`。給某一項設定自己的 `Background` 可以給它的底塊上色；預設底塊使用柔和的表面色。停駐列會自己置中，並預留放大後最寬的一排和文字所需的空間，所以它放大時頁面不會移動。

### 尺寸與透鏡

`ItemSize` 是靜止時圖示的邊長（48 px），`Gap` 是圖示之間的間距（8 px）。`Magnification` 是指標正下方圖示的縮放倍數（1.8；設為 1 就關閉透鏡），`Reach` 是離指標多遠的圖示仍會變大（150 px）。`ShelfPadding` 是架子邊緣與圖示之間的空隙（8 px）。

<Demo name="Launch" />

### 啟動

點擊、輕觸圖示，或在圖示上按 Enter 或 Space 即啟動：它會彈跳，觸發 `ItemLaunched`，帶上它的序號、容器和資料項目；`MarksLaunched` 開啟時（預設），它的 `IsRunning` 會變為開啟。`Launch(index)` 在程式碼中做同樣的事。每一項都是按鈕，所以 `Command` 和 `Click` 也可以用在它上面。應用程式關閉時請把 `IsRunning` 設為 false。

### 最佳實務

**建議**

- 給每一項一個簡短的 `Label`；它既是圖示上方的名稱，也是它的無障礙名稱。
- 在有色底塊上使用簡潔、高對比的圖示，這樣它們在 48 px 時易讀，放大到 1.8 倍時依然清晰。

**避免**

- 把停駐列放在可能被蓋住的位置：放大後的圖示和文字會升到架子上方約 80 px。
- 用它來做人們會快速重複的操作，比如設定文字格式。

### 常見問題

**停駐列放大時會讓頁面移動嗎？** 不會。它在版面配置時按完整高度預留最寬的一排，放大只是對圖示做轉換。

**可以在群組之間放分隔線嗎？** 暫時不行。請並排用兩個停駐列，或加大 `Gap`。

## API 參考

<!-- api: MagnifyDock -->

<!-- api: MagnifyDockItem -->

<!-- api: MagnifyDockItemEventArgs -->

## 無障礙

- 停駐列是一個由 `Label`（預設為「Dock」）命名的工具列。每個圖示是一個由它的 `Label` 命名的按鈕。
- 停駐列是一個 Tab 停靠點。方向鍵在圖示之間移動（在兩端循環），Home 和 End 跳到第一個和最後一個，取得焦點的圖示會放大，並像指標在它上面一樣顯示它的名稱。
- 鍵盤聚焦時，圖示周圍顯示焦點環，並隨圖示一起放大；指標聚焦不顯示。
- 執行中的圓點只是裝飾；如果重要，請在你自己的介面裡說明哪些應用程式是開啟的。

### 鍵盤互動

| 按鍵 | 操作 |
|---|---|
| `Tab` | 把焦點移入停駐列，到上一次取得焦點的圖示 |
| `Left` / `Right` | 移到上一個或下一個圖示 |
| `Home` / `End` | 移到第一個或最後一個圖示 |
| `Enter` / `Space` | 啟動取得焦點的圖示 |

## 動效

- 每個圖示的大小圍繞指標按一條平滑的鐘形曲線變化，各自用自己的彈簧（偏硬、略欠阻尼），所以快速劃過時會沿著這一排泛起漣漪，並在沒有明顯晃動的情況下穩定下來。
- 這一排圍繞變大的圖示按相同的間距重新排布並保持置中；架子隨之變寬。
- 名稱出現在目前圖示上方，在 120 ms 內淡入，並跟隨圖示變大和彈跳。
- 一次啟動讓圖示彈跳三次，高度依次為 62%、30% 和 12%，每次 400 ms。
- 放大是每個圖示的算繪轉換，每影格寫入一次；版面配置不會改變。所有圖示都靜止時停止影格，停駐列離開視野時暫停。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 指標在這一排上 | 圖示像透鏡一樣放大 | 圖示保持大小 | 圖示保持大小 |
| 名稱 | 在 120 ms 內淡入 | 在 120 ms 內淡入 | 直接出現 |
| 啟動 | 彈跳三次 | 只顯示執行圓點 | 只顯示執行圓點 |

## 響應式行為

- 停駐列的寬度等於圖示完全放大時的寬度，並且自己置中；它不會換行。在手機上，請用更少的圖示或更小的 `ItemSize`，約 40 px。
- 觸控螢幕沒有懸停，所以在觸控螢幕上圖示不會放大；輕觸圖示會像點擊一樣啟動它。
- 名稱在停駐列預留的高度內升到它上方，所以不會被停駐列自己的框裁掉。

## 相關元件

- `DockLayout` 排布應用程式的面板，那是另一種停駐列。
- `SegmentedControl` 在一排精簡的按鈕裡切換幾個檢視。

| 元件 | 何時選擇 |
|---|---|
| `MagnifyDock` | 幾個應用程式或工具要讓人覺得開啟時有觸感 |
| `DockLayout` | 應用程式需要可停駐、可調整大小的面板 |
| `SegmentedControl` | 人們在幾個檢視之間切換 |
