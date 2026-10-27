---
title: Choropleth explorer
summary: 依分位數分檔為地圖著色，並搭配連動的排行列表；選擇一個地區即可飛入並對其縣排名，切換指標會讓新的顏色掃過整張地圖，也可以更換國家。
source-hash: 1c3add29dcfa
---

<Demo name="Preview" />

## 何時使用

`ChoroplethExplorer` 在一個畫面上回答兩個問題：數值在哪裡高、哪裡低，以及一個地區為什麼是這個值。區域內的每個地區都依所選指標著色，同樣的地區在地圖旁邊排名，選擇其中一個會讓相機飛入該地區，顯示下一級，並重新分檔、重新排名。

- **依地理位置檢視營收、客戶或用量。** 州、省或地區依總量著色，領先者與長尾都列在列表中。
- **某一期間內的變化。** 發散型指標以零為界：下降用一種色相，成長用另一種，因此成長地圖一眼就能讀懂。
- **市場涵蓋情況。** 每個地區的占比（已簽約帳戶占目標帳戶的比例），放大後沒有資料的地區會如實顯示為無資料。

## 何時不用

- **精確比較。** 顏色分檔會把數值歸組；要比較幾個地區之間的精確差異，請使用長條圖或表格。排行列表有幫助，但不是主要的閱讀方式。
- **非地理類別。** 只有當位置本身有意義時，著色才有意義。產品線或團隊請使用矩形樹狀圖或排行長條列表。
- **點資料。** 門市、辦公室或事件應放在有標記的地圖上，而不是著色區域上。
- **無法交付的地圖。** 此區塊繪製的是你提供的輪廓，本身不包含任何地圖。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。你給它一個或多個 `ChoroplethArea`（國家），每個帶有自己的 `ChoroplethRegion`，以及用來著色的 `ChoroplethMetric`。地區以區域檢視框（預設 975 × 610）內的 SVG 路徑資料提供輪廓，另有邊界框（選用：可省去為放置相機而解析路徑的開銷）、每個指標的值，以及選用的、位於同一檢視框內的下級區域。輪廓只在第一次繪製時解析一次。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ChoroplethExplorer Description="Fiscal year ending March 31, 2027">
        <arc:ChoroplethExplorer.Metrics>
            <arc:ChoroplethMetric Key="revenue" Label="Revenue"
                                  HeadlineLabel="Annual recurring revenue"
                                  LegendCaption="Shaded by annual recurring revenue" />
            <arc:ChoroplethMetric Key="growth" Label="Growth" Format="Change" Scale="Diverging"
                                  Aggregate="WeightedAverage" />
        </arc:ChoroplethExplorer.Metrics>
        <arc:ChoroplethArea Key="us" Name="United States" RegionNoun="states" RegionNounSingular="state"
                            SubregionNoun="counties" SubregionNounSingular="county" />
    </arc:ChoroplethExplorer>
