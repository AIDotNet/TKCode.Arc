---
title: 輪播
summary: 透過拖曳、輕掃或方向鍵瀏覽一排投影片。
source-hash: 6fd1e19ca14e
---

<Demo name="Slides" />

## 何時使用

`Carousel` 在頁面上展示一組簡短、有順序的同類項目，讓檢視者快速瀏覽後選擇其中一項。作用中的投影片位於中央，兩側的相鄰項目提示還有更多項目。它適合放在頁首、商店版塊或內容中心，這些項目屬於同一類事物，而且檢視者不需要對其中任何一項做出決定。

- **商店首頁的精選系列**：四到六個季節性系列，每個都是一張照片加上標題與商品數量。拖曳或點按箭頭即可看到其餘系列，檢視者打開符合需求的那一個。
- **飯店或民宿訂房頁的房型**：每張投影片是一間房，附上坪數與每晚價格，因此住客透過滑動即可比較房間，而不必開啟五個頁面。
- **產品更新日誌中的版本重點**：最新版本的三到四項功能，每張投影片一項，並關閉自動播放，讓讀者依自己的節奏瀏覽。
- **行銷網站上的客戶案例**：三到五個案例研究，每個包含一個標誌、一段引言與一個連結，排成一排，訪客無需長時間捲動即可瀏覽。

## 何時不用

- **需要逐一對每個項目做出決定**：用 `CardStack`，它會對每張卡片要求保留或略過，並記錄下來。
- **比較兩張圖片**：用 `ImageCompare`，它把前後對比放在同一畫面上，並以分隔線區分。
- **需要掃讀或排序的長記錄清單**：用 `Table`。輪播一次只顯示一張投影片，讀者無法跨列比較欄位。
- **必須完整閱讀的文字，例如條款、說明或檢核表**：用 `Accordion` 或 `ExpandableText`。投影片會自行切換，把讀者尚未讀到的文字隱藏起來。
- **在內容各不相同的面板之間切換**：用 `Tabs`。輪播意味著投影片是同一類的同級項目，而標籤頁則代表不同的區段。
- **使用者不能錯過的訊息，例如帳單警告或服務中斷**：用 `Alert` 或 `AnnouncementBar`。自動播放會把訊息移走，輪播並不適合承載這類內容。

## 用法

每個子元素成為一張投影片。`Label` 為輪播命名，供輔助技術使用。

```xml
<arc:Carousel Label="港灣套房精選">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
    <Border Height="200" Background="#8AA399" CornerRadius="12" />
</arc:Carousel>
```

`Carousel` 衍生自 Avalonia 的 `SelectingItemsControl`。它擁有項目清單，即帶有 `ItemTemplate` 的 `ItemsSource`，以及雙向的 `SelectedIndex`，且始終選取一張投影片。這使輪播與清單擁有相同的選取模型，因此繫結、`SelectionChanged` 與自動化選取的運作方式都一致。只有目前位置附近的投影片會被實例化，因此長清單的成本與短清單大致相當。

### 結構

1. **檢視區**：容納投影片的軌道。作用中的投影片位於中央。投影片寬度為輪播寬度的 `SlideWidthRatio`，上限為 `MaxSlideWidth`，`SlideGap` 設定投影片之間的間距。
2. **相鄰項目**：兩側的投影片縮放至 90%，並隨著遠離中心而降至 50% 的不透明度。它們的邊緣顯示在邊界中，這是存在更多投影片的提示。
3. **檢視區焦點環**：當檢視區取得鍵盤焦點時，在投影片外側 4 px 處繪製的 2 px 外框。
4. **控制列**：位於檢視區下方 16 px 的一列。當輪播只有一張或沒有投影片時隱藏。
5. **播放與暫停**（`Interval`）：列左側的按鈕。只有當 `Interval` 為正值且動態效果為完整模式時才會出現。它的名稱在「開始投影片輪替」與「暫停投影片輪替」之間切換。
6. **指示器**：列中間的頁面圓點。每個圓點為 6 px，作用中的圓點會延展成 22 px 的膠囊，並隨軌道移動。該列高 36 px。
7. **上一張與下一張**：右側的箭頭按鈕，相距 8 px。在第一張與最後一張投影片處，對應的按鈕處於軟停用狀態：它仍保留焦點，但不執行任何動作。

