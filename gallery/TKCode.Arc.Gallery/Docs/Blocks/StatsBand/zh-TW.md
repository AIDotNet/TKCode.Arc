---
title: 數據帶
summary: 進入視野時向上計數的醒目數字，每個都配有一幅佐證它的小圖，懸停時顯示一行背景說明，可平鋪或排成細線格線。
source-hash: a76708197aa5
---

<Demo name="Preview" />

## 何時使用

幾個具體的數字比一段話更能支撐你的說法。`StatsBand` 把三到四個數字並排放在首屏下方或兩個區塊之間，並為每個數字配上一幅佐證它的小圖。

- **用數字支撐說法的行銷頁**：可用性、速度、使用產品的人數。
- **關於頁或投資人頁**：醒目的數字，各附一則簡短說明。

## 何時不用

- **附趨勢的產品即時指標**：請使用 `MetricCard` 或 `MetricsDashboard`。
- **超過四個數字**：分成兩條數據帶，或使用表格。
- **你無法擔保的數字**：取整且沒有來源的數字讀起來像編造的。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:StatsBand Layout="Divided" Title="Quick and steady">
        <arc:StatsBand.Stats>
            <arc:StatsBandStat Value="38" Suffix="ms" Label="Median response" Detail="At the edge" />
            <arc:StatsBandStat Value="99.98" Decimals="2" Suffix="%" Label="Uptime" Detail="Over 90 days" />
            <arc:StatsBandStat Value="2340000" Notation="Compact" Decimals="1" Label="Deploys" Detail="This year" />
        </arc:StatsBand.Stats>
    </arc:StatsBand>
</UserControl>
```

`StatsBand` 衍生自 `TemplatedControl`。`Stats` 為空時顯示範例數字，上線前請替換。`Layout` 可以是 `Plain`（數字浮在留白上）或 `Divided`（上下有線的細線格線）。

### 單個數據

| 屬性 | 作用 |
|---|---|
| `Value`、`Decimals` | 最終數字及其小數位數 |
| `Prefix`、`Suffix` | 數字前後的文字，繪製得更小更淡，單位放在這裡：%、ms、+、/5 |
| `Notation` | `Compact` 寫成 2.3M，並在該單位內計數，從 0.0M 到 2.3M |
| `Label`、`Detail` | 它衡量什麼，以及一行簡短的說明 |
| `Context` | 懸停或聚焦時取代 `Detail` 的一行文字，例如「Up from 41%」 |
| `Visual` | 一幅小圖，見下文 |

`Locale` 決定數字分隔符號（預設 `en-US`），緊湊單位始終是 K、M、B 和 T。

<Demo name="OwnNumbers" />

### 小圖

每個 `Visual` 都是選用的；但只要有一個數據帶圖，所有數據都會保留這一列，讓數字對齊。

| 類型 | 資料 | 呈現為 |
|---|---|---|
| `StatsBandTrend` | `Values`，最舊的在前 | 迷你折線圖，最後一個值上有一個點 |
| `StatsBandUptime` | `Days`，單位是百分比 | 每天一根柱。有停機的日子明顯更短並著色 |
| `StatsBandDistribution` | `Bins`、`Maximum`、`Marker` | 直方圖，標記值以下的柱著色 |
| `StatsBandMap` | `Points`，經度和緯度 | 點陣世界地圖，每個地點點亮一個點，並吸附到最近的陸地 |

懸停或聚焦某個數據時，它的小圖採用強調色，其他小圖保持中性。

<Demo name="Numbers" />

### 最佳實務

**建議**

- 使用具體的數字：99.98% 比「幾乎一直在線」更有說服力。
- 選擇能佐證數字的小圖，以及一行用於比較的背景說明。
- 標籤控制在幾個詞內，讓各欄對齊。

**避免**

- 沒有理由地混用有無背景說明的數據。大家從第一個數據學會懸停。
- 一個頁面放多條數據帶。

## API 參考

<!-- api: StatsBand -->

<!-- api: StatsBandStat -->

<!-- api: StatsBandTrend -->

<!-- api: StatsBandUptime -->

<!-- api: StatsBandDistribution -->

<!-- api: StatsBandMap -->

## 無障礙

- 數據帶是一個以 `Title` 命名的區域，沒有標題時以 `AccessibleLabel`（「Key numbers」）命名。
- 每個數據以其最終值、標籤、說明和背景說明命名。計數中的數字不會被朗讀。
- 附 `Context` 的數據可以取得焦點，因此鍵盤或點按也能顯示這行文字。小圖屬於裝飾。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在附背景說明的數據之間移動，焦點會顯示這行文字 |

## 動效

- 數據帶第一次有 40% 進入視野時，每個數字在 `CountDuration`（1.6 秒）內以強緩出從零開始計數，每個數據上升 8 px 並淡入。兩者都間隔 90 ms。
- 數字等寬，最終值預留好空間，因此計數時不會有內容移動。
- 數字基本落定後，小圖開始繪製：線條和地圖在 1.1 秒內從左側擦入，柱形以 420 ms 的掃動從基線長出，趨勢線的末端圓點最後落定。
- 在 `Divided` 中，上下兩條線在 900 ms 內畫過去，數據之間的細線在 700 ms 內長出。
- 懸停時，說明列在 240 ms 內交叉淡化為背景說明列，並有 4 px 的位移。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 進場 | 計數、上升並繪製 | 立即顯示最終值 | 立即顯示最終值 |
| 懸停 | 帶位移的交叉淡化，混入強調色 | 文字立即切換，240 ms 混入強調色 | 全部立即 |

呼叫 `Replay` 可重新播放進場動畫。

## 響應式

- `StatsBand` 跟隨自身寬度，而不是視窗寬度。數字在 40 到 52 px 之間縮放。
- 一列最多四個數據。寬度不超過 800 px（`:stacked`）時排成兩欄，三個數據時排成一欄，因此不會有落單的數據。
- 寬度不超過 520 px（`:phone`）時每列一個數據，小圖位於數字旁邊。
- 細線跟隨格線：相鄰數據之間是直線，列之間是橫線。
- 觸控：點按會聚焦數據，從而顯示背景說明列。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `StatsBand` | 行銷頁上幾個有佐證的數字 |
| `MetricCard` | 產品內一個附背景的數字 |
| `AnimatedCounter` | 一個會變化的總數 |
| `MetricsDashboard` | 附圖表的即時指標 |
