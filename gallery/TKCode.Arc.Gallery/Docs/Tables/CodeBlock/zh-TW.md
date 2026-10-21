---
title: 程式碼區塊
summary: 以清楚的層級呈現程式碼，並提供便捷的複製入口。
source-hash: 59decc74ec12
---

<Demo name="Default" />

## 何時使用

`CodeBlock` 以唯讀、可選取的框體展示原始檔或程式片段，框體帶有檔名標題列、語法配色與複製操作。它用於說明文件、設定頁與開發者工具中，讓人能夠閱讀、複製程式碼，並知道程式碼屬於哪個檔案，而無需提供編輯器。

- **安裝與設定說明**：入門或整合頁面中的 `dotnet add package` 指令、`.env` 檔或 `docker compose` 檔，標題列中寫明檔名，讓人知道每一行該放在哪裡。
- **產生或匯出的設定**：管理後台中操作員要複製到其他系統的 Webhook 酬載、CI 流程或 Terraform 計畫。複製操作複製的是完整原始碼，而不只是可見的部分。
- **需要摺疊的長檔案**：一份 200 行的部署腳本或產生的 SDK 用戶端只顯示前幾行，並提供「顯示全部」切換，讓設定頁保持易於瀏覽。
- **開發者入口或 API 參考中的片段**：`json` 格式的要求與回應、C# 用戶端呼叫或 `curl` 指令，各自帶有檔名或語言標籤。
- **支援或事件檢視中的程式範例**：工程師與時間軸並排閱讀的失敗查詢或設定差異，帶有行號，團隊可以在討論中引用某一行。

## 何時不用

- **複製單一個值，例如 API 金鑰或 Webhook 密鑰**：單獨使用 `CopyButton`。單一權杖不需要程式碼框體，額外的標題與框架只會喧賓奪主。
- **需要依鍵逐層瀏覽、搜尋或展開的 JSON**：用 `JsonViewer`，它以樹狀呈現並支援搜尋與逐列複製。`CodeBlock` 只把 JSON 當作文字顯示。
- **可導覽的資料夾與檔名層級**：用 `TreeView` 瀏覽檔案，並將 `CodeBlock` 放在旁邊，讓所選檔案決定顯示的程式碼。
- **原地編輯程式碼或文字**：多行純文字用 `Textarea`。`CodeBlock` 是唯讀的，使用者可以選取並複製其中的文字，但不能修改。
- **混合了標題、清單與程式碼的豐富文件**：用 `Markdown`，它會在文件流中以相同的語法配色呈現圍欄程式碼。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CodeBlock FileName="greeting.tsx"
                   Language="tsx"
                   Code="{Binding Source}" />
