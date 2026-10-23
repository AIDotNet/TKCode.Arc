---
title: 簽名板
summary: 筆跡平順，速度越快越細；支援復原、重播，並可匯出 PNG 或 SVG。
source-hash: 589b36028e3d
---

<Demo name="Sign" />

## 何時使用

`SignaturePad` 為需要在應用程式內簽署的文件採集手寫簽名。它在畫板上繪製墨跡，並自帶工具列（顏色、筆尖、復原、重做、重播、清除與匯出），保留筆畫以便複核與修正簽名，並將結果匯出為圖片或向量檔案，由紀錄保存。

- **銷售或帳單主控台中的訂單與合約**：客戶在通話結束時用平板簽署訂購單。墨跡匯出為 PNG，與簽署人姓名一併歸檔，姓名由表單另外保存。
- **物流應用程式中的簽收確認**：司機在手持裝置螢幕上為包裹簽收。簽名以 PNG 儲存在運送紀錄上，細筆尖讓墨跡在小畫板上依然清晰。
- **診所或病患登記應用程式中的同意書**：病患簽署治療同意書。SVG 匯出在以任何尺寸列印時都保持清晰。
- **金融科技開戶流程中的確認書**：客戶簽署揭露聲明，筆畫隨帳戶一併儲存，因此可以用 `Reset` 再次顯示同一個簽名。
- **現場服務驗收**：技術人員簽署工單，並在紀錄關閉前用復原修正一筆。

## 何時不用

- **輸入姓名即可**：用 `Input`，它以文字保存姓名，無需繪製。
- **只需一個明確的確認，無需保留簽名**：用 `HoldToConfirm`，它需要按住確認，只記錄決定。
- **已經是檔案的已簽署文件**：用 `FileDropzone` 附加該檔案，例如簽名服務回傳的 PDF。
- **對條款的同意與否**：用 `Checkbox`，並在附近連結條款。手繪簽名增加了條款並不需要的一步。
- **長篇備註或文件本文**：用 `RichTextEditor`，它編輯文字，不採集簽名。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SignaturePad Signer="王小明"
                      Hint="請在此簽名" />
</UserControl>
```

`SignaturePad` 衍生自 `TemplatedControl`，自行繪製墨跡，因此它不是文字方塊，沒有插入點，也沒有文字。墨跡位於畫板單位中，即寬高比為 600:260 的座標空間，畫板會把該空間縮放到其寬度。筆畫是一般資料（`InkStroke` 記錄，包含其點、顏色、筆尖與開始時間），因此同一個簽名無需螢幕點陣圖即可匯出、儲存與重播。

### 結構

1. **畫板**：繪圖畫布。在任何寬度下都保持 600:260 的寬高比，因此寬 560 px 的畫板約高 243 px。它的圓角與面板圓角一致，並有一條 1.5 px 的虛線基線，從寬度的 7% 延伸到 93%，位於高度的 72% 處。一個 X 標記簽名線。
2. **提示**（`Hint`，預設為「在此簽名」）：基線上方的大型提示文字，在第一筆落下前顯示。開始繪製時淡出，清除後重新出現。
3. **簽署人**（`Signer`）：基線下方列印的姓名，使用超小文字尺寸。它只繪製在畫板上；匯出內容只包含墨跡，因此應將姓名與紀錄一併儲存。
4. **墨跡**：筆畫。黑色墨跡跟隨前景色，藍色與紫色墨跡是隨淺色和深色模式變化的主題值。匯出使用固定的列印顏色，因此 PNG 在任一主題下都相同。
5. **顏色群組**：三個 36 px 選項（`Black`、`Blue`、`Violet`），為單選群組。
6. **筆尖群組**：三個 36 px 選項（`Fine`、`Medium`、`Bold`）。工具列分成兩組，分別對齊兩端，寬度不足時換行。
7. **操作**：復原、重做與重播為 36 px 的膠囊圖示按鈕（重播執行時圖示切換為停止），然後是「另存為」標籤下的匯出按鈕（PNG 與 SVG），以及清除按鈕。匯出成功後，匯出按鈕會在短時間內顯示「已儲存」。
8. **狀態**：即時區域，播報每一次變化，例如「已復原」或「簽名已清除。復原可將其恢復。」

### 墨水顏色與筆尖

`InkColor`（預設 `Black`）與 `InkWidth`（預設 `Medium`）設定新筆畫的預設值。每一筆都保留繪製時所用的顏色與筆尖，因此之後更改選擇不會重繪先前的筆畫。

<Demo name="InkAndNib" />

需要列印或作為合約歸檔的簽名使用黑色。藍色或紫色墨跡可以讓第二位簽署人或見證人在同一頁上區分開。小型手持畫板用 `Fine`，大多數表單用 `Medium`，大螢幕上用手指書寫的自助終端用 `Bold`。筆尖以畫板單位設定寬度（3.4、5.6 與 8.4），因此粗筆畫在小畫板上依然保持比例。

裝置回報壓力時，筆壓決定墨跡的寬度。滑鼠或手指沒有壓力，因此畫板會根據筆畫的速度模擬壓力：快速的筆畫細，慢速的筆畫飽滿。

### 讀取筆畫

每一筆之後，以及復原、重做、清除與重設之後，都會執行 `StrokesChanged`。`Strokes` 為唯讀，以畫板單位保存筆畫。第一筆之前以及清除之後，`IsEmpty` 為 true；`CanUndo` 與 `CanRedo` 回報歷程狀態。

```csharp
pad.StrokesChanged += (_, e) =>
{
    saveButton.IsEnabled = e.Strokes.Count > 0;
};
```

每個 `InkStroke` 都有 `Id`、一組 `InkPoint` 值（`X`、`Y`、`Pressure`，取值 0 到 1，無壓力時為 -1，以及 `Time`，即距筆畫起點的毫秒數）、`Color`、`Width` 與 `At`（筆畫開始的時間）。

### 匯出

`ExportPng` 與 `ExportSvg` 會傳回裁切到墨跡邊界的內容。`ExportPng` 接受選用的 `scale` 參數（預設 3），用來放大畫板的解析度；`ExportBitmap` 會傳回同一張圖像的 `RenderTargetBitmap`，供預覽使用。`SaveAsync` 會開啟系統的儲存對話方塊，並以 `FileName`（預設為「簽名」）作為建議的檔名。當使用者取消、沒有任何筆畫或寫入失敗時，它傳回 `false`。

<Demo name="UseTheSignature" />

```csharp
var png = pad.ExportPng();
var svg = pad.ExportSvg();

