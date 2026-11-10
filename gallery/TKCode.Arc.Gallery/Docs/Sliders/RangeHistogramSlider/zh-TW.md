---
title: 範圍直方圖滑桿
summary: 疊在資料即時直方圖上的價格範圍，範圍內的長條會亮起，滑桿會停靠在長條邊緣。
source-hash: 5b9fb270402a
---

<Demo name="PriceFilter" />

## 何時使用

`RangeHistogramSlider` 用來選取下限和上限，同時顯示資料在兩者之間的分布。使用者在選擇之前就能看到數值集中在哪裡，因此很少會篩選出空結果。

- **目錄或預訂搜尋中的價格篩選**：長條顯示大多數房間約 $140，另一組集中在 $380 附近。
- **規模或時長篩選**：公司員工數、檔案大小、工作階段時長，或其他帶長尾的資料。
- **分析檢視中的範圍**：回應時間或訂單金額，資料的形狀會告訴人們哪一段值得查看。

## 何時不用

- **背後沒有資料的範圍**，例如營業時段：使用帶 `IsRange` 的 `Slider`。
- **單一值**：使用 `Slider`；需要精細控制時使用 `RulerSlider`。
- **必須輸入的精確數字**：使用 `NumberField`。
- **只供閱讀、不用來篩選的圖表**：使用 Charts 分組中的圖表。

## 用法

設定 `Label`，並把原始數值傳給 `Data`。控制項把它們統計到 `Bins` 根長條中。`Data` 可在程式碼中或透過繫結接收清單；資料變化時請指派一個新清單，因為清單內部的修改不會被監聽。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RangeHistogramSlider Label="Price per night"
                              Data="{Binding Prices}"
                              LowerValue="{Binding From}"
                              UpperValue="{Binding To}"
                              Step="5" />
</UserControl>
```

`LowerValue` 和 `UpperValue` 是雙向的，可以為 null，表示刻度的端點。`RangeChanged` 在每次變化時觸發；`ValueCommitted` 在拖曳放開或按鍵改變範圍時觸發一次，這正是發起搜尋的時機。

刻度來自資料：`Minimum` 是向下取整的最小值，且不大於 0；`Maximum` 是向上取整的最大值。如果資料變化時刻度應保持不變，請明確設定它們。

### 長條邊緣與滑桿間距

`SnapToBins` 開啟時（預設），放開後落在長條邊緣 9 px 以內的滑桿會停靠到該邊緣，因此範圍不會把一根長條切成兩半。如果 `Step` 網格上的任何值都同樣合適，請關閉它。`MinStepsBetweenThumbs` 讓兩個滑桿保持距離，以步長計。預設為 1，因此兩個滑桿不會重合。

<Demo name="FreeThumbs" />

### 停用

`IsEnabled="False"` 讓長條和範圍以一半的不透明度保持可見，並把滑桿移出 Tab 順序。

<Demo name="Disabled" />

## API 參考

<!-- api: RangeHistogramSlider -->

## 無障礙

- 每個滑桿都是獨立的滑桿控制項，有自己的定位點。它們的名稱由 `Label` 產生，如「Price per night, minimum」和「Price per night, maximum」，除非設定了 `LowerThumbName` 和 `UpperThumbName`。
- 每個滑桿回報自己能到達的範圍（止於另一個滑桿），並透過 `ValueFormatter` 朗讀其值。
- 圖表對輔助技術而言只是裝飾，它能提供的數字都在滑桿和讀數中。
- 焦點環在鍵盤聚焦時和按鍵之後出現。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Right` / `Up` | 把目前滑桿提高一個 `Step` |
| `Left` / `Down` | 把目前滑桿降低一個 `Step` |
| `Shift` + 方向鍵、`PageUp` / `PageDown` | 移到該方向上的下一個長條邊緣 |
| `Home` / `End` | 轉到此滑桿能到達的最小值或最大值 |
| 在極限處按鍵 | 值不變；滑桿朝按鍵方向用力後彈回 |

## 動效

- 亮起的長條和軌道填充共用一個裁剪，因此它們在同一影格切在滑桿處，即使切在一根長條中間。
- 被按住的滑桿以 snappy 彈簧放大到 1.18 倍。一條細線從滑桿升起穿過圖表：在 160 ms 內淡入，並在 240 ms 內從 60% 長到全高。
- 超過極限時，滑桿最多多走 10 px 後彈回。放開時，速度最多能再帶它走一根長條，然後 snappy 彈簧保持這一速度把它帶到目標值。
- 在滑桿以外的地方按下時，最近的滑桿在數值上立即到位，螢幕上則以 snappy 彈簧移動過去。
- 減弱動效時沒有拉伸、彈簧和慣性：滑桿直接到達目標值，細線以全高直接出現，不淡入。關閉動效時一切立即完成。

## 響應式

控制項填滿父層給出的寬度，沒有中斷點。長條以 2 px 間距平分寬度，可能變得很窄，因此在狹窄的欄中請減少 `Bins`。空間不足時先用省略號截斷標籤，而不是讀數。父層高度不夠時，圖表讓出高度，36 px 的控制列保持不變。觸控與滑鼠的滑桿目標一樣是 44 px，在圖表任意位置按下也有效；控制項不依賴懸停。

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `Slider` | 範圍沒有資料可顯示，或只有一個值 |
| `RulerSlider` | 單一值需要在刻度上精細控制 |
| `ElasticSlider` | 單一值憑手感設定 |
| `NumberField` | 必須輸入精確數字 |
