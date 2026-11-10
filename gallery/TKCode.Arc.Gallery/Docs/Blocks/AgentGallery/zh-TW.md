---
title: 智慧體商店
summary: 可瀏覽的現成智慧體列表：分類膠囊、會標出命中文字的搜尋、排序開關和自適應的卡片網格；每張卡片都能打開詳情視圖，列出能力、所需權限和可以試問的提示，並在其中安裝（帶進度）或解除安裝。
source-hash: 499e42b0bc8f
---

<Demo name="Preview" />

## 何時使用

智慧體商店是一個提供現成智慧體的產品的展示窗。人們按分類瀏覽，按名稱或任務搜尋，按熱度、日期或評分排序，並掃視一個卡片網格：帶底色的標誌、名稱和作者、一句話說明智慧體做什麼、評分和安裝數、兩個標籤和一個「Install」按鈕。點開卡片會在網格的位置顯示這個智慧體的頁面：更長的介紹、它能做什麼、它需要存取什麼（敏感的存取用圖示和文字標出）、可以試問的提示，以及安裝按鈕：安裝時顯示進度，裝好後顯示已安裝，並可以移除。

- **供人挑選的目錄**：一個工作區可以加入的十幾個到幾百個智慧體、範本或助手。
- **先看再裝**：在任何人按下安裝之前，詳情視圖就說明了智慧體能存取什麼。
- **放置你自己團隊做的智慧體**：給它們一個分類，它們就和其他智慧體放在一起。
- **從選擇開始對話**：詳情視圖裡的提示會觸發 `PromptChosen`，你可以用這個智慧體打開一個對話。

## 何時不用

- **為對話選一個模型**：請用 `ModelPicker`；商店用來加入東西，不是用來切換的。
- **管理已經安裝的內容**：帶開關的設定列表比展示窗更合適。
- **成千上萬項**：卡片會重複使用，但沒有虛擬化；超過幾百項時請分頁，或在伺服器上搜尋。
- **建立或編輯智慧體**：那是一個包含說明、工具和測試對話的表單，而不是商店。

## 用法

`AgentGallery` 衍生自 Avalonia 的 `TemplatedControl`。沒有資料時，它會顯示一個虛構的目錄：六個分類中的十四個智慧體，因此可以直接放到頁面上。傳入你自己的 `Agents`（`AgentGalleryItem` 列表）和 `Categories`（帶鍵和標籤的 `AgentGalleryCategory`），並繫結 `InstalledIds`。

```xml
<arc:AgentGallery Agents="{Binding Catalog}"
                  Categories="{Binding Categories}"
                  InstalledIds="{Binding InstalledAgentIds}" />
```

```csharp
gallery.Agents = new[]
{
    new AgentGalleryItem
    {
        Id = "brief-writer", Name = "Brief Writer", Maker = "Your team", IsVerified = true,
        CategoryKey = "writing", Glyph = ArcIcons.PenLine, Tint = AgentGalleryTint.Series2,
        Pitch = "Writes a project brief from a short chat.",
        Rating = 4.7, RatingCount = 120, Installs = 2_400, AddedOn = new DateOnly(2026, 10, 1),
        Tags = ["Briefs", "Planning"],
        Abilities = ["Ask the questions a brief needs", "Write the brief in your template"],
        Permissions = [new AgentGalleryPermission("Read your project folder")],
        SamplePrompts = ["Start a brief for the spring launch"]
    }
};
```

### 結構

- **頁首**：標題、副標題和搜尋框。
- **分類膠囊**：「All」加上每個分類一個膠囊；選中膠囊的背景會在膠囊之間滑動。
- **數量和排序**：顯示了多少個智慧體，以及 Popular、Newest、Top rated 排序開關。
- **卡片網格**：按 248 px 能放下多少欄就放多少欄（最多四欄），等寬；每一列和這一列最高的卡片一樣高。
- **卡片**：標誌（智慧體的圖示放在由主題顏色著色的方塊上）、名稱、作者（已驗證的作者帶一個勾）、Featured 標記、一句話介紹（最多兩行）、兩個標籤、評分和安裝數，以及安裝按鈕。
- **詳情視圖**：返回按鈕、大標誌與名稱、作者和一行資訊、安裝操作、介紹、「What it can do」、「Needs access to」和「Try asking」。

### 瀏覽和搜尋

膠囊、搜尋和排序可以組合：搜尋在選中的分類裡進行，比對名稱、作者、介紹、標籤和分類名，不區分大小寫；名稱和介紹中的命中文字會被標出。沒有結果時，網格換成空狀態，提供 **Clear search**（選了分類時還有 **Show all categories**）。`SelectedCategoryKey`、`SearchText`、`Sort` 和 `OpenAgentId` 都是雙向的，所以視圖可以儲存並還原。

<Demo name="Filtered" />

### 安裝

卡片上的按鈕和詳情視圖裡的按鈕都會安裝智慧體。設定 `InstallAsync` 來呼叫你的後端：執行期間按鈕顯示轉圈和「Installing」；完成後 id 加入 `InstalledIds`，並觸發 `AgentInstalled`。如果它擲出例外，卡片顯示 **Retry**，詳情視圖顯示錯誤訊息，並觸發 `InstallFailed`。沒有處理常式時，商店會模擬一次 1.2 秒的安裝。已安裝的智慧體在詳情視圖中顯示 **Uninstall**，它會呼叫 `UninstallAsync`（或者直接移除 id）。

<Demo name="Install" />

```csharp
gallery.InstallAsync = async (agent, cancellation) =>
{
    await api.InstallAgentAsync(workspaceId, agent.Id, cancellation);
};
```

