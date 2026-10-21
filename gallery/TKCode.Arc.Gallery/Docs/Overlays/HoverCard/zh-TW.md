---
title: 懸停卡片
summary: 懸停或聚焦時預覽人員或連結，無需離開頁面。
source-hash: 9342f8a0604b
---

<Demo name="Mention" />

## 何時使用

- 提及、頭像或作者名稱，懸停後展開簡短的個人資料。
- 連結預覽，且點選仍需跳轉到該連結。
- 密集清單中，從一個觸發器移動到下一個時應立即開啟下一張卡片。

## 何時不用

- 純文字標籤：用 `Tooltip`。
- 包含按鈕或輸入框的內容：用 `Popover`。焦點不會進入懸停卡片，因此其內容保持唯讀。
- 使用者自己的帳戶選單：用 `UserMenu`。

## 用法

`HoverCard` 在其 `Trigger` 上短暫懸停或取得鍵盤焦點後開啟。其內容是預覽，通常為 `HoverCardProfile`。

### 個人資料卡片

```xml
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
```

### 連結預覽

`arc:HoverCard.Attach` 可以為任意控制項加上卡片，例如一般的文字連結。

<Demo name="LinkPreview" />

### 時間與寬度

`OpenDelay` 與 `CloseDelay` 預設分別為 500ms 與 140ms。`CardWidth` 設定卡片寬度，卡片始終受視窗可用空間的限制。

```csharp
card.OpenDelay = TimeSpan.FromMilliseconds(300);
card.CardWidth = 320;
```

### 從程式碼中顯示

`Show` 與 `Hide` 直接修改 `IsOpen`。`IsOpen` 是雙向的，`Opened` 與 `Closed` 會回報狀態變化。

```csharp
card.Show();
card.Closed += (_, e) => log.Text = $"關閉方式：{e.Reason}";
```

### 密集清單

一張卡片開啟後，移到下一個觸發器時，下一張卡片幾乎立即開啟，且只有淡入效果。

<Demo name="DenseList" />

## API 參考

<!-- api: HoverCard -->

<!-- api: HoverCardProfile -->

<!-- api: HoverCardStat -->

## 無障礙

- 卡片開啟時，其摘要會寫入觸發器的說明文字中。
- 焦點從不進入卡片。請保持其內容唯讀，並將操作放在觸發器上或 `Popover` 中。
- 透過 Tab 取得焦點會立即開啟卡片，Enter 跳過開啟延遲，按 Escape 關閉。指標停留在觸發器上時保持關閉。
- 在觸控裝置上，點按會切換卡片。同一時間只開啟一張卡片。

## 動態效果

- 卡片從觸發器邊緣生長：以平滑彈簧從縮放 0.96 變為 1，並向觸發器方向移動 4px，同時單獨淡入。
- 個人資料各列從下方 4px 處依序就位。頭像載入完成後從 4px 的模糊中淡入。
- 若另一張卡片在 300ms 之前剛關閉，則下一張卡片在 80ms 後僅以淡入方式開啟。
- 減少動態效果時移除縮放與移動，保留短暫淡入。關閉動態效果時卡片立即顯示或移除。

## 相關元件

- `Tooltip` 用於懸停或聚焦時的簡短文字標籤。
- `Popover` 用於包含按鈕或輸入框、點選開啟的卡片。
- `Avatar` 用於個人資料中顯示的圖片。
