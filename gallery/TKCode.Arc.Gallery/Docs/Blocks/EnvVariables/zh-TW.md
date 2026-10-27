---
title: Env variables
summary: 專案的環境變數：以每個環境一欄開關的矩陣呈現，支援遮罩值、就地編輯、匯入 .env、指紋比對，以及重新部署橫幅。
source-hash: ec25f6b52de3
---

<Demo name="Preview" />

## 何時使用

`EnvVariables` 是團隊保存專案設定與機密值的設定頁面。每一列是一個值以及它所適用的環境，因此同一個鍵可以在開發環境與預備環境中有一個值，在正式環境中有另一個值。環境是一欄欄開關：撥動開關會把該列移入或移出該環境；若某個開關會讓同一個鍵在同一環境裡有兩個值，則會被拒絕並給出一則簡短提示。值在有人顯示之前一直隱藏，被標記為敏感的值可以被取代，但永遠不會再次顯示。

每一次變更都會標記所在的列，並開啟一個橫幅，統計變更數量，並指明哪些環境仍在執行舊值。按下 Redeploy 會呼叫你的部署；橫幅會確認部署完成，或變紅、保留這些變更並提供重試。

- **專案或服務設定。** 保存應用程式、背景工作或網站的變數，每個環境一欄。
- **專案接入。** 貼上現有的 `.env` 檔案，在任何內容改變之前，逐行查看將新增、取代或略過什麼。
- **發布檢查。** 透過指紋比對預備與正式環境，找出某一方缺少的鍵或持有不同值的鍵，並把缺少的值複製過去。

## 何時不用

- **編輯單一設定檔。** 當使用者處理的是檔案本身，且註解與順序很重要時，請使用 `CodeBlock` 或文字編輯器。
- **成千上萬個鍵。** 列不做虛擬化，而且每一列都有動畫；本區塊適合幾十個鍵，而不是整個機密儲存庫。
- **絕不能傳送到用戶端的機密值。** 區塊只顯示你提供給它的內容。對敏感列請將 `Value` 留空，改為提供 `Fingerprint`，並且只在 `ValueRevealed` 中載入可讀取的值。
- **一組互不相關的設定。** 對於與環境無關的開關與選項，請使用由 `Switch` 與 `Select` 組成的設定表單。

## 用法

### 環境與列

`Environments` 依序列出各欄；每個 `EnvEnvironment` 有供列參照的 `Key` 與供使用者閱讀的 `Name`。若未提供，區塊會使用 Development、Preview 與 Production。每個 `EnvVariable` 有 `Key`、`Value`，以及它所適用的 `Environments` 的鍵；加上 `UpdatedAt` 與 `UpdatedBy` 可以填入該列的第二行文字。列可以依任意順序提供：區塊會依鍵排序（依序數比較：大寫字母在小寫字母之前，底線在字母之後），鍵相同時依其第一個環境排序。

```xml
<arc:EnvVariables ProjectName="lumen-console" SampleDotenv="FEATURE_FLAGS_URL=https://flags.lumen.example">
    <arc:EnvVariables.Environments>
        <arc:EnvEnvironment Key="development" Name="Development" />
        <arc:EnvEnvironment Key="staging" Name="Staging" />
        <arc:EnvEnvironment Key="production" Name="Production" />
    </arc:EnvVariables.Environments>
</arc:EnvVariables>
```

```csharp
block.Variables.Add(new EnvVariable("CACHE_TTL_SECONDS", "30", "development", "staging"));
block.Variables.Add(new EnvVariable("CACHE_TTL_SECONDS", "300", "production"));
block.Variables.Add(new EnvVariable("PRIMARY_DB_DSN", null, "production") { IsSensitive = true, Fingerprint = "5e0c2a" });
```

對於你自己擁有的集合，請使用 `VariablesSource`；`ObservableCollection` 會被追蹤，列的屬性與環境清單的變動也會被追蹤。當使用者新增、匯入或移除列時，區塊會就地編輯該集合。

### 值、敏感值與指紋

