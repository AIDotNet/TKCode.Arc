---
title: 公告列
summary: 頂部橫幅，輪播訊息、倒數計時，關閉時平滑收合。
source-hash: dc63d15d468c
---

<Demo name="RotatingMessages" />

## 何時使用

`AnnouncementBar` 是頁面頂端的細長橫幅，面向每一位訪客，而不是某個區域。它容納兩到四則簡短訊息並依序輪播，每則訊息可附帶一個動作按鈕與即時倒數計時。關閉後，下方頁面會向上移動。它適合放置對整個網站或應用程式都適用、且使用者一眼就能讀完的訊息。

- **實體店面或 SaaS 定價網站的季節性促銷**：「春季促銷：年繳方案七折」，附帶 `Upgrade` 動作與優惠截止的倒數計時，讓截止時間在每個頁面上都看得見。
- **管理後台的發佈訊息**：「Arc 2.0 已發佈，新增 40 個元件」與「週四 17:00（UTC）線上示範」輪播顯示，每則都有各自的連結，一列橫幅即可承載一次發佈的消息。
- **狀態或維運儀表板上的排定維護**：使用反色橫幅，附帶「開始於」倒數計時，直到維護時段開始前都保持可見。它是頁面上最重要的通知，因此使用最強的語氣。
- **希望回訪客戶只看一次的活動**：帶 `DismissalKey` 的橫幅，在下次造訪時保持關閉，當新活動更換了鍵值後再次顯示。

## 何時不用

- **關於某個區域或表單的訊息**，例如帳單頁面上即將到期的信用卡：用 `Alert`，它位於頁面的內容流中，緊鄰相關內容。
- **訪客剛剛操作後的回饋**，例如「變更已儲存」：用 `Toast` 或 `ToastStack`。
- **Cookie 或追蹤的同意選擇**：用 `CookieConsent`，它會提出問題並記錄答案。
- **使用者繼續前必須做出的決定**：用 `Dialog`。
- **以倒數計時本身為主題之頁面上的大型倒數計時**，例如發佈頁或結帳計時器：用 `Countdown`，它把時間作為頁面的主要內容顯示，而不是橫幅中的一行。

## 用法

將公告加入 `Messages`。公告列先顯示第一則，之後每隔 `Interval`（預設為 6 秒）輪播其餘訊息。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnnouncementBar>
        <arc:AnnouncementBar.Messages>
            <arc:Announcement Id="launch"
                              Message="Arc 2.0 已發佈，新增 40 個元件。" />
            <arc:Announcement Id="webinar"
                              Message="週四 17:00（UTC）進行線上示範。" />
            <arc:Announcement Id="status"
                              Message="所有系統運作正常。" />
        </arc:AnnouncementBar.Messages>
    </arc:AnnouncementBar>