var saved = await pad.SaveAsync(SignatureFileKind.Png);
```

`ExportSvg` 在沒有筆畫時會傳回空字串，因此在儲存匯出結果之前應檢查 `IsEmpty`。列印時使用 `SignatureFileKind.Svg`，用於電子郵件或 PDF 頁面時使用 `SignatureFileKind.Png`。

### 保存與還原簽名

`Reset` 會取代筆畫，不播放清除動畫，並清空復原歷程，因此從儲存空間載入的簽名不會被視為一次編輯。傳入先前保存的筆畫即可重新顯示；不帶參數呼叫則會重新開始。

```csharp
// 儲存：筆畫是一般記錄，任何序列化器都可以使用。請使用應用程式自己的選項；當應用程式被裁剪或以 AOT 發佈時，使用原始碼產生的內容脈絡。
var json = JsonSerializer.Serialize(pad.Strokes);

// 還原：畫板顯示已儲存的墨跡，且歷程為空。
pad.Reset(JsonSerializer.Deserialize<List<InkStroke>>(json));
```

儲存的筆畫保留其畫板單位，因此在 560 px 畫板上儲存的簽名在 400 px 畫板上也能正確顯示。

### 繫結到檢視模型

簽名是檢視模型儲存的值，畫板只負責採集。透過 `StrokesChanged` 讀取筆畫，因為 `Strokes` 是唯讀的，無法反向繫結。`Signer`、`Hint`、`InkColor` 與 `InkWidth` 是單向設定：由檢視模型設定，畫板不會將其寫回。

```csharp
public partial class SignOffViewModel : ObservableObject
{
    private readonly ISignatureStore _store;

    [ObservableProperty]
    private string _signerName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _hasSignature;

    public IReadOnlyList<InkStroke> Strokes { get; private set; } = Array.Empty<InkStroke>();

    public SignOffViewModel(ISignatureStore store) => _store = store;

