---
title: 步驟條
summary: 顯示使用者在多步驟流程中的位置，以及已完成的步驟。
source-hash: 8ff6646e8303
---

<Demo name="Checkout" />

## 何時使用

`Stepper` 顯示在固定步驟序列中所處的位置，以及哪些步驟已經完成。它位於流程內容的上方，例如結帳或引導精靈，告訴使用者已經走了多遠、還剩什麼。它只表示進度：步驟內容、驗證以及進入下一步的規則仍然由你的應用程式負責。

- **電商商店的結帳**：帳號、配送、付款與確認。使用者會看到配送已完成、接下來是付款，並且可以回到配送修改地址，而不會遺失訂單的其餘部分。
- **SaaS 管理主控台中的工作區設定**：建立工作區、邀請團隊、新增帳單並確認。付款步驟失敗時，錯誤會出現在該步驟上，也就是使用者必須處理的地方。
- **金融科技導入中的身分驗證**：上傳證件、自拍核驗、居住地址與最終確認。步驟是嚴格的，後面的步驟絕不會在前面的步驟完成之前顯示為已完成。
- **內部工具中的多步驟匯入**：上傳 CSV、對應欄位、預覽資料列並確認。垂直步驟條配合每一步的說明，在側邊面板中解說每個階段。
- **維運主控台中的操作手冊**：確認、緩解、驗證並關閉一次事件。步驟條顯示值班團隊在一個始終依相同順序執行的流程中所處的位置。

## 何時不用

- **已完成工作的單一百分比**，例如上傳或每晚匯出：用 `Progress`，它將一個值與整體作比較。每百分之一對應一步的步驟條將無法閱讀。
- **使用者可以依任意順序開啟的分區**，例如帳號設定頁的標籤頁：用 `Tabs`。步驟條隱含順序，並且只能從中選取已完成的步驟。
- **事件的歷程記錄**，例如訂單的稽核紀錄或工單的回覆：用 `Timeline`。時間軸列出隨時間發生的事件；步驟條顯示固定流程目前所處的位置。
- **仍在載入的內容**，例如某一步的欄位在其資料抵達之前：在步驟本文中顯示 `Skeleton`。步驟條本身沒有載入狀態。
- **從多個選項中選取一個值**：用 `Select`、`RadioGroup` 或 `SegmentedControl`。步驟條不收集值，它只回報流程中的位置。

## 用法

為 `Stepper` 提供步驟，以及進行中步驟的索引。`CurrentIndex` 從零開始計數。索引等於步驟數時，所有步驟都標示為已完成。步驟既可以像下例一樣內嵌書寫，也可以從集合繫結。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Stepper CurrentIndex="1">
        <arc:Stepper.Steps>
            <arc:StepperStep Label="帳號" Description="建立登入資訊" />
            <arc:StepperStep Label="配送" Description="寄送到哪裡" />
            <arc:StepperStep Label="付款" Description="信用卡或發票" />
            <arc:StepperStep Label="確認" Description="檢查並確認" />
        </arc:Stepper.Steps>
    </arc:Stepper>
