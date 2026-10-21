---
title: 操作按鈕
summary: 用於高頻工具列操作的精簡按鈕。
source-hash: c5a5043a2562
---

<Demo name="Default" />

## 何時使用

`ActionButton` 是一次非同步送出的行動呼籲，例如儲存、發布或寄送邀請。它直接在按鈕上呈現工作進度：工作執行時，標籤切換為進行中的文字，載入圖示旋轉；成功時，一段文字加上繪製出的勾號確認結果。使用者在點按的位置就能得知結果，無需跳出提示，按鈕也會自行回到閒置標籤。把它放在表單末端、文件頁首或設定卡片中，凡是送出就是那個最重要操作的地方都適用。

- **在內容編輯器中發布文章**：編輯器頁首中的「發布文章」按鈕在文章送出時顯示「發布中」，上線後顯示「已發布」，編輯器無需離開頁面即可知道變更已生效。
- **在管理主控台中儲存設定頁**：工作區設定頁底部的「儲存設定」按鈕在請求成功後顯示「設定已儲存」，表單保持在畫面上，方便下一次修改。
- **從團隊頁面寄送邀請**：郵件服務接受請求後，「寄送邀請」變為「已寄送」，2.4 秒後回到「寄送邀請」，以便管理員邀請下一位成員。
- **從 Webhook 編輯器寄送測試事件**：呼叫端點時，「寄送測試事件」顯示「寄送中」，端點回應後顯示「已送達」。呼叫失敗時按鈕回到閒置，編輯器在旁邊說明錯誤。

## 何時不用

- **一般的表單送出，只需載入指示、不需結果文字**：用 `Button` 並設定 `IsLoading`。它在工作執行期間保留標籤與寬度，也不需要成功狀態。
- **需要使用者刻意按住才能執行的破壞性操作**：用 Gestures 中的 `HoldToConfirm`。長按讓誤刪變得困難，而成功文字只會在資料已經消失之後才出現。
- **需要在原位確認的破壞性操作**：用 `ConfirmMorph`，它在任何操作執行之前，就在控制項所在的位置提出問題。
- **同一操作的多個備選項**：用 `SplitButton`，例如主按鈕為「合併拉取請求」，選單中有「Squash and merge」與「Rebase and merge」。
- **密集表格中每列重複的列操作**：在列內用小號 `Button` 保留結果。每一列都有動畫標籤會造成干擾，而且標籤的每個字母都是一個動畫元素。
- **把值複製到剪貼簿**：用 `CopyButton`，它會就地確認複製結果，專為這項工作而設計。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActionButton Label="儲存" PendingLabel="儲存中" SuccessLabel="已儲存" />
</UserControl>
```

```csharp
var save = new ActionButton
{
    Label = "儲存",
    PendingLabel = "儲存中",
    SuccessLabel = "已儲存",
    AsyncAction = () => documentService.SaveAsync()
};
```

`ActionButton` 衍生自 Avalonia 的 `Button`，而不是 Arc 的 `Button`。它保留按鈕的全部功能：`Click`、`Command`、`IsEnabled`、透過 Enter 與 Space 啟動、焦點模型以及自動化對等體。它不公開 `Variant`、`Size` 或 `IsLoading`，因為它只有一種外觀與一種高度，而等待狀態來自 `AsyncAction`。應設定 `Label` 而不是 `Content`：範本從 `Label`、`PendingLabel` 與 `SuccessLabel` 繪製文字。

### 結構

1. **標籤**（`Label`）：閒置文字，採用小號內文字級（`TypeSM`）與中等字重。它由文字變形繪製，因此等待與成功文字會在同一位置取代它。它同時是無障礙名稱。
2. **圖示位**：標籤之後 8 px 處的 17 px 方塊。閒置時顯示右箭頭，操作等待時顯示 16 px 的載入圖示，成功時顯示勾號。
3. **外框**：1 px 邊框，控制項圓角（`RadiusControl`）。填色為 Foreground 權杖，文字色為 Background 權杖，因此按鈕與頁面反色：淺色頁面上為深色，深色頁面上為淺色。
4. **內距與高度**：左右各 20 px 內距，最小高度為中號控制項尺寸（44 px）。它只有一種尺寸，不隨視窗寬度變化。
5. **焦點環**：2 px 寬的焦點色環，繪製在外框外 2 px 處。僅在鍵盤焦點時出現。

按鈕預設靠左對齊。位於置中的欄位中時，請設定 `HorizontalAlignment`。懸停時外框加上靜止陰影。按下時按鈕縮放到 97%，若寬度超過 220 px 則縮放到 98.5%，使用靈敏的彈簧動畫。等待中的按鈕不縮放。停用時，它淡化到 70% 不透明度，並顯示「不允許」游標。

### 標籤與重設

`Label` 是閒置文字，也是無障礙名稱。`AsyncAction` 執行時顯示 `PendingLabel`（預設為 “Saving”），完成後顯示 `SuccessLabel`（預設為 “Saved”）。閒置標籤使用動詞，等待文字使用其現在分詞，成功文字使用過去式：「發布文章」「發布中」「已發布」。這樣設定的一組標籤，在文字變形時寬度變化較小。

`ResetAfter` 設定成功文字停留多久後按鈕回到閒置，預設為 2.4 秒（2400 毫秒）。設為零時，成功狀態會一直保留，直到呼叫 `Reset()`。在工具列中使用預設值，因為結果只需一瞥即可。在使用者持續編輯的設定頁上設為零，使確認訊息不會在他們閱讀時消失。

<Demo name="Labels" />

<Demo name="KeepResult" />

```xml
<arc:ActionButton Label="發布貼文"
                  PendingLabel="發布中"
                  SuccessLabel="已發布"
                  ResetAfter="00:00:03" />
