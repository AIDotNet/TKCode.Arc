---
title: 智慧代理建構器
summary: 在即時測試對話旁設定一個 AI 智慧代理：名稱和說明、附 token 計數的指令、模型、附開關的工具、在原處建立索引的知識來源、附「捨棄」的儲存列，以及附狀態標籤的「發布」。
source-hash: 5cabb78a31b0
---

<Demo name="Preview" />

## 何時使用

`AgentBuilder` 是人們建立或編輯 AI 智慧代理、並在別人使用之前先試一試的頁面。左邊的卡片依照人們做決定的順序放著定義智慧代理的一切：它是誰（名稱、說明），它被告知什麼（指令），由什麼來思考（模型），它能做什麼（工具），以及它知道什麼（知識來源）。右邊的卡片是與這個智慧代理的對話。修改在儲存之前一直是草稿；「發布」把已儲存的版本交給使用這個智慧代理的人，標籤會說明他們用的是否是最新版本。測試對話會記住它開始時的設定，所以一則回覆不會混用兩個版本：草稿改變後，會出現一則提示，讓你用新設定重新開始對話。

- **AI 產品、內部工具平台或客服台中的智慧代理或助理編輯器**。
- **調整提示詞和工具**：改一條指令或切換一個工具，重新開始對話，再問一次同樣的問題。
- **依靠知識的智慧代理**：加入連結或上傳檔案，看著每一項建好索引，之後智慧代理才能搜尋它。

## 何時不用

- **只需要編輯提示詞並在測試案例上執行**：使用 `PromptStudio`，它可以比較版本和輸出。
- **只需要選擇模型**：使用 `ModelPicker`。
- **給最終使用者的對話**：使用 `AiChat`。這裡的測試對話是給建置智慧代理的人用的，不保留歷史。
- **含分支和步驟的工作流程**：一個附工具的智慧代理和多個智慧代理組成的流程是兩回事；請使用圖形編輯器。

## 用法

這個區塊衍生自 Avalonia 的 `TemplatedControl`。沒有資料時，它會顯示一個虛構的客服智慧代理 Harbor（三個模型、五個工具、三個來源，其中一個索引失敗），並模擬儲存、發布、建立索引和測試回覆，因此在接上任何東西之前就可以試用。

```xml
<arc:AgentBuilder AgentName="{Binding Name}"
                  AgentDescription="{Binding Description}"
                  Instructions="{Binding Instructions}"
                  SelectedModelId="{Binding ModelId}" />
```

在程式碼中給它你自己的目錄（`Models`、`Providers`、`Tools`、`Sources`、`Suggestions`）；只要在它顯示之前設定了其中任何一項、名稱或指令，就不會出現範例。設定 `ShowsSampleData="False"` 可以得到一個從空白開始的建構器。

<Demo name="NewAgent" />

### 結構

- **頁首**：一個顯示名稱第一個字母的頭像（顏色隨名稱而定）、名稱、狀態標籤（Draft、Published、Unpublished changes）、說明和 **Publish**。
- **身分**：名稱（儲存時必須填寫）和一行說明。
- **指令**：一個多行輸入框，附字元數和大略的 token 數（大約四個字元一個 token）。超過 `InstructionsMaxLength`（8,000）後，輸入框會顯示錯誤，草稿無法儲存。
- **模型**：`ModelPicker` 的模型膠囊；測試對話的模型按鈕修改的是同一個設定。
- **工具**：每個 `AgentBuilderTool` 一列，附一個開關；按在這一列的任何地方都會切換它。計數說明有幾個已開啟。
- **知識**：每個 `AgentBuilderSource` 一列，顯示類型、大小和狀態：建立索引時顯示一個圓環，就緒時顯示勾號和段落數，失敗時顯示原因和 **Retry**，另有 **Remove**。清單下方是連結輸入框，以及 **Add link** 和 **Upload files**。
- **儲存列**：「All changes saved」或「Unsaved changes」，附 **Discard** 和 **Save**；儲存失敗時，訊息顯示在這裡。
- **測試對話**：一個 `AiChat`，回覆來自 `Responder`；草稿改變後會出現一則提示和 **Restart**。

### 草稿、已儲存和已發布

