---
title: Cookie 同意
summary: 同意列可展開為偏好設定，並收合為重新開啟按鈕。
source-hash: 086912befeca
---

## 何時使用

- 需要訪客接受或拒絕 Cookie 與追蹤的行銷網站或應用程式。
- 依類別徵求同意：選用類別預設關閉，訪客之後可以更改選擇。
- 提供重新開啟按鈕，讓訪客在儲存選擇後仍能修改。

## 何時不用

- 不需要任何選擇的全站通知：用 `AnnouncementBar`。
- 無需選擇、會自行消失的訊息：用 `Toast`。
- 必須等使用者回答才能繼續瀏覽頁面的決定：用 `Dialog`。
- 頁面某個區域內的一般訊息：用 `Alert`。

## 用法

將元件放在頁面之上，例如作為視窗根 `Grid` 的最後一個子元素。此元件是一個圖層：除自身表面外，不接收指標輸入。未設定類別時，使用 Arc 的四個預設類別：嚴格必要、偏好設定、分析與行銷。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- 頁面內容放在這裡。 -->
        <arc:CookieConsent StorageKey="site-consent" Version="1" />
    </Grid>
</UserControl>
```

### 類別與政策

設定 `Categories` 以定義類別。`IsRequired` 讓某個類別始終開啟，`IsEnabledByDefault` 設定選用類別的初始狀態。`Policy` 會在同意列與面板中加入指向完整政策的連結。

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

類別或政策變更時請修改 `Version`。版本不同的已儲存選擇會被忽略，訪客將再次被詢問。

### 回應選擇

訪客儲存、接受、拒絕或重設選擇時會引發 `ConsentChanged`。`Consent` 將每個類別 id 對應為 `True` 或 `False`；訪客尚未決定時為 `null`。

```csharp
consent.ConsentChanged += (_, e) =>
{
    if (e.Consent is not null && e.Consent.TryGetValue("analytics", out var allowed))
    {
        Analytics.Enabled = allowed;
    }
};
```

### 讀取已儲存的選擇

在元件顯示之前、啟動時讀取已儲存的選擇，即可為已做出決定的訪客略過同意列。

```csharp
var record = CookieConsent.Read("site-consent", "2");
if (record is not null)
{
    Analytics.Enabled = record.Value.TryGetValue("analytics", out var allowed) && allowed;
}
```

### 位置與重新開啟按鈕

`Position` 將同意列設定在底部、左下或右下。儲存選擇後，`IsReopenButtonVisible` 會保留 44px 的按鈕。`OpenPreferences` 可從其他元件（例如頁尾連結）開啟面板。

```xml
<arc:CookieConsent Position="BottomRight" IsReopenButtonVisible="True" />
```

```csharp
settingsLink.Click += (_, _) => consent.OpenPreferences();
```

## API 參考

<!-- api: CookieConsent -->

<!-- api: CookieCategory -->

## 無障礙

- 同意列與面板都是非模態對話方塊。二者均以標題命名，其訊息或描述作為說明文字。
- 開啟面板時焦點移至其標題。儲存面板後焦點移至重新開啟按鈕。
- 每個類別的開關以類別標籤命名，描述作為說明文字。必要類別以 `Always on` 文字取代開關。
- Tab 在按鈕與開關之間移動。空白鍵切換目前焦點所在的開關，點按類別名稱也會切換。
- Escape 關閉面板且不儲存，焦點返回同意列。
- 全部拒絕與全部接受權重相同，並排放置。

## 動態效果

- 同意列、面板與重新開啟按鈕是同一個表面，它會改變位置、寬度、高度與圓角。
- 每個面板內容從短暫的模糊中上升幾個像素出現。離場的面板淡出並略微下沉。
- 同意列以輕微縮放從底邊進入。
- 減少動態效果時，表面在各形態之間直接跳轉，內容只做淡入。關閉動態效果時，每次變化立即顯示。

## 相關元件

- `AnnouncementBar`：不需要使用者選擇的全站通知。
- `Dialog`：必須等使用者回答才能繼續瀏覽頁面的決定。
- `Toast`：會自行關閉的簡短確認。
