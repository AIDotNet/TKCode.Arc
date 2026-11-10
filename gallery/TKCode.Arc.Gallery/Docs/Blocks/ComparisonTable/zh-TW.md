---
title: Comparison table
summary: 一張我方與對手的比較表，帶固定表頭、醒目欄和手機堆疊檢視。
source-hash: 02f4e5dd2ddd
---

<Demo name="Preview" />

## 何時使用

在定價頁或替代方案頁使用 `ComparisonTable`，展示你的產品與具名替代方案的差異，適合三到五欄、分組列的情境。

## 何時不用

- **比較自己的方案**：使用 `PlanComparison` 區塊。
- **需要排序的大型資料集**：使用資料表格。
- **無法查證的說法**：競品名稱保持通用或符合事實。

## 用法

```csharp
table.Columns =
[
    new ComparisonColumn { Id = "us", Name = "Relay", Caption = "$10 per seat", IsHighlighted = true },
    new ComparisonColumn { Id = "them", Name = "Legacy suite", Caption = "$24 per seat" }
];
table.Sections =
[
    new ComparisonSection
    {
        Id = "core", Title = "Core",
        Rows =
        [
            new ComparisonRow { Id = "offline", Feature = "Offline mode", Values = { ["us"] = true, ["them"] = false } },
            new ComparisonRow { Id = "sso", Feature = "SSO", Values = { ["us"] = true, ["them"] = ComparisonValue.Partial("Top plan") } },
            new ComparisonRow { Id = "seats", Feature = "Seats", Values = { ["us"] = "Unlimited", ["them"] = "Up to 50" } }
        ]
    }
];
table.Cta = new ComparisonCta { Label = "Start free trial", Href = "https://example.com/signup" };
```

<Demo name="CustomData" />

值可以是 `bool`（勾號或短橫）、字串（顯示文字）或 `ComparisonValue.Partial(note)`。沒有值的欄顯示短橫。用 `IsHighlighted` 標記你的產品；否則第一欄就是你的產品。

### 只看差異

開關設定 `IsDifferencesOnly`，隱藏所有可見欄取值相同的列；備註不計入比較。一個分組沒有剩餘列時，其標題也會收起。

```xml
<arc:ComparisonTable MaxTableHeight="560" StickyTop="64" IsDifferencesOnly="True" />
```

### 固定表頭

表格捲過時表頭保持固定，對任意可捲動的祖先都生效。`StickyTop` 讓它偏移到固定網站頁首下方。`MaxTableHeight` 限制表格高度並在區塊內部捲動。

### 窄容器

寬度低於 `StackBelow` 時，表格顯示你的欄和一個透過選擇器（`CompareWith`）選定的競品。

<Demo name="Narrow" />

### 行動呼籲

`Cta` 在你的欄底部新增按鈕。設定 `Href` 且沒有 `Command` 時開啟連結，除非 `CtaInvoked` 的處理常式將事件標記為已處理。否則執行命令並變形為 `DoneLabel`。

## API 參考

<!-- api: ComparisonTable -->

<!-- api: ComparisonCtaEventArgs -->

## 無障礙

- 每個儲存格都有帶欄名的替代文字：「Big suite: Included」「Partial, Top plan」「Not included」。
- 含義從不只依賴顏色：勾號、半圓和短橫形狀不同，並有圖例說明。
- 開關的名稱取自 `DifferencesLabel`，列被篩選時即時區域會讀出提示。
- `Tab` 可到達開關、選擇器和行動呼籲；`Space` 切換開關。

## 動效

- 你的欄中的勾號在所在列完全進入檢視區時繪製一次，歷時 480 ms，延遲 60 ms 後每列錯開 35 ms，最多到第十二列。
- 「只看差異」用 smooth 彈簧收起列和分組標題。
- 選擇競品時表頭捲動 8 px，標記彈出（透明度與 .8 縮放，snappy 彈簧）。
- 行動呼籲變形寬度，並以 2 px 模糊切換文字。
- 減弱動效時標記立即顯示，切換只淡入淡出，收起沒有彈簧。關閉動效時立即完成。

## 響應式

- 區塊跟隨自身寬度。820 px 時特性欄變窄；640 px 時間距收緊；低於 `StackBelow`（640 px）時堆疊為兩欄並顯示選擇器。
- 欄寬按比例分配，因此不會橫向捲動。列懸停只對滑鼠或觸控筆生效。

## 相關元件

- `PlanComparison` 區塊：比較自己的定價層級。
- `PricingCalculator` 區塊：隨席位數變化的價格。
