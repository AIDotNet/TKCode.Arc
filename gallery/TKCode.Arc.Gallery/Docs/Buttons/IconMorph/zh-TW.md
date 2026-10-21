---
title: 圖示變形
summary: 在不同狀態之間變形的圖示：選單變關閉、播放變暫停、複製變勾選等。
source-hash: 3289aa0bf5d6
---

<Demo name="EightPairs" />

## 何時使用

`IconMorph` 繪製產品中用於狀態切換的八組圖示：選單變為關閉圖示、播放按鈕變為暫停、複製圖示變為勾選。它位於按鈕之內，以使用者看到的方式呈現狀態變化，因此這一變化讀起來是一個操作，而不是兩個圖示互相替換。它適用於應用程式中的工具列、頁首和列，在這些地方使用者只看圖示。

- **維運儀表板中的即時資料頁首**：播放與暫停控制項可在使用者閱讀尖峰時凍結指標資料流。圖示變形，讓使用者看到資料流已經停止。
- **事故或訂單頁面中的通知開關**：為跟進單一事故的團隊，開啟通知時鈴鐺響一下，關閉時鈴鐺被劃線。
- **設定或帳單清單中的展開控制項**：展開發票群組或篩選面板時，箭頭轉動，使該列無需另外的標籤即可顯示其狀態。
- **SaaS 應用程式外殼中的導覽切換**：窄視窗中打開側邊導覽時，選單圖示變為關閉圖示。
- **共用報告上的喜歡或收藏**：隊友標記某份報告有用時，愛心以一次短促的迸發填滿。

## 何時不用

- **帶確認的複製操作**：用 `CopyButton`，它已經會變形圖示並播報結果。
- **帶文字的淺色與深色切換**：用 `ThemeSwitcher`，它同時承載文字和狀態。
- **從不改變狀態的圖示**：在按鈕中使用 `ArcIcons` 中的一般 Lucide 圖示。沒有第二個狀態的變形沒有意義。
- **僅以圖示作為控制項**：`IconMorph` 是裝飾性的，從不取得焦點，因此應放在 `Button` 或其他有名稱和狀態的控制項中。
- **一個圖示背後的多個操作選單**：用帶觸發器的 `DropdownMenu`。變形只表示某個東西是打開還是關閉。

## 用法

`IconMorph` 繪製八組圖示中的一組。`Kind` 選擇圖示組，`IsActive` 選擇第二個狀態。把它放進按鈕中，並為按鈕命名。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Variant="Ghost" AutomationProperties.Name="開啟選單">
        <arc:IconMorph Kind="Menu" />
    </arc:Button>
