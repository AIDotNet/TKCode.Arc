---
title: 新手引導清單
summary: 一步一步引導新工作區完成設定。
source-hash: 000b2cbf4093
---

<Demo name="Workspace" />

## 何時使用

新手引導清單帶領使用者完成一次性的簡短設定。圓環和計數跟隨每次勾選，完成的步驟劃掉後收進「已完成」列，下一步自動展開。

- **新帳戶或新工作區**：三到八個步驟，例如建立專案、邀請團隊，順序不限。
- **本身就能完成工作的步驟**：為步驟設定 `ActionLabel` 和 `Action`。任務執行時按鈕顯示進度，任務完成後自動勾選該步驟。
- **只有使用者自己能確認的步驟**：不設定操作，由圓形勾選框手動標記。
- **需要保留一段時間的清單**：`IsHidden` 把它收起到只剩標題列，可以留在儀表板上而不佔據頁面。

## 何時不用

- **順序嚴格的流程**：使用 `Stepper`，它顯示固定序列中的一個位置。
- **單一執行中任務的進度**：使用 `Progress`，它衡量已知的工作量。
- **不斷增長的任務清單**：使用 `Table` 或清單；本控制項用於有終點的簡短設定。
- **單一問題**：使用 `Checkbox`。

## 用法

在控制項內列出步驟，並為每一步設定穩定的 `Id`。設定 `Title`，也可以設定所有步驟完成時顯示的 `DoneTitle`（預設是「You're all set」）。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OnboardingChecklist Title="Before your first release" DoneTitle="Ready to ship">
        <arc:OnboardingChecklistStep Id="guide" Title="Read the release guide" Description="It covers versioning." />
        <arc:OnboardingChecklistStep Id="owner" Title="Add a second owner" />
    </arc:OnboardingChecklist>
</UserControl>
```

### 手動勾選的步驟

沒有 `ActionLabel` 的步驟沒有按鈕，圓形勾選框是完成它的唯一方式。在步驟收起之前再次勾選，或者從「已完成」列勾選，都會把它標記為未完成並放回清單。

<Demo name="ByHand" />

### 操作與錯誤

`Action` 是 `Func<Task>`。執行期間按鈕顯示載入圈並忽略按壓。任務完成後步驟被勾選；任務擲回例外時步驟保持展開，並在按鈕上方顯示例外訊息，使用者可以重試。`Command` 也可以執行步驟，適用於檢視模型。

<Demo name="FailingAction" />

### 儲存進度

把 `Completed` 設為之前已完成步驟的鍵，這些步驟一開始就位於「已完成」列。每當使用者或操作改變它們時，`CompletedChanged` 按步驟順序回報這些鍵，便於儲存。`CompleteStep` 和 `ReopenStep` 在程式碼中做同樣的事，例如工作在別處完成時。

<Demo name="PartlyDone" />

### 隱藏清單

Hide 按鈕收起主體，保留帶圓環和計數的標題列。繫結 `IsHidden` 可以在多次造訪之間保留這一選擇；使用者變更時由 `HiddenChanged` 回報。

<Demo name="Hidden" />

### 其他語言

控制項自身寫出的文字（可見的和朗讀的）都在 `Strings` 中。設定新的 `OnboardingChecklistStrings` 即可翻譯。

## API 參考

<!-- api: OnboardingChecklist -->

<!-- api: OnboardingChecklistStep -->

`OnboardingChecklistStrings` 為每段固定文字提供一個文字屬性：`Show`、`Hide`、`ShowChecklist`、`HideChecklist`、`CountFormat`、`Completed`、`Progress`、`ProgressFormat`、`OpenSteps`、`CompletedSteps`、`StepDoneFormat`、`StepUndoneFormat`、`AllDoneFormat` 和 `ActionFailed`。

## 無障礙

| 按鍵 | 操作 |
|---|---|
| Tab | 在勾選框、標題、操作、Hide 按鈕和「已完成」列之間移動 |
| Space、Enter | 按下目前聚焦的勾選框、標題、操作或按鈕 |
| Up、Down | 在步驟標題之間移動，到達兩端時循環 |
| Home、End | 移至第一個或最後一個步驟標題 |

已聚焦的步驟完成時，焦點移到下一步的操作；沒有操作時移到它的標題；沒有剩餘步驟時移到「已完成」列。從「已完成」列重新開啟步驟時，焦點移到該步驟的勾選框。正在收起或已隱藏部分中的控制項不取得焦點。

控制項是一個具名群組，名稱跟隨標題，並以禮貌方式播報每次變化，例如「Create a project done. 1 of 4 complete.」。圓環把自己回報為名為「Setup progress」的進度列，狀態類似「1 of 4 steps done」。每個勾選框都是以步驟命名的核取方塊，標題、Hide 按鈕和「已完成」列會回報是否已展開。正在執行或已完成的操作仍可取得焦點，並回報其狀態。

## 動效

勾選步驟時，填充以 snappy 彈簧從 0.6 放大，勾號在 240 ms 內繪製，一條 1 像素的刪除線在 480 ms 內劃過標題，標題換行時逐行劃過。720 ms 後步驟收進「已完成」列。列以 smooth 彈簧展開，以稍快的彈簧收起；圓環以 morph 彈簧填充，最後一步完成時在中心繪製勾號。數字按計數變化的方向翻轉，標題和 Hide 文字從柔和的模糊中升起，且不改變所在位置的尺寸。

減弱動效時，尺寸和圓環立即變化，只有透明度淡入淡出。關閉動效時，一切立即變化。720 ms 的停頓在任何情況下都保留，讓使用者能看到自己完成了什麼。

## 響應式

控制項填滿給定的寬度，長標題和說明會換行。寬度不超過 380 px 時，Hide 按鈕只顯示箭頭並保留朗讀名稱，標題列因此保持一行。每個勾選框有 40 px 的目標。懸停效果不作用於觸控。

## 相關元件

| 元件 | 適用場景 |
|---|---|
| `Stepper` | 步驟順序嚴格，有一個目前位置 |
| `Progress` | 執行單一任務且進度已知 |
| `Accordion` | 各部分是要閱讀的內容，而不是要完成的工作 |
| `Checkbox` | 只有一個獨立的選擇 |
