---
title: 表情回應
summary: 一鍵回應一則內容，計數會滾動變化。
source-hash: 3d9ea606c47a
---

<Demo name="Default" />

## 何時使用

`Reactions` 是一排帶計數的回應膠囊，外加一個開啟小型選取器的新增按鈕。它讓使用者一鍵回應一段內容，無需撰寫回覆。它位於所回應內容的下方，例如版本說明、狀態更新或設計審查留言，計數顯示每個選項有多少人作出了回應。

- **版本說明與變更日誌項目**：團隊看到「2026.10 版本已上線」，點按「已發佈」或「慶祝」。計數顯示有多少工程師看過該版本；儲存的清單包含檢視者自己的回應時，該回應會醒目顯示。
- **團隊工作區中的狀態更新**：每日事故摘要或專案更新會收到讀者的「讚」或「感謝」，作者無需開啟回覆串就能知道更新已被看到。
- **設計審查留言**：審查者點按一下，就把留言標記為「同意」或「有疑問」。計數告訴作者團隊已經接受了哪些留言。
- **內部入口網站中的公告資訊流**：歡迎新進同仁的貼文會收到「愛心」和「慶祝」。提示框列出回應者的名字，讓計數變成一種「誰看過」的訊號。

## 何時不用

- **需要文字的回應**，例如提問、決定或對留言的回覆：用 `CommentThread` 或 `ChatThread`，它們把文字與作者放在一起。回應無法承載一句話。
- **檢視者無法更改的計數**，例如未關閉的工單數或未讀訊息數：固定計數用 `Badge`；當數字需要在指標中滾動顯示時，用 `AnimatedCounter`。
- **從清單中選擇一個值**，例如優先順序、狀態或分類：用 `Select`、`Combobox` 或 `MorphSelect`。回應列允許同時選擇多個選項，沒有單一結果。
- **數字刻度上的評分**，例如一到五星或滿意度分數：用 `Slider`，或用儲存數字的表單控制項。回應表達的是選擇，而不是數量。
- **觸控清單中的列操作**，例如收件匣列上的封存或標為已讀：用 `SwipeActions`，它讓操作緊挨著其所作用的列。

## 用法

選項是 `Reactions` 的子元素。計數與檢視者自己的回應構成它的 `Value`，可以在程式碼中設定，也可以從檢視模型繫結。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Reactions Label="表情回應">
        <arc:ReactionOption Id="like" Label="讚" />
        <arc:ReactionOption Id="love" Label="愛心" />
        <arc:ReactionOption Id="celebrate" Label="慶祝" />
    </arc:Reactions>
