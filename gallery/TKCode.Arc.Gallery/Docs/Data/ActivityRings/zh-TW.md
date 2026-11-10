---
title: Activity rings
summary: 表示每日目標的同心圓環。每個圓環隨彈簧填滿，到達目標時合上，超過目標後繼續繞圈；旁邊的圖例數字會滾動。
source-hash: c09dc14af601
---

<Demo name="Preview" />

## 何時使用

`ActivityRings` 用一組同心圓環同時展示幾個每日目標，最外圈排在第一個。圓環從 12 點方向順時針填充，達到目標時合上；超過目標後繼續繞圈，新的一圈壓在第一圈上面，頭部下方帶一點陰影，所以「超額完成」一眼就能看出。數值變化時圓環從目前位置隨彈簧繼續走，旁邊的圖例寫出每個目標的名稱，並滾動顯示數值。

- **每日目標**：步數、運動分鐘、站立小時、喝水、閱讀頁數。
- **連續打卡與習慣頁面**：一排小圓環就能看完一週。
- **可以超額的指標**：常常被超過的銷售或學習目標，超出的部分也有意義。

## 何時不用

- **需要精確比較**：圓環靠感覺讀；需要比較數值時請用長條圖。
- **超過四個目標**：內圈會變小，不好區分。請用一列進度條。
- **只有一個數值**：普通的進度環或 `Gauge` 更簡單。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActivityRings Label="Today's goals">
        <arc:ActivityRing Label="Move" Value="380" Goal="520" Unit="kcal" />
        <arc:ActivityRing Label="Exercise" Value="22" Goal="30" Unit="min" />
        <arc:ActivityRing Label="Stand" Value="9" Goal="12" Unit="hr" />
    </arc:ActivityRings>
</UserControl>
```

`ActivityRings` 在 Special 套件中；請在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。每個目標放一個 `ActivityRing`。`Value` 達到 `Goal` 時圓環填滿，`Progress` 表示繞了多少（1.4 表示滿一圈再多 40%）。沒有設定 `Brush` 的圓環按順序使用主題的系列顏色。

### 超過目標

超過目標後圓環繼續繞圈，最多四圈。上面那一圈在頭部前方投下一點陰影，所以即使顏色相同也能看出重疊。

<Demo name="Overflow" />

### 不帶圖例的小圓環

把 `IsLegendVisible` 設為 false，再給較小的 `Diameter` 和 `RingThickness`，就是一個緊湊的檢視，例如一週的每一天。圓環顯示之後，某個數值達到目標時會觸發 `GoalReached`；`Replay()` 讓圓環從空重新填滿。

<Demo name="Week" />

### 最佳實踐

**建議**

- 只放兩到四個目標，名稱要短，比如「Move」。
- 給 `Label` 一個整體名稱，比如「Today's goals」：輔助技術會先讀它。
- 使用 `Unit`，讓圖例讀作「380 / 520 kcal」。

**避免**

- 給沒有自然目標的數值用圓環。
- 使用相近的顏色：相鄰的圓環必須容易區分。

## API 參考

<!-- api: ActivityRings -->

<!-- api: ActivityRing -->

## 無障礙

- 整組圓環是一個元素，名稱來自 `Label`。它的說明文字會讀出每個目標：「Move: 380 of 520 kcal, 73 percent.」
- 圖例列只給眼睛看，不會一列一列地讀。
- 某個圓環達到目標時，一條禮貌的狀態訊息會說明，例如「Move goal reached」（`GoalReachedText`）。
- 圓環不可互動，也不取得焦點。

## 動效

- 第一次顯示時，圓環從空開始依次填滿，最外圈最先，每圈間隔 90 ms；彈簧大約 0.9 s 停穩，帶一點回彈。圖例數字同時向上滾動。
- 之後的變化從圓環目前位置隨彈簧繼續走；中途再次變化時保留速度。
- 只有彈簧運動時才請求影格；圓弧原地更新，每個影格不配置記憶體。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 第一次顯示 | 圓環依次填滿 | 直接顯示數值 | 直接顯示數值 |
| 新數值 | 隨彈簧到新數值 | 直接跳到 | 直接跳到 |
| 圖例數字 | 滾動 | 直接跳到 | 直接跳到 |

## 響應式

- 空間足夠時圓環使用 `Diameter`，空間較小時縮小以適應；圓環粗細不變。
- 寬度小於 340 px（`SideLegendMinWidth`）時圖例移到圓環下方，大於時放在旁邊。
- 圖例中的名稱過長時以省略號結束，不換行。

## 相關元件

- `Gauge` 和 `DonutChart` 展示一個數值或整體中的佔比。
- `UsageMeter` 是帶上限的額度條。

| 元件 | 適用場景 |
|---|---|
| `ActivityRings` | 幾個每日目標，超出目標也算數 |
| `Gauge` | 刻度上的一個數值 |
| `UsageMeter` | 一個有硬上限的額度 |
