---
title: 顏色選擇器
summary: 色塊展開為選擇器，支援格式切換、滴管、已儲存色塊與對比度讀數。
source-hash: 7d81791f9bc3
---

<Demo name="ColorField" />

## 何時使用

`ColorPicker` 在緊湊的欄位中提供完整的顏色編輯器。收合時，控制項是一個帶標籤與 HEX 值的色塊；展開後會顯示飽和度與明度區域、色相與不透明度滑桿、可接受四種表示法的文字方塊、對比度讀數，以及一排已儲存的顏色。它適用於顏色是使用者必須細緻調整的真實設定的場景，而不是從一個短清單中做選擇。

- **多租戶管理後台中的品牌設定**：管理員為某位客戶的帳單與登入頁設定主色。色塊在設定清單中顯示目前的品牌色，面板可以貼上客戶樣式指南中的 HEX 值。
- **主題與設計權杖編輯器**：產品團隊編輯背景色與強調色，包括半透明的疊加層，其中 Alpha 通道是值的一部分。OKLCH 格式讓他們手動調整明度與彩度。
- **內容工具中的文字顏色檢查**：編輯者選擇文字顏色，並讀取它與所在頁面背景之間的 WCAG 對比度。等級（AAA、AA、AA large 或 Fails）會顯示文字在發布前是否清晰可讀。
- **圖表與儀表板的系列顏色**：分析師為每個圖表系列指定顏色。已儲存的顏色保留團隊已經約定的配色，因此每個新系列都從同一組顏色中選取。
- **行銷工具中的已儲存品牌色塊**：活動建立者最多保留七種顏色，團隊在橫幅與電子郵件中重複使用，最新的顏色排在最前。

## 何時不用

- **少量固定顏色**：選項是「淺色」「深色」或三種狀態色這類有名稱的選擇時，用 `SegmentedControl` 或 `RadioGroup`。使用者只是挑選一個，不會調整顏色。
- **沒有選擇器的一般 HEX 值**：值是輸入或貼上的，使用者從不需要色彩區域或滑桿時，用 `Input`，例如從文件複製到 Webhook 設定中的 API 權杖顏色。
- **用於篩選清單的小型預設顏色標籤**：「紅色」「藍色」或「未指派」這類用於篩選發票或工單的標籤，用 `ChipGroup`。標籤組只在預設中選擇，不會產生任意的值。
- **使用者不應修改、由品牌色盤衍生的顏色**：在它所屬的設定旁邊，用唯讀的 `Input` 顯示該值。停用的選擇器仍會佔據空間，並讓人疑惑為何被鎖定；應改為在說明文字中寫明規則。
- **日期、數字或度量**：有單位的值用 `NumberField`、`DatePicker` 或 `Slider`。顏色有三到四個通道，選擇器需要讓它們保持同步；這些控制項只保存一個數字。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ColorPicker Label="強調色"
                     Color="#2F6BFF"
                     Format="Hex" />
