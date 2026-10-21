---
title: 懸停卡片
summary: 懸停或聚焦時預覽人員或連結，無需離開頁面。
source-hash: 309b3a59cc84
---

<Demo name="Mention" />

## 何時使用

`HoverCard` 在文字中的某個名字被指向或取得焦點時，顯示該人員、記錄或連結的簡短唯讀預覽。預覽的主體留在頁面中，緊挨著提及它的文字，因此讀者可以確認提及的是誰，而不會在討論串、表格或清單中失去位置。

- **留言或事件討論串中的提及。** 事後檢討中的 `@maya` 會展開她的角色、團隊與當地時間，值班工程師在 @ 任何人之前就能知道該問誰。
- **表格中的負責人或作者名稱。** 帶負責人欄的部署清單可以在每個名稱上展開一張小型個人資料卡，而不必為了快速確認而跳轉到另一個使用者頁面。
- **指向內部記錄的連結。** 指向提取要求、訂單或支援工單的連結，可以在懸停時預覽標題與一行背景資訊，點選仍會跳轉到該記錄。
- **密集的人員名單。** 審查人或負責人清單中，讀者會從一個名稱移到下一個。一張卡片開啟後，下一張幾乎立即開啟，因此瀏覽名單仍然很快（參見密集清單）。

## 何時不用

- **圖示或被截斷單字的一行標籤：** 用 `Tooltip`。懸停卡片用於呈現關於某個對象的多項資訊；標籤只是一個片語，工具提示較輕量，也不需要預覽版面。
- **按鈕、輸入框或任何操作：** 用 `Popover`。焦點不會進入懸停卡片，因此其中的按鈕無法透過 Tab 到達，欄位也無法輸入。
- **登入使用者自己的帳戶選單：** 用 `UserMenu`，它列出預覽卡片不提供的帳戶操作（個人資料、登出）。
- **讀者在繼續之前必須看到的決定或警告：** 用 `Dialog`。指標離開或在卡片外按下時，懸停卡片就會關閉，因此它無法承載讀者必須確認的訊息。
- **完整個人資料、動態摘要或長篇簡介：** 連結到該記錄的頁面。卡片預設寬 296 px，過長的預覽會遮住下方的內容。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoverCard Placement="Bottom">
        <arc:HoverCard.Trigger>
            <arc:Button Variant="Ghost" Content="@maya" />
        </arc:HoverCard.Trigger>
        <arc:HoverCardProfile DisplayName="Maya Chen"
                              Role="支付產品設計師"
                              Bio="負責結帳與帳單流程的設計。"
                              Meta="里斯本 · 當地時間 14:20">
            <arc:HoverCardProfile.Stats>
                <arc:HoverCardStat Label="專案" Value="12" />
                <arc:HoverCardStat Label="審查" Value="48" />
            </arc:HoverCardProfile.Stats>
        </arc:HoverCardProfile>
    </arc:HoverCard>
