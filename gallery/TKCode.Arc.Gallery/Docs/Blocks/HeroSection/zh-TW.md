---
title: 首屏區塊
summary: 三個全螢幕的 SaaS 首屏：從底邊升起、覆蓋在飄動漸層之上的即時儀表板，逐節點傳遞範例事件的工作流程圖，以及網格漸層上的大字排版。
source-hash: 99d69ed6ec96
---

<Demo name="Preview" />

## 何時使用

首屏是登陸頁的第一個畫面，只有片刻時間說明產品是做什麼的。`HeroSection` 為這一刻提供三種完整設計，以及一種用來放你自己文字的簡單版面。

- **產品畫面本身就是賣點**：`Centered` 把標題放在緩慢流動的柔和漸層上，下方是一個從底邊升起的儀表板。裡面的時間範圍切換確實可用。
- **展示產品運作就是賣點**：`Split` 把文案放在一個自動執行範例事件的工作流程旁邊，適合自動化、開發者工具和基礎設施產品。
- **一句有力的話**：`Minimal` 是滿版網格漸層上的大字，適合發表頁或下載頁。
- **以上都不合適**：設定 `Title` 後，首屏會在所選版面中顯示你自己的公告、標題、操作、安裝指令、要點和配圖。

## 何時不用

- **應用程式內部的頁面標題**：請使用 `PageHeader`。首屏用於產品的對外頁面，而不是產品內的畫面。
- **目標是收集電子郵件地址**：請使用 `NewsletterSignup`，它會就地驗證並確認。
- **頁面的結尾**：請使用 `CtaSection`。首屏開啟一段內容，行動呼籲負責收尾。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:HeroSection Variant="Split" />
</UserControl>
```

`HeroSection` 衍生自 `TemplatedControl`。`Variant` 選擇設計。每種設計都會填滿它得到的高度；放在捲動容器裡時則佔滿一個檢視區，因此在頁面中佔滿一屏，在預覽中貼合外框。範例文案和資料屬於虛構產品，上線前請替換，或設定 `Title`。

### 設計

| `Variant` | 顯示內容 | 適合 |
|---|---|---|
| `Centered` | 飄動漸層上的標題，以及從傾斜狀態落定、在底邊淡出的儀表板 | 畫面有說服力的產品 |
| `Split` | 文案旁邊是點陣畫布上的工作流程圖 | 自動化、開發者工具 |
| `Minimal` | 帶顆粒的網格漸層上的大字，底邊排列客戶標誌 | 發表、下載 |

<Demo name="Editorial" />

### 操作

`PrimaryAction` 和 `SecondaryAction` 接受 `HeroAction`，它在 `CtaAction` 的基礎上增加了 `IsExternal`。未設定時顯示該設計自帶的操作。按下時先引發 `ActionInvoked` 並執行 `Command`，然後：

| 操作具有 | 按下時還會 |
|---|---|
| `Href` | 開啟該位址，除非處理常式已將事件標記為已處理。只會開啟絕對的 http、https 和 mailto 位址 |
| `ConfirmedLabel` 且沒有 `Href` | 顯示勾選標記和該標籤 2.4 秒 |
| 兩者都沒有 | 不再做其他事 |

主要操作末尾有一個箭頭，在滑鼠或觸控筆懸停時前傾 2 px。`IsExternal` 會改為顯示向外的箭頭。

```csharp
hero.PrimaryAction = new HeroAction { Label = "Start free trial", Command = viewModel.StartTrial };
hero.SecondaryAction = new HeroAction { Label = "Pricing", Href = "https://example.com/pricing", IsExternal = true };
hero.ActionInvoked += (_, e) => analytics.Track("hero", e.Action.Label);
```

### 工作流程

`Split` 在區塊內部繪製工作流程圖，不依賴其他套件。每個事件都走相同的步驟：觸發、查詢、分支，然後是三個動作。每完成一步，資料就沿連線繪製過去，每一步都會回報耗時。金額較小的付款走「否」分支，因此有兩個動作被略過，它們的連線變為虛線。**Send test event** 會立即執行下一個範例。離開螢幕時執行暫停，回來後從停下的地方繼續。

<Demo name="Workflow" />

### 你自己的內容

設定 `Title` 後，首屏按自然高度顯示你的內容。此時 `Variant` 表示版面：`Centered` 讓文案置中，`Minimal` 讓文案靠左，`Split` 還會在旁邊顯示 `Media`。其他屬性都是選用的：`Description`、`Announcement`、`InstallCommand`（附複製按鈕）和 `Meta`（小字要點）。操作只有在你設定後才會顯示。

<Demo name="OwnContent" />

```xml
<arc:HeroSection Variant="Split" Title="Ship your docs today" InstallCommand="npm create quickdocs">
    <arc:HeroSection.Media>
        <Border Height="320" Background="{arc:ArcDesignTokenResource SurfaceMuted}" />
    </arc:HeroSection.Media>
