---
title: 主題切換
summary: 在亮色與暗色外觀之間切換的四種平滑方式。
source-hash: ba6acbbfdbff
---

<Demo name="Variants" />

## 何時使用

主題切換是產品外層的一個控制項，位於頁首、工具列或桌面工具的頂列中。使用者按一下，整個視窗就在亮色與暗色之間切換。`ThemeSwitcher` 會為輔助技術給出下一種外觀的名稱，顯示目前外觀的圖示，並播放與版面形狀相符的頁面轉場。當外觀的變化應當顯得有意為之、而不是突兀時，就使用它。

- **管理後台頁首中的切換**：切換控制項位於工作區選單與通知鈴鐺旁邊。僅圖示的形式讓頁首保持簡潔，並且控制項在每個頁面上都位於同一個角落。
- **長時間分析工作階段中在亮色與暗色之間切換**：財務團隊在一天快結束時檢視營收儀表板，並把視窗切換為暗色。`Rise` 變體最安靜，因此圖表顏色平穩過渡，不會在資料上掃過一道干擾的光。
- **在展示或發佈頁面預覽外觀**：設計團隊展示新產品版本時使用 `Eclipse`，其圓盤橫越頁面，讓觀眾看到變化的發生。
- **置中頂列的文件網站或行銷首頁**：置中版面沒有固定的角落可作為變化的起點。`Split` 從中線接縫展開，不依賴按鈕的位置。
- **依工作區記住外觀的桌面工具**：內部 CRM 或開發者工具把選擇儲存在設定中，並套用到每個視窗。處理常式接收要求，因此由應用程式決定如何套用。

## 何時不用

