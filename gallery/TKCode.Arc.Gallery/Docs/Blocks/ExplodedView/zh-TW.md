---
title: 分解檢視
summary: 畫面隨捲動逐層拆開：圖層向後傾斜、帶著標籤分開，依序輪流成為焦點並各有一段說明，最後再合回成一個畫面。
source-hash: ef8070d398d7
---

<Demo name="Preview" />

## 何時使用

`ExplodedView` 是用於行銷或發表頁面的區段，用來說明一個畫面是如何組成的。一疊大小相同、各自帶有一張圖的圖層，一開始是一個平面的畫面。讀者捲動時，這疊圖層向後傾斜並轉動，圖層在深度方向上分開，每一層旁邊都會伸出一個標籤。接著圖層輪流成為焦點：其他圖層變暗，焦點圖層採用強調色，舞台下方的說明則解釋它。最後圖層收攏，畫面重新立起來。一排停點按鈕可以直接跳到完整畫面、任一圖層，或拼回後的畫面。

- **設計系統或 UI 套件頁面**：圖層是同一個範例畫面的權杖、格線、元件與覆蓋層，每段說明都寫出該圖層負責什麼。
- **產品發表**：逐層帶讀者走過重新設計後的畫面，從背景到浮動的提示，每層只講一個重點。
- **帶螢幕的硬體**，例如手錶或車用顯示器：圖層是玻璃、介面與其背後的感測器。
- **案例研究**：說明一個團隊如何把一個繁忙的畫面拆成各自只負責一件事的平靜圖層。

## 何時不用

- **人們需要閱讀或操作的內容。** 舞台是一張圖：它不接受輸入，螢幕閱讀器會略過它（圖層改以文字列出）。真正的控制項請放在頁面本身。
- **需要使用者完成的一連串步驟。** 任務的進度請使用 `Stepper`。
- **供瀏覽的一組投影片或圖片。** 請使用 `Carousel`，它一次只顯示一個項目，並以按鈕與滑動翻頁。
- **有幾張插圖的長篇文章。** 隨捲動推進的區段會搶走注意力；每頁一個最適合閱讀。
- **超過八層。** 右側的標籤會堆疊起來，停點也會擠在一起；請把故事拆成兩個區段。

## 用法

每個圖層加入一個 `ExplodedLayer`，由下往上排列。每個圖層的 `Content` 可以是任何控制項，並以 `LayerSize` 配置（預設為 280 × 620，即手機螢幕的比例）。`Name` 是標籤的第一行、說明的標題與停點按鈕的名稱；`Note` 是標籤的第二行；`Body` 是圖層成為焦點時顯示的說明。`StartCaption` 與 `EndCaption` 則是圖層疊開之前與收攏之後的說明。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExplodedView Height="760"
                      Title="Made of layers"
                      Description="Every layer does one job, and together they stay quiet.">
        <arc:ExplodedView.StartCaption>
            <arc:ExplodedCaption Title="A single screen" Body="Scroll to lift it apart, layer by layer." />
        </arc:ExplodedView.StartCaption>
        <arc:ExplodedLayer Name="Base" Note="Two neutral tokens" Kind="Plate"
                           Body="One quiet base layer under everything else.">
            <Panel />
        </arc:ExplodedLayer>
        <arc:ExplodedLayer Name="Content" Note="Fixed-width numbers"
                           Body="The numbers, the chart and the list.">
            <TextBlock Margin="16,52" Text="38.6 km" FontSize="34" />
        </arc:ExplodedLayer>
    </arc:ExplodedView>