</UserControl>
```

`AnnouncementBar` 衍生自 Avalonia 的 `TemplatedControl`。它的訊息是 `Announcement` 物件，由公告列自行繪製。它的寬度由容器決定，因此請把它放在視窗或頁面版面的第一列，使其橫跨整個寬度。每個 `Announcement` 的 `Id` 用於在事件與你的分析資料中識別它，所以請在各個版本之間保持 `Id` 穩定。

### 結構

1. **動作**（`Announcement.Action`）：動作按鈕的文字，後面接一個箭頭。它以中等字重繼承公告列的前景色，懸停時箭頭會朝目的地移動 2 px。
2. **訊息**（`Message`）：訊息文字，使用公告列的字級（`TypeSM`）與前景色，行高 19.6 px。訊息改變時，文字會在至少 32 px 高的視窗內升起就位。
3. **倒數計時**（`Announcement.Countdown`）：剩餘時間以中等粗細的數字顯示，前面可選擇性地加上 `Label`，兩者之間相距 6 px。冒號與天數標記使用弱化色，倒數計時會以單一控制項繪製。
4. **鏡像欄**：左側一個與右側控制項等寬的空欄（每個控制項 32 px，控制項之間相距 2 px）。它讓訊息在頁面上保持置中，在窄列上會被移除。
5. **控制項**（`IsNavigationVisible`、`IsDismissible`）：當導覽可見且訊息多於一則時，依序為上一則（向上箭頭）、暫停環與下一則（向下箭頭）；當 `IsDismissible` 為 `True` 時，還有關閉按鈕（X）。每個都是 32 px 的圓形幽靈按鈕，使用弱化色。懸停時以 10% 填滿，按下時以 12% 填滿。
6. **公告列本體**：弱化表面，底部有 1 px 框線。內距為左 16 px、右 8 px、上下各 6 px。在寬 560 px 或更窄的列上，左側內距為 14 px，欄距為 8 px（而非 12 px），並移除鏡像欄，使訊息從左邊緣開始。

### 語氣

`Tone` 決定公告列的外觀。`Neutral`（預設）是帶底部框線的弱化表面，適合幾乎所有頁面。`Inverted` 會以前景色填滿公告列，並以背景色設定文字。只用於不能錯過的那一則通知，且同一頁面上不要超過一列這樣的公告列。

<Demo name="Inverted" />

在反色語氣中，次要文字使用 66% 的背景色，懸停填滿使用 10% 的背景色，因此控制項在填滿的公告列上仍然清楚可讀。

### 控制與自動輪播

設定 `IsNavigationVisible` 即可顯示上一則與下一則按鈕以及暫停環。`IsAutoPlayEnabled` 可關閉輪播。`Next()` 與 `Previous()` 可在程式碼中切換訊息，且兩端都會循環。

<Demo name="ControlsAndCountdown" />

```xml
<arc:AnnouncementBar IsNavigationVisible="True"
                     Interval="0:0:8"
                     IsAutoPlayEnabled="True" />
```

只有同時滿足三個條件時才會輪播：訊息多於一則、`IsAutoPlayEnabled` 為 `True`，且公告列的動態效果等級為完整。在減少或關閉動態效果時，公告列會停留在第一則訊息上，因此要為使用者提供閱讀其他訊息的方式：`IsNavigationVisible` 提供的上一則與下一則按鈕，或你自己呼叫 `Next()` 與 `Previous()` 的按鈕。暫停環會在每個 `Interval` 內逐漸填滿。按下它會設定 `IsPaused` 並讓計時停在當前位置；再按一次則恢復，並完成該間隔剩餘的部分。

`SelectedIndex` 是雙向的，會回報目前可見的訊息。使用 `SelectedIndexChanged` 記錄顯示了哪則訊息，例如用來統計訪客看到了哪則公告。

### 動作與倒數計時

`Announcement` 可以有一個帶 `Label` 與 `Href` 的 `Action` 按鈕，以及一個倒數至 `To` 的 `Countdown`。當動作需要執行程式碼時，請在動作上使用 `Command` 取代 `Href`。若動作同時設定了兩者，會先執行命令，然後才開啟連結。無論哪種方式，之後都會引發 `ActionInvoked`，並附上訊息及其索引。

```xml
<arc:AnnouncementBar>
    <arc:AnnouncementBar.Messages>
        <arc:Announcement Id="sale"
                          Message="春季促銷：年繳方案七折。">
            <arc:Announcement.Action>
                <arc:AnnouncementAction Label="升級" Href="https://example.com/upgrade" />
            </arc:Announcement.Action>
            <arc:Announcement.Countdown>
                <arc:AnnouncementCountdown To="2026-11-01T00:00:00+00:00" Label="距結束" />
            </arc:Announcement.Countdown>
        </arc:Announcement>
    </arc:AnnouncementBar.Messages>
</arc:AnnouncementBar>
```

```csharp
public static class Analytics
{
    public static void Track(string name, string id) { }
}

