---
title: Market terminal
summary: 一個交易介面：可縮放、可平移的 K 線圖，附迷你走勢線與閃爍價格的即時自選清單，以及帶委託單清單的市價 / 限價下單票。
source-hash: 78e493f1a00a
---

<Demo name="Preview" />

## 何時使用

`MarketTerminal` 是「一次看一個代碼、交易一個代碼」的產品主介面。圖表、自選清單和下單票讀的是同一個即時價格，所以一次跳動會同時帶動標頭價格、自選列、K 線和預估金額。

- **券商或投資應用。** 在自選清單裡選一個代碼，研究它的 K 線，然後在旁邊的下單票裡買進或賣出。
- **模擬交易與教學。** 預設一切都在本機模擬：價格會動，限價單會掛著並自行成交，沒有任何資料離開介面。
- **內部行情看板。** 用鍵盤逐根查看 K 線，比較 20 與 50 根均線，一鍵在今天與一年之間切換。

## 何時不用

- **盤口與深度。** 此區塊每個代碼只顯示一個即時價格，不顯示每個價位的買賣掛單。
- **業務指標。** 營收、註冊或用量沒有開高低收；請使用 `LineChart` 或 `BarChart`。
- **持倉總覽。** 列出持倉、損益與占比需要表格，而不是單一代碼的圖表。
- **同時看很多代碼。** 需要一格格小圖時，請在表格裡為每個代碼放一個 `Sparkline`。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。把自選清單以 `MarketSymbol` 傳入；每個代碼有代碼、名稱、目前價格、昨收、日波動率和平均成交量。每個範圍的 K 線都由這些數字產生，同一個代碼每次產生的結果都一樣，所以圖表在多次執行之間保持穩定。`Holdings`、`Orders` 和 `BuyingPower` 用來填入下單票。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MarketTerminal Ticker="ORBT" Range="SixMonths" BuyingPower="18420.50">
        <arc:MarketSymbol Ticker="ORBT" Name="Orbital Freight" Price="184.53" PreviousClose="182.97" Volatility="0.024" />
        <arc:MarketSymbol Ticker="KLVN" Name="Kelvin Grid" Exchange="NYSE" Price="64.12" PreviousClose="64.80" />
        <arc:MarketTerminal.Holdings>
            <arc:MarketHolding Ticker="ORBT" Shares="120" />
        </arc:MarketTerminal.Holdings>
    </arc:MarketTerminal>
</UserControl>
```

### 結構

- **標頭**：代碼、公司名稱與交易所；逐位滾動的價格；帶箭頭的今日漲跌；今日開盤、最高、最低與成交量；以及 Live 按鈕。
- **工具列**：時間範圍切換（1D、5D、1M、6M、1Y）、兩個均線開關，以及放大、縮小與重設檢視按鈕。
- **圖表**：讀數列、價格格線上的 K 線、下方的成交量、均線、帶標籤的即時價格線，以及帶價格與時間標籤的十字線。
- **自選清單**：每個代碼一列，包含名稱、疊在昨收虛線上的今日迷你走勢線、價格和今日漲跌。目前圖表代碼所在的列帶有填色。
- **下單票**：買進或賣出、市價或限價、股數、限價（僅限價單）、價格、預估成本或所得、購買力或持股數、狀態列、送出按鈕和委託單清單。

### 範圍與圖表

`Range` 決定 K 線：`OneDay` 使用今天的 5 分鐘 K 線，`FiveDays` 使用 30 分鐘 K 線，`OneMonth`、`SixMonths` 與 `OneYear` 使用日線。今天在每個範圍裡都是同一個交易時段：今天的 30 分鐘線和日線都由 5 分鐘線合併而來，一次跳動會同時移動每個範圍的最後一根 K 線。`ShowMa20` 和 `ShowMa50` 控制是否顯示均線（工具列開關與之雙向繫結）。

預設視窗顯示整個範圍；在較窄的圖表上會減少可見根數，讓 K 線之間至少相隔五個像素。滾動滾輪會以指標為中心縮放，拖曳平移（甩動後會繼續移動並逐漸減速），在觸控螢幕上可雙指捏合，也可以使用工具列。指標在圖表上移動時顯示十字線，並把那根 K 線寫進讀數列。

<Demo name="Intraday" />

### 即時價格

`IsLive` 為 true 時，模擬每隔 `TickInterval`（預設 1.2 秒）前進一步：每個價格隨機走一小步，標頭價格滾動，自選列的價格閃一下綠色或紅色，圖表把最後一根 K 線、它的標籤和均線平滑地移到新價格。Live 按鈕可以暫停和恢復；區塊不在視窗中時跳動也會停止。若要顯示真實行情，請保持 `IsLive` 為 false，並在每次報價時呼叫 `SetPrice(ticker, price)`；`Tick()` 讓模擬前進一次。

### 委託單

下單票會在按鈕可送出之前檢查每一筆委託：至少一股、限價大於零、買進時購買力足夠、賣出時持股足夠。原因寫在按鈕下方。

- **市價單**立即依即時價格成交。
- 目前就能成交的**限價單**立即依限價與市價中較有利的價格成交；否則它會**掛單等待**，價格到達限價時自行成交。掛單帶有撤單按鈕。
- 成交會更新 `BuyingPower`（雙向）和 `Holdings`。`OrderPlaced`、`OrderFilled` 與 `OrderCanceled` 回報每一步。

若要透過真實服務下單，請設定 `PlaceOrderAction`。它接收一個 `MarketOrderRequest` 和一個取消權杖；工作完成之前按鈕顯示載入指示，擲回例外時委託不會進入清單，例外訊息寫在按鈕下方。`CancelOrderAction` 對撤單做同樣的事。`SubmitOrderAsync()` 和 `CancelOrderAsync(order)` 與按鈕的作用相同。

```csharp
terminal.PlaceOrderAction = (request, cancellation) => broker.PlaceAsync(request.Ticker, request.Side, request.Shares,
    request.Type == MarketOrderType.Limit ? request.Price : null, cancellation);