畫面上的設定就是草稿（`Config`）。`SavedConfig` 是已儲存的內容；建構器第一次顯示時如果它為 null，畫面上的設定就算作已儲存。`IsDirty` 比較這兩者，**Discard** 會還原已儲存的設定。`PublishedConfig` 是人們正在使用的內容：null 表示 Draft，與已儲存的設定相同表示 Published，其他情況都是 Unpublished changes。需要時 **Publish** 會先儲存。這三個快照都是 `AgentBuilderConfig` 記錄；當名稱、說明、指令、模型、已開啟的工具和來源 id 都相同時，兩個快照相等。

```csharp
builder.SaveHandler = async (config, cancellation) => await api.SaveAgentAsync(agentId, config, cancellation);
builder.PublishHandler = async (config, cancellation) => await api.PublishAgentAsync(agentId, cancellation);
builder.Load(await api.GetAgentAsync(agentId));   // 作為已儲存的內容顯示
```

擲出例外的處理常式會保留草稿，並顯示「Not saved: …」和例外訊息。`Ctrl+S`（`Cmd+S`）用於儲存。`SaveAsync`、`PublishAsync` 和 `Discard` 在程式碼中做同樣的事；`ConfigChanged`、`Saved` 和 `Published` 回報每一步。

<Demo name="Wired" />

### 知識來源

**Add link**（或在輸入框中按 Enter）只接受完整的 `http` 或 `https` 位址，其他內容或已是來源的頁面都會被拒絕，並在輸入框下方顯示訊息。**Upload files** 呼叫 `PickFilesHandler`，未設定時使用視窗的檔案選擇器（兩者都沒有時按鈕會隱藏）。新的來源和重試的來源透過 `IndexHandler` 建立索引：

```csharp
builder.IndexHandler = async (source, progress, cancellation) =>
{
    var job = await api.StartIndexAsync(source.Url, cancellation);
    await foreach (var step in api.WatchAsync(job, cancellation))
    {
        progress.Report(step.Fraction);
    }
    return job.PassageCount;
};
```

擲出例外會把來源標示為失敗並顯示訊息；Remove 會取消這項工作。沒有處理常式時會模擬建立索引（大約 2.4 秒）。`AddSources` 加入你自己的程式碼收到的來源，`RemoveSource` 和 `RetrySource` 作用於單一來源，`SourceIndexed` 回報每個完成或失敗的來源。

### 測試對話

對話使用它第一則訊息時的設定來回答（`TestConfig`）。修改之後，`IsTestStale` 變為 true，提示會說明這一點；**Restart**（或對話自己的新對話按鈕，或 `RestartTest()`）會清空對話，下一則訊息將使用草稿。實作 `IAgentBuilderResponder` 即可接上你的智慧代理執行環境：

```csharp
public sealed class RuntimeResponder(AgentRuntime runtime) : IAgentBuilderResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AgentBuilderTestRequest request,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        await foreach (var delta in runtime.StreamAsync(request.Config, request.Chat.Messages, cancellation))
        {
            yield return new AiChatTextDelta(delta);
        }
    }
}
```

請求中帶有設定、模型、已開啟的工具和對話內容。沒有設定回覆者時，由 `AgentBuilderSimulatedResponder` 回答：它會搜尋已就緒的來源，在問題需要時執行一個已開啟的工具（「Where is order 4417?」會執行 Order lookup），並總結智慧代理的設定，所以每個開關都有看得見的效果。

### 窄容器

版面跟隨建構器自身的寬度：低於 760 px 時，測試對話移到設定下方；低於 520 px 時，各部分的內距會收緊。側邊面板或手機上用的是同一個建構器。

<Demo name="Narrow" />

### 文案

所有文字都來自 `Labels`（`AgentBuilderLabels`），包括測試對話的（`Chat`）和模型選單的（`Model`）。帶數值的文字是函式，因此可以調整語序。`CultureName` 決定計數和大小的格式。

### 內容指南

- **名稱**：簡短而具體，因為人們會在清單中看到它（「Harbor」、「Release notes writer」）。
- **說明**：一行文字說明智慧代理能幫什麼忙，結尾不加句號。
- **指令**：規則清單比長段落更好讀；寫明智慧代理不能做什麼。
- **工具說明**：一行文字說明智慧代理能用這個工具做什麼，而不是它如何運作。

## API 參考

<!-- api: AgentBuilder -->

<!-- api: AgentBuilderTool -->

<!-- api: AgentBuilderSource -->

### AgentBuilderConfig

| 屬性 | 說明 |
|---|---|
| `Name`、`Description`、`Instructions` | 文字設定 |
| `ModelId` | 所選模型的 id，或 null |
| `ToolIds` | 已開啟的工具的 id，依 `Tools` 的順序 |
| `Sources` | 知識來源，依順序 |

