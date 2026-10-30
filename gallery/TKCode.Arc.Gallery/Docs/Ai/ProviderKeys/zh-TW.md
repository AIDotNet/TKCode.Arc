---
title: Provider keys
summary: 為自備金鑰（BYOK）AI 產品設計的設定卡片：顯示每個供應商的遮罩金鑰、狀態與最近使用時間，支援顯示、複製、輪替與可復原的移除，並透過內嵌表單在儲存前測試新金鑰。
source-hash: 1ebaeb7e3937
---

<Demo name="Preview" />

## 何時使用

`ProviderKeys` 是讓使用者自備 AI 供應商金鑰的產品中的設定卡片。每個供應商佔一列，顯示其標誌、遮罩金鑰（`sk-••••4f2a`）、由圖示加文字組成的狀態，以及金鑰最近一次使用的時間。金鑰可以顯示五秒、複製、輪替，或在短暫的復原視窗內移除。新增或輪替金鑰時，會在該列下方開啟一個內嵌表單，在輸入時檢查格式，並在儲存前測試連線。

- **工作區或帳戶設定**，使用者在這裡為你支援的供應商新增自己的金鑰。
- **新手引導步驟**，請求第一把金鑰並顯示它何時連線成功。
- **閘道管理頁面**，顯示哪些供應商已連線、哪些金鑰被拒絕，以及每把金鑰的使用時間。
- **輪替已外洩的金鑰**，或移除金鑰並保留片刻復原。

## 何時不用

- **為某次請求選擇模型**：使用模型選擇器。
- **你自己產品提供給客戶的 API 金鑰**，帶有權限範圍與到期時間：使用表格。
- **花費或速率限制**：使用用量計量。

## 用法

把 `Providers` 設為你支援的供應商，把 `Keys`（雙向）繫結到伺服器傳回的資料，處理 `KeySaved` 與 `KeyRemoved` 以保存，並設定 `TestKey` 在伺服器上測試金鑰。沒有金鑰的供應商顯示為 Not set。沒有 `TestKey` 時，測試會以 "No key test is set up" 失敗，因此任何金鑰都不會未經檢查就顯示為 Connected。

```csharp
card.Providers =
[
    new ProviderKeyProvider { Id = "anthropic", Name = "Anthropic", KeyPrefix = "sk-ant-api03-" },
    new ProviderKeyProvider { Id = "google", Name = "Google" },   // no documented prefix: no prefix check
];
card.Keys = await api.GetProviderKeysAsync();      // one ProviderKey per provider
card.TestKey = request => api.TestProviderKeyAsync(request.Provider, request.Key, request.CancellationToken);
card.KeySaved += (_, e) => api.SaveProviderKey(e.Provider, e.Key);
card.KeyRemoved += (_, e) => api.DeleteProviderKey(e.Provider);
```

### 結構

1. **標誌**：供應商的圖示（或字首）放在帶色調的圓角方塊裡。
2. **名稱與金鑰**：供應商名稱，下方是等寬字型的遮罩金鑰（顯示時為完整金鑰）。
3. **狀態與最近使用**：帶圖示的 Connected、Invalid 或 Not set，下方是 "Used 2 min ago"、"Never used" 或拒絕原因。按下狀態會重新測試已儲存的金鑰。
4. **操作**：帶倒數圓環的顯示、複製、輪替與移除；空白列顯示 Add key；移除後顯示 Undo。
5. **內嵌表單**：金鑰輸入框與 Cancel、Save，以及一行提示，說明哪裡不對、Testing connection 或 Connected。

### 你的 API

<Demo name="YourApi" />

`TestKey` 接收一個 `ProviderKeyTestRequest`：供應商的 id、新增或輪替時貼上的金鑰（重新測試已儲存的金鑰時沒有金鑰，請在伺服器上依供應商測試），以及一個在表單關閉時取消的權杖。傳回 `ProviderKeyTestResult.Ok(detail)` 或 `ProviderKeyTestResult.Fail("401 Unauthorized")`；擲出的例外視為失敗，並使用其訊息。輪替時，該列會保留舊金鑰，直到新金鑰通過測試才替換。

| 事件 | 何時觸發 |
|---|---|
| `KeysChanged` | 每次變更：新增、輪替、移除、透過 Undo 復原或重新測試。此時 `Keys` 已更新。 |
| `KeySaved` | 新金鑰通過了測試。在這裡保存；該列已顯示為已連線。 |
| `KeyRemoved` | 復原視窗關閉，所以移除只有在確定後才會傳到你的伺服器。 |
| `KeyCopied` | 金鑰已被複製，可用於稽核記錄。 |

在正式環境中不要把完整金鑰傳到用戶端：每個 `ProviderKey` 只提供 `Last4`，`Value` 保持為 null。該列隨後顯示遮罩金鑰，並關閉顯示與複製。

### 空白狀態

<Demo name="Empty" />

空的 `Keys` 清單會讓每一列顯示為 Not set，並帶有 Add key 按鈕。`Providers` 決定哪些供應商有一列以及它們的順序；每個 `ProviderKeyProvider` 提供名稱、表單檢查的金鑰前綴與最小長度，以及 `Mark` 圖示或 `Monogram`。使用供應商文件中提供的前綴；文件沒有提供前綴時，讓 `KeyPrefix` 保持空白。