bar.ActionInvoked += (_, e) => Analytics.Track("announcement.action", e.Announcement.Id);
bar.CountdownEnded += (_, e) => bar.Messages.Remove(e.Announcement);
```

倒數計時在剩餘整天或更多時顯示天數，之後顯示小時、分鐘與秒，數字以整秒變化。歸零後會停止並顯示零。`CountdownEnded` 在倒數計時到達零時引發一次；若訊息是在結束時間已過之後才顯示的，也會引發。請像上面那樣在該處理常式中移除訊息，讓公告列不會保留已過期的優惠。

### 繫結到檢視模型

公告列是帶有狀態的資料清單，因此請繫結到檢視模型擁有的集合。訊息使用 `ItemsSource`，設定後會覆寫 `Messages`。只有當檢視模型需要知道使用者關閉了公告列或切換了訊息時，才對 `IsOpen` 與 `SelectedIndex` 使用雙向繫結。

```csharp
public partial class StorefrontViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Announcement> _announcements = new()
    {
        new Announcement { Id = "sale", Message = "春季促銷：年繳方案七折。" },
        new Announcement { Id = "webinar", Message = "週四 17:00（UTC）線上示範。" }
    };

    [ObservableProperty]
    private bool _isBannerOpen = true;

    [ObservableProperty]
    private int _bannerIndex;
}
```

```xml
<arc:AnnouncementBar ItemsSource="{Binding Announcements}"
                     IsOpen="{Binding IsBannerOpen, Mode=TwoWay}"
                     SelectedIndex="{Binding BannerIndex, Mode=TwoWay}"
                     IsNavigationVisible="True" />
```

集合改變時，公告列會保留選取的索引（依新的數量取模）並重新開始輪播計時器。因此取代整個集合是安全的，新增或移除訊息時也不需要另外重新整理。

### 記住關閉狀態

設定 `DismissalKey` 可在多次執行之間記住關閉狀態。此鍵值儲存在 `Storage` 中。呼叫 `AnnouncementBar.ClearDismissal` 清除已儲存的值，即可再次顯示公告列。關閉狀態由 `Storage` 儲存，預設為應用程式資料資料夾中的一個檔案。

<Demo name="RememberedDismissal" />

```xml
<arc:AnnouncementBar DismissalKey="spring-campaign" IsDismissible="True" />
```

```csharp
AnnouncementBar.ClearDismissal("spring-campaign");
bar.IsOpen = true;
```

鍵值應標明活動而非頁面，並在新活動開始時更換，使每位訪客都能看到新訊息一次。關閉時會立即把鍵值寫入儲存空間，`Closed` 也會立即引發，早於公告列完成收合動畫。

### 範例：SaaS 網站上的春季活動

Northwind Analytics 的行銷頁面在定價表上方顯示春季優惠。公告列承載優惠內容及其截止的倒數計時、升級頁面的連結，以及上一則與下一則控制項，讓訪客不必等待即可閱讀其他通知。自動輪播已關閉，因為優惠才是要閱讀的訊息，而不是投影片。下方的頁面內容代表定價表，因此公告列收合時，它會像線上網站上一樣向上移動。

![位於定價表上方、帶有倒數計時、升級連結與導覽的春季促銷橫幅](images/SaleBanner.png)

```xml
<Border Width="720"
        CornerRadius="12"
        BorderThickness="1"
        ClipToBounds="True"
        Background="{arc:ArcDesignTokenResource Background}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
    <StackPanel>
        <arc:AnnouncementBar IsNavigationVisible="True"
                             IsAutoPlayEnabled="False">
            <arc:AnnouncementBar.Messages>
                <arc:Announcement Id="sale" Message="Spring sale: 30% off annual plans.">
                    <arc:Announcement.Countdown>
                        <arc:AnnouncementCountdown To="2026-11-01T00:00:00+00:00" Label="Ends in" />
                    </arc:Announcement.Countdown>
                    <arc:Announcement.Action>
                        <arc:AnnouncementAction Label="Upgrade" />
                    </arc:Announcement.Action>
                </arc:Announcement>
                <arc:Announcement Id="annual"
                                  Message="Annual plans include the Pro reports and 10 seats." />
                <arc:Announcement Id="status" Message="All systems operational." />
            </arc:AnnouncementBar.Messages>
        </arc:AnnouncementBar>
        <StackPanel Margin="24,20" Spacing="6">
            <TextBlock Text="Pricing" FontSize="22" FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Pro is $24 per seat per month, billed annually."
                       FontSize="14"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
    </StackPanel>
