---
title: 展開式按鈕群組
summary: 緊湊排列的圖示按鈕群組：指向或聚焦的按鈕會展開顯示文字，相鄰按鈕讓位，動作會在原地確認。
source-hash: 626e9599af5b
---

<Demo name="MessageActions" />

## 何時使用

`ExpandingButtonGroup` 會把三到六個相關動作放進一個緊湊的膠囊中。總有一個動作顯示文字，指標或鍵盤所到之處的動作會接手這段文字，相鄰的動作則讓位。它適用於空間緊湊、而動作又很常見的介面：使用者能很快學會圖示，只在需要時才看到文字。

- **訊息與留言動作**：支援收件匣或團隊聊天中，訊息下方的回覆、封存和刪除。目前展開的文字說明了動作，封存確認時會在原地顯示「已封存」。
- **檔案動作**：文件清單或檔案瀏覽器中，對選取檔案的下載、分享和刪除，並使用與表格密集列相符的 `Small` 尺寸。
- **卡片動作**：儀表板或專案看板上的貼文或工作卡片，例如讚、留言和發布。其中最可能是下一步的動作在靜止時即展開。
- **儲存與確認流程**：編輯器工具列中，儲存動作可以在片刻內以「已儲存」確認，然後文字回到靜止狀態，讓使用者知道變更已被保存。

## 何時不用

- **需要保持選取的選擇**，例如在清單、看板和日曆之間切換檢視：用 Toggles 分類中的 `SegmentedControl`，它會顯示並保留選取狀態。
- **一兩個動作且空間充足**：用附文字的 `Button`，因為文字無需被發現。
- **超過約六個動作**：用 Menus 分類中的 `DropdownMenu`，它會以文字列出這些動作。
- **需要始終顯示文字的圖示動作**，例如新使用者必須讀懂的共用工具列：用 `FloatingButtonGroup` 或 `ButtonGroup`，它們的文字始終可見。

## 用法

每個動作都是一個 `ExpandingButtonGroupItem`。群組需要一個 `Label`，它就是無障礙名稱。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExpandingButtonGroup Label="訊息動作">
        <arc:ExpandingButtonGroupItem Label="回覆" />
        <arc:ExpandingButtonGroupItem Label="傳送" DoneLabel="已傳送" />
        <arc:ExpandingButtonGroupItem Label="封存" DoneLabel="已封存" />
    </arc:ExpandingButtonGroup>
</UserControl>
```

`ExpandingButtonGroup` 衍生自 `ItemsControl`，因此它的項目就是 `ExpandingButtonGroupItem` 容器。每個項目都衍生自 Avalonia 的 `Button`：`Command`、`Click` 和 `IsEnabled` 的行為與按鈕一致，而群組在此之上加入展開、確認、忙碌狀態與鍵盤模型。

### 結構

1. **膠囊**（`Label` 為其命名）：1 px 邊框，使用表面顏色與膠囊圓角，並有 3 px 的內縮。各動作之間相距 2 px。`Label` 是工具列的名稱，不會繪製出來。
2. **收合的動作**：在 Medium 尺寸下為 36 px 的正方形（`Small` 下為 28 px），圖示為 16 px（`Small` 下為 14 px）。圖示距左邊緣 10 px（`Small` 下為 7 px）。
3. **展開的文字**：文字接在圖示之後，間距 7 px（`Small` 下為 5 px），末端有 14 px 的內縮（`Small` 下為 11 px）。展開的動作以柔和的表面色填滿，文字使用前景色。
4. **危險色調**（`Tone="Danger"`）：展開時，文字使用危險色，並在表面之上疊加 10% 的危險色底色（深色模式下為 14%）。
5. **確認**（`DoneLabel`）：圖示變為勾號，文字變為完成詞，並在表面之上疊加 10% 的成功色底色（深色模式下為 14%）。完成詞預留的空間與兩個詞中較長的那個相同。
6. **焦點框**：只在鍵盤取得焦點時出現的框。
7. **停用的動作**（`IsDisabled`）：變暗，文字使用次要文字顏色。它仍可取得焦點，也仍會展開，讓使用者看到它會做什麼。

### 圖示與靜止時的動作

在程式碼或 `x:Static` 中把 `Icon` 設為 16 × 16 的 Lucide 圖形。群組在靜止時只顯示一段文字：預設是第一個可用的動作。`DefaultExpandedItem` 可以指定另一個項目；當該項目被停用或不在群組中時，群組會退回到第一個可用的動作。

<Demo name="RestingAction" />

```csharp
reply.Icon = ArcIcons.Reply;
archive.Icon = ArcIcons.Archive;

