---
title: Upgrade prompt
summary: 在使用者用完額度的那一刻出現的升級提示：用量條、可在月繳與年繳之間切換並滾動變化的價格、三項具體的解鎖內容和成功狀態，卡片尺寸始終不變。
source-hash: 4c42633c3bda
---

<Demo name="Preview" />

## 何時使用

`UpgradePrompt` 是 AI 產品在使用者用完額度的那一刻顯示的卡片：它說明用掉了什麼，顯示已達上限的用量條，提供下一級方案的價格和解鎖內容，
並讓使用者立即升級或稍後再說。升級後卡片滾入成功狀態，列出新的額度，用量條也隨之落到新的上限。

- **AI 產品的用量上限**：點數、生成次數或席次用完，某個操作被擋下。把提示放在錨定於被擋下控制項的對話框或彈出層中，或直接取代被擋下的結果。
- **付費牆時刻**：只有一級下一階方案，附月繳和年繳價格，年繳省下的金額是賣點的一部分。
- **重視轉換但保持克制的提示**：Maybe later 之後顯示一則安靜的提醒並留有返回入口，而不是再推銷一次。

## 何時不用

- **並排比較多個方案**：使用價目表或方案選擇器。這個提示只提供一級方案。
- **不改方案、只購買預付點數**：使用點數儲值卡片。
- **在帳單頁被動呈現用量**：使用用量條或 `Progress`。這個提示是一次推動，不是報表。
- **頁面一載入就顯示**：只在某個操作真的因上限被擋下時顯示。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:UpgradePrompt Used="50" Limit="50" ResetsOn="Nov 1" />
</UserControl>
```

`UpgradePrompt` 是 Special 套件中的 `TemplatedControl`。在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊這個套件。卡片最多 440px
寬，隨自身寬度版面配置，從不隨內容變寬。

方案（`Plan`）與用量（`Used`、`Limit`、`Unit`、`ResetsOn`）由你的應用程式提供。沒有設定方案時，卡片為空：標題顯示
"No plan to offer"，沒有用量條也沒有報價，Upgrade 與 Maybe later 都無法使用；套件不附帶範例方案。預覽傳入的是展示庫自己的方案：
一個虛構產品的 Pro 方案，每月 $20 或按年繳費每月 $16，含 2,000 個 AI 點數、Claude Opus 5.5 和優先佇列。模型是真實的；方案、
價格與額度屬於這個虛構產品，並非模型廠商的價格。沒有 `UpgradeHandler` 時，Upgrade 會執行一個 1.1 秒、不收取任何費用的模擬，成功狀態也會如實說明。

### 結構

1. **標題區**：帶圖形的著色小方塊（報價時是閃電，升級後是勾號，提醒時是時鐘）、作為標題的標題文字和兩行說明。
2. **用量條**：「AI credits this month」，右側是計數（「50 / 50」），下面是膠囊形進度條，再下面是狀態列：圖示加標籤（「Limit reached」或「1,950 AI credits left」），右側是重設日期或方案的額度。
3. **價格**：方案名稱在上，下面是滾動的每月價格和「/ month」，右側是計費切換（Monthly，以及附「Save 20%」的 Yearly）。下方一個位置顯示計費說明（「$192 billed yearly」）或錯誤。
4. **解鎖內容**：「Pro unlocks」和三列內容，每列一個放在小方塊裡的標記和一行文字。
5. **底部**：Maybe later 維持自然寬度，Upgrade 填滿其餘空間。成功狀態是 Continue，提醒狀態是 See Pro again。

### 狀態

| 狀態 | 顯示 | 按鈕 |
|---|---|---|
| `Offer` | 上限、附切換的價格和解鎖內容 | Maybe later、Upgrade to Pro |
| `Processing` | 同上，切換無法使用 | Maybe later（無法使用）、附載入圈的 Upgrading… |
| `Error` | 價格下方的錯誤訊息 | Maybe later、Try again |
| `Success` | 「You're on Pro」、落到新上限的用量條和新的額度 | Continue |
| `Reminder` | 「No problem」、額度何時重設以及一則提示 | See Pro again |

所有狀態共用一個儲存格：內文面板和底部都疊放在一起並交叉淡化，因此卡片維持最高那個狀態的尺寸，按鈕也待在原位。`State` 唯讀且可繫結；卡片處於
`Error` 時，`ErrorMessage` 保存失敗的訊息。

### 你的方案和單位

`Plan` 接受一個 `UpgradePromptPlan`：名稱、月繳價格、按年繳費時的每月價格、幣別、新的上限、解鎖內容（`UpgradePromptFeature`：id、標籤，以及選用的標記和顏色）
和新的額度（`UpgradePromptLimit`：標籤、值和選用的標記）。Yearly 上的節省徽章和年繳金額都由這兩個價格算出。`Unit` 說明計數的是什麼，`Title` 和
`Description` 取代報價狀態的文字。

<Demo name="YourPlan" />

```xml
<arc:UpgradePrompt Used="3" Limit="3" Unit="seats" Interval="Monthly" Title="You've used all 3 seats">
    <arc:UpgradePrompt.Plan>
        <arc:UpgradePromptPlan Name="Team" MonthlyPrice="48" YearlyPrice="40" Currency="USD" Limit="25" />
    </arc:UpgradePrompt.Plan>