</UserControl>
```

`IconMorph` 衍生自 Avalonia 的 `Control`。它在 `Render` 中以 24 × 24 的格線繪製圖形，因此沒有範本，也沒有可以重新設定樣式的部件。它的 `IsActive` 屬性預設是雙向的，這使它很適合繫結到檢視模型屬性。該控制項不處理指標或鍵盤輸入：點擊與按鍵會交給承載它的按鈕。

### 結構

1. **圖形**（`Kind`）：一個繪製的形狀，位於 24 單位格線上，在非啟用與啟用形態之間變形。
2. **線條**（`StrokeThickness`）：格線上預設為 1.75。它隨 `IconSize` 縮放，因此圖示在 16 px、24 px 和 32 px 時都保持相同的粗細。
3. **尺寸**（`IconSize`）：渲染後的像素尺寸，預設為 24。主題設定了相同的預設值。
4. **顏色**（`Foreground`）：與文字前景色一樣從父層繼承，因此幽靈按鈕會用自己的文字顏色為圖示上色。
5. **喜歡顏色**（`LikeBrush`）：已喜歡的愛心的顏色。主題將其設為危險色。
6. **瞬間效果**（Moment）：鈴鐺的響鈴，以及愛心的光環與火花。每次圖示被開啟時，瞬間效果播放一次；在其他狀態下沒有可見部分。

### 圖形種類

`Menu` 變為關閉，`Play` 變為暫停，`Plus` 變為勾選，`Copy` 變為勾選，`Theme` 變為月亮，`Chevron` 轉動，`Bell` 開啟與關閉通知，`Heart` 表示喜歡。每組的啟用狀態是其名稱中的第二項：加號變為勾選，鈴鐺開啟，愛心被喜歡。

```xml
<arc:IconMorph Kind="Play" />
<arc:IconMorph Kind="Theme" IsActive="True" />
<arc:IconMorph Kind="Heart" IsActive="{Binding IsLiked}" />
```

選擇圖形組時依據的是狀態的意義，而不是圖示的外觀。`Plus` 變為 `Check` 適合「加入清單」這類操作，其結果是一個已勾選的項目。`Copy` 變為 `Check` 適合複製操作。`Play` 變為 `Pause` 用於使用者可以開始和停止的資料流或片段，而不用於單向操作。

### 切換狀態

將 `IsActive` 繫結到按鈕的狀態。在點擊處理常式中翻轉它，並更新按鈕的名稱以符合新的意義。

```csharp
var icon = new IconMorph { Kind = IconMorphKind.Play };
var button = new Button { Content = icon, Variant = ButtonVariant.Ghost };
button.Click += (_, _) =>
{
    icon.IsActive = !icon.IsActive;
    AutomationProperties.SetName(button, icon.IsActive ? "暫停" : "播放");
};
```

### 尺寸與線寬

`IconSize` 設定渲染後的像素尺寸，預設為 24。`StrokeThickness` 在 24 單位格線上設定，並隨 `IconSize` 縮放，因此預設的 1.75 線寬在任何尺寸下都保持比例。

<Demo name="Sizes" />

```xml
<arc:IconMorph Kind="Menu" IconSize="16" />
<arc:IconMorph Kind="Menu" IconSize="32" StrokeThickness="2" />
```

讓圖示與按鈕相配。16 px 圖示適合 36 px 的按鈕，24 px 圖示適合 44 px，32 px 圖示適合 52 px 的大型主按鈕。24 px 圖示放在 28 px 的按鈕中會顯得擁擠，16 px 圖示放在 52 px 的按鈕中則顯得空泛。

### 箭頭方向

對於 `Chevron`，`ChevronPointing` 設定靜止時的方向，`ChevronTurn` 設定啟用時轉動的角度（單位為度）。值為 180 時由下翻轉為上，值為 90 時由右轉為下。

<Demo name="ChevronAndMoments" />

對向下展開的區域使用 `Down` 加 180 度轉動，對向側邊展開的列（例如樹狀節點）使用 `Right` 加 90 度轉動。同一清單中的所有箭頭保持相同方向，這樣展開的列與未展開的列就容易區分。

### MorphGlyph

`MorphGlyph` 是路徑變形基本元件：它可以在任意兩段路徑資料之間變形。選單變關閉會保留其頂點，關閉變勾選則依弧長重新取樣。

<Demo name="MorphGlyph" />

對於八組圖示未涵蓋的形狀，使用 `MorphGlyph`。用 `MorphTo` 提供第二個形狀，它會從目前輪廓開始做動畫。在程式碼中需要知道變形何時完成時，使用 `Morph`。

```csharp
var glyph = this.FindControl<MorphGlyph>("StatusGlyph")!;
await glyph.MorphTo("M4 12.5l5 5L20 6.5");
```

### 繫結到檢視模型

儀表板檢視模型擁有即時狀態，頁首只負責顯示。`IsActive` 為雙向繫結，因此圖示既能顯示狀態，也能改變狀態。

```csharp
public partial class LiveMetricsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveToggleName))]
    private bool _isPaused;

    public string LiveToggleName => IsPaused ? "恢復即時更新" : "暫停即時更新";

    [RelayCommand]
    private void ToggleLive() => IsPaused = !IsPaused;
}
```

```xml
<arc:Button Variant="Ghost"
            Width="36"
            Height="36"
            Padding="0"
            AutomationProperties.Name="{Binding LiveToggleName}"
            Command="{Binding ToggleLiveCommand}">
    <arc:IconMorph Kind="Play" IsActive="{Binding IsPaused}" IconSize="18" />