</UserControl>
```

`HoverCard` 衍生自 Avalonia 的 `ContentControl`，因此它的 `Content` 就是預覽內容，`ContentTemplate` 套用於該內容。`Trigger` 是留在頁面中、可取得焦點的控制項。在頁面中，卡片只顯示觸發器；其內容在一個錨定的 `PopoverSurface` 中開啟，這與 `Popover` 使用的表面相同，並承載於浮層中。浮層不參與版面配置，因此卡片永遠不會推動周圍的文字。`HoverCardProfile` 衍生自 `TemplatedControl`，提供標準的人員版面（頭像、名稱、角色、簡介、統計與頁尾），使應用程式中的每張卡片外觀一致。

### 結構

1. **觸發器**（`Trigger`）：頁面中可取得焦點的控制項，例如帶有帳號代號的 Ghost `Button` 或連結。它是版面配置中唯一佔據空間的部分。
2. **卡片表面**：一個 `PopoverSurface`，寬度由 `CardWidth` 決定（預設 296），內距 16 px，使用浮起表面色、1 px 的框線色邊框與面板圓角。它不可取得焦點，浮動陰影由浮層繪製。
3. **頭像**（`Avatar`）：一個大型 `Avatar`，由 `AvatarSource` 提供影像。沒有肖像時顯示 `DisplayName` 的首字母。
4. **名稱**（`DisplayName`）：基礎字級、中等字重、主要文字顏色。只佔一行，過長時以省略號結尾。
5. **角色**（`Role`）：次要文字顏色的小號文字，只佔一行，過長時以省略號結尾。字串為空時隱藏。
6. **簡介**（`Bio`）：次要文字顏色的小號段落。會換行；為空時隱藏。
7. **統計**（`Stats`）：位於 1 px 分隔線下方的一行，分隔線使用淡框線色，分隔線上方留 12 px。每項以基礎字級、中等字重顯示數值，數值下方是超小號淡化色的標籤。各欄間距 24 px，列間距 8 px，該行會換行。`Stats` 為空時隱藏。
8. **中繼資訊**（`Meta`）：超小號淡化色的頁尾，通常是地點與當地時間。值為 null 時隱藏。

個人資料各列之間相距 12 px，頭像與文字欄之間相距 12 px。卡片與觸發器相距 8 px（`Offset`）。

### 個人資料卡片

上面的個人資料是預覽的常見形式：名稱與角色用來辨識此人，簡介說明此人在做什麼，統計給出兩個有助於讀者判斷的數字，中繼資訊則為身處其他時區的人提供地點與當地時間。統計項保持兩到三個。卡片寬度固定，因此每項統計都與相鄰項並排佔據一欄，統計項過多會讓卡片比讀者預期的更高。

### 連結預覽

`arc:HoverCard.Attach` 可以為任意控制項加上卡片，例如一般的文字連結，而無需 `Trigger`。附加的卡片錨定在它所附著的控制項上。卡片開啟時會加入該控制項的邏輯樹，因此樣式與目標的 `DataContext` 都能傳遞到卡片中。

<Demo name="LinkPreview" />

此處卡片的 `Content` 是字串。字串會在卡片的預設文字範本中以換行文字顯示，足以容納連結的標題與一行摘要。當對象是人員時，請使用 `HoverCardProfile`。

### 時間與寬度

`OpenDelay` 預設為 500 ms，`CloseDelay` 預設為 140 ms。開啟延遲可以防止指標在前往別處的途中經過某個名稱時，每次都彈出卡片。關閉延遲是一段寬限期：它給指標留出穿過 8 px 間隙進入卡片的時間，而進入卡片會取消關閉，讀者因此可以捲動簡介或選取文字。`CardWidth` 設定卡片的寬度，預設為 296（18.5 rem）。卡片同樣受視窗可用空間的限制，當首選的一側放不下時會翻轉到另一側。

```csharp
card.OpenDelay = TimeSpan.FromMilliseconds(300);
card.CardWidth = 320;
```

只有當簡介長到在 296 寬度下會換行成多行時，才加寬卡片。只在部署負責人這類預覽比安靜頁面更重要的觸發器上縮短 `OpenDelay`。頁面範圍還有一種預熱狀態：任何卡片開啟期間，以及最後一張卡片關閉後的 300 ms 內，下一張卡片會在 80 ms 後僅以淡入方式開啟，因此跨越名單的讀者不必在每個名稱上都等待 500 ms。

### 從程式碼中顯示

`Show` 與 `Hide` 直接修改 `IsOpen`。`Show` 立即開啟卡片，不經過 `OpenDelay`；`Hide` 立即關閉卡片，不經過 `CloseDelay`。`IsOpen` 為雙向繫結，因此繫結與程式碼修改都能開啟卡片。`Opened` 與 `Closed` 回報狀態變化。`Closed` 還攜帶 `Reason`：讀者按下 Escape 時為 `Escape`，在卡片外按下時為 `PressOutside`，指標離開觸發器或程式碼呼叫 `Hide` 時為 `Programmatic`。引發 `Closed` 時，退出動畫仍可能在播放。

```csharp
card.Show();
card.Closed += (_, e) => log.Text = $"關閉方式：{e.Reason}";
```

`Anchor` 屬性是卡片定位所依據的控制項：卡片附加時為附加的目標，否則為 `Trigger`。`Surface` 與 `Popup` 在首次開啟後公開表面與浮層，在此之前為 null。

### 密集清單

卡片開啟後，移到下一個觸發器會幾乎立即開啟它，且只有淡入效果。審查人名單是典型場景：每個名稱都顯示各自的卡片而無需等待半秒，頁面也不會在卡片之間閃爍。

<Demo name="DenseList" />

### 繫結到檢視模型

在 MVVM 應用程式中，人員資料由檢視模型持有，懸停卡片為每一項呈現一份個人資料。只有 `IsOpen` 是雙向的；`Trigger`、`Content` 與個人資料屬性都應從資料單向繫結。

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

public sealed record MemberProfile(
    string Handle,
    string Name,
    string Role,
    string Bio,
    string Location,
    int Projects,
    int Reviews);

public partial class ReviewersViewModel : ObservableObject
{
    public ObservableCollection<MemberProfile> Reviewers { get; } = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Reviewers}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <arc:HoverCard>
                <arc:HoverCard.Trigger>
                    <arc:Button Variant="Ghost" Content="{Binding Handle}" />
                </arc:HoverCard.Trigger>
                <arc:HoverCardProfile DisplayName="{Binding Name}"
                                      Role="{Binding Role}"
                                      Bio="{Binding Bio}"
                                      Meta="{Binding Location}" />
            </arc:HoverCard>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

`Stats` 是唯讀集合，XAML 無法繫結它。請在建立個人資料時用程式碼填入，並以目前的文化特性格式化數字，使千分位分隔符遵循讀者的設定：

```csharp
var profile = new HoverCardProfile { DisplayName = member.Name, Role = member.Role };
profile.Stats.Add(new HoverCardStat("專案", member.Projects.ToString("N0", CultureInfo.CurrentCulture)));
profile.Stats.Add(new HoverCardStat("審查", member.Reviews.ToString("N0", CultureInfo.CurrentCulture)));
```

把帳號代號保留在觸發器上，名稱放在卡片中：觸發器是鍵盤使用者會到達的控制項，它需要有能辨識此人的文字。

### 焦點與關閉

懸停卡片由三種輸入開啟：指標、Tab 焦點與點選。它們的規則各不相同，這樣鍵盤使用者與滑鼠使用者都能獲得可預測的卡片行為。

- **Tab 焦點**會立即開啟卡片。由指標點選取得的焦點不會開啟卡片，開啟它的是點選本身。
- **失去焦點**會啟動關閉寬限期。卡片會在 `CloseDelay` 之後關閉，除非指標在觸發器或卡片上。
- **Escape** 關閉已開啟的卡片。之後卡片會保持關閉，直到指標或焦點離開觸發器，因此讀者仍停留在同一個名稱上時，它不會重新開啟。
- **在卡片外按下**會以 `PressOutside` 原因關閉它。
- **同一時間只有一張卡片開啟。** 開啟一張卡片會關閉先前開啟的那張。
- **從頁面移除觸發器**會立即關閉卡片，且不播放關閉動畫，因此重新整理的清單不會留下殘留的卡片。

焦點永遠不會進入卡片。卡片本身沒有可取得焦點的內容，讀者無法透過 Tab 進入，也無法在其中輸入。需要控制項的卡片應改用 `Popover`。

### 範例：管理主控台中的部署負責人

SaaS 營運主控台中的部署歷程。每位負責人都是一個帶帳號代號的 Ghost 按鈕，因此該列讀起來很快，負責人的角色與當地時間則在懸停或取得焦點時才出現。卡片只顯示兩項統計，因為讀者需要的是負責人目前的工作量，而不是歷史紀錄。中繼資訊列給出當地時間，這在值班交接跨越時區時尤為重要。

```xml
<StackPanel Width="560" Spacing="12">
    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="16">
        <TextBlock Text="Payments API 2026.10.14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="已成功" VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:HoverCard Grid.Column="2" Placement="Left">
            <arc:HoverCard.Trigger>
                <arc:Button Variant="Ghost" Content="@ari" />
            </arc:HoverCard.Trigger>
            <arc:HoverCardProfile DisplayName="Ari Okafor"
                                  Role="資深工程師，平台"
                                  Bio="負責建置流水線與發布節奏。"
                                  Meta="拉哥斯 · 當地時間 13:20">
                <arc:HoverCardProfile.Stats>
                    <arc:HoverCardStat Label="待審審查" Value="3" />
                    <arc:HoverCardStat Label="值班" Value="今天" />
                </arc:HoverCardProfile.Stats>
            </arc:HoverCardProfile>
        </arc:HoverCard>
    </Grid>
    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="16">
        <TextBlock Text="帳單 Webhook 2026.10.19" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="執行中" VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:HoverCard Grid.Column="2" Placement="Left">
            <arc:HoverCard.Trigger>
                <arc:Button Variant="Ghost" Content="@lena" />
            </arc:HoverCard.Trigger>
            <arc:HoverCardProfile DisplayName="Lena Novak"
                                  Role="研究主管"
                                  Bio="負責每季的可用性研究。"
                                  Meta="布拉格 · 當地時間 14:20">
                <arc:HoverCardProfile.Stats>
                    <arc:HoverCardStat Label="待審審查" Value="1" />
                    <arc:HoverCardStat Label="值班" Value="明天" />
                </arc:HoverCardProfile.Stats>
            </arc:HoverCardProfile>
        </arc:HoverCard>
    </Grid>