terminal.OrderFilled += (_, e) => portfolio.Refresh();
```

<Demo name="OrderApi" />

### 窄版容器

區塊讀取的是自身寬度。寬度不小於 1080 時，自選清單和下單票疊成圖表旁邊 340 寬的一欄；不小於 720 時，它們在圖表下方並排；更窄時所有卡片縱向堆疊。小於 640 時，統計數字單獨占一列、分成四個等寬欄，範圍切換占滿整列，工具列按鈕增大到 44 以利觸控。讀數列放不下時會折到第二列。

<Demo name="Narrow" />

### 文案

所有文字都來自 `Labels`（一個 `MarketTerminalLabels`）。替換它即可在地化區塊：帶參數的項目是接收已格式化數值的函式，例如按鈕用的 `Submit`（"Buy 10 ORBT"），或狀態列用的 `NotEnoughShares`。

## API 參考

<!-- api: MarketTerminal -->

<!-- api: MarketSymbol -->

<!-- api: MarketHolding -->

<!-- api: MarketOrder -->

<!-- api: MarketOrderRequest -->

<!-- api: MarketRange -->

<!-- api: MarketOrderSide -->

<!-- api: MarketOrderType -->

<!-- api: MarketOrderStatus -->

<!-- api: MarketTerminalLabels -->

## 無障礙

- 圖表是一個可聚焦的元素，名稱包含代碼、範圍和 K 線週期（"ORBT price chart, six months of daily candles"），說明文字會說明可用的按鍵。用鍵盤移動十字線時，會透過禮貌模式的即時區域朗讀那根 K 線的時間、開盤、最高、最低、收盤、漲跌和成交量。
- 每個自選列都是一個按鈕，名稱包含代碼、名稱、價格和漲跌（"up 0.85 percent today"）。
- 漲跌從不只靠顏色表達：它們帶有正負號和箭頭，委託狀態以文字寫出。
- 委託的問題和失敗寫在按鈕下方的那一列，這一列是禮貌模式的即時區域；下單、成交與撤單都會播報。
- 指標聚焦不繪製焦點環；鍵盤聚焦時，圖表、自選列、開關和按鈕周圍會顯示 Arc 的焦點環。

### 鍵盤

- **Tab** 依序經過 Live 按鈕、範圍切換、均線開關、縮放按鈕、圖表、自選列和下單票。
- 在圖表上，**Left** 和 **Right** 把十字線移動一根（按住 **Shift** 時移動十根），十字線越過邊緣時檢視跟著捲動。**Home** 與 **End** 跳到第一根和最新一根。**+** 與 **-** 以十字線為中心縮放，**0** 重設檢視，**Escape** 清除十字線。
- 在自選清單中，**Up** 與 **Down** 在代碼之間移動，**Home** 與 **End** 跳到第一個和最後一個。**Enter** 或 **Space** 把焦點所在的代碼顯示到圖表上。
- 範圍切換和下單票裡的切換遵循分段控制項的規則：方向鍵改變選取。

## 動態效果

- **縮放、平移與切換範圍**：可見視窗和價格刻度以指數方式逼近目標，時長取自 smooth 彈簧，因此不會過衝，新的手勢可以在中途讓運動反向。拖曳後甩出會保持速度，然後像拋出的卡片一樣逐漸減速。
- **即時跳動**：最後一根 K 線、它的價格標籤、價格線和均線平滑地移到新價格；標頭價格滾動；每個變動的自選價格閃一下綠色或紅色，並在 900 毫秒內淡出。選取列的填色在列之間滑動。
- **下單票**：限價輸入框以高度與透明度一起展開，收合更快；新委託從清單頂部長出來。
- 價格流動時 Live 圓點會脈動；它是區塊裡唯一的循環動畫，因為它回報的是一種狀態。
- 圖表只在有東西在動時才繪製影格。減少動態效果時，檢視、價格和面板直接就位，沒有慣性和脈動，閃爍改為短暫的靜止色塊。關閉動態效果時所有變化立即生效。

## 相關

- `Budget variance`，Finance 分組的另一個區塊。
- `SegmentedControl`、`NumberField`、`AnimatedCounter` 和 `Button`，組成此區塊的控制項。
- 不需要 K 線時可使用 `Sparkline` 和 `LineChart`。
