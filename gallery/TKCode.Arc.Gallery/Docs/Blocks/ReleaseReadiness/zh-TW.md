---
title: Release readiness
summary: 發布閘門：一組簡短的檢查項目會解鎖唯一的一個動作，並附上進度條、負責人、通知開關，以及就地顯示的結果。
source-hash: 93dc8b911186
---

<Demo name="Preview" />

## 何時使用

`ReleaseReadiness` 是某件事送出前的最後一關：一小串必須成立的條件，加上一個在條件全部滿足前都保持鎖定的按鈕。隨著檢查項目被勾選，進度條逐漸填滿，文字也會倒數（「2 to go」、「All clear」）；當所有必要檢查都完成時，進度條會轉為綠色，按鈕隨之解鎖。按下按鈕會執行你的程式碼，過程中顯示載入動畫，完成後會在頁尾正下方說明發生了什麼事。

- **發布或推出。** 完成簽核、測試、發行說明與支援簡報後，按下「Start rollout」。通知開關決定是否通知某個頻道，成功訊息也會說明這一點。
- **團隊之間的交接。** 操作手冊、儀表板、警示與尚未結案的事件都必須完成；一兩項「最好有」的檢查可以標記為選填，讓它們仍會顯示，但不會擋住交接。
- **任何帶有檢查清單的不可逆動作。** 遷移、調價、行銷活動發送等：任何需要由人先確認幾項事實、並在同一處看到結果的情況。

## 何時不用

- **很長或巢狀的檢查清單。** 此區塊設計用於一個清單中三到八項檢查。若項目多達數十項，或項目本身有各自的負責人與日期，請改用工作清單、`Timeline` 或專案看板。
- **系統能自行驗證的檢查。** 若建置伺服器已經知道測試是否通過，請顯示該狀態，而不要請人勾選；這裡的勾選只代表某個人的說法。
- **表單。** 檢查項目是帶有標題與說明的布林值。當人員需要輸入數值時，請使用 `Checkbox`、`Input` 與送出按鈕。
- **多個動作。** 此閘門只會解鎖一個動作。其他動作請放在頁面的其他位置。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。每個檢查項目是一個 `ReleaseCheck`，它是 `AvaloniaObject`，具有標題、選填的說明，以及雙向繫結的 `IsDone`。負責人是 `ProjectPerson`，會以產生的頭像呈現：由姓名的首字母組成，底色是依該人的 key 選出的淡色；除非你提供照片，否則不會使用照片。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ReleaseReadiness Title="Version 5.3"
                          Subtitle="Lumen mobile · goes live Tuesday"
                          ChannelName="#launches">
        <arc:ReleaseReadiness.Owners>
            <arc:ProjectPerson Name="Rina Kato" />
            <arc:ProjectPerson Name="Samir Aziz" />
        </arc:ReleaseReadiness.Owners>
        <arc:ReleaseCheck Title="Design signed off" Description="Final screens approved by product" IsDone="True" />
        <arc:ReleaseCheck Title="Smoke tests green" Description="Build 5.3.0-beta.2 passed on preview" />
        <arc:ReleaseCheck Title="Release notes written" Description="Customer summary drafted and reviewed" />
        <arc:ReleaseCheck Title="Support briefed" Description="Help desk knows what is changing" />
    </arc:ReleaseReadiness>
