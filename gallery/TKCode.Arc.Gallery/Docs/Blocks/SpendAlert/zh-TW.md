---
title: Spend alert
summary: 帶消費提醒的預算：已花費金額與預算和本期節奏的對比、為每個提醒畫出刻度的進度條、預算與提醒的編輯器，以及消費越過提醒時出現的橫幅。
source-hash: 7575cac3499d
---

<Demo name="Preview" />

## 何時使用

`SpendAlert` 幫人守住一項預算。它顯示本期已花費的金額與預算的對比、還剩多少、還有幾天，以及按目前的速度本期結束時會花到多少。進度條為人們設定的每個提醒畫出標記，例如預算的 50%、80% 和 100%。下方可以編輯預算和提醒：打開或關閉提醒、修改、刪除或新增。當消費上升並越過一個已打開的提醒時，橫幅會說明發生了什麼，並通過事件告訴應用，以便發送相應的通知。

- **個人理財**：每個分類一項月度預算，並帶通知。
- **團隊或雲服務的花費**：越過閾值時通知負責人。
- **任何有上限的用量**：由使用者自己選擇提醒點。

## 何時不用

- **同時查看很多項預算**：使用 Budget variance 區塊，它把各部門放在同一刻度上比較。
- **帶預測曲線的用量變化**：使用 Usage forecast 區塊。
- **一次性的警告**：使用 `Alert` 或 `Toast`。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。設定 `Title`、`Period`、`Budget`、`Spent`，並用 `DaysElapsed` 與 `DaysInPeriod` 計算節奏。提醒是 `SpendThreshold`，包含占預算的比例 `Share`（0.8 表示 80%）和 `IsEnabled`。

```xml
<arc:SpendAlert Title="Dining out" Period="November" Budget="600" Spent="412.80" DaysElapsed="18" DaysInPeriod="30">
    <arc:SpendThreshold Share="0.5" />
    <arc:SpendThreshold Share="0.8" />
    <arc:SpendThreshold Share="1" />
</arc:SpendAlert>
```

### 越過提醒

只有 `Spent` 上升才會觸發提醒：當新的金額達到一個已打開、而舊金額還沒達到的提醒時，其中最高的那個會顯示在橫幅中、被讀出，並由 `ThresholdCrossed` 報告。`AddSpending` 用於加上一筆消費。橫幅會一直顯示，直到按下 Dismiss（或調用 `DismissAlert`），或者消費、預算或提醒的變化讓它不再成立。編輯提醒本身不會觸發提醒。

### 狀態

`Status` 的值為 OnTrack、AtRisk（還沒有達到提醒，但按目前節奏會超出預算）、Warning（已達到某個提醒）或 Over（消費已超過預算），標題旁的標籤隨之變化。

### 編輯器

預算欄位寫入 `Budget`。每一行提醒有開關、百分比和刪除按鈕；Add alert 在最高的提醒之上加十個百分點新增一個，最多 `MaxThresholds`（5）個。編輯會寫回 `SpendThreshold` 對象並引發 `ThresholdsEdited`。`IsEditable="False"` 會隱藏編輯器，作為摘要卡片使用。

<Demo name="ReadOnly" />

## API 參考

<!-- api: SpendAlert -->

<!-- api: SpendThreshold -->

## 無障礙

- 進度條的名稱包含已花費金額、預算和比例。
- 每個提醒的開關名為 "Alert at 80%"，欄位名為 "Alert share of budget, percent"，刪除按鈕名為 "Remove the 80% alert"。刪除後焦點移到下一行的刪除按鈕，或移到 Add alert。
- 越過提醒時，強制的即時區域會讀出橫幅內容。

## 動效

- 已花費的填充、節奏段和刻度以 smooth 彈簧移動到新值。
- 橫幅上升 6 px 並在 240 ms 內淡入；從警告變為危險時顏色交叉淡化。
- **減弱動效**：橫幅只淡入不上升，進度條立即到位。**關閉**時所有變化立即生效。

## 響應式

- 低於 520 px 時，預算欄位移到提醒標題下方，節奏說明移到剩餘金額下方。
- 進度條占滿區塊寬度；刻度上方的標籤始終留在區塊內。

## 相關組件

- Budget variance 區塊：比較各部門的預算。
- Usage forecast 區塊：帳單周期內的預算線。
- Wallet card 區塊：消費所用的卡片。
