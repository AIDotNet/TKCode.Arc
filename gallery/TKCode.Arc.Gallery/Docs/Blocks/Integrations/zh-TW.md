---
title: Integrations
summary: 可連結應用程式的目錄：附有搜尋、顯示數量的分類篩選、會轉圈並連結或提供重試的連結膠囊，以及用於權限、同步頻率與長按中斷連結的抽屜。
source-hash: b24aed8fc1e9
---

<Demo name="Preview" />

## 何時使用

`Integrations` 是工作區串接其所依賴服務的設定頁面。每個應用程式佔一列，顯示它的標誌、名稱、分類與一行說明，右側的膠囊說明它目前所處的狀態。按下 **Connect** 會執行你的授權流程，同時膠囊轉圈；成功後膠囊變為綠色的 **Connected**，旁邊滑入一個 **Settings** 按鈕。點擊名稱或設定按鈕會開啟一個抽屜，裡面是應用程式被允許做的事、同步的頻率，以及一個按住即可中斷連結的按鈕。

- **應用程式市集或附加元件頁面。** 輸入時搜尋會即時縮小清單，篩選項顯示每個分類有多少應用程式，而且應用程式連結後順序不會改變，所以使用者總能在原來的位置找到它。
- **工作區或專案設定。** Connected 篩選顯示目前已串接的內容，抽屜則回答「這個應用程式能看到什麼」，無論串接之前或之後。
- **新手引導。** 一份簡短的推薦服務清單，每個服務只需點擊一次即可串接。

## 何時不用

- **數百個應用程式。** 列是在清單變動時會滑動的動態元素；本區塊適用於幾十個應用程式。大型市集請使用分頁或虛擬化的目錄。
- **只有一個服務。** 一個附有自己狀態行的連結按鈕，比只有一項的目錄更清楚。
- **需要整頁的設定。** 抽屜裡只有權限、一個頻率選單與中斷連結按鈕。當應用程式需要欄位對應、欄位選擇或多個步驟時，請連結到設定頁面。

## 用法

### 應用程式、分類與頻率

每個 `IntegrationItem` 是一列。`Name` 是它在各處的標題，`Category` 是某個 `IntegrationCategory` 的鍵（用於它的篩選，也是名稱後面的那個詞），`Description` 是其下方的一行文字，同時也是抽屜的說明。`Scopes` 列出應用程式被允許做的事，每項的 `Kind` 決定它的圖示（read、write、sync、admin）。`SyncOptions` 填入抽屜中的選單；留空則不顯示該選單。

```xml
<arc:Integrations Title="Connected apps" Subtitle="Link the services your workspace relies on.">
    <arc:Integrations.Categories>
        <arc:IntegrationCategory Key="files" Label="Files" />
        <arc:IntegrationCategory Key="insights" Label="Insights" />
    </arc:Integrations.Categories>
    <arc:Integrations.SyncOptions>
        <arc:IntegrationSyncOption Key="realtime" Label="Real-time" Hint="Updates land within seconds." Phrase="in real time" />
        <arc:IntegrationSyncOption Key="nightly" Label="Nightly" Hint="Runs every night at 03:00." Phrase="nightly" />
    </arc:Integrations.SyncOptions>
    <arc:IntegrationItem Key="vaultline" Name="Vaultline" Category="files"
                         Description="Mirror design files into the matching workspace board.">
        <arc:IntegrationScope Kind="Read" Text="Browse folders you share with the workspace" />
        <arc:IntegrationScope Kind="Write" Text="Add file links to boards and tasks" />
    </arc:IntegrationItem>
</arc:Integrations>
```

當應用程式來自你自己的清單時，請繫結 `ItemsSource`，而不是填入 `Items`；區塊會跟隨它的變動。`SelectedFilter`（`all`、`connected` 或某個分類的鍵）與 `SearchText` 都是雙向的，因此頁面可以直接開啟到某個篩選，或把搜尋詞保存在網址中。

### 標誌

`Logo` 接受圖片、`Geometry`（以 `MarkBrush` 填色）或任何控制項，以 20 乘 20 的大小繪製，不帶底板。沒有 Logo 時，區塊會繪製一個產生的標誌：名稱的第一個字母（或 `Monogram`），放在一塊圓角方塊上，底色取自圖表系列色之一，由鍵決定，因此每次執行都保持一致。區塊不附帶任何第三方 Logo。

### 連結

`ConnectAction` 是你的授權流程。它會接收應用程式與嘗試次數，以及一個權杖，當呼叫 `CancelConnect` 或區塊離開視窗時，該權杖會被取消。回傳 `IntegrationConnectResult.Connected` 並附上連結者（null 表示正在使用區塊的人："Linked by you just now"），或回傳附有原因的 `IntegrationConnectResult.Failed`；擲出例外同樣視為失敗，`ErrorFormatter` 會把例外轉換為抽屜中的那行文字。失敗的應用程式會在紅色底色上顯示 **Retry**；按下它會以下一個嘗試次數再次執行該動作。

<Demo name="Failing" />