</UserControl>
```

```csharp
var california = new ChoroplethRegion("CA", "California", pathData, new Rect(18.7, 160, 143.9, 247));
california.SetMetricValue("revenue", 9_400_000);
california.SetMetricValue("growth", 4.2);   // percent units: +4.2%
area.Regions.Add(california);
```

本頁的示範讀取公有領域的輪廓（美國用美國人口普查局的製圖邊界檔案，其他國家用 Natural Earth 的一級行政區），這些輪廓是隨圖庫一起發布的文字檔；所有數值均為虛構。

### 結構

- **標題區**：標題（`Title`、區域的 `Title`，或來自 `ChoroplethLabels.TitleFormat` 的 "Revenue by state"）、`Description`、國家開關（僅在多於一個區域時出現，每個選項都帶有地區數量）與指標開關。
- **摘要**：區域或已開啟地區的名稱、頭條數字（總和、各地區的加權平均值，或已開啟地區自己的值）以及指標的 `HeadlineLabel`。放大時，有一個按鈕可以返回上一級。
- **地圖**：每個地區依其所在分檔著色；指向某個地區會為它描邊，並顯示一張卡片，包含它的排名、所有指標（目前指標突出顯示）以及選擇它會發生什麼。
- **圖例**：每個分檔一個色階，分檔邊界位於其間；當下級區域缺少值時會多一個無資料色階；圖例說明會寫出指標下的色階（"10 states $120K to $230K"）。
- **排行列表**：每個地區顯示排名、名稱、數值與一根長條；帶有下級區域的地區以箭頭標記。指向某一行會為對應地區描邊，指向地圖則會找到對應的行。

### 分檔與顏色

連續型指標會依目前檢視中數值的分位數切成 `BinCount`（5）檔；發散型指標先在零處分開，兩側各依自己的分位數切分，分檔數量依落在各側的數值多寡比例分配。每個邊界值取兩位有效數字，等於邊界的值歸入其上一檔。每一級都會重新計算分檔：放大之後，縣有自己的分檔。

正向色相是強調色，強調色為灰色時則用藍色；負向色相是與之相對的琥珀色；沒有值的地區使用邊框顏色。設定 `PositiveColor`、`NegativeColor` 與 `NoDataColor` 即可使用你自己的顏色。

<Demo name="CustomLook" />

### 放大

選擇一個地區（在地圖上或列表中）會設定 `OpenRegionKey`、觸發 `RegionOpened`，並讓相機飛行，使該地區占滿地圖的 80%（縮放不超過 12 倍）。其他地區保留為淡化的背景，可以選擇它們直接飛過去。有下級區域時，該地區讓位給它們，圖例與列表重新分檔、重新排名，選擇某個下級區域會將其固定（`PinnedSubregionKey`、`SubregionPinned`）。沒有下級區域時，列表會說明沒有更細的明細，並提供返回的途徑。`ZoomTo(key)` 與 `ZoomOut()` 可在程式碼中完成同樣的操作。

<Demo name="Events" />

### 單一區域與發散型指標

只有一個區域時，國家開關會消失。`SelectedMetricKey` 選擇起始指標；這裡是一個以零為界的成長指標。

<Demo name="SingleArea" />

### 卡片外觀與窄容器

`IsCardChrome` 把區塊繪製成有邊框的卡片。區塊會讀取自己的寬度：從 820 起，列表位於地圖旁邊，高度與地圖欄完全一致；低於這個寬度時，列表移到地圖下方（高 400，寬度低於 560 時為 360），控制項移到標題下方；低於 560 時，內距收緊、頭條數字縮小，圖例鋪滿整個寬度。

<Demo name="Card" />

### 文字

除資料之外的每一個詞都來自 `Labels`，即帶有英文預設值的 `ChoroplethLabels`：計數（"51 states"，使用區域的單數與複數名詞）、"Ranked by revenue"、圖例的色階名稱、懸停卡片的提示、返回按鈕、空面板以及每一則播報。指標自帶 `Label`、`Noun`、`HeadlineLabel`、`LegendCaption`、`CurrencySymbol`，以及選用的 `Formatter` 與 `TickFormatter`。

## API 參考

<!-- api: ChoroplethExplorer -->

<!-- api: ChoroplethArea -->

<!-- api: ChoroplethRegion -->

<!-- api: ChoroplethMetric -->

<!-- api: ChoroplethLabels -->

<!-- api: ChoroplethValueFormat -->

<!-- api: ChoroplethScale -->

<!-- api: ChoroplethAggregate -->

<!-- api: ChoroplethRegionOpenedEventArgs -->

<!-- api: ChoroplethSubregionPinnedEventArgs -->

<!-- api: ChoroplethSelectionChangedEventArgs -->

## 無障礙

- **Tab** 依序到達國家開關、指標開關（一個停靠點；方向鍵移動選擇）、放大時的返回按鈕、每個圖例色階，以及列表（一個停靠點，停在你上次所在的行）。
- 在列表中，**↑** 與 **↓** 每次移動一行並在兩端停住，**Home** 與 **End** 跳到第一行與最後一行；取得焦點的行會在地圖上描邊並捲動到可見位置。
- 在地區所在行上按 **Enter** 或 **Space** 會放大，並把焦點移到其下級區域的第一行（沒有下級區域時移到返回按鈕）；在下級區域所在行上則是固定或釋放它。
- 在圖例色階上按 **Enter** 或 **Space** 會固定它；再按一次則釋放。
- **Escape** 先釋放已固定的圖例色階，然後縮小回上一級，並把焦點還給該地區所在行。
- 地圖是一張名為 "Map of United States shaded by revenue." 的影像，帶有指向列表的說明文字；列表承載同樣的資料，是用鍵盤或螢幕閱讀器探索的途徑。
- 每一行以排名、名稱與數值命名（"1st, California, $9.8M. Open 58 counties"）；負值讀作 "minus 4.8%"。下級區域所在行是切換按鈕，會回報自己是否已固定。
- 每個圖例色階是一個切換按鈕，以數量與範圍命名（"10 states $120K to $230K"）。
- 一個禮貌型即時區域會播報放大（"California, 58 counties ranked by revenue. Press Escape to zoom out."）、縮小、新的指標及其領先者、新的國家，以及已固定的下級區域及其數值與排名。
- 數值帶有正負號與真正的減號；方向從不只依賴顏色。
- 焦點環為 2px，位於元素外側，並且只在鍵盤操作時顯示。

## 動態效果

- **切換指標**：每個地區的顏色用 480 ms 過渡，延遲從西向東遞增（其中心的 x 除以地圖寬度再乘以 260 ms），因此新的著色在不到 750 ms 內掃過整張地圖。各行以臨界阻尼彈簧滑到新的排名，每根長條用 580 ms 增長或縮短；文字用 240 ms 交叉淡入淡出；頭條數字直接跳變。
- **放大或縮小**：相機的縮放與位置共用一根臨界阻尼彈簧（約 800 ms，可在飛行途中掉頭）。其他地區用 240 ms 淡化到 26%；下級區域從 140 ms 起用 480 ms 淡入，因此在相機仍在飛行時就已出現；離開時用 160 ms 淡出。頭條數字用 480 ms 計數到新的值。
- **指向**：懸停卡片用 160 ms 淡入並跟隨指標；列表滑動，使該地區所在行置中。在圖例上，被點亮的色階高度增長到 1.6 倍，其他地區淡化到 14%，其他行淡化到 32%，均用 240 ms。
- **更換國家**：舊地圖用 180 ms 淡出，新地圖從西向東淡入；列表淡出後再淡入。
- 減少動態效果只保留文字的交叉淡入淡出：顏色、相機、行、長條與懸停卡片立即變化。關閉動態效果則讓所有變化立即生效。

## 相關

- `SegmentedControl` 與 `MorphSelect`，標題區中的開關。
- `Metric explorer` 與 `Revenue explorer`，其他拆解數字的 Analytics 區塊。
- `Revenue globe`，以 3D 檢視呈現依位置劃分的營收。
