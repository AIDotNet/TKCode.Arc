---
title: 大頭貼
summary: 用於人員與帳號的精簡身分標示。
source-hash: ac97c0bbaae5
---

<Demo name="Initials" />

## 何時使用

`Avatar` 以照片或首字母標示一位人員，並可顯示此人是否在線上。它放在頁首、紀錄、留言或表格列中的名稱旁邊，讓人在讀完文字之前就能認出對方。它只用於顯示：從不取得焦點，不開啟任何內容，也不會把值寫回去。

- **工單、交易或發票中的負責人與經辦人欄位**：在「指派給」旁邊放一個大頭貼，操作人員不必逐一讀取姓名，就能在密集的佇列中找到負責人。
- **團隊通訊錄或支援主控台中的上線狀態**：照片旁的狀態圓點表示某位客服或同事在線上，`AvatarStatus.Offline` 則表示誰已下線。只在產品確實掌握上線狀態的地方使用。
- **管理後台標頭中的目前登入使用者**：在 SaaS 主控台的頂列使用 `Medium` 或 `Large` 尺寸的大頭貼，並顯示使用者的首字母，即使還沒有個人照片。
- **缺少照片的聯絡人清單**：CRM 清單中有許多紀錄沒有圖片。當 `Source` 為空或載入失敗時，由首字母接替顯示，因此每一列都有穩定的標示。
- **每則紀錄都有執行者的活動記錄與稽核記錄**：`Small` 尺寸的大頭貼讓數百列中的執行者一目了然，又不佔用表格欄位的空間。

## 何時不用