var comment = new ExpandingButtonGroupItem { Label = "留言", Icon = ArcIcons.MessageCircle };
var card = new ExpandingButtonGroup
{
    Label = "卡片動作",
    DefaultExpandedItem = comment
};
```

靜止時的動作應選擇最可能是下一步的那個。在一張要求審閱的卡片上，審閱動作靜止時即是展開的，因此即使沒有指標，卡片的用途也一目了然。

### 確認

`DoneLabel` 是動作之後顯示的過去式文字。對於開啟內容的動作，請不要設定它。確認會保持 `ConfirmDuration` 指定的時長，預設為 1.4 秒；之後該項目收合，除非指標或鍵盤仍停留在它上面。群組會以禮貌方式朗讀完成詞，重複執行的動作會再次朗讀。

```xml
<arc:ExpandingButtonGroupItem Label="儲存" DoneLabel="已儲存" />
```

### 非同步動作

`AsyncAction` 會傳回 `Task<bool>`。在工作結束之前，該項目處於忙碌狀態，圖示會在允許的動態效果等級下脈動，重複觸發會被忽略。傳回 `false` 或拋出例外都會略過確認。沒有非同步動作時，`Click` 和 `Command` 視為成功。

```csharp
var archive = new ExpandingButtonGroupItem
{
    Label = "封存",
    Icon = ArcIcons.Archive,
    DoneLabel = "已封存",
    AsyncAction = async () =>
    {
        await archiveService.ArchiveAsync();
        return true;
    }
};
```

### 尺寸與語氣

`Size` 設定整個群組的尺寸。`Medium`（預設）使用 36 px 的動作與 16 px 的圖示。`Small` 使用 28 px 的動作、14 px 的圖示與較小的文字尺寸，適合密集的表格列。`Large` 按 `Medium` 處理。

`Tone="Danger"` 會以危險色顯示展開的破壞性動作。停用的項目仍可取得焦點並顯示文字，但不會執行。

<Demo name="Small" />

### 事件

`ActionInvoked` 先於該項目自身的 `AsyncAction` 引發，並且每個執行的動作都會引發它。`e.Item` 是即將執行的動作。對於內嵌項目，`e.Data` 是項目本身；對於 `ItemsSource`，它是資料物件。

```csharp
message.ActionInvoked += (_, e) => log.Text = $"動作：{e.Item.Label}";
```

### 繫結到檢視模型

頁面上的動作集合通常是固定的，因此在 XAML 中定義這些項目，並把它們的命令與非同步動作繫結到檢視模型。工作由檢視模型負責，群組只回報狀態。

```csharp
public interface ITicketService
{
    Task<bool> ArchiveAsync(string ticketId);

    Task<bool> DeleteAsync(string ticketId);
}

public partial class TicketViewModel : ObservableObject
{
    private readonly ITicketService _tickets;

    public TicketViewModel(ITicketService tickets)
    {
        _tickets = tickets;
    }

    [ObservableProperty]
    private string _ticketId = "tkt_2026_0412";

    public Func<Task<bool>> ArchiveAction => () => _tickets.ArchiveAsync(TicketId);

    public Func<Task<bool>> DeleteAction => () => _tickets.DeleteAsync(TicketId);

    [RelayCommand]
    private void Reply()
    {
        // 為 TicketId 開啟回覆編輯器。
    }
}
```

```xml
<arc:ExpandingButtonGroup Label="工單動作">
    <arc:ExpandingButtonGroupItem Label="回覆"
                                  Icon="{x:Static arc:ArcIcons.Reply}"
                                  Command="{Binding ReplyCommand}" />
    <arc:ExpandingButtonGroupItem Label="封存"
                                  Icon="{x:Static arc:ArcIcons.Archive}"
                                  DoneLabel="已封存"
                                  AsyncAction="{Binding ArchiveAction}" />
    <arc:ExpandingButtonGroupItem Label="刪除"
                                  Icon="{x:Static arc:ArcIcons.Trash2}"
                                  DoneLabel="已刪除"
                                  Tone="Danger"
                                  AsyncAction="{Binding DeleteAction}" />
