---
title: Cookie 同意
summary: 同意列可展開為偏好設定，並收合為重新開啟按鈕。
source-hash: 71df8065065e
---

<Demo name="ConsentBar" />

## 何時使用

`CookieConsent` 向訪客徵求關於 Cookie 與追蹤的選擇，並保存這一選擇。它以圖層的形式覆蓋在頁面底部邊緣，頁面在它後面仍可閱讀。同意列提供「全部接受」「全部拒絕」與「自訂」。點選「自訂」會開啟一個附開關的類別面板；儲存後的選擇會收合為一個小型的重新開啟按鈕，讓訪客在任何頁面都能隨時改變主意。

- **載入分析與廣告指令碼的行銷網站**：同意列在任何選用指令碼執行之前出現。「全部接受」與「全部拒絕」權重相同，拒絕與接受一樣容易。
- **帶選用類別的商店或 SaaS 網頁應用程式**：偏好設定、分析與行銷是各自獨立的開關，初始為關閉；嚴格必要（登入、安全性、購物車）永遠開啟。
- **之後更改選擇**：做出選擇後，重新開啟按鈕會保留在角落，訪客不必尋找頁尾連結，就能在任何頁面開啟偏好設定。
- **政策更新需要新的答覆**：類別或政策變更時修改 `Version`，先前儲存的版本選擇會被忽略，訪客將被再次詢問。

## 何時不用

- **不需要任何選擇的全站通知**，例如促銷或維護時段：用 `AnnouncementBar`。
- **會自行關閉的簡短確認**：用 `Toast`。
- **必須等使用者回答才能繼續的決定**，例如年齡驗證：用 `Dialog`。
- **頁面某個區域內的一般訊息**：用 `Alert`。

## 用法

將元件放在頁面之上，例如作為視窗根 `Grid` 的最後一個子元素。此元件是一個圖層：除自身表面外，不接收指標輸入。未設定類別時，使用 Arc 的四個預設類別：嚴格必要、偏好設定、分析與行銷。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Grid>
        <!-- 頁面內容放在這裡。 -->
        <arc:CookieConsent StorageKey="site-consent" Version="1" />
    </Grid>
</UserControl>
```

`CookieConsent` 衍生自 Avalonia 的 `TemplatedControl`。它的同意列、面板與重新開啟按鈕是同一個表面的三種形態，元件會在它們之間移動，因此只需設定一個元件，無須自行放置各個形態。由於元件會覆蓋其周圍的內容，它必須排在父面板中那些內容之後。

### 結構

1. **圖層**：與元件父層的左、右、下邊緣保持 16 px 的距離。只有表面接收指標輸入，因此圖層下方的頁面仍可點選。
2. **表面**：帶 1 px 邊框環與浮動陰影的凸起表面。同意列的圓角為 22 px，面板為 26 px。
3. **同意列**（`IsPreferencesOpen` 為 `False` 且沒有已儲存的選擇）：次要文字顏色的訊息，末尾是行內的 `Policy` 連結。同意列最寬 660 px，訊息前為 18 px，訊息後為 10 px。三個按鈕依序為自訂（幽靈按鈕）、全部拒絕與全部接受（次要按鈕），皆為小尺寸，間距 6 px。寬度小於 560 px 時，訊息會位於按鈕上方。
4. **面板**（`IsPreferencesOpen` 為 `True`）：寬 420 px，內距 18 px。包含標題、32 px 的關閉按鈕、說明、類別清單與操作區。類別清單超過 300 px 高度後可捲動。每一列包含類別標籤及其說明，以及一個開關；必要類別則顯示文字 `Always on`。操作區中，「全部拒絕」與「全部接受」並排放置，「儲存選擇」橫跨整個寬度，位於一條細分隔線之下。
5. **重新開啟按鈕**：直徑 44 px 的圓形幽靈按鈕，附 Cookie 圖示。做出選擇後，當 `IsReopenButtonVisible` 為 `True` 時出現，位於同意列所在一側的角落（由 `ReopenSide` 決定，同意列置中時位於左側）。

### 類別與政策

設定 `Categories` 以定義類別。`IsRequired` 讓某個類別永遠開啟，`IsEnabledByDefault` 設定選用類別在儲存選擇之前的初始狀態。除非你設定此項，否則選用類別預設為關閉。`Policy` 會在同意列訊息末尾與面板說明中加入一個指向完整政策的連結。

四個預設類別如下：

| Id | 標籤 | 初始狀態 |
|---|---|---|
| `necessary` | Strictly necessary | 開啟，永遠開啟（必要） |
| `preferences` | Preferences | 關閉 |
| `analytics` | Analytics | 關閉 |
| `marketing` | Marketing | 關閉 |

```xml
<arc:CookieConsent StorageKey="site-consent" Version="2">
    <arc:CookieConsent.Categories>
        <arc:CookieCategory Id="necessary" Label="嚴格必要"
                            Description="網站正常運作所需。"
                            IsRequired="True" IsEnabledByDefault="True" />
        <arc:CookieCategory Id="analytics" Label="分析"
                            Description="協助我們了解哪些頁面有用。" />
    </arc:CookieConsent.Categories>
    <arc:CookieConsent.Policy>
        <arc:CookieConsentPolicy Label="Cookie 政策" Href="https://example.com/cookies" />
    </arc:CookieConsent.Policy>
