---
title: Spotlight grid
summary: 由一束柔光照亮的功能卡片：光隨指針或鍵盤焦點在整個網格上移動，經過之處卡面變暖、邊框發亮。
source-hash: 03d439378de3
---

<Demo name="Preview" />

## 何時使用

`SpotlightGrid` 用安靜的卡片展示一組功能（圖示塊、標題、一句說明和可選的標記），並加上一個效果：一束強調色的柔光隨指針在整個網格上移動。光下的卡面略微變暖，卡片邊框發亮，相鄰卡片的邊緣也會亮起，看起來像同一個光源在頁面上移動，而不是每張卡片各自的懸停效果。指針離開時光會淡出；鍵盤把焦點移到某張卡片時，光會移到那張卡片上。

- **登陸頁的功能區**，三到九個功能。
- **深色營銷頁**，用一點細微的互動讓網格更有生氣。

## 何時不用

- **自帶配圖的功能**：請使用 `FeatureBento`，它的每張卡片都有動態繪圖。
- **靜止時就要看起來可點的導航卡片**：請使用帶明確行動按鈕的 `Card`。
- **密集的數據**：請使用表格或指標區塊。

## 用法

```xml
<arc:SpotlightGrid Title="Everything in one place" MaxColumns="3">
    <arc:SpotlightGridItem Title="Instant search" Description="Every document answers in under 50 ms." Icon="{x:Static arc:ArcIcons.Search}" />
    <arc:SpotlightGridItem Title="Branch previews" Description="Every change gets its own link." Badge="New" />
</arc:SpotlightGrid>
```

沒有項目時網格顯示六個虛構的功能；把 `ShowsSampleItems` 設為 false 則保持為空。`Icon` 接受任何 24×24 的描邊圖形，例如 `ArcIcons`。

### 只有網格

<Demo name="CustomItems" />

沒有小標籤、標題和說明時，標題區會省略，只留下網格。只要每列不窄於 `MinColumnWidth`（260）就會增加列，最多 `MaxColumns`（3）列。`IsSpotlightEnabled` 可關閉光效。`ItemInvoked` 報告在卡片上的點擊、輕點、Enter 或 Space。

## API 參考

<!-- api: SpotlightGrid -->

## 無障礙

- 區塊以標題命名，標題是二級標題。
- 每張卡片都可聚焦，以標題命名，說明作為幫助文字；鍵盤焦點環在卡片外 4 像素。
- 光效只是裝飾：沒有任何內容依賴它，開關光效對比度都不變。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 依次經過各張卡片；光跟隨焦點。 |
| 方向鍵 | 按行列在卡片之間移動。 |
| `Home` / `End` | 第一張或最後一張卡片。 |
| `Enter` / `Space` | 觸發 `ItemInvoked`。 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 光跟隨指針 | 沒有回彈的快速彈簧 | 直接跟隨 | 直接跟隨 |
| 光出現和淡出 | 160 毫秒出現，180 毫秒淡出 | 相同 | 立即 |

跟隨指針不分配記憶體：光的漸變已緩存，每一影格只移動繪制的原點。

## 響應式

- 列數隨寬度變化：寬頁面三列，平板兩列，手機一列。
- 區塊自身寬度小於 720（`:compact`）時，標題變小，邊距收緊。
- 在觸控螢幕上，手指按下時光出現在手指下，抬起時淡出。

## 相關元件

- `FeatureBento`：帶動態繪圖的卡片網格。
- `Card`：單張內容卡片。
