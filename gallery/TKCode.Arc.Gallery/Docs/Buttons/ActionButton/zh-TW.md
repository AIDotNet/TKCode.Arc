---
title: 操作按鈕
summary: 用於高頻工具列操作的精簡按鈕。
source-hash: b3c11d0f2177
---

<Demo name="Default" />

## 何時使用

- 單次非同步送出，例如儲存、發布或寄送邀請，且結果應顯示在按鈕上。
- 文字會切換為等待中與成功兩種狀態、並自動回到閒置狀態的操作。
- 表單或工具列末端的行動呼籲，需要清楚的結果回饋，但不需要跳出提示。

## 何時不用

- 一般的表單送出，只需載入指示、不需結果文字：用 `Button` 並設定 `IsLoading`。
- 需要使用者刻意按住才能執行的破壞性操作：用 Gestures 中的 `HoldToConfirm`。
- 同一操作的多個備選項：用 `SplitButton`。
- 列數很多的密集表格：結果應保留在列內，不要為每一列都放置一個按鈕。

## 用法

`ActionButton` 衍生自 `Button`。其工作透過 `AsyncAction` 執行，該屬性在程式碼中設定。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActionButton Label="儲存" PendingLabel="儲存中" SuccessLabel="已儲存" />
</UserControl>
```

```csharp
var save = new ActionButton
{
    Label = "儲存",
    PendingLabel = "儲存中",
    SuccessLabel = "已儲存",
    AsyncAction = () => documentService.SaveAsync()
};
```

### 標籤與重設

`Label` 是閒置狀態的文字，同時也是無障礙名稱。`AsyncAction` 執行期間顯示 `PendingLabel`，完成後顯示 `SuccessLabel`。`ResetAfter` 設定成功狀態保留的時間，預設為 2.4 秒。設為零時，成功狀態會一直保留，直到呼叫 `Reset()`。

<Demo name="Labels" />

<Demo name="KeepResult" />

```xml
<arc:ActionButton Label="發布貼文"
                  PendingLabel="發布中"
                  SuccessLabel="已發布"
                  ResetAfter="00:00:03" />
```

### 失敗

`AsyncAction` 擲出例外時，按鈕回到閒置狀態，並引發 `ActionFailed`。事件引數攜帶 `Exception`。

<Demo name="Failure" />

### 命令與點按

`Command` 與 `Click` 會在 `AsyncAction` 之前執行。若沒有非同步操作，它們視為立即成功。

### 停用

將 `IsEnabled` 設為 `False` 即可使按鈕變暗。`AsyncAction` 等待期間，按鈕保持鍵盤焦點，並忽略重複啟動。

<Demo name="Disabled" />

## API 參考

<!-- api: ActionButton -->

## 無障礙

- 無障礙名稱始終為 `Label`，因此動畫中的文字不會被逐字朗讀。
- 等待期間，按鈕向輔助技術回報自身無法使用，但保持鍵盤焦點。
- 一個隱藏的禮貌狀態區域會播報等待與成功文字。
- Enter 與 Space 均可執行操作。等待期間忽略重複啟動。

## 動態效果

- 變化的文字從柔和的模糊中升起，保留的文字滑動到新位置。寬度隨彈簧動態過渡。
- 箭頭沿操作方向離開，並從左側回到原位。操作執行時載入指示持續旋轉，成功時勾號逐筆繪製。
- 減少動態效果時，移除按下縮放、寬度彈簧與繪製的勾號。文字切換改為瞬間交叉淡化。關閉動態效果時，文字立即替換。

## 相關元件

- `Button`：一般操作；若只需載入指示而無結果文字，設定 `IsLoading`。
- `CopyButton`：複製一個值，並以同類的行內確認方式回饋。
- `ConfirmMorph`：破壞性操作，在原位要求確認。
- `HoldToConfirm`：需要使用者刻意按住才能執行的破壞性操作。