</UserControl>
```

```csharp
var reactions = new Reactions
{
    Value = [new Reaction("like", 3)]
};
reactions.Options.Add(new ReactionOption("like", "讚", ArcIcons.ThumbsUp));
reactions.Options.Add(new ReactionOption("love", "愛心", ArcIcons.Heart));
reactions.Options.Add(new ReactionOption("celebrate", "慶祝", ArcIcons.Smile));
```

`Reactions` 衍生自 Avalonia 的 `TemplatedControl`，因此它的範本、樣式與版面配置與其他 Arc 控制項的行為一致。選項只是一般資料（`ReactionOption` 不是控制項），列會為每個有計數的回應建立一個切換膠囊。`Options` 與 `Value` 中使用相同的 `Id`：Id 是伺服器為回應儲存的值，Label 只是給人閱讀的文字。

`ReactionOption` 透過 `Icon` 接收 24 x 24 的 Lucide 幾何圖形，以 1.75 的筆畫繪製。`ArcIcons` 中沒有的圖示可以用 `ArcIcons.Parse` 解析，或取自 `StreamGeometry` 資源，正如範例庫為彩炮圖示所做的那樣。

### 結構

1. **列**（`Label` 為其命名）：把膠囊包裹成多行的群組。同一行中膠囊相距 6 px，行與行相距 8 px。列會依所需行數增高。
2. **膠囊**：高 32 px，圓角為膠囊圓角。圖示前內距 9 px，計數後內距 11 px，圖示與計數之間 6 px。圖示 16 px，計數使用小字級與次要文字顏色。
3. **計數**：回應的人數。數字變化時，只滾動發生變化的位數。
4. **檢視者自己的回應**：設定了 `Reacted` 的膠囊帶有淡色強調填滿與深色強調文字。其邊框會加入一圈強調色，按 45% 混入邊框色，因此在任何表面上都清晰可見。
5. **新增按鈕**（`AddLabel`，預設為「新增回應」）：寬 38 px、高 32 px，顯示一個笑臉加號圖示。選取器開啟時改為顯示關閉圖示。
6. **選取器**（`PickerLabel`，預設為「新增一個回應」）：在新增按鈕上方開啟的托盤，與按鈕相距 8 px。內容內距為 5 px。每個選項是直徑 40 px 的圓形切換按鈕，圖示 20 px；選項下方的說明文字會指出醒目的選項。檢視者已選擇該選項時，說明文字會加上一個勾號。
7. **提示**：膠囊的名稱與提示框，在 `TipDelay`（預設 450 ms）之後出現。它顯示選項標籤；已知 `ReactedBy` 時，還顯示回應的人。

### 值與變更

`Value` 為雙向繫結。每個 `Reaction` 包含一個 id、一個計數和檢視者自己的標記。計數為 0 的回應不會顯示，因此會離開列。id 與任何選項都不符的回應同樣不顯示。同一個 id 在 `Value` 中出現兩次時，以第一項為準。

| 操作 | `Value` 中的結果 | 事件 |
|---|---|---|
| 檢視者點按別人的膠囊 | 計數加 1，`Reacted` 設為 `true` | `ValueChanged`，`Change.Reacted` 為 `true` |
| 檢視者點按自己的膠囊 | 計數減 1，`Reacted` 設為 `false`；計數為 0 時移除該回應 | `ValueChanged`，`Change.Reacted` 為 `false` |
| 檢視者選擇一個還沒有膠囊的選項 | 新增一個計數為 1 的回應，加到清單末尾 | `ValueChanged` |
| 程式碼設定 `Value` | 列顯示新清單；不引發變更 | 無 |

<Demo name="WhoReacted" />

`ValueChanged` 在 `Value` 變更後引發。其 `Next` 屬性是列順序下的新值（不含計數已降為零的回應），`Change` 屬性說明選項名稱以及檢視者是否做出了回應。

```csharp
reactions.ValueChanged += (_, e) =>
{
    // 將 e.Next 與內容一併儲存，並用 e.Change 說明檢視者做了什麼。
};
```

`Toggle(id)` 執行與點按相同的變更，因此鍵盤快速鍵或選單項目可以在程式碼中為檢視者新增或移除回應。`OpenPicker()` 與 `ClosePicker()` 開啟和關閉選取器，`IsPickerOpen` 表示選取器是否開啟。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型持有已儲存的回應，檢視者點按時列會更新它們。每個 `Reaction` 都是不可變記錄，因此檢視模型應取代清單，而不是原地修改某一項。使用 CommunityToolkit.Mvvm：

```csharp
public partial class ReleaseNoteViewModel : ObservableObject
{
    private readonly IReleaseNoteService _notes;
    private readonly string _noteId;

    public ReleaseNoteViewModel(IReleaseNoteService notes, string noteId)
    {
        _notes = notes;
        _noteId = noteId;
    }

    [ObservableProperty]
    private IReadOnlyList<Reaction> _reactionValues = [];

    public async Task SaveReactionsAsync(IReadOnlyList<Reaction> next)
    {
        await _notes.SaveReactionsAsync(_noteId, next);
    }
}
```

```xml
<arc:Reactions Label="對 2026.10 版本的回應"
               Value="{Binding ReactionValues}"
               HorizontalAlignment="Left">
    <arc:ReactionOption Id="ship" Label="已發佈" Icon="{x:Static arc:ArcIcons.Zap}" />
    <arc:ReactionOption Id="love" Label="愛心" Icon="{x:Static arc:ArcIcons.Heart}" />
    <arc:ReactionOption Id="celebrate" Label="慶祝" Icon="{x:Static arc:ArcIcons.Smile}" />