### 打開智慧體

點擊卡片、按 Enter 或空白鍵會打開它的詳情視圖；`Open("agent-id")` 在程式碼中做同樣的事，`AgentOpened` 會回報它。返回按鈕、Escape 或 `CloseDetail()` 回到網格，並把焦點放回這張卡片。打開智慧體時在搜尋框裡輸入，也會回到網格。

### 窄容器

商店按自身的寬度變化。寬度小於 600 px 時，搜尋框移到標題下方，內距收緊，網格只保留放得下的欄數。

<Demo name="Narrow" />

### 文案

所有文字都來自 `Labels`（`AgentGalleryLabels`）；需要格式化的文字是函式，所以詞序可以改變。控制項上的 `Title` 和 `Subtitle` 優先於文案物件；把 `Subtitle` 設為空字串會隱藏這一行。

### 內容建議

- **一句話介紹**：一句以動詞開頭的話，說明智慧體做什麼（「Sorts new mail into folders」）。
- **能力**：三到五個短句，每一句都是人們可以提出的一件事。
- **權限**：寫出資料和動作（「Read your mail」），在說明裡寫原因，並把任何會傳送、修改或刪除內容的權限標為敏感。
- **標誌**：選擇一個說明任務的圖示和一個主題顏色；商店從不顯示品牌顏色。

## API 參考

<!-- api: AgentGallery -->

<!-- api: AgentGalleryItem -->

<!-- api: AgentGalleryCategory -->

<!-- api: AgentGalleryPermission -->

### 事件

| 事件 | 觸發時機 | 參數 |
|---|---|---|
| `AgentOpened` | 打開一個智慧體的詳情視圖 | `Agent` |
| `AgentInstalled` | 安裝完成 | `Agent` |
| `AgentUninstalled` | 智慧體被移除 | `Agent` |
| `InstallFailed` | `InstallAsync` 擲出例外 | `Agent`、`Message` |
| `PromptChosen` | 按下「Try asking」中的一個提示 | `Agent`、`Prompt` |

## 無障礙

- 商店是一個區域；標題和智慧體名稱是標題，詳情中的各部分是低一級的標題。
- 每張卡片都是一個按鈕，名稱包含智慧體、作者和安裝狀態（「Inbox Sorter by Tidewater Labs, Installed」），一句話介紹作為說明文字。安裝按鈕的名稱是「Install Inbox Sorter」。
- 只有一張卡片是 Tab 停駐點，後面跟著它的安裝按鈕；方向鍵在卡片之間移動，所以頁面不會每張卡片都佔一次 Tab。
- 一個禮貌的即時區域會播報篩選或搜尋顯示了多少個智慧體，以及安裝何時開始、完成或失敗。
- 敏感權限帶有警告圖示和「Sensitive」一詞；安裝失敗有圖示和一句話。任何狀態都不只依靠顏色。
- 在詳情視圖中安裝後，焦點移到 Uninstall；移除後再回到 Install。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 搜尋框、選中的分類膠囊、排序開關、目前的卡片、它的安裝按鈕 |
| 搜尋框中的 `ArrowDown` | 移到目前的卡片 |
| 卡片上的 `ArrowLeft` / `ArrowRight` | 上一張或下一張卡片 |
| 卡片上的 `ArrowUp` / `ArrowDown` | 上方或下方的卡片 |
| 卡片上的 `Home` / `End` | 第一張或最後一張卡片 |
| 卡片上的 `Enter` 或空白鍵 | 打開它的詳情視圖 |
| 詳情視圖中的 `Escape` | 回到網格，焦點在這張卡片上 |
| 搜尋框中的 `Escape` | 清除搜尋 |

## 動效

- 篩選、搜尋或排序之後，卡片在 160 ms 內從 25% 的不透明度淡入（只有透明度，沒有位移）。
- 詳情視圖在 240 ms 內淡入並上升 8 px；離開時網格立即出現。
- 懸停卡片時，背景著色、邊框加深，用時 160 ms；選中的分類沿彈簧滑動。
- 安裝按鈕安靜地切換文字，安裝期間顯示轉圈。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 篩選後的卡片 | 160 ms 內從 25% 淡入 | 同樣的淡入 | 立即 |
| 打開詳情視圖 | 淡入並上升 8 px | 只淡入，120 ms | 立即 |
| 懸停卡片 | 160 ms 著色 | 立即 | 立即 |
| 分類膠囊 | 沿彈簧滑動 | 跳到位 | 立即 |

## 響應式

- 商店按自身的寬度變化。欄數：按 248 px 加 12 px 間距能放下多少欄就放多少欄，最多四欄。
- 寬度小於 600 px（`:compact`）時，搜尋框移到標題下方並拉伸，兩側內距從 20 降到 12。
- 分類膠囊放不下時在自己的列裡橫向捲動；排序開關也會在自身內部捲動。
- 在詳情視圖中，安裝操作換行到名稱下方，文字欄最寬 720 px。
- 外框最寬 1120 px 並置中；給它頁面的寬度，讓它向下增長。

## 相關元件

- `ModelPicker`：選擇智慧體使用的模型。
- `AgentMemory`：已安裝的助手記住了什麼。
- `PromptStudio`：編寫並測試提示。

| 元件 | 何時選擇 |
|---|---|
| `ModelPicker` | 人們在幾個模型之間切換 |
| `SearchResults` | 結果列表是文件，而不是要安裝的東西 |
| `PlanComparison` | 人們並排比較幾個方案 |
