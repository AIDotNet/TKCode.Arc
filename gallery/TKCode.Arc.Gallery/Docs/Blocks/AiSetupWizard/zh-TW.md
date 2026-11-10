---
title: AI setup wizard
summary: 為工作區開啟 AI 的五步設定：服務商、模型和密鑰，資料訪問，隱私規則，連接測試，以及上線前的總結。
source-hash: e0fd9b4b2f2d
---

<Demo name="Preview" />

## 何時使用

`AiSetupWizard` 帶領工作區管理員開啟 AI 功能。共五步，每步一頁：選擇服務商和模型並輸入密鑰；選擇助手可以讀取哪些工作區資料；決定隱私規則（資料不用於訓練、隱去個人資訊、聊天紀錄保留多久）；用這些設定執行一次簡短的測試；開啟前檢查全部選擇。頁面旁的步驟條顯示管理員所在的位置，並可以回到任何已完成的步驟。每一步都會檢查自己的字段，測試通過後才能進入總結。

- **管理員設定。** 工作區第一次啟用助手，或改用另一家服務商時。
- **自建服務或自帶密鑰。** 不需要密鑰的服務商（你自己的伺服器）會跳過密鑰輸入框。
- **法遵檢查。** 總結在一處列出所有選擇，然後才做出任何改動。

## 何時不用

- **個人偏好。** 不需要測試時，一個開關或 `Settings page` 區塊就夠了。
- **只選擇模型。** 使用 `Model picker` 區塊。
- **很長的表單。** 字段多且彼此無關時，使用分節的設定頁，而不是分步。

## 用法

本區塊派生自 Avalonia 的 `TemplatedControl`。不設定任何資料時，它提供虛構的服務商和資料來源以及模擬的測試（`AiSetupWizardSamples`；密鑰填 "fail" 可以看到失敗的情況）。

```xml
<arc:AiSetupWizard Providers="{Binding Providers}" DataSources="{Binding DataSources}" />
```

```csharp
wizard.TestRunner = async (configuration, progress, cancellation) =>
{
    await client.PingAsync(configuration.ProviderId, cancellation);   progress.Report(1);
    await client.CheckKeyAsync(configuration.ApiKey, cancellation);   progress.Report(2);
    var reply = await client.AskAsync(configuration.Model, "Say hi", cancellation); progress.Report(3);
    await index.ReadOneAsync(configuration.DataSourceIds, cancellation); progress.Report(4);
    return new AiSetupTestResult(true, $"The model answered in {reply.Elapsed.TotalMilliseconds:0} ms.");
};
wizard.Completed += (_, e) => workspace.EnableAi(e.Configuration);
```

### 各個步驟

1. **服務商和模型**：`Providers` 顯示為單選卡片；模型選單列出所選服務商的 `Models`；`RequiresKey` 時顯示密鑰輸入框，下方是 `KeyHint`。繼續之前需要選好服務商，需要時還要填寫密鑰。
2. **資料訪問**：`DataSources` 中每一項一個核取方塊；`IsEnabledByDefault` 決定初始狀態。
3. **隱私**：兩個開關（訓練、隱去資訊）以及來自 `HistoryChoices` 的保留時長（天數；0 表示不保留）。
4. **測試**：四項檢查，從圓圈變為載入圈，再變為對勾或叉號；顯示結果消息，並有再次執行的按鈕。之前步驟中的任何改動都會要求重新測試。
5. **完成**：所有選擇的總結，以及觸發 `Completed` 並帶上 `AiSetupConfiguration` 的按鈕。

`CurrentStep`（雙向）只會移到已經到過的步驟；`Next()`、`Back()`、`GoTo(step)`、`RunTestAsync()` 和 `Reset()` 從程式碼完成按鈕所做的事。`Configuration`、`TestResult`、`IsTesting` 和 `IsCompleted` 報告當前狀態。

### 窄布局

<Demo name="Narrow" />

### 文字

`Labels`（`AiSetupWizardLabels`）包含步驟名稱和說明、按鈕、字段標簽、錯誤提示、開關文字、測試檢查項、總結的各行名稱以及步驟播報。

## API 參考

<!-- api: AiSetupWizard -->

<!-- api: AiSetupProvider -->

<!-- api: AiSetupDataSource -->

<!-- api: AiSetupConfiguration -->

<!-- api: AiSetupTestResult -->

<!-- api: AiSetupStep -->

<!-- api: AiSetupWizardLabels -->

## 無障礙

- 向導是一個表單地標；標題是二級標題，每頁的標題是三級標題。
- 步驟條讀出每一步的名稱和狀態；已完成的步驟可以用鍵盤選擇。
- 切換步驟時播報 "Step 2 of 5: Data access"。缺少服務商或密鑰時會播報，並顯示在字段下方，焦點移到該字段。
- 每項測試檢查報告自己的狀態；結果消息會禮貌地朗讀。
- 開關以其文字命名，並以下方的說明作為描述。

## 動效

- 頁面沿前進方向滑動 12 px 並交叉淡化；步驟條依次填充連線並移動光圈。
- 檢查行立即切換圖標；測試執行期間測試按鈕顯示載入圈。
- 減弱動效時頁面只交叉淡化不滑動；關閉動效時立即切換。

## 響應式

- 向導最寬 860。寬布局時，步驟條是頁面旁寬 220 的一列。寬度不超過 640 時（`:compact`），步驟條變為橫向並移到頁面上方，內邊距從 24 收緊到 16。
- 頁面保持至少 360 的高度，步驟之間切換時頁腳不會跳動。
- 單選卡片和核取方塊的文字會換行；沒有任何功能依賴停留。

## 相關元件

- `Stepper`、`RadioCards`、`Select`、`PasswordField`、`Checkbox`、`Switch` 和 `SegmentedControl`，組成它的控制項。
- `Multi-step form`，通用的分步表單。
- `Model picker`，用於在產品中選擇模型。
