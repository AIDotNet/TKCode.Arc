---
title: Cover flow
summary: 一排帶透視效果的專輯或書籍封面：中間的封面正對著你，其餘封面轉向它，快速滑動後會繼續滑行並停在一張封面上。
source-hash: 370d49126271
---

<Demo name="Preview" />

## 何時使用

`CoverFlow` 用大家熟悉的封面來展示一組內容。中間的封面正對前方，下面顯示標題和副標題；兩側的封面轉向它，向遠處層層排開，站在會映出倒影的光亮地面上。拖曳這一排時，它跟著手移動；放手後按快速滑動的慣性繼續滑行，然後讓一張封面停在中間，所以一次就能劃過很長的一排。點擊兩側的封面會把它移到中間，點擊中間的封面會打開它。

- **音樂和有聲書**：按封面挑選專輯、播放清單和系列。
- **書籍和雜誌**：閱讀清單或一排期刊。
- **推薦內容**：首頁上的一小組遊戲、電影或範本。

## 何時不用

- **按名稱查找**：可以搜尋的清單比翻封面更快。
- **大量的小內容**：同時只能看到幾張封面，請用網格。
- **不是封面的圖片**：形狀各異的照片在透視中顯得擁擠，請用 `PhotoGrid` 或 `LightboxGallery`。

## 用法

```xml
<arc:CoverFlow Label="Albums" SelectedIndex="1">
    <arc:CoverFlowItem Source="https://example.com/covers/lanterns.jpg" Title="Paper Lanterns" Subtitle="Mira Vale" />
    <arc:CoverFlowItem Source="https://example.com/covers/copper.jpg" Title="Copper Hours" Subtitle="The Lindens" />
    <arc:CoverFlowItem Source="https://example.com/covers/north.jpg" Title="North of Nowhere" Subtitle="Oskar Fen" />
</arc:CoverFlow>
```

`CoverFlow` 位於 Special 包中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。可以直接寫 `CoverFlowItem`，也可以把 `ItemsSource` 繫結到由它們、`ImageSource` 或地址組成的清單。`Title` 和 `Subtitle` 顯示在中間封面的下方，`Tag` 可以帶上你自己的任意物件。`SelectedIndex` 是中間的封面，支援雙向繫結；`SelectedItem` 就是這張封面。

### 其他形狀的封面

`CoverWidth` 設定封面的寬度，`CoverAspectRatio` 設定它的形狀（預設為正方形，書籍約為 0.67）。`TiltAngle` 設定兩側封面轉動的角度，`ShowsReflection="False"` 會去掉地面倒影。

<Demo name="Books" />

### 打開封面

點擊或輕觸中間的封面，或按 Enter 或 Space 時，會觸發 `ItemInvoked`。它的參數帶有 `Index` 和 `Item`。

<Demo name="Invoked" />

### 最佳實務

**建議**

- 為每張封面寫 `Title`：它顯示在中間封面下方，讀屏軟體也會讀出它。
- 內容沒有明顯的第一項時，從中間的一張開始，兩側都能看到封面。
- 一排最好只有幾十張封面；更多時請提供搜尋或網格。

**避免**

- 以小字為主的封面：兩側的封面是轉過去並變暗的。
- 放在橫向拖曳還要捲動其他內容的地方。

## API 參考

<!-- api: CoverFlow -->

## 無障礙

- 整排封面是一個 Tab 停駐點，是一個名稱來自 `Label` 的清單。它的幫助文字和一條禮貌模式的狀態文字會說明中間的封面：“Night Ferry, Ilse Marrow, 10 of 12”。`ItemNameFormat` 可以改變位置部分的說法。
- 方向鍵移動一張封面，Page Up 和 Page Down 移動五張，Home 和 End 移到兩端；Enter 或 Space 打開中間的封面。
- 指標獲得焦點時不顯示焦點環；鍵盤獲得焦點時，在整排周圍顯示統一的焦點環。

### 鍵盤互動

| 按鍵 | 操作 |
|---|---|
| Left / Right | 把上一張或下一張封面移到中間。 |
| Page Up / Page Down | 移動五張封面。 |
| Home / End | 移到第一張或最後一張封面。 |
| Enter 或 Space | 打開中間的封面（`ItemInvoked`）。 |

## 動效

- 所有封面都由同一個位置決定，也就是中間的序號。在中間和第一張相鄰的封面之間，封面的轉動、移動和縮小都是連續的，所以慢慢拖曳時，封面會一張一張地轉過來。
- 拖曳跟隨手的動作，預設每 73 px 移動一張封面，超過兩端時有阻力。放手時，按每毫秒 0.995 的衰減估算快速滑動會停在哪張封面，再用 0.5 s 的臨界阻尼彈簧從放手時的速度開始停到那裡。每秒超過三張封面的快速滑動至少會移動一張。
- 滑鼠滾輪每格移動一張封面；觸控板橫向捲動這一排，停下 140 ms 後對齊到最近的封面。
- 超出邊緣的封面會淡出。每一個影格為每張可見的封面寫一個透視變換；不會重新配置版面。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 換一張封面 | 這一排以彈簧滑動 | 這一排直接移動 | 這一排直接移動 |
| 拖曳和快速滑動 | 跟隨手的動作，再滑行並停下 | 跟隨手的動作，再直接停下 | 跟隨手的動作，再直接停下 |
| 標題文字 | 從對應的一側滑入 | 交叉淡化 | 直接切換 |

## 響應式行為

- 這一排佔滿給定的寬度；變窄時兩側顯示的封面就少一些。高度由 `CoverWidth`、`CoverAspectRatio` 和倒影決定。
- 在手機上請用較小的 `CoverWidth`（約 120），讓兩側各露出一張封面。
- 橫向開始的觸摸拖曳會移動這一排；上下拖曳仍然捲動頁面，觸控板的上下捲動也一樣。

## 相關元件

- `Carousel` 一次翻一頁內容。
- `LightboxGallery` 全螢幕打開照片。
- `HoloCard` 用透視效果展示一張卡片。

| 元件 | 何時選擇 |
|---|---|
| `CoverFlow` | 翻看一排封面 |
| `Carousel` | 帶內容的頁面，一次一頁 |
| `LightboxGallery` | 需要仔細查看的照片 |
