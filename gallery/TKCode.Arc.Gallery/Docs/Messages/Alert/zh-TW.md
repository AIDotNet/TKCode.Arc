---
title: 警告提示
summary: 持續顯示的訊息，協助使用者復原或繼續操作。
source-hash: 1e6aba0fce9e
---

## 何時使用

- 頁面或表單中的內嵌通知，例如即將到期的卡片或缺少的網域驗證。
- 會隨時間改變的狀態。保留同一個 `Alert`，改變它的語氣、標題與內容即可。
- 使用者可以關閉的通知，關閉後其下方的空間也會收合。

## 何時不用

- 完成動作後的簡短確認：用 `Toast`。
- 具有類型、動作與堆疊的應用程式通知：用 `ToastStack`。
- 使用者必須先回應才能繼續的訊息：用 `Dialog`。
- 沒有內容可顯示的頁面或清單：用 `EmptyState`。

## 用法

基本用法會設定 `Title` 與 `Content`。預設的 `Tone` 為 `Info`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Alert Title="Your card expires next month"
               Content="Update your payment method to keep your plan active." />
</UserControl>
```

### 語氣

`Tone` 決定圖示與其顏色。`Danger` 會以強烈方式宣告，其他語氣則以禮貌方式宣告。

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="Success" Title="Domain verified"
               Content="Emails from acme.dev will now be delivered." />
    <arc:Alert Tone="Warning" Title="You're close to your storage limit"
               Content="Free up space or upgrade before uploads pause." />
    <arc:Alert Tone="Danger" Title="Payment failed"
               Content="We couldn't charge the card ending in 4242." />
</StackPanel>
```

### 原位變形

狀態改變時，保留同一個執行個體並改變它的屬性。圖示會替換，新文字會升起出現，高度則以彈簧跟隨變化。若改為替換控制項，就會略過這些轉場。

```csharp
var status = new Alert { Title = "Ready to sync", Content = "Changes are saved on this device." };

async void OnSave(object? sender, RoutedEventArgs e)
{
    status.Tone = AlertTone.Info;
    status.Title = "Saving changes";
    status.Content = "Uploading 3 files to the workspace.";

    await UploadAsync();

    status.Tone = AlertTone.Success;
    status.Title = "All changes saved";
    status.Content = "Everyone in the workspace now sees the latest version.";
}
```

### 關閉

設定 `IsDismissible` 即可顯示關閉按鈕。按下後警示會先收合，然後引發 `Dismissed`。將 `IsOpen` 設回 `True` 會再次顯示警示。

```xml
<arc:Alert Tone="Warning"
           Title="Scheduled maintenance on Sunday"
           Content="The dashboard is read-only from 02:00 to 03:00 UTC."
           IsDismissible="True" />
```

```csharp
var notice = new Alert { Tone = AlertTone.Warning, IsDismissible = true, Title = "Scheduled maintenance on Sunday" };
notice.Dismissed += (_, _) => SaveDismissedState();

// Show the alert again later.
notice.IsOpen = true;
```

### 自訂內容

任何非文字的 `Content` 都會顯示在標題下方。文字內容會套用詳細說明的樣式；其他內容則維持原樣顯示。

```xml
<arc:Alert Title="A new version is available">
    <arc:Alert.Content>
        <arc:Button Content="Reload" Size="Small" Variant="Secondary" />
    </arc:Alert.Content>
</arc:Alert>
```

## API 參考

<!-- api: Alert -->

## 無障礙

- `Title` 為警示命名，`Content` 會被當作其說明文字朗讀。標題改變時會再次宣告。
- `Danger` 警示以強烈方式宣告；其他語氣以禮貌方式宣告。
- 關閉按鈕名稱為 `關閉：{Title}`，讓使用者聽得出它關閉的是哪一則警示。
- 語氣圖示不會對輔助技術公開。語氣由標題與內容傳達。
- 關閉按鈕是一般按鈕：Tab 可以移至此處，按下 Enter 或 Space 即可啟動。

## 動態效果

- 顯示與隱藏警示時，其高度會以平滑彈簧展開與收合，下方的內容也隨之收合上移。
- 語氣改變時，圖示會以短暫的縮放與模糊替換。變化的文字會升起並變清晰。
- 減少動態效果時，會移除高度動畫與位移。文字與圖示僅淡入淡出，顯示或隱藏則立即完成。
- 關閉動態效果時，直接跳到最終狀態。

## 相關元件

- `Toast`：用於完成動作後的簡短確認。
- `ToastStack`：用於需要排隊、更新與堆疊的應用程式通知。
- `AnnouncementBar`：用於網站頂部的輪播橫幅。
- `EmptyState`：用於完全沒有內容可顯示的情況。