</UserControl>
```

### 組成

- **標題列**：`Title`、`Subtitle`，以及右側的負責人頭像（沒有負責人時隱藏）。
- **進度**：左側是尚餘的項目，右側是數量（「1 of 4」），以等寬數字呈現，下方是 4 px 的進度條。
- **檢查項目**：每個項目一列。整個標籤都屬於核取方塊，因此按下標題或說明即可勾選；將指標移到該列的任何位置都會使其著色。
- **頁尾**：起點是通知開關，終點是按鈕。若兩者放不進同一行，按鈕會移到第二行，但仍靠在結尾處。
- **狀態列**：在頁尾下方展開，顯示成功或錯誤文字。

### 檢查項目與閘門

`TotalCount` 計算必要檢查項目的數量，`DoneCount` 計算其中已完成的數量；當至少有一項檢查，且所有必要項目都已完成時，`IsReady` 為 true。`IsRequired="False"` 的檢查會顯示並可勾選，但不計入數量、進度條或閘門。若要使用你自己擁有的集合，請使用 `ChecksSource`（它會監看 `ObservableCollection`），或直接填入 `Checks`。勾選核取方塊會把值寫回 `IsDone`，因此對它做雙向繫結即可讓你的模型保持同步。

`CheckChanged` 會回報每次勾選的舊值與新值，以及之後的數量；`ReadyChanged` 則在閘門重新開啟或關閉時觸發。

### 開始發布

將 `ReleaseAction` 設為按鈕要啟動的工作。它會接收 `ReleaseRequest`（是否通知、所有檢查項目、被略過的選填項目，以及嘗試次數）與一個取消權杖。工作執行期間 `State` 為 `Releasing`：按鈕顯示載入動畫並保持鍵盤焦點，檢查項目與開關則被鎖住。工作完成後，按鈕會顯示帶有勾號的 `ReleasedText`，狀態列會說明發布已在進行中（若開關開啟，也會說明已通知頻道），並出現重設按鈕。

```csharp
gate.ReleaseAction = async (request, cancellation) =>
{
    await deployments.StartAsync("lumen-mobile", "5.3.0", notify: request.Notify, cancellation);
};
gate.Released += (_, e) => log.Info($"Rollout started on attempt {e.Request.Attempt}");
```

`ReleaseCommand` 會先以該請求作為參數執行；若它無法執行，發布便不會開始。`ReleaseStarting` 會在這兩者之前觸發，並可取消發布。`ReleaseAsync()` 執行的是按鈕所做的事；`CancelRelease()` 會取消權杖並立即讓閘門退回就緒狀態（工作稍後的結果會被忽略）；`Reset()` 會把每個檢查項目放回原位：若設定了 `IsInitiallyDone`，就回到該值，否則回到區塊首次顯示時該項目的值。通知開關在重設後會保留其值。

### 失敗與重試

來自動作（或命令）的例外會將 `State` 設為 `Failed`。錯誤會以危險色顯示在狀態列，檢查項目與開關重新解鎖，按鈕則保持焦點，可以再次嘗試。錯誤列顯示例外的訊息；若你設定了 `ErrorFormatter`，則顯示它的文字；若訊息為空，則顯示 `FailureText`。失敗後變更任何一項檢查都會清除錯誤。取消（`CancelRelease`，或工作執行期間區塊離開視窗）不算失敗：閘門只會回到就緒狀態。

<Demo name="Failing" />

### 有框或無框

`IsFramed`（預設值）會繪製一張最寬 560 的卡片，附 1 px 邊框、16 的圓角、表面色，並在檢查項目與頁尾上方加上分隔線。設為 false 則會把區塊平鋪在頁面上，寬度與容器相同，適用於整頁或側邊面板；各列仍保留淺色分隔線。

<Demo name="Flat" />

### 窄版容器

區塊讀取的是自己的寬度，而不是視窗的寬度。寬度為 388 或以下時，每一側的內距會從 20 縮為 16。標題列保持單行，副標題以省略號縮短；當按鈕與開關放不進同一行時，頁尾會把按鈕移到開關下方。

### 其他文字與選填檢查

每段文字都是一個屬性，並有英文預設值：`ReleaseText`、`ReleasedText`、`ResetText`；進度文字為 `RemainingFormat`（「{0} to go」）、`RemainingOneText`、`ReadyText`、`ReleasingText` 與 `CountFormat`（「{0} of {1}」）；通知開關為 `NotifyText`，或搭配 `ChannelName` 使用的 `NotifyFormat`（「Notify {0}」）；成功訊息為 `SuccessFormat` 與 `SuccessNotifiedFormat`（搭配 `ReleaseName`，或標題，以及頻道）；螢幕閱讀器的名稱為 `ChecksLabel`、`ProgressLabel`、`OwnersLabel` 或 `OwnersLabelPrefix`（搭配 `AndWord`）。`IsNotifyVisible` 會隱藏開關，`IsResetVisible` 會隱藏重設按鈕。

<Demo name="Handoff" />

## API 參考

<!-- api: ReleaseReadiness -->

<!-- api: ReleaseCheck -->

<!-- api: ReleaseRequest -->

<!-- api: ReleaseReadinessState -->

## 無障礙

- **Tab** 依序移動經過各檢查項目、通知開關與按鈕。在閘門就緒之前，按鈕不是 Tab 停駐點；發布之後，只有重設按鈕是停駐點。
- **Space** 會勾選或清除焦點所在的檢查項目。**Enter** 則不會，因此檢查項目不會被誤勾。
- **Space** 或 **Enter** 會切換通知開關，並按下按鈕。
- 發布後，焦點移到重設按鈕（若重設按鈕隱藏，則移到狀態列）。重設後，焦點移到第一個未完成的檢查項目。失敗後，焦點停留在按鈕上以便重試。焦點只有在原本位於區塊內時才會移動，因此從程式碼啟動的發布不會奪走鍵盤焦點。
- 區塊以其標題命名，標題為二級標題。
- 負責人是一個群組，名稱為「Owners: Rina Kato and Samir Aziz」（由 `OwnersLabel` 或 `OwnersLabelPrefix` 提供）；頭像不會被逐一朗讀。
- 進度條是唯讀的進度條，名稱由 `ProgressLabel` 提供，其值為已完成的必要檢查數，最大值為全部必要檢查數。檢查項目清單的名稱由 `ChecksLabel` 提供。
- 每個核取方塊以其檢查項目的標題命名，並以說明作為描述；必要檢查會標示為必要。
- 尚餘項目（「2 to go」、「All clear」、「Rolling out」）在變化時會以禮貌模式朗讀；旁邊的數量則不朗讀。成功訊息以禮貌模式朗讀，錯誤則以強制模式朗讀，重試時重複出現的錯誤會再次朗讀。
- 動作執行期間，按鈕保留其名稱，並以發布中文字作為其狀態回報。

## 動態效果

- **勾選檢查項目**：進度條的填色以臨界阻尼彈簧滑動（無過衝，約 580 ms），當所有必要項目都完成時，會在 240 ms 內轉為綠色。核取方塊以自己的彈簧描繪勾號；標題在 160 ms 內淡化為次要色。文字原地變化，不做動畫。
- **懸停**：列會在 160 ms 內著色。
- **發布中**：按鈕顯示載入動畫，接著其文字以短暫的模糊進行交叉淡化，同時寬度以彈簧調整為新文字的寬度。狀態列以平滑彈簧展開，並搭配另外 160 ms 的淡入；重設時以相同方式收合。
- 減少動態效果時，進度條、顏色與狀態列會立即就位，只保留按鈕的短暫交叉淡化。關閉動態效果時，所有變化都會立即套用。

## 相關

- `Checkbox`、`Switch` 與 `Button`，是組成此區塊的控制項。
- `Progress`，用於附標籤的獨立進度條。
- `ActionButton`，用於單一動作、具有處理中與成功狀態、但沒有檢查項目的情況。
- `Project board` 與 `Kanban board`，其他專案區塊。