</UserControl>
```

`ColorPicker` 衍生自 Avalonia 的 `TemplatedControl`，並實作 Arc 選擇器的自動化契約，因此它支援常見的版面配置、`IsEnabled`、焦點與樣式屬性。它的值是 Avalonia 的 `Color`，因此同一個顏色無需轉換即可用於筆刷、資源與檢視模型。標籤、說明與錯誤文案都來自共用的欄位基礎，所以選擇器能與表單中的其他欄位對齊。

### 結構

1. **觸發器**：44 px 的膠囊形，圓角為 22 px。內含 32 px 的色塊（顏色為半透明時，色塊下方顯示棋盤格）、12 px 文字的 `Label`，以及 14 px 文字的 `HexValue`，色塊與文字之間留 10 px 間距。靠左對齊，寬度隨內容而定。
2. **說明**（`Description`）：觸發器下方的輔助文案，使用淡化的文字顏色。
3. **錯誤**（`ErrorMessage`）：觸發器下方的危險色文案。內容非空時，選擇器會被標示為無效。
4. **面板標頭**：面板展開時，重複顯示色塊、標籤與 HEX 值，右側是 32 px 的「完成」按鈕。面板最寬 304 px，且不會超過視窗寬度減 32 px。
5. **飽和度與明度區域**：高 164 px，圓角 16 px。飽和度從左到右遞增，明度從下到上遞增，滑桿指示目前的組合。
6. **色相與不透明度滑桿**：區域下方是兩條 14 px 的軌道。不透明度軌道會在棋盤格上顯示該顏色。
7. **值欄位**：一列 40 px 高的區域，圓角 14 px。起始處的格式按鈕（HEX、RGB、HSL 或 OKLCH）寬度至少 56 px。文字方塊佔據該列其餘部分；輸入無效時，其下方顯示提示訊息。
8. **對比度列**：36 × 28 px 的色樣，圓角 9 px，顯示 `ContrastBackground` 上的顏色；接著是比值文字、22 px 膠囊中的等級，以及「相對於背景」字樣（視口寬 360 px 及以下時隱藏）。
9. **已儲存色塊**：一排已儲存的顏色，每個都是小方塊，末尾是虛線邊框的「儲存」按鈕。

面板從觸發器向下展開，並從觸發器中生長出來。只有當視窗邊緣會裁切它時，面板才會向左或向上移動；頁面捲動到底部時，頁面會先捲動以騰出空間。

### 值與格式

`Color` 是 Avalonia 的 `Color`，且為雙向繫結，是需要繫結的值。`Format` 設定文字方塊中顯示的表示法，格式按鈕會在面板中循環切換四種表示法。在程式碼中設定 `Format` 只會改變表示法，不會改變顏色。

`HexValue` 為唯讀，以大寫的 CSS 十六進位給出顏色：`#RRGGBB`，顏色為半透明時為 `#RRGGBBAA`。用於顯示、複製與匯出。它的順序與 Avalonia 自身的字串形式不同：Avalonia 的 `Color.Parse` 把八位數值讀作 `#AARRGGBB`，因此應把 CSS 值交給選擇器的文字方塊，或自行轉換。

| 格式 | #2F6BFF 的範例輸出 | 說明 |
|---|---|---|
| `Hex` | `#2F6BFF` | 預設值。半透明顏色會多出兩位：`#2F6BFF80`。 |
| `Rgb` | `rgb(47 107 255)` | 以空格分隔。半透明顏色以相同形式追加 `/ 50%`。 |
| `Hsl` | `hsl(223 100% 59%)` | 色相以度數表示，飽和度與明度以百分比表示。 |
| `Oklch` | `oklch(56.2% 0.214 263.5)` | 感知明度與彩度；當兩種顏色必須看起來一樣亮時很有用。 |

文字方塊接受相同的四種表示法，且輸入時很寬容：三位與四位的十六進位、`rgb()` 與 `rgba()`、帶或不帶 `deg` 的 `hsl()` 與 `hsla()`，以及 `oklch()`，並同時支援現代的空格分隔形式與逗號分隔形式。其他輸入都會被拒絕，見下文的驗證一節。

```xml
<arc:ColorPicker Label="品牌色" Color="#E5484D" Format="Oklch" />
```

```csharp
picker.ColorChanged += (_, e) => status.Text = $"顏色：{e.Hex}";
```

事件引數包含變更前的顏色、變更後的顏色與 CSS 十六進位值：

| 成員 | 類型 | 含義 |
|---|---|---|
| `OldColor` | `Color` | 變更前的顏色。 |
| `NewColor` | `Color` | 變更後的顏色。 |
| `Hex` | `string` | 新顏色的大寫 CSS 十六進位，半透明時包含透明度的兩位。 |

