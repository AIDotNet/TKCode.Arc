---
title: 進度
summary: 顯示已知總量任務的完成進度。
source-hash: d642f7397d3e
---

<Demo name="CountedValue" />

## 何時使用

- 上傳、匯入等總工作量已知的長時間任務。
- 需要讓使用者一眼讀到百分比的情境，此時開啟 `IsValueVisible`。

## 何時不用

- 無法度量進度的載入：用 `Skeleton` 預留空間。
- 相對於上限的配額：用 `UsageMeter`。
- 儀表板指標：用 `Gauge`。
- 多步驟流程中的位置：用 `Stepper`。

## 用法

設定 `Value`，若總量不是 100，還需設定 `Maximum`。進度條為唯讀，無法取得焦點。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Progress Label="正在上傳 4 個檔案"
                  IsValueVisible="True"
                  Value="35" />
</UserControl>
```

### 僅進度條

不設定標籤和數值時，進度條單獨使用。其無障礙名稱為 "進度"。

<Demo name="BarOnly" />

### 自訂最大值

`Percentage` 為 `round(Value / Maximum × 100)`，例如 1,392 / 2,400 顯示為 58%。

<Demo name="CustomMaximum" />

### 切換標籤

切換 `Label` 時，新文字從柔和的模糊中升起，舊文字同時淡出。

<Demo name="ChangingLabel" />

```xml
<arc:Progress Label="{Binding PhaseName}"
              Value="{Binding PhaseProgress}" />
```

### 完成

達到 100% 時，填滿色變為成功色，計數旁出現勾號。使用 `:complete` 偽類別為該狀態設定樣式。

```csharp
upload.Value = 100;
// 此時 upload.IsComplete 為 true；開啟 IsValueVisible 時會顯示勾號。
```

進度條本身不會朗讀完成狀態。任務結束時，請透過 Toast 等方式給出提示。

## API 參考

<!-- api: Progress -->

## 無障礙

- `Label` 是無障礙名稱。未設定標籤時，名稱為 "進度"。
- 控制項以百分比形式公開數值，因此螢幕閱讀器會朗讀目前的百分比。
- 它不是即時區域。任務結束時請另外播報完成狀態。
- 無法取得焦點，也沒有鍵盤互動。

## 動態效果

- 同一條平滑彈簧同時驅動填滿與計數的百分比，因此數字始終與進度條一致。
- 達到 100% 時，填滿在短暫延遲後變為成功色，勾號以輕微縮放和模糊的方式落定。
- 標籤變化時，新標籤從柔和的模糊中升起，舊標籤淡出。
- 減少動態效果時，填滿與計數直接跳到新值，標籤以短暫淡入切換。

## 相關元件

- `Skeleton`：工作量未知且內容仍在載入時使用。
- `UsageMeter`：數值佔滿帶上限的配額時使用。
- `Gauge`：儀表板上的單一指標。
- `Stepper`：任務由一連串具名步驟組成時使用。