- **在亮色、暗色和跟隨系統之間做選擇，或選項位於帳號選單中**：使用 `UserMenu`。選單可以把這三個選項收在選單裡，而不佔用每天都需要的頁首空間。
- **需要在設定頁中持續可見的選擇**：使用 `SegmentedControl`，把亮色、暗色和跟隨系統作為保持選取的具名選項；每個選項都需要說明時使用 `RadioCards`。
- **不改變主題的一般操作**，例如匯出報表或邀請成員：使用 `Button`。
- **單一的開或關設定**，例如精簡側邊欄或在圖表上顯示數值：使用 `Switch`，它表示一項設定，而不是整個視窗的變化。
- **其他具名的選擇，例如語言、密度或強調色**：使用 `Select` 或 `SegmentedControl`。`ThemeSwitcher` 只在亮色與暗色之間切換。
- **不是主題的兩種圖示狀態**，例如播放與暫停、選單與關閉：使用 `IconMorph`，即雙狀態圖示按鈕。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ThemeSwitcher Variant="Reveal" />
</UserControl>
```

`ThemeSwitcher` 衍生自 Avalonia 的 `TemplatedControl`，其範本中包含一個小尺寸、次要變體的 Arc `Button`。因此按下縮放、懸停與焦點環都來自 Arc 按鈕主題，與任何其他 Arc 按鈕一致。切換控制項在此之上加入外觀邏輯：它追蹤視窗的主題變體，切換太陽與月亮圖示，在主題變化前發出要求，並把按鈕作為切換按鈕提供給輔助技術。未設定 `Appearance` 時，它跟隨視窗實際的主題變體。

### 結構

1. **按鈕外框**（內部的 Arc 按鈕）：小尺寸的次要按鈕，高 36 px。文字形式下，懸停會把邊框加深為強邊框顏色。僅圖示的形式是 38 × 38 px 的方形，圓角 12 px，沒有內距；懸停時會移除陰影，因此方塊只以表面顏色的變化作出反應。
2. **圖示**（圖示部分）：亮色外觀顯示太陽，暗色外觀顯示月亮。圖示為 16 px，描邊 1.75 px，繪製在 18 px 的圖示區域內，該區域位於 20 px 的方形中，並帶有變體對應的圖示位移。
3. **間距**（`IconTextGap`）：圖示與文字之間 8 px。
4. **文字**（`Text`）：可見的標籤。預設為「切換主題」，採用按鈕的文字顏色；當 `IsIconOnly` 為 `True` 時隱藏。

切換控制項自身靠左對齊、垂直置中，因此不需額外排版即可放在工具列的起始位置。把它放進 `StackPanel` 或 `DockPanel`，由父層容器決定位置即可。

### 變體

`Variant` 選擇頁面轉場方式。同一個值也決定暗色模式下圖示位移的方向，因此圖示與揭示效果朝同一方向移動。轉場只在切換控制項自行套用變化時播放；變體本身並不改變外觀。

| 變體 | 新外觀 | 適用情境 |
|---|---|---|
| `Reveal`（預設） | 從按鈕處以圓形向視窗最遠的角落擴散 | 切換位於角落，因此揭示從使用者按下的位置開始 |
| `Eclipse` | 一個與視窗對角線等寬的圓盤從右側進入，並覆蓋在頁面上 | 展示頁、簡報與發佈頁，變化本身就是重點 |
| `Split` | 中線處的細縫擴展為整個視窗 | 置中版面，此時按鈕的位置不提供方向感 |
| `Rise` | 從底部邊緣升起，直到頂部 | 資訊密集的工作畫面，此時最安靜的變化最合適；它是僅圖示頁首的選擇 |

```xml
<arc:ThemeSwitcher Variant="Eclipse" />
<arc:ThemeSwitcher Variant="Split" />
<arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
```

每個產品只選定一種變體，並在每個頁面上保持一致。在頁首中學會揭示效果的使用者，在其他地方使用切換時也應看到相同的動態效果。當切換位於角落、揭示可以從那裡開始時，使用 `Reveal`；當切換置中時，選擇 `Split`，因為從置中列的中間揭示會顯得隨意。

### 僅圖示與文字

`IsIconOnly` 顯示 38 × 38 的方形按鈕，並隱藏 `Text`。適用於圖示已足夠的精簡工具列或頁首，且周圍的控制項也都是圖示。當切換與帶文字的操作並列，或使用者可能看不出太陽和月亮是主題控制項時，使用文字形式，即設定 `Text="外觀"` 或保留預設值。

```xml
<arc:ThemeSwitcher Text="外觀" Label="切換外觀" />
```

`Label` 設定無障礙名稱。未設定 `Label` 時，切換控制項會自行說明下一種外觀（亮色時為「切換到暗色模式」，暗色時為「切換到亮色模式」）。固定的 `Label` 會在兩種外觀中取代這個名稱，因此它不再說明下一次按下會切換到哪種外觀。只有當名稱不應改變時才設定 `Label`，並保持簡短、具體。

`Text` 從不被用作無障礙名稱。切換控制項會自行設定按鈕的名稱，因此可見文字是給看得見控制項的使用者看的，而不是給輔助技術讀取的名稱。

### 處理切換要求

每次按下以及呼叫 `Toggle()` 時，都會在主題變化之前觸發 `ThemeChangeRequested`。其參數給出要切換到的外觀（`Next`）、變體（`Variant`）、被按下的按鈕（`Trigger`），以及該按鈕中心在其頂層座標系中的位置（`Origin`），即揭示效果的起點。設定 `Handled` 後由你自行套用主題；切換控制項隨即不再處理主題，也不會修改 `Appearance`：若你的處理常式沒有設定它，圖示與名稱都保持原樣。

<Demo name="HandledByApp" />

```csharp
switcher.ThemeChangeRequested += (_, e) =>
{
    e.Handled = true;
    switcher.Appearance = e.Next;
    ApplyAppearance(e.Next);
};
```

當應用程式自行掌握外觀時（例如選擇依使用者儲存、套用到多個視窗，或在伺服器呼叫之後才套用），處理這個要求。可以在切換控制項或其父層上處理它：事件會冒泡，因此視窗上的一個處理常式即可服務其中的每個切換控制項。當處理常式需要與內建套用相同的頁面轉場時，把觸發按鈕、變體以及你的套用步驟傳給 `RunTransitionAsync`：

```csharp
switcher.ThemeChangeRequested += async (_, e) =>
{
    e.Handled = true;
    await ThemeSwitcher.RunTransitionAsync(e.Trigger, e.Variant,
        () => _viewModel.ApplyAsync(e.Next));
};
```

`RunTransitionAsync` 會為視窗擷取快照，在其下方執行你的套用步驟，並依變體揭示新外觀。在減少動態效果、關閉動態效果，或觸發按鈕沒有覆蓋層時，它會直接執行套用步驟並返回，不播放轉場。

### 受控外觀

`Appearance` 是目前顯示的外觀，決定圖示、按下狀態與名稱。它是可為空的 `ThemeSwitcherAppearance`，值為 `Light` 與 `Dark`，預設是雙向繫結。在程式碼中設定它，可使切換控制項與應用程式保持同步。`EffectiveAppearance` 會回傳實際生效的外觀，無論它來自 `Appearance` 還是來自視窗。

- 當 `Appearance` 為 `null`（預設值）時，切換控制項跟隨視窗實際的主題變體，並在其變化時更新。
- 當 `Appearance` 有值時，切換控制項顯示該值，並停止跟隨視窗。將其設回 `null` 即可恢復跟隨。
- 設定 `Appearance` 會改變圖示與名稱，但不會播放頁面轉場。轉場只在切換控制項自行套用變化時執行，即由按下或 `Toggle()` 觸發。

```xml
<arc:ThemeSwitcher Appearance="Light" Variant="Rise" />
```

```csharp
switcher.Toggle();
```

`Toggle()` 的行為與按下一樣：切換到另一種外觀，引發 `ThemeChangeRequested`，並且除非要求已被處理，否則套用此次變化。可將它用於應用程式中的鍵盤快速鍵，例如命令選擇台中的一個命令。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型掌握外觀與套用步驟；切換控制項只顯示該值並回報要求。下面的檢視模型使用 CommunityToolkit.Mvvm 與應用程式端的設定儲存。`ApplyAsync` 是應用程式自己的程式碼，負責改變應用程式的主題。

```csharp
public partial class AppearanceViewModel : ObservableObject
{
    private readonly IAppearanceStore _store;
    private readonly IThemeService _themes;

