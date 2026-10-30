---
title: Agent plan
summary: "代理的計畫是一份可編輯的檢查清單：草稿階段可以重新排序、改名與新增步驟，核准後則依序追蹤執行過程。"
source-hash: 001769f62b45
---

<Demo name="Preview" />

## 何時使用

`AgentPlan` 是代理在「這是我會做的事」與「完成」之間顯示的卡片。草稿階段，人員會閱讀各個步驟並加以修正：就地改名標題、把步驟拖到另一個位置、移除或新增一個步驟，或請代理再提供一個步驟。核准計畫後，計畫即鎖定，同一張卡片會變成即時檢查清單，隨代理逐步前進，顯示每個步驟花費的時間、活動行的位置，以及步驟失敗時需要做的決定。

- **編碼或維運代理的計畫模式**：代理提出步驟，人員編輯並核准，卡片再追蹤工作進度。核准之前什麼都不會執行。
- **追蹤多步驟任務的側邊面板或聊天訊息**：一個目前步驟，已完成的步驟會沉降為安靜的文字，頁尾則以總時間與一行摘要收尾。
- **可能中途失敗的工作**：失敗的步驟會停止執行，並顯示原因與 Retry 和 Skip，而不是把失敗藏在記錄裡。
- **在頁面或示範中試用完整流程**：給卡片一個帶有你自己腳本的 `AgentPlanSimulation`，它就會播放這份計畫，因此不需要代理也能看到每一種狀態。

## 何時不用

- **代理所做的一切的逐字紀錄**：請使用 `ChatThread` 或記錄檢視。計畫是一份簡短的有序意圖清單，而不是工具呼叫與其輸出的紀錄。
- **逐一核准有風險的單一動作**：計畫只在一開始核准一次。當人員應逐案決定時，請讓每個動作各自出現一則提示。
- **跨多個代理的獨立任務**：這是一份有序的計畫。若是佇列，請使用任務清單。
- **新手上手檢查清單**：這裡的步驟屬於代理，人員無法勾選，它們只會隨代理工作而往前推進。
- **回報單一工作的狀態**：請使用 `Alert`、`Badge` 或表格。只有一個步驟的計畫說明的內容太少。

## 用法

