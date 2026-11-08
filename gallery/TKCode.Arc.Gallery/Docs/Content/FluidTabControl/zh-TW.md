---
title: 流式標籤頁
summary: 標籤列旁是一個捲動區域，依序容納每個標籤的內容。選取標籤會捲動到對應區段，捲動內容會選取畫面中區段對應的標籤。
source-hash: 3f88e2411322
---

<Demo name="Default" />

## 何時使用

`FluidTabControl` 在一個捲動區域旁放置標籤列，捲動區域依序容納每個標籤的內容，一個區段接一個區段。選擇標籤會平滑捲動到它的區段；捲動內容時，到達捲動區域頂端（或中線）的區段所對應的標籤會被選取。內容不會被替換，所以使用者既可以一路讀下去，也可以在區段之間跳轉。這種模式也稱為捲動連動標籤頁或 scroll spy。

- **設定頁面**：一般、外觀、帳號、通知、隱私與進階。只想找一個開關的人選擇對應標籤；想全部檢查一遍的人從上到下捲動一次即可。
- **分章節的長表單**：包含公司、聯絡人、帳單與文件的客戶申請。標籤列顯示使用者進行到哪裡，所有欄位都在同一頁，在各部分之間切換時不會遺失內容。
- **文件或手冊**：章節加上一個跟隨閱讀位置的目錄。
- **屬性編輯器**：選取物件的屬性依版面、外觀與行為分組，各組依序排列，而不是藏在不同的標籤後面。

## 何時不用

- **不應一起閱讀的檢視**，例如一筆紀錄的概覽、動態與設定：使用 `Tabs`，它一次只顯示一個面板。
- **使用者每次開啟一個或幾個的少量區段**：使用 `Accordion`。
- **必須依序完成的步驟**：使用 `Stepper`。流式標籤頁允許跳到任何區段。
- **同類項目的長清單**，例如訊息或搜尋結果：使用自帶捲動的清單。流式標籤頁的每個區段都會預先建立，適合幾十個區段，而不是成千上萬列。
- **應用程式頁面之間的導覽**：使用側邊欄。流式標籤頁只在一個頁面內移動。

## 用法

項目是 `FluidTabItem` 容器。`Header`（以及選用的 `Icon`）是標籤，`Content` 是該標籤的區段。給控制項一個高度，或把它放在高度受限的位置（格線列、DockPanel 的填滿區）：區段在控制項內部捲動。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:FluidTabControl Height="360" AutomationProperties.Name="Settings">
        <arc:FluidTabItem Header="General">
            <StackPanel Spacing="12">
                <TextBlock Text="General" FontSize="18" FontWeight="SemiBold" />
                <arc:Switch Content="Open at login" IsChecked="True" />
                <arc:Switch Content="Check for updates automatically" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Appearance">
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" FontSize="18" FontWeight="SemiBold" />
                <arc:Switch Content="Follow the system theme" IsChecked="True" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Account">
            <arc:Input Label="Email" Text="noor@larkspur.co" />
        </arc:FluidTabItem>
    </arc:FluidTabControl>
