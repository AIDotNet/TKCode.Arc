---
title: API keys
summary: API 金鑰管理：點擊 Create key 膠囊即可展開為表單，新金鑰只顯示一次，每一列都能展開查看用量詳情，長按即可撤銷金鑰。
source-hash: 3f74d0d55c2f
---

<Demo name="Preview" />

## 何時使用

`ApiKeys` 是 API 產品開發者設定中的金鑰頁面。頁首說明這些金鑰的用途，工具列統計有效金鑰的數量，每把金鑰是一列，顯示名稱、測試或正式標記、最後四個字元、權限範圍，以及上次使用的時間。建立與撤銷都在原地完成，使用者不必離開清單：

- **建立金鑰。** 「Create key」膠囊會展開為表單：名稱、Test 或 Live，以及權限範圍。由你的伺服器建立金鑰，之後同一個面板會把完整的金鑰顯示一次，並附上複製按鈕與警告，同時新的一列從清單頂端滑入，並在面板關閉前保持醒目標示。
- **檢查金鑰。** 點擊一列即可展開詳情：建立者、最近一次請求（時間與來源），以及過去 30 天內由它簽署的請求數量。久未使用的金鑰會帶有警告。
- **撤銷金鑰。** 詳情的末端是長按撤銷按鈕。按住 1.2 秒會在該列畫上刪除線；你的伺服器隨即撤銷該金鑰，這一列摺疊消失。

## 何時不用

- **需要反覆查看的金鑰。** 本區塊只會把金鑰顯示一次。對於需要一直可讀取的值（設定、環境變數），請改用附有顯示按鈕的遮罩輸入欄。
- **具有到期規則或大量權限的個人存取權杖。** 表單只提供名稱、兩種環境與一份扁平的權限範圍清單。若權杖需要到期日、資源選擇器或依專案區分的權限，請使用完整的表單頁面。
- **數百把金鑰。** 列不做虛擬化；本區塊適用於幾把到幾十把金鑰。搜尋與分頁應交給資料表格。
- **唯讀清單。** 如果這裡沒有人能建立或撤銷金鑰，一般清單或資料表格會更簡單。

## 用法

本區塊衍生自 Avalonia 的 `TemplatedControl`。每把金鑰是一個 `ApiKeyItem`（`AvaloniaObject`）：包含名稱、環境、用來識別它的前綴與最後四個字元（絕不包含金鑰本身）、權限範圍、建立日期與建立者、上次使用時間，以及請求數量。表單所提供的權限範圍是 `ApiKeyScope`，包含名稱、說明，以及新金鑰是否預設勾選。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ApiKeys x:Name="Keys"
                 Description="Keys let your backend call the Northwind Relay API. Store them in a vault, never in client code." />
</UserControl>
```

```csharp
Keys.Scopes.Add(new ApiKeyScope { Name = "orders:write", Description = "Create, update and cancel orders" });
Keys.Scopes.Add(new ApiKeyScope { Name = "accounts:read", Description = "Read account profiles and contacts", IsDefault = true });

