---
title: Feature bento
summary: 便當格功能網格：大小不同的卡片，每張都有一幅小動態繪圖，只在出現在螢幕上時才動。
source-hash: 811dfbf4702c
---

<Demo name="Preview" />

## 何時使用

`FeatureBento` 把功能排成便當格：四種尺寸的卡片排進六列，一兩個重要的功能占大卡片，其餘的圍繞它們填滿。每張卡片頂部有一幅小動態繪圖，下面是標題和一句說明。繪圖是內建的，風格安靜：不斷畫出的折線圖、繞著中心轉動的圓點、輸入命令並給出結果的終端、起伏的柱條、來回傳遞數據包的兩台設備、帶掃描線的盾牌。它們只在卡片出現在螢幕上且動效完整時才動，其他時候保持靜止。

- **平台或產品概覽**，四到八個分量不同的功能。
- **登陸頁區塊**，需要視覺節奏但沒有截圖。

## 何時不用

- **分量相同的功能**：請使用 `SpotlightGrid`，它的卡片大小一致。
- **按步驟講解**：請使用 `ScrollStory`。
- **真實數據**：請使用圖表和分析區塊；這裡的繪圖只是示意。

## 用法

```xml
<arc:FeatureBento Title="One platform, every stage">
    <arc:FeatureBentoItem Title="Live metrics" Art="Pulse" Size="Large" Description="Every request lands on the chart within a second." />
    <arc:FeatureBentoItem Title="Integrations" Art="Orbit" Size="Tall" Description="Connect the tools you already use." />
    <arc:FeatureBentoItem Title="Secure by default" Art="Shield" Size="Small" Description="Single sign-on on every plan." />
</arc:FeatureBento>
```

沒有項目時網格顯示六張虛構的卡片；把 `ShowsSampleItems` 設為 false 則保持為空。

### 尺寸與排列

| 尺寸 | 六列 | 兩列 |
|---|---|---|
| `Small` | 2 × 1 | 1 × 1 |
| `Medium` | 3 × 1 | 1 × 1 |
| `Wide` | 4 × 1 | 2 × 1 |
| `Tall` | 2 × 2 | 1 × 2 |
| `Large` | 4 × 2 | 2 × 2 |

卡片按順序放置，每張放在第一個放得下的空位，所以高卡片旁邊會留出位置給後面的卡片。只有一列時每張卡片占一行。

### 自己的卡片

<Demo name="CustomItems" />

`Art` 選擇繪圖（`None` 表示不畫）。`CommandText` 和 `ResultText` 設定 `Typing` 繪圖的兩行文字。`ItemInvoked` 報告在卡片上的點擊、輕點、Enter 或 Space。

## API 參考

<!-- api: FeatureBento -->

## 無障礙

- 區塊以標題命名，標題是二級標題。
- 每張卡片都可聚焦，以標題命名，說明作為幫助文字；鍵盤焦點環在卡片外 4 像素。
- 繪圖僅作裝飾，對螢幕閱讀器隱藏。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 按閱讀順序經過各張卡片。 |
| 方向鍵 | 下一張或上一張卡片。 |
| `Home` / `End` | 第一張或最後一張卡片。 |
| `Enter` / `Space` | 觸發 `ItemInvoked`。 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 繪圖 | 在螢幕上時持續運動 | 靜止 | 靜止 |
| 懸停的卡片 | 邊框在 160 毫秒內加深 | 立即 | 立即 |

所有繪圖共用一個影格時鐘，卡片滚出視野或窗口隱藏時暫停，繪制時不分配記憶體。

## 響應式

- 區塊自身寬度小於 860（`:compact`）時網格為兩列；小於 520（`:narrow`）時為一列，行高略增。
- 行高固定（240），繪圖保持比例；需要更多空間的文字應盡量簡短。

## 相關元件

- `SpotlightGrid`：大小一致、帶互動光效的卡片。
- `ScrollStory`：隨滚動展開的導覽。