    [ObservableProperty]
    private ThemeSwitcherAppearance? _appearance;

    public AppearanceViewModel(IAppearanceStore store, IThemeService themes)
    {
        _store = store;
        _themes = themes;
        _appearance = store.Load();
    }

    public async Task ApplyAsync(ThemeSwitcherAppearance next)
    {
        await _themes.ApplyAsync(next == ThemeSwitcherAppearance.Dark);
        Appearance = next;
        await _store.SaveAsync(next);
    }
}
```

```xml
<arc:ThemeSwitcher x:Name="ThemeSwitch"
                   Appearance="{Binding Appearance}"
                   Variant="Rise" />
```

```csharp
public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
        ThemeSwitch.ThemeChangeRequested += OnThemeChangeRequested;
    }

    private async void OnThemeChangeRequested(object? sender, ThemeSwitcherChangeRequestedEventArgs e)
    {
        e.Handled = true;
        await ThemeSwitcher.RunTransitionAsync(e.Trigger, e.Variant,
            () => _viewModel.ApplyAsync(e.Next));
    }
}
```

- `Appearance` 是雙向的：外觀變化時，切換控制項把其值寫入檢視模型；應用程式設定它時（例如啟動時載入已儲存的選擇），檢視模型的值會傳到切換控制項。
- 切換控制項沒有命令。要求透過事件發出，因為事件攜帶了轉場所需的觸發按鈕與起點。如上所示在程式碼後置中連接事件，並讓處理常式呼叫檢視模型。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求簡潔，這裡的範例省略了它）。

### 持久化選擇

在第一個影格之前或之後立即載入已儲存的值，並將其指派給檢視模型。首次設定外觀時不產生動畫，並且在最初幾個影格內到達的值也不會播放圖示切換，因此切換控制項會以正確的狀態開始，不會閃爍。在 `ApplyAsync` 中於主題變化之後再儲存該值，這樣套用失敗時，就不會儲存一個視窗從未顯示過的外觀。

### 節制地使用動態效果

頁面轉場適用於整個視窗都發生的變化。它讓使用者看到主題在各處都已改變，而快速的重新著色可能會被忽略。不要把它用於經常發生的變化：一分鐘內多次切換主題的使用者，使用安靜的 `Rise` 比每次都出現日蝕效果更合適。轉場會依渲染比例擷取整個視窗，因此其成本隨視窗尺寸與顯示密度增長。在大視窗中保持變體安靜。

減少動態效果會移除頁面轉場與圖示位移，圖示立即淡入。關閉動態效果則直接切換主題，沒有任何這些效果。

### 範例：分析主控台頁首

營運主控台的頁首，包含產品名稱與工作區、一個用於匯出的幽靈按鈕，以及末端一個僅圖示的切換控制項。由於切換控制項沒有設定 `Appearance`，它跟隨視窗的主題；頁首本身不決定外觀，由應用程式決定。僅圖示的 `Rise` 變體讓頁首保持安靜，匯出按鈕採用幽靈樣式，因此不會與下方的內容爭奪注意力。切換位於列的末端，這也是使用者尋找視窗層級控制項的位置。

![主控台頁首，包含匯出幽靈按鈕與僅圖示的主題切換](images/ConsoleHeader.png)

```xml
<Border Width="640"
        Padding="16,12"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1">
    <DockPanel LastChildFill="False">
        <StackPanel DockPanel.Dock="Left" Spacing="2">
            <TextBlock Text="Acme Analytics"
                       FontSize="15"
                       FontWeight="SemiBold" />
            <TextBlock Text="Workspace: Northwind EMEA"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel DockPanel.Dock="Right"
                    Orientation="Horizontal"
                    Spacing="8"
                    VerticalAlignment="Center">
            <arc:Button Content="Export" Variant="Ghost" />
            <arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
        </StackPanel>
    </DockPanel>