</arc:CookieConsent>
```

類別或政策變更時請修改 `Version`。版本不同的已儲存選擇會被忽略，訪客將被再次詢問。版本是字串，因此 `"2"` 與 `"2.0"` 是不同的版本。

### 回應選擇

訪客儲存、接受、拒絕或重設選擇時會引發 `ConsentChanged`。`Consent` 將每個類別的 id 對應為 `True` 或 `False`，訪客尚未決定時為 `null`。只在此事件中啟動選用指令碼，或在啟動時讀取已儲存的選擇後再啟動，切勿在訪客作答之前啟動。

```csharp
public static class Analytics
{
    public static bool Enabled { get; set; }
}

consent.ConsentChanged += (_, e) =>
{
    if (e.Consent is not null && e.Consent.TryGetValue("analytics", out var allowed))
    {
        Analytics.Enabled = allowed;
    }
};
```

`Action` 說明選擇是如何做出的：`AcceptAll`、`RejectAll`、`Save`（面板）或 `Reset`。執行 `Reset` 之後 `Consent` 的值為 `null`，因此可以據此判斷訪客正在被再次詢問。

### 讀取已儲存的選擇

在元件顯示之前、啟動時讀取已儲存的選擇，即可為已做出決定的訪客略過同意列。

```csharp
var record = CookieConsent.Read("site-consent", "2");
if (record is not null)
{
    Analytics.Enabled = record.Value.TryGetValue("analytics", out var allowed) && allowed;
}
```

當沒有儲存任何內容、儲存空間無法使用，或已儲存的選擇是為另一個版本儲存的時，`Read` 會返回 `null`。記錄會公開其 `Value` 字典、`Version` 與 `UpdatedAt` 時間。元件在首次掛載時讀取同一份儲存空間，因此已做決定的訪客不會在重新開啟按鈕出現之前看到同意列閃現。

### 繫結到檢視模型

元件包含一個值（`Consent`）、一個類別清單（`ItemsSource`）以及一個開啟狀態（`IsPreferencesOpen`）。使用 CommunityToolkit.Mvvm 時，這些屬性由檢視模型持有，繫結會讓它們與訪客的選擇保持同步。

```csharp
public partial class ConsentViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<CookieCategory> _categories = new()
    {
        new CookieCategory { Id = "necessary", Label = "嚴格必要", IsRequired = true, IsEnabledByDefault = true },
        new CookieCategory { Id = "analytics", Label = "分析", Description = "以彙總方式統計造訪次數。" }
    };

    [ObservableProperty]
    private IReadOnlyDictionary<string, bool>? _consent;

    [ObservableProperty]
    private bool _isPreferencesOpen;
}
```

```xml
<arc:CookieConsent StorageKey="site-consent"
                   Version="2"
                   ItemsSource="{Binding Categories}"
                   Consent="{Binding Consent, Mode=TwoWay}"
                   IsPreferencesOpen="{Binding IsPreferencesOpen, Mode=TwoWay}" />