</UserControl>
```

`CodeBlock` 衍生自 Avalonia 的 `TemplatedControl`，因此擁有控制項常見的版面配置、樣式與自動化行為。它不是 `TextBox`：`Code` 是它唯一接受的文字，文字為唯讀，框體在程式碼區域之外還有標題列與可選的展開列。語法醒目提示使用 StreamMark 引擎的 TextMate 語法，並以 Arc 的語法角色顏色繪製。

### 結構

1. **標題列**（`FileName`、`SourceCodeLabel`、`Language`）：高度至少 52 px，左側距邊緣 16 px，右側 12 px，上下各 8 px。它顯示檔案圖示、檔名、中間點與語言名稱。過長的檔名會以省略號截斷，語言名稱保留其寬度。`FileName` 為空時，標題列顯示 `SourceCodeLabel`（預設為「Source code」）。
2. **複製按鈕**（`CopyLabel`）：標題列末端的 `CopyButton`，距檔案標籤 12 px。它複製 `Code`。
3. **行號欄**（`ShowLineNumbers`）：右對齊的一欄數字，顏色偏淡，並向表面色混合。它對輔助技術隱藏，程式碼橫向捲動時也不會移動。
4. **程式碼區域**：使用 Arc 權杖中的等寬字型，字級 14 px，行高 23.8 px（字級的 1.7 倍），四周內距 20 px。文字可選取。過長的行會在區塊內橫向捲動。
5. **展開切換**（`IsCollapsible`、`MaxLines`、`IsExpanded`）：一列全寬的項目，高度至少 44 px，帶頂部邊框、16 px 的箭頭，標籤為「顯示全部 14 行」（展開時為「收合部分行」）。只有當原始碼行數超過 `MaxLines` 時才會出現。
6. **框體**：1 px 邊框，圓角為面板圓角，表面色背景。框體會裁切其內容，因此標題列與展開列會保持框體的圓角。

窄版配置從寬度低於 420 px 開始（主題中的 `CompactWidth`）：標題列左側內距降至 12 px，複製按鈕只顯示圖示，程式碼字級降至 12 px，行高 20.4 px，內距 16 px，行號也隨之縮小。

### 摺疊長程式碼

`IsCollapsible` 會新增展開列。`MaxLines` 設定摺疊時顯示的行數（預設 12，至少為 1）。行數等於或少於它的原始碼不顯示切換按鈕，因此可以在清單中的每個程式碼區塊上都保持開啟 `IsCollapsible`。

<Demo name="Collapsible" />

`IsExpanded` 為雙向繫結，表示區塊是否已展開。當頁面需要記住狀態時繫結它；當周圍的操作應該展開原始碼時（例如指向第 40 行的搜尋結果），則從程式碼中設定它。

```csharp
block.IsCollapsible = true;
block.MaxLines = 6;
block.IsExpanded = false;
```

摺疊高度等於 `MaxLines` 乘以行高，再加上程式碼區域的內距，因此相同設定下，摺疊的區塊總有相同的高度，與程式碼內容無關。

### 語言

`Language` 設定語法醒目提示以及標題列中的語言標籤。它接受常用名稱以及程式碼圍欄使用的別名，例如 `cs`、`ts`、`py`、`sh`、`yaml`、`json` 與 `axaml`。名稱不區分大小寫，前導點號會被忽略，因此 `.TSX` 與 `tsx` 是同一種語言。預設值為 `tsx`。

沒有語法的語言（例如 `text`）會以無顏色的純文字顯示。記錄檔、筆記與非程式碼的輸出使用它，這樣區塊就不會意外地為其中的字詞上色。

<Demo name="Languages" />

```xml
<arc:CodeBlock FileName="stats.py" Language="py" Code="{Binding PythonSource}" />
<arc:CodeBlock FileName="notes.txt" Language="text" Code="純文字。" />
```

醒目提示器將 TextMate 的詞法顏色對應到九種角色：註解、字串、數字、關鍵字、型別、函式、屬性、標籤與標點。註解為斜體，使用弱化的文字顏色；標點使用次要文字顏色；其他角色在每種外觀下各有自己的顏色。角色顏色不跟隨強調色，因此產品更換強調色時，程式碼的可讀性不會改變。此對應只是對語法類別的近似。少數詞元（例如 JavaScript 物件常值的鍵）可能顯示為純文字，某些語言中的內建型別名稱也會被著色為型別。

### 複製與行號

複製按鈕位於標題列，複製的內容與你設定的 `Code` 完全一致：定位字元與換行符號都與原始碼相同。顯示只在兩方面有所不同：定位字元顯示為兩個空格，歸位加換行顯示為一個換行。`ShowLineNumbers` 會新增行號欄，程式碼橫向捲動時該欄保持不動。

<Demo name="LineNumbers" />

```xml
<arc:CodeBlock FileName="deploy.sh"
               Language="bash"
               ShowLineNumbers="True"
               Code="{Binding Script}" />
