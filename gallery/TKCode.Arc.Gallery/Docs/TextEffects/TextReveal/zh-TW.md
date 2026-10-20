---
title: 文字呈現
summary: 以克制的動態效果呈現一小段內容。
source-hash: 925fb0e5d554
---

## 何時使用

- 首屏大標題或登陸頁開場，需要在出現時播放一次。
- 位於標題下方的簡短引言，逐字呈現。
- 即使指令碼載入緩慢，也必須從第一格起就可讀的標題。

## 何時不用

- 頁面較下方、需要隨捲動進入可視範圍時呈現的分節標題：用 `InViewTitle`。
- 長篇內文：逐字效果僅適用於標題與短段落。
- 持續進行的狀態列：用 `TextShimmer`。

## 用法

控制項只播放一次進場效果。文字中的 `\n` 會刻意換行；各行經過平衡處理，且寬度不超過 24 個字元。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextReveal Text="從第一格起就顯得周到的介面"
                    Level="H1" />
</UserControl>
```

### 標題層級與段落

`Level` 設定標題層級（`H1`、`H2`、`H3`）或 `Paragraph`。標題使用展示字級與更緊湊的行距；段落使用內文字級與更柔和的模糊。對於標題，`Level` 也會設定 `AutomationProperties.HeadingLevel`。

```xml
<arc:TextReveal Text="悄悄打磨細節。&#10;讓動態效果保持真實。"
                Level="H2"
                Delay="0:0:0.2" />

<arc:TextReveal Text="段落以更柔和的模糊逐字呈現。"
                Level="Paragraph" />
```

`Delay` 會在第一個字詞升起前等待。之後的錯落間隔維持在半秒以內，因此長文字不會拉長進場時間。

### 重播

`Replay()` 會再次播放進場效果。請在程式碼中呼叫，例如在按鈕點選時。

```csharp
var hero = new TextReveal { Text = "從第一格起就顯得周到的介面", Level = TextLevel.H1 };
var replay = new Button { Content = "重播", Variant = ButtonVariant.Secondary };
replay.Click += (_, _) => hero.Replay();
```

### 首次配置前的進場

掛載在首屏以下的控制項會保持隱藏，直到第一次進入可視範圍才開始等待。掛載時已在螢幕上的控制項則從第一格開始播放。進場效果執行期間 `IsRevealing` 為 `true`。

### 字距、行距與模糊

`Tracking` 以 em 為單位設定字元間距，`Leading` 以字級的倍數設定行高，`BlurRadius` 設定每個字詞的起始模糊半徑。層級主題已設定這些值；僅當標題需要不同的節奏時才覆寫它們。

```xml
<arc:TextReveal Text="預設保持安靜" Level="H2" Tracking="-0.02" Leading="1.2" />
```

## API 參考

<!-- api: TextReveal -->

## 無障礙

- 標題以完整文字作為名稱，並將 `HeadingLevel` 設為 1 至 3。段落則為純文字。
- 強制換行會被讀作空格。
- 控制項本身不播報變化。需要播報文字時，請在父元素上使用即時區域。
- 本控制項無法取得焦點，也沒有鍵盤互動。
- 請依頁面大綱選擇層級，而不是依想要的字級。

## 動態效果

- 每個字詞從各自的裁剪區中升起，同時從柔和的模糊中逐漸清晰。字詞依序開始。
- 整個進場效果在控制項出現於螢幕上時播放一次，只有呼叫 `Replay()` 時才會重播。
- 減少動態效果時保留字詞順序，但去掉上升與裁剪；字詞快速淡入。
- 關閉動態效果時立即顯示最終文字。

## 相關元件

- `InViewTitle`：隨捲動進入可視範圍而呈現的分節標題。
- `TextShimmer`：顯示持續工作的狀態列。
- `TextMorph`：簡短標籤逐字母切換狀態。