- **小空間內顯示多人並附上其餘人數**：用 `AvatarGroup`，它會把溢出的人摺疊成 "+N" 標籤。
- **點擊後開啟帳號操作（例如登出或切換工作區）的大頭貼**：用 `UserMenu`，由它負責選單、焦點與鍵盤模型。
- **需要比較姓名、角色與日期的清單**：用 `SortableDataTable`。單靠大頭貼無法承載讀者需要的文字資訊。
- **每則訊息都有作者與時間的對話**：用 `ChatThread` 或 `CommentThread`，它們會把作者與訊息文字並排排版。
- **不是人員的狀態或類別**，例如進行中、草稿或失敗：用 `Badge`。
- **編輯人員的姓名或電子郵件**：用 `Input`。大頭貼只顯示值，沒有可編輯的文字。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Avatar DisplayName="Ada Lovelace" />
</UserControl>
```

`Avatar` 衍生自 Avalonia 的 `TemplatedControl`。它的外框使用標準的 `Background`、`BorderBrush`、`BorderThickness` 和 `CornerRadius` 屬性，因此可以像其他樣板控制項一樣在區域內覆寫主題的值。尺寸由 `Size` 決定：在實例上設定 `Width` 或 `Height` 會取代主題中的直徑，此時狀態圓點與首字母將不再與 Arc 的尺寸比例相符。請優先使用 `Size`。

### 結構

1. **外框**（控制項本身）：一個圓形，帶 1 px 的 `Border` 外框線，填色為 `SurfaceMuted`。圓角為膠囊圓角，因此在任何尺寸下都保持圓形，內容也會被裁切到這個形狀內。
2. **首字母**（`Initials`，唯讀）：置中的文字，使用一般前景色與中等字重。在 `Small` 尺寸下文字為 10 px；在 `Medium`、`Large` 與 `XLarge` 下，它跟隨 Arc 字級階梯中的 XS、SM 與 XL。
3. **照片**（`Source`）：填滿圓形的非同步影像。只有在設定了 `Source` 且尚未載入失敗時才會顯示；載入期間外框保持為空。
4. **狀態圓點**（`Status`）：位於右下角的實心圓，帶有與表面色相同的環（`Small` 為 1.5 px，其他尺寸為 2 px），外圍還有一層邊框色的淡色光暈（1 px）。`Online` 使用成功色，`Offline` 使用淡化的文字顏色，`None` 則完全移除圓點。

### 照片與回退

`Source` 用於載入照片。照片會裁切以填滿圓形。已快取的照片會立即出現；如果大頭貼已顯示後照片仍在下載，照片會從 4 px 的模糊中淡入，因此外框不會閃爍。

<Demo name="Photos" />

照片載入失敗時，大頭貼會回退到 `Initials` 並保留首字母。失敗的網址會針對該 `Source` 值被記住，因此每次大頭貼重繪時不會重複請求同一個失效連結。若要重試，請為 `Source` 指派一個新值。照片並非版面所必需：無論有沒有照片，每個尺寸都會保留相同的外框。

### 尺寸

`Size` 決定直徑：`Small` 為 28 px，`Medium`（預設）為 36 px，`Large` 為 48 px，`XLarge` 為 88 px。尺寸不隨視窗變化，在任何寬度下都相同。

| 尺寸 | 直徑 | 首字母文字 | 狀態圓點 | 適用情境 |
|---|---|---|---|---|
| `Small` | 28 px | 10 px | 8 px | 表格列、活動記錄、密集清單 |
| `Medium` | 36 px | Arc XS | 10 px | 頁首、卡片與表單摘要的預設值 |
| `Large` | 48 px | Arc SM | 12 px | 人員名冊面板、個人資料卡 |
| `XLarge` | 88 px | Arc XL | 14 px | 帳號與個人資料頁的頁首 |

```xml
<arc:Avatar DisplayName="Ada Lovelace" Size="Small" />
<arc:Avatar DisplayName="Grace Hopper" Size="Medium" />
<arc:Avatar DisplayName="Alan Turing" Size="Large" />
<arc:Avatar DisplayName="Katherine Johnson" Size="XLarge" />
```

同一個清單或同一組相關的大頭貼使用同一個尺寸，這樣列高一致，圓點也能對齊。在密集的表格中，`Small` 搭配 8 px 的圓點仍然清楚可讀，因為圓點帶有一圈能把它與照片分隔開的環。

### 狀態

`Status` 在右下角顯示上線或離線圓點。預設值 `AvatarStatus.None` 會隱藏圓點。

<Demo name="Status" />

只有在產品確實掌握上線狀態時才顯示它，例如目前聊天室中的人、值班的工程師，或正在接待對話的客服。對於上線狀態未知的人，不要自行假設為 `Offline`，請保持 `None`。`Offline` 是中性狀態而非錯誤，因此使用淡化的文字顏色，從不使用危險色。狀態也會改變無障礙名稱（見「無障礙」一節），因此圓點從來不是唯一的訊號。

### 首字母

首字母取自 `DisplayName` 前兩個單字的首字母，並轉為大寫。多餘的空白會被忽略；只有一個單字的名稱只得到一個字母。

| `DisplayName` | `Initials` |
|---|---|
| `Ada Lovelace` | `AL` |
| `  grace   brewster hopper ` | `GB` |
| Plato | `P` |
| 空白或只含空白字元 | 空 |

`Initials` 為唯讀，每當 `DisplayName` 變動時都會更新。`Avatar.ComputeInitials` 對任意字串套用同樣的規則，例如用於在程式碼中產生標籤。

### 繫結到檢視模型

`Avatar` 的值是單向流動的。從檢視模型繫結 `DisplayName`、`Source`、`Status` 與 `Size`；大頭貼從不把值寫回，也不會引發檢視模型需要處理的事件。

```csharp
public partial class OnCallMemberViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private ImageSource? _photo;

    [ObservableProperty]
    private AvatarStatus _status = AvatarStatus.None;
}
```

```xml
<arc:Avatar DisplayName="{Binding DisplayName}"
            Source="{Binding Photo}"
            Status="{Binding Status}"
            Size="Large" />
