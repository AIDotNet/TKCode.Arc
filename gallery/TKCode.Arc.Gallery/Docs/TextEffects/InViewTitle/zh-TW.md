---
title: 可視範圍內標題
summary: 標題隨捲動進入可視範圍時逐步呈現。
source-hash: 40e209892575
---

<Demo name="Variants" />

## 何時使用

可視範圍內標題是長篇頁面上的分節標題。它在有 45% 進入可視範圍之前一直隱藏，然後以五種樣式之一呈現。每個分節在讀者看到它時才出現，因此長頁面有了有節奏的進場效果，而無需為每個標題個別編排動畫時間軸。它適合人們從上到下閱讀的頁面：功能概覽、產品導覽、年度報告、安全或合規頁面。

- **帳單產品概覽**：「發票與付款條款」、「稅務與加值稅」和「付款失敗」作為長篇著陸頁上的分節標題。每個標題在讀者捲動到它時呈現，頁面因此有節奏感，而應用程式中無需任何動態效果的工作。
- **客戶成功回顧**：季度報告，每章以標題開始。預設的 `Blur` 樣式適合編輯式標題：讀者看到的是平靜的進場，而不是一陣動態效果。
- **說明中心或新手引導**：當讀者捲回某一步時，分節標題應使用 `IsReplayEnabled` 重新播放，讓他們回到的那個標題清楚可見。
- **帶固定斷行的定價或方案比較**：兩行的標題，例如 "Pay for the seats you use." 與 "Add more when your team grows."。搭配 `Lines` 使用 `Line` 變體，可在任何寬度下保持這些斷行。

## 何時不用

- **頁面載入時出現的首個標題**：用 `TextReveal`。可視範圍內標題要等讀者捲動到它才開始，因此頁面頂端的大標題會在讀者已經看到空白之後才開始出現。
- **持續進行的狀態文字，例如「正在產生發票摘要」**：用 `TextShimmer`。它在工作進行期間循環播放；可視範圍內標題只播放一次，然後保持靜止。
- **內文、標籤、表頭或長段落**：用一般的 `TextBlock`。逐字效果是為標題設計的，逐字出現的段落讀起來很慢。
- **會就地變化的值，例如狀態詞或方案名稱**：用 `TextMorph`，它會把字母從舊標籤移動到新標籤。可視範圍內標題不會為 `Text` 的變更播放動畫。
- **用來展開和收合內容的標題**：用 `Accordion`，它為標題提供開關，並與之保持內容狀態。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InViewTitle Text="模糊效果逐字變清晰"
                     Variant="Blur" />