</UserControl>
```

`Stepper` 衍生自 Avalonia 的 `TemplatedControl`，而不是項目控制項。它負責整個流程：把步驟排成一個面板、繪製步驟之間的連接線，並把流程當作一個整體做動畫。每個步驟在面板中作為一個內部項目繪製，這就是為什麼進度跳躍時步驟條能依序填滿連接線。由於它不是項目控制項，因此沒有 `ItemTemplate`；步驟內容一律是標籤與說明。

兩條規則決定了整個控制項的行為：

- `CurrentIndex` 是唯一的資料來源。它之前的步驟已完成，它所在的步驟為目前步驟，之後的步驟為待辦。小於零的值視為零，大於步驟數的值視為步驟數。
- 步驟的 `Error` 會覆寫其狀態。即使帶錯誤的步驟位於目前索引之前或之後，它也會顯示為失敗，因此只應在使用者必須修正的步驟上設定 `Error`。

### 結構

1. **標記**（每個步驟一個）：直徑 28 px 的圓盤，帶 1 px 的內框線，使用強邊框色。步驟待辦或進行中時顯示步驟編號，完成時顯示繪製的勾號，有錯誤時顯示警示標記。
2. **光暈**：目前步驟外 4 px 處的一圈環，使用強調色的淡化色。失敗的步驟則改用強度為 16% 的危險色光暈。
3. **連接線**：兩個標記之間 2 px 的線條。軌道為邊框色；已到達的部分以強調色填滿。
4. **標籤**（`StepperStep.Label`）：步驟名稱，14 px、中等字重，行高 20 px。已完成的步驟使用次要文字顏色，目前步驟與出錯的步驟使用前景色，待辦步驟使用淡化文字顏色。
5. **說明**（`StepperStep.Description`）：12 px 的文案，行高 16.8 px，位於標籤下方 2 px，使用淡化文字顏色。`Error` 會以危險色取代它。
6. **註解文字**（僅精簡模式）：目前步驟的標籤與詳細資訊，上方留 12 px 間距。只在精簡模式的水平步驟條中顯示。

尺寸來自步驟條的設計權杖。水平方向上，每個步驟占據自己的欄，欄與欄之間留 16 px，標記與標籤列之間留 12 px。垂直方向上，標記位於左側，與文字之間留 12 px，每個步驟下方留 20 px（精簡時為 24 px），首行標籤下移 4 px，使其與標記置中對齊。

### 繫結步驟

`ItemsSource` 優先於內嵌的 `Steps`；設定它之後，內嵌步驟會被忽略。繫結一組 `StepperStep` 物件的集合。步驟條會監看集合以及每個步驟，因此修改某一步的 `Label`、`Description` 或 `Error` 會直接更新步驟條，而不需要重建。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}" />
```

為每個步驟設定穩定的 `Id`。`Id` 是該步驟動畫的鍵，因此當集合被重新排序或在其前面新增步驟時，該步驟會保留自己的狀態動畫。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有步驟、目前索引以及進入下一步的規則，步驟條只負責顯示。使用 CommunityToolkit.Mvvm 時，下面的檢視模型保存流程狀態，並在對應步驟上標記付款失敗。

```csharp
public partial class CheckoutViewModel : ObservableObject
{
    private readonly IOrderService _orders;

    public CheckoutViewModel(IOrderService orders)
    {
        _orders = orders;
    }

    public ObservableCollection<StepperStep> Steps { get; } = new()
    {
        new StepperStep("帳號", "建立登入資訊") { Id = "account" },
        new StepperStep("配送", "寄送到哪裡") { Id = "shipping" },
        new StepperStep("付款", "信用卡或發票") { Id = "payment" },
        new StepperStep("確認", "檢查並確認") { Id = "review" }
    };

    [ObservableProperty]
    private int _currentStep = 1;

    [RelayCommand]
    private async Task PayAsync()
    {
        var result = await _orders.PayAsync(OrderId);
        Steps[2].Error = result.IsSuccess ? null : result.Error;
        if (result.IsSuccess)
        {
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void Continue()
    {
        CurrentStep = Math.Min(Steps.Count, CurrentStep + 1);
    }
}
```

```xml
<StackPanel Spacing="20">
    <arc:Stepper ItemsSource="{Binding Steps}"
                 CurrentIndex="{Binding CurrentStep}"
                 IsNavigable="True" />
    <arc:Button Content="繼續" Command="{Binding ContinueCommand}" />
</StackPanel>
```

- `CurrentIndex` 預設為雙向繫結，因此當使用者選取已完成的步驟時，檢視模型能得知其返回的步驟。像上面那樣明確寫出 `Mode=TwoWay`，可以表明意圖。
- 步驟條不會阻擋移動。上例中的 `Continue` 只會向前移動；決定使用者能否離開某一步的規則應放在檢視模型中，只有在通過驗證時才設定 `CurrentIndex`。
- 在檢視模型中為步驟設定 `Error`，並用 `null` 或空字串將其清除。步驟條會把兩者都視為「沒有錯誤」。

### 錯誤與復原

錯誤應屬於使用者能夠修正的那個步驟。狀態立即改變，標記變為警示，錯誤文字取代說明，因此原因會顯示在需要操作的地方。

- 當伺服器拒絕該步驟時（例如信用卡被拒、檔案過大），設定 `Error`，並讓使用者停留在該步驟。`CurrentIndex` 不變，因此流程仍停在原處。
- 值變動或使用者重試時立即清除 `Error`。已修正的步驟上殘留的舊錯誤會被誤讀為第二次失敗。
- 已完成步驟上的錯誤（例如審核之後被撤銷的付款）會把該步驟變為警示，而目前索引保持不變。可以考慮把目前索引移回該步驟，讓使用者在上下文中看到原因。