</Border>
```

### 文案規範

- **預設名稱與文字**：頁首中保留預設文字「切換主題」，以及說明下一種外觀的預設無障礙名稱。當切換位於一排帶標籤的設定中時，用一個簡短的名詞覆寫文字，例如「外觀」。
- **標籤**：設定時，應說明動作及其切換到的外觀，例如「切換到暗色模式」。省去螢幕閱讀器已經會播報的詞，例如「開關」「按鈕」。
- **文字**：句首大寫的動詞加受詞或名詞片語，不加標點。文字中不要寫出目前狀態，因為圖示已經表明了。
- **僅圖示**：只在周圍的控制項同樣是圖示時使用僅圖示形式。在帶標籤的按鈕中間單獨放一個太陽與月亮，很容易被忽略。

| 不要寫 | 改為 |
|---|---|
| 深色 | 切換到暗色模式 |
| 更改外觀 | 切換外觀 |
| 主題切換（點擊） | 切換主題 |
| 應用程式的夜間模式 | 切換到暗色模式 |
| 模式 | 切換到亮色模式 |

### 最佳做法

**建議**

- 在每個頁面的頁首相同位置放置切換控制項。找到它一次的使用者會預期它總在同一位置，因此切換控制項始終容易被發現。
- 當應用程式需要儲存選擇、把它套用到多個視窗，或在呼叫伺服器之後套用時，處理 `ThemeChangeRequested`。處理常式是唯一能同時看到下一種外觀、變體與起點的地方。
- 整個產品只保留一種變體。只有重複出現時，動態效果才會成為產品的標誌。
- 除非名稱必須固定，否則不要設定 `Label`。預設名稱會告訴螢幕閱讀器下一次按下會切換到哪種外觀，而固定名稱做不到。

**避免**

- 在資訊密集的儀表板中使用 `Eclipse` 或 `Reveal`。整個視窗掃過的效果會與圖表重繪爭奪注意力；更安靜的 `Rise` 能把注意力留給資料。
- 在程式碼中設定 `Appearance` 來偽造主題切換。設定它只會移動圖示與名稱，視窗仍保持舊的外觀。應當先在處理常式中套用主題，再設定該值。
- 在兩種狀態下都使用固定的 `Label`，例如「切換外觀」。名稱將不再告訴使用者下一次按下會切換到哪種外觀。
- 頁首有空間時，把切換控制項放進與系統選項並列的選單中。選單會把切換控制項藏起來，而三選一的選擇應當放在 `UserMenu` 中。
- 在程式碼中迴圈按下切換，例如自動化測試在每一步都翻轉主題。每次按下都會擷取整個視窗並執行轉場，因此快速連續的操作每次都要付出一次快照的代價。

### 常見問題

**為什麼圖示變了，視窗卻沒有變？** 設定 `Appearance` 只會移動圖示、按下狀態與名稱。當切換控制項或你的處理常式套用主題時，主題才會改變。如果你處理了要求，請像「處理切換要求」中所示，在處理常式裡套用主題。

**設定 `Appearance` 後，為什麼圖示不再跟隨視窗？** `Appearance` 中的值會覆寫視窗的主題變體。將 `Appearance` 設回 `null` 即可再次跟隨視窗。

**啟動時載入已儲存的偏好，會播放轉場嗎？** 不會。設定 `Appearance` 從不播放頁面轉場。圖示切換只在切換控制項載入並穩定之後執行，因此啟動時設定的值會直接顯示，不產生切換動畫。

**內建套用會保留我的強調色與其他主題設定嗎？** 會。切換控制項會沿用它最近觀察到的主題要求的設定，並且只切換暗色演算法。在任何要求之前，它會先沿用目前的 Arc 強調色預設，因此切換外觀時強調色不會重設。

**內建套用能使用我自己的轉場嗎？** 可以。處理要求，並以觸發按鈕、變體和你的套用步驟呼叫 `RunTransitionAsync`，如「處理切換要求」中所示。該輔助方法會執行變體指定的轉場，在減少動態效果下則退回為直接套用。

## API 參考

<!-- api: ThemeSwitcher -->

**`ThemeSwitcherChangeRequestedEventArgs`** 是 `ThemeChangeRequested` 的引數。

| 成員 | 型別 | 意義 |
|---|---|---|
| `Next` | `ThemeSwitcherAppearance` | 要切換到的外觀 |
| `Variant` | `ThemeSwitcherVariant` | 切換控制項要求的頁面轉場 |
| `Trigger` | `Control` | 被按下的按鈕 |
| `Origin` | `Point` | 觸發按鈕中心在其頂層座標中的位置，即揭示效果的起點 |
| `Handled` | `bool` | 設為 `True` 即表示由你自行套用主題；繼承自 `RoutedEventArgs` |

**`ThemeSwitcherVariant`** 與 **`ThemeSwitcherAppearance`** 是切換控制項使用的兩個列舉。

| 列舉 | 成員 |
|---|---|
| `ThemeSwitcherVariant` | `Reveal`（預設）、`Eclipse`、`Split`、`Rise` |
| `ThemeSwitcherAppearance` | `Light`、`Dark` |

## 無障礙

- 切換控制項是一個切換按鈕。處於暗色外觀時它回報為開啟，處於亮色外觀時為關閉；外觀每次變化都會為輔助技術引發一次切換狀態變更。
- 除非設定了 `Label`，其無障礙名稱會說明下一種外觀（「切換到暗色模式」或「切換到亮色模式」）。外觀變化時名稱隨之更新。
- 太陽與月亮圖示對輔助技術隱藏。名稱承載的是含義，而不是圖示。
- 切換控制項與其他 Arc 按鈕一樣可以取得鍵盤焦點，並顯示標準的焦點環。Enter 與 Space 都可以切換外觀。
- 減少動態效果時，主題立即切換，不播放頁面轉場，也不做圖示位移。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter` 或 `Space` | 切換到另一種外觀；先觸發 `ThemeChangeRequested` |

