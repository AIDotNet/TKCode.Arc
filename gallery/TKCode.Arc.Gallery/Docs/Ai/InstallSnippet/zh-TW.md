---
title: 安裝片段
summary: 一張快速上手卡片：框架分頁、三個編號步驟和醒目標示的程式碼片段帶開發者從安裝走到第一個事件，「產生金鑰」會把新金鑰就地擦入預留位置。
source-hash: 8834ae45e0b1
---

<Demo name="Preview" />

## 何時使用

`InstallSnippet` 是一張「30 秒上手」卡片。框架分頁選擇讀者的技術堆疊，三個編號步驟（安裝、新增金鑰、上線）各顯示一段語法標示的程式碼，並附有複製按鈕。當框架透過套件安裝時，步驟 1 提供 npm、pnpm、yarn 和 bun 可選。「產生金鑰」會建立一把金鑰，並把它就地擦入預留位置，所有用到它的片段都會更新。切換框架時，片段在為最長內容預留的空間裡交叉淡化，因此卡片尺寸始終不變。

- **適用多個框架的快速上手**，不必為每個框架各寫一頁文件。
- **建立 API 金鑰屬於入門一環的引導流程**：註冊後的第一個畫面，或等待第一個事件的空白儀表板。
- **開發者工具的行銷頁面**，展示接入只需要多少設定。

## 何時不用

- **只有一段程式碼、沒有步驟**：使用 `CodeBlock`。
- **對線上 API 試送請求**：API 測試台更合適。
- **分支眾多的長篇安裝指南**：撰寫文件頁面並連結過去。
- **完整顯示既有的金鑰**：請在 API 金鑰頁面中遮蔽顯示（參見 `ProviderKeys`）。

## 用法

沒有框架也沒有模擬（見下文）時，卡片顯示三個步驟與空白的程式碼片段（上方的 Empty 分頁）。沒有設定金鑰產生器時，「產生金鑰」會編造一把展示金鑰並據實說明：不會傳送任何內容。傳入你自己的框架和金鑰端點：

```csharp
snippet.KeyGenerator = async token =>
{
    var response = await http.PostAsync("/api/keys", null, token);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<NewKey>(token))!.Key;
};
snippet.Copied += (_, e) => analytics.Track("onboarding_copy", e.Step.ToString());
```

傳回金鑰後，它會擦入步驟 2。擲出例外時，步驟 2 會以警示圖示顯示例外的訊息（或「Could not create a key.」），按鈕變為「重試」（Try again）。當卡片離開視窗或讀者再次要求金鑰時，權杖會被取消。

### 結構

1. **標題區**：標題、其下的一行說明，以及右上角低調的版本標籤（`Version`；為空則隱藏）。
2. **框架分頁**：每個框架一個標記和名稱；浮起的醒目標示會滑到所選分頁下。
3. **步驟**：軌道上的編號標記、標題、一行說明要做什麼，以及含檔名、複製按鈕和程式碼片段的程式碼區塊。片段被複製過的步驟會把編號換成勾號。
4. **步驟 1** 在框架透過套件安裝時，於標題右側顯示套件管理工具分頁。
5. **步驟 2** 在標題右側有「產生金鑰」，程式碼下方有一行狀態。

### 你的框架

<Demo name="YourFrameworks" />

每個 `InstallSnippetFramework` 都有 `Id`、`Label` 和三段片段。透過 npm、pnpm、yarn 或 bun 安裝時設定 `Packages`（以空格分隔的套件名稱），指令或指令碼標籤則設定 `Install`。在金鑰所在位置寫 `{{key}}`；在金鑰出現之前，那裡會顯示帶底色的預留位置（`KeyPlaceholder`），並依原樣複製：

```xml
<arc:InstallSnippet KeyPlaceholder="YOUR_CONTOSO_KEY">
    <arc:InstallSnippetFramework Id="ruby" Label="Ruby">
        <arc:InstallSnippetFramework.Install>
            <arc:InstallSnippetFile FileName="Terminal" Code="gem install contoso-labs" />
        </arc:InstallSnippetFramework.Install>
        <arc:InstallSnippetFramework.Key>
            <arc:InstallSnippetFile FileName=".env" Code="CONTOSO_KEY={{key}}" />
        </arc:InstallSnippetFramework.Key>
    </arc:InstallSnippetFramework>
</arc:InstallSnippet>
```

id 為 `node`、`python`、`go` 和 `html` 時會有各自的標記；其他 id 顯示純文字分頁，除非你把 `Mark` 設為 24 × 24 的描邊圖形。語法標示所用的語言依檔名而定（`Terminal` 和 `.env` 視為 shell），也可以設定 `Language`。每段片段保持在十行以內，卡片才方便快速瀏覽。

### 你的金鑰服務

<Demo name="YourKeyService" />

`SelectedFramework` 和 `PackageManager` 是雙向繫結的：繫結它們即可在不同頁面間記住讀者的選擇。`FrameworkChanged`、`PackageManagerChanged`、`KeyGenerated` 和 `Copied` 回報讀者的操作，複製事件會帶上放入剪貼簿的確切文字。對已登入且已有金鑰的讀者傳入 `ApiKey`：每段片段都會顯示它，「產生金鑰」隨之隱藏。

### 模擬的 SDK