### 垂直方向與錯誤

<Demo name="VerticalWithError" />

將 `Orientation` 設為 `Vertical` 即得到時間軸：標記位於左側，標籤與說明位於其旁邊，連接線向下延伸。它適用於側邊面板、抽屜或分割版面的一欄，步驟從上到下閱讀。如下例所示為它設定寬度，使標籤換行，而不是溢出該欄。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             Orientation="Vertical"
             Width="320" />
```

### 精簡與目前詳細資訊

寬度小於 480 px 的水平步驟條會自動切換為精簡模式。設定 `IsCompact="True"` 可以在任何寬度下要求精簡模式。`IsCompact="False"` 並不會關閉自動規則：窄的水平步驟條仍然是精簡的，因此完整的步驟條應至少有 480 px 寬。

<Demo name="Compact" />

在精簡模式下，標記保留，標籤隱藏，步驟條下方的註解文字會標示目前步驟及其說明。垂直步驟條不會依此規則變為精簡，因為它有足夠的空間容納標籤。

`Details` 決定顯示哪些說明。`StepperDetails.All`（預設值）顯示每個說明。`Current` 只顯示目前步驟的說明，使長流程在狹小空間中保持簡潔。無論此設定為何，錯誤一律會顯示。

<Demo name="CurrentDetails" />

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             IsCompact="True" />
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="1"
             Details="Current" />
```

### 可導覽的步驟

設定 `IsNavigable` 後，使用者可以透過點擊或鍵盤返回已完成的步驟。唯讀的步驟條（預設值）只顯示進度：其項目不接收焦點，也忽略點擊。當步驟在流程回退之前必須執行某些工作（例如儲存目前步驟的草稿）時，請處理 `StepSelected`。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}"
             IsNavigable="True" />
```

```csharp
stepper.StepSelected += (_, e) =>
{
    // e.Index 是被選取的已完成步驟；e.Step 是它的 StepperStep。
    SaveDraft();
};
```

`StepSelected` 是路由事件。它的引數包含 `Index`（所選步驟從零開始的索引）與 `Step`（`StepperStep` 本身）。如果沒有處理常式將事件標記為已處理，所選步驟就會成為目前步驟。若處理常式設定 `e.Handled = true`，則由它接手：索引不會改變，因此當移動應該發生時，檢視模型必須自行設定 `CurrentIndex`。

程式碼可以像點擊一樣透過 `Select(int index)` 選取步驟。當 `IsNavigable` 關閉、索引超出範圍，或索引不在目前步驟之前時，它不會有作用。

只有已完成的步驟可以被選取。目前步驟與待辦步驟不可點擊；在已完成的步驟上按下指標時，其標記會稍微縮小，直到按下結束；只有在指標於其上放開時，該步驟才會被選取。

### 範例：為新工作區設定帳單

SaaS 管理主控台引導擁有者完成四個步驟：為工作區命名、邀請團隊、新增帳單並確認。由於信用卡被拒，帳單步驟已經失敗。步驟條停留在該步驟上，標記變為警示，錯誤文字取代說明，因此使用者能看到需要修正什麼以及在哪裡修正。頁尾提供一個「返回」操作（次要按鈕）和一個主要操作，因此下一步很明確。卡片與頁尾都位於使用標準表面與邊框的面板中，使整個流程在頁面上呈現為一個整體。

![帶有失敗帳單步驟與下一步操作的工作區設定卡片](images/SetupWorkspace.png)

```xml
<Border Width="680"
        Padding="24"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="24">
        <TextBlock Text="Set up Acme Analytics"
                   FontSize="{arc:ArcDesignTokenResource TypeLG}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:Stepper CurrentIndex="2" Label="Workspace setup" CompleteLabel="Setup complete">
            <arc:Stepper.Steps>
                <arc:StepperStep Id="workspace" Label="Workspace" Description="Acme Analytics" />
                <arc:StepperStep Id="team" Label="Team" Description="Invite 6 people" />
                <arc:StepperStep Id="billing"
                                 Label="Billing"
                                 Description="Card or invoice"
                                 Error="Card declined. Try another card." />
                <arc:StepperStep Id="review" Label="Review" Description="Check and confirm" />
            </arc:Stepper.Steps>
        </arc:Stepper>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Back" Variant="Secondary" />
            <arc:Button Content="Continue" />
        </StackPanel>
    </StackPanel>