```

`Consent` 與 `IsPreferencesOpen` 是雙向繫結的。訪客做決定、開啟或關閉面板時，元件會寫入這兩個屬性，因此檢視模型無須事件處理常式即可讀取選擇。從程式碼設定 `Consent` 不會儲存任何內容：只有訪客的決定或 `Reset` 才會在設定了 `StorageKey` 時寫入儲存空間。若要儲存你自己計算出的選擇，請使用 `Read` 讀取已儲存的值，或使用你自己的儲存空間。

### 位置與重新開啟按鈕

`Position` 將同意列設定在底部、左下或右下。`IsReopenButtonVisible` 會在儲存選擇後保留 44px 的按鈕。`OpenPreferences` 可從其他元件（例如頁尾連結）開啟面板。

```xml
<arc:CookieConsent Position="BottomRight" IsReopenButtonVisible="True" />
```

```csharp
settingsLink.Click += (_, _) => consent.OpenPreferences();
```

請將位置放在頁面自身的控制項不會被遮住的地方：右下角的同意列可能會遮住聊天按鈕或「回到頂部」按鈕。除非訪客可以在其他地方更改選擇（例如帳號設定頁面），否則請保持 `IsReopenButtonVisible` 為 `True`。若沒有重新開啟按鈕，已儲存的選擇會將元件完全隱藏。

### 範例：行銷網站上的同意

一位訪客來到 Northwind Outdoors（一家販售健行背包的商店），尚未作答。同意列覆蓋在商品頁面之上，訊息、政策連結與三個選項具有相同的閱讀權重。「全部接受」與「全部拒絕」並排放置且尺寸相同，這正是訪客信任的同意模式。「自訂」保持為低調的幽靈按鈕，既可用，又不會與兩個答案爭奪注意力。

![商店頁面上的 Cookie 同意列，「全部接受」與「全部拒絕」按鈕權重相同](images/StoreConsent.png)

```xml
<Border Width="720"
        Height="400"
        CornerRadius="12"
        ClipToBounds="True"
        Background="{arc:ArcDesignTokenResource Background}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
    <Grid>
        <StackPanel Margin="32,28" Spacing="10">
            <TextBlock Text="Northwind Outdoors" FontSize="24" FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Trail packs for weekend hikes, shipped within two days."
                       FontSize="14" TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <TextBlock Text="Free returns within 30 days." FontSize="14"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:CookieConsent StorageKey="northwind-consent">
            <arc:CookieConsent.Storage>
                <arc:InMemoryMessageStorage />
            </arc:CookieConsent.Storage>
            <arc:CookieConsent.Policy>
                <arc:CookieConsentPolicy Label="Cookie policy" />
            </arc:CookieConsent.Policy>
        </arc:CookieConsent>
    </Grid>