</StackPanel>
```

`Placement="Left"` 讓每張卡片不遮住狀態欄及其下方的列。

### 文案規範

- **顯示名稱**：組織使用的完整名稱，例如 "Maya Chen"。帳號代號屬於觸發器，因此卡片中不重複它。
- **角色**：職能與團隊，以逗號分隔，例如「產品設計師，支付」。使用句首大寫。不要用「員工」之類的預留文字湊數，寧可省略角色。
- **簡介**：一句話說明此人在做什麼，使用現在式。在卡片寬度下控制在一到兩行之內。
- **中繼資訊**：地點與當地時間，以中點作分隔符，例如「里斯本 · 當地時間 14:20」。
- **統計標籤**：一到兩個詞的複數名詞（「專案」「審查」）。把數字以阿拉伯數字寫入 `Value`，便於比較。

| 不要寫 | 改為 |
|---|---|
| 嗨！我是一位熱愛各種事物的設計師。 | 負責結帳與帳單流程的設計。 |
| 角色：員工 | 產品設計師，支付 |
| 你參與過的專案數量 | 專案 |
| 時間：14:20 | 里斯本 · 當地時間 14:20 |
| 名稱：@maya | Maya Chen（代號即觸發器文字） |

### 最佳做法

**建議**

- 使用可取得焦點的控制項作為觸發器，其文字要能辨識此人，例如帶帳號代號的 Ghost `Button`。鍵盤使用者透過觸發器到達卡片，而觸發器的文字是螢幕閱讀器最先朗讀的內容。
- 保持卡片唯讀。操作應放在該記錄的頁面上或觸發器上；當預覽需要按鈕時，改用 `Popover`。焦點永遠不會進入懸停卡片，因此其中的按鈕無法從鍵盤到達。
- 簡介控制在一句話，統計控制在兩到三個值。卡片寬度固定，因此在長清單中，短卡片能一眼看懂。
- 在包含很多觸發器的頁面上保留預設的 `OpenDelay`。500 ms 的等待能讓指標經過其他名稱前往別處時，卡片保持安靜。
- 讓連結預覽的文字與連結文字相同，這樣卡片開啟時朗讀的說明文字，與讀者已讀到的內容一致。

**避免**

- 在卡片中放置按鈕、欄位或連結。焦點永遠不會進入卡片，這些控制項無法從鍵盤到達，觸控使用者也無從按下它們。
- 在清單中的每個觸發器上都把 `OpenDelay` 設為零。卡片會在指標每次經過時開啟，頁面看起來充滿雜訊。
- 把卡片作為了解某人詳情的唯一途徑。螢幕閱讀器只會聽到觸發器說明文字中的摘要，而關閉時卡片並不在頁面上。
- 在 `Bio` 或 `Meta` 中放長段落。卡片會隨文字增高，並遮住其下方的列；請改為連結到完整個人資料。
- 使用不可取得焦點的 `Trigger`，例如一般的 `TextBlock`。鍵盤使用者無法聚焦它，卡片對他們永遠不會開啟。請在文字上使用 `arc:HoverCard.Attach`，或把可取得焦點的控制項作為觸發器。

### 常見問題

**為什麼懸停要等半秒，而點選會立即開啟卡片？** 在觸發器上點選滑鼠是明確的要求，因此它不經過 `OpenDelay` 就開啟卡片。等待只適用於懸停，而懸停可能在指標經過觸發器時意外發生。

**讀者能把指標移入卡片嗎？** 能。離開觸發器會開始 `CloseDelay`（140 ms）的關閉寬限期。進入卡片會取消該關閉，只要指標停留在卡片上，卡片就保持開啟。離開卡片後，它會在寬限期結束時關閉。

**觸控螢幕如何開啟卡片？** 輕觸觸發器會切換卡片：第一次輕觸立即開啟，下一次輕觸觸發器則關閉。觸控時的指標進入與離開事件不會開啟或關閉它。

**卡片開啟時螢幕閱讀器會聽到什麼？** 卡片開啟時，觸發器的說明文字會被設為摘要。對於 `HoverCardProfile`，即名稱，若有角色則其後接角色；對於字串內容，即該字串；否則為卡片自身的 `AutomationProperties.Name`。卡片關閉後會恢復先前的說明文字，因此之後讀者聽到的是觸發器的一般描述。

## API 參考

<!-- api: HoverCard -->

<!-- api: HoverCardProfile -->

<!-- api: HoverCardStat -->

## 無障礙

- 卡片開啟時，其摘要會寫入觸發器的說明文字。卡片本身沒有無障礙名稱（除非另行設定），因此觸發器的文字必須能辨識此人。
- 焦點永遠不會進入卡片。請保持其內容唯讀，並將操作放在觸發器上或 `Popover` 中。
- 卡片表面向輔助技術公開為窗格，而非按鈕或連結。
- 透過 Tab 取得焦點會立即開啟卡片，Enter 跳過開啟延遲，按 Escape 關閉。指標停留在觸發器上時，卡片保持關閉。
- 在觸控裝置上，輕觸會切換卡片。同一時間只有一張卡片開啟，因此螢幕閱讀器使用者不會同時面對兩個互相爭奪注意力的預覽。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` 移到觸發器上 | 立即開啟卡片，沒有開啟延遲 |
| `Tab` 離開觸發器 | 在 `CloseDelay`（140 ms）之後關閉卡片，除非指標在觸發器或卡片上 |
| 觸發器上的 `Enter` | 卡片關閉時立即開啟它 |
| `Escape` | 關閉卡片；直到指標或焦點離開觸發器之前，它都保持關閉 |

