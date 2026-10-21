---
title: 可視範圍內標題
summary: 標題隨捲動進入可視範圍時逐步呈現。
source-hash: 65cd1e721b3b
---

<Demo name="Variants" />

## 何時使用

- 長篇功能頁面中的分節標題，需要共用同一種進場效果。
- 每次捲回可視範圍時都應重新播放的標題，使用 `IsReplayEnabled`。
- 帶有明確換行、在窄螢幕上不能重新排版的標題。

## 何時不用

- 頁面載入時播放的首個大標題：用 `TextReveal`。
- 持續進行的狀態文字，例如「正在產生摘要」：用 `TextShimmer`。
- 內文或長段落：逐字效果僅適用於標題。

## 用法

標題可見 45% 時開始呈現。預設使用 `Blur` 變體。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InViewTitle Text="模糊效果逐字變清晰"
                     Variant="Blur" />
</UserControl>
```

### 變體

`Variant` 選擇呈現樣式。每種樣式都先淡入不透明度，再清除模糊，最後才讓位移穩定下來，因此標題在完全到位之前就已可讀。

- `Word` 讓每個字詞從各自的裁剪區中升起。
- `Line` 讓每一行從裁剪區中升起，行與行之間間隔 80 毫秒。它使用 `Lines`。
- `Blur` 在字詞上升的同時，從模糊中逐字變清晰。這是預設值。
- `Tracking` 在每個字詞清除柔和模糊的同時，展開字距。
- `Wipe` 以柔和的邊緣從左到右揭開標題。

```xml
<arc:InViewTitle Text="文字從裁剪區中升起" Variant="Word" />
<arc:InViewTitle Text="柔和的擦除揭開標題" Variant="Wipe" />
```

### 明確分行

`Lines` 為 `Line` 變體設定各行。各行依給定內容顯示，不會重新排版。每一行都必須能在窄螢幕上放得下。未設定 `Lines` 時，整個 `Text` 為一行。

```csharp
title.Variant = InViewTitleVariant.Line;
title.Lines = ["逐行升起，", "不再重新排版"];
```

### 每次進入都重播

`IsReplayEnabled` 會在標題離開可視範圍時以快速淡出隱藏它，且沒有錯落效果。每次標題再次進入時都會重新播放。預設值為 `false`，因此標題只播放一次。

```xml
<arc:InViewTitle Text="每次進入都會重播"
                 IsReplayEnabled="True"
                 Level="H3" />
```

### 標題層級

`Level` 設定標題層級（`H1`、`H2` 或 `H3`），並同步設定對應的 `AutomationProperties.HeadingLevel`。預設為 `H2`。請依頁面大綱選擇層級。

## API 參考

<!-- api: InViewTitle -->

## 無障礙

- 標題以其完整文字作為名稱，並由 `Level` 設定 `HeadingLevel`。
- 動畫部分對輔助技術隱藏，只朗讀文字。
- 頁面開啟時已捲動越過的標題會立即顯示最終狀態。
- 本控制項無法取得焦點，也沒有鍵盤互動。
- 需要播報變化時，請在父元素上使用即時區域。

## 動態效果

- 標題可見 45% 時開始呈現。不透明度先行，模糊隨後清除，位移最後穩定。
- `Word` 與 `Line` 從裁剪區中升起，`Wipe` 從左到右揭開，`Tracking` 展開字距，`Blur` 逐字變清晰。
- 啟用 `IsReplayEnabled` 後，離開可視範圍是快速淡出，沒有錯落效果。
- 減少動態效果時標題立即顯示，沒有位移、模糊或遮罩。關閉動態效果時同樣如此。

## 相關元件

- `TextReveal`：頁面載入時只播放一次的大標題。
- `TextShimmer`：表示正在進行工作的狀態文字。