</arc:HeroSection>
```

### 最佳實務

**建議**

- 用幾個詞在標題裡說清產品做什麼。標題會平衡各行，並在 12 到 16 個字元處換行。
- 給主要操作設定真實的命令或位址。
- 把範例產品、數字和客戶換成你自己的。

**避免**

- 過長的描述。兩句話就夠了。
- 一個頁面放兩個首屏。每種設計都佔滿一屏，並各自執行動態背景。

## API 參考

<!-- api: HeroSection -->

<!-- api: HeroAction -->

## 無障礙

- 標題是一級標題；設定 `Title` 時，區塊以它命名。
- 每個操作、儀表板內的切換器和複製按鈕都有無障礙名稱。
- 網格漸層、點陣、光暈和客戶標誌都是裝飾。儀表板和工作流程圖被命名為範例內容，每個節點的狀態都有名稱：Waiting、Running、Done 或 Skipped。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在各操作之間移動，然後進入設計內部的控制項 |
| `Enter` / `Space` | 執行操作或傳送測試事件 |

## 動效

- 進場動畫只播放一次：各項從 2 px 模糊中上升 12 px，用時 640 ms，間隔 80 ms。將 `IsEntranceAnimated` 設為 false 可略過，呼叫 `ReplayEntrance` 可重新播放。
- 儀表板在 1.1 秒內上升 56 px，同時在 1.5 秒內從 22 度傾斜和 94% 縮放落定。
- 網格漸層在螢幕內時以每秒 30 格、20 秒一輪的節奏飄動。
- 工作流程節點的狀態以 snappy 彈簧彈出，填色在 240 ms 內變化，每條連線按其步驟的時長繪製。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 進場 | 上升、模糊並傾斜 | 只淡入 | 立即顯示 |
| 網格漸層 | 飄動 | 一格靜止畫面 | 一格靜止畫面 |
| 工作流程 | 逐個事件執行 | 每個事件直接顯示完成狀態 | 每個事件直接顯示完成狀態 |

## 響應式

- `HeroSection` 跟隨自身寬度，而不是視窗寬度。標題隨寬度縮放。
- `Centered`：可用寬度不足 720 px 時，儀表板切換到 600 px 設計，並在兩側均勻裁切，而不是縮小到 60% 以下。寬度不超過 560 px 時，側邊留白消失，操作佔滿一列。
- `Split`：寬度不超過 900 px 時，工作流程圖移到文案下方。低於 520 px 時使用緊湊版面並隱藏執行摘要。
- `Minimal`：寬度不超過 800 px 時，描述和操作上下堆疊，客戶標誌折為四欄，然後是三欄。
- 觸控：箭頭只在滑鼠或觸控筆下前傾，因此觸控操作從不依賴懸停。

<Demo name="Narrow" />

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `HeroSection` | 登陸頁的第一個畫面 |
| `CtaSection` | 在頁面末尾請求試用或示範 |
| `NewsletterSignup` | 操作是填寫電子郵件地址 |
| `StatsBand` | 在首屏下方用數字支撐你的說法 |