在有人按下眼睛圖示之前，值不會出現在螢幕與無障礙樹中；它在那一刻才被讀取，隱藏時再次丟棄。`ValueRevealed` 會在顯示之前觸發（在這裡填入 `Value` 即可依需求載入），並在隱藏之後再觸發一次。敏感列顯示的是鎖頭與 "Sensitive"，而不是眼睛與複製按鈕，它的編輯器一開始是空的：新值會取代舊值，只有移除該列再重新新增，它才會變為可讀取。

比對檢視顯示的是六位指紋，而不是每個值：預設取值的 SHA-256 的前六位十六進位數字，在視覺樹之外計算。相同的值得到相同的指紋。對於你沒有載入的值，請自行設定 `Fingerprint`。

### 新增與匯入

Add variable 會開啟單一變數的表單：鍵、值、環境（預設選取除最後一個之外的所有環境；設定 `DefaultAddEnvironments` 可選擇其他環境）以及敏感選項。錯誤在第一次嘗試後出現，並隨使用者輸入而更新：鍵為空、鍵以數字開頭或包含字母、數字與底線以外的字元、所選環境中已設定該鍵、值為空，或沒有選擇環境。

Import .env 會開啟附有 `SampleDotenv` 的貼上表單。在 Key 欄位中貼上多行文字也會切換到該表單。文字方塊下方的預覽會把每一行鍵標記為新增、取代現有值或略過（在後面的行中再次設定、鍵不合法、缺少等號）；註解、空行、`export` 與引號都會被處理。匯入時會在被取代的列上關閉所選環境，移除不再有任何環境的列，並為每一行新增一列。

<Demo name="ImportDotenv" />

### 比對環境

比對檢視把兩個環境並排放置（`CompareLeft` 與 `CompareRight`，雙向；此處為預備與正式）。某一方缺少的鍵會有一個 Copy to 按鈕，它會把該環境加到持有該值的列上，敏感標記也一併帶上。`ShowMatching`（Show identical 開關）會同時列出相同的鍵。搜尋會同時篩選兩個檢視。

<Demo name="Compare" />

### 重新部署

`RedeployAction` 接收一個 `EnvRedeployRequest`（環境與變更數量）和一個取消權杖。執行期間按鈕會轉圈；這期間所做的變更會等待下一次部署。擲出例外即表示失敗：橫幅會顯示訊息，保留所有變更，並提供 Retry。沒有設定動作時，區塊會等待約 1.7 秒，作為示範。在 `RedeployRequested` 中設定 `Cancel` 可以先行阻止，也可以在程式碼中呼叫 `RedeployAsync` 與 `CancelRedeploy`。

```csharp
// Your own call to the hosting service; here it only waits.
static Task StartDeployment(IReadOnlyList<string> environments, CancellationToken cancellation) =>
    Task.Delay(1500, cancellation);

block.RedeployAction = (request, cancellation) => StartDeployment(request.Environments, cancellation);
```

<Demo name="FailingRedeploy" />

### 窄版容器

區塊讀取的是它自身的寬度。低於 760 時，每一列堆疊顯示：先是鍵與編輯按鈕，然後是值，再是每個環境一個附標籤的膠囊，每行最多放得下幾個就放幾個（每個至少 120）；比對表格變為每個鍵一張卡片。低於 560 時，左右內縮收緊，頁首按鈕平分寬度，搜尋與檢視開關各佔一行。

<Demo name="Compact" />

### 事件與繫結狀態

`ViewMode`、`SearchText`、`CompareLeft`、`CompareRight` 與 `ShowMatching` 是雙向的。`VariablesChanged` 跟隨每一次變更，附帶變更原因與所有列；具體的事件（`VariableAdded`、`VariableSaved`、`VariableRemoved`、`VariablesImported`、`ScopeToggled`、`ScopeConflict`、`ValueCopied`）則攜帶發生了什麼變化。`PendingChangeCount`、`PendingEnvironments` 與 `RedeployState` 描述橫幅。`IsReadOnly` 讓區塊僅限讀取與比對，`IsFramed="False"` 讓它平鋪在頁面上。