</UserControl>
```

`InViewTitle` 衍生自 Avalonia 的 `Control`，並自行繪製整個標題。它不會為每個字詞或每行建立子控制項，因此一個有四十個分節標題的頁面只持有四十個控制項，而不是數千個。它依文字的尺寸量測，因此可以像任何標題一樣放進 `StackPanel` 或 `Grid`。它以 `Text` 作為自身名稱，並由 `Level` 設定標題層級，因此螢幕閱讀器在頁面大綱中看到的是真正的標題。

呈現從標題有 45% 可見時開始。頁面開啟時已可見的標題會立即開始；位於首屏以下的標題會等到讀者捲動到它時才開始。預設變體為 `Blur`。

### 結構

1. **標題文字**（`Text`）：整個標題，依變體繪製為一組字詞、行或字母。它的文字同時也是無障礙名稱。
2. **標題層級**（`Level`）：設定字型大小與標題層級。`H1` 使用 4XL 展示字級，`H2`（預設）使用 3XL，`H3` 使用 2XL，`Paragraph` 使用 XL。四個層級都使用主題中的展示字型家族、中等字重與展示字距。
3. **行框**（`Leading`）：行高為字型大小的 1.1 倍，取自標題權杖。因此兩行標題約為 2.2 個字級的高度。
4. **前景**（`Foreground`）：標題顏色。主題從前景權杖設定它；未設定筆刷時，標題以黑色繪製。
5. **明確分行**（`Lines`）：`Line` 變體的選用行清單。未設定時，整個 `Text` 視為一行。
6. **起始模糊**（`BlurRadius` 與 `SoftBlurRadius`）：`Blur` 變體中每個字詞的起始模糊（`BlurRadius`），以及 `Tracking` 變體中的起始模糊（`SoftBlurRadius`）。主題從模糊權杖同時設定兩者。

### 選擇變體

`Variant` 選擇呈現樣式。不透明度先上升，模糊在該樣式有模糊時隨後清除，位移最後穩定，因此標題在完全到位之前就已可讀。頁面上的大多數標題應使用同一種變體。

| 變體 | 移動內容 | 單元間距 | 穩定耗時 | 適用情境 |
|---|---|---|---|---|
| `Blur`（預設） | 每個字詞從模糊中變清晰，並上升 0.2em | 60 ms，長標題會縮短 | 0.8 s | 大多數分節標題 |
| `Word` | 每個字詞從各自的剪裁區中上升 0.9em | 60 ms，長標題會縮短 | 0.8 s | 剪裁式上升符合版面的標題 |
| `Line` | 每一行從其剪裁區中上升 | 每行 80 ms | 0.86 s | 帶固定斷行的大標題（需要 `Lines`） |
| `Tracking` | 緊密的字距展開，同時每個字詞清除柔和模糊 | 每個字母 32 ms | 0.9 s | 短而醒目的開場，例如單行的首屏區域 |
| `Wipe` | 柔邊遮罩從左到右揭開標題 | 無：整體為一個單元 | 0.9 s | 用作分隔線的單一短語 |

```xml
<arc:InViewTitle Text="文字從剪裁區中升起" Variant="Word" />
<arc:InViewTitle Text="柔和的擦除揭開標題" Variant="Wipe" />
```

逐字錯開的間隔為每字 60 ms，但標題字數較多時會縮短，使整個錯開時長保持在 0.4 s 以內。因此十個字詞的標題，每個字詞在上一個字詞之後 40 ms 開始。長標題請選擇 `Blur` 或 `Word`：錯開時間短，標題很快就完整呈現。

只對短標題使用 `Tracking`。字母會在標題的整個寬度上展開，因此長標題在移動時看起來被拉伸。`Wipe` 要少用：它的遮罩會被看作橫跨整個標題的一個動作，每頁用一次很有效，若每個分節都這樣做就會令人疲倦。

### 標題層級

`Level` 設定標題層級（`H1`、`H2` 或 `H3`），並同時設定對應的 `AutomationProperties.HeadingLevel`。預設為 `H2`。請依頁面大綱選擇層級，而不是依想要的字級。頁面通常有一個作為標題的 `H1`，分節用 `H2`，分節內的子節用 `H3`。`Paragraph` 不設定標題層級，因此只用於不屬於大綱的簡短引導文字。

### 明確分行

`Lines` 為 `Line` 變體設定各行。各行依給定內容顯示，不會重新排版。每一行在最窄的螢幕上都必須放得下，因此請以頁面支援的最小寬度檢查最長的一行。未設定 `Lines` 時，整個 `Text` 為一行。

```csharp
title.Variant = InViewTitleVariant.Line;
title.Lines = ["逐行升起，", "不再重新排版"];
```

只有當斷行本身承載含義時才設定 `Lines`，例如標題把論斷與其限定語配成一對。若文案應自然換行，請只用 `Text` 搭配 `Word` 或 `Blur`。

### 每次進入都重播

`IsReplayEnabled` 會在標題離開可視範圍時以快速淡出隱藏它，且沒有錯落效果。每次標題重新進入時都會再次播放。預設值為 `false`，因此標題只播放一次，然後保持可見。

```xml
<arc:InViewTitle Text="每次進入都會重播"
                 IsReplayEnabled="True"
                 Level="H3" />
```

對讀者會捲回去的標題使用重播，例如長篇設定指南中，讀者在後續步驟時會返回的某一步。頁面上的其他標題請保持關閉，因為每次捲動都淡出再升起的標題，會與讀者正在閱讀的內容爭奪注意力。

### 由資料驅動

`InViewTitle` 只負責顯示：文字來自檢視模型，呈現效果自行執行。繫結 `Text`，並在 XAML 中保留標題層級與變體。

```csharp
public partial class BillingOverviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _invoicesTitle = "發票與付款條款";

    [ObservableProperty]
    private string _failuresTitle = "付款失敗";
}
```

```xml
<StackPanel Spacing="48">
    <arc:InViewTitle Text="{Binding InvoicesTitle}" Variant="Blur" />
    <arc:InViewTitle Text="{Binding FailuresTitle}" Variant="Blur" Level="H3" />
