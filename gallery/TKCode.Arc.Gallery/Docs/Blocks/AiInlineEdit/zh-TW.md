---
title: AI inline edit
summary: 就地用 AI 修改一段文字：選取文字，說出要改什麼，在段落中以劃掉和新增的詞查看改寫，並逐處接受或拒絕。
source-hash: c5eceb9f0df4
---

<Demo name="Preview" />

## 何時使用

`AiInlineEdit` 讓使用者在不離開文字的情況下藉助 AI 改進它。選取一段文字，按 Edit with AI（或 Ctrl+K），說出想要的效果，或選一個快捷指令，例如 Make it shorter。改寫不會立刻取代原文，而是在段落中以逐詞的改動顯示：刪掉的詞被劃掉，新增的詞帶底色，每處改動都有自己的接受和拒絕按鈕。每處改動都決定後，或按下 Accept all 或 Reject all 後，文字更新，改過的段落保持選取，方便下一次請求。

- **文件、郵件、筆記和說明文字**：使用者想在措辭上得到幫助，但要掌控每一個詞。
- **審閱流程**：AI 的建議在落地前必須經過檢查。

## 何時不用

- **從零開始寫。** 請使用 `AI composer` 或聊天；沒有可以對比的原文。
- **涉及整份文件或多個檔案的修改。** 請使用 `AI side panel`，它按段落列出改動。
- **單純的拼字檢查。** 請使用系統內建的拼字檢查。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`。`Text` 是雙向的，會收到修改後的文字。改寫來自 `RewriteHandler`；沒有設定時，一個簡單的內建替身會改寫範例文字：

```xml
<arc:AiInlineEdit Text="{Binding Draft}" />
```

```csharp
editor.RewriteHandler = async (request, cancellation) =>
{
    // request.Selection is the passage, request.Instruction what to do, request.Text the whole text for context.
    return await model.RewriteAsync(request.Selection, request.Instruction, request.Text, cancellation);
};
editor.EditApplied += (_, e) => history.Push(e.OldText);
```

### 階段

使用者閱讀和選取時 `Phase` 為 `Idle`，輸入框開啟時為 `Instructing`，產生改寫時為 `Thinking`，決定改動時為 `Reviewing`。選取範圍兩端的空格不計入選取。處理程式擲出例外時，訊息顯示在底欄中，輸入框保持開啟，使用者可以再試一次。如果改寫與原文相同，底欄會說明沒有建議的改動。

### 審閱改動

改寫與原段落逐詞比較。每個改動區域是一處改動：舊詞以危險色劃掉，新詞帶綠色底色，後面跟著一個勾和一個叉。已接受的改動在審閱結束前以底線顯示新詞；已拒絕的改動恢復為舊詞。`AcceptChange`、`RejectChange`、`AcceptAll`、`RejectAll` 和 `Cancel` 可在程式碼中完成同樣的操作。

<Demo name="Handler" />

## API 參考

<!-- api: AiInlineEdit -->

<!-- api: AiInlineEditRequest -->

<!-- api: AiInlineEditLabels -->

## 無障礙

- 編輯器是名為 "Text with AI editing" 的區域；文字可以用鍵盤選取。
- **Ctrl+K**（或 **Cmd+K**）為選取範圍開啟輸入框；**Enter** 送出；**Escape** 取消，審閱時則拒絕剩下的改動。
- 審閱時焦點移到第一處未決定的改動；**Tab** 依次經過每處改動的接受和拒絕按鈕，按鈕以改動命名（"Accept: replace "utilize" with "use""）。
- 一個禮貌式即時區域會在改動就緒和套用後播報。
- 刪掉和新增的詞透過劃線和底色區分，不只靠顏色。

## 動效

- 改動按鈕在指標停留時於 160ms 內變色。
- 底欄的面板立即切換；產生改寫時送出按鈕顯示載入圈。
- 減少動效和關閉動效時顏色變化立即生效。

## 響應式

- 編輯器最寬 680，小於這個寬度時跟隨容器。
- 段落和審閱內容自動換行；快捷指令會換到更多行。

## 相關元件

- `AI side panel`，用於整份文件中的建議改動。
- `AI composer`，用於撰寫新的提示詞。
- `Prompt studio`，逐詞比較提示詞的不同版本。