### 模擬測試

用於展示或原型時，設定 `Simulation` 而非 `TestKey`：`ProviderKeysSimulation` 等待 `Delay`（700ms），讓已儲存的無效金鑰以其自身的錯誤（或 `RejectedError`）再次失敗，其餘金鑰都連線成功。不會傳送任何內容，每個朗讀的結果都以 "Simulated, nothing was sent." 結尾。本頁的展示使用真實的供應商名稱以及其文件提供的金鑰前綴；金鑰是預留位置，被拒絕的金鑰是虛構的。

```xml
<arc:ProviderKeys>
    <arc:ProviderKeys.Simulation>
        <arc:ProviderKeysSimulation Delay="0:0:0.7" />
    </arc:ProviderKeys.Simulation>
</arc:ProviderKeys>
```


### 載入中

<Demo name="Loading" />

`IsLoading` 在金鑰載入期間顯示預留列，幾何尺寸與真實的列一致。

### 窄卡片

<Demo name="Narrow" />

寬度在 560px 以上時，每一列是一行多欄。低於該寬度（`NarrowWidth`）時，狀態移到名稱旁邊，金鑰獨佔一行，最近使用與操作共用一行；表單把輸入框放在 Cancel 與 Save 上方。卡片在 360px 寬時仍不會出現水平捲動。

### 你自己的文字

`Labels` 包含卡片顯示或朗讀的每一個詞，`Title`、`Description` 與 `Footer` 設定卡片自己的文字（設為 null 會隱藏描述或頁尾）：

```csharp
card.Labels = new ProviderKeysLabels
{
    Connected = "Verbunden",
    NotSet = "Nicht gesetzt",
    AddKey = "Schlüssel hinzufügen",
    CopyName = provider => $"{provider}-Schlüssel kopieren",
};
```

### 最佳做法

- 在伺服器上測試金鑰（請求一次模型清單就足夠），並傳回供應商自己的錯誤。
- 只傳 `Last4`；顯示與複製適用於把金鑰保存在裝置上的產品。
- 讓 `RevealDuration` 保持簡短；五秒足以讀取或比對一把金鑰。

## API 參考

<!-- api: ProviderKeys -->

<!-- api: ProviderKey -->

<!-- api: ProviderKeyProvider -->

<!-- api: ProviderKeysSimulation -->

<!-- api: ProviderKeysLabels -->

## 無障礙

- 卡片以其標題命名。每個圖示按鈕都有針對供應商的名稱，例如 "Copy the Anthropic key"。
- 狀態從不只靠顏色表達：圖示加上 Connected、Invalid、Not set 或 Testing。狀態按鈕的名稱包含拒絕原因（"OpenAI key: Invalid, 401 Unauthorized. Test again"）。
- 顯示是一個回報按下狀態的切換鈕；Rotate 與 Add key 回報內嵌表單是否展開。
- 一個禮貌的即時區域會朗讀顯示、複製、測試結果、移除與復原。`Simulation` 的測試結果會附加 "Simulated, nothing was sent."
- 表單的輸入框名為 "Anthropic API key"，其說明文字就是提示行；格式錯誤與拒絕會把它標記為無效。
- 焦點受到管理：表單會聚焦其輸入框，Cancel 與 Escape 會把焦點還給開啟表單的按鈕，Remove 會把焦點移到 Undo，Undo 再把它還給 Remove。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過每一列的狀態（重新測試）、顯示、複製、輪替與移除，空白列則是 Add key |
| Enter、Space | 按下取得焦點的按鈕 |
| Enter | 在表單中檢查格式，然後測試並儲存金鑰 |
| Escape | 在表單中取消，並把焦點還給開啟它的按鈕 |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 狀態、金鑰、最近使用與提示文字 | 原地交叉淡化一次：淡出 100ms，淡入 160ms 並上升 2px | 短暫交叉淡化，不上升 | 立即 |
| 內嵌表單開啟或關閉 | 高度彈簧（0.24s，無回彈）並伴隨短暫淡化 | 立即 | 立即 |
| 顯示倒數 | 按鈕周圍的細線圓環在 `RevealDuration` 內線性清空 | 沒有圓環（金鑰仍會自動遮罩） | 沒有圓環 |
| 復原倒數 | Undo 內的小圓環在 `UndoDuration` 內清空 | 沒有圓環 | 沒有圓環 |
| 眼睛圖示 | 新圖示從 0.6 放大並在 160ms 內淡入 | 僅淡入 | 立即 |
| 按下圖示按鈕 | 以彈簧縮放到 0.94 | 不縮放 | 不縮放 |
| 測試中的載入圈 | 每 0.8s 轉一圈 | 每 1.6s 轉一圈 | 靜止 |

每段文字都位於固定的儲存格中，狀態欄寬度固定，所以狀態或金鑰變化時不會改變尺寸。相對時間每 30 秒重新整理一次。

## 相關元件

- `CopyButton`：複製單一值並即時確認。
- `Input`：帶標籤與錯誤提示的單行輸入框。
- `ContextWindow` 與 `AgentStatus`：AI 產品設定與執行的其他部分。