寬度低於 340 px 時輪播進入緊湊模式：箭頭按鈕之間的間距從 8 px 縮為 4 px，播放按鈕更靠近箭頭。

### 投影片寬度與間距

投影片佔輪播寬度的 80%（`SlideWidthRatio` 為 0.8），上限為 340 px（`MaxSlideWidth`）。輪播寬度達到 425 px 時即達到上限，因此在寬頁面上投影片保持固定的閱讀尺寸，多出的寬度用來顯示更多相鄰項目。若想讓投影片佔據更多列寬，可將比例提高到 0.9；若想在窄欄中顯示更多相鄰項目，可降到 0.7。除非投影片本身有需要更多留白的陰影或邊框，否則 `SlideGap` 維持 16 px。

投影片尺寸跟隨輪播自身的寬度，而不是視窗寬度，因此放在窄側邊欄中的輪播與放在主內容區的輪播保持相同的比例。

### 繫結項目

當投影片來自資料時，使用 `ItemsSource` 與 `ItemTemplate`。

```xml
<arc:Carousel Label="港灣套房" ItemsSource="{Binding Suites}">
    <arc:Carousel.ItemTemplate>
        <DataTemplate>
            <Border Height="200" Background="#2F4858" CornerRadius="12">
                <TextBlock Text="{Binding Name}" Margin="18" />
            </Border>
        </DataTemplate>
    </arc:Carousel.ItemTemplate>
</arc:Carousel>
```

### 繫結到檢視模型

檢視模型擁有投影片，以及輪播應該顯示的位置。將 `SelectedIndex` 設為雙向繫結，使檢視模型能在啟動時設定投影片，並在檢視者切換時讀回位置。將 `IsAutoPlay` 設為雙向繫結，使設定開關能開啟或關閉輪替。

```csharp
public interface IStorefrontService
{
    IEnumerable<Collection> GetFeatured();
}

public partial class HomeViewModel : ObservableObject
{
    public HomeViewModel(IStorefrontService storefront)
    {
        Collections = new ObservableCollection<Collection>(storefront.GetFeatured());
    }

    public ObservableCollection<Collection> Collections { get; }

    [ObservableProperty]
    private int _featuredIndex;

    [ObservableProperty]
    private bool _rotateFeatured;
}
```

```xml
<StackPanel Spacing="16">
    <arc:Carousel Label="精選系列"
                  ItemsSource="{Binding Collections}"
                  SelectedIndex="{Binding FeaturedIndex, Mode=TwoWay}"
                  Interval="0:0:6"
                  IsAutoPlay="{Binding RotateFeatured, Mode=TwoWay}">
        <arc:Carousel.ItemTemplate>
            <DataTemplate>
                <Border Height="200" Background="#2F4858" CornerRadius="12">
                    <TextBlock Text="{Binding Title}" Margin="18" />
                </Border>
            </DataTemplate>
        </arc:Carousel.ItemTemplate>
    </arc:Carousel>
</StackPanel>
```

- 優先使用繫結的 `SelectedIndex`，而不是在檢視模型中呼叫 `GoTo()`。這樣檢視模型無需持有控制項的參照即可移動輪播，同一個繫結也能處理檢視者自己的操作。
- `SelectionChanged` 會在檢視者操作與輪替時觸發。用它來更新讀數或記錄瀏覽，並把該值視為目前的投影片，而不是一個決定。
- `IsRotating` 與 `CanRotate` 是唯讀的。讀取它們可以得知輪替是否正在執行，但只能透過 `IsAutoPlay` 變更輪替。
- 輪播沒有命令介面。下一張、上一張與跳至都是控制項上的方法，因此需要它們的檢視模型應將輪播的 `SelectedIndex` 保持為唯一的資料來源。