</UserControl>
```

`FluidTabControl` 衍生自 Avalonia 的 `SelectingItemsControl`，所以 `SelectedIndex`、`SelectedItem` 與 `SelectionChanged` 的用法和任何可選取清單一樣，兩個屬性都支援雙向繫結。控制項載入時選取第一個標籤。無論以何種方式選取標籤（點按、鍵盤、繫結、程式碼），它的區段都會捲動到位；捲動內容會改變選取項目，但不會再把內容捲回去。

`FluidTabItem` 衍生自 `HeaderedContentControl`。標籤列顯示它的 `Header` 與 `Icon`；它的 `Content` 顯示在捲動區域中，所以其中的控制項在使用者捲開再捲回來時保持狀態。停用的標籤會被方向鍵略過且無法選取；隱藏的標籤（`IsVisible="False"`）也會隱藏它的區段。

### 結構

1. **標籤列**：寬 200 px（`TabListWidth`），內距 8 px，在區段一側有一條淡邊框色的 1 px 分隔線。標籤放不下時它獨立捲動。
2. **標籤**（`FluidTabItem`）：至少 36 px 高，內距 14 px × 8 px，圓角 12 px。文字使用小號字、中等字重與弱化文字色。滑鼠停留時以弱化表面色填滿；選取的標籤使用強調色（強），停用的標籤降到 50% 不透明度。
3. **圖示**：在標題之前，與標題相距 10 px。可以是任何內容：字形、文字或範本的結果。
4. **選取醒目提示**：選取標籤後方的淡強調色填滿，在標籤之間滑動。
5. **側邊指示器**：選取標籤外側邊緣的 3 px 強調色直條，上下各內縮 9 px。標籤列在右側時它移到右邊緣。
6. **捲動區域**：依項目順序排列所有區段，間距 40 px（`SectionSpacing`），內距 24 px（`ContentPadding`）。
7. **焦點環**：取得焦點的標籤周圍的 2 px 環。

### 位置與錨點

`TabPlacement` 把標籤列放在 `Left`（預設）或 `Right`。`SnapAlignment` 決定以捲動區域的哪條線為準：`Top`（預設）時，選取的區段捲動到頂端，位於頂端的區段就是目前區段；`Center` 時，選取的區段捲動到中間，跨過中線的區段就是目前區段。兩者都可以隨時切換，不會遺失選取項目與捲動位置。

<Demo name="Placement" />

設定、表單與文件用 `Top`，因為每個區段以使用者最先閱讀的標題開頭。區段比捲動區域短、且作為整體閱讀時（例如卡片或步驟）用 `Center`，讓目前區段位於中間，前後的區段圍在它周圍。

兩端的規則很簡單：捲動到最頂端時選取第一個標籤；捲動到最底端時選取最後一個標籤，即使最後幾個區段太短、無法到達錨線。無法到達錨線的區段會盡量捲動，它的標籤保持選取。

### 資料驅動的標籤

設定 `ItemsSource`。`ItemTemplate` 呈現標籤，`IconTemplate` 呈現標籤的圖示，`ContentTemplate` 呈現區段；三者接收同一個資料項目，所以標籤和它的區段顯示同一個物件。雙向繫結 `SelectedItem`（或 `SelectedIndex`），即可在檢視模型中保存目前區段。

<Demo name="DataBound" />

```csharp
public sealed record HandbookChapter(string Number, string Title, string Subtitle, string Body);

public partial class HandbookViewModel : ObservableObject
{
    public ObservableCollection<HandbookChapter> Chapters { get; } = new();

    [ObservableProperty]
    private HandbookChapter? _currentChapter;
}
```

```xml
<arc:FluidTabControl ItemsSource="{Binding Chapters}"
                     SelectedItem="{Binding CurrentChapter, Mode=TwoWay}"
                     AutomationProperties.Name="Handbook chapters">
    <arc:FluidTabControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:FluidTabControl.ItemTemplate>
    <arc:FluidTabControl.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Body}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:FluidTabControl.ContentTemplate>
</arc:FluidTabControl>
```

範本是強型別的（`x:DataType`）並使用編譯繫結，所以資料驅動的標籤相容於修剪與 Native AOT；沒有需要透過反射讀取的成員路徑。本身是 `FluidTabItem` 的資料項目會被直接使用。

### 平滑捲動

選取標籤時，內容在 `AnimationDuration`（預設 480 ms）內依 `EasingFunction`（預設 Arc 的 in-out 曲線）捲動到它的區段。時長為零、`IsAnimationEnabled="False"` 或減少動態效果時，直接跳到區段。上面的範例使用了 `AnimationDuration="0:0:0.6"` 與 `EasingFunction="CubicEaseOut"`。

捲動過程中經過的標籤不會被選取，選取項目直接移到被選的標籤。捲動中再選另一個標籤，會從目前位置繼續捲向新的區段。用滾輪、觸控板、捲軸或鍵盤捲動會立即停止平滑捲動，把控制權交還給使用者。捲動中某個區段的高度變化（圖片載入、群組展開）時，捲動會跟隨它的新位置。

```xml
<arc:FluidTabControl AnimationDuration="0:0:0.3"
                     EasingFunction="QuadraticEaseOut"
                     IsAnimationEnabled="True" />