</arc:Reactions>
```

```csharp
// 在檢視的程式碼後置檔中，即檢視模型可用的地方：
reactions.ValueChanged += async (_, e) => await viewModel.SaveReactionsAsync(e.Next);
```

- `Value` 預設為雙向繫結，因此列會在引發 `ValueChanged` 之前把每次點按寫回 `ReactionValues`。請保持雙向繫結；單向繫結會讓列顯示檢視模型從未儲存的變更。
- 列本身不記住檢視者。哪些回應屬於已登入的使用者，由伺服器或檢視模型決定；載入清單時由它設定 `Reacted`。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求簡潔，這裡的範例省略了它）。

### 換行與寬度

列放不下時會把膠囊換行。設定 `Width` 即可看到換行效果，或讓寬度自由，使列占滿父容器的寬度。變更增加一行時，列的高度會以彈簧過渡到新尺寸，膠囊則平滑滑動到新位置。

<Demo name="Wrapping" />

在密集的清單列或卡片頁尾中，只要給列足夠的寬度容納預期數量的選項，就能讓它維持一行。在窄欄中，換行是正確的行為：它讓每個膠囊都可見，無需捲軸。

### 標籤與提示

`Label` 為列命名，`AddLabel` 為新增按鈕及其提示命名，`PickerLabel` 為選取器命名。名稱應說明列指向什麼，例如「對本次發佈的回應」，讓螢幕閱讀器使用者在聽到膠囊之前先聽到情境。

`TipDelay` 設定指標停留在膠囊上多久後才顯示提示，預設為 450 ms。`TipSelector` 為每個膠囊建立提示：它接收選項與回應，回傳要顯示的內容，或回傳 `null` 以使用預設文字。

```xml
<arc:Reactions Label="對本次發佈的回應"
               AddLabel="新增回應"
               TipDelay="0:0:0.6">
    <arc:ReactionOption Id="ship" Label="已發佈" />
    <arc:ReactionOption Id="star" Label="星號" />
</arc:Reactions>
```

提示由選項標籤加上回應者構成（資料中有這些人時）。預設最多列出三個名字，檢視者已回應時「你」排在最前，然後說明還有多少其他人：

| 回應資料 | 提示 |
|---|---|
| 沒有 `ReactedBy` | "愛心" |
| `Reacted` 且有兩個名字 | "愛心 · 你、Ada 和 Lin" |
| 六人，列出四個名字 | "愛心 · Ada、Lin、Sam 等另外 3 人" |

### 誰回應了

在 `Reaction` 上設定 `ReactedBy`，即可說明計數背後的人。名字只顯示在提示框中，不顯示在列裡，因此膠囊保持精簡。按你希望的順序傳入名字，最相關的排在前面。提示框最多顯示三個名字。

名字是選用的。只有計數而沒有名字依然正確，此時提示框會退回顯示選項標籤。

### 即時計數

在別人螢幕上變動的計數透過 `Value` 到達。列只滾動發生變化的位數，計數增加時向上，減少時向下；它不會對圖示播放點按的衝擊效果，因為檢視者並沒有點按。即時更新中出現的新膠囊會進入列，計數降為零的膠囊則會離開列。

<Demo name="LiveCounts" />

伺服器發送更新時，用新清單取代 `Value` 中的清單。列按 `Id` 比對回應，因此留在清單中的膠囊保持其身分，只有計數發生變化。新的 id 會得到新膠囊，被移除的 id 對應的膠囊則會離開列。

### 範例：版本說明中的表情回應

內部變更日誌中的一則版本說明。回應位於摘要下方，團隊無需回覆即可確認這次發佈。列的標籤寫明了發佈版本，計數顯示團隊的反應，檢視者自己的「愛心」會醒目顯示。每個選項名稱都是簡短的動詞或片語，在提示框中讀起來通順。在列上方放一段簡短摘要，讓回應有具體的對象可指。列繫結到上文所示檢視模型的 `ReactionValues`，因此開啟說明時會顯示已儲存的計數。

```xml
<Border Padding="20"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Spacing="12">
        <TextBlock Text="2026.10 版本已上線"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="用量匯出最多快四倍，帳單摘要現在讀取新的發票結構。"
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:Reactions Label="對 2026.10 版本的回應"
                       Value="{Binding ReactionValues}"
                       HorizontalAlignment="Left">
            <arc:ReactionOption Id="ship" Label="已發佈" Icon="{x:Static arc:ArcIcons.Zap}" />
            <arc:ReactionOption Id="love" Label="愛心" Icon="{x:Static arc:ArcIcons.Heart}" />
            <arc:ReactionOption Id="celebrate" Label="慶祝" Icon="{x:Static arc:ArcIcons.Smile}" />
        </arc:Reactions>
    </StackPanel>