### 由程式碼驅動

```csharp
carousel.SelectionChanged += (_, _) => readout.Text = $"第 {carousel.SelectedIndex + 1} 張，共 {count} 張";

carousel.Next();
carousel.Previous();
carousel.GoTo(3);
```

`Next` 與 `Previous` 在兩端停止，不會循環。`GoTo` 會把索引限制在有效範圍內，因此超過最後一張的值會選取最後一張投影片。

### 投影片尺寸與標籤

`SlideLabelFormatter` 產生為每張投影片播報的文字，並作為投影片的無障礙名稱。若未設定，輪播使用本地化的「2 / 5」格式。當投影片名稱本身有意義時（例如投影片上內容的標題），應使用格式化工具。

```csharp
carousel.SlideLabelFormatter = (index, count) => $"檔案照片 {index + 1}，共 {count}";
```

### 自動播放

將 `Interval` 設為正的時間長度即可加入播放控制項。輪播會依該間隔向前移動，並在最後一張之後回到第一張。`IsAutoPlay` 開始輪替，播放控制項則切換其狀態。輪替預設關閉，因此由檢視者決定投影片何時移動。

投影片顯示期間，指示器會從右向左逐漸耗盡，讓檢視者知道距離下一張還有多久。只有在動態效果為完整模式且至少有兩張投影片時，輪替才可用。

<Demo name="Rotation" />

```xml
<arc:Carousel Label="季節性優惠" Interval="0:0:4" IsAutoPlay="True">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
</arc:Carousel>
```

### 節制地使用動態效果

輪替會在沒有請求的情況下移動內容，因此只應在投影片屬於裝飾或預告，且讀者不需要看完每一張時使用。對於檢視者必須閱讀的內容，例如優惠條款、產品價格或警告，應保持關閉。

當檢視者需要控制時，輪替會自動暫停：指標位於輪播上方時、鍵盤焦點位於其中時、拖曳進行時、輪播捲出可視範圍時以及視窗最小化時。原因消失後，輪替會恢復。播放控制項始終可見，因此檢視者可以徹底停止輪替。使用至少四秒的間隔，讓投影片有時間被讀完再移動。

減少動態效果會完全關閉輪替，因此要求減少動態效果的檢視者會看到第一張投影片，並可以手動瀏覽其他投影片。

### 長輪播

有 120 張投影片時，只會實例化目前位置附近的投影片。指示器則會顯示 120 個圓點。它會捲動，使作用中的膠囊保持在中央，並在兩端淡出，因此這一列不會變成一堵圓點的牆。

<Demo name="LongCarousels" />

### 照片

投影片可以在繪製的場景之上顯示 `ArcImage` 照片，因此離線時看起來仍然完整。

<Demo name="Photos" />

### 範例：商店精選系列

商店首頁上一排四個季節性系列。每張投影片有一個代替照片的色塊、一個標題，以及一行包含商品數量與起始價格的文字。自動播放關閉，由檢視者決定何時切換，兩側的相鄰項目提示還有更多系列。文案簡短，只陳述購物者會比較的事實：有多少件商品、起價多少。輪播寬度與頁面內容寬度一致，因此投影片尺寸為 340 px 的上限，相鄰項目填滿兩側的邊界。