```

### 新增與移除標籤

區段始終與項目一致。在 `ObservableCollection` 或 `Items` 中新增、移除、取代或移動項目，捲動區域都會隨之更新；清空項目會清空區段。移除選取的標籤時，會選取另一個標籤並把它的區段捲動到位。修改 `FluidTabItem` 的 `Content` 會取代它的區段，隱藏該項目會隱藏它的區段。

<Demo name="DynamicItems" />

### 在程式碼中選取

```csharp
settings.SelectTab(3);
settings.SelectionChanged += (_, _) =>
{
    status.Text = $"Reading: {settings.SelectedItem}";
};
```

`SelectTab` 在索引處的標籤存在且已啟用時選取它，否則傳回 `false`。對已選取的標籤呼叫它，會把它的區段重新捲動到位，適合使用者稍微捲開之後使用。設定 `SelectedIndex` 或 `SelectedItem` 也會同樣捲動，但不檢查標籤是否已啟用。

### 自訂標籤與圖示

`Header` 與 `Icon` 接受任何內容。在 `FluidTabItem` 上使用 `HeaderTemplate` 與 `IconTemplate`，或對資料項目在控制項上使用 `ItemTemplate` 與 `IconTemplate`。有圖示的標籤有 `:has-icon` 虛擬類別；控制項依位置設定 `:left` 或 `:right`，樣式可以據此為每一側調整標籤。

```xml
<arc:FluidTabItem Header="Billing">
    <arc:FluidTabItem.Icon>
        <arc:ArcGlyph Data="{x:Static arc:ArcIcons.CreditCard}" Size="16" StrokeThickness="1.8" />
    </arc:FluidTabItem.Icon>
    <arc:FluidTabItem.HeaderTemplate>
        <DataTemplate>
            <StackPanel Spacing="2">
                <TextBlock Text="{Binding}" />
                <TextBlock Text="Plan and invoices" FontSize="12" FontWeight="Normal" />
            </StackPanel>
        </DataTemplate>
    </arc:FluidTabItem.HeaderTemplate>
    <TextBlock Text="Business plan, renews November 30, 2026." />
</arc:FluidTabItem>
```

### 資料結構與效能

每個區段在控制項載入時建立並一直存在，這樣使用者才能捲動瀏覽全部區段。這適合最多幾十個區段的設定頁與文件。如果一個區段裡有很長的清單，在區段中放一個有固定高度的虛擬化清單，或改用清單控制項。

控制項只在需要時讀取區段位置（選取、捲動、平滑捲動中的版面變化），其他時候不訂閱版面事件。區段高度可以任意，也可以隨時變化。

### 範例：帳號設定

![左側是標籤列、右側依序排列一般與外觀區段的設定頁](images/AccountSettings.png)

```xml
<Border Width="720"
        Height="340"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        ClipToBounds="True">
    <arc:FluidTabControl AutomationProperties.Name="Settings">
        <arc:FluidTabItem Header="General">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Settings}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <StackPanel Spacing="12">
                <TextBlock Text="General" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="Startup, language and updates." FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Switch Content="Open at login" IsChecked="True" />
                <arc:Switch Content="Check for updates automatically" IsChecked="True" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Appearance">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Palette}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="Theme, density and motion." FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Switch Content="Follow the system theme" IsChecked="True" />
                <arc:Switch Content="Compact rows" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Account">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.User}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <arc:Input Label="Email" Text="noor@larkspur.co" />
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Privacy">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Shield}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <arc:Switch Content="Send crash reports" IsChecked="True" />
        </arc:FluidTabItem>
    </arc:FluidTabControl>
