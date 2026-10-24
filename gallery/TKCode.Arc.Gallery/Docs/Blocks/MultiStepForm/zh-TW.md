---
title: Multi-step form
summary: 一次只問一個步驟的短表單，放在卡片中，附進度清單、「Back」與「Continue」按鈕，每一步前都會進行驗證，最後呈現成功狀態。
source-hash: dc7c21c7ca0d
---

<Demo name="Preview" />

## 何時使用

`MultiStepForm` 將一份短表單拆成幾個重點明確的步驟，並在同一張卡片中引導使用者逐步完成。標頭會列出每個步驟，並以圓形標記標示，同時顯示目前位置（「Step 1 of 2」）；中間放置一個步驟的欄位；底部是「Back」與「Continue」，在最後一步時「Continue」會變成完成動作。送出最後一步後，欄位會被一段簡短的成功訊息取代，按鈕也會收起。

- **入職與註冊。** 命名工作區、選擇負責人、挑選方案：這些是兩到四個小決定，若放在一張很長的表單中會顯得沉重。
- **收件與申請。** 客服需求、預約或申請，每一步只問一類問題，最後一步送出所有內容。
- **依固定順序的設定。** 個人資料、地址、配送：使用者可以回到前面的步驟修改答案，而不會遺失其他答案。

## 何時不用

- **只需要進度指示器。** 當你只需要步驟清單，而不需要卡片、按鈕與欄位時，請使用 `Stepper`。
- **冗長的流程或完整頁面。** 五個或更多步驟、步驟本身有導覽，或流程跨越多個頁面（例如結帳），應該有自己的頁面版面。步驟放不下時進度清單會捲動，但清單很長是一個訊號，表示流程應該縮短。
- **單一簡短表單。** 三個欄位不需要分成步驟；把它們放在一張表單中，搭配一個送出按鈕即可。
- **在其他地方完成的任務。** 使用者要在別處完成的事項（連接行事曆、邀請團隊成員）屬於檢查清單，不是表單。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。每個步驟都是一個 `MultiStepFormStep`，它是一個 `AvaloniaObject`，具有 `Title`（欄位上方的標題，也是進度清單中的標籤）、選用的 `Description` 說明行，以及步驟的欄位作為 `Content`。將步驟寫成子元素；欄位則是一般控制項。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MultiStepForm CompleteLabel="Create workspace"
                       SuccessTitle="You're in"
                       SuccessDescription="Your workspace is ready.">
        <arc:MultiStepFormStep Id="workspace"
                               Title="Workspace basics"
                               Description="Give it a short, memorable name.">
            <arc:Input Label="Workspace name" Text="Northwind studio" />
        </arc:MultiStepFormStep>
        <arc:MultiStepFormStep Id="lead" Title="Pick a lead">
            <arc:Combobox Label="Lead" SelectedIndex="0">
                <arc:SelectItem Content="Priya Raman" />
                <arc:SelectItem Content="Tomás Ortega" />
                <arc:SelectItem Content="June Whitfield" />
            </arc:Combobox>
        </arc:MultiStepFormStep>
    </arc:MultiStepForm>