```

當按鈕回報的狀態已不再屬實時，請由你自己重設按鈕。`Reset()` 會立即回到閒置狀態，並取消待執行的成功重設。它也會使仍在執行的操作失效：當該工作稍後完成時，其結果會被忽略，按鈕保持閒置。

### 生命週期

| 時機 | 狀態 | 可見文字 | 使用者聽到的內容 |
|---|---|---|---|
| 按鈕就緒 | `Idle` | `Label` 與箭頭 | 名稱：`Label` |
| 啟動後開始 `AsyncAction` | `Pending` | `PendingLabel` 與載入圖示 | 等待文字，僅一次，來自狀態區域 |
| 工作完成 | `Success` | `SuccessLabel` 與勾號 | 成功文字，僅一次 |
| `ResetAfter` 到時或執行 `Reset()` | `Idle` | `Label` 與箭頭 | 無 |
| 工作擲出例外 | `Idle` | `Label`；引發 `ActionFailed` | 按鈕本身不發出內容；請在附近顯示錯誤 |
| 成功期間按鈕離開視覺樹 | `Idle` | `Label` | 無 |

狀態只會透過這些時機變動，因此你可以讀取 `State`（唯讀的 `ActionButtonState`），決定畫面上其他內容應如何變化。

### 失敗

`AsyncAction` 擲出例外時，按鈕回到閒置狀態，並引發 `ActionFailed`。該事件是冒泡的路由事件，其 `ActionButtonFailedEventArgs.Exception` 保存了操作擲出的例外。成功文字不會顯示，重設計時器也不會啟動。

<Demo name="Failure" />

```csharp
sync.ActionFailed += (_, e) => status.Text = $"已回到閒置：{e.Exception.Message}";
```

請把訊息顯示在按鈕旁邊，而不是按鈕上。按鈕已回到閒置標籤，使用者可以立即重試，旁邊的文字說明出了什麼問題。保留使用者輸入的內容，使重試不必從空白表單開始。

### 命令與點按

`Click` 與 `Command` 會在 `AsyncAction` 之前執行。按鈕在仍為閒置時引發 `Click`，然後進入等待狀態並啟動操作。沒有 `AsyncAction` 時，命令或點按被視為立即成功，成功文字立即顯示。同時設定兩者是有效的：命令同步執行，非同步操作在其之後開始，因此命令應只是工作中快速的部分。

`ICommand.Execute` 不傳回任何內容，因此命令無法為一項工作保持等待狀態。對於非同步工作，請使用 `AsyncAction`（見下方的檢視模型範例）。

### 停用

將 `IsEnabled` 設為 `False` 會使按鈕變暗到 70% 不透明度，並將其移出 Tab 順序。`AsyncAction` 等待期間，按鈕保持鍵盤焦點並忽略重複啟動，但不會被停用。當某個操作無法執行時（例如沒有可儲存的內容），請用 `IsEnabled` 阻止它，並在附近說明原因。

<Demo name="Disabled" />

不要透過停用按鈕來表示表單無效。應在使用者按下按鈕時進行驗證，然後把焦點移到第一個需要處理的欄位。

### 繫結到檢視模型

`ActionButton` 有一個送出操作並回報狀態，因此工作由檢視模型負責，按鈕只顯示其進度。下面的檢視模型使用 CommunityToolkit.Mvvm。`PublishAsync` 是一般的公開方法，傳回 `Task`。

```csharp
public partial class PostEditorViewModel : ObservableValidator
{
    private readonly IPostService _posts;

