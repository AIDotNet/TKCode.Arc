---
title: 模型選擇器
summary: 用一個緊湊的膠囊選擇模型及其推理強度：精選模型各附一句說明、推理強度設定、依提供者分組的其他模型，以及此選擇所需成本的概覽。
source-hash: 4e7c68136a56
---

<Demo name="Preview" />

## 何時使用

模型選擇器讓使用者決定由哪個模型回答，以及它要花多少力氣思考。它是一個膠囊，顯示目前的選擇；膠囊後面是一個選單；還有一個可選的概覽，說明該模型能容納的上下文大小與費用。它適合放在選擇很重要、但在使用者去找之前不應佔用空間的地方。

- **AI 功能的設定頁面**：支援助理或撰寫工具的預設模型。膠囊下方的概覽顯示上下文大小與每百萬權杖的價格，讓選擇有依據。
- **附有少數精選模型的模型目錄**：三到四個精選模型，各附一句說明（"Quick and capable for daily chats"），其餘模型依提供者分組，放在「更多模型」列之下。
- **與模型相關的推理強度控制**：有些模型在被要求時會思考得更久。「Effort」列只會出現在提供層級的模型上；下一個模型沒有的層級，會移到最接近的一層。
- **標頭或工具列中的小晶片**：將 `IsSummaryVisible` 設為 false，選擇器就只剩膠囊，可放在聊天頂部或提示欄旁邊。
- **依用量計費的產品**：概覽會列出輸入與輸出的價格，其下的小註記說明它們使用的幣別與單位。

## 何時不用

- **沒有說明的短固定選項**，例如小、中、大：使用 `SegmentedControl` 或 `Select`。
- **多個模型並排比較**：選擇器一次只顯示一個模型。需要比較價格與上限時，用 `Table` 建立表格。
- **數百個模型、使用者需要依名稱搜尋**：選單沒有搜尋欄。使用 `Combobox`，並自行篩選你的清單。
- **與模型無關的模型設定**，例如溫度或停止詞：把它們放在由 `NumberField` 與 `Switch` 組成的表單中。選擇器只選擇模型及其推理強度。
- **執行模型**：選擇器不會呼叫任何東西。它只回報選擇；由你的程式碼決定如何處理。

## 用法

`ModelPicker` 衍生自 Avalonia 的 `TemplatedControl`。它的範本包含一個 `ModelPill`、一個 `ModelSummary`，以及一個隱藏的 `ModelMenu`。選擇器擁有狀態：你提供模型，並讀回使用者的選擇。模型是一個 `AiModel`，即 `AvaloniaObject`，帶有名稱、供精選清單使用的一行 `Blurb`、供概覽使用的 `Description`、上下文大小與價格，以及它提供的推理強度層級。提供者為「更多模型」列分組，並為沒有標記的模型提供其提供者的標記。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ModelPicker SelectedModelId="aurora-prime-3" SummaryNote="Prices in USD per 1M tokens">
        <arc:ModelPicker.Providers>
            <arc:AiModelProvider Id="aurora" Name="Aurora Labs">
                <arc:AiModelProvider.Mark>
                    <StreamGeometry>M2 19a10 10 0 0 1 20 0h-3.2a6.8 6.8 0 0 0-13.6 0Z</StreamGeometry>
                </arc:AiModelProvider.Mark>
            </arc:AiModelProvider>
        </arc:ModelPicker.Providers>
        <arc:AiModel Id="aurora-prime-3" Name="Aurora Prime 3" ProviderId="aurora"
                     Blurb="Deep work: refactors, research, long tasks"
                     Description="A careful planner for multi-step coding and research."
                     ContextTokens="800000" InputPrice="6" OutputPrice="24" />
        <arc:AiModel Id="aurora-pocket-24" Name="Aurora Pocket 2.4" ProviderId="aurora" Tag="Fast"
                     ContextTokens="400000" InputPrice="0.4" OutputPrice="1.6" />
    </arc:ModelPicker>