```

`Copied` 是路由事件，在複製操作把程式碼放入剪貼簿之後觸發。用它在你自己的狀態區域中確認複製：

```csharp
block.Copied += (_, _) => status.Text = "程式碼已複製";
```

`LineCount` 給出 `Code` 中的行數，可用於「42 行」之類的說明文字。

### 標籤

複製標籤、檔名備用文字與摺疊文字都是屬性，因此可以翻譯，或使用你的產品用語。`ShowAllLinesFormat` 接受一個 `{0}` 佔位符，它會接收行數，並依目前文化格式化。

```xml
<arc:CodeBlock Code="{Binding Source}"
               CopyLabel="複製程式碼片段"
               SourceCodeLabel="程式碼片段"
               ShowAllLinesFormat="顯示全部 {0} 行"
               ShowFewerLinesLabel="收合部分行" />
```

### 切換檔案

同時設定新的 `FileName`、`Language` 與 `Code`，即可就地換入另一個檔案。檔名所在的位置會跟隨新的寬度，新的原始碼向上浮現，高度以彈簧動態跟隨。三個屬性應一起修改，這樣標題列與語法醒目提示就不會出現一個檔案的名稱配著另一個檔案的程式碼。

<Demo name="SwitchingFiles" />

### 窄寬度

寬度低於 420 px 時，內距與文字縮小，複製操作只保留圖示。程式碼區塊依自身寬度調整，而不是視窗寬度，因此窄側邊欄無需任何程式碼即可取得緊湊配置。

<Demo name="Narrow" />

### 資料結構與效能

`CodeBlock` 只保存一個字串。它不會為每個詞元建立一個控制項：整段原始碼是一個文字配置，帶有著色的文字段，因此成本隨原始碼長度增長，而不是隨詞元數量增長。

- 醒目提示會在 `Code` 或 `Language` 變更時執行，而不是在每次重繪時執行。若原始碼不超過 32 KB 且其語法已被使用過，會立即在 UI 執行緒上醒目提示。其他原始碼會先顯示純文字，再由背景處理取得顏色，因此工作階段中第一個長檔案可能會短暫地無顏色顯示。
- 對超過幾個螢幕高的原始碼進行摺疊。摺疊的區塊保持固定高度，其餘原始碼按需展開，因此包含許多長檔案的頁面依然易於瀏覽。
- 每個檔案使用獨立的區塊。區塊是一個可選取的文字配置，因此許多短片段的頁面適合拆分為多個區塊，各自帶有檔名與複製操作。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有原始碼、檔名與展開狀態，程式碼區塊只負責呈現。使用 CommunityToolkit.Mvvm 時，`ObservableObject` 與 `[ObservableProperty]` 即已足夠。

```csharp
public partial class PipelineViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = "deploy.yml";

    [ObservableProperty]
    private string _language = "yaml";

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private bool _isSourceExpanded;
}
```

```xml
<StackPanel Spacing="16">
    <arc:CodeBlock FileName="{Binding FileName}"
                   Language="{Binding Language}"
                   Code="{Binding Source}"
                   IsCollapsible="True"
                   MaxLines="8"
                   IsExpanded="{Binding IsSourceExpanded}" />
</StackPanel>
```

- `Code`、`FileName` 與 `Language` 為單向輸入。在檢視模型中修改它們，程式碼區塊會同時更新標題列、語法醒目提示與摺疊狀態。
- `IsExpanded` 預設為雙向繫結。使用者的切換操作會寫回 `IsSourceExpanded`，因此頁面可以記住狀態，或檢視模型可以在搜尋結果指向區塊時將其展開。
- `Copied` 是事件，而不是命令。在程式碼後置或附加行為中訂閱它，並把回饋保留在檢視模型中，由它負責狀態文字的顯示。

### 範例：設定頁上的部署流程

部署工具中專案設定頁展示專案所用的流程檔。標題列寫明檔名及其語言，行號讓工程師在審查中能夠引用「第 4 行」，原始碼摺疊為三行，完整行數顯示在切換標籤中。複製操作把整個檔案交給需要貼到其他儲存庫的人。下方的安裝指令未設定 `IsCollapsible`，因此不顯示切換按鈕。

![設定頁上帶有行號與摺疊展開列的部署流程檔](images/DeployPipeline.png)

```xml
<StackPanel Width="680" Spacing="16">
    <arc:CodeBlock FileName="deploy.yml"
                   Language="yaml"
                   ShowLineNumbers="True"
                   IsCollapsible="True"
                   MaxLines="3"
                   Code="on: push&#10;jobs:&#10;  build:&#10;    runs-on: ubuntu-latest" />
    <arc:CodeBlock FileName="install.sh"
                   Language="bash"
                   CopyLabel="Copy command"
                   Code="dotnet add package TKCode.Arc" />