</arc:Button>
```

- `IsActive` 是雙向的。檢視模型中的變化會移動圖示，圖示的變化會寫入新狀態。只把它繫結到擁有該狀態的屬性；第二個寫入者會使變形來回翻轉。
- 按鈕的名稱來自檢視模型，因此螢幕閱讀器讀到的是下一次按下將執行的操作，而不是圖示。
- 命令負責執行變化。圖示跟隨狀態，但自身不執行操作。

### 節制地使用動態效果

變形是狀態變化的回饋，因此凡是使用者需要看到某物發生了變化的地方，它都有幫助。若同一變化連續重複多次，或圖示就是操作的全部訊號，它就會分散注意力。

- **有幫助**：改變下一次操作意義的單一切換，例如暫停與繼續，或選單與關閉。
- **會分散注意力**：清單中為每一項都會觸發的鈴鐺，或訊息流中使用者連續喜歡多則貼文時的愛心。迸發效果會在每次按下時播放。瞬間效果只保留給單次、有意的操作。
- **減少動態效果**會立即完成每次變形，並跳過鈴鐺的響鈴和愛心的迸發。也要在沒有動態效果的情況下測試狀態變化：圖示和按鈕名稱仍必須告訴使用者發生了什麼。

### 範例：事故頁首控制項

維運主控台中的事故頁面顯示事故的即時狀態，以及三個控制項：暫停即時更新、開啟或關閉通知，以及展開受影響服務的清單。每個控制項都是幽靈按鈕，名稱與按鈕所處的狀態相符，圖示變形使狀態無需標籤即可看見。頁首保留一個選單圖示用於較少使用的操作，其名稱寫成它所打開的操作。

![事故頁首，包含暫停控制項、鈴鐺、收合的箭頭與選單圖示](images/IncidentHeader.png)

```xml
<StackPanel Width="600" Spacing="12">
    <Border Padding="20"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
        <StackPanel Spacing="14">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Incident INC-2026-0412"
                               FontSize="16"
                               FontWeight="SemiBold"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Checkout latency above 900 ms since 14:05 UTC"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <StackPanel Grid.Column="1"
                            Orientation="Horizontal"
                            Spacing="4"
                            VerticalAlignment="Center">
                    <arc:Button Width="36"
                                Height="36"
                                Padding="0"
                                Variant="Ghost"
                                AutomationProperties.Name="Pause live updates">
                        <arc:IconMorph Kind="Play" IsActive="True" IconSize="18" />
                    </arc:Button>
                    <arc:Button Width="36"
                                Height="36"
                                Padding="0"
                                Variant="Ghost"
                                AutomationProperties.Name="Turn notifications off">
                        <arc:IconMorph Kind="Bell" IsActive="True" IconSize="18" />
                    </arc:Button>
                    <arc:Button Width="36"
                                Height="36"
                                Padding="0"
                                Variant="Ghost"
                                AutomationProperties.Name="Open incident actions">
                        <arc:IconMorph Kind="Menu" IconSize="18" />
                    </arc:Button>
                </StackPanel>
            </Grid>
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="Affected services (3)"
                           FontWeight="Medium"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <arc:Button Width="28"
                            Height="28"
                            Padding="0"
                            Variant="Ghost"
                            AutomationProperties.Name="Collapse affected services">
                    <arc:IconMorph Kind="Chevron" IsActive="True" IconSize="16" />
                </arc:Button>
            </StackPanel>
            <StackPanel Spacing="6">
                <TextBlock Text="Checkout API: p95 1.2 s, error rate 4.8%"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Text="Payment gateway: p95 640 ms, error rate 0.3%"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Text="Session service: healthy"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </StackPanel>
    </Border>