`Color` 每次變化時都會觸發 `ColorChanged`，包括拖曳把手時指標的每一次移動。若每次移動都向伺服器儲存，會產生大量請求；應在面板關閉時儲存（`IsDropDownOpen` 變為 `False` 時），或為處理程式加上防彈跳。

### 已儲存色塊

`Swatches` 是 `IReadOnlyList<ColorSwatch>?`。`ColorSwatch` 是帶有 `Id` 與 `Color` 的記錄；用 `ColorSwatch.Create(color)` 建立，它會取得新的識別。清單依最新在前排列。「儲存」按鈕把目前的顏色加到最前，當清單長於 `MaxSwatches`（預設為 7）時，最早的色塊會被移除。色塊可以透過拖曳，或用 `Alt` 加方向鍵重新排序，並可用 `Delete` 或 `Backspace` 刪除。

每次變更都會建立新的清單，因此檢視模型應雙向繫結 `Swatches`，並取代清單而不是修改它。在面板中進行儲存、重新排序與刪除時會觸發 `SwatchesChanged` 事件。在程式碼中設定 `Swatches` 只會更新屬性與色塊列，不會觸發該事件。

<Demo name="FormatsAndSavedSwatches" />

每個色塊在重新排序時都會保留其 `Id`，因此檢視模型可以追蹤某個色塊的移動。

### 限制與滴管

`ColorPicker` 不提供滴管。Avalonia 沒有跨平台的 API 可以從螢幕讀取顏色，而且在 macOS 上嘗試這樣做需要螢幕錄製權限。需要滴管的設計工具，應從自己的畫布或剪貼簿取得顏色，並把 HEX 值貼到欄位中。

### 對比度與無障礙

`ContrastBackground` 是所選顏色將要放置其上的顏色。讀數使用 WCAG 對比度比值（範圍 1 到 21）將該值與之比較，並以等級標示結果：

| 比值 | 等級 | 用途 |
|---|---|---|
| 7 或以上 | AAA | 任意字級的內文 |
| 4.5 或以上 | AA | 內文 |
| 3 或以上 | AA large | 僅用於大型文字與介面圖形 |
| 低於 3 | Fails | 不要用於文字 |

預設背景為白色。應把它設為顏色實際所在的表面，例如卡片顏色或深色頁面顏色，因為比值的可靠程度取決於所測量的背景。`ContrastRatio` 與 `ContrastGrade` 在程式碼中提供相同的值，因此表單無需開啟面板就能對不合格的文字顏色發出警告。

<Demo name="Contrast" />

### 驗證與錯誤狀態

`ErrorMessage` 非空時，欄位會被標示為無效。使用者按下 Enter 時，若輸入的文字不是顏色，會被拒絕：欄位抖動，出現危險色邊框，面板中顯示「請輸入 HEX、RGB、HSL 或 OKLCH 顏色」這段文字（設定 `InvalidColorText` 可更改它）。在輸入有效值之前，選擇器會保留最後一個有效顏色。

`ErrorMessage` 屬於應用程式本身。用它表達選擇器無法得知的規則，例如顏色與必填元素的背景過於接近，並以平實的語言說明規則。`IsEnabled="False"` 會停用選擇器；它仍會顯示色塊，因此被鎖定的值依然能顯示目前生效的顏色。

```xml
<arc:ColorPicker Label="已鎖定" IsEnabled="False" />
```

### 繫結到檢視模型

租戶的品牌設定是檢視模型中的一個屬性，包含一個顏色與一個已儲存色塊的清單。選擇器對兩者都做雙向繫結，何時儲存由檢視模型決定。

```csharp
public interface IBrandSettingsStore
{
    Task SaveBrandAsync(Color primary, IReadOnlyList<Color> saved);
}

public partial class BrandSettingsViewModel(IBrandSettingsStore settings) : ObservableObject
{
    [ObservableProperty]
    private Color _primaryColor = Color.Parse("#2F6BFF");

    [ObservableProperty]
    private IReadOnlyList<ColorSwatch> _savedColors = [];

    [RelayCommand]
    private async Task SaveAsync()
    {
        await settings.SaveBrandAsync(PrimaryColor, SavedColors.Select(s => s.Color).ToList());
    }
}
```