```csharp
// authorize and revoke are your own calls to the service (here: delegates you provide).
Func<string, CancellationToken, Task<bool>> authorize = (key, token) => Task.FromResult(true);
Func<string, CancellationToken, Task> revoke = (key, token) => Task.CompletedTask;

directory.ConnectAction = async (request, cancellation) =>
{
    var approved = await authorize(request.Item.Key, cancellation);
    return approved
        ? IntegrationConnectResult.Connected()
        : IntegrationConnectResult.Failed($"{request.Item.Name} did not grant access. Try again.");
};
directory.DisconnectAction = (item, cancellation) => revoke(item.Key, cancellation);
```

沒有 `ConnectAction` 時，區塊會等待 `SimulatedConnectDuration`（1.4 秒）然後成功，本頁的示範就是這樣。`ConnectRequested` 可以在連結開始前取消它，`StatusChanged` 回報閒置、連結中、已連結與失敗之間的每一次轉換，`ConnectCommand` / `DisconnectCommand` 以該應用程式為參數執行。

### 窄版容器

版面依循區塊自身的寬度，而不是視窗的寬度。低於 720 時，搜尋欄落到標題下方；到 520 時，左右內距收緊；在 480 以下（含），篩選項可橫向捲動，列把膠囊放在兩行說明旁邊，隱藏分類與設定按鈕，點擊名稱即可開啟抽屜。

<Demo name="Compact" />

### 僅分類

設定 `ShowConnectedFilter="False"` 即可只提供 All 與各個分類；當你的應用程式沒有同步排程時，請將 `SyncOptions` 留空：抽屜隨後只顯示權限與中斷連結按鈕。

<Demo name="CategoriesOnly" />

### 事件與繫結狀態

`FilterChanged` 與 `SearchChanged` 回報新的可見數量，`SyncFrequencyChanged` 回報舊選項與新選項的鍵，`ConfigurationOpened` / `ConfigurationClosed` 回報抽屜所對應的應用程式。`ConnectedCount`、`VisibleCount` 與 `OpenItem` 可以繫結。`IsFramed="False"` 讓區塊平鋪在頁面上。每一個詞都在 `Labels`（`IntegrationLabels`）中，包括螢幕閱讀器的朗讀文字。

<Demo name="EventLog" />

## API 參考

<!-- api: Integrations -->

<!-- api: IntegrationItem -->

<!-- api: IntegrationCategory -->

<!-- api: IntegrationScope -->

<!-- api: IntegrationSyncOption -->

<!-- api: IntegrationStatusChangedEventArgs -->

<!-- api: IntegrationConnectRequestedEventArgs -->

<!-- api: IntegrationFilterChangedEventArgs -->

<!-- api: IntegrationSearchChangedEventArgs -->

<!-- api: IntegrationSyncFrequencyChangedEventArgs -->

## 無障礙

- 篩選項是名稱為 "{filter}, {count}" 的選項按鈕；清單、搜尋欄，以及每個膠囊、名稱與按鈕都有無障礙名稱（"Connect Pulsegrid"、"Connecting Pulsegrid"、"Settings for Chirp"），名稱的說明文字就是描述。
- 已連結的膠囊是一個狀態標籤：無法取得焦點，名稱為 "{app} connected"。
- 一個禮貌的即時區域會朗讀正在連結、已連結、失敗與已中斷連結的應用程式、頻率變更，以及篩選或搜尋變更之後的應用程式數量。
- 狀態從不只依賴顏色：每個膠囊都有自己的圖示與文字。

### 鍵盤

| 按鍵 | 動作 |
| --- | --- |
| Tab | 依序經過搜尋欄、已選取的篩選項，然後是每一列：先是它的名稱，再是它的膠囊（閒置、連結中或失敗）或設定按鈕（已連結）。每個動作一個停靠點。 |
| Left / Up、Right / Down | 在篩選項中：選取上一個或下一個篩選項，到兩端時循環。 |
| Home / End | 在篩選項中：選取第一個或最後一個篩選項。 |
| Enter / Space | 按下取得焦點的膠囊、名稱或按鈕。 |
| Escape | 關閉抽屜。 |

連結之後，焦點從膠囊移到新的設定按鈕（設定按鈕被隱藏時則移到名稱）；在抽屜中連結之後，焦點移到同步頻率選單；中斷連結之後，焦點落在抽屜的 Connect 膠囊上。當抽屜關閉而開啟它的那一列已不在清單中時，焦點移到標題。持有焦點的列離開時，會把焦點交給下一列。

## 動態效果

- 搜尋與篩選會讓清單重新排版：離開的列在原位淡出並模糊到 4px（160ms），其餘的列沿同一條彈簧滑到新位置，新的列從模糊中淡入（240ms），清單高度隨之變化。
- 篩選醒目標示沿同一條彈簧在膠囊之間滑動；數量變動時向上或向下滾動。
- 膠囊的標籤以模糊方式交替替換，寬度以彈簧跟隨；勾號自行繪製；設定按鈕在連結後 140ms 滑入，中斷連結後淡出。抽屜中的設定區域以高度彈簧展開與收合。
- 減少動態效果會保留 120ms 的淡入淡出，去掉位移、模糊與彈簧（離開的列會立即移除）；關閉動態效果則直接跳變。

## 相關

- `Drawer`，設定所在的面板。
- `HoldToConfirm`，用於中斷連結按鈕。
- `SearchField` 與 `Select`，用於搜尋與頻率選單。
- `EmptyState`，用於因其他原因而為空的清單。