</StackPanel>
```

### 文案規範

- **按鈕名稱**：按下後執行的操作，用動詞加物件，句首大寫的句子式書寫：「暫停即時更新」「關閉通知」「收合受影響的服務」。狀態變化時名稱隨之變化。
- **僅圖示的按鈕**：名稱是螢幕閱讀器得到的唯一文字，因此必須完整。不要用圖示來命名按鈕（「播放圖示」）。
- **圖示對**：同一操作在整個應用程式中都使用相同的圖示對，這樣播放控制項總是表示播放。

| 不要寫 | 改為 |
|---|---|
| 播放 | 暫停即時更新（執行期間） |
| 選單 | 開啟事故操作 |
| 點選展開 | 展開受影響的服務 |
| 鈴鐺圖示 | 開啟通知 |
| 喜歡按鈕 | 喜歡（喜歡後改為「取消喜歡」） |

### 最佳做法

**建議**

- 把圖示放進按鈕中，並由按鈕提供名稱。圖示是裝飾性的，只有按鈕可以被聚焦和命名。
- 將 `IsActive` 繫結到檢視模型所擁有的狀態，由命令執行操作。
- 每一列使用一種圖示尺寸，並與按鈕尺寸相符。
- `IsActive` 變化時總是更改按鈕的名稱，使名稱與圖示永不互相矛盾。

**避免**

- 把變形用於沒有第二個狀態的單向操作。永遠不會還原的複製操作不需要變形，`CopyButton` 已經會確認它。
- 在使用者會連續多次按下的控制項上使用愛心或鈴鐺迸發。重複的瞬間效果會變成雜訊。
- 同時在圖示上設定 `IsActive`，又繫結一個會把它寫回的繫結。狀態會翻轉兩次，變形會卡頓。
- 只依賴顏色變化。形狀也會變化；保留名稱，使狀態在沒有顏色的情況下也清楚。

### 常見問題

**為什麼我的圖示顏色不變？** `IconMorph` 與文字一樣從父層繼承 `Foreground`。在按鈕或圖示上設定顏色，或使用主題的幽靈按鈕顏色。喜歡的愛心使用 `LikeBrush`，而不是 `Foreground`。

**為什麼點擊圖示沒有切換它？** 圖示不參與命中測試，因此點擊會交給承載它的按鈕。應把點擊處理常式或命令放在按鈕上。

**第二次開啟鈴鐺時還會響嗎？** 會。每當 `IsActive` 變為 `True`，響鈴就播放一次；關閉時鈴鐺被劃線。

**如何顯示八組之外的狀態？** 使用帶有自己路徑資料的 `MorphGlyph`，或使用 `ArcIcons` 中的一般圖示並在程式碼中切換。

## API 參考

<!-- api: IconMorph -->

`IconMorphKind` 列出八組圖示，順序與 `Kind` 的值一致：`Menu`、`Play`、`Plus`、`Copy`、`Theme`、`Chevron`、`Bell` 與 `Heart`。`IconMorphChevronPointing` 有兩個值：`Down`（預設）與 `Right`。

## 無障礙

- 圖示對輔助技術隱藏。周圍的按鈕提供名稱。
- 在按鈕上顯示狀態：按下狀態用切換，展開狀態用展開標記，或改變名稱。
- 意義變化時更改名稱，例如從「播放」變為「暫停」。
- 圖示不取得焦點、不參與命中測試，也不處理按鍵。周圍的按鈕負責這些，因此鍵盤使用者透過按鈕及其焦點環到達控制項。

## 動態效果

- 線條旋轉並滑入定位，輪廓在兩種形狀之間插值。快速切換會從圖示目前所在的位置反向變化。
- 複製圖示會折起其背面的紙張並繪製勾選。太陽變為帶有月牙缺口的月亮。
- 鈴鐺開啟時響一次，愛心每次被喜歡時以光環和火花迸發。
- 減少動態效果會立即完成每次變形，並跳過響鈴與迸發。關閉動態效果時同樣如此。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選單、播放、加號、主題、箭頭 | 從目前位置以可中斷的彈簧變形 | 立即完成 | 立即完成 |
| 複製變勾選 | 紙張折起，勾選繪出 | 立即完成 | 立即完成 |
| 鈴鐺開啟 | 響一次，鈴舌略微落後於鈴身 | 不響 | 不響 |
| 愛心被喜歡 | 以光環和火花迸發 | 無迸發 | 無迸發 |

## 相關元件

- `CopyButton`：帶自身確認的複製操作。
- `ThemeSwitcher`：帶標籤的淺色與深色切換。
- `Button`：承載圖示變形的名稱與狀態的控制項。
- `ExpandingButtonGroup`：展示標籤的圖示操作。

| 元件 | 適用情境 |
|---|---|
| `CopyButton` | 操作複製一個值，並應就地確認複製結果 |
| `ThemeSwitcher` | 淺色與深色的選擇需要可見的標籤 |
| `Button` | 圖示需要名稱、狀態以及按下或焦點的呈現 |
| `ExpandingButtonGroup` | 多個圖示操作應在焦點時展示標籤 |
| `DropdownMenu` | 一個觸發器打開操作清單，而不是顯示狀態 |
| `ArcIcons` 圖形 | 圖示從不改變狀態 |
