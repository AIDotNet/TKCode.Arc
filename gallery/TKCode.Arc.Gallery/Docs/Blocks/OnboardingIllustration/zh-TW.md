---
title: Onboarding illustration
summary: 精簡的首次執行卡片，線條插畫會隨人們逐步前進，從空白的盒子形變為檔案、團隊與徽章。
source-hash: e287773de8c6
---

<Demo name="Preview" />

## 何時使用

`OnboardingIllustration` 是人員建立帳號或工作區之後最先看到的畫面：一張簡短的卡片，說明三到四個概念，每一步各配一張簡單的插畫。這些插畫是同一組線條圖，其中的形狀會從一個場景移到下一個場景，因此各步驟連成一個完整的故事（空白的空間、內容送達、人員加入、完成），而不是四張各自獨立的投影片。把它放在對話方塊或首次執行的頁面中，並讓 Get started 送出到設定工作區的流程。

- **文件或工作區產品的首次啟動**：一款筆記、維基或檔案空間，一開始是空的。各步驟說明內容存放在哪裡、如何匯入檔案，以及如何邀請人員；按下 Get started 會開啟新的空間。
- **新的專案、看板或頻道**：專案工具引導人員建立第一個看板，或團隊聊天應用程式建立頻道並邀請成員。通常三個步驟就夠了。
- **改變工作方式的功能**：共同審閱、在某一行上留言，或新的匯入功能。在人員第一次使用前，先用三到四個步驟的簡短導覽介紹。
- **人們會回來看的導覽**：完成狀態會提供 Replay intro，因此同一張卡片可以放在說明選單中，例如 "Show the tour again" 這個選項。

## 何時不用

- **需要數天才能完成的工作**：使用檢查清單，追蹤每項工作的進度，並在最後一項完成之前保持可見。這張卡片不會記住人員已讀過哪些步驟。
- **分多個步驟收集資訊的表單**：使用 `Stepper` 或多步驟表單。這張卡片只顯示資訊；它沒有欄位，也沒有驗證。
- **行銷頁面上的功能區段**：使用含較大圖片與行動呼籲的功能區塊。這張卡片依對話方塊設計，最寬 440 px。
- **長篇說明**：每一步只放一個標題與一兩句話。需要標題、清單或程式碼的內容應放在文件中，並從最後一步連結過去。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OnboardingIllustration Label="Northdesk onboarding"
                                GetStartedCommand="{Binding CreateWorkspaceCommand}" />
</UserControl>
```

`OnboardingIllustration` 衍生自 `TemplatedControl`。沒有設定步驟時，它會顯示四個範例步驟，每個場景各一個，因此在您寫入任何文字之前，它看起來已經完整。此區塊本身不做任何工作：它負責在步驟之間移動、引發事件，其餘交由應用程式處理。處理 `GetStarted`（或繫結 `GetStartedCommand`）來建立工作區、關閉對話方塊或開啟下一頁。

### 組成部分

1. **卡片**：寬度最多 440 px（容器較窄時依容器），1 px 邊框、20 px 圓角、凸起表面顏色與凸起陰影。`IsFramed="False"` 會移除邊框、圓角與陰影。
2. **舞台**：淡色表面，下方有 1 px 線。上方內距 40 px，左右 20 px，下方 16 px。
3. **插圖**：寬 320、高 200 的畫布會等比縮放，最大為 352 × 220 px。共七個圓角形狀、一條點狀軌跡、三條短線與一個勾號。1.5 px 的描邊會隨插圖一起縮放。
4. **略過**：28 px 的膠囊按鈕，位於舞台右上角內 10 px 處，在除最後一步以外的每一步都會顯示。
5. **文字**：步驟標題（展示字型，18 px，中等粗細）位於句子（14 px，次要文字顏色）上方。文字區至少 72 px 高，因此一行與兩行的句子，按鈕都會在同一位置。
6. **步驟圓點**：每一步一個 6 px 的圓點；目前步驟的圓點寬 20 px。已完成的步驟使用淡文字顏色，目前步驟使用強調色，之後的步驟使用較強的邊框色。每個圓點都是一個按鈕，點擊範圍為 32 px。
7. **返回**：36 px 的外框按鈕，附箭頭。在第一步與完成狀態中，它看起來是停用的，但仍保留在 Tab 順序中。
8. **主要按鈕**：寬 136 px，以前景色填滿。中間步驟顯示 Next，最後一步顯示 Get started，完成導覽後顯示 Replay intro。其寬度不會因文字而改變。

### 場景

每個步驟都指定一個場景。各場景共用相同的形狀，而且每個形狀在每個場景中都扮演一個角色，因此從一步移到下一步時，看起來是同一張圖在變化，而不是投影片在切換。

| 場景 | 顯示內容 | 用於 |
|---|---|---|
| `Folder` | 一個空的開啟盒子，旁邊有加號 | 新的空白空間；第一步 |
| `Files` | 三頁文件落入盒子，同時一張便條沿著點狀軌跡飛入 | 匯入內容：匯入、上傳、已連結的來源 |
| `Team` | 三個人站在共用看板下方；一則點狀留言指向一行被標示的文字 | 邀請人員、分享、共同審閱 |
| `Badge` | 圓形徽章，附兩條緞帶、三道光線，以及正在描繪出來的勾號 | 完成：最後一步 |

同一個場景可以出現在多個步驟中；兩個使用相同場景的步驟之間切換時，只會改變文字。步驟不一定要照上面的順序排列，但依此順序故事最容易理解，因為每個形狀都會移到它的下一個角色。

### 自訂步驟

<Demo name="CustomSteps" />

```xml
<arc:OnboardingIllustration Label="Fieldnote onboarding"
                            CompletedTitle="Study created"
                            CompletedDescription="Open the tour again from Help whenever you like.">
    <arc:OnboardingStep Scene="Folder"
                        Title="Name your research space"
                        Description="A space holds one study. Start with the April interviews and add more studies later."
                        SceneDescription="An empty open box with a plus sign beside it" />
    <arc:OnboardingStep Scene="Files"
                        Title="Import your recordings"
                        Description="Drop audio, transcripts and notes. Fieldnote tags each file by speaker and date."
                        SceneDescription="Recordings dropping into the open box while a note flies in" />