</StackPanel>
```

變更 `Text` 會更新文字，但不會再次播放呈現效果。進場動畫屬於標題進入可視範圍的那一刻。請在每個頁面上只設定一次文案。若標題的文字會隨頁面中的選擇而變化，例如目前開啟的發票名稱，一般標題會更合適。

### 節制地使用動態效果

分節標題正是這種效果發揮作用的地方：讀者已經在捲動，而標題標示著一個新觀點的開始。動態效果很短（不到一秒），並在讀者開始閱讀其下的段落之前結束。

當頁面上有許多動畫各異的標題、當長清單的設定中每次捲動都重播標題，或當標題位於人們需要掃視數字的密集儀表板中時，動態效果就會分散注意力。對於這類頁面，請保持預設的 `Blur`，關閉 `IsReplayEnabled`，讓內容來承載頁面。

減少動態效果會去掉所有這些效果。當動態效果層級設為 `Reduced` 或 `Off` 時，標題會立即顯示最終狀態，沒有位移、模糊或遮罩。請用 `arc:ArcMotion.Level` 在視窗或容器上設定層級，標題會繼承該層級。

### 範例：帳單產品功能頁

帳單產品的功能頁面有一個純文字頁面標題，以及其下的兩個分節：發票如何開立，以及付款失敗如何重試。兩個分節標題都使用 `Blur` 與 `H2`，使頁面從第一節到最後一節都保持同一節奏。每個標題下的段落是一般文字，因此標題穩定後讀者便可立即閱讀。每個標題都是真正的 `H2`，這使螢幕閱讀器看到的大綱保持正確。

![帳單功能頁上的兩個分節標題，下方為段落](images/InViewTitleFeature.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Billing automation"
               FontSize="14"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:InViewTitle Text="Invoices and payment terms" Variant="Blur" />
    <TextBlock Text="Acme Analytics issues invoices on the first business day of each month and sends the net 30 terms with every one."
               TextWrapping="Wrap"
               FontSize="15"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:InViewTitle Text="Failed payment retries" Variant="Blur" />
    <TextBlock Text="A declined card is retried on days 3, 7 and 14. The account owner receives a notice before the last attempt."
               TextWrapping="Wrap"
               FontSize="15"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
</StackPanel>
```

### 文案規範

分節標題應點明主題並保持簡短。請寫成句首大寫的名詞片語，結尾不加句號，並盡量在頁面寬度下保持一行。標題是分節的標籤，因此其下的段落承載說明。

| 不要寫 | 改為 |
|---|---|
| "INVOICES & PAYMENTS" | "發票與付款條款" |
| "這裡說明我們如何處理你寄出的發票。" | "發票" |
| "準備好開始了！"（附驚嘆號） | "開始你的第一張發票" |
| "付款失敗處理系統概覽" | "付款失敗" |
| "進一步了解加值稅。" | "稅務與加值稅" |

### 最佳做法

**建議**

- 頁面上的大多數標題使用同一種變體。`Blur` 作為預設值是有原因的：它在每個分節都顯得平靜。
- 依頁面大綱設定 `Level`：一個 `H1`，分節用 `H2`，子節用 `H3`。螢幕閱讀器和大綱都依賴它。
- 只有當斷行是資訊的一部分時才使用 `Lines`，並在發布前於 360 px 寬的螢幕上檢查每一行。
- 保持標題簡短。一個折成四行的標題會延遲其下的段落，並削弱節奏。
- 在頁面的整個生命週期內保持文案不變。變更文案不會重播呈現效果，因此這種變化不會被宣告。

**避免**

