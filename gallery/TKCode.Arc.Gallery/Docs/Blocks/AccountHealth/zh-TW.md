---
title: Account health
summary: 一個客戶帳戶的健康狀況：分數及其等級和每週走勢、續約日期和剩餘天數、未關閉的工單、按指標查看的使用量、風險，以及能預估挽回分數的建議行動。
source-hash: 9825ce6f3fc4
---

<Demo name="Preview" />

## 何時使用

`AccountHealth` 把一個客戶帳戶放在一張卡片上，給負責續約的人看。三個方塊回答最先要問的問題：帳戶有多健康（分數、等級以及幾週來的變化）、什麼時候續約還剩幾天、有多少工單未關閉。下面是可以切換指標的使用量圖表、按嚴重程度排列的風險，以及可以勾選的建議行動，勾選後能看到預計挽回的分數。

- **準備續約。** 通話前先看走勢、未關閉的工單和風險。
- **每週的帳戶回顧。** 每個帳戶一張卡片，每次布局都一樣。
- **制定挽留計劃。** 勾選要採取的行動，讀出預計分數。

## 何時不用

- **同時看很多帳戶。** 請用 `Table` 列出帳戶，再為其中一個打開本區塊。
- **原始使用量分析。** 深入分析請使用 `LineChart` 或分析類區塊。
- **無法解釋的分數。** 本區塊只顯示分數；請在能展示輸入的地方計算它。

## 用法

本區塊派生自 `TemplatedControl`。沒有健康歷史時，它顯示一個虛構的帳戶。提供 `HealthHistory`（每週分數，從早到晚）、`RenewalDate`、`Usage`、`Tickets`、`Risks` 和 `Actions`。

```xml
<arc:AccountHealth AccountName="Copperleaf Hotels" Subtitle="Scale plan" RenewalWarningDays="90" />
```

```csharp
account.HealthHistory.AddRange([58, 61, 63, 66, 70, 74, 79, 81]);
account.Usage.Add(new AccountHealthUsageMetric("Active properties", [12, 14, 18, 21, 26, 28], "properties") { Limit = 40 });
account.ActionToggled += (_, e) => plan.Save(e.Action, e.ProjectedScore);
```

### 分數、續約與行動

`Score` 是歷史中的最後一個值；75 分起為健康，50 分起為觀察，低於 50 為有風險。`DaysToRenewal` 從 `Today`（或今天）算起；在 `RenewalWarningDays` 之內，這一行變為警告色。勾選一項行動會寫入 `IsDone`，更新 `ProjectedScore`（分數加上所有已完成行動的影響，最多 100）並引發 `ActionToggled`。`SelectedMetric`（雙向）是使用量圖表的指標。文字都在 `Labels`（`AccountHealthLabels`）中。

<Demo name="OwnAccount" />

## API 參考

<!-- api: AccountHealth -->

<!-- api: AccountHealthUsageMetric -->

<!-- api: AccountHealthTicket -->

<!-- api: AccountHealthRisk -->

<!-- api: AccountHealthAction -->

## 無障礙

- 帳戶名稱是二級標題；每個部分有三級標題。
- 每個方塊讀作一行（"Health score 64, Watch, −18 in 11 weeks"）。
- 使用量圖表就是 Arc 的 `LineChart`，帶有自己的摘要和鍵盤讀數。
- 行動是核取方塊；預計分數變化時會以禮貌方式播報。

## 動效

- 健康走勢第一次顯示時從左向右畫出（700 毫秒，僅完整動效）。
- 使用量圖表像 `LineChart` 一樣在指標之間變形。
- 減少動效和關閉動效時兩者都立即顯示。

## 響應式

- 在 `CompactWidth`（720 px）或更窄時，三個方塊上下排列，清單也上下排列；內邊距收緊到 16。
- 只有一個指標時，指標切換會隱藏。
- 過長的工單標題和風險說明會換行。

## 相關元件

- `LineChart`、`Badge` 和 `Checkbox`，組成本區塊的控制項。
- `Metrics dashboard` 和 `KPI drilldown`，用於整個產品的數字。
- `Call insights` 和 `Lead scoring`。