</arc:OnboardingIllustration>
```

`Steps` 是 `AvaloniaList<OnboardingStep>`，也是控制項的內容，因此步驟可以直接寫在元素內。檢視模型可以改用 `StepsSource` 提供步驟；它的優先順序高於 `Steps`，可觀察集合的變更會立即顯示。顯示中的步驟若修改了 `Title`、`Description` 或 `Scene`，卡片會就地更新。

`SceneDescription` 是該步驟插圖的無障礙名稱。請用一個簡短的片語描述圖片所呈現的內容，例如 "Three people gathered around one shared board"。

按鈕文字、圓點的無障礙名稱與朗讀的狀態文字，都來自 `Labels`。它是一個 `OnboardingIllustrationLabels` 物件，預設為英文。含數字的文字是函式，因此其他語言可以調整字詞的順序：

```csharp
card.Labels = new OnboardingIllustrationLabels
{
    Skip = "Skip tour",
    GetStarted = "Open study",
    ReplayIntro = "Show tour",
    StepStatus = (step, count) => $"Tour step {step} of {count}",
    DotName = (step, title) => $"Tour step {step}: {title}"
};
```

### 步驟、完成與事件

| 動作 | 結果 | 事件 |
|---|---|---|
| Next（主要按鈕，最後一步之前） | 下一步 | `StepChanged`（`Next`） |
| Back | 上一步；第一步時無作用 | `StepChanged`（`Back`） |
| 步驟圓點 | 該步驟，即使相隔數步；離開完成狀態 | `StepChanged`（`Dot`） |
| 左 / 右方向鍵 | 上一步或下一步；兩端或完成後無作用 | `StepChanged`（`Keyboard`） |
| Skip | 最後一步（不是完成狀態） | `StepChanged`（`Skip`），然後 `Skipped` |
| Get started（最後一步） | 完成狀態，若 `IsCompletionVisible` 為真 | `GetStarted` |
| Replay intro | 第一步 | `StepChanged`（`Replay`），然後 `Replayed` |

`CurrentIndex`（從 0 開始）與 `IsCompleted` 是雙向繫結，因此檢視模型可以追蹤卡片並移動它。將 `IsCompleted` 設為 true 會移到最後一步；在完成狀態下設定 `CurrentIndex`，會離開完成狀態。方法 `Next`、`Back`、`GoTo`、`Skip`、`Complete` 與 `Replay` 可從程式碼執行相同的動作，若沒有任何可做的事，則回傳 false。

<Demo name="Controlled" />

`StepChanged` 會帶著舊的索引、新的索引與原因，包括由程式碼所做變更的 `Programmatic`。`GetStartedCommand` 與 `SkipCommand` 會在其事件之後執行，並以事件引數作為參數。

<Demo name="EventsLog" />

```csharp
card.GetStarted += async (_, _) =>
{
    await workspaces.CreateAsync(name);
    dialog.Close();
};
```

當應用程式自行關閉對話方塊時，請設定 `IsCompletionVisible="False"`：此時 Get started 會引發 `GetStarted`，卡片則停留在最後一步，而不會顯示完成狀態。若卡片會一直留在畫面上（例如說明頁或側邊面板），請保留預設值，讓人員看到導覽已完成，並可以重播。

### 窄版配置

卡片依自身寬度調整，而不是視窗寬度。寬度小於 410 px 時，它會切換為精簡配置：插圖上方 42 px、插圖周圍 12 px，文字周圍 16 px，標題較小，文字區至少 88 px 高（足以容納三行），返回按鈕改為 36 px 的箭頭按鈕（對螢幕閱讀器仍名為 "Back"），主要按鈕為 128 px。插圖會隨卡片縮小，並保持比例。

<Demo name="Narrow" />

在手機上，卡片會從邊緣到邊緣佔滿螢幕，請設定 `IsFramed="False"`。

### 謹慎使用動態效果

形變是這張卡片的特色：方盒子變成紙張的目的地，紙張變成人，人再變成徽章。它之所以能運作，是因為每一步都很短，而且由人員掌控節奏。

- 每一步只放一個標題與一兩句話。插圖會承載變化；過長的文字會分散注意力，並讓人必須等待文字，而不是看插圖。
- 由人員推動步驟。不要用計時器自動前進：形變大約需要一秒，若步驟在人們閱讀時自行前進，內容就會錯過。
- 減少動態效果時，形變會改為整張插圖 240 ms 的交叉淡化，文字只淡入淡出，而不移動。

<Demo name="ReducedMotion" />

### 內容指引

- **標題**：人員在這一步要做什麼或會得到什麼，四到七個字，採句首大寫（sentence case）："Bring the files you already use"。不加結尾標點。
- **句子**：一到兩句話，附上該空間中的具體例子："Drag in slides, sheets, and PDFs."。若應用程式知道真實名稱（專案或隊友），請提及。
- **最後一步**：確認已就緒的內容；若已知數量，請附上數字："Spring launch notes now has 5 files and 2 teammates."。
- **完成狀態**：說明發生了什麼，以及如何再看一次導覽，各用一句簡短的話。
- **場景描述**：描述圖片，而不是步驟："Pages dropping into the open box"，而不是 "Import step"。

| 避免 | 改寫為 |
|---|---|
| Welcome to our amazing platform! | Set up your first space |
| Step 2 | Bring the files you already use |
| You can do lots of things with teams. | Invite Ana and Ruben to the same draft. |
| Success! | You are all set |
| Finish | Get started |

### 最佳實務

**建議**

- 導覽保持在三到四個步驟；步驟更多時，請把其餘內容放在人員實際遇到該功能的產品中。
- 依故事順序使用場景（`Folder`、`Files`、`Team`、`Badge`），讓每個形狀都移到它的下一個角色。
- 讓 Get started 送到有用的地方：新的工作區、匯入對話方塊或第一個專案。
- 保留 Skip。熟悉產品的人應該能一按就到達結尾。
- 在說明選單中提供再次導覽，並在那裡顯示完成狀態，讓 Replay intro 只要一按即可使用。

**避免**

- 在卡片中收集輸入。它沒有欄位；設定與名稱請使用表單。
- 依計時器或捲動前進。由人員決定何時前進。
- 隱藏圓點。圓點顯示導覽有多長，並讓人員跳回想再次閱讀的步驟。
- 在導覽中途使用徽章場景（`Badge`）：它表示「完成」，會讓導覽中間的人感到困惑。

### 常見問題

**如何只顯示一次卡片？** 卡片不會記住任何事。在 `GetStarted`（或 `Skipped`）被引發時儲存一個旗標，下次就不要再開啟對話方塊。

**為什麼第一步的 Back 仍可取得焦點？** Back 被標示為不可用，但仍留在 Tab 順序中（Arc 的 `aria-disabled`），因此步驟讓它不可用時，焦點不會跳走。螢幕閱讀器會朗讀它為停用，按下它不會有任何作用。

**按下 Skip 後，焦點會怎樣？** Skip 在最後一步會消失。如果它原本有焦點，焦點會移到主要按鈕（Get started），讓鍵盤使用者可以按 Enter 繼續。

**步驟可以重複使用場景嗎？** 可以。兩個步驟使用相同場景時，在兩者之間移動只會改變文字。

**卡片可以有超過四個步驟嗎？** 可以，也可以只有一個步驟（此時唯一的按鈕是 Get started）。圓點會隨步驟增加；超過五或六個時，通常應該縮短導覽，而不是增加圓點。

## API 參考

<!-- api: OnboardingIllustration -->

<!-- api: OnboardingStep -->

<!-- api: OnboardingIllustrationLabels -->

<!-- api: OnboardingStepChangedEventArgs -->

## 無障礙

- 卡片是一個區域，名稱取自 `Label`（"Northdesk onboarding"）。首次執行時開啟的話，請把它放在有自己名稱的對話方塊中。
- 插圖是一個影像，名稱取自目前步驟的 `SceneDescription`；名稱會隨步驟改變。
- 每次步驟改變時，都會透過禮貌的即時區域朗讀新的標題、句子與位置（"Step 2 of 4"）。完成狀態會朗讀其標題與句子，接著是 "Onboarding complete"。
- 圓點是名為 "Onboarding steps" 的群組。每個圓點是名為 "Step 2: Bring the files you already use" 的按鈕，目前的圓點會回報 "Current step"。
- 第一步與完成狀態中，Back 會回報為停用，但仍可取得焦點。在精簡配置中，它只顯示箭頭，名稱仍為 "Back"。
- 每次操作後焦點都停留在原處：按 Next、Get started 與 Replay intro 後在主要按鈕，按 Back 後在 Back，按圓點後在該圓點。Skip 在取得焦點時消失，焦點會移到主要按鈕。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 依序移過 Skip、每個步驟圓點、Back 與主要按鈕 |
| `Left` / `Right` | 上一步或下一步，從卡片內任何位置皆可；焦點不會移動。兩端無作用，完成後也無作用（`Right` 永遠不會按下 Get started） |
| `Enter` / `Space` | 按下已取得焦點的按鈕或圓點 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 步驟改變 | 每個形狀彈向下一個場景（中心約在半秒內到位，尺寸、圓角與轉動稍慢），每個相隔 30 ms 依序進行；往回時，最後一個形狀先移動。軌跡在 100 ms 時彎入，其圓點在 160 ms 時出現；線條依 120、160 與 200 ms 跟進；顏色在 480 ms 內交叉過渡 | 舊畫面淡出，新畫面淡入，240 ms | 立即 |
| 徽章出現 | 勾號從 320 ms 開始描繪，歷時 480 ms | 直接顯示為完成 | 直接顯示為完成 |
| 約 0.7 秒內再次改變 | 每個形狀立即朝新場景轉動，並保持其速度 | 交叉淡化 | 立即 |
| 文字改變 | 舊文字上移 4 px，並在 180 ms 內模糊淡出；新文字從 80 ms 起由模糊中上升 6 px，歷時 480 ms | 文字淡入淡出（180 ms 淡出，480 ms 淡入） | 立即 |
| 主要按鈕的標籤改變 | 舊標籤在 180 ms 內上移淡出；新標籤在 240 ms 內上升淡入 | 標籤淡入淡出 | 立即 |
| 步驟圓點 | 目前圓點以彈簧方式加寬到 20 px，舊圓點收窄到 6 px；顏色在 240 ms 內交叉過渡 | 寬度立即改變；顏色仍交叉過渡 | 立即 |
| 略過出現或消失 | 160 ms 內淡入 | 160 ms 內淡入 | 立即 |
| 滑鼠移過舞台 | 形狀朝指標飄移，水平最多 6 px、垂直向下最多 4 px，位於前方的形狀飄移更多 | 不飄移 | 不飄移 |

卡片沒有進場動畫，靜止時也沒有動態效果：插圖只在操作之後或滑鼠移動時才會移動。

## 相關元件

- `EmptyStates`，用於空白的檢視，其插圖會在四種情況之間形變。
- `Stepper`，用於需要收集輸入並驗證每一步的多步驟任務。
- `Dialog`，用於在首次執行時承載這張卡片。

| 元件 | 適用時機 |
|---|---|
| `OnboardingIllustration` | 三到四個概念的簡短首次執行導覽，每個概念都配一張圖 |
| `EmptyStates` | 檢視是空的，而插圖說明原因 |
| `Stepper` | 人員分多個步驟填寫表單 |
| `Dialog` | 首次執行時，卡片開啟於應用程式之上 |