var checkout = new ApiKeyItem
{
    Key = "checkout",
    Name = "Checkout service",
    Environment = ApiKeyEnvironment.Live,
    Prefix = "rk_live_",
    LastFour = "7c3e",
    LastUsedAt = DateTimeOffset.Now.AddMinutes(-4),
    CreatedAt = new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero),
    CreatedBy = "Lena Ortiz",
    LastRequestText = "4 min ago from 18.144.62.10, Frankfurt",
    RequestsLast30Days = 942_317,
};
checkout.Scopes.AddRange(["orders:write", "accounts:read"]);
Keys.Keys.Add(checkout);
```

### 組成

- **頁首**：`Title`（二級標題，預設為 "API keys"）與 `Description`，最大寬度 520。
- **工具列**：數量（"5 active keys"，數字變化時會向上或向下滾動），以及末端的 "Create key" 膠囊。整個膠囊都可以點擊，包括其中的文字。面板開啟時數量會淡出。
- **列**：整列是一個按鈕，分為四欄（名稱、標記與遮罩；權限範圍標籤；上次使用與建立日期；箭頭）。詳情在其下方展開：先是各項資訊，接著是一行說明與長按撤銷按鈕。
- **空狀態**：標題、一行文字與一個選用按鈕（`EmptyActionText`），在最後一列摺疊消失後顯示。

### 在你的伺服器上建立金鑰

將 `GenerateAction` 設為建立金鑰的呼叫。它會接收一個 `ApiKeyRequest`（去除前後空白的名稱、環境、已勾選的權限範圍，以及嘗試次數）與一個取消權杖，並回傳 `ApiKeyCreated`：新的一列與一次性的金鑰。執行期間 `PanelState` 為 `Generating`：Generate 按鈕顯示載入動畫並保持焦點，欄位被鎖定，按 Escape 會取消請求並回到表單。區塊只在顯示金鑰期間保留它，面板關閉時就會丟棄。

```csharp
Keys.GenerateAction = async (request, cancellation) =>
{
    var created = await api.CreateAsync(request.Name, request.Environment, request.Scopes, cancellation);
    var item = new ApiKeyItem
    {
        Key = created.Id,
        Name = request.Name,
        Environment = request.Environment,
        Prefix = created.Prefix,
        LastFour = created.Secret[^4..],
        CreatedAt = DateTimeOffset.Now,
        CreatedBy = currentUser.Name,
    };
    item.Scopes.AddRange(request.Scopes);
    return new ApiKeyCreated(item, created.Secret);
};
```

新的一列會插入到 `Keys` 的頂端（若 `KeysSource` 是可新增項目的清單，則插入其頂端），接著觸發 `KeyCreated` 並執行 `KeyCreatedCommand`。若未設定 `GenerateAction`，區塊會模擬伺服器：約 730 ms 後產生一把以 `SimulatedPrefixFormat` 開頭的隨機 40 字元金鑰，並記錄為由 `CurrentUserName` 建立。僅限用於原型與示範。

表單在每次開啟時都會重設：名稱為空、環境為 `DefaultEnvironment`，權限範圍為標記了 `IsDefault` 的項目。名稱為空時，會在欄位下方顯示錯誤並把焦點留在那裡；沒有選取任何權限範圍時，會在權限範圍下方顯示錯誤並聚焦第一項。使用者一輸入或勾選權限範圍，兩種錯誤隨即清除。在名稱欄位按 Enter 即可送出。

### 失敗與重試

當 `GenerateAction` 擲出例外時，表單保持開啟並保留使用者已輸入的所有內容，按鈕上方出現一行錯誤（例外訊息，或 `ApiKeyLabels.GenerateFailed`），`GenerateError` 會保存該錯誤，並觸發 `KeyCreateFailed`。再次按下 Generate 會以下一個嘗試次數重試。當 `RevokeAction` 擲出例外時，帶有刪除線的那一列會恢復原狀，長按按鈕重設，錯誤會被朗讀，並觸發 `KeyRevokeFailed`。

<Demo name="Failing" />

### 撤銷

按住按鈕 `RevokeHoldDuration`（1.2 秒，可用指標，也可按住空白鍵或 Enter）即確認撤銷。`KeyRevoking` 會先觸發，而且可以取消。接著名稱與遮罩被畫上刪除線，該列其餘部分變暗，並執行 `RevokeAction`；該列至少維持這個狀態 `RevokeExitDelay`（0.9 秒），兩者都完成後摺疊消失。金鑰會從 `Keys`（或 `KeysSource` 清單）中移除，`KeyRevoked` 連同該列先前的索引一起觸發，並執行 `KeyRevokedCommand`。`RevokeAsync` 可在程式碼中完成同樣的操作，不需長按。

### 詳情與閒置金鑰

`ExpandedKey`（雙向）是詳情已展開的那一列；同一時間只能展開一列。`LastRequestText` 會填入 "Last request" 這一項；沒有它時，區塊會寫出金鑰是多久之前使用的。`LastUsedAt` 早於 `StaleAfter`（30 天）的金鑰，會在上次使用時間之前顯示警告，其撤銷說明會寫明它已閒置幾天。`ReferenceTime` 會為這些文字固定「現在」的時間（用於測試與螢幕截圖）。

### 窄版容器

本區塊依循它自身的寬度，而不是視窗的寬度。寬度低於 700 時，權限範圍欄與建立日期會離開列（上次使用移到末端），改在詳情中顯示。低於 540 時，左右內距從 20 收緊為 16，各項資訊堆疊為單欄，撤銷按鈕移到說明下方。當展開的面板寬度不超過 560 時，表單使用單欄，提示文字獨佔按鈕上方的一行。

<Demo name="Compact" />

### 空狀態

沒有金鑰時，清單會顯示 `ApiKeyLabels.EmptyTitle` 與 `EmptyDescription`。設定 `EmptyActionText` 可加入一個按鈕：它會觸發 `EmptyActionInvoked` 並執行 `EmptyActionCommand`；若兩者都沒有處理，則開啟表單。新加入的金鑰會一起淡入展開，數量向上滾動。

<Demo name="Empty" />

### 事件與狀態

`PanelState`（`Closed`、`Form`、`Generating`、`Reveal`）與 `PanelStateChanged`（附帶面板關閉的原因：Cancel、Done、關閉開關、Escape 或程式碼）讓頁面可以追蹤面板；`IsCreateOpen`（雙向）、`OpenCreate` 與 `CloseCreate` 用來驅動它。`ActiveCount` 是清單中的金鑰數量。金鑰被複製時觸發 `SecretCopied`。

<Demo name="EventLog" />

### 文字

區塊顯示或朗讀的每一個詞都在 `Labels` 中，這是一個附有英文預設值的 `ApiKeyLabels`。包含數值的文字是函式，因此語序可以調整：

```csharp
Keys.Labels = new ApiKeyLabels
{
    CreateKey = "New key",
    CountWords = count => count == 1 ? "key in use" : "keys in use",
    Used = ago => $"Last call {ago}",
};
```

## API 參考

<!-- api: ApiKeys -->

<!-- api: ApiKeyItem -->

<!-- api: ApiKeyScope -->

<!-- api: ApiKeyRequest -->

<!-- api: ApiKeyCreated -->

<!-- api: ApiKeyLabels -->

## 無障礙

- 區塊以其標題命名，標題為二級標題。數量會作為一個片語朗讀（"5 active keys"）。
- 開關名稱為 "Create key"，面板開啟時為 "Close"。權限範圍是一個名為 "Scopes" 的群組；每個核取方塊以其權限範圍命名，並由其說明文字描述。驗證錯誤是警示訊息。
- 金鑰欄位的名稱為 "Secret key"。金鑰絕不會出現在視覺樹的其他任何位置。
- 清單名稱為 "Active keys"。每一列的按鈕會讀出名稱、環境、"Key ending in 7c3e"、權限範圍與日期，閒置金鑰在上次使用之前會讀出 "Idle"，並說明詳情是否已展開。詳情的名稱為 "<name> details"。長按按鈕的名稱為 "Hold to revoke: <name>"。
- 展開一列、複製金鑰、建立金鑰、撤銷金鑰以及撤銷失敗，都以禮貌方式朗讀。產生失敗時，錯誤行以強烈方式朗讀。
- 列的鍵盤焦點環繪製在列的內部，因此裁切不會將其遮住。

### 鍵盤

- **Tab** 依序到達 "Create key" 開關與每一列。展開列的長按按鈕緊接在該列之後。
- 在開關上按 **Enter** 或 **空白鍵** 會開啟表單並聚焦名稱。接著 **Tab** 依序經過環境開關（一個停靠點，用方向鍵選擇）、權限範圍、Cancel 與 Generate。表單不會困住焦點。
- 在名稱欄位按 **Enter** 送出。在面板內任何位置按 **Escape** 會關閉面板並把焦點還給開關；正在產生金鑰時，Escape 改為取消請求並留在表單中。
- 建立金鑰後，焦點移到 Copy 按鈕。
- 在列上按 **Enter** 或 **空白鍵** 展開或收合其詳情。
- 在長按按鈕上按住 **空白鍵** 或 **Enter** 1.2 秒後撤銷；提早放開則取消。
- 撤銷之後，焦點移到下一列，沒有則移到上一列，再沒有則移到 "Create key" 開關。

## 動態效果

- **膠囊變面板**：一條平滑彈簧讓表面向左擴展到完整寬度並向下擴展到表單；加號旋轉 45° 變成關閉叉號；表單在 140 ms 後淡入；數量淡出。關閉時，主體在 100 ms 內淡出，表面摺回，數量在 100 ms 後淡入。
- **表單**：切換環境時，表單下方的提示以模糊方式交替替換；錯誤行以平滑彈簧展開。
- **產生**：標題以模糊方式替換為 "Key created"；表單在 120 ms 內淡出，顯示區在淡入的同時上升 8 px（60 ms 後開始，歷時 320 ms）；金鑰從 8 px 的模糊中在 480 ms 內顯影。新的一列從零高度以平滑彈簧長出，80 ms 後開始 240 ms 的淡入，並保持強調色底色直到面板關閉，然後底色在 300 ms 後以 1.2 秒淡去。數量向上滾動。
- **列**：詳情以平滑彈簧展開，並在 50 ms 後以 240 ms 淡入；收合時在 160 ms 內淡出。箭頭沿 580 ms 的彈簧曲線旋轉 180°；展開時的底色在 160 ms 內變化。同一時間只展開一列。
- **撤銷**：長按進度線性填滿；確認後，一條線劃過名稱（480 ms），80 ms 後再劃過遮罩；其餘部分在 240 ms 內變暗到 40%。停頓之後該列以平滑彈簧摺疊消失，下方的列隨之上移；數量向下滾動。
- 減少動態效果時，尺寸與位置會立即就位，只保留短促的淡入淡出；長按仍然需要 1.2 秒來填滿。關閉動態效果則會立即套用每一次變化。

## 相關

- `HoldToConfirm`、`CopyButton`、`Input`、`SegmentedControl`、`Checkbox`、`Badge` 與 `Avatar`，即組成本區塊的控制項。
- Webhooks、Integrations 與 Env variables，其他幾個開發者區塊。