</Border>
```

### 文案規範

- **訊息**：一句話，陳述消息，並在需要時給出日期或數字：「春季促銷：年繳方案七折。」若要在窄視窗中維持一行顯示，請控制在約 70 個字元以內。
- **動作文字**：採用句子式大小寫的動詞加受詞：「查看新功能」「保留座位」。避免使用「按這裡」。
- **倒數計時標籤**：時間前的簡短片語：「距結束」「距開始」。標籤會在時間之前被朗讀。
- **語氣**：直接、客觀。給出日期或截止時間，而不是急迫性的字眼，並避免使用驚嘆號。

| 不要寫 | 改為 |
|---|---|
| 超級大特價！！！ | 春季促銷：年繳方案七折。 |
| 按這裡 | 保留座位 |
| 新！了解更多 | Arc 2.0 已發佈，新增 40 個元件。 |
| 即將維護 | 今晚 22:00（UTC）排定維護。 |

### 最佳做法

**建議**

- 一列公告保留兩到四則訊息。每則訊息都應能在一個間隔（約 6 秒）內讀完。
- 把最重要的訊息放在最前面，因為公告列載入時顯示的是第一則。
- 為每則訊息設定穩定的 `Id`。事件會回報它，因此分析可以跨輪播與版本追蹤同一則訊息。
- 倒數計時結束時，從 `Messages` 移除該訊息，讓公告列不再保留已過期的優惠。

**避免**

- 長訊息。換行成多行的訊息會把頁面往下推，公告列也就不再像橫幅。
- 同一頁面上有多列反色公告列。兩個強烈的橫幅彼此競爭，沒有人能分辨哪個才是重要的。
- 把公告列用於頁面的某一個區域。公告列面向每位訪客，因此關於某個表單的通知應放在 `Alert` 中。
- 只仰賴輪播。在減少動態效果時，公告列會停在目前的訊息上，因此應提供導覽控制項或指向完整清單的連結。

### 常見問題

**訪客關閉公告列後，為什麼 `IsOpen` 仍為 `True`？** 記住的關閉狀態會在不改變 `IsOpen` 的情況下隱藏公告列，因此公告列能區分本次工作階段中的關閉與已記住的關閉。把 `IsOpen` 設為 `True` 並不會讓它重新出現。請使用該鍵呼叫 `AnnouncementBar.ClearDismissal`，然後再次設定 `DismissalKey`（例如先設為 `null` 再設回原值），讓公告列重新讀取儲存空間。

**為什麼我的應用程式中公告列不輪播？** 輪播需要多於一則訊息、`IsAutoPlayEnabled` 設為 `True`，並且動態效果為完整。在減少或關閉動態效果時，公告列會依設計停留在目前的訊息上。若仍需輪播，請新增導覽控制項，或從你自己的計時器呼叫 `Next()`。

**關閉狀態存放在哪裡？** 若你在公告列上設定了 `Storage`，就存放在其中。否則會存放在 `MessageStorage.Default`，它會在本機應用程式資料資料夾中寫入一個小檔案。請在啟動時設定一次 `MessageStorage.Default`，將其保存到你的設定服務中，或在測試中改用記憶體儲存。

**如何讀取使用者正在看的訊息？** 使用 `Current`，它是目前可見的 `Announcement`；使用 `Count` 取得訊息數量。`SelectedIndex` 給出位置，從零開始。

## API 參考

<!-- api: AnnouncementBar -->

<!-- api: Announcement -->

`Announcement` 所使用的配套型別：

| 型別 | 屬性 |
|---|---|
| `AnnouncementAction` | `Label`（文字）、`Href`（`Uri`，透過系統啟動器開啟）、`Command`（`ICommand`，在呼叫時執行）、`CommandParameter` |
| `AnnouncementCountdown` | `To`（`DateTimeOffset`，倒數計時歸零的時間）、`Label`（時間前的文字，選填） |

## 無障礙

- 公告列是一個區域，由 `Label` 命名，預設為 `Announcements`。
- 每則訊息都有名稱，例如 `1 of 3`，讓螢幕閱讀器知道它在輪播中的位置。
- 輪播切換時不會逐則播報。公告列暫停時才會播報，方便使用者閱讀。
- Tab 依序聚焦動作、上一則按鈕、暫停環、下一則按鈕與關閉按鈕。Enter 與空白鍵會啟動目前聚焦的控制項。
- 暫停環是切換按鈕，其名稱會在 `Pause announcements` 與 `Resume announcements` 之間變換。
- 指標位於公告列上方、焦點位於其內部，或視窗最小化時，輪播會暫停。只有滑鼠指標會使其暫停，觸控螢幕不會，因此要為觸控使用者提供暫停環。
- 倒數計時滴答時不會被播報。其無障礙名稱會完整說出剩餘的天數、小時與分鐘。
- 上一則、下一則與關閉按鈕的名稱分別為 `Previous announcement`、`Next announcement` 與 `Dismiss`。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 依序經過動作、上一則、暫停環、下一則與關閉控制項。隱藏的控制項會被略過 |
| `Enter` / `Space` | 執行聚焦的動作、按下聚焦的按鈕，或切換暫停環 |

公告列本身不接收焦點，因此 `Tab` 會直接進入其控制項。公告列內的焦點會暫停輪播，讓鍵盤使用者可以閱讀一則訊息，而不會在閱讀時它自行改變。

## 動態效果

- 下一則訊息會帶著輕微模糊從下方升起，同時公告列以彈簧調整至適合其高度。
- 暫停環會在每個間隔內填滿。暫停時停在原處，恢復後繼續。
- 倒數計時的數字變化時以下落方式出現。
- 關閉時公告列收合，下方頁面隨之上移。
- 減少動態效果時，輪播停止，訊息與數字改為淡入淡出。關閉時僅淡出。
- 關閉動態效果時，每次變化都會立即顯示。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 自動輪播 | 每隔 `Interval` 前進；下一則訊息從下方升起 | 停止；公告列停留在目前的訊息上 | 停止 |
| 手動切換（`Next`、`Previous`） | 訊息帶模糊升起或落下，高度以彈簧調整至適合的尺寸 | 訊息淡入淡出 | 立即完成 |
| 暫停環 | 輪播時可見；在間隔內填滿，暫停時停止 | 隱藏 | 隱藏 |
| 倒數計時數字變化 | 新數字下落出現，舊數字落下消失 | 數字淡入淡出 | 立即完成 |
| 關閉 | 高度以平滑彈簧收合，下方頁面上移 | 公告列淡出而不收合 | 立即完成 |

## 相關元件

- `Alert`：位於某個區域或表單內，直到問題解決前一直保留的訊息。
- `ToastStack`：使用者剛執行之動作的排隊結果。
- `CookieConsent`：Cookie 與追蹤的同意。

| 元件 | 適用情境 |
|---|---|
| `Alert` | 通知屬於某個區域或表單，並會保留到問題解決 |
| `Toast` | 已完成的動作需要一則會自行關閉的簡短確認 |
| `ToastStack` | 應用程式的結果、錯誤或復原動作需要在視窗邊緣堆疊 |
| `CookieConsent` | 通知請訪客就 Cookie 或追蹤做出選擇 |
| `Countdown` | 倒數計時是頁面的主要內容，而不是橫幅中的一行 |
| `Dialog` | 使用者必須先回應，頁面才能繼續 |