用於展示時，讓 `Frameworks` 維持空白，並給卡片一個 `InstallSnippetSimulation`：它的框架分頁，以及「產生金鑰」編造的金鑰所用的前置字串 `KeyPrefix`。你自己的 `Frameworks` 會讓它讓位，把 `Simulation` 設為 null 會清空卡片。這裡的預覽就以這種方式呈現本程式庫自己的安裝步驟：透過 .NET CLI、PackageReference 或中央套件管理加入 `TKCode.Arc.Special`，把模型服務供應商的金鑰存放在 user secrets、開發環境設定或環境變數中（供你的應用程式自己呼叫模型；Arc 的元件永遠接觸不到它），以及在 `Program.cs` 中註冊。這些金鑰都是編造的展示金鑰。

```csharp
snippet.Simulation = new InstallSnippetSimulation
{
    Frameworks =
    [
        new InstallSnippetFramework("dotnet", ".NET CLI")
        {
            Install = new InstallSnippetFile("Terminal", "dotnet add package TKCode.Arc.Special --version 0.3.2-preview.1"),
            Key = new InstallSnippetFile("Terminal", "dotnet user-secrets set \"Chat:ApiKey\" \"{{key}}\""),
        },
    ],
    KeyPrefix = "demo_",
};
```

### 窄版面

<Demo name="Narrow" />

卡片填滿容器，最寬 640px。寬度低於 460px 時內距收緊，套件管理工具分頁只顯示標記（名稱保留給螢幕閱讀器，並顯示在工具提示中）。低於 380px 時框架分頁拿掉標記，版本標籤隱藏。過長的行在各自的程式碼區塊內橫向捲動，而不是整個頁面捲動。

### 自訂文字

`Labels` 包含卡片顯示或朗讀的每一個詞，`Title`、`Description` 和 `Version` 除外：

```csharp
snippet.Labels = new InstallSnippetLabels
{
    InstallTitle = "Installer",
    KeyTitle = "Ajouter votre clé",
    CodeTitle = "Livrer",
    GenerateKey = "Générer une clé",
};
```

### 內容準則

- 步驟說明用幾個詞講清楚片段該怎麼用（「把金鑰放進 .env，切勿提交。」）。
- 檔名使用讀者真實的檔案（`.env`、`app.py`）；指令顯示為 Terminal。
- 每段片段保持簡短、可直接執行，`{{key}}` 準確放在金鑰所在位置。

### 最佳做法

- 把 `KeyGenerator` 接到你的金鑰端點，並為已有金鑰的讀者傳入 `ApiKey`。
- 追蹤 `Copied`，了解引導流程卡在哪一步。
- 別把模擬金鑰帶進正式環境：沒有 `KeyGenerator` 時，卡片會刻意編造金鑰。

## API 參考

<!-- api: InstallSnippet -->

<!-- api: InstallSnippetFramework -->

<!-- api: InstallSnippetFile -->

<!-- api: InstallSnippetSimulation -->

<!-- api: InstallSnippetLabels -->

## 無障礙

- 框架分頁和套件管理工具分頁都是採用遊走焦點的分頁清單：只有所選分頁是 Tab 停駐點，每個分頁都會回報自己是否被選取。步驟是框架分頁的面板。
- 步驟是一個含三項的清單，每個標題都是標題元素。完成的步驟會朗讀「，done」，並以勾號取代編號，因此進度從不只靠顏色表達。
- 一個禮貌型即時區域會播報框架切換、新金鑰和錯誤，並在金鑰為模擬時加以說明。複製按鈕會自行播報「已複製」。
- 出錯時顯示警示圖示和文字，按鈕變為「重試」。
- 標記僅作裝飾；每個分頁都有名稱，窄版面隱藏套件管理工具分頁的名稱時仍會保留它。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 在框架分頁、套件管理工具分頁、複製按鈕和「產生金鑰」之間移動 |
| 左方向鍵、右方向鍵 | 在分頁清單中移到並選取上一個或下一個分頁（循環） |
| Home、End | 在分頁清單中選取第一個或最後一個分頁 |
| Enter、Space | 複製片段或產生金鑰 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 分頁醒目標示 | 以無回彈的彈簧（0.24s）滑到量測出的分頁 | 立即移動 | 立即 |
| 片段、檔名、步驟說明 | 舊內容在 100ms 內淡出，新內容在 160ms 內淡入並上升 2px，兩者共用同一個儲存格 | 120ms 交叉淡化，不上升 | 立即 |
| 新金鑰 | 欄位從預留位置的寬度緩動到金鑰的寬度（依字元精確計算），同時金鑰在 340ms 內從左側擦入，接著底色在 480ms 內淡去 | 金鑰立即出現；底色仍會淡去 | 立即 |
| 完成的步驟 | 標記填入強調色的同時，編號淡出、勾號淡入 | 勾號在 120ms 內淡入 | 立即 |

每個程式碼區塊都預留其步驟中最長片段的高度，每行步驟說明都預留最長說明的高度，因此切換框架不會移動任何東西。只有不透明度、裁切和一個量測出的寬度參與動畫，閒置時不執行任何東西。

## 相關元件

- `CodeBlock`：附複製按鈕的單段程式碼。
- `CopyButton`：複製單一值。
- `ProviderKeys`：顯示和輪替使用者既有的金鑰。
- `Tabs`：在同一處切換相關內容。