</StackPanel>
```

### 文案規範

- **檔名**：程式碼所屬的路徑，即讀者在專案中找到它的方式：`deploy.yml`、`Program.cs`。只有在沒有檔案時才使用描述性標籤，並保持簡短：「Webhook 酬載」「範例回應」。
- **複製標籤**：動詞加物件，句子式大小寫：「複製指令」「複製程式碼片段」。使用你的產品在其他地方對該操作所用的標籤，讓人們能夠認出它。
- **摺疊標籤**：在標籤中保留行數，讓人們在打開前就知道檔案有多長。預設的「顯示全部 14 行」即是如此；翻譯後的格式必須保留 `{0}`。
- **原始碼文字**：讓程式碼以其應當執行的樣子呈現。在程式碼進入頁面前，刪除機密資訊、真實客戶 ID 與內部主機名稱，因為複製操作會讓它們很容易被帶走。

| 不要寫 | 改為 |
|---|---|
| 點選下方複製程式碼 | 複製指令 |
| 程式碼 | 原始碼，或檔名 |
| 顯示更多 | 顯示全部 14 行 |
| 輸出（純文字） | 將 `text` 作為語言，並將檔案命名為 "output.txt" |

### 最佳做法

**建議**

- 為讀者可能複製進檔案的每個區塊設定 `FileName`。標題列告訴人們程式碼應放在哪裡，區塊也因此取得自己的名稱，供輔助技術使用。
- 用 `IsCollapsible` 摺疊長原始碼，並設定 `MaxLines` 顯示人們最需要的部分，例如函式簽章或流程的第一個工作。
- 當人們討論具體的行時（例如審查、事件或支援對話），使用 `ShowLineNumbers`。
- 輸出與記錄檔使用 `text`，這樣區塊就不會為不是程式碼的文字上色。

**避免**

- 用程式碼區塊展示單一個值：金鑰、權杖或 URL 應使用 `CopyButton`，單行外加框體會吸引超過該值應得的注意力。
- 在未設定 `Language` 的情況下把 `Code` 換成另一種語言的範例。新文字會沿用舊語言的語法醒目提示，於是 Python 範例會顯示 C# 的顏色。應同時設定 `FileName`、`Language` 與 `Code`。
- 每次按鍵都從文字編輯器驅動 `Code`。區塊是唯讀檢視，每次變更都會啟動一次新的醒目提示處理；編輯應使用 `Textarea`，只在使用者儲存後才以 `CodeBlock` 顯示結果。
- 期望複製的文字中含有行號。行號欄不屬於 `Code`，因此複製操作永遠不會包含它們。

### 常見問題

**原始碼很長時，為什麼區塊沒有切換按鈕？** 只有當 `IsCollapsible` 為 `True` 且原始碼行數超過 `MaxLines` 時才會出現切換按鈕。兩者都要檢查，並確認 `MaxLines` 大於你預計要隱藏的行數。

**摺疊會改變複製操作複製的內容嗎？** 不會。複製操作會完整複製 `Code`，無論是否摺疊，因此使用者可以從只顯示八行的區塊中複製一個 200 行的檔案。

**為什麼程式碼會在一段時間內沒有顏色？** 當原始碼的語法尚未載入，或原始碼大於 32 KB 時，醒目提示器會在背景處理中為其上色。顏色到達之前，文字以純文字顯示。沒有語法的語言（例如 `text`）始終保持純文字。

**如何在自己的文字中顯示行數？** 讀取 `LineCount`。它在換行符號正規化之後統計 `Code` 的行數，因此結尾帶換行符號的原始碼會比顯示的文字多計一行。

**能修改定位字元的寬度嗎？** 不能透過屬性修改。定位字元始終顯示為兩個空格。當精確寬度很重要時，在原始碼中使用空格，因為複製操作會依你設定的樣子複製原始碼。

## API 參考

<!-- api: CodeBlock -->

## 無障礙

- 程式碼區塊是一個帶名稱的區域。其名稱來自 `FileName`（例如「deploy.yml 原始碼」），或在檔名為空時來自語言（「yaml 原始碼」），因此頁面上的每個區塊都有各自的名稱。
- 程式碼區域名為「可選取的原始碼」。行號欄被標記為原始內容，因此螢幕閱讀器讀取程式碼時會略過行號。
- Tab 依序移到複製按鈕、程式碼區域與展開切換按鈕。
- Enter 或 Space 會複製程式碼或切換展開狀態，取決於目前獲得焦點的控制項。
- 程式碼區域可以獲得焦點，因此鍵盤使用者可以用方向鍵捲動長行。鍵盤焦點會在框體內顯示 2 px 的焦點環；滑鼠點選則不顯示。
- 展開切換按鈕會向輔助技術公開其展開狀態。複製結果由 `CopyButton` 播報。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在複製按鈕、程式碼區域與展開切換按鈕之間移動 |
| 複製按鈕上的 `Enter` 或 `Space` | 將 `Code` 複製到剪貼簿 |
| 展開切換按鈕上的 `Enter` 或 `Space` | 展開或收合原始碼 |
| 程式碼區域中的方向鍵 | 捲動原始碼；長行橫向捲動 |
| 程式碼區域中的 `Ctrl+C`（macOS 上為 `Cmd+C`） | 複製選取的文字 |

## 動態效果

- 展開或收合時，程式碼的高度以平滑的彈簧變化。
- 新程式碼會淡入並輕微上升，使更新清楚可見，而不會突然跳動。
- 展開切換按鈕的文字以短暫的模糊切換，箭頭隨之轉動。
- 檔名以短暫的模糊切換，其後的語言標籤滑動到新的位置。
- 減少動態效果時，高度立即設定，文字原地切換，沒有移動。關閉動態效果時直接到最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 展開或收合 | 高度以彈簧變化到新尺寸，程式碼區域裁切至該尺寸 | 高度立即設定 | 立即 |
| 新的 `Code` | 文字在淡入的同時上升 4 px | 文字原地替換 | 立即 |
| 切換標籤 | 標籤模糊地淡出後淡入，箭頭在快速彈簧上轉動 180 度 | 標籤原地切換，箭頭立即轉動 | 立即 |
| 新的 `FileName` | 名稱模糊地淡出後淡入，語言標籤滑動到新位置 | 名稱原地切換 | 立即 |

## 相關元件

- `CopyButton`：複製單一個值，例如權杖或 URL。
- `JsonViewer`：使用者需要搜尋與瀏覽的 JSON。
- `TreeView`：放在程式碼區塊旁邊，讓所選檔案決定顯示的程式碼。
- `Markdown`：混合了說明文字與圍欄程式碼的文件。

| 元件 | 適用情境 |
|---|---|
| `CopyButton` | 要複製的是一個值，而不是檔案或片段 |
| `JsonViewer` | 使用者需要搜尋、展開或複製 JSON 文件的一部分 |
| `TreeView` | 檔案構成層級，顯示的程式碼取決於所選內容 |
| `Markdown` | 程式碼嵌在說明文字中，例如說明文章或變更紀錄 |
| `Textarea` | 文字是純文字，使用者需要編輯它 |