### 事件

| 事件 | 何時觸發 | 參數 |
|---|---|---|
| `ConfigChanged` | 草稿改變 | `Field`、`Config`、`IsDirty` |
| `Saved` | 儲存成功 | `Config` |
| `Published` | 發布成功 | `Config` |
| `SourceIndexed` | 一個來源完成索引或失敗 | `Item` |

## 無障礙

- 設定卡片是一個名為「Agent settings」的表單，每個部分有三級標題；智慧代理的名稱是二級標題。
- 每個輸入框都有標籤或無障礙名稱；指令輸入框把提示作為說明文字。錯誤（名稱為空、指令太長、連結無效）以輸入框訊息的形式出現，用文字表達，而不只靠顏色。
- 每個工具的開關帶有工具的名稱和說明；它是這一列唯一的 Tab 停留點。
- 來源列會讀出名稱和狀態（「returns-and-repairs.pdf, File · 412 KB · Ready · 86 passages」）；Remove 和 Retry 的名稱帶有來源（「Remove help.tidewater.example/fitting」）。
- 一個禮貌的即時區域會播報儲存、捨棄、發布、切換的工具、加入、移除、就緒或失敗的來源、過期的測試對話和重新開始。儲存列的狀態也是一個禮貌的即時區域。
- 焦點會前往人們接下來要看的地方：移除來源後，到下一列的 Remove（或連結輸入框）；重試後，到這一列的 Remove；捨棄後，到名稱輸入框；重新開始後，到對話的訊息框。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | Publish、各個輸入框、模型膠囊、每個工具的開關、每個來源的 Retry 和 Remove、連結輸入框、Add link、Upload、Discard、Save，然後是測試對話 |
| `Space` | 切換取得焦點的工具開關 |
| 在連結輸入框中按 `Enter` | 加入連結 |
| `Ctrl+S` / `Cmd+S` | 儲存草稿 |
| 在模型膠囊上按 `Enter`、`Space` 和 `ArrowDown` | 開啟模型選單 |

## 動效

- 儲存列的狀態在已儲存、未儲存、儲存中和失敗之間變化時交叉淡入淡出（100 ms 淡出，160 ms 淡入，上升 2 px）。
- 過期提示淡入，它的高度以彈簧方式在對話上方展開；重新開始後它會再次收起。
- 來源的圓環跟隨索引進度，每一格追上一點；就緒時勾號取代它。
- 游標停留時各列在 160 ms 內著色；工具開啟時，它的圖示方塊換成強調色；名稱改變頭像顏色時，顏色會交叉淡入淡出。
- 測試對話像 `AiChat` 一樣串流顯示，按鈕、開關和輸入框保留各自的動效。
- 減少動效時所有狀態都會保留：提示只淡入而不展開，圓環直接跳到進度，顏色立即改變。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 儲存列狀態 | 交叉淡入淡出並上升 2 px | 120 ms 交叉淡入淡出 | 立即 |
| 過期提示 | 淡入，高度以彈簧展開 | 淡入 | 立即 |
| 索引圓環 | 追趕進度 | 直接跳到 | 直接跳到 |
| 列和圖示的顏色 | 160 ms | 立即 | 立即 |

## 響應式

- `AgentBuilder` 依自身寬度變化，而不是依視窗寬度。從 760 px（`StackWidth`）起，對話位於設定旁邊，約佔寬度的 46%，至少 340 px 寬、至少 560 px 高（隨設定卡片一起變高）。低於 760 px（`:stacked`）時，對話移到設定下方，高度為 540 px。
- 低於 520 px（`:compact`）時，各部分的內距從 24 收緊到 16。
- 名稱和說明過長時以省略號結尾；工具說明、來源狀態和過期提示會換行。
- 讓建構器橫向延展（`HorizontalAlignment="Stretch"`），並把它放進可以捲動的頁面：設定卡片的高度與內容相同。

## 相關元件

- `ModelPicker`：只選擇模型。
- `PromptStudio`：在儲存的測試案例上編輯提示詞。
- `AiChat`：給你的使用者看的對話。
- `AgentMemory`：助理記住了關於一個人的什麼。

| 元件 | 何時選擇 |
|---|---|
| `PromptStudio` | 你要在測試案例上比較提示詞版本 |
| `ModelPicker` | 只需要選擇模型 |
| `AiChat` | 人們與完成的智慧代理對話 |
| `SettingsPage` | 設定與智慧代理無關 |
