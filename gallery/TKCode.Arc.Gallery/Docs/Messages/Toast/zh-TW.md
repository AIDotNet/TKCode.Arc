---
title: 輕提示
summary: 已完成背景操作的簡短確認。
source-hash: 6adfd1506979
---

## 何時使用

- 背景操作完成後的確認，例如「變更已儲存」，使用者仍停留在目前頁面。
- 由頁面透過 `IsOpen` 控制的單則本機訊息。
- 不排隊、可自行關閉或透過滑動關閉的簡短訊息。

## 何時不用

- 多則帶類型、操作或更新的通知：用 `ToastStack`。
- 需要保留在頁面上、直到使用者處理的訊息：用 `Alert`。
- 必須先回應才能繼續的訊息：用 `Dialog`。
- 網站頂部輪播的橫幅：用 `AnnouncementBar`。

## 用法

`Toast` 顯示其 `Title` 與選用的 `Description`。`IsOpen` 為 `True` 時可見，並在 `Duration`（預設 4.5 秒）後關閉。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Toast Title="變更已儲存"
               Description="已同步到你的所有裝置。"
               VerticalAlignment="Bottom" />
</UserControl>
```

### 顯示與隱藏

輕提示關閉後，將 `IsOpen` 設為 `True` 即可再次顯示。輕提示開啟期間，標題與描述可以改變；新文字會向上浮現，高度隨之變化。

```csharp
toast.Title = "草稿已發佈";
toast.Description = "讀者幾秒內即可看到。";
toast.IsOpen = true;
```

### 持續時間

`Duration` 設定輕提示關閉前的時間。值為零或更小時，輕提示會保持開啟，直到使用者將其關閉。懸停時計時器不會暫停，因此文案應簡短。

```xml
<arc:Toast Title="上傳完成" Duration="0:0:8" />
<arc:Toast Title="仍在同步" Duration="0" />
```

### 關閉事件

輕提示開始關閉時會引發 `Closed`。`Reason` 說明關閉原因：使用者按下關閉按鈕、滑走了輕提示，還是時間到了。

```csharp
toast.Closed += (_, e) =>
{
    if (e.Reason == ToastCloseReason.TimedOut)
    {
        log.Text = "訊息已自動關閉。";
    }
};
```

### 應用程式層級的輕提示

`Toast.Show` 可從視窗中的任何控制項，將輕提示送到視窗的 `ToastStack`。當訊息來自不持有 `Toast` 控制項的程式碼時使用它。`ToastKind` 選擇圖示與顏色。`Toast.Show` 會傳回一個 id，可交給 `Toast.Update` 與 `Toast.Dismiss` 使用。

```csharp
Toast.Show(button, "邀請已寄出", ToastKind.Success, "Maya 很快會收到一封電子郵件。");
```

## API 參考

<!-- api: Toast -->

## 無障礙

- `Title` 是輕提示的名稱，`Description` 是其說明文字。輕提示以禮貌方式播報，且不會取得焦點。
- 關閉按鈕名為 `Dismiss notification`。Tab 可以移至此處，按 Enter 或空白鍵即可關閉輕提示。
- 勾號圖示不會對輔助技術公開。
- 文字請保持簡短。計時器不會暫停，需要更多時間的使用者應取得會一直保留的訊息，例如 `Alert`。
- 滑動是指標與觸控手勢。鍵盤使用者可透過關閉按鈕得到相同的結果。

## 動態效果

- 輕提示從下方升起並淡入。片刻之後，其勾號圖示會逐筆繪出。
- 關閉時輕提示略微下沉並淡出。已關閉的輕提示不再佔用空間。
- 變化的文字向上浮現，高度隨彈簧變化。
- 減少動態效果時，輕提示只做淡入淡出，並關閉滑動拖曳。
- 關閉動態效果時，輕提示立即顯示與隱藏。

## 相關元件

- `ToastStack`：用於應用程式任何位置發出、可排隊、帶類型且可更新的通知。
- `Alert`：需要保留在頁面上、直到問題解決的訊息。
- `AnnouncementBar`：帶倒數計時的輪播橫幅。