    public PostEditorViewModel(IPostService posts)
    {
        _posts = posts;
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "請輸入文章標題。")]
    private string _title = string.Empty;

    [RelayCommand]
    private void Discard()
    {
        Title = string.Empty;
    }

    public async Task PublishAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        await _posts.PublishAsync(Title);
    }
}
```

```xml
<StackPanel Width="420" Spacing="16">
    <arc:Input Label="文章標題" Text="{Binding Title}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="放棄" Variant="Ghost" Command="{Binding DiscardCommand}" />
        <arc:ActionButton x:Name="Publish" Label="發布文章" PendingLabel="發布中" SuccessLabel="已發布" />
    </StackPanel>
</StackPanel>
```

```csharp
public partial class PostEditorView : UserControl
{
    public PostEditorView()
    {
        InitializeComponent();
        Publish.AsyncAction = () => ((PostEditorViewModel)DataContext!).PublishAsync();
    }
}
```

繫結是單向的。`Label`、`PendingLabel` 與 `SuccessLabel` 來自檢視或在地化資源，`Title` 在輸入框上為雙向繫結。`ActionButton` 不會寫回任何內容：`State` 為唯讀，其結果由檢視模型自身的狀態回報。`AsyncAction` 的指派保留在程式碼中，因為方法群組無法在 XAML 中繫結到 `Func<Task>`。不要把同一項工作同時繫結到 `Command` 與 `AsyncAction`，否則它會執行兩次。`Discard` 按鈕使用 `Command`，因為它會立即完成。

### 範例：在內容編輯器中發布文章

內容管理系統中的文章編輯器頁首。標題是編輯器的標題，頁尾放著寫作者會用到的三個操作：放棄草稿、儲存為草稿、發布。發布是此介面上唯一的主要操作，因此它是唯一的 `ActionButton`。說明文字告訴寫作者發布會對讀者產生什麼影響，這是他們按下之前需要知道的後果。成功文字「已發布」在按下的位置確認變更，按鈕幾秒後回到「發布文章」，頁首因此可以隨時準備好進行下一次編輯。

![文章編輯器頁尾，含「放棄」「儲存草稿」以及閒置狀態的「發布文章」操作按鈕](images/PostEditorFooter.png)

```xml
<StackPanel Width="600" Spacing="20">
    <StackPanel Spacing="6">
        <TextBlock Text="Q2 pricing update"
                   FontSize="20"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Draft saved 4 minutes ago. Starter plan readers see the new prices after you publish."
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Discard" Variant="Ghost" />
        <arc:Button Content="Save draft" Variant="Secondary" />
        <arc:ActionButton Label="Publish post" PendingLabel="Publishing" SuccessLabel="Published" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **閒置標籤**：動詞加其對象，句首大寫：「發布文章」「儲存設定」「寄送邀請」。對象告訴使用者將會改變什麼，而單純的「送出」做不到。
- **等待文字**：現在分詞，不加省略號：「發布中」。載入圖示已經表示進度，句末的「...」只是重複。
- **成功文字**：動作的過去式，一到兩個詞：「已發布」「設定已儲存」。保持簡短，使寬度變化較小。
- **範圍**：每個按鈕只對應一個結果。描述多件事的成功文字（「已儲存並寄出」）既難讓人信任，也難以翻譯。

| 不要寫 | 改為 |
|---|---|
| 送出 | 發布文章 |
| 儲存中... | 儲存中 |
| 完成！ | 已發布 |
| 點按儲存 | 儲存設定 |

### 最佳做法

**建議**

- 對於必須保持等待狀態的任何工作，使用 `AsyncAction`。命令會在非同步工作完成前傳回，因此按鈕會過早顯示成功。
- 每個介面只放一個 `ActionButton`，用於最重要的那次送出。兩個帶動畫的結果會爭奪注意力，使用者將無法分辨結果屬於哪一個。
- 在按鈕可以執行之前設定好 `Label`、`PendingLabel` 與 `SuccessLabel`。`Label` 的變更只在按鈕閒置時顯示，因此在等待或顯示成功期間，可見文字保持不變，直到按鈕回到閒置。
- 在按鈕旁邊顯示失敗訊息。按鈕已回到閒置標籤，因此訊息應緊挨著失敗的欄位或表單。

**避免**

- 放在密集表格中，或每列一個按鈕的清單中。每個按鈕都會對自己的字母做動畫，動態效果變成雜訊（見上方表格的指引）。
- 將 `ResetAfter` 設為不足一秒。成功文字在被閱讀之前就消失，結果無從得知，禮貌播報也容易被錯過。
- 為表示請求正在執行而停用按鈕。等待狀態已經讓按鈕保持焦點並忽略啟動，而且會告訴輔助技術該按鈕無法使用。
- 用於破壞性操作。成功文字是在資料消失之後才出現的；應使用 `HoldToConfirm` 要求刻意按下，或使用 `ConfirmMorph` 在原位確認。

