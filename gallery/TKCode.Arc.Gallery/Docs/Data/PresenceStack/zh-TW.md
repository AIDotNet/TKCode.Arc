---
title: Presence stack
summary: 以重疊的頭像顯示此刻正在檢視頁面的人，每人帶有自己的顏色；新來的人在最前面放大出現，離開的人縮小消失，其餘的人收進 +N 標籤。
source-hash: 5cd3b713c88e
---

<Demo name="LiveViewers" />

## 何時使用

`PresenceStack` 顯示此刻還有誰開著這份文件、看板或頁面。每位檢視者是一個頭像，外圈是他自己的顏色（例如他的游標顏色），與下一個頭像重疊，
最近到達的人在最前面。它會跟隨一份即時清單：新來的人在最前面放大出現，並向外送出一圈光環，其餘的人往旁邊滑開；這一排滿了時，最後一個可見的人
會滑進 "+N" 標籤；離開的人縮小消失，空位隨之合攏。暫時離開但仍保持連線的人會變淡。一個滾動的計數說明有多少人在檢視，按一下標籤可開啟完整清單。

- **協作編輯器**：共用文件或設計檔標題列中的頭像。
- **儀表板與工單**：「此刻還有誰在看這個事故」。
- **即時工作階段**：簡報或會議頁面的觀眾。

## 何時不用

- **固定的團隊或負責人清單**：請使用 `AvatarGroup`，它顯示的是成員，而不是誰在線上。
- **數百位檢視者**：改為顯示一個數字；這組頭像是給使用者想要認出的那幾個人用的。
- **每個人確切在哪裡**：文件中的游標與選取範圍醒目提示會說明這一點；這組頭像只說明誰在這裡。

## 用法

依從早到晚的順序加入 `PresenceStackViewer` 項目，也就是把新來的人加在最後，並移除離開的人；頭像組會把最新的人顯示在最前面
（`IsNewestFirst="False"` 會保留清單原本的順序）。

```xml
<arc:PresenceStack MaxVisibleCount="3">
    <arc:PresenceStackViewer Name="Iris Mendel" Detail="Reading" />
    <arc:PresenceStackViewer Name="Jon Kale" IsIdle="True" />
    <arc:PresenceStackViewer Name="Kim Arden" Detail="Editing" />
    <arc:PresenceStackViewer Name="Lou Prentiss" />
</arc:PresenceStack>
```

若是即時清單，請將 `ItemsSource` 繫結到可觀察的集合。同時發生的變化（幾個人一起加入）會一起移動。

```csharp
viewers.Add(new PresenceStackViewer("Ada Brook", detail: "Editing the intro"));
viewers.Remove(leaving);
```

檢視者的 `Name` 提供姓名縮寫、工具提示與協助工具名稱；`Source` 設定一張圖片；`Detail` 加上一句說明（"Editing slide 3"）；`IsIdle` 會讓頭像變淡，
外圈變成灰色。未設定 `Brush` 時，外圈會依名字選取一種圖表系列顏色，因此同一個人在不同工作階段中會保持相同的顏色。

### 大小與數量

`Size` 為 `Small`（28 px 頭像，預設）或 `Medium`（36 px）。`MaxVisibleCount`（預設 4）是顯示標籤前最多顯示的頭像數。`IsCountVisible` 與
`CountLabel` 控制這一排後面的 "4 viewing" 文字；`ViewerCount` 回報人數。

<Demo name="Sizes" />

### 完整清單

標籤會開啟一份列出所有檢視者的清單，附上他們的說明或 "Idle"。`IsListOpen`（雙向）、`OpenList()` 與 `CloseList()` 可在程式碼中控制它；
按 Escape 或按一下外部會關閉它。

## API 參考

<!-- api: PresenceStack -->

<!-- api: PresenceStackViewer -->

## 無障礙

- 頭像組是一份名為 "Viewing now, 4 viewing" 的清單（`Label` 與 `CountLabel`）。這一排是一個 Tab 停駐點：方向鍵在各個人與標籤之間移動，
  Home 與 End 跳到兩端。每個人的名字、離開狀態與說明就是他的協助工具名稱與工具提示。
- 標籤是名為 "2 more viewers" 的按鈕；Enter 或空白鍵開啟清單，Escape 關閉清單並把焦點還回原處。
- 加入與離開會以禮貌的方式朗讀（"Ada Brook joined"、"Ben Ortiz left"）；如果頁面本身已會朗讀這些，請設定 `AnnouncesChanges="False"`。
- 離開狀態會以文字說出，而不只靠變淡表示。

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 有人加入 | 在最前面放大出現，向外送出一圈光環，其餘的人滑開 | 淡入，其餘的人立即移動 | 立即 |
| 有人離開 | 在原位置縮小消失，空位合攏 | 淡出，空位立即合攏 | 立即 |
| 這一排已滿 | 最後一個人滑進標籤並淡出 | 淡出 | 立即 |
| 計數與標籤 | 數字滾動 | 立即改變 | 立即改變 |
| 這一排的寬度 | 彈簧動畫 | 立即 | 立即 |

## 響應式

- 頭像組的寬度等於頭像與計數的寬度；它不會延展。在較窄的標題列中請調低 `MaxVisibleCount`。
- 工具提示需要滑鼠指標；在觸控裝置上，標籤開啟的清單會顯示每個名字與說明。
- 小頭像為 28 px，小於 44 px 的觸控尺寸；每個頭像仍可取得焦點，清單也只要輕觸一下就能開啟。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `AvatarGroup` | 顯示團隊成員，而不是此刻誰在這裡 |
| `Avatar` | 顯示一個人 |
| `LiveVisitors` | 在地圖上統計匿名訪客 |