</arc:ExpandingButtonGroup>
```

- 這裡沒有雙向繫結。項目只需設定一次，而 `ExpandedItem`、`IsExpanded`、`IsDone` 和 `IsBusy` 都是唯讀屬性，用於回報狀態，供樣式或測試使用。
- `Command` 作為動作執行，並視為成功，因此不會失敗的命令無需 `AsyncAction`。
- 需要回傳結果的命令應放在 `AsyncAction` 中，因為只有 `Task<bool>` 才決定是否顯示確認。

### 指標、觸控與鍵盤

群組會展開使用者正在操作的那個動作，並跟隨目前正在使用的輸入方式。

- **滑鼠**：文字移動到指標下的動作。指標必須先進入相鄰的項目 4 px，展開才會移動，因此動作不會在靜止的指標下滑過時搶走焦點。指標離開群組時，展開會回到已確認的動作、已聚焦的動作或靜止的動作。
- **觸控**：沒有懸停。第一次點按展開該動作，第二次點按執行它。已展開的動作會立即執行。在群組外點按會收合由第一次點按開啟的文字。
- **鍵盤**：焦點會展開取得焦點的動作。整個群組只佔一個 Tab 停駐點，方向鍵在各動作之間移動，因此展開始終跟隨焦點。

### 版面與寬度

每個動作的高度固定，收合時的寬度為正方形。展開後的寬度為圖示、間距與 `Label` 和 `DoneLabel` 中較長者之和。文字區域從一開始就預留了較長的那個詞，因此確認時寬度不會改變。群組的寬度會隨展開的動作變化而增減，相鄰的動作會讓出空間。

將群組放在其起始邊，並為最寬的展開狀態留出空間。對於五個 Medium 尺寸、文字較短的動作，應預留約 260 到 280 px。如果群組位於一個經常重新排版的大型容器中，應把它放在獨立的一列，因為展開位置變化時寬度也會隨之變化。

### 範例：支援收件匣中的工單動作

支援人員閱讀工單中的一則訊息，動作群組位於該訊息下方。回覆因為是最可能的下一步而保持展開。封存在工作完成後以「已封存」確認，刪除使用危險色調，因此它展開時的狀態明顯不同。該群組與其所作用的文字靠左對齊。

![一則展開了回覆動作、並帶有兩個收合動作的支援工單訊息](images/TicketActions.png)

```xml
<StackPanel Width="600" Spacing="14">
    <Border Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
            Padding="20">
        <StackPanel Spacing="12">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="Maya Chen"
                           FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="21 Oct 2026, 09:14"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}"
                           VerticalAlignment="Center" />
            </StackPanel>
            <TextBlock Text="The March invoice still shows the old seat count. Can you check it before the 28th?"
                       TextWrapping="Wrap"
                       FontSize="{arc:ArcDesignTokenResource TypeBase}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:ExpandingButtonGroup Label="Ticket actions" Margin="0,4,0,0">
                <arc:ExpandingButtonGroupItem Label="Reply"
                                              Icon="{x:Static arc:ArcIcons.Reply}" />
                <arc:ExpandingButtonGroupItem Label="Archive"
                                              Icon="{x:Static arc:ArcIcons.Archive}"
                                              DoneLabel="Archived" />
                <arc:ExpandingButtonGroupItem Label="Delete"
                                              Icon="{x:Static arc:ArcIcons.Trash2}"
                                              DoneLabel="Deleted"
                                              Tone="Danger" />
            </arc:ExpandingButtonGroup>
        </StackPanel>
    </Border>