    public void OnStrokesChanged(IReadOnlyList<InkStroke> strokes)
    {
        Strokes = strokes;
        HasSignature = strokes.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(HasSignature))]
    private async Task SubmitAsync()
    {
        await _store.SaveAsync(SignerName, Strokes);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:Input Label="列印姓名" Text="{Binding SignerName, Mode=TwoWay}" />
    <arc:SignaturePad x:Name="Pad"
                      Signer="{Binding SignerName}"
                      Hint="簽名以接受訂單" />
    <arc:Button Content="接受並簽署"
                Command="{Binding SubmitCommand}"
                IsDefault="True" />
</StackPanel>
```

```csharp
// Code behind: 檢視把畫板的事件連接到檢視模型。
Pad.StrokesChanged += (_, args) => vm.OnStrokesChanged(args.Strokes);
```

- `Signer` 是單向的：列印的姓名會隨使用者輸入而跟隨姓名欄位。更改姓名不會清除筆畫。
- 應監聽 `StrokesChanged`；`IsEmpty`、`CanUndo` 與 `CanRedo` 是唯讀的，用來更新檢視的狀態。
- 畫板沒有送出命令。送出應使用檢視模型的命令，啟用狀態則使用畫板的 `IsEmpty` 或 `HasSignature` 旗標。

### 驗證

簽名在使用者送出時檢查，畫板為空時提示文字保持可見。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 存在簽名 | 送出時 | 表單自己的訊息；畫板為空時提示文字保持可見 |
| 已填寫列印姓名 | 送出時，或離開姓名欄位時 | 姓名欄位的錯誤列，來自表單自己的規則 |
| 再次顯示已儲存的簽名 | 紀錄開啟時 | 使用已儲存的筆畫呼叫 `Reset`，不增加復原步驟 |

這些規則屬於表單而非畫板，因此訊息使用表單自己的元件。表單開啟期間保持送出按鈕可用，並在使用者按下後顯示「未簽名」的訊息。在畫板有墨跡之前停用它，會讓使用者無從得知這種狀態的原因。

### 範例：訂購單確認

客戶在銷售通話結束時簽署訂購單。標題與一句話摘要說明接受的是什麼，畫板的提示用平實的語言重述該動作，簽署人姓名列印在簽名線下方，讓客戶看清這條簽名線屬於誰。在此情境中畫板沒有任何筆畫，因為它是在客戶簽署之前顯示的。

![訂購單，包含摘要、一個帶提示的空白簽名板，以及列印的簽署人姓名](images/OrderFormSignature.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Accept order form"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind Logistics agrees to the 12-month Analytics Pro plan at 4,800 USD per month, billed quarterly."
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:SignaturePad Signer="Maya Chen, Head of Finance"
                      Hint="Sign to accept the order" />
    <arc:Button Content="Accept and sign" />
</StackPanel>
```

### 文案規範

- **提示**：用平實的語言說明動作，例如「簽名以接受訂單」。避免使用「簽名處」，因為使用者不知道這個簽名接受的是什麼。
- **簽署人**：列印法定姓名與職務，例如「陳美雅，財務主管」，並將同樣的文字與紀錄一併儲存，因為匯出的墨跡中不包含姓名。
- **送出按鈕**：使用動詞加受詞，例如「接受並簽署」「簽署協議」。避免使用「完成」，因為它沒有說明確認的是什麼。
- **狀態訊息**：說明發生了什麼變化以及接下來可以做什麼，例如「簽名已清除。復原可將其恢復。」

| 不要寫 | 改為 |
|---|---|
| 簽名 | 簽名以接受訂單 |
| 請在此簽名！！ | 在此簽名 |
| 完成 | 接受並簽署 |
| 錯誤：未簽名 | 送出前請加入你的簽名。 |

### 最佳做法

**建議**

- 為每個簽名將 `Signer` 設為法定姓名，讓簽署人清楚是誰在簽署，並將該姓名與筆畫一併儲存。
- 將 `Strokes` 與紀錄一併儲存，並用 `Reset` 重新載入，而不是只儲存畫板的圖片。
- 對於以多種尺寸列印的文件使用 `ExportSvg`，對於電子郵件與螢幕顯示使用 `ExportPng`。
- 為簽署人保留復原歷程，並在基線可見的寬度下顯示畫板。
- 送出時檢查 `IsEmpty`，並在使用者修正簽名時保留表單資料。

**避免**

- 儲存空的匯出結果。沒有筆畫時 `SaveAsync` 傳回 false，而空的 SVG 不是簽名。
- 依賴顏色來傳達含義。藍色與紫色墨跡只是視覺選擇，列印的紀錄需要姓名。
- 在表單中無理由地讓簽名變為可選。應說明它是否必填以及原因。
- 在使用者仍在繪製時用 `Reset` 取代筆畫。Reset 會立即清空歷程與墨跡。
- 在狹窄的欄中顯示畫板。基線與工具列群組會換行，狹窄的畫板很難簽名。

### 常見問題

**為什麼 Delete 會清除畫板，使用者能取回嗎？** Delete 與 Backspace 會以擦除動畫清除畫板。清除是一個復原步驟，因此 `Ctrl+Z`（macOS 上為 `Cmd+Z`）可以把筆畫恢復。

**為什麼我匯出的 PNG 是空的？** `ExportPng` 會裁切到墨跡，沒有筆畫時就沒有可匯出的內容。檢查 `IsEmpty`，或使用 `SaveAsync` 的結果（畫板為空時它傳回 false）。

**更改墨跡顏色會重繪既有的筆畫嗎？** 不會。每一筆都保留繪製時所用的顏色與筆尖。`InkColor` 與 `InkWidth` 只為新筆畫設定選擇。

**滑鼠或觸控時壓力如何運作？** 觸控筆會回報壓力，壓力直接決定寬度。滑鼠或手指不回報壓力，畫板會根據速度推算寬度。

**如何再次顯示簽名，例如作為重播？** 呼叫 `Replay()` 以原有節奏重繪筆畫，大約需要 3.2 秒；呼叫 `StopReplay()`（或按 Escape）停止重播。使用 `Reset(strokes)` 可以顯示已儲存的筆畫而不重播。

## API 參考

<!-- api: SignaturePad -->

## 無障礙

- 畫板是一個可聚焦的影像，名稱為「簽名板」。其無障礙名稱包含筆畫數量；沒有墨跡時，描述為「簽名板。空。請用指標、觸控筆或手指書寫。」
- 鍵盤快速鍵寫在畫板的說明文字中，因此畫板取得焦點時螢幕閱讀器會播報它們。
- 墨水顏色與筆尖是單選群組。只有被選取的選項在 Tab 順序中，方向鍵在各選項之間移動。
- 工具列按鈕帶有名稱與工具提示。筆畫、復原、重做、清除、重播與匯出結果都會以禮貌方式播報，因此使用者能聽到每一次變化而不會失去焦點。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Ctrl+Z`（macOS 上為 `Cmd+Z`） | 畫板取得焦點時復原上一次變化 |
| `Ctrl+Shift+Z`（macOS 上為 `Cmd+Shift+Z`），或 `Ctrl+Y` | 重做 |
| `Delete` 或 `Backspace` | 以擦除動畫清除畫板；清除可以復原 |
| `Escape` | 停止正在進行的重播 |
| 顏色或筆尖群組中的 `Left` / `Right`（或 `Up` / `Down`） | 移到上一個或下一個選項並選取它 |
| `Tab` / `Shift+Tab` | 在畫板、各群組與工具列按鈕之間移動 |

## 動態效果

- 清除時，一片刮板橫掃畫板，在約半秒內將墨跡一併刮去。
- 重播會依原有節奏重繪每一筆，筆尖處帶有一個圓點，整個重播約在 3.2 秒內完成。筆畫之間的停頓有上限。
- 提示文字在上浮的同時淡入，顏色與筆尖的醒目提示會滑動到所選選項。
- 減少動態效果時，取消擦除與模糊效果，改用短暫的淡入淡出。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 清除 | 刮板橫掃，約 0.56 秒內刮去墨跡 | 墨跡立即清除 | 立即 |
| 重播 | 筆畫以原有節奏在約 3.2 秒內重繪 | 筆畫以其節奏重繪 | 重播立即結束 |
| 提示出現或消失 | 淡入並上浮；開始繪製時模糊淡出 | 約 120 ms 的短暫淡入淡出 | 立即 |
| 顏色或筆尖選擇變化 | 醒目提示滑動到所選選項 | 醒目提示直接跳到該選項 | 立即 |
| 重播與停止圖示 | 圖示縮放並切換 | 圖示切換 | 立即 |

## 相關元件

- `Input`：與手寫簽名並列或代替手寫簽名時，用於輸入姓名。
- `HoldToConfirm`：無需簽名的確認動作。
- `FileDropzone`：附加已簽署文件等檔案。

| 元件 | 適用情境 |
|---|---|
| `Input` | 姓名是輸入的，且不需要手繪簽名 |
| `HoldToConfirm` | 按住確認的決定即為紀錄，不保留簽名 |
| `Checkbox` | 使用者以是或否的選擇接受條款 |
| `FileDropzone` | 必須附加其他服務已簽署的檔案 |
| `RichTextEditor` | 內容是使用者撰寫的文字，而不是簽名 |