</UserControl>
```

推理強度層級是一個清單，因此要在程式碼中設定（XAML 沒有陣列常值）：

```csharp
prime.EffortLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
prime.DefaultEffort = ModelEffort.Medium;
```

前四個模型為精選模型。`FeaturedIds` 會依你給定的順序挑選其他模型。其餘模型依 `Providers` 的順序分組，顯示在「更多模型」列之下。若沒有選擇，選擇器會選取第一個精選模型一次，並將它寫回 `SelectedModel`。

### 結構

1. **膠囊**：高 40 px，圓角為全圓，1 px 邊框。它顯示模型的標記（16 px，不加底圖）、中等字重的名稱、若模型有推理強度則以弱化文字顏色顯示的推理強度，以及選單開啟時會轉動的箭頭。懸停與開啟時使用弱化表面與較強的邊框。鍵盤焦點環只在使用鍵盤時出現。
2. **選單**：寬 304 px（視窗較窄時為視窗寬度減 24 px），位於抬起的表面上，使用面板圓角與浮動陰影。它在膠囊下方 8 px 處開啟。
3. **精選列**：高 54 px。每列有標記（18 px）、名稱、次要顏色的 Blurb，以及選中模型上的勾選。
4. **推理強度列**：高 40 px，目前的層級顯示在右側。當所選模型沒有任何層級時隱藏。
5. **更多模型列**：當所選模型不是精選時，在右側顯示其名稱。當所有模型都是精選時隱藏。
6. **子選單**：推理強度清單寬 264 px，每個層級有名稱、說明與勾選，模型預設層級之後會有 "Default"。其他模型清單寬 272 px、最高 420 px、可捲動，並把緊湊列（38 px）依提供者名稱分組，列尾顯示模型的 `Tag`。
7. **概覽**：模型的說明、一個包含上下文、輸入、輸出與推理強度的表格、一則可選註記，以及一行確認提示。

### 選擇模型、推理強度與其他項目

按下膠囊，或在它有焦點時按 `Enter`、`Space` 或 `ArrowDown`，即可開啟選單。一個醒目提示會跟隨指標移動，並隨方向鍵跳動。選擇一個模型即可選取它、關閉選單，並把焦點返回膠囊。選擇推理強度列或更多模型列會在右側開啟子選單；若右側沒有空間，它會在左側開啟；若左右都沒有空間，則會以頁面的形式開啟。選擇目前已選取的模型會關閉選單，且不做任何變更。

不提供目前所選推理強度的模型，會把它移到該模型提供的最接近層級，若兩個層級距離相同，則移到較低的那一個。選擇器會在確認提示中說明此事："Aurora Prime 3 → Cobalt Atlas 2 · Effort adjusted to High"。沒有層級的模型沒有推理強度（`Effort` 為 null），推理強度列會消失。

### 窄視窗中的頁面

當視窗寬度不超過 `PagedMenuWidth`（預設為 640）時，推理強度列與更多模型列會在選單內開啟頁面，而不是彈出的子選單。每個頁面上方都有一列返回列，內容從移動方向的那一側滑入，選單的高度會隨頁面調整。示範設定了非常寬的 `PagedMenuWidth`，因此你能在寬視窗中看到頁面。

<Demo name="Pages" />

### 概覽與確認提示

概覽會顯示所選模型的 `Description`，以及四項事實：上下文（`ContextTokens`，例如 "800K tokens"）、輸入與輸出（每百萬權杖的 `InputPrice` 與 `OutputPrice`，附 `CurrencySymbol`），以及推理強度。沒有層級的模型顯示 "Fixed"。模型留空的事實顯示破折號。在 400 px 或更窄時，事實會摺成兩行、每行兩項，三行的說明會使概覽變高，但不會移動膠囊。

在選單中做出選擇後，事實下方會出現一則確認提示：模型為 "Aurora Prime 3 → Cobalt Atlas 2"，推理強度為 "Effort: Medium → High"。它會停留 `NoticeDuration`（2.6 秒）並淡出。它的空間一律保留，因此其下的內容不會移動。`SummaryNote` 是事實下方的小字，用來說明單位，以及價格是否為真實價格。

### 只有膠囊

將 `IsSummaryVisible` 設為 false，選擇器就只剩膠囊。選單、鍵盤行為、事件與朗讀都與之相同，因此膠囊可以放在頁首或工具列中。

<Demo name="PillOnly" />

### 讀取與設定選擇

`SelectedModel`、`SelectedModelId` 與 `Effort` 是雙向的。`ModelChanged` 與 `EffortChanged` 會在選單中做出選擇、切換到模型提供的層級，或在程式碼中變更時執行。推理強度事件上的 `IsSnap` 在新模型使推理強度移動時為 true。`SelectCommand` 與 `EffortCommand` 會以新值執行，供偏好命令的檢視模型使用。

<Demo name="Bindings" />

```csharp
public sealed class PickerSettings
{
    public string? ModelId { get; set; }

