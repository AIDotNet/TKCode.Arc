---
title: 空狀態集
summary: 同一幅插圖中的四種空狀態，切換標籤頁時，圖形會在各場景之間變形。
source-hash: 3680320a4c58
---

<Demo name="Preview" />

## 何時使用

- 頁面或面板可能因多種原因而為空：沒有搜尋結果、沒有連線、沒有待讀內容，或頁面不存在。
- 空狀態有一個明確的下一步操作，且該操作需要一點時間，例如重試、重新整理或搜尋。
- 設定頁或說明頁中並排展示某個產品的各種空狀態。

## 何時不用

- 只有一個帶圖示和按鈕的空清單：使用 `EmptyState`。
- 內容仍在載入：使用 `Skeleton`。
- 頁面已顯示內容時出現錯誤：使用 `Alert`。

## 用法

沒有任何項目時，區塊會顯示 Arc 的四個場景及其文案。處理 `ActionInvoked` 並設定 `Phase`，即可顯示操作的結果。區塊本身不執行任何操作。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:EmptyStates Label="工作區" />
</UserControl>
```

### 場景與文案

每個 `EmptyStatesItem` 對應一個標籤頁。`Scene` 決定圖形，可選值為 `NoResults`、`Offline`、`CaughtUp` 或 `NotFound`。為 Idle 階段設定 `Header`、`Title`、`Description` 與 `ActionLabel`，並為結果設定 `Done` 版本。未設定的文字使用 Arc 為該場景提供的文案。只有一項時，會隱藏標籤列。

<Demo name="CustomItems" />

```xml
<arc:EmptyStates>
    <arc:EmptyStatesItem Scene="Offline"
                         Title="同步已暫停"
                         ActionLabel="恢復同步" />
    <arc:EmptyStatesItem Scene="NotFound" />
</arc:EmptyStates>
```

### 操作與階段

按下按鈕會引發 `ActionInvoked`，參數為該項目以及按下時所處的階段，隨後以相同參數執行 `ActionCommand`。工作進行期間將 `Phase` 設為 `Loading`：按鈕顯示載入指示器，描述變為 `LoadingDescription`，標題與按鈕文字保持不變。成功時設為 `Done`，失敗時設為 `Idle`。載入期間，按下操作會被忽略。

<Demo name="AsyncRetry" />

```csharp
states.ActionInvoked += async (_, e) =>
{
    if (e.Phase == EmptyStatesPhase.Done)
    {
        states.Phase = EmptyStatesPhase.Idle;
        return;
    }
    states.Phase = EmptyStatesPhase.Loading;
    var ok = await sync.RetryAsync();
    states.Phase = ok ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
};
```

要在失敗的嘗試之後更改文案，請在階段仍為 `Loading` 時設定項目的 `Description`。新文字會隨切換回 `Idle` 一同出現。

### 選取

`SelectedIndex` 與 `SelectedItem` 是雙向的，並保持同步。選取另一個標籤頁會將 `Phase` 恢復為 `Idle`。若要在開啟時就顯示某個結果，請與選取的項目一同設定 `Phase`。

```xml
<arc:EmptyStates SelectedIndex="1" Phase="Done" />
```

### 窄版配置

區塊跟隨自身寬度變化。寬度在 420 及以下時，標籤頁使用較小的文字與內距。寬度在 560 及以上時，標題變大。放不下的標籤會保持其寬度，標籤列會捲動，以保持選取的標籤頁可見。

<Demo name="Narrow" />

## API 參考

<!-- api: EmptyStates -->

<!-- api: EmptyStatesItem -->

## 無障礙

- 區塊是一個由 `Label` 命名的區域。標籤列是一個由 `TabListLabel` 命名的標籤頁清單，每個標籤頁都會回報自己是否被選取。
- 只有選取的標籤頁可透過 Tab 鍵取得焦點。ArrowLeft 與 ArrowRight 在標籤頁之間移動，並首尾循環。Home 與 End 跳到第一個與最後一個標籤頁。移動即選取該標籤頁。
- 圖形是一幅以其所示內容命名的影像，例如“一朵雲透過一條斷開的線連接到一台裝置”。設定 `IllustrationLabel`、`LoadingIllustrationLabel` 與 `DoneIllustrationLabel`，即可以其他語言為圖形命名。
- 每次階段變化後，新的標題與描述，或載入文字，都會以禮貌式播報。

## 動態效果

- 切換標籤頁時，圖形的每個形狀都會以彈簧動畫過渡到下一個場景。輪廓逐點變形，線條稍後再逐步繪出。顏色在 480 毫秒內交叉過渡。
- 階段變化時，以較短的延遲輕推同一批形狀。
- 舊文案帶著模糊向上移開，新的標題與描述隨之升起。文案高度以彈簧方式調整以符合內容，因此按鈕不會跳動。標籤頁的醒目提示會滑動到選取的標籤頁。
- 使用滑鼠時，圖形的各層會略微朝指標方向傾斜。
- 動態效果等級為 Reduced 時，舊圖形交叉淡化為新圖形，文案淡入淡出。等級為 Off 時，立即切換。

## 相關元件

- `EmptyState`：用於帶圖示、文字說明與操作的單一空清單。
- `Skeleton`：內容仍在載入時使用。
- `Alert`：已顯示內容的頁面中出現錯誤時使用。