```xml
<StackPanel Width="304" Spacing="16">
    <arc:ColorPicker Label="主色"
                     Color="{Binding PrimaryColor, Mode=TwoWay}"
                     Swatches="{Binding SavedColors, Mode=TwoWay}"
                     Description="按鈕、連結與焦點環。" />
    <arc:Button Content="儲存品牌色" Command="{Binding SaveAsyncCommand}" />
</StackPanel>
```

`Color`、`Swatches` 與 `Format` 是雙向屬性。`HexValue`、`ContrastRatio` 與 `ContrastGrade` 是單向讀取的值，檢視模型可以用來顯示。`IsDropDownOpen` 也是雙向的，因此使用者離開設定頁時，檢視模型可以關閉面板。檢視模型的 `SaveAsync` 由按鈕觸發，而不是每次顏色變化都觸發，因此拖曳到一半的顏色不會送到伺服器。

### 範例：租戶品牌色

SaaS 產品管理後台中的一張品牌設定卡片。卡片包含三個選擇器：主色、圖表強調色，以及帶有對比度讀數的內文顏色。標籤是一個詞，說明文字寫明每種顏色用在哪裡，內文顏色上的錯誤訊息則指出了未通過的規則。截圖中選擇器是收合的，因此該場景展示的是使用者開啟面板之前所看到的內容。

![帶有三個顏色選擇器的品牌設定卡片，其中一個對比度未通過](images/BrandSettings.png)

```xml
<Border Width="560"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="20">
        <StackPanel Spacing="4">
            <TextBlock Text="Brand colors"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Acme Analytics customer portal"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:ColorPicker Label="Primary"
                         Color="#2F6BFF"
                         Description="Buttons, links and focus rings." />
        <arc:ColorPicker Label="Chart accent"
                         Color="#E5484D"
                         Format="Oklch"
                         Description="Highlights the selected series in charts." />
        <arc:ColorPicker Label="Body text"
                         Color="#9CA3AF"
                         ContrastBackground="#FFFFFF"
                         ErrorMessage="Contrast 2.5 to 1 fails. Choose a darker color." />
        <arc:Button Content="Save brand colors" HorizontalAlignment="Right" />
    </StackPanel>
</Border>
```

範例為圖表強調色使用 `Oklch`，因為團隊需要比較各系列之間的明度；內文顏色與白色卡片進行對比檢查，因此無需開啟面板就能看到錯誤。按鈕寫明了它的作用，並且是卡片上唯一的操作。

### 文案規範

- **標籤**：一個簡短的名詞，說明顏色的角色，例如「主色」「圖表強調色」「內文顏色」。標籤繪製在色塊內部，因此請控制在兩個詞以內。
- **說明**：用一句話說明顏色的使用位置，例如「按鈕、連結與焦點環。」
- **錯誤**：說明規則與修正方法，例如「對比度 2.5 比 1，未通過。請選擇更深的顏色。」不要重複標籤。
- **儲存操作**：使用動詞加上描述儲存範圍的對象，例如「儲存品牌色」，而不是「送出」。
- **背景名稱**：用背景的角色來命名，例如「卡片背景」，讓使用者知道對比度讀數比較的是什麼。

| 不要寫 | 改為 |
|---|---|
| 顏色 | 主按鈕顏色 |
| 顏色無效！！ | 請輸入 HEX、RGB、HSL 或 OKLCH 顏色。 |
| 送出 | 儲存品牌色 |
| 對比度不通過 | 對比度 2.5 比 1，未通過。請選擇更深的顏色。 |

### 最佳做法

**建議**