</Border>
```

### 文案規範

- **訊息**：說明 Cookie 的用途以及訪客能獲得什麼。預設訊息會同時說明必要 Cookie 與選用 Cookie，因此替換時請保持相同的結構。
- **按鈕文字**：保留預設值（全部接受、全部拒絕、自訂、儲存選擇），讓訪客在每個網站上看到相同的字詞。它們採用句首大寫，並說明按鈕的作用。
- **類別說明**：用淺白的文字以一句話說明用途：「只以彙總方式統計造訪次數，並了解哪些頁面有幫助。」
- **在地化**：文案來自 `Labels`（`CookieConsentLabels`）。每個元件都有自己的標籤，因此請用頁面的語言設定它們，而不是在程式碼中修改訊息。

| 不要寫 | 改為 |
|---|---|
| 接受 | 全部接受 |
| 我們使用 Cookie 來改善您的體驗！！！ | 我們使用必要的 Cookie 來執行本站。若您同意，我們還會使用選用的 Cookie。 |
| 必要 | 嚴格必要 |
| Cookie 設定 | 自訂 |

### 最佳做法

**建議**

- 讓「全部接受」與「全部拒絕」擁有相同的權重、尺寸與位置。讓拒絕變得更困難的同意會被視為暗黑模式，注意到它的訪客會停止信任網站的其他部分。
- 在訪客選擇之前，將選用類別保持為關閉，並且只在 `ConsentChanged` 中啟動選用指令碼，或在啟動時讀取已儲存的選擇後再啟動。
- 每當類別或政策變更時修改 `Version`，讓訪客被再次詢問。
- 保持重新開啟按鈕可見，讓訪客隨時可以更改選擇。

**避免**

- 將 `IsEnabledByDefault` 設為 `True` 來預先勾選選用類別。這些類別並非訪客自己選的。
- 從程式碼設定 `Consent` 來記錄選擇。它不會被儲存；請透過元件做出決定，或自行儲存該值。
- 讓同意列遮住訪客需要的內容，例如結帳按鈕。請在小視窗中測試該圖層。
- 用同意列詢問與 Cookie 或追蹤無關的問題。詢問其他事項的同意對話方塊會讓訪客養成直接關閉的習慣。

### 常見問題

**為什麼已經作答的訪客看不到同意列？** 目前 `Version` 的已儲存選擇會將元件收合為重新開啟按鈕，因此同意列依設計被隱藏。使用 `Reset` 可忘記該選擇並重新詢問。如果你預期同意列會出現，請檢查 `Read` 是否返回了具有相同 `StorageKey` 與版本的記錄。

**Escape 會儲存選擇嗎？** 不會。Escape 會關閉偏好設定面板而不儲存，同意列維持原樣。開關的草稿會被捨棄，因此重新開啟面板時顯示的是已儲存的值。

**如何從頁尾連結開啟偏好設定？** 呼叫 `OpenPreferences()`（如用法範例所示），或雙向繫結 `IsPreferencesOpen`。`ClosePreferences()` 會在不儲存的情況下關閉面板。`Reset()` 會忘記已儲存的選擇、清除儲存空間，並重新顯示同意列。

**選擇儲存在哪裡？** 若你在元件上設定了 `Storage`，則儲存在那裡；否則儲存在 `MessageStorage.Default` 中，它是本機應用程式資料資料夾中的一個檔案。儲存的文字是 JSON，包含值、版本與時間，例如 `{"value":{"analytics":true},"version":"2","updatedAt":"2026-10-21T09:30:00.0000000+00:00"}`。在預覽與測試中請使用記憶體儲存。

**能否在網站之外使用此元件？** 它是一個通用的同意圖層。在桌面應用程式中，措辭與類別由你決定：`Labels` 修改同意列與面板的文案，你自己的類別則說明要詢問的內容，因此同意列可以詢問遙測或資料共享，而不是 Cookie。

## API 參考

<!-- api: CookieConsent -->

<!-- api: CookieCategory -->

與 `CookieConsent` 搭配使用的輔助型別：

| 型別 | 成員 |
|---|---|
| `CookieConsentPolicy` | `Label`（連結文字，預設 `Cookie policy`）、`Href`（`Uri`，透過系統啟動器開啟）、`Command`（`ICommand`，觸發時執行） |
| `CookieConsentLabels` | `Message`、`Customize`、`AcceptAll`、`RejectAll`、`Save`、`PreferencesTitle`、`PreferencesDescription`、`Reopen`、`AlwaysOn`、`Close` |
| `CookieConsentAction` | `AcceptAll`、`RejectAll`、`Save`、`Reset`：選擇是如何做出的 |
| `CookieConsentPosition` | `Bottom`（預設）、`BottomLeft`、`BottomRight` |
| `CookieConsentRecord` | `Value`（類別 id 對應到是否允許）、`Version`、`UpdatedAt`：由 `CookieConsent.Read` 返回 |

## 無障礙

- 同意列與面板都是非模態對話方塊。二者均以標題命名，其訊息或說明作為輔助說明。
- 開啟面板時，焦點移至其標題。儲存面板後，焦點移至重新開啟按鈕。
- 每個類別的開關以類別標籤命名，說明作為輔助說明。必要類別顯示 `Always on` 文字，而不是開關。
- Tab 在按鈕與開關之間移動。空白鍵切換目前焦點所在的開關，點按類別名稱也會切換。
- Escape 關閉面板且不儲存，並將焦點返回到同意列。
- 「全部拒絕」與「全部接受」權重相同，並排放置。
- 重新開啟按鈕以 `Cookie preferences`（`Reopen` 標籤）命名，因此在做出選擇之後可以透過名稱找到它。
- 儲存按鈕的輔助說明會統計選擇的數量，例如「3 個選用類別中已允許 2 個」，因此螢幕閱讀器使用者在儲存之前就能聽到效果。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在同意列或面板的按鈕與開關之間移動 |
| `Space` | 切換目前焦點所在的類別開關 |
| `Enter` | 按下目前焦點的按鈕：自訂、全部拒絕、全部接受、儲存選擇、關閉或重新開啟按鈕 |
| `Escape` | 在面板中關閉它且不儲存，並將焦點返回到同意列 |

## 動態效果

- 同意列、面板與重新開啟按鈕是同一個表面，它會改變位置、寬度、高度與圓角。
- 每個面板內容從短暫的模糊中上升幾個像素出現。離場的面板淡出並略微下沉。
- 同意列以輕微縮放從底邊進入。
- 減少動態效果時，表面在各形態之間直接跳轉，內容只做淡入。關閉動態效果時，每次變化立即顯示。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 同意列進入 | 以輕微縮放從底邊升起並淡入 | 淡入 | 立即 |
| 表面改變形態（同意列、面板、重新開啟按鈕） | 移動、改變寬高與圓角。放大時帶有輕微過衝，縮小時則直接穩定，沒有過衝 | 直接跳轉到新形態 | 立即 |
| 內容切換 | 新內容從約 10 px 的模糊中上升；離場內容淡出並略微下沉 | 新內容淡入；離場內容快速淡出 | 立即 |

## 相關元件

- `AnnouncementBar`：不需要使用者選擇的全站通知。
- `Dialog`：必須等使用者回答才能繼續的決定。
- `Toast`：會自行關閉的簡短確認。

| 元件 | 適用情境 |
|---|---|
| `AnnouncementBar` | 通知面向所有訪客，且不需要做出選擇 |
| `Dialog` | 使用者必須先作答，頁面才能繼續 |
| `Toast` | 簡短的確認應自行關閉 |
| `Alert` | 一般訊息屬於頁面的某一個區域 |
| `Switch` | 在你自己的頁面中，表單內設定單一偏好 |
