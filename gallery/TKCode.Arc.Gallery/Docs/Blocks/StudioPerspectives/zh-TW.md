---
title: Studio perspectives
summary: 評價舞台：一次展示一條大號引言，附上說話人和公司，會自動切換，切換時舊的模糊消失、新的上升出現，並帶進度條、箭頭和暫停按鈕。
source-hash: 18c6de3706a2
---

<Demo name="Preview" />

## 何時使用

`StudioPerspectives` 是一個評價舞台：它不是一牆小卡片，而是把整個寬度給一條引言，用展示字型大號排出，下面是說話人、職位和公司。幾秒後，舊引言模糊消失，下一條上升出現，人物稍晚一拍跟著切換。每條引言對應一根細進度條，顯示當前位置和距離下一條還有多久；箭頭和暫停按鈕讓讀者接手控制。指針停在舞台上或焦點進入舞台時計時會暫停，讀者不會在閱讀中途錯過內容。

- **登陸頁**中介於功能和價格之間的社會證明區。
- **案例頁**，輪流展示幾位客戶的評價。
- **宣傳或活動頁**，有幾條有分量的引言。

## 何時不用

- **很多條需要一起瀏覽的短評**：請使用卡片網格。
- **評分和星級**：本區塊沒有評分。
- **自己團隊的成員**：請使用 `TeamShowcase`。

## 用法

```xml
<arc:StudioPerspectives Title="What studios say" Interval="0:0:8">
    <arc:StudioPerspectivesQuote Text="We plan from one board now." Name="Amara Lindqvist" Role="Creative Director" Company="Fieldnote Studio" />
    <arc:StudioPerspectivesQuote Text="Nothing gets in the way." Name="Diego Ferraz" Role="Lead Engineer" Company="Harbor and Pine" />
</arc:StudioPerspectives>
```

沒有引言時舞台顯示四條虛構的引言；把 `ShowsSampleQuotes` 設為 false 則保持為空。引言的 `Image` 會替換生成的頭像。

### 自己的引言

<Demo name="CustomQuotes" />

`SelectedIndex` 是雙向的。`Next()`、`Previous()`、`Pause()` 和 `Play()` 可在程式碼中控制舞台；`IsPaused` 反映暫停按鈕的状態。`IsAutoAdvance` 關閉計時，`Interval` 設定每條停留多久（7 秒）。`QuoteChanged` 報告每一次切換，以及是否由計時觸發。

## API 參考

<!-- api: StudioPerspectives -->

## 無障礙

- 舞台以標題命名，標題是二級標題。
- 上一條、暫停和下一條按鈕的名稱分別是 `PreviousLabel`、`PauseLabel` 或 `PlayLabel`，以及 `NextLabel`。
- 讀者自己切換時會播報（「Quote 2 of 4, Diego Ferraz, Lead Engineer · Harbor and Pine」）；計時切換不播報，不會打斷螢幕閱讀器。
- 指針在舞台上、焦點在舞台內或按下暫停後，計時都會停止，滿足可暫停移動內容的要求。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 移到各個按鈕。 |
| `Left` / `Right` | 焦點在舞台內時切到上一條或下一條。 |
| `Home` / `End` | 第一條或最後一條。 |
| 按鈕上的 `Enter` / `Space` | 按下按鈕。 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 引言切換 | 舊引言模糊消失，新引言上升 4 像素並變清晰；高度以彈簧跟隨 | 120 毫秒交叉淡化 | 立即 |
| 人物切換 | 在引言之後 80 毫秒上升出現 | 交叉淡化 | 立即 |
| 進度條 | 在 `Interval` 內填滿 | 填滿 | 填滿 |

## 響應式

- 區塊自身寬度小於 640（`:compact`）時，引言從 36 降到 22，邊距收緊。
- 舞台最寬 880；長引言會換行，舞台高度以彈簧隨之增長。
- 只有一條引言時，按鈕和進度條會隱藏。

## 相關元件

- `TeamShowcase`：介紹自己的團隊。
- `Carousel`：任意內容的通用輪播。
- `HeroSignup`：通常作為頁面開頭的區塊。