</UserControl>
```

`Completed` 會在最後一步送出並顯示成功區塊後引發（`CompleteCommand` 會緊接著以相同參數執行）。參數中包含各步驟，其內容仍保留使用者輸入的值。

```csharp
form.Completed += (_, e) => workspaces.Create(nameBox.Text, (string?)leadPicker.SelectedValue);
```

### 組成部分

- **進度標頭**：每個步驟一個項目，包含 20 px 的標記與步驟標題。尚未到達的步驟以淡色顯示，目前步驟使用強調色，已完成的步驟顯示綠色勾號。右側的計數會依 `CountFormat`（「Step {0} of {1}」）顯示。
- **步驟**：步驟標題為標題（heading），接著是選用的說明，然後是欄位。卡片寬 540（若容器較窄則與容器同寬），步驟區域至少 224 高，因此簡短的步驟不會讓卡片跳動。
- **按鈕**：「Back」（第一步時停用）與送出按鈕。送出按鈕會顯示 `NextLabel`（「Continue」），最後一步則顯示 `CompleteLabel`（「Finish」）。
- **成功區塊**：一個勾號、`SuccessTitle` 與 `SuccessDescription`，置中於原本放置欄位的位置。

### 值會被保留

每個步驟在其他步驟顯示時仍會保持欄位存活：非目前步驟會被隱藏但仍留在表單中，因此其中輸入的文字、選取項目、繫結與資料內容，在「Back」與「Continue」之間切換後都會保留。你可以在 `Completed` 引發時讀取值、將它們繫結到你的檢視模型，或兩者並用。`Restart()` 會再次顯示第一步；它不會清除欄位，因此若新的開始應該是空白的，請重設你的模型（或控制項）。

### 前進前檢查步驟

步驟由「Continue」、完成按鈕，或步驟中單行文字欄位內按下 Enter 送出。表單前進之前，會依序檢查：

1. **資料驗證錯誤。** 步驟中顯示驗證錯誤的欄位（來自繫結、`INotifyDataErrorInfo` 或 `DataValidationErrors.SetError`）會停止表單，並取得焦點。
2. **`StepValidating`。** 在引數上設定 `Cancel` 以留在原步驟；適合不需等待的快速檢查。
3. **`MultiStepFormStep.ValidateAsync`。** 你自己的檢查，會被等待。回傳 false 以留在原步驟（通常在此之前會設定欄位的 `ErrorMessage`）。執行期間 `IsBusy` 為 true，送出按鈕會顯示載入動畫並保留焦點，「Back」會停用。「Back」、其他步驟變更、`Restart()`，或離開視窗，都會取消其權杖。擲出例外的檢查視為不通過。

<Demo name="Validation" />

```csharp
// isEmailTaken is your own check, for example a call to your API.
form.Steps[0].ValidateAsync = async cancellation =>
{
    var taken = await isEmailTaken(emailBox.Text, cancellation);
    emailBox.ErrorMessage = taken ? "That email already has an account." : null;
    return !taken;
};
```

### 送出最後一步

`CompleteAsync` 會在最後一步送出並通過檢查後執行。送出按鈕會顯示載入動畫，直到工作完成；接著出現成功區塊，並引發 `Completed`。若工作擲出例外，表單會停留在最後一步，`CompleteFailed` 會回報該例外，按鈕也可以再次嘗試。若沒有 `CompleteAsync`，表單會立即完成。

<Demo name="AsyncSubmit" />

```csharp
// sendRequest is your own call that stores or sends the answers.
form.CompleteAsync = cancellation => sendRequest(request, cancellation);
form.CompleteFailed += (_, e) => status.Text = e.Exception.Message;
```

### 自行控制步驟

`CurrentIndex`（以零為起點，雙向繫結）是目前畫面上的步驟。設定它可以從其他步驟開始或跳轉；表單會依前進或後退的方向做動畫，並引發 `StepChanged`，帶有舊的與新的索引。超出步驟範圍的值會在步驟已知後被限制在範圍內，因此 XAML 中的 `CurrentIndex="1"` 在步驟寫入之前也能運作。`NextAsync()` 執行與「Continue」相同的動作（包括檢查），並回傳表單是否已移動；`Back()` 執行與「Back」相同的動作。`IsComplete` 也是雙向的：設定為 true 可顯示成功區塊而不引發 `Completed`，設為 false 則再次顯示步驟。

<Demo name="Controlled" />

### 窄版容器

寬度在 540 以內時，卡片會跟隨其容器；在更寬的容器中，它保持 540 寬並置中。區塊讀取的是自己的寬度，而不是視窗的寬度：寬度為 388 或更小時，計數會移到進度清單上方，內距由 20 收緊為 16，步驟區域則增加到至少 240。無論寬度多少，當步驟無法放在同一行時，清單會左右捲動（沒有捲軸），並讓目前步驟保持在可見範圍內。

<Demo name="Narrow" />

### 其他文字

每段文字都是具有英文預設值的屬性：畫面上的 `NextLabel`、`CompleteLabel`、`BackLabel`、`SuccessTitle`、`SuccessDescription` 與 `CountFormat`；供螢幕閱讀器使用的 `FormName`、`ProgressNameFormat`、`AnnouncementFormat`、`CurrentStepText` 與 `CompletedStepText`。在格式字串中，`{0}` 是從 1 開始的步驟編號，`{1}` 是步驟總數；步驟變更時，計數中只有 `{0}` 的部分會滾動。

<Demo name="Labels" />

## API 參考

<!-- api: MultiStepForm -->

<!-- api: MultiStepFormStep -->

<!-- api: MultiStepFormStepChangedEventArgs -->

<!-- api: MultiStepFormStepValidatingEventArgs -->

<!-- api: MultiStepFormCompletedEventArgs -->

<!-- api: MultiStepFormCompleteFailedEventArgs -->

## 無障礙

- 每次步驟變更後，焦點會移到新步驟的標題；完成最後一步後，焦點會移到成功標題。兩者都只能由程式碼取得焦點（它們不是 Tab 停駐點，也不繪製焦點環），因此螢幕閱讀器會從正確的位置開始朗讀，下一次 Tab 則會移到第一個欄位。只有當焦點原本在表單內時焦點才會移動，因此從其他地方進行的步驟變更（例如 `CurrentIndex` 繫結）不會奪走焦點。
- 「Back」只有在焦點移動之後才會在第一步停用，因此焦點不會遺失。
- 卡片是一個表單地標，名稱由 `FormName` 提供。進度標頭是導覽地標，清單則是一個 list，兩者都以 `ProgressNameFormat`（「Progress, step 1 of 2」）命名；每個項目以其步驟標題命名，並以 `CurrentStepText` 或 `CompletedStepText` 作為狀態。畫面上的計數對螢幕閱讀器隱藏。
- 每個步驟是一個以其標題命名的群組。步驟變更會以 `AnnouncementFormat`（「Step 2 of 2」）進行禮貌模式朗讀；完成時會朗讀成功標題與說明行。
- 送出按鈕以其目前的標籤命名；在檢查或最終送出執行期間，它會保留焦點並忽略按下動作。
- 鍵盤焦點環為 2 px，位於按鈕外 2 px；指標按下時不繪製焦點環。

### 鍵盤

| 按鍵 | 動作 |
| --- | --- |
| Enter | 在步驟的單行文字欄位中：送出該步驟（繼續，或在最後一步時完成）。若欄位自己使用 Enter（開啟中的下拉清單且有反白選項，或多行文字區域），則由該欄位保留 Enter。 |
| Enter / Space | 在「Back」或送出按鈕上：按下該按鈕。 |
| Tab / Shift+Tab | 依序移過步驟的欄位，然後是「Back」（停用時略過）與送出按鈕。 |

## 動態效果

- **步驟變更**：新步驟在前進方向上滑入 20 px（前進時由右側、後退時由左側），採用約 0.4 秒的臨界阻尼彈簧，同時在 240 ms 內淡入；舊步驟向相反方向滑出 20 px，並在 120 ms 內淡出。兩者同時開始。在變化進行中途後退時，舊步驟會從目前位置折返，並保持其速度。
- **高度**：步驟區域以相同的彈簧調整到新步驟的高度，較高的步驟會變高，較矮的則縮小（且不會低於其最小高度）。只有在高度變化時，內容才會在上下方裁切。
- **進度**：已完成的步驟會把數字換成勾號；勾號從 0.6 開始，以短促的臨界阻尼彈簧放大，接著在 320 ms 內自行描繪出來；後退時，數字會從輕微的模糊中重新彈出。標記與標籤的顏色在 240 ms 內過渡。
- **計數與標籤**：步驟數字在前進時向上滾動 0.3em，後退時向下滾動，從柔和的模糊中在 240 ms 內升起，舊數字則在 120 ms 內離開。送出按鈕的標籤以相同方式交換（一律向上），同時按鈕寬度以彈簧調整到新標籤的寬度。
- **完成**：最後一步滑出，按鈕在 120 ms 內淡出，其列則依彈簧收合；成功區塊以 8 px 上升並淡入，其勾號從 80 ms 開始描繪，標題從 100 ms 開始從模糊中升起，說明行從 140 ms 開始。
- **按鈕**：懸停顏色在 160 ms 內過渡；按下時按鈕在 100 ms 內縮放至 0.98，放開時以彈簧彈回。
- 減少動態效果與關閉動態效果時，步驟、高度、標籤、標記與成功區塊會立即變更，且按下時不做縮放；焦點與朗讀則相同。

## 相關元件

- `Stepper`，單獨用於進度指示。
- `Input`、`Combobox`、`Textarea` 與 `RadioCards`，即範例中使用的欄位。
- 其他 Auth 區塊：Login、OTP input 與 Onboarding illustration。