</UserControl>
```

`ExplodedView` 衍生自 Avalonia 的 `TemplatedControl`。舞台是 `Progress` 的函式，`Progress` 從 0（完整畫面）到 1（拼回後的畫面），隨捲動位置變化；`SmoothedProgress` 是經過柔和、過阻尼彈簧平滑後的值，舞台實際繪製的就是它。`CurrentStop` 在完整畫面時為 0，各圖層為 1 到 N，拼回後的畫面為 N + 1；每當它改變時都會引發 `StopChanged`。控制項會設定 `:exploded`、`:wide`、`:compact`、`:tall-stage`、`:framed` 與 `:region-focus` 這幾個偽類，供樣式使用。

### 結構

1. **引言**（`Title`、`Description`）：標題使用展示字型，隨控制項變寬，字級從 28 px 增至 52 px；其下是一到兩句說明。
2. **舞台**：各圖層以透視繪製。`Plate` 圖層是不透明的，帶有 1 px 邊框；`Sheet` 圖層只顯示其內容，在圖層分開時會出現虛線外框；`Grid` 圖層是一張薄板，其內容隨分開而淡入，用於只存在於分解檢視中的參考線。所有圖層共用 `LayerCornerRadius`。
3. **標籤**：右側每個圖層一個，位於圖層右邊緣的一個點上（即底部以上 `AnchorFromBottom` 處），接一條 24 px 的引線，然後是名稱與註記。圖層疊開時，標籤由下往上依序畫出。
4. **說明**：目前停點的標題與內文。說明區的高度與最高的說明相同，因此頁面不會跳動。
5. **停點按鈕**：每個停點一個 36 × 44 的按鈕，上面有一個刻度；目前的刻度較粗，並採用強調色。
6. **外框**（`IsFramed`）：1 px 邊框、薄板的圓角與淡化的填色。在容器模式中，它就是捲動區域。

### 捲動模式

`ScrollMode` 決定進度從哪裡來：

- `Container`（預設）：控制項在自己的外框內捲動。外框內有一段高度為 `Length` 個外框高（預設 4 個）的空白軌道，舞台在軌道經過時保持固定。觸控與捲動鍵會原生地捲動它，滾輪一格移動 100 px，與瀏覽器相同；捲到任一端時，外框會保留滾輪事件，而不是捲動頁面。外框的高度取自 `Height`，若未設定，則為視窗高度的 88%，範圍在 620 至 860 px 之間。
- `Page`：控制項是頁面自己的 `ScrollViewer` 內、高度為 `Length` 個可視區域的軌道，舞台在軌道經過時固定在可視區域中。此模式請關閉外框。
- `External`：沒有任何捲動。由你自己設定 `Progress`，可以來自滑桿、時間軸或你自己的捲動位置。

<Demo name="PageScroll" />

### 驅動進度

在外部模式中，`Progress` 是唯一的輸入：繫結或設定它，舞台就會以其彈簧跟隨。`GoToStop(n)` 在任何模式下都會滑到某個停點，`Reset()` 則回到完整畫面。`GetStopName(n)` 與 `GetStopCaption(n)` 會傳回某個停點的文字，例如要把它們顯示在頁面的其他位置時使用。

<Demo name="SliderProgress" />

```csharp
view.StopChanged += (_, e) =>
{
    // e.Stop: 0 whole screen, 1..N a layer, N + 1 put back together. e.Layer is null at the ends.
    analytics.Track("exploded-stop", e.Stop);
};
```

### 自訂圖層

圖層就是一般的控制項，因此任何圖片都可以：一張卡片、一個儀表板、一台裝置。把 `LayerSize` 改為它們的設計尺寸，並把 `LayerCornerRadius` 改為它們的圓角。縮放會讓分解後的堆疊與其標籤在任何尺寸下都留在舞台之內。

<Demo name="CustomLayers" />

### 窄版容器

版面依控制項自己的寬度變化，而不是視窗的寬度。寬度從 880 px 起，引言與說明會放在一個 380 px 的欄位中，位於全高的舞台旁邊。寬度在 560 至 879 px 之間時，引言在上方，說明則放在停點按鈕旁邊。寬度低於 560 px 時，停點按鈕共用說明上方的一列，標籤只顯示名稱；當舞台比寬度高時，堆疊轉得較少、散開得較遠（`TallMaxTilt`、`TallMaxTurn`、`TallLayerGap`），使標籤仍然放得下。

<Demo name="Narrow" />

### 減少動態效果

在減少動態效果時，堆疊不會逐漸傾斜或分開：兩端顯示平面的畫面，每個圖層停點則顯示完全分開的堆疊。停點會立即跟隨捲動位置，說明與顏色直接切換而不淡入淡出，停點按鈕則直接跳轉而不滑動。

<Demo name="ReducedMotion" />

### 調整場景

`MaxTilt`（60°）、`MaxTurn`（−40°）與 `LayerGap`（136 px）設定完全分解時的姿態；`Perspective`（2400）設定透視的強度；`DimmedLayerOpacity`（0.5）設定其他圖層在某一圖層成為焦點時淡化的程度。進度的時間軸是固定的：堆疊在 0.04 至 0.22 之間向後傾斜，在 0.08 至 0.28 之間分開，各圖層停點平均分佔 0.28 至 0.80，收攏則是展開的反向。

## API 參考

<!-- api: ExplodedView -->

<!-- api: ExplodedLayer -->

<!-- api: ExplodedCaption -->

## 無障礙

- 在容器模式中，外框是一個可取得焦點的捲動區域，名稱取自 `Title`，並由 `RegionLabelFormat` 組成（「… Scroll to take it apart, one layer at a time.」）。鍵盤焦點環會在外框外側顯示 2 px；以指標取得焦點時則不顯示。
- 舞台與其標籤對輔助技術隱藏。一個隱藏的清單（名稱為 `LayerListLabel`）會由下往上，把每個圖層朗讀為「名稱: 內文」。
- 停點按鈕是一組，名稱為 `StopsLabel`。每個按鈕以其停點命名（`AssembledText`、圖層名稱、`ReassembledText`），目前的按鈕則以 `CurrentStopText` 作為狀態回報。目前的刻度除了顏色之外也會加粗，因此不只依賴顏色。
- 每當停點改變時，說明會以禮貌模式被朗讀。

### 鍵盤

| 按鍵 | 位置 | 操作 |
|---|---|---|
| `ArrowUp` / `ArrowDown` | 外框 | 捲動 40 px |
| `PageUp` / `PageDown`、`Shift+Space` / `Space` | 外框 | 捲動外框高度減 40 px |
| `Home` / `End` | 外框 | 捲到起點或終點 |
| `ArrowLeft` / `ArrowRight` | 外框 | 無動作 |
| `Tab` | 外框、停點按鈕 | 依序從外框移到每一個停點按鈕 |
| `Enter` 或 `Space` | 停點按鈕 | 捲動到該停點；焦點留在按鈕上 |

## 動態效果

- 舞台以過阻尼彈簧（剛度 330、阻尼 60）跟隨進度：它會在捲動的一個影格內開始移動，約 130 ms 時到達一半，約 750 ms 時靜止，且沒有過衝。中途反向時會保留速度，因此來回捲動仍然順暢。
- 展開時，堆疊先向後傾斜並轉動，接著圖層分開，標籤由下往上畫出；收合時，圖層先合攏，標籤由上往下消失，然後畫面重新立起。
- 停點改變時，舊的說明在 180 ms 內向上離開，新的說明在 480 ms 內上移 10 px 就位。圖層的變暗在 480 ms 內緩和完成；外框、標籤顏色與刻度在 240 ms 內變色；新的刻度則以帶彈性的 580 ms 曲線變粗。薄板的邊框與外框的虛線會立即切換。
- 停點按鈕會把捲動位置滑到該處，距離越遠時間越長（最長 700 ms）。
- 靜止時沒有任何動畫。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 捲動 | 舞台以其彈簧跟隨 | 兩端為平面，每個圖層停點完全分開 | 同減少 |
| 停點改變 | 說明交叉淡化，變暗與顏色緩和變化 | 一切立即切換 | 立即 |
| 停點按鈕 | 滑動到該停點 | 直接跳轉 | 直接跳轉 |

## 相關元件

- `Stepper`，用於任務步驟的進度。
- `Carousel`，用於以按鈕與滑動瀏覽投影片。
- `ScrollArea`，用於帶有 Arc 捲軸的一般捲動區域。
- `SegmentedControl` 與 `Avatar`，範例畫面就是由它們組成的。

| 元件 | 適用情境 |
|---|---|
| `Stepper` | 人們逐步完成一項任務 |
| `Carousel` | 數張圖片或投影片一次瀏覽一張 |
| `Tabs` | 故事的每個部分都是獨立的檢視，依名稱選取 |
| `ScrollArea` | 內容只需要捲動 |