</Border>
```

```csharp
// 伺服器在說明載入時回傳已儲存的計數；檢視者自己的回應設定了 Reacted。
viewModel.ReactionValues =
[
    new Reaction("ship", 12),
    new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
    new Reaction("celebrate", 2)
];
```

列按 `Value` 的順序顯示回應，檢視者自己的「愛心」帶有強調色填滿。它的提示框顯示為「愛心 · 你、Ada Okafor、Jonas Berg 以及另外 1 人」：檢視者排在最前，總共最多三個名字，然後是其餘人數。

### 文案規範

- **選項標籤**：一到兩個詞，用人們平常說的方式命名這個回應，例如「讚」「愛心」「慶祝」「已發佈」。標籤會被朗讀並顯示在提示框中，因此避免使用「thumbs_up」或「+1」之類的程式碼。
- **列標籤**：寫明回應所指的內容，例如「對 2026.10 版本的回應」，而不是籠統的「回應」。
- **新增按鈕**：預設為「新增回應」，與選取器的用途相符。除非產品對同一動作使用不同的術語，否則保留預設值。
- **計數用語**：無障礙名稱以數字給出計數（「讚，4 個回應，包括你的」）。不要在列的 `Label` 中重複計數。
- **提示**：標籤加回應者，末尾不加標點。

| 不要寫 | 改為 |
|---|---|
| 回應 | 對 2026.10 版本的回應 |
| +1 | 讚 |
| 點擊回應 | 新增回應 |
| thumbs_up | 讚 |
| 笑臉 | 慶祝 |

### 最佳做法

**建議**

- 為每個選項設定穩定的 `Id`（資料會儲存它），並設定可隨語言變化的 `Label`。更改 id 會建立新的回應，並讓舊計數失去歸屬。
- 載入清單時從已登入使用者的資料中設定 `Reacted`。沒有它，列就無法顯示檢視者自己的回應，第二次點按會增加計數而不是減少計數。
- 在 `ValueChanged` 中儲存變更，或把 `Value` 繫結到會儲存它的檢視模型。未儲存的點按會在重新載入後回復，人與人之間的計數也會對不上。
- 選項集合保持簡短，三到六個。選取器的設計本就精簡，清單太長會讓選擇比點按一下更慢。
- 用 `Label` 為列命名，讓螢幕閱讀器使用者知道這些回應指的是什麼。

**避免**

- 用於需要回覆的選擇。在提案上讓人「同意」的列會隱藏投票背後的理由；應使用留言串或投票。
- 把計數為零顯示為膠囊。零計數依設計會離開列，因此應把零計數存為缺漏。
- 在列已經顯示的清單中原地修改 `Reaction`。記錄是不可變的，應使用 `with` 取代該項並指派新清單，否則列可能不會重新整理。
- 依賴提示來傳達含義。提示在懸停或焦點並經過延遲後才出現，因此選項標籤本身必須已經能單獨表達含義。
- 兩個選項使用相同的標籤。提示和螢幕閱讀器的名稱無法區分它們，檢視者也無法選對。

### 常見問題

**為什麼列裡不顯示某個回應？** 列只顯示計數大於零且 `Id` 與某個選項相符的回應。請檢查選項 id 與回應 id 是否一致，並把計數設為正數。

**如何在程式碼中移除檢視者做出的回應？** 對檢視者已有的回應呼叫 `Toggle(id)`。檢視者自己的回應會被移除，計數減一。對檢視者沒有做出的回應呼叫 `Toggle`，則會為其加一。

**為什麼別人回應時計數沒有變化？** 控制項不會輪詢伺服器。資料變動時（例如來自即時資訊流或重新整理後）更新 `Value`，計數隨即滾動到新值。

**選項的順序會改變膠囊的順序嗎？** 不會。選取器依新增順序顯示選項。列依 `Value` 的順序顯示回應，檢視者新增的回應會加到該清單末尾。

**檢視者自己的回應如何在重新載入後保留？** 除非你的資料儲存了它，否則不會保留。請與內容一同儲存 `Reacted` 或回應的 `ReactedBy` 名字，並在下次開啟頁面時載入到 `Value` 中。

## API 參考

<!-- api: Reactions -->

<!-- api: ReactionOption -->

<!-- api: Reaction -->

### 變更值

`ValueChanged` 傳遞一個 `ReactionChange`，其中包含被變更的回應以及檢視者的新狀態。

| 成員 | 含義 |
|---|---|
| `Id` | 檢視者點按或選擇的選項 id |
| `Reacted` | 檢視者做出回應時為 `true`，移除回應時為 `false` |

## 無障礙

- 整個列是一個由 `Label` 命名的群組。每個膠囊都是切換按鈕，其名稱包含標籤、計數以及檢視者是否在其中，例如「讚，4 個回應，包括你的」。
- 選取器是一個使用漫遊焦點的工具列。四個方向鍵在選項之間移動並循環，Home 與 End 跳到兩端，每個選項都有名稱。
- 在膠囊上按 Enter 或 Space 會切換檢視者的回應。在新增按鈕上按 Enter 或 Space 會開啟選取器，按 Escape 關閉選取器並將焦點返回新增按鈕。
- 每次變更都會透過禮貌等級的狀態區域播報一次，例如「已新增愛心，共 4 個回應。」該訊息不顯示在螢幕上。
- 焦點所在的膠囊離開列時，焦點會移到相鄰的膠囊或新增按鈕，鍵盤焦點永遠不會遺失。
- 懸停只在滑鼠操作時醒目標示選取器中的選項。觸控直接選取選項，觸控使用者無需先懸停。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在膠囊與新增按鈕之間移動 |
| 膠囊上的 `Enter` 或 `Space` | 開啟或關閉檢視者的回應 |
| 新增按鈕上的 `Enter` 或 `Space` | 開啟選取器，並把焦點移到第一個選項 |
| `Right` / `Down`、`Left` / `Up`（在選取器中） | 移到下一個或上一個選項；焦點在兩端處循環 |
| `Home` / `End`（在選取器中） | 移到第一個或最後一個選項 |
| `Enter` 或 `Space`（在選取器中） | 選中該選項，切換檢視者的回應，並把焦點返回新增按鈕 |
| `Escape`（在選取器中） | 關閉選取器，並把焦點返回新增按鈕 |

## 動態效果

- 新膠囊從半尺寸開始，帶著柔和模糊放大出現。被移除的膠囊會縮小並淡出。其他膠囊平滑滑動到新位置。
- 檢視者回應時，圖示會有一個小幅跳動；快速連續點按會疊加，而不是重新開始。
- 計數只滾動發生變化的位數，增加時向上，減少時向下。
- 選取器從新增按鈕中展開，各選項依序出現。
- 減少動態效果時只使用短暫淡入淡出，不使用尺寸彈簧。關閉動態效果時所有變化立即完成。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 膠囊出現 | 從半尺寸與柔和模糊中放大出現，帶有輕快的縮放與快速淡入 | 短暫淡入 | 立即完成 |
| 膠囊減為零時離開 | 縮小到 0.6 並帶輕微模糊地淡出；其他膠囊平滑滑動 | 短暫淡入 | 立即完成 |
| 檢視者點按回應 | 圖示跳動，快速點按會疊加速度 | 無跳動 | 無跳動 |
| 計數變化 | 只滾動發生變化的位數，增加時向上，減少時向下 | 瞬間淡入 | 立即完成 |
| 行數變化 | 列的高度以彈簧變化，膠囊平滑滑動 | 無尺寸彈簧 | 立即完成 |
| 選取器開啟 | 從新增按鈕中展開；選項在 40 ms 後出現，每個間隔 35 ms | 短暫淡入 | 立即完成 |

## 相關元件

- `Badge`：檢視者無法更改的固定計數。
- `Popover` 與 `DropdownMenu`：由按鈕開啟的其他內容或選項。
- `SwipeActions`：觸控清單中的列操作。
- `HoldToConfirm`：不應因誤觸而執行的單一破壞性操作。

| 元件 | 適用情境 |
|---|---|
| `CommentThread` | 回應需要文字，並且留言應保留作者與時間 |
| `ChatThread` | 對話是人與人之間的一系列訊息 |
| `Badge` | 數字固定，檢視者無法更改 |
| `Select` / `MorphSelect` | 人從清單中選擇一個值 |
| `SwipeActions` | 動作屬於觸控清單中的某一列 |
| `Slider` | 人在刻度上設定數量 |