## 動態效果

- 卡片從觸發器邊緣生長：以平滑彈簧從縮放 0.96 變為 1，並向觸發器方向移動 4px，同時單獨淡入。
- 個人資料各列依序從下方 4px 處就位。頭像在肖像載入完成後，從柔和的模糊中淡入。
- 若另一張卡片在 300ms 之前剛關閉，則下一張卡片會在 80ms 後僅以淡入方式開啟。
- 每次開啟都會重新播放個人資料各列，因此再次開啟的卡片總是從相同的起點就位。
- 減少動態效果時移除縮放與移動，保留短暫淡入。關閉動態效果時卡片立即顯示或移除。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 卡片開啟 | 以平滑彈簧從縮放 0.96 開始並向觸發器方向移動 4px；淡入同步進行 | 僅短暫淡入 | 立即顯示 |
| 卡片關閉 | 淡出；若在退出途中重新開啟則反向 | 短暫淡入 | 立即移除 |
| 個人資料各列開啟 | 每一列上升 4px 並淡入，在前一列之後 35ms 開始，每次開啟都如此 | 直接以靜止狀態顯示各列 | 直接以靜止狀態顯示各列 |
| 最近關閉後的下一張卡片 | 80ms 後開啟，僅淡入 | 僅淡入 | 立即顯示 |

## 相關元件

- `Tooltip` 用於懸停或聚焦時的簡短文字標籤。
- `Popover` 用於包含按鈕或輸入框、點選開啟的卡片。
- `Avatar` 用於個人資料中顯示的圖片。

| 元件 | 適用情境 |
|---|---|
| `Tooltip` | 一行標籤足以說明圖示、快捷鍵或被截斷的單字 |
| `Popover` | 內容包含按鈕、輸入框或小型表單，並透過點選開啟 |
| `Avatar` / `AvatarGroup` | 需要卡片所顯示的肖像或首字母，或一組人員 |
| `Dialog` | 讀者必須閱讀或處理某些內容後才能繼續 |
| `UserMenu` | 選單屬於登入使用者自己的帳戶 |
| `DropdownMenu` | 從觸發器開啟一組命令清單 |