- 將 `Color` 雙向繫結，並在明確的操作時儲存，因為拖曳把手時每次指標移動都會觸發 `ColorChanged`。
- 將 `ContrastBackground` 設為真實的表面，讓讀數與使用者實際看到的一致。
- 將 `Format` 設為團隊的表示法：CSS 與設計交接用 `Hex`，需要比較明度時用 `Oklch`。
- 除非團隊的色盤需要更多，否則保持 `MaxSwatches` 的預設值 7；過長的一列更難掃讀。
- 為每個選擇器設定 `Label`，即使在緊湊的設定中也是如此，因為螢幕閱讀器播報的正是這個名稱。

**避免**

- 將 `ColorChanged` 用作儲存的觸發器。每次移動都會產生一個事件，因此每次移動都發出一次網路請求會淹沒伺服器。
- 對 CSS 八位數值呼叫 `Color.Parse`。Avalonia 讀取的是 `#AARRGGBB`，因此透明度通道會落在錯誤的位置；CSS 輸出請使用 `HexValue`，CSS 輸入交給選擇器解析。
- 在程式碼中設定 `Swatches` 並期待觸發 `SwatchesChanged`。該事件描述的是在面板中所做的變更，自行設定清單的檢視模型本身已經知道新值。
- 為必須維持固定的色盤使用選擇器。應改用 `SegmentedControl` 或 `ChipGroup` 提供已核准的顏色，因為自由選擇器會讓人脫離色盤。
- 顏色位於深色表面上時，卻以預設的白色背景判斷對比度。等級對所給的背景是準確的，對其他背景則會造成誤導。

### 常見問題

**如何從已儲存的 HEX 字串啟動選擇器？** 在檢視模型中以 Avalonia 的 `Color` 保存該值，並設定 `Color`。對於 CSS 順序的值，例如 `#2F6BFF80`，在呼叫 `Color.Parse` 之前把數字重新排列為 `#802F6BFF`，因為 Avalonia 把八位數值讀作 `#AARRGGBB`。用 `HexValue` 輸出該值，使儲存形式維持 CSS 順序。

**為什麼選擇器顯示的表示法與我設定的不同？** `Format` 是文字方塊的表示法。使用者可以在面板中循環切換，而值不會改變；顏色在每種表示法中都是相同的。

**能否在程式碼中關閉面板？** 可以。將 `IsDropDownOpen` 設為 `False`。「完成」、Escape 與在面板外按下，都會以同樣的方式關閉面板；「完成」與 Escape 會把焦點返回到觸發器。

**為什麼輸入時按 Escape 不能關閉面板？** 第一次 Escape 會捨棄輸入的草稿，使文字方塊恢復為目前的顏色；第二次 Escape 才會關閉面板。這樣可以避免輸入一半的值被意外遺失。

**半透明顏色的讀數如何處理？** 測量比值前，它會先將顏色與 `ContrastBackground` 合成，因此等級描述的是顏色在該背景上的實際樣子，而不是原始顏色。

## API 參考

<!-- api: ColorPicker -->

<!-- api: ColorSwatch -->

## 無障礙