### 常見問題

**為什麼我的非同步命令在儲存完成前就顯示「已儲存」？** `ICommand.Execute` 不傳回任何內容，因此按鈕在命令傳回的那一刻就視其為已完成。請把工作移到 `AsyncAction` 中，按鈕會等待它。

**能否同時使用 `Command` 與 `AsyncAction`？** 可以，但應讓每個各司其職。命令先同步執行，然後非同步操作開始。兩條路徑儲存同一份資料，會導致儲存執行兩次。

**如何讓成功文字在下一次儲存前一直顯示？** 將 `ResetAfter` 設為零（`ResetAfter="00:00:00"`）。按鈕隨後保持 `Success`，直到你呼叫 `Reset()`，例如當使用者再次編輯表單時。

**按鈕顯示成功時離開了視覺樹，會發生什麼？** 重設計時器停止，按鈕立即回到閒置。等待中的操作不會被取消：它繼續執行，完成時只有當該次執行仍是目前的執行時，按鈕才會記錄成功。

**為什麼設定 `Content` 後標籤不變？** 範本從 `Label`、`PendingLabel` 與 `SuccessLabel` 繪製文字。`Content` 繼承自 `Button`，但不屬於視覺呈現的一部分。

## API 參考

<!-- api: ActionButton -->

此頁面使用的附屬型別：

| `ActionButtonState` 值 | 意義 |
|---|---|
| `Idle` | 顯示 `Label`。啟動時執行 `Click`、`Command` 與 `AsyncAction`。 |
| `Pending` | `AsyncAction` 正在執行。顯示 `PendingLabel`。忽略啟動。 |
| `Success` | 操作已完成。顯示 `SuccessLabel`，直到 `ResetAfter` 到時或呼叫 `Reset()`。 |

| `ActionButtonFailedEventArgs` 成員 | 意義 |
|---|---|
| `Exception` | 操作擲出的例外。引發事件時按鈕已回到閒置。 |

## 無障礙

- 無障礙名稱始終為 `Label`，因此動畫標籤的字母不會被逐個朗讀。
- 等待期間，按鈕向輔助技術回報自身無法使用，但保持鍵盤焦點。
- 一個隱藏的禮貌狀態區域會播報等待文字與成功文字，因此無需移動焦點即可聽到變化。
- Enter 與 Space 執行操作。等待期間忽略重複啟動。
- 按鈕不會播報失敗。應把錯誤文字放在螢幕閱讀器能夠讀到的位置，例如即時區域或按鈕旁邊的文字區塊中，使使用者聽到按鈕回到閒置的原因。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter` / `Space` | 執行操作。等待期間忽略。 |
| `Tab` / `Shift+Tab` | 移到下一個或上一個控制項。等待中的按鈕保留其焦點停留位置，因此焦點不會跳走。 |

## 動態效果

- 變動的字母從柔和的模糊中升起，保留的字母滑動到新位置。寬度隨彈簧過渡。
- 箭頭沿操作方向離開，並從左側回來。操作執行時載入圖示旋轉，成功時勾號逐筆繪製。
- 減少動態效果時，移除按下縮放、寬度彈簧與繪製的勾號。文字切換改為瞬間交叉淡化。關閉動態效果時，文字立即取代。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 標籤變為等待或成功 | 變動的字母從柔和模糊中升起，保留的字母滑動，寬度以彈簧過渡 | 瞬間交叉淡化 | 立即完成 |
| 成功勾號 | 在 240 ms 內繪製完成 | 直接出現，不繪製 | 立即完成 |
| 按下 | 以靈敏的彈簧縮放到 97%（寬按鈕為 98.5%）；等待時從不縮放 | 無按下縮放 | 無按下縮放 |

## 相關元件

- `Button`：一般操作；需要載入指示而無結果文字時，設定 `IsLoading`。
- `CopyButton`：複製一個值，並以同類的行內確認方式回饋。
- `ConfirmMorph`：破壞性操作，在原位要求確認。
- `HoldToConfirm`：破壞性操作，需要使用者刻意按住才能執行。

| 元件 | 適用情境 |
|---|---|
| `Button` | 操作沒有結果文字，或在請求執行時顯示載入圖示 |
| `SplitButton` | 一個預設送出旁邊有一組相近的備選操作選單 |
| `CopyButton` | 操作是複製一個值，確認方式為行內文字 |
| `ConfirmMorph` | 破壞性操作應在執行前於原位提出問題 |
| `HoldToConfirm` | 破壞性操作需要刻意按住，不會被意外觸發 |
| `ButtonGroup` | 同一物件上的多個相關操作共用一個表面 |
