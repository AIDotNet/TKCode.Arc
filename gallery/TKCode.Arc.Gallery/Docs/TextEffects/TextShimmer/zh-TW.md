---
title: 文字閃光
summary: 用平緩的光帶掃過文字，表示正在進行的工作。
source-hash: 4fc30a945d04
---

<Demo name="StatusLine" />

## 何時使用

- 應用程式忙碌時的狀態標籤，例如「思考中」或「正在產生摘要」。
- 隨工作推進而變化的狀態列，例如「正在讀取 14 個檔案」與「正在起草計畫」。
- 工作結束後沉澱為完成狀態的標籤。

## 何時不用

- 頁面載入時的佔位內容：用 `Skeleton`。
- 可以量測的進度：用 `Progress`，它顯示已完成的比例。
- 逐字母變化且沒有掃光的標籤：用 `TextMorph`。
- 長文字或多行文字：掃光僅適用於一行簡短文字。

## 用法

只要 `IsActive` 為 `true`（預設值），掃光就會持續進行。工作完成後將 `IsActive` 設為 `false`，文字隨即沉澱為實色。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextShimmer Text="思考中" />
</UserControl>
```

### 更改文字

新的 `Text` 值會在原地升起，同時該列以彈簧方式調整到新寬度。步驟變化期間請保持 `IsActive` 為開啟狀態。

<Demo name="ChangingAndFinishing" />

### 完成

請將 `IsActive` 設為 `false`，而不是移除控制項。掃光會平滑退場，文字變為實色，因此該列不會突然跳動。

```csharp
summary.Text = "摘要已就緒";
summary.IsActive = false;
```

### 掃光速度

`SweepDuration` 設定光帶掃過文字一次所需的時間。預設值為 1.8 秒。

<Demo name="SlowerSweep" />

### 尺寸

文字使用 `TextElement` 的字型屬性。較大的字級適用於狀態標題。

```xml
<arc:TextShimmer Text="正在產生摘要" FontSize="22" />
```

## API 參考

<!-- api: TextShimmer -->

## 無障礙

- 文字即為控制項的名稱，並且始終保持可讀。
- 只要 `IsActive` 為 `true`，控制項就會透過 `AutomationProperties.ItemStatus` 回報忙碌狀態。
- 掃光僅為裝飾，不會被逐字母朗讀。
- 控制項本身不播報變化。需要播報完成狀態時，請在父元素上使用即時區域。
- 本控制項無法取得焦點，也沒有鍵盤互動。

## 動態效果

- 一條文字顏色的柔和光帶掠過較暗的底色。每次掃過耗時 `SweepDuration`，隨後光帶短暫停留，再開始下一次掃過。
- 掃光僅在控制項處於啟用、接近可視範圍且可見時運行，並從停止的位置繼續。
- 新標籤從柔和的模糊中升起。
- 將 `IsActive` 設為 `false` 時，光帶平滑退場，文字變為實色。
- 減少動態效果時停止掃光，標籤以一般淡入淡出切換。關閉動態效果時立即切換標籤。

## 相關元件

- `Skeleton`：頁面載入時的佔位內容。
- `Progress`：可以量測的工作進度。
- `TextMorph`：簡短標籤的狀態切換。
