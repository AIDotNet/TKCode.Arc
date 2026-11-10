---
title: Catch up
summary: 一份「你不在時」的總結：一段文字回顧，帶有人物、地點和時間的要聞、提及與決定，按分組篩選，並可逐條或一次全部標為已讀。
source-hash: dddd2473b6a6
---

<Demo name="Preview" />

## 何時使用

`CatchUp` 迎接度過週末或假期後回來的人。一段簡短的文字回顧用兩三句話說明發生了什麼變化。下面的條目分為要聞（值得知道的事）、提及（有人找他）和決定（某件事已經定下），每條都寫明是誰、在哪裡、什麼時候，並有未讀圓點。篩選器一次顯示一個分組及其未讀數量。打開某條會把它的連結交給應用程式並標為已讀；每條旁邊的小按鈕可以標為已讀或未讀；"Mark all as read" 清空全部，回顧隨之變為 "You are all caught up."

- **回到工作區。** 離開一段時間後看到的第一個畫面，由你的總結服務寫成。
- **每日摘要。** 用同一張卡片展示過去 24 小時。
- **頻道或專案回顧。** 把條目限定在一個地方。

## 何時不用

- **實時通知。** 持續更新的資訊流請使用 `Notification center` 區塊。
- **完整的活動紀錄。** 每個事件都重要時，使用 `Timeline`，而不只是挑出重要的事。
- **單條更新。** 使用 `Toast` 或 `Alert`。

## 用法

本區塊派生自 Avalonia 的 `TemplatedControl`。不設定任何資料時顯示一個虛構的週末（`CatchUpSamples`）。

```xml
<arc:CatchUp Since="Since Friday, 6:10 PM" Summary="{Binding Recap}" Items="{Binding Changes}" />
```

```csharp
catchUp.Items =
[
    new() { Id = "m1", Kind = CatchUpKind.Mention, Title = "Priya asked you to review the pricing page",
            Author = "Priya Raman", Source = "Pricing refresh", Time = "Sat 3:02 PM", Link = "doc:pricing#c12" },
];
catchUp.ItemInvoked += (_, e) => Navigate(e.Item.Link);
catchUp.ItemsRead += (_, e) => store.SaveRead(e.Items.Select(i => i.Id), e.IsRead);
```

### 結構

- **頁頭**：標題、帶未讀數量的 `Since`，以及 "Mark all as read"。
- **回顧**：`Summary` 和旁邊的閃光圖標；全部已讀後，變為帶雙對勾的 "You are all caught up."。
- **篩選器**：All、Highlights、Mentions 和 Decisions，各帶未讀數量（`Filter`，雙向）。
- **條目**：按 `CatchUpItem.Kind` 依次分組；每條顯示作者的生成頭像（或分組圖標）、標題、詳情、地點和時間以及未讀圓點；旁邊的按鈕切換已讀狀態。

已讀狀態按 `CatchUpItem.Id` 保存；條目上的 `IsRead` 設定初始值。`IsRead(item)`、`SetRead(item, value)`、`MarkAllRead()` 和 `Open(item)` 從程式碼完成按鈕所做的事；`UnreadCount` 隨之更新。設定 `MarkReadOnOpen="False"` 時，打開條目不會將其標為已讀。

### 打開條目

<Demo name="Opened" />

### 文字

`Labels`（`CatchUpLabels`）包含標題、分組名稱、篩選器格式、全部已讀和為空時的提示，以及已讀按鈕的讀屏名稱。

## API 參考

<!-- api: CatchUp -->

<!-- api: CatchUpItem -->

<!-- api: CatchUpKind -->

<!-- api: CatchUpFilter -->

<!-- api: CatchUpLabels -->

## 無障礙

- 卡片是一個有名稱的區域；標題是二級標題，每個分組名稱是三級標題。
- 每個條目都是一個按鈕，以標題命名，未讀時加上 "unread"，並以詳情、作者、地點和時間作為說明。**Up** 和 **Down** 在條目之間移動，**Home** 和 **End** 跳到兩端，**Enter** 或 **Space** 打開。
- 每條旁邊的已讀按鈕命名為 "Mark "…" as read" 或 "as unread"。
- 篩選器是可用方向鍵操作的分段控制項；"Mark all as read" 之後會播報 "You are all caught up."。

## 動效

- 回顧以上升方式切換為全部已讀的提示（舊內容 100 ms 淡出，新內容 160 ms 淡入並上升 4 px），高度隨平滑彈簧變化。
- 條目的未讀圓點淡出，文字在 240 ms 內淡到已讀的強度；行在停留時於 160 ms 內變色。
- 減弱動效時回顧只交叉淡化不上升；關閉動效時所有變化立即生效。

## 響應式

- 卡片最寬 600。寬度不超過 440 時（`:compact`）內邊距從 20 收緊到 16，"Mark all as read" 移到標題下方。
- 標題會換行；詳情最多顯示兩行並以省略號結尾；地點和時間以省略號截斷。
- 已讀按鈕始終可見，因此沒有任何功能依賴停留。

## 相關元件

- `Notification center`，用於實時更新的資訊流。
- `Timeline` 和 `Comment thread`，用於完整的歷史紀錄。
- `AI insight card`，用於說明一個指標的變化。