## 動態效果

- 太陽與月亮沿共用路徑相互轉動、縮放，舊圖示迅速離開。兩個圖示朝同一方向轉動，因此切換讀起來是一次旋轉，而不是兩個分開的圖示。
- 暗色模式下圖示會朝其變體的方向移動：`Reveal` 將其放大到 108%，`Eclipse` 向左移動 2 px，`Split` 向右移動 1 px，`Rise` 向上移動 2 px。
- 頁面轉場依變體揭示新外觀。視窗作為快照保留在覆蓋層中，新外觀在其下方套用，快照的裁剪區域隨之打開以顯示新外觀。
- 首次設定外觀時不產生動畫。圖示切換只在切換控制項載入並穩定之後開始。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 主題變化 | 依變體播放頁面轉場 | 無轉場；主題立即切換 | 立即完成 |
| 太陽與月亮切換 | 圖示相互轉動、縮放 | 圖示立即淡入 | 立即完成 |
| 暗色模式圖示位移 | 圖示依其變體移動 | 無位移 | 立即完成 |

## 相關元件

- `Button`：不改變主題的一般操作，也是切換控制項所基於的按鈕。
- `UserMenu`：包含跟隨系統選項、位於帳號選單中的主題選擇。
- `SegmentedControl`：在幾個具名選項之間選擇，並保持選取狀態。
- `IconMorph`：在兩種狀態之間變形的獨立圖示。

| 元件 | 適用情境 |
|---|---|
| `Button` | 操作不改變主題 |
| `UserMenu` | 主題選擇包含跟隨系統，或位於帳號選單中 |
| `SegmentedControl` | 亮色、暗色與跟隨系統是設定頁中可見的具名選項 |
| `RadioCards` | 每個外觀選項在設定頁中都需要自己的說明 |
| `Switch` | 單一的開或關設定，且不改變整個視窗 |
| `IconMorph` | 不是主題的雙狀態圖示 |