```

這些屬性必須在 UI 執行緒上設定，因為 Avalonia 屬性只能在該執行緒上變更。當上線狀態服務在背景執行緒上回報變化時，請透過 `Dispatcher.UIThread.Post` 投遞更新。`ImageSource` 由檢視模型建立，例如由你自己的服務載入的點陣圖；沒有圖片時將 `Photo` 保持為 null，此時會顯示首字母。

### 清單與表格中的照片

每個畫面上的列都會為照片耗用記憶體與解碼時間。`Source` 會非同步載入，照片以大頭貼的顯示尺寸解碼，因此很大的原始圖檔也不會比圓形區域所需的成本更高。儘管如此，包含數百列的清單仍應遵循三條規則：

- 在長清單中使用 `Small`，只在頁面焦點的一兩個大頭貼上使用 `Large` 與 `XLarge`。
- 保持清單虛擬化，正如 Avalonia 的 `ListBox` 與 `DataGrid` 預設的那樣，這樣只有可見的列會建立大頭貼。
- 對沒有照片的紀錄完全不設定 `Source`，而不是使用空白或佔位圖像，這樣直接使用首字母，也不會產生失敗的請求。

### 範例：事件主控台中的值班名單

事件主控台列出了正在處理一次即時故障的回應人員。每一列都有一個 `Large` 大頭貼、姓名、角色與狀態圓點。其中兩位在線上的回應人員由上線狀態服務標為綠色圓點；第三位為 `Offline`，但保留了相同的外框，使姓名維持對齊。由於事件工具中還沒有這些人員的照片，名單只使用首字母；缺少照片不會產生任何成本，日後加入照片時版面也不會改變。

![值班名單，兩位在線上的回應人員，一位離線的回應人員，大頭貼皆為首字母](images/AvatarRoster.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="Incident response roster"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Padding="20"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="12">
        <StackPanel Spacing="18">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <arc:Avatar Grid.Column="0"
                            DisplayName="Maya Chen"
                            Size="Large"
                            Status="Online" />
                <StackPanel Grid.Column="1" Margin="14,0,0,0" Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Maya Chen" FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Incident commander" FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="2" Text="Primary" FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
            <Grid ColumnDefinitions="Auto,*,Auto">
                <arc:Avatar Grid.Column="0"
                            DisplayName="Noah Park"
                            Size="Large"
                            Status="Online" />
                <StackPanel Grid.Column="1" Margin="14,0,0,0" Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Noah Park" FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Database on-call" FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="2" Text="Secondary" FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
            <Grid ColumnDefinitions="Auto,*,Auto">
                <arc:Avatar Grid.Column="0"
                            DisplayName="Leah Fox"
                            Size="Large"
                            Status="Offline" />
                <StackPanel Grid.Column="1" Margin="14,0,0,0" Spacing="2" VerticalAlignment="Center">
                    <TextBlock Text="Leah Fox" FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Payments on-call, off shift until 18:00" FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="2" Text="Standby" FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### 文案規範

- **顯示名稱**：產品中人們使用的完整姓名，例如 "Maya Chen"。首字母與無障礙名稱都來自它，因此簡短的暱稱會得到糟糕的結果。
- **名稱文字**：不要把職稱、角色與縮寫放進 `DisplayName`。像 "Dr." 這樣的前綴會改變首字母，而角色應放在大頭貼旁邊的文字中。
- **狀態**：控制項中只存在 "online" 與 "offline" 兩個詞。更具體的內容，例如 "18:00 前離開"，請放在大頭貼旁邊的文字裡。

| 不要寫 | 改為 |
|---|---|
| `DisplayName="mchen"` | `DisplayName="Maya Chen"`，首字母為 "MC" |
| `DisplayName="Dr. Maya Chen"` | `DisplayName="Maya Chen"`，首字母為 "MC" 而非 "DM" |
| `DisplayName="Maya Chen (away)"` | `DisplayName="Maya Chen"`，並在列文字中寫 "18:00 前離開" |
| `DisplayName="Maya"` | `DisplayName="Maya Chen"`，兩個單字得到兩個字母 |

### 最佳做法

**建議**

- 傳入完整的顯示名稱，使首字母與無障礙名稱都能正確讀出。只有一個單字只會得到一個字母，讀者無法將它與同名的人區分開。
- 每個清單使用同一尺寸，表格與記錄中使用 `Small`。等高的列較容易掃讀，圓點也能保持在一直線上。
- 只根據產品掌握的上線狀態設定 `Status`。一個永遠是灰色的圓點毫無意義，還會讓人習慣忽略這個控制項。
- 只對產品確實擁有的照片使用 `Source`。沒有照片的紀錄保留首字母，外框中不會出現破圖。

**避免**

- 把大頭貼當成按鈕或選單觸發器。此控制項不可取得焦點，也沒有鍵盤處理；應把帳號選單包在 `UserMenu` 中，由它負責焦點與按鍵。
- 為了選擇尺寸而在區域內設定 `Width` 或 `Height`。這會覆寫 `Size` 提供的直徑，導致狀態圓點與首字母不再與 Arc 的尺寸比例相符。
- 在長清單中載入全尺寸照片。數百張 88 px 的照片會耗用大量記憶體；`Small` 搭配首字母幾乎沒有成本。
- 把離線圓點染成錯誤色。離線是中性狀態，危險色只屬於失敗。
- 在純文字匯出或螢幕閱讀器中只依賴圓點來表示上線狀態。狀態會透過名稱傳遞給輔助技術，因此當上線狀態關乎判斷時，也要把它寫在文字裡。

### 常見問題

**為什麼某一列沒有狀態圓點？** 預設的 `Status` 為 `AvatarStatus.None`，會隱藏圓點。設定 `Online` 或 `Offline` 即可顯示。

**為什麼照片沒有出現，首字母卻還在？** 要嘛照片仍在下載（抵達之前外框為空），要嘛載入失敗，於是保留了首字母。失敗的網址會針對該 `Source` 值被記住；為 `Source` 指派一個新值即可重試。

**為什麼兩個中文名只顯示一個字？** 首字母取自以空白分隔的每個單字的第一個字元，因此沒有空白的名字只會得到一個字，例如 "張三" 得到 "張"。本版本的 `Avatar` 沒有首字母的覆寫選項。若此類名字需要兩個字，請用你自己的標籤取代大頭貼。

**螢幕閱讀器如何朗讀大頭貼？** 無障礙名稱是 `DisplayName`，有狀態時後面接上狀態："Maya Chen, online"。沒有狀態時只唸出名稱。務必設定 `DisplayName`，因為空白名稱會使大頭貼沒有標籤。

**如何為同一個人載入新照片？** 將新的 `ImageSource` 指派給 `Source`。指派值會清除舊網址的失敗記錄，因此即使先前的網址失敗過，新照片也會被載入。

## API 參考

<!-- api: Avatar -->

## 無障礙

- 大頭貼是一個以 `DisplayName` 為名稱的影像。有狀態時，狀態會加入名稱，例如 "Maya Chen, online"。
- 照片與首字母僅作裝飾，對輔助技術隱藏，因此名稱只會被唸出一次。
- 狀態圓點已隱藏；狀態透過名稱傳遞給輔助技術。
- 大頭貼不可取得焦點，也沒有鍵盤行為：`Tab` 會略過它，鍵盤操作由擁有該動作的控制項負責，例如 `UserMenu` 或大頭貼所在的清單。
- 沒有狀態的名稱只唸出名稱本身。空白的 `DisplayName` 會使大頭貼沒有名稱，因此務必設定它。
- 當上線狀態關乎判斷時，請把它保留在周圍的文字中，例如 "離線，18:00 起值班"。

## 動態效果

- 仍在載入的照片會從 4 px 的模糊中淡入。已快取的照片會立即顯示。
- 當照片失敗或被移除、首字母回來時，首字母會在 0.24 秒內淡入。
- 狀態圓點會以 0.6 倍縮放開始、透過輕快的彈簧彈出；離開時則縮小消失。
- 狀態變化時，圓點顏色會交叉淡化。
- 減少動態效果時，狀態圓點立即切換，照片不經淡入直接顯示。關閉動態效果時，所有變化立即完成。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 仍在載入的照片到達 | 從 4 px 模糊中淡入 | 立即顯示，無模糊或淡入 | 立即 |
| 已快取的照片顯示 | 立即顯示 | 立即顯示 | 立即 |
| 照片失敗後首字母回來 | 0.24 s 內淡入 | 立即 | 立即 |
| 狀態圓點出現 | 以 0.6 倍縮放透過輕快彈簧彈出 | 立即 | 立即 |
| 狀態圓點離開 | 在 160 ms 內縮小消失 | 立即 | 立即 |
| 上線與離線切換顏色 | 圓點顏色交叉淡化 | 立即 | 立即 |

## 相關元件

- `AvatarGroup`：用於帶溢出計數的人員堆疊。
- `UserMenu`：大頭貼需要開啟帳號操作時使用。
- `Badge`：用於非人員的狀態或類別標籤。
- 下表列出其他顯示人員的元件。

| 元件 | 適用情境 |
|---|---|
| `AvatarGroup` | 一小組團隊成員或編輯者需要放進頁首、卡片或表格儲存格 |
| `UserMenu` | 大頭貼需要開啟登出、個人資料或工作區操作 |
| `SortableDataTable` | 姓名、角色與日期需要以欄位形式閱讀與比較 |
| `ChatThread` / `CommentThread` | 每則訊息都需要作者與時間與文字並排顯示 |
| `Badge` | 標籤是狀態或類別，而非人員 |
| `Input` | 人員的姓名或電子郵件需要輸入或編輯 |