未設定 `Steps`、`Status` 與 `Simulation` 時，卡片是一份空草稿："Nothing is planned yet. Add a step, or let the agent keep planning."，並帶有 Add step、Keep planning 與停用中的 Approve plan；它的按鈕只會引發各自的事件。套件不附帶範例計畫。上方的預覽為卡片設定了一個由展示庫撰寫的模擬：GPT-6.1 Sol（真實的模型名稱）為說明中心新增全文搜尋起草計畫（任務是虛構的），核准後由模擬執行。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentPlan MaxWidth="560" />
</UserControl>
```

`AgentPlan` 衍生自 Avalonia 的 `TemplatedControl`，位於 Special 套件中；請在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 來註冊。卡片的寬度與容器相同，最寬 560px，約 320px 寬時仍易於閱讀，低於 420px 時會收緊間距。

### 結構

1. **標頭**：模型的標記、"Plan · 6 steps"（新增或移除步驟時數字會滾動），其下為目標。計畫為草稿時，右側顯示模型名稱；開始執行後，右側會計算 "3 of 6"。
2. **進度線**：位於標頭底部分隔線上的 2px 線條，會隨已完成的步驟而延伸。草稿時隱藏。
3. **步驟**：編號清單。每一列都有固定的前導欄（拖曳把手，之後會變成狀態標記）、標題，以及固定的尾隨欄（移除按鈕，之後會變成時間或文字 Skipped）。核准計畫後欄位不會改變，因此標題換行的方式不變，也不會移動。
4. **Add step**，清單為空時會有一句說明。
5. **頁尾**：草稿時為 Keep planning 與 Approve plan；執行中為狀態，以及 Pause 或 Resume；完成時為總時間、摘要與 Start over。

六種狀態標記各有自己的形狀，因此狀態不會只靠顏色傳達：空心圓環（待處理）、每 800ms 轉一圈、帶四分之一弧線的圓環（進行中）、帶兩條槓的圓環（暫停）、會自行描繪出勾號的綠色圓盤（完成）、帶驚嘆號的紅色圓盤（失敗），以及虛線圓環（略過）。

### 編輯草稿

每個步驟標題都是一個文字區域，會隨文字增長。

- **Enter** 儲存改名並保留焦點。**Escape** 復原自欄位取得焦點以來（或自上次 Enter 以來）的編輯。離開欄位時會儲存。
- 在空白標題中按 **Backspace** 會移除該步驟，並把插入點移到上一步的結尾；第一個步驟則把插入點移到下一步的開頭。離開一個你已清空的欄位，同樣會移除該步驟。
- 在標題中按 **Alt+Up** 與 **Alt+Down** 會把步驟移動一個位置；插入點維持原位。
- 拖曳把手可用指標移動步驟：該列會抬起並跟隨指標，越過相鄰步驟高度的一半時與其交換位置；放開後即定位。讓把手取得焦點後按 **Up** 或 **Down**，即可不用指標移動。
- 移除按鈕會在列被滑鼠停留或持有焦點時顯示。
- **Add step** 會附加一個空白步驟，並讓其標題取得焦點。沒有剩餘步驟時，Approve plan 會停用；Keep planning 仍可使用。

核准時會修剪標題並捨棄空白步驟。Keep planning 會請代理再提供一個步驟：按鈕中會出現旋轉指示，接著建議步驟會插入最後一個步驟之前，其標題會以每次數個字的方式串流出現。完成後，它就是一般可編輯的步驟，並附有 "Suggested" 註記。

### 受控的計畫

設定 `Status` 後，計畫即為受控：控制項不會改變狀態，也不會動到步驟的狀態、活動行、錯誤或時間。每個動作都會透過事件回報，由你更新資料。

| 事件 | 何時 | 你通常會 |
|---|---|---|
| `Approved` | 按下 Approve plan，步驟依序排列、已修剪且不含空白步驟 | 啟動代理，並將 `Status` 設為 Running |
| `Changed` | 草稿編輯：改名、移動、移除、新增或串流的建議（`Kind`） | 讓你保存的草稿副本與之同步 |
| `KeepPlanningRequested` | 按下 Keep planning | 設定 `IsPlanning`、詢問代理，並呼叫 `StreamSuggestion` |
| `PauseChanged` | 暫停（true）或繼續（false） | 暫停代理，並將 `Status` 設為 Paused 或 Running |
| `StepAction` | 對失敗的步驟按下 Retry 或 Skip（`Kind`、`Step`） | 重新執行該步驟或略過它，並更新其狀態 |
| `StartOverRequested` | 完成後按下 Start over | 重設步驟，並將 `Status` 設回 Draft |

草稿的編輯直接在 `Steps` 本身進行（控制項會在那裡改名、移動、移除與新增），並透過 `Changed` 回報；只有執行過程歸你負責。

<Demo name="Controlled" />

```csharp
plan.Approved += (_, e) =>
{
    foreach (var step in plan.Steps) { step.State = AgentPlanStepState.Pending; }
    plan.Status = AgentPlanStatus.Running;
    agent.Start(e.Steps);
};
plan.StepAction += (_, e) => agent.Decide(e.Step, e.Kind);
```

代理工作時，你改變步驟，控制項就會跟著更新：一次將一個步驟設為 `Active`，代理回報時把它附加到其 `Activities`，然後以 `Duration` 設為 `Done`。若是失敗，先設定 `Error`，再設為 `Failed`，讓通知能朗讀原因。將 `NoteKind = AddedByAgent` 的步驟插入到它該在的位置，它會滑入並帶有淡淡的強調色底色。在狀態變為 Done 之前設定 `Summary`，它會在 "Done in …" 之後串流出現。

### 執行中途的計畫

同一份資料也能驅動一個已在執行中的計畫。這個範例有兩個已完成的步驟、一個代理新增的步驟，以及一個等待決定的失敗步驟。

<Demo name="MidRun" />

失敗的步驟會顯示 Failed 一字、其原因，以及 Retry 與 Skip。頁尾會指出哪個步驟失敗，而 Pause 按鈕會退到一旁。鍵盤焦點會移到 Retry（否則 Pause 消失時，焦點會落到頁面上）；按下 Retry 或 Skip 後，焦點會回到 Pause。

### 模擬

`AgentPlanSimulation` 是用於預覽、新手導覽與測試的模擬代理：把它設為 `Simulation`，它會在 `Status` 為 null 時執行。它由事件驅動：每次狀態變化只等待一個計時器，計畫閒置時不執行任何東西；Pause 會保留剩餘時間，Resume 則從該處繼續。它會回應主機會發出的同樣按鈕。節拍依照實測的流程（1× 速度）設定：

| 節拍 | 時間 |
|---|---|
| 按下 Approve plan → 第一個步驟開始 | 280ms |
| 步驟開始 → 其第一行活動 | 372ms |
| 活動行之間 | 540ms |
| 最後一行 → 步驟完成 | 470ms |
| 步驟完成 → 下一個步驟開始 | 290ms |
| 最後一個步驟完成 → 計畫完成 | 420ms |
| Retry → 第一行新活動，接著 577ms，再 465ms → 完成 | 555ms |
| Skip → 下一個步驟開始 | 277ms |

模擬所說的一切都由你的應用程式撰寫：`Steps`（草稿，每一項是一個 `AgentPlanScriptedStep`，帶有由活動行與時間組成的 `AgentPlanStepScript`、可選的失敗，以及代理可選插入的步驟）、Keep planning 用的 `Suggestions`、沒有腳本的步驟（例如你新增的）所用的 `GenericScript`（未設定時，這樣的步驟完成時沒有活動行，也沒有時間），以及 `Goal`、`Model` 與 `Summary`（計畫未設定自己的值時顯示）。在預覽中，代理會在第三個步驟的第二行之後（417ms 之後）插入一個步驟，而測試步驟會在最後一行之後失敗一次，接著只要需要就會一直等待 Retry 或 Skip。腳本是依步驟 id 尋找的，因此改名的步驟會保留其腳本，被移除的步驟則會被略過。Start over 會回到執行開始前的草稿，所有建議都會再次可用。計畫掛接模擬時讀取這些資料；每個計畫各建一個模擬，因為它保存執行狀態。在 XAML 中，把 `<arc:AgentPlan.Simulation>` 設為一個 `AgentPlanSimulation`，其屬性來自 `{x:Static}`，寫法見預覽的 Code 分頁。把 `Simulation` 設回 null 會帶走它的草稿，計畫變為空。

下面的示範還替換了文字，並在程式碼中設定模擬：

<Demo name="Labels" />

```csharp
plan.Simulation = new AgentPlanSimulation
{
    Steps = [new("a", "Find every link to the old page", new(["Found 14 links"], TimeSpan.FromSeconds(9)))],
    Suggestions = [],
    Summary = "The page now lives at /preferences."
};
```

### 時間與計數

步驟時間在一分鐘以下以整數秒顯示（"23s"），滿一分鐘後以分鐘加兩位數秒顯示（"1m 07s"）。總時間是已完成步驟時間的總和，因此略過與失敗的步驟不計入。標題列計數為 "{n} of {count}"，其中 n 是第一個尚未完成或略過的步驟；進度線則是已完成與略過的步驟數除以全部步驟數。計數以保留的寬度與等寬數字滾動，因此數字變化時不會推動旁邊的文字。

### 窄容器

低於 420px 時，標頭、清單與頁尾都會收緊；草稿的兩個按鈕會平均分享一列。標題會依需要換成多行；欄位保持不變。

<Demo name="Narrow" />

### 減少動態效果

減少與關閉動態效果時，會保留每一種狀態、標籤、焦點移動與播報，但會去除所有會移動的效果：列、活動行、失敗區塊與頁尾會立即改變高度，圓環不再轉動，勾號直接以完整狀態出現，閃爍停止，插入的步驟不再有底色，建議的文字則會一次全部出現。模擬仍會執行相同的時間軸。

### 文案規範

將標題寫成約 6 到 10 個字的指令，活動行則寫成簡短的過去式片語，並以計畫認得的動詞開頭（`AgentPlanActivityIcons`）：Read、Searched、Found、Created、Edited、Ran 或 Measured。若要註冊更多動詞，請使用 `AgentPlanActivityIcons.Set`。

| 不要寫 | 改為 |
|---|---|
| 步驟 1 | 找出舊設定頁面的每一個連結 |
| 正在進行搜尋 | Searched for /settings |
| 測試 | Ran 6 tests |
| 出了點問題 | 14 項測試中有 3 項失敗：拼字比對會略過兩個字母的字詞 |
| 完成了！ | 此頁面現在位於 /preferences，舊網址會重新導向。 |

### 最佳做法

**建議**

- 將計畫控制在四到八個步驟。更長的計畫是一個專案，請拆分它。
- 用代理自己的語言說明失敗的原因，並提供 Retry 與 Skip，讓人員自行決定。
- 傳入一個有自己標記的模型，讓代理的備註看起來是代理自己寫的。
- 改名或重新排序時保持 `Id` 穩定；模擬、播報與你自己的記錄都依賴它。

**避免**

- 有人正在編輯草稿時，來回切換 `Status`。
- 把計畫當成記錄。活動行是每個步驟的少數幾則簡短事實，步驟完成後會收合。
- 為模型使用品牌標誌。請使用通用字符，並在文字中說明模型名稱。

### 常見問題

**計畫會執行任何東西嗎？** 不會。它只顯示計畫；沒有 `Status` 時，它會播放你給它的 `Simulation`（如果有）。實際工作由你負責：回應 `Approved` 並更新步驟。

**如何顯示代理新增的步驟？** 計畫執行期間，插入一個 `NoteKind = AgentPlanNoteKind.AddedByAgent` 的 `AgentPlanStep`。它會滑入、帶有底色與註記，並會被播報。

**執行中可以編輯嗎？** 不行。計畫核准後列就固定了；唯一的控制項是 Pause 或 Resume、失敗時的 Retry 與 Skip，以及結束時的 Start over。

**如果我沒有模型怎麼辦？** 將 `Model` 保持為 null（並使用你自己的步驟）：標題列不會顯示標記與名稱。

**為什麼 Approve plan 會停用？** 草稿沒有任何步驟。新增一個，或讓代理繼續規劃。

## API 參考

<!-- api: AgentPlan -->

<!-- api: AgentPlanStep -->

<!-- api: AgentPlanLabels -->

<!-- api: AgentPlanSimulation -->

<!-- api: AgentPlanStepScript -->

<!-- api: AgentPlanScriptedStep -->

<!-- api: AgentPlanApprovedEventArgs -->

<!-- api: AgentPlanChangedEventArgs -->

<!-- api: AgentPlanStepActionEventArgs -->

<!-- api: AgentPlanPauseChangedEventArgs -->

<!-- api: AgentModel -->

列舉：

| 類型 | 值 |
|---|---|
| `AgentPlanStatus` | `Draft`, `Running`, `Paused`, `Done` |
| `AgentPlanStepState` | `Pending`, `Active`, `Done`, `Failed`, `Skipped` |
| `AgentPlanNoteKind` | `None`, `Suggested`, `AddedByAgent` |
| `AgentPlanChangeKind` | `Rename`, `Move`, `Remove`, `Add`, `SuggestionStreamed` |
| `AgentPlanStepActionKind` | `Retry`, `Skip` |

## 無障礙

- 卡片是一個群組，以模型與目標命名（例如 "GPT-6.1 Sol plan: Add full-text search to the help center"）。步驟是有序清單，因此螢幕閱讀器會朗讀總數與每個步驟的位置。
- 計畫執行時，每個步驟都會在標題前以文字播報其狀態（Pending、In progress、Done、Failed、Skipped）。每種狀態也有自己的形狀，因此絕不只靠顏色傳達。
- 失敗的步驟會朗讀 Failed 一字、其原因，以及兩個有標籤的按鈕（Retry 與 Skip，各以步驟名稱命名）。
- 標題是一個有標籤的文字區域（"Step 3"），並有一段提示說明 Alt 加方向鍵與 Escape。把手與移除按鈕則以其位置與步驟命名（"Move step 3: …"）。
- 進度線是帶有文字值的進度列（"2 of 6 steps finished"）；草稿時它會對輔助技術隱藏。
- 一個禮貌的狀態區域會播報編輯、核准（以及執行是模擬的）、每個完成的步驟、插入的步驟、失敗、重試、略過、暫停與繼續、最後的摘要與重設。用指標拖曳時不會播報。
- 焦點跟隨動作：移動後的步驟保留焦點；被移除的步驟把焦點交給相鄰步驟（最後一個時交給 Add step）；Add step 讓新標題取得焦點；Approve plan 把焦點交給 Pause；失敗時焦點移到 Retry；Retry 與 Skip 把焦點交還給 Pause；Done 時焦點移到 Start over，Start over 則移到 Approve plan。
- 鍵盤焦點會顯示 2px 的焦點環（列內的控制項則為內嵌）；滑鼠焦點不顯示。標題則改以安靜的內嵌線條表示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 草稿中：依序為每個步驟的把手、標題與移除按鈕，然後是 Add step、Keep planning 與 Approve plan。執行中：失敗步驟的 Retry 與 Skip，以及 Pause 或 Resume。完成：Start over |
| `Alt+Up` / `Alt+Down` 於標題 | 將步驟移動一個位置；插入點保持不動 |
| `Up` / `Down` 於把手 | 將步驟移動一個位置；到達兩端時不會有動作 |
| `Enter` 於標題 | 儲存改名並停留在欄位中 |
| `Escape` 於標題 | 復原自欄位取得焦點或上次儲存以來的編輯 |
| `Backspace` 於空白標題 | 移除該步驟並讓上一步取得焦點 |
| `Space`、`Enter`、`Home`、`End` 於把手 | 無動作：沒有挑選模式 |

## 動態效果

- **所有大小或位置的變化都使用同一種彈簧**：臨界阻尼，約 260ms，無過衝。步驟插入、移除或移動時，列會滑動並伸展；被拖曳列的相鄰列會讓開；活動行或失敗區塊展開時也是如此；頁尾與進度線會跟隨。放下的列會以稍快的彈簧定位。
- 步驟完成時，勾號會以筆畫描繪（45ms 後開始，歷時 200ms），圓盤淡入，標題從主要文字沉降為次要文字（240ms），時間則淡入。其活動行會在同一時刻收合。
- 目前步驟的標題會在轉動的弧線旁閃爍（1.8 秒一趟，之後短暫休息）。暫停會同時停止兩者。
- 建議以每 34ms 三到五個字為一段串流出現，每段只做淡入；代理插入的步驟會以 8% 的強調色底色到場，維持約 640ms，然後在約 1.2 秒內淡出。
- 計數以等寬數字在保留的寬度中滾動。標題列右側、頁尾的各列與狀態標記，都以一次短暫的交叉淡化切換（舊的 60ms 淡出，新的上升 3px 並在約 145ms 內淡入）。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 高度與位置 | 彈簧 | 立即 | 立即 |
| 狀態切換 | 交叉淡化並上升 3px | 短暫交叉淡化 | 立即 |
| 旋轉指示與閃爍 | 轉動與掃過 | 靜止 | 靜止 |
| 勾號 | 200ms 內描繪 | 直接顯示為完成 | 直接顯示為完成 |
| 插入的步驟 | 滑入並帶底色 | 出現 | 出現 |
| 串流的建議 | 各段淡入 | 所有文字同時出現 | 所有文字同時出現 |
| 模擬的時間軸 | 不變 | 不變 | 不變 |

## 相關元件

- `AgentStatus`：一個執行中代理的精簡即時狀態。
- `ToolCall`：單一工具呼叫，含其輸入、輸出與核准。
- `AgentTimeline`：協調執行的時間軸。
- `ContextWindow`：一次執行使用了模型上下文的多少。
- `ChatThread`：當你要顯示對話本身時使用。