![A featured collections carousel with the middle slide active, its neighbors peeking in from both sides](images/FeaturedCollections.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="Featured collections"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Carousel Label="Featured collections" SelectedIndex="1">
        <Border Height="236"
                BorderThickness="1"
                ClipToBounds="True"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
            <StackPanel>
                <Border Height="150" Background="#2F4858" />
                <StackPanel Margin="18,14,18,18" Spacing="4">
                    <TextBlock Text="Spring linen"
                               FontSize="16"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="24 pieces · from $180"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </StackPanel>
        </Border>
        <Border Height="236"
                BorderThickness="1"
                ClipToBounds="True"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
            <StackPanel>
                <Border Height="150" Background="#D9D2C5" />
                <StackPanel Margin="18,14,18,18" Spacing="4">
                    <TextBlock Text="Trail essentials"
                               FontSize="16"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="38 pieces · from $95"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </StackPanel>
        </Border>
        <Border Height="236"
                BorderThickness="1"
                ClipToBounds="True"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
            <StackPanel>
                <Border Height="150" Background="#8AA399" />
                <StackPanel Margin="18,14,18,18" Spacing="4">
                    <TextBlock Text="Harbor kitchen"
                               FontSize="16"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="17 pieces · from $60"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </StackPanel>
        </Border>
    </arc:Carousel>
</StackPanel>
```

### 文案規範

- **標籤**：為這組投影片命名，例如「精選系列」或「港灣套房精選」。標籤是輪播對輔助技術的唯一名稱，因此避免使用「輪播」或「投影片」。
- **投影片標題**：一個簡短的名詞片語，用來命名項目。在投影片寬度內保持一行。
- **投影片細節**：一個數量加上讀者會比較的一個事實，例如「24 件 · 起價 $180」。數字使用阿拉伯數字，貨幣符號與金額放在一起。
- **投影片名稱**：當位置很重要時，使用 `SlideLabelFormatter` 為投影片命名，例如「檔案照片 3 / 共 120」，而不是只說位置。

| 不要寫 | 改為 |
|---|---|
| 輪播 | 精選系列 |
| 點這裡看更多！ | 徒步必備，38 件，起價 $95 |
| 投影片 1 | 春季亞麻 |
| 自動捲動的優惠 | 季節性優惠，每 6 秒輪替一次 |

### 最佳做法

**建議**

- 為每個輪播設定一個說明投影片內容的 `Label`。它是螢幕閱讀器最先播報的內容。
- 讓作用中投影片的文案自成一體，使從連結進入的檢視者能立即理解。
- 除非投影片只是裝飾，否則保持 `IsAutoPlay` 關閉。沒有請求動態效果的讀者不應失去正在閱讀的投影片。
- 使用 `SelectedIndex` 讓頁面從需要的投影片開始，例如最新版本，而不是總是從第一張開始。

**避免**

- 對包含警告、價格或截止日期的內容使用自動播放。輪替會在被讀完之前把它移出視野。
- 投影片類型不一致，例如橫幅與商品卡片並排。相鄰項目會變成雜訊，而不像是同類內容的更多項目。
- 把頁面唯一的導覽放在輪播裡。鍵盤與螢幕閱讀器使用者仍能存取它，但連結清單更易於掃讀與記憶。
- 過短的間隔。幾秒鐘內就移動的投影片不足以讓文案被讀完。
- 每次資料重新整理都取代 `ItemsSource`。位置會被限制，且項目變動時軌道可能跳動；資料變動時應就地更新集合。

### 常見問題

**為什麼輪播在第一張與最後一張處停止，而不是循環？** 手動導覽不會循環：`Previous` 與 `Next` 在兩端停止，對應的按鈕會被軟停用。只有自動播放會循環，並在最後一張之後回到第一張。

**為什麼沒有出現播放按鈕？** 只有當 `Interval` 為正的時間長度且動態效果為完整模式時，按鈕才會出現。在減少動態效果模式下輪替不可用，因此按鈕保持隱藏，`CanRotate` 為 `False`。

**即使設定了 `IsAutoPlay`，為什麼自動播放仍未開始？** 輪替需要間隔、至少兩張投影片以及完整動態效果。當指標位於輪播上方、鍵盤焦點位於其中、拖曳期間、輪播捲出可視範圍以及視窗最小化時，它也會暫停。`IsRotating` 可告訴你輪替是否正在執行。

**如何從某一張特定的投影片開始？** 設定 `SelectedIndex`，例如在 XAML 中寫 `SelectedIndex="2"`，或在輪播載入後呼叫 `GoTo(2)`。`SelectedIndex` 是雙向的，因此檢視者停留的值可以被讀回。

**為什麼觸控板滑動會自行停下？** 水平方向的觸控板滑動會讓軌道與手指一對一地移動。在 120 毫秒內沒有滾輪事件後，輪播會停在最近的投影片上，因此半途的滑動不會讓軌道停在兩張投影片之間。

## API 參考

<!-- api: Carousel -->

## 無障礙

- `Label` 為輪播命名。選取變更時，投影片會透過 `SlideLabelFormatter` 以「2 / 5」之類的形式播報。
- 左右方向鍵每次移動一張投影片。Home 與 End 分別跳到第一張與最後一張。
- Tab 依序切換投影片、播放控制項、指示器以及上一張、下一張按鈕。
- 非作用中的投影片會從焦點順序與自動化樹中移除。
- 自動播放會在指標位於輪播上方、鍵盤焦點位於其中、正在拖曳、捲出可視範圍以及視窗最小化時暫停。自動播放不會播報每一張投影片。
- 指示器是一個名為「選擇投影片」的標籤清單，每個圓點是一個標籤，其名稱與它所選的投影片相同，例如「2 / 4」。
- 播放控制項名為「開始投影片輪替」或「暫停投影片輪替」，上一張與下一張按鈕則分別名為「上一張投影片」與「下一張投影片」。
- 由檢視者或按鍵引起的選取變更會以禮貌方式播報。輪替不做播報。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Left` / `Right` | 後退或前進一張投影片；當某個指示器標籤取得焦點時，焦點移到新的指示器標籤 |
| `Home` / `End` | 跳到第一張或最後一張投影片 |
| `Tab` / `Shift+Tab` | 在檢視區、播放控制項、指示器以及上一張、下一張按鈕之間移動 |
| `Enter` 或 `Space` | 按下已取得焦點的按鈕：播放或暫停、上一張或下一張 |

輪播不處理帶有 `Ctrl`、`Alt` 或 `Cmd` 的方向鍵，在文字方塊中輸入的方向鍵則留在文字方塊內。

## 動態效果

- 拖曳時投影片跟隨指標移動，並帶著慣性吸附到最近的投影片。鍵盤或按鈕操作會沿移動方向切換投影片。
- 相鄰項目會隨與作用中投影片的距離而縮小並變暗。
- 指示器會延展成跟隨作用中投影片的膠囊；自動播放等待期間，膠囊會逐漸耗盡。
- 減少動態效果時直接跳到目標投影片，並停止自動播放。關閉動態效果時立即到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 拖曳放開 | 慣性帶動軌道吸附到最近的投影片；快速輕掃至少移動一張投影片 | 直接跳到最近的投影片 | 立即 |
| 鍵盤或按鈕移動 | 以平滑彈簧滑向新投影片 | 直接跳到新投影片 | 立即 |
| 相鄰項目的縮放與不透明度 | 每一影格跟隨軌道位置 | 隨軌道一起跳動 | 立即 |
| 指示器膠囊 | 延展並跟隨作用中投影片 | 直接跳到作用中的圓點 | 立即 |
| 自動播放 | 依間隔前進，並使膠囊耗盡 | 不可用 | 不可用 |

## 相關元件

- 每個項目都需要做決定而不只是瀏覽時，用 `CardStack`。
- 同一張圖片的前後對比，用 `ImageCompare`。
- 在不相似的面板之間切換時，用 `Tabs`。

| 元件 | 適用情境 |
|---|---|
| `CardStack` | 列中的每個項目都需要保留或略過的決定 |
| `ImageCompare` | 用可拖曳的分隔線比較同一張圖片的兩個版本 |
| `Tabs` | 這一列在內容不同的區段之間切換 |
| `Accordion` / `ExpandableText` | 讀者必須按順序完整閱讀內容 |
| `Alert` / `AnnouncementBar` | 訊息必須保持可見直到使用者閱讀 |
| `Table` | 讀者掃讀、排序或比較大量記錄 |