</Border>
```

範例使用內嵌步驟，因為內容是固定的。對於執行時會變動的流程，應依前文所示繫結 `ItemsSource`。失敗的步驟保持其在序列中的位置：步驟條從不跳過步驟，檢視模型決定使用者能否離開失敗的步驟。

### 文案規範

- **步驟標籤**：一到兩個字詞，句首大寫，用來命名步驟，例如「配送」「付款」「確認」。避免動詞與完整句子，細節由說明承擔。
- **說明**：關於步驟內容的簡短提示，例如「信用卡或發票」「邀請 6 人」。它顯示在標籤下方，是標籤不足以說明時使用者會閱讀的內容。
- **錯誤**：說明哪裡出了問題以及該怎麼做，用詞與步驟名稱保持一致。錯誤會取代說明，因此它必須能獨立成立。
- **完成標籤**：說明流程已完成的簡短陳述，例如「設定完成」或「所有步驟已完成」。它會向輔助技術播報，並顯示在精簡模式的註解文字中。
- **步驟條標籤**：整個流程的名稱，例如「結帳進度」。它是該群組的無障礙名稱。

| 不要寫 | 改為 |
|---|---|
| 第 1 步 | 帳號 |
| 請在此輸入您的收件地址 | 配送 |
| 錯誤！ | 信用卡被拒，請換一張卡。 |
| 完成 | 所有步驟已完成 |
| 進入下一步 | 繼續 |
| 進度 | 結帳進度 |

### 最佳做法

**建議**

- 每個流程使用一個步驟條，並讓步驟是同一類東西：一組頁面，而不是頁面與操作的混合。
- 把錯誤放在使用者必須修正的步驟上，並讓目前索引停留在該步驟，使原因與操作位於同一處。
- 只有在返回已完成的步驟是安全的時候才使用 `IsNavigable`。先儲存目前步驟的草稿，可以在檢視模型中完成，也可以在 `StepSelected` 中完成。
- 當步驟來自資料時，為每個步驟設定穩定的 `Id`，使每個步驟在變動時保留自己的動畫。
- 為垂直步驟條設定 `Width`，使標籤在欄內換行。

**避免**

- 把步驟條用於使用者可以從任意位置進入的流程。它的順序由 `CurrentIndex` 決定，無法跳到待辦步驟；需要自由導覽時請使用 `Tabs`。
- 在有未完成步驟的流程中，把 `CurrentIndex` 設到末尾來表示「已完成」。等於步驟數的索引會把所有步驟都標示為已完成，因此流程必須真正完成。
- 在 `Label` 中放入長文字。長標籤會在步驟標題處換行，並把其他步驟擠出對齊；應把細節移到 `Description` 中。
- 同時在多個步驟上設定 `Error`。連續多個警示會讓使用者不清楚該先修正哪一步；應標示第一個失敗的步驟，並在頁面上說明其餘問題。
- 只依靠顏色表示狀態。每種狀態都有自己的標記：數字、勾號和警示標記，因此即使沒有顏色，流程也能讀懂。

### 常見問題

**當某個步驟無效時，步驟條會阻止使用者繼續前進嗎？** 不會。步驟條只顯示 `CurrentIndex` 與 `Error`。在提高索引之前，請在檢視模型中檢查規則，並在失敗的步驟上設定 `Error`。

**為什麼我不能用 Tab 鍵切換到目前步驟？** 開啟 `IsNavigable` 後，Tab 會停在每個已完成的步驟上。目前步驟需要從已完成的步驟用方向鍵到達，`End` 會移動到它。這樣即使在長流程中，Tab 順序也能保持簡短。

**如何在窄螢幕上只顯示目前步驟的標題？** 在水平步驟條上設定 `IsCompact="True"`。標記保留，下方的註解文字會標示目前步驟及其說明。不設定 `IsCompact` 時，寬度小於 480 px 的水平步驟條會自動這樣做。

**如何變更輔助技術使用的預設名稱？** 為流程設定 `Label`（預設為 "Progress"），為結束狀態設定 `CompleteLabel`（預設為 "All steps complete"）。每個步驟的名稱由其標籤與狀態產生，例如「帳號，已完成」。

**流程執行中步驟數量變動會怎樣？** 集合變動時，步驟條會重建其步驟，並把索引限制在新數量的範圍內顯示；它不會改寫 `CurrentIndex`。請讓檢視模型中的索引與集合保持同步，使使用者不會在變動後落到另一個步驟上。

## API 參考

<!-- api: Stepper -->

<!-- api: StepperStep -->

`StepSelected` 的事件引數：

| 成員 | 類型 | 意義 |
|---|---|---|
| `Index` | `int` | 所選已完成步驟從零開始的索引 |
| `Step` | `StepperStep` | 所選的步驟 |

## 無障礙

- `Label` 為步驟條命名，預設為 "Progress"。將它設為流程的名稱，例如「結帳進度」。
- 每個步驟都以其狀態命名，例如「帳號，已完成」「確認，未開始」或「帳單，錯誤」，因此失敗的步驟既可透過名稱辨識，也可透過警示標記辨識。
- 目前步驟會被標示為目前步驟，步驟條會回報其位置，例如「第 3 步，共 4 步：付款」。
- 步驟變化會以禮貌的方式播報，因此不需移動焦點即可讀到新位置。當所有步驟都已完成時，改為播報 `CompleteLabel`。
- 唯讀的步驟條不接收焦點。它會作為一組被讀出，連同其步驟及各自的狀態。
- 可導覽的步驟條會把焦點保留在步驟上。焦點以一圈環繪製，位於步驟標題外 4 px。
- 尚未到達的步驟無法取得焦點，並會被 Tab 略過。

### 鍵盤

只有在 `IsNavigable` 開啟時按鍵才會生效。否則步驟條不接收焦點。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在已完成的步驟之間移動；目前步驟與待辦步驟不是 Tab 停駐點 |
| `Right` / `Down`、`Left` / `Up` | 移到下一個或上一個可到達的步驟（已完成或目前）；在由右至左的水平步驟條中，`Left` 與 `Right` 互換 |
| `Home` | 移到第一個可到達的步驟 |
| `End` | 移到最後一個可到達的步驟，通常是目前步驟 |
| `Enter` / `Space` | 選取取得焦點的已完成步驟，與點擊相同；對目前或待辦步驟不會有作用 |

## 動態效果

- 進度跨越多個步驟時，連接線會依序填滿，新的目前標記會出現一圈逐漸擴大的光暈。
- 步驟完成時，數字會變為繪製的勾號；失敗時，變為警示標記。
- 變化的說明會從柔和的模糊中升起，它占用的空間以彈簧效果調整到新高度。
- 減少動態效果時，所有變化立即生效。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 進度向前或向後移動 | 連接線依序填滿或排空，每條相隔 80 ms；光暈在新的目前步驟周圍擴大 | 立即完成 | 立即完成 |
| 步驟完成 | 數字從 0.5 彈出，伴隨輕微模糊，其勾號筆畫被繪製出來 | 立即完成 | 立即完成 |
| 步驟失敗 | 標記變為警示，錯誤文字取代說明 | 立即完成 | 立即完成 |
| 說明變化 | 文字從柔和的模糊中升起，列以彈簧效果調整到新高度 | 立即完成 | 立即完成 |
| 標記與標籤顏色 | 標記顏色在 240 ms 內交叉過渡；標籤顏色以標準補間變化 | 立即完成 | 立即完成 |

按下已完成的步驟時，其標記會縮小到 0.92。指標懸停在已完成的步驟上時，其標籤會提亮為前景色。

## 相關元件

- `Progress`：工作以百分比而非步驟計量時使用。
- `Tabs`：使用者可以依任意順序切換分區時使用。
- `Skeleton`：某個步驟的內容仍在載入時使用。

| 元件 | 適用情境 |
|---|---|
| `Progress` | 一個值顯示單一任務完成了多少 |
| `Tabs` | 分區彼此平等，可以依任意順序開啟 |
| `Timeline` | 隨時間發生的事件列表本身就是內容，例如訂單歷程 |
| `Skeleton` | 某個步驟的內容正在載入，且尚未有形狀 |
| `Breadcrumb` | 使用者需要看到通往某個頁面的路徑，而非流程中的進度 |
| `Select` / `RadioGroup` | 步驟要求從一組選項中選擇一個值 |