</StackPanel>
```

訊息與寄件者已經提供了上下文，因此三個動作只需圖示即可。回覆的文字可見，是因為它是靜止時展開的動作，在指標移動之前就告訴了支援人員這個群組能做什麼。

### 文案規範

- **標籤**：簡短的動詞，句首大寫，例如「回覆」「封存」或「儲存」。即使收合時，標籤仍是無障礙名稱，因此必須能獨立讀懂。
- **完成詞**：動詞的過去式，一個詞，例如「已封存」或「已傳送」。它大約顯示一秒半，因此必須一眼就能看懂。
- **圖示**：選擇使用者已經能與該動作聯想在一起的圖形，並在整個產品中保持一致。

| 不要寫 | 改為 |
|---|---|
| 封存此訊息 | 封存 |
| 完成！ | 已封存 |
| 點擊傳送 | 傳送 |
| 標記為已儲存 | 已儲存 |

### 最佳做法

**建議**

- 把最可能的下一步動作放在最前面，並讓它靜止時保持展開，使群組在指標移動之前就能說明自己。
- 對破壞性動作使用 `Danger` 色調，使展開狀態體現其後果。
- 當工作可能失敗時，為每個項目設定 `AsyncAction`。傳回 `false` 或拋出例外會略過確認，因此失敗的封存絕不會聲稱成功。
- 保持三到六個動作。少於三個時用按鈕更好；超過六個則展開內容難以掃視。

**避免**

- 在群組中放置選擇類控制項。會記住選擇的群組屬於 `SegmentedControl`，把兩者混在一起會讓展開與選取相互混淆。
- 過長的文字。比圖示長得多的文字會在每次展開時加寬群組，使這一列的移動超出使用者的預期。
- 沒有說明就停用使用者最需要的項目。被停用的項目仍可取得焦點並展開，但使用者仍需要在周圍的文字中看到原因。
- 只依賴完成詞來確認重要的變更。勾號與文字大約持續 1.4 秒，因此重要的結果也應在頁面中確認。

### 常見問題

**為什麼第一個可用的動作在靜止時顯示文字？** 這是預設的靜止動作。第一個被停用的項目會被略過，`DefaultExpandedItem` 可以覆寫這個選擇。如果該項目不在群組中，群組會退回到第一個可用的動作。

**為什麼我的動作執行時沒有確認？** 要嘛它沒有 `DoneLabel`，要嘛它的 `AsyncAction` 傳回了 `false`，要嘛它拋出了例外。只有帶 `DoneLabel` 的工作傳回 `true` 時才會顯示確認。

**能否從程式碼執行動作，例如鍵盤快速鍵？** 在群組上呼叫 `ActivateAsync(item)`。它會像按下一樣執行該動作；對於被停用、忙碌或正在確認的項目則不做任何處理。

**確認之後群組會收合嗎？** 會，在 `ConfirmDuration` 之後收合，除非指標或鍵盤焦點仍在該項目上。對同一項目的重複動作會再次顯示它的完成詞。

## API 參考

<!-- api: ExpandingButtonGroup -->

<!-- api: ExpandingButtonGroupItem -->

## 無障礙

- 群組的角色為 ToolBar，名稱由 `Label` 提供。即使文字處於收合狀態，完整的 `Label` 仍是名稱。
- 整個群組只佔一個 Tab 停駐點。左右方向鍵在各動作之間移動並循環。Home 與 End 分別移到第一個和最後一個動作。
- Enter 與 Space 執行目前聚焦的動作。動作忙碌時，重複觸發會被忽略。
- 停用的動作仍保有鍵盤焦點，並回報為無法使用。
- 完成詞會以禮貌方式朗讀，重複確認時會再次朗讀。
- 收合的文字對輔助技術隱藏，因此名稱只會被讀一次，而不是兩次。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 進入群組時落在上次聚焦的動作或靜止的動作上；再按一次 Tab 離開該群組 |
| `Left` / `Right` | 移到上一個或下一個動作並展開它；到達兩端時循環 |
| `Home` / `End` | 移到第一個或最後一個動作 |
| `Enter` / `Space` | 執行目前聚焦的動作；動作忙碌時被忽略 |

## 動態效果

- 聚焦或懸停的動作以彈簧展開，顯示其文字，相鄰動作隨之讓位。
- 文字從圖示後方滑出，伴隨短暫的淡入與輕微模糊；收合時收回得更快。
- 確認時圖示繪出勾號，文字切換為完成詞：新詞升起，舊詞移出。
- 指標需要先進入相鄰的項目幾個像素，文字才會移動，因此指標靜止時群組不會跳動。
- 減少動態效果時，文字與寬度會以快速淡入同時變化，勾號直接出現而不繪製。關閉動態效果時進行同樣的變化，但沒有任何過場。
- 忙碌的動作會讓圖示脈動。減少動態效果時，圖示保持在 60% 不透明度，而不是脈動。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 展開 | 寬度在圖示與圖示加文字之間以彈簧變化；相鄰動作讓位 | 文字與寬度以快速淡入同時變化 | 立即 |
| 文字 | 帶模糊地從圖示後方滑出：進入 240 ms（有短暫延遲），退出 120 ms | 快速淡入 | 立即 |
| 確認 | 勾號在 240 ms 內繪出；舊詞上移退出，新詞同時升起 | 勾號直接出現而不繪製；文字淡入 | 立即 |

## 相關元件

- `FloatingButtonGroup`：獨立的柔和按鈕，文字始終顯示在托盤中。
- `ButtonGroup`：把相關動作拼接在同一個表面上，以分隔線區分。
- `ActionButton`：單一非同步提交，按鈕上顯示等待與成功狀態。
- `DropdownMenu`：較長的指令清單。

| 元件 | 適用情境 |
|---|---|
| `SegmentedControl` | 始終有一個選項處於選取狀態，例如檢視或篩選條件 |
| `Button` | 一兩個動作有空間顯示文字 |
| `ButtonGroup` | 需要可見文字的相關動作，拼接成一個表面 |
| `FloatingButtonGroup` | 獨立的柔和按鈕，文字在托盤中始終可見 |
| `ActionButton` | 單一提交需要自己的等待與成功狀態 |
| `DropdownMenu` | 命令超過約六個，或命令需要完整文字 |
