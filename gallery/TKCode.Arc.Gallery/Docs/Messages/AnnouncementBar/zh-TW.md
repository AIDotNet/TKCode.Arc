---
title: 公告列
summary: 頂部橫幅，輪播訊息、倒數計時，關閉時平滑收合。
source-hash: 0603b1c8c592
---

## 何時使用

- 頁面頂部的全站通知，例如促銷、發佈或維護時段。
- 在同一列中輪播的兩到四則簡短公告。
- 帶即時倒數計時的促銷活動。

## 何時不用

- 關於頁面某個區域或表單的訊息：用 `Alert`。
- 訪客剛剛操作後的回饋：用 `Toast` 或 `ToastStack`。
- Cookie 或追蹤的同意選擇：用 `CookieConsent`。
- 使用者必須先處理才能繼續的一次性訊息：用 `Dialog`。

## 用法

將公告加入 `Messages`。公告列先顯示第一則，之後每隔 `Interval` 輪播其餘訊息，預設間隔為 6 秒。

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

### 操作與倒數計時

`Announcement` 可以帶一個 `Action` 按鈕，設定 `Label` 與 `Href`；也可以帶一個 `Countdown`，倒數至 `To`。當操作需要執行程式碼時，在按鈕上使用 `Command` 取代 `Href`。

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
bar.ActionInvoked += (_, e) => Analytics.Track("announcement.action", e.Announcement.Id);
bar.CountdownEnded += (_, e) => bar.Messages.Remove(e.Announcement);
```

### 控制與自動輪播

設定 `IsNavigationVisible` 即可顯示上一則、下一則按鈕以及暫停環。`IsAutoPlayEnabled` 可關閉輪播；`Next()` 與 `Previous()` 則可在程式碼中切換訊息。

```xml
<arc:AnnouncementBar IsNavigationVisible="True"
                     Interval="0:0:8"
                     IsAutoPlayEnabled="True" />
```

### 語氣

`Inverted` 會將前景色作為填滿色，背景色作為文字色。僅用於最重要的通知。

```xml
<arc:AnnouncementBar Tone="Inverted" />
```

### 記住關閉狀態

設定 `DismissalKey` 可在多次執行之間記住關閉狀態。此鍵值儲存在 `Storage` 中。呼叫 `AnnouncementBar.ClearDismissal` 清除已儲存的值，即可再次顯示公告列。關閉狀態由 `Storage` 儲存，預設為應用程式資料資料夾中的一個檔案。

```xml
<arc:AnnouncementBar DismissalKey="spring-campaign" IsDismissible="True" />
```

```csharp
AnnouncementBar.ClearDismissal("spring-campaign");
bar.IsOpen = true;
```

## API 參考

<!-- api: AnnouncementBar -->

<!-- api: Announcement -->

## 無障礙

- 公告列是一個區域，名稱由 `Label` 指定，預設為 `Announcements`。
- 每則訊息都有名稱，例如 `1 of 3`，螢幕閱讀器由此得知它在輪播中的位置。
- 輪播切換時不會逐則播報。公告列暫停時才會播報，方便使用者閱讀。
- Tab 依序聚焦操作按鈕、上一則按鈕、暫停環、下一則按鈕與關閉按鈕。Enter 與空白鍵啟動目前焦點所在的控制項。
- 暫停環是切換按鈕，其名稱會在 `Pause announcements` 與 `Resume announcements` 之間變換。
- 指標位於公告列上方、焦點位於其內部，或視窗最小化時，輪播會暫停。

## 動態效果

- 下一則訊息從下方帶著輕微模糊升起，同時公告列以彈簧調整至適合的高度。
- 暫停環會在每個間隔內逐漸填滿。暫停時停在當前位置，恢復後繼續。
- 倒數計時的數字變化時以下落方式出現。
- 關閉時公告列收合，下方頁面隨之上移。
- 減少動態效果時，輪播停止，訊息與數字改為淡入淡出。關閉時僅淡出。
- 關閉動態效果時，每次變化立即顯示。

## 相關元件

- `Alert`：位於某個區域或表單內，直到問題解決前一直保留的訊息。
- `ToastStack`：使用者剛執行之操作的排隊結果。
- `CookieConsent`：Cookie 與追蹤的同意。