- `Label` 為觸發器命名，展開的面板以它命名：「主色」。觸發器會公開其展開狀態，因此螢幕閱讀器會播報面板是否已開啟。
- 飽和度與明度區域是一個二維滑桿，其值同時回報飽和度與明度，類型播報為「二維滑桿」。色相與不透明度滑桿分別回報角度與百分比。
- 文字方塊以標籤與表示法命名，例如「主色的 Hex 格式」。輸入無效時，輔助文字會被設為錯誤訊息，並以禮貌的方式播報。
- 對比度比值及其等級是文字，而不只是顏色：讀數會說「對比度 4.52 比 1，通過 AA」或「未通過」，因此等級不依賴膠囊的顏色。
- 已儲存的色塊組成一列水平清單，只有一個 Tab 停駐點。目前的顏色以已選取的狀態公開，色塊的名稱會說明如何移動與刪除它們。
- 透過「完成」或 Escape 關閉面板時，焦點會返回觸發器，使使用者在表單中保持原來的位置。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 觸發器上的 `Enter` / `Space` | 開啟面板，並把焦點移到飽和度區域 |
| 展開時觸發器上的 `Enter` / `Space` | 關閉面板，並把焦點返回觸發器 |
| 面板中的 `Escape` | 關閉面板，並把焦點返回觸發器 |
| 區域上的 `Left` / `Right` / `Up` / `Down` | 以 1% 為步幅移動飽和度（左右）與明度（上下） |
| 區域上的 `Shift` 或 `PageUp` / `PageDown` | 以 10 個步幅移動飽和度 |
| 滑桿上的 `Left` / `Right`（`Up` / `Down`） | 將色相移動 1 度，或將不透明度移動 1% |
| 滑桿上的 `Shift`、`PageUp` / `PageDown` | 以 10 為步幅移動 |
| 滑桿上的 `Home` / `End` | 移到最小值或最大值 |
| 文字方塊中的 `Enter` | 套用輸入的值；無效值會使欄位抖動並顯示錯誤 |
| 文字方塊中的 `Escape` | 捨棄輸入的草稿；再按一次 `Escape` 關閉面板 |
| 色塊上的 `Left` / `Right`、`Home` / `End` | 在已儲存的色塊之間移動焦點 |
| 色塊上的 `Alt` + `Left` / `Right` | 將色塊移動一個位置 |
| 色塊上的 `Enter` / `Space` | 套用已儲存的顏色 |
| 色塊上的 `Delete` / `Backspace` | 刪除該色塊 |

指標可以在區域與滑桿上拖曳。在面板外按下會關閉面板，並且不會把焦點從使用者按下的位置移走。

## 動態效果

- 面板從色塊中展開：裁剪區域從觸發器的角落生長為完整的面板，陰影隨裁剪區域變化。
- 把手以快速彈簧跟隨指標，拖曳時會收緊。
- 格式按鈕使文字在 HEX、RGB、HSL 與 OKLCH 之間變形。對比度比值會計數到新的數值。
- 無效輸入會使欄位抖動。新色塊彈出，被移動的色塊落定到新位置。
- 減少動態效果時，把手直接跳到新位置，面板以淡入代替裁剪展開，並取消格式變形與抖動。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 面板開啟 | 裁剪區域以彈簧從觸發器生長；內容淡入，並在前緣後方升起 10 px | 面板以完整尺寸淡入 | 立即完成 |
| 面板關閉 | 裁剪區域以短促、無回彈的彈簧折回觸發器 | 面板淡出 | 立即完成 |
| 把手移動 | 以快速彈簧跟隨指標；拖曳時收緊為較硬的彈簧 | 直接跳到新位置 | 立即完成 |
| 格式變化 | 文字從舊表示法變形為新表示法 | 文字立即改變 | 立即完成 |
| 對比度比值變化 | 數字計數到新的值 | 數字立即改變 | 立即完成 |
| 無效輸入 | 欄位左右抖動一次，錯誤隨即出現 | 錯誤出現，不抖動 | 錯誤立即出現 |
| 色塊儲存或重新排序 | 色塊彈出，或落定到新位置 | 立即出現或移動 | 立即完成 |

## 相關元件

- 少量固定顏色用 `SegmentedControl` 或 `RadioGroup`。
- 一般文字值用 `Input`。
- 小型預設標籤用 `ChipGroup`。
- 表單中遵循相同欄位模式的日期值用 `DatePicker`。

| 元件 | 適用情境 |
|---|---|
| `SegmentedControl` / `RadioGroup` | 使用者從幾種固定顏色或主題中選擇一種 |
| `Input` | 顏色是輸入或貼上的，從不需要在區域中調整 |
| `ChipGroup` | 顏色是用於篩選或標記清單的預設 |
| `Slider` | 只需設定單一數值通道，例如不透明度 |
| `DatePicker` | 值是日期而非顏色，表單版面相同 |