<Demo name="EventLog" />

### 文字

每一個詞都在 `Labels`（`EnvVariablesLabels`）中，格式是函式，因此語序可以調整。

```csharp
block.Labels = new EnvVariablesLabels
{
    Title = "Variables d’environnement",
    AddButton = "Ajouter",
    ScopeName = (env, key) => $"{key} en {env}",
};
```

## API 參考

<!-- api: EnvVariables -->

<!-- api: EnvVariable -->

<!-- api: EnvEnvironment -->

<!-- api: EnvRedeployRequest -->

<!-- api: EnvVariablesChangedEventArgs -->

## 無障礙

鍵盤：

- Tab 依序經過 Import .env、Add variable、橫幅的按鈕、搜尋欄、檢視開關，然後是每一列：眼睛圖示（敏感列沒有；值顯示時，複製按鈕在它之前）、每個環境一個開關，以及編輯按鈕。
- 空白鍵或 Enter 撥動開關、顯示或隱藏值，以及開啟或關閉編輯器。
- 檢視開關與新增面板的表單開關用方向鍵移動並選擇。
- 在編輯器與新增面板中，Enter 儲存，Escape 關閉；焦點回到開啟它們的按鈕。
- 在搜尋欄中按 Escape 會清除搜尋。

螢幕閱讀器：

- 每個開關是一個名為 "<Environment> for <KEY>" 的切換按鈕，位於名為 "Environments for <KEY>" 的群組中；欄標題不會被朗讀。眼睛圖示是 "Reveal <KEY>"，複製按鈕是 "Copy <KEY>"，編輯按鈕是 "Edit <KEY>"。
- 隱藏的值讀作 "Hidden value"，敏感值讀作 "Write-only value"；圓點不會被朗讀。比對儲存格會讀出環境、"Fingerprint" 與數字。
- 開關被拒絕的提示是一個強烈的即時區域；變更以禮貌方式朗讀："KEY now on in Staging"、"KEY updated"、"KEY created in Development and Staging"、"KEY deleted from Production"、"Imported 3 variables into …"、"KEY now set in Staging"、"Deployed Production"，以及附有訊息的部署失敗。顯示值與搜尋結果不會朗讀。
- 焦點永遠不會回落到視窗：儲存後回到編輯按鈕，新增或匯入後回到開啟面板的按鈕，移除後移到下一列的編輯按鈕，Copy 之後移到下一個 Copy to 按鈕（或 Show identical），Clear search 之後移到搜尋方塊，橫幅關閉後移到搜尋方塊。

## 動態效果

- **展開與收合**：橫幅、新增面板、編輯器、衝突提示與新的列沿平滑的臨界阻尼彈簧長出（約 700 ms），透明度沿同一條曲線變化；它們的關閉，以及被移除或被篩掉的列的摺疊，使用加速曲線，歷時 180 ms。在單一表單與 .env 表單之間切換是即時的。
- **儲存、新增與匯入**：該列以強調色底色發光 1.6 秒（先保持，再淡去）。
- **開關**：方框在 120 ms 內填滿，並在勾號繪製的同時，以一條快速彈簧從 60% 放大。
- **值**：圓點向上淡出，同時值升起；複製按鈕彈出。
- **橫幅**：數量與文字行以短暫的退出與上升的進入相互替換；成功時圖示彈成勾號，底色漸變為綠色，橫幅在 2.8 秒後關閉。衝突提示在 3.8 秒後關閉。
- **檢視**：舊檢視在 120 ms 內淡出，接著新檢視上升 6 px 並在 240 ms 內淡入，同時高度以彈簧適應。
- 減少動態效果與關閉動態效果會立即套用每一次變更，不帶發光。

## 相關

- API keys、Webhooks 與 Integrations，其他幾個開發者區塊。
- `SearchField`、`SegmentedControl` 與 `Switch`，用於工具列與比對控制項。
- `CopyButton`，用於把單一值複製到別處。
- `Alert`，用於不與變更清單綁定的橫幅。