    public ModelEffort? Effort { get; set; }
}

picker.ModelChanged += (_, e) => settings.ModelId = e.NewModel?.Id;
picker.EffortChanged += (_, e) => settings.Effort = e.NewEffort;

// An id the catalog does not have yet waits for it.
picker.SelectedModelId = "cobalt-atlas-2";
```

```xml
<arc:ModelPicker SelectedModel="{Binding Model, Mode=TwoWay}"
                 Effort="{Binding Effort, Mode=TwoWay}"
                 ModelsSource="{Binding Catalog}"
                 IsOpen="{Binding IsPickerOpen, Mode=TwoWay}" />
```

- `ModelsSource` 跟隨你可以變更的集合；它會取代行內的 `Models`。
- `IsOpen` 是雙向的。在選擇器顯示到畫面之前設定它，會在顯示後開啟選單一次。`Open()` 與 `Close()` 在程式碼中做同樣的事，而 `Open(ModelMenuOpenFocus.Selected)` 會把鍵盤位置設在所選模型上。
- 把 `Effort` 設為所選模型不提供的層級，會立即修正它，而事件會回報實際使用中的層級。

### 文案

`Labels` 保存選擇器顯示或朗讀的每個字，型別為 `ModelPickerLabels` 物件，帶有英文預設值。它分為膠囊、選單與概覽的部分，另外還有確認提示與朗讀。指派新的實例即可修改它們。層級名稱會出現在四個地方，因此請讓每個部分的函式功能一致。

<Demo name="CustomWording" />

```csharp
picker.Labels = new ModelPickerLabels
{
    EffortName = effort => effort == ModelEffort.Max ? "Maximal" : ModelEffortNames.Name(effort),
    ModelNotice = (previous, next) => $"{previous} → {next}",
    Menu = new ModelMenuLabels { MoreModels = "Weitere Modelle", Effort = "Aufwand" }
};
```

### 標記

模型的 `Mark` 可以是以 24 × 24 方框繪製、以 `MarkColor` 填滿（或以文字顏色填滿，會隨主題翻轉）的 `Geometry`，也可以是例如 `DrawingImage` 這類會保留自身顏色的 `IImage`。若沒有 `Mark`，模型會使用其提供者的標記。標記僅供裝飾；名稱一定會緊接在旁邊。請使用你自己的圖形：選擇器不會在標記周圍加上底圖或邊框。

### 文案規範

- **Blurb**：一行，說明模型擅長什麼，例如 "Reads charts, screenshots and PDFs"。它會以省略號截斷，因此把有用的字放在前面。
- **Description**：一到兩句，供概覽使用。說明它用來做什麼，以及使用者需要權衡什麼。
- **Tag**：一到兩個字，例如 "Fast" 或 "Open weights"。它是純文字，不是徽章。
- **價格**：只有真實時才印出真實數值。若是佔位價格，設定 `IsSamplePrice`，並在 `SummaryNote` 中說明。
- **名稱**：使用你的產品中使用者看到的名稱，並把精選清單維持在三到五個模型。

| 不要寫 | 改為 |
|---|---|
| 最適合所有事的模型 | 深度工作：重構、研究、長時間任務 |
| 便宜 | 快速又好用的日常聊天 |
| 模型有 1000000 個權杖 | 1M 權杖的上下文（概覽會將其格式化） |

### 最佳做法

**建議**

- 為每個模型提供穩定的 `Id`，並儲存該 id，而不是顯示名稱。
- 把 `EffortLevels` 與 `DefaultEffort` 放在模型上一起設定；預設值必須是其中一個層級。
- 把選擇器放在選擇所套用的地方，例如聊天的標頭或它所影響的欄位旁。
- 處理 `ModelChanged` 與 `EffortChanged` 來儲存選擇；選擇器只把它保存在記憶體中。
- 傳入 `Providers`，讓「更多模型」清單分組，並讓沒有標記的模型取得其提供者的標記。

**避免**

- 在不屬於該品牌的設計中使用真實的廠商標誌。
- 超過五個精選模型。選單就不再是一份短清單。
- 隱藏模型無法變更的推理強度：把 `EffortLevels` 留空，推理強度列會自行消失。
- 在每次更新時呼叫 `Open()`。它會把鍵盤焦點移入選單。

### 常見問題

**為什麼沒有搜尋？** 選單是一份短清單，其長尾依提供者分組。若是很長的目錄，請使用 `Combobox` 與自己的篩選。

**為什麼我選另一個模型時推理強度會改變？** 新模型不提供它。選擇器會把推理強度移到該模型提供的最接近層級，並在確認提示中說明。

**選單可以在懸停時開啟嗎？** 不行。膠囊在按下時開啟，與選單按鈕相同，因此指標經過時不會開啟任何東西。

**為什麼概覽顯示 "Fixed"？** 所選模型沒有推理強度層級。要更改文字，請使用 `Labels.Summary.NotAdjustable`。

## API 參考

<!-- api: ModelPicker -->

<!-- api: AiModel -->

<!-- api: AiModelProvider -->

### ModelEffort

| 值 | Name | 說明 |
|---|---|---|
| `Low` | Low | 簡短回覆，延遲最低 |
| `Medium` | Medium | 日常的平衡 |
| `High` | High | 處理較難的問題 |
| `ExtraHigh` | Extra high | 漫長而謹慎的推理 |
| `Max` | Max | 詳盡、最慢 |

`ModelEffortNames.Name(effort)` 與 `ModelEffortNames.Description(effort)` 會回傳這些字，而 `ModelEffortSnap.Snap` 會挑選模型提供的層級。

### ModelPickerLabels

| 屬性 | 預設值 | 顯示位置 |
|---|---|---|
| `Pill` | `ModelPillLabels` | 膠囊的無障礙名稱 |
| `Menu` | `ModelMenuLabels` | 選單的列、頁面與無障礙名稱 |
| `Summary` | `ModelSummaryLabels` | 概覽的事實標籤與值 |
| `EffortName`（層級的函式） | "Medium" | 確認提示與朗讀 |
| `ModelNotice`（舊名與新名的函式） | "A → B" | 更換模型後的確認提示 |
| `EffortNotice`（舊層級與新層級的函式） | "Effort: Medium → High" | 更換推理強度後的確認提示 |
| `ModelAdjustedNotice` | "A → B · Effort adjusted to High" | 推理強度也一併移動時的確認提示 |
| `ModelAnnouncement` | "Now using Aurora Prime 3, medium effort" | 更換模型後的禮貌朗讀 |
| `EffortAnnouncement` | "Aurora Prime 3, high effort" | 更換推理強度後的禮貌朗讀 |

### ModelMenuLabels, ModelPillLabels and ModelSummaryLabels

| 屬性 | 預設值 | 顯示位置 |
|---|---|---|
| `ModelMenuLabels.Menu` | "Models" | 選單的無障礙名稱 |
| `ModelMenuLabels.Featured` | "Featured models" | 精選群組的無障礙名稱 |
| `ModelMenuLabels.Effort` / `MoreModels` | "Effort" / "More models" | 精選清單下方的兩列 |
| `ModelMenuLabels.EffortMenu` / `MoreModelsMenu` | "Effort" / "More models" | 子選單名稱與頁面標題 |
| `ModelMenuLabels.Default` | "Default" | 模型預設層級之後 |
| `ModelMenuLabels.BackTo` | "Back to featured models, Effort" | 返回列的無障礙名稱 |
| `ModelMenuLabels.Empty` | "No models available" | 空選單中唯一的一行 |
| `ModelPillLabels.AutomationName` | "Model: Aurora Prime 3, medium effort. Change model" | 膠囊的無障礙名稱 |
| `ModelSummaryLabels.Region` | "Selected model" | 概覽的無障礙名稱 |
| `ModelSummaryLabels.Context` / `Input` / `Output` / `Effort` | "Context" / "Input" / "Output" / "Effort" | 事實標籤 |
| `ModelSummaryLabels.NotAdjustable` | "Fixed" | 沒有層級之模型的推理強度事實 |
| `ModelSummaryLabels.ContextValue` | "800K tokens" | 上下文事實 |

## 無障礙

- 膠囊是一個按鈕，名稱由模型與推理強度組成，例如 "Model: Aurora Prime 3, medium effort. Change model"，並回報選單是否開啟。
- 選單是一個 menu。精選模型與推理強度層級是帶有勾選狀態的單選項；推理強度列與更多模型列會回報它們會開啟子選單；提供者群組以提供者命名。
- 選擇之後，一個禮貌的即時區域會朗讀 "Now using Aurora Swift 3, medium effort"（推理強度變更時則為對應的句子）。螢幕閱讀器聽到的不是畫面上的確認提示。
- 選單關閉時焦點返回膠囊。以鍵盤關閉後，膠囊會顯示焦點環；以指標關閉後則不顯示。
- 概覽是一個有標籤的區域，每項事實都讀成一個片語，例如 "Context: 800K tokens"。
- 標記僅供裝飾。
- 減少動態效果會保留每一種狀態與每一則朗讀。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 膠囊上的 `Enter`、`Space` 或 `ArrowDown` | 開啟選單，並醒目提示所選模型 |
| 選單中的 `ArrowUp` / `ArrowDown` | 在列之間移動；到達兩端時循環 |
| `Home` / `End` | 第一列或最後一列 |
| 字母 | 跳到下一個名稱以該字母開頭的列；重複按則循環 |
| 模型或層級上的 `Enter` 或 `Space` | 選擇它，關閉選單，並把焦點返回膠囊 |
| 推理強度列或更多模型列上的 `Enter`、`Space` 或 `ArrowRight` | 開啟子選單，並醒目提示所選項目 |
| `ArrowLeft` | 關閉子選單（或返回上一頁），並回到其列 |
| `Escape` | 關閉整個選單，並把焦點返回膠囊 |
| `Tab` | 在選單內沒有作用 |

## 動態效果

- 選單從膠囊長出來：起始時朝膠囊方向偏移 6 px、縮放為 0.97，在 160 ms 內淡入，並以 Arc 的彈簧曲線在 580 ms 內定位。離開時以 130 ms 完成，移動距離為一半，縮放為 0.985。在開啟過程中關閉，或在關閉過程中開啟，都會從表面目前的位置接續。
- 一個醒目提示會在各列之間移動：指標操作時以敏捷的彈簧滑動，鍵盤操作時直接跳動；它出現的位置會在 80 ms 內淡入，並在 80 ms 內淡出。
- 子選單的進入與離開方式相同，從其列的那一側出現。移到另一列會立即關閉已開啟的子選單；朝向子選單的斜向移動則不會。
- 在窄視窗中，新頁面會從移動方向的那一側滑入 12 px，同時在 160 ms 內淡入，選單的高度會以平滑彈簧跟隨頁面。
- 做出選擇後，膠囊的標記與文字以 100 與 160 ms 交叉淡入淡出，其寬度以快速彈簧跟隨新文字。概覽會交叉淡入淡出其說明，以及有變動的事實。確認提示會淡入，停留 2.6 秒後淡出。
- 減少動態效果會移除移動與彈簧，並保留 120 ms 的淡入淡出。關閉動態效果時，所有變化立即完成。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選單開啟或關閉 | 從膠囊長出；160 ms 淡入，580 ms 彈簧 | 120 ms 淡入 | 立即完成 |
| 醒目提示 | 滑動，並在 80 ms 內淡入與淡出 | 跳動，120 ms 淡入 | 立即完成 |
| 窄視窗中的頁面 | 滑入 12 px 並淡入；高度以彈簧變化 | 120 ms 淡入 | 立即完成 |
| 膠囊變更模型 | 標記與文字交叉淡入淡出；寬度以彈簧變化 | 120 ms 交叉淡入淡出 | 立即完成 |
| 概覽變更 | 說明與有變動的事實交叉淡入淡出 | 120 ms 交叉淡入淡出 | 立即完成 |
| 確認提示 | 淡入，停留 2.6 秒，淡出 | 120 ms 淡入淡出 | 立即完成 |

## 相關元件

- `Select` 與 `Combobox`：用於從非模型的清單中選擇一項。
- `DropdownMenu`：用於動作選單。
- `SegmentedControl`：用於需要持續可見的短選項。

| 元件 | 適用情境 |
|---|---|
| `Select` / `Combobox` | 清單很長、需要搜尋，或與模型無關 |
| `DropdownMenu` | 選單列出的是動作，而非保持選取的選擇 |
| `SegmentedControl` | 需要同時看見少數幾個選項 |
| `Popover` | 小型表單或面板從按鈕開啟 |