- 在同一頁面混用 `Word`、`Line`、`Tracking` 和 `Wipe`。每種變體都有自己的時間安排，混用會顯得雜亂而非有結構。
- 為每個標題都設定 `IsReplayEnabled`。重播是為讀者會回頭看的標題準備的；若每個標題都重播，它會與內容爭奪注意力。
- 在長標題上使用 `Tracking`。字母會在整個寬度上展開，因此長標題在移動時看起來被拉伸。
- 必須立刻讀到的標題，例如表單上方的警告。標題在 45% 可見之前一直隱藏，因此警告應使用 `Alert` 或一般文字列。
- 把標題當作狀態列或計數器使用。對於會改變狀態的文案，請使用 `TextShimmer` 或 `TextMorph`。

### 常見問題

**為什麼我捲動到標題時它沒有出現？** 當標題有 45% 位於可捲動的可視範圍之內時，呈現才會開始。從未顯示過的標題，例如位於未選取分頁中的標題，要等它被顯示並捲動進入可視範圍後才會呈現。

**頁面開啟時，長頁面頂端附近的標題已經可見。會怎樣？** 掛載時已可見的標題會立即開始呈現。讀者已經捲動越過的標題會直接顯示最終狀態而沒有動態效果，因此在頁面中段開啟頁面的讀者，不會看到標題在他們身後播放進場效果。

**讀者捲回上方時，標題會再次播放嗎？** 只有設定 `IsReplayEnabled="True"` 才會。預設只播放一次。開啟重播後，標題離開可視範圍時會快速淡出，再次進入時完整呈現一遍。

**如何更改單一標題的字型？** 在控制項上設定 `FontFamily`、`FontSize`、`FontWeight` 或 `Foreground`。這些本機值只覆寫該標題的主題設定。若想要與大綱相符的標題字級，請改為設定 `Level`。

**能否為單一標題單獨關閉動態效果？** 不能單獨關閉。動態效果層級是繼承的，因此在視窗或容器上設定 `arc:ArcMotion.Level="Reduced"`（或 `Off`），其下的每個標題都會立即顯示最終狀態。

## API 參考

<!-- api: InViewTitle -->

## 無障礙

- 標題以其完整文字作為名稱，並由 `Level` 設定 `HeadingLevel`。`Paragraph` 標題不設定標題層級。
- 控制項會回報文字角色。動畫部分對輔助技術隱藏，因此只朗讀文字。
- 頁面開啟時已捲動越過的標題會立即顯示最終狀態。
- `Text` 的修改不會被播報。需要播報變化時，請在父元素上使用即時區域。
- 本控制項無法取得焦點，也沒有鍵盤互動，因此不會為頁面增加焦點停駐點。

## 動態效果

- 標題在 45% 可見時開始呈現。不透明度先行，模糊隨後清除，位移最後穩定。
- `Word` 與 `Line` 從剪裁區中升起，`Wipe` 從左到右揭開，`Tracking` 展開字距，`Blur` 使每個字詞變清晰。
- 啟用 `IsReplayEnabled` 後，離開可視範圍是快速淡出，沒有錯落效果。
- 減少動態效果時標題立即顯示，沒有位移、模糊或遮罩。關閉動態效果時同樣如此。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 標題到達 45% 可見 | 各單元依變體的順序在 0.8 到 0.9 s 內呈現 | 立即顯示 | 立即顯示 |
| 掛載時標題已捲動越過 | 立即顯示 | 立即顯示 | 立即顯示 |
| 啟用 `IsReplayEnabled` 時標題離開可視範圍 | 快速淡出，沒有錯落效果（約 180 ms） | 保持可見 | 保持可見 |
| 啟用 `IsReplayEnabled` 時標題重新進入 | 再次呈現 | 立即顯示 | 立即顯示 |

## 相關元件

- `TextReveal`：頁面載入時只播放一次的首個大標題。
- `TextShimmer`：表示正在進行工作的狀態文字。

| 元件 | 適用情境 |
|---|---|
| `TextReveal` | 標題是頁面上的第一個元素，出現時應播放 |
| `TextShimmer` | 狀態列顯示仍在進行的工作 |
| `TextMorph` | 短標籤改變狀態，字母應從舊文字移動到新文字 |
| `Accordion` | 標題開啟和收合其下方的內容 |
| `Alert` | 警告或訊息必須立即閱讀，且不需要呈現效果 |