</Border>
```

### 文案規範

- **標籤文字**：一兩個詞，用名詞命名區段：「通知」「帳單」。
- **區段標題**：每個區段以重複標籤文字的標題開頭，捲動到這裡的人不用看標籤列也知道自己在哪。
- **順序**：最常造訪的區段放在前面，危險的區段（刪除帳號）放在最後。

| 不要寫 | 改寫為 |
|---|---|
| 標籤「其他」 | 進階 |
| 標籤「通知設定相關內容」 | 通知 |
| 區段標題「設定群組 3」 | 隱私 |

### 最佳做法

**建議**

- 每個區段都有一個與標籤文字一致的標題。
- 限制控制項高度，讓區段在控制項內部捲動，而不是讓整個頁面變長。
- 用 `AutomationProperties.Name` 為標籤列命名。
- 讓標籤數量少到可以一眼看完，大約四到十個。

**避免**

- 在區段中巢狀放置同方向、但沒有自身高度的捲動區域；內層會搶走滾輪，選取項目就不再跟隨。
- 用它呈現不應一起閱讀的檢視；請用 `Tabs`。
- 結尾有很多永遠到不了錨線的短區段；這種情況優先用 `Center`，或用 `ContentPadding` 增加底部內距。

### 常見問題

**為什麼最後一個區段還沒到頂端，最後一個標籤就被選取了？** 捲動到最底端時一定選取最後一個標籤，因為較短的最後區段永遠無法到達頂端那條線。

**平滑捲動進行時使用者可以捲動嗎？** 可以。捲動區域中的任何滾輪、觸控板、捲軸或按鍵輸入都會停止平滑捲動，選取項目重新跟隨使用者的捲動。

**來自檢視模型的選取會讓捲動跳動嗎？** 不會；它和點按一樣平滑捲動。若希望直接跳轉，設定 `IsAnimationEnabled="False"`。

## API 參考

<!-- api: FluidTabControl -->

<!-- api: FluidTabItem -->

## 無障礙

- 標籤列的角色是 Tab，每個 `FluidTabItem` 的角色是 TabItem 並帶有選取狀態。用 `AutomationProperties.Name` 為控制項命名。
- 標題是字串時，每個標籤以標題命名。其他標題請明確命名。
- 只有選取的標籤在 Tab 鍵順序中。Tab 鍵從標籤列進入區段，區段中的每個控制項都能依序到達。
- 在區段內按下的鍵屬於區段中的控制項；只有標籤取得焦點時，方向鍵才會切換標籤。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在選取的標籤上進入標籤列；從標籤列進入區段 |
| `Up` / `Down` | 選取上一個或下一個已啟用的標籤並捲動到它的區段；到兩端時停止 |
| `Home` / `End` | 選取第一個或最後一個已啟用的標籤並捲動到它的區段 |
| `Enter` / `Space` | 再次捲動到取得焦點標籤的區段 |
| 指標按下 | 左鍵按下時選取標籤；觸控在放開時選取 |

## 動態效果

- 選取標籤時，內容在 `AnimationDuration` 內依 `EasingFunction` 捲動到它的區段。
- 選取醒目提示與側邊指示器以 morph 彈簧滑到選取的標籤。
- 標籤列以 smooth 彈簧把選取的標籤捲動到可見位置。
- 減少動態效果時直接跳到區段，醒目提示立即移動；關閉動態效果時同樣如此。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選取一個標籤 | 內容在 480 ms 內以 in-out 曲線捲動到它的區段 | 跳轉 | 跳轉 |
| 選取項目移動 | 醒目提示與指示器以 morph 彈簧滑動 | 立即移動 | 立即移動 |
| 選取的標籤在標籤列外 | 標籤列以 smooth 彈簧捲動 | 跳轉 | 跳轉 |
| 滑鼠停留與選取的標籤 | 文字與填滿顏色交叉淡化 | 立即 | 立即 |
| 使用者捲動內容 | 選取項目立即跟隨，沒有動畫 | 相同 | 相同 |

## 響應式

- `FluidTabControl` 沒有中斷點。標籤列保持 `TabListWidth`，區段占用其餘寬度，所以在窄螢幕上請減小 `TabListWidth` 或隱藏圖示。
- 在手機尺寸的檢視中，給標籤列約 120 px，或改用位於內容上方、不需要側欄的 `Tabs`。
- 標籤比控制項高時，標籤列獨立捲動，並讓選取的標籤保持可見。
- 標籤至少 36 px 高。輕觸與點按一樣選取標籤，在標籤列上拖曳會捲動它。

## 相關元件

- `Tabs`：一次顯示一個的並列檢視。
- `Accordion`：在原位展開的區段。

| 元件 | 何時選擇 |
|---|---|
| `Tabs` | 每個檢視取代其他檢視，一起閱讀沒有意義 |
| `Accordion` | 使用者每次開啟一個或幾個區段 |
| `Stepper` | 區段必須依序完成 |
| `ScrollArea` | 只需要捲動區域，不需要標籤列 |