</arc:UpgradePrompt>
```

標記是 24 × 24 的描邊圖形，例如 `ArcIcons` 的成員。請使用中性圖形或你自己產品的標記；預覽在模型名稱旁使用閃光圖形，而不是任何廠商的標誌。

### 執行升級

`UpgradeHandler` 收到一個 `UpgradePromptSelection`（方案、計費週期、今天收取的金額和幣別）以及一個取消權杖。方案生效後完成工作；擲回例外則在價格下方
顯示例外訊息並提供 Try again。提示離開視覺樹時權杖會被取消，提示隨後回到報價狀態。

<Demo name="Checkout" />

```csharp
prompt.UpgradeHandler = async (selection, cancellationToken) =>
{
    var response = await http.PostAsJsonAsync("/api/billing/upgrade",
        new { plan = selection.Plan.Name, interval = selection.Interval.ToString() }, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Your card was declined. Try another card.");
    }
};
prompt.Upgraded += (_, e) => workspace.Refresh();
prompt.Dismissed += (_, _) => nudges.Snooze("upgrade");
prompt.ContinueRequested += (_, e) =>
{
    e.Handled = true;
    dialog.Close();
    RetryBlockedAction();
};
```

升級成功、成功狀態顯示之後引發 `Upgraded`。`UpgradeFailed` 帶有例外。`Dismissed` 在 Maybe later 之後引發，請在這裡記錄暫緩。`ContinueRequested` 在
Continue 之後引發：關閉提示時把 `Handled` 設為 true 可保留成功狀態；未處理時，提示重設回報價狀態。`Interval` 是雙向的，`IntervalChanged` 跟隨切換。
`UpgradeAsync()`、`Dismiss()`、`ShowOffer()` 和 `Reset()` 可以從程式碼執行同樣的步驟。

選擇中的金額只用於確認文案。請在伺服器上按方案和計費週期收費。

### 窄容器

卡片在 440px 以內隨容器伸縮。卡片寬度小於 420px 時，內距從 24 降到 20。價格和切換無法並排放下時，切換移到價格下方。解鎖內容始終維持一行，
超出部分以刪節號結尾；按鈕在任何寬度下都並排顯示。

<Demo name="Narrow" />

### 減少動態效果

減少動態效果時，進度條和價格直接跳到目標值，交叉淡化變為一般的淡入淡出。

<Demo name="ReducedMotion" />

### 文案規範

三項解鎖內容要具體，並與使用者剛才在做的事有關：更多他們剛用完的東西、更好的模型、不必等待。

| 不要寫 | 改寫為 |
|---|---|
| Unlock premium features | 2,000 AI credits every month |
| Access to advanced AI | Claude Opus 5.5 for your hardest tasks |
| Enhanced performance | Priority queue, no waiting at peak times |

`Labels` 接受一個 `UpgradePromptLabels` 物件，包含卡片顯示或朗讀的每個詞。帶值的句子是函式，因此其他語言可以調整語序：

```csharp
prompt.Labels = new UpgradePromptLabels
{
    MaybeLater = "Not now",
    TitleAtLimit = (limit, unit) => $"All {limit} {unit} used"
};
```

### 最佳做法

**建議**

- 在某個操作因上限被擋下時顯示提示，並錨定在被擋下的控制項上。
- 在 `Dismissed` 中暫緩這則提示，避免每次被擋下都再次出現。
- 處理 `ContinueRequested` 來關閉提示並重試被擋下的操作，讓升級感覺像是替使用者解除了阻礙。

**避免**

- 把示範中的價格當作真實價格呈現。預覽中的方案價格是為一個虛構產品編造的。
- 按選擇中的金額收費。請在伺服器上按方案和計費週期收費。
- 列出超過三項解鎖內容；卡片只為三項預留了空間。

### 常見問題

**為什麼 Maybe later 之後卡片沒有變小？** 所有狀態共用一個儲存格，因此卡片維持最高那個狀態的尺寸，周圍的內容都不會移動。

**用量條能顯示未達上限的用量嗎？** 能。未達上限時，標題說明還剩多少，狀態顯示附儀表圖示的「5 AI credits left」。

**升級進行中關閉提示會怎樣？** 處理常式的權杖被取消，提示回到報價狀態。

## API 參考

<!-- api: UpgradePrompt -->

<!-- api: UpgradePromptPlan -->

<!-- api: UpgradePromptFeature -->

<!-- api: UpgradePromptLimit -->

<!-- api: UpgradePromptSelection -->

<!-- api: UpgradePromptLabels -->

<!-- api: UpgradePromptIntervalChangedEventArgs -->

<!-- api: UpgradePromptUpgradedEventArgs -->

<!-- api: UpgradePromptFailedEventArgs -->

列舉：

| 型別 | 值 |
|---|---|
| `UpgradePromptInterval` | `Monthly`、`Yearly` |
| `UpgradePromptState` | `Offer`、`Processing`、`Error`、`Success`、`Reminder` |

## 無障礙

- 卡片是名為「Upgrade to Pro」的群組，標題是一個標題元素，因此放在對話框或彈出層中也容易朗讀。
- 用量條是一個進度條，名稱就是它的值（「50 of 50 AI credits used」），狀態作為說明文字。
- 狀態始終是圖示加標籤（「Limit reached」、「1,950 AI credits left」），從不只靠顏色。
- 一個禮貌的即時區域朗讀升級中、成功、錯誤和提醒。沒有 `UpgradeHandler` 時，它會說明升級是模擬的，不會收取任何費用。
- 滾動的價格對輔助技術隱藏；價格群組以純文字命名（「$16 a month, $192 billed yearly」）。
- 未啟用的面板是惰性且隱藏的：無法點擊、Tab 會略過、也不會公開，因此焦點永遠不會落在淡出的面板上。狀態變化隱藏了取得焦點的按鈕時，焦點移到新狀態的主要按鈕。
- 升級進行中 Upgrade 仍可取得焦點，因此鍵盤焦點不會遺失。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在計費切換、Maybe later 和 Upgrade 之間移動；其他狀態下是 Continue 或 See Pro again |
| `Left` / `Right` | 在計費切換上切換 Monthly 和 Yearly |
| `Enter` / `Space` | 啟用取得焦點的按鈕 |

## 動態效果

- 卡片第一次捲入檢視區時用量條填滿一次，升級後以 0.5 秒無回彈的彈簧落到新的上限。填色是一個透過平移移動並裁切在軌道內的膠囊，所以它的圓角端從不被拉長。
- 價格在月繳和年繳之間以 0.28 秒無回彈的彈簧逐位滾動，使用等寬數字，旁邊的內容不會移動。
- 標題、說明、用量條標籤、計數、狀態和計費說明交叉淡化：100ms 淡出，160ms 淡入並上升 2px。
- 內文面板和底部以同樣方式淡化並上升 4px；舊面板淡出後新面板才開始出現。
- 計費切換的滑塊以自身的彈簧滑動，選取 Yearly 後「Save 20%」徽章換上成功色調。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 進度條 | 0.5 秒彈簧 | 直接跳轉 | 直接跳轉 |
| 價格 | 以 0.28 秒彈簧滾動 | 直接跳轉 | 直接跳轉 |
| 文字 | 交叉淡化並上升 2px | 淡入淡出 | 直接取代 |
| 面板與底部 | 淡化並上升 4px | 淡入淡出 | 直接取代 |

## 相關元件

- `BillingToggle` 和 `BillingPrice`：價目頁上的計費週期切換。
- `Progress`：附標籤的單一額度。
- `ContextWindow`：模型上下文視窗裡裝了什麼。

| 元件 | 何時選擇 |
|---|---|
| `BillingToggle` | 你在做包含多個方案的價目頁 |
| `Progress` | 你只需要回報用量 |
| `ContextWindow` | 上限是模型的上下文，而不是方案 |
