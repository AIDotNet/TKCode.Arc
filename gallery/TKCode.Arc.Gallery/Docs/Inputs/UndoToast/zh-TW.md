---
title: Undo toast
summary: 用於可以撤回的操作的提示，帶有倒數圓環和復原按鈕；指標停在上面或焦點在其中時，倒數會暫停。
source-hash: a86c35284a6c
---

<Demo name="Preview" />

## 何時使用

`UndoToast` 讓操作立即生效，同時在幾秒內保留撤回的機會。它顯示剛剛發生了什麼（「Conversation archived」）、一個倒數剩餘秒數的圓環和一個復原按鈕。指標停在提示上或焦點在其中時，倒數暫停，之後從停下的地方繼續，所以沒有人會在伸手去按按鈕時錯過機會。提示開啟時，系統的復原快速鍵也能用。倒數結束或提示被關閉後，操作保留。

- **封存、移動或標記訊息**：項目立即離開清單，復原後再回來。
- **從看板、播放清單或購物車中移除一項**：做起來快，撤回也快。
- **容易還原的批次修改**：「12 messages marked as read」。

## 何時不用

- **操作無法還原**：永久刪除或傳送給別人；先用 `ConfirmPopover` 或 `Dialog` 詢問。
- **只是確認，沒有可以撤回的東西**：用 `Toast`。
- **同時有多則通知**：一個復原提示只顯示最近的操作；通知佇列用 `ToastStack`。

## 用法

在版面中放置提示的位置（通常是視窗底部）放一個 `UndoToast`，操作後呼叫 `ShowAsync`。使用者復原時它回傳 true。

```xml
<arc:UndoToast x:Name="Toast" HorizontalAlignment="Center" VerticalAlignment="Bottom" />
```

```csharp
inbox.Remove(thread);
if (await Toast.ShowAsync("Conversation archived"))
{
    inbox.Insert(index, thread);
}
else
{
    await api.ArchiveAsync(thread.Id);
}
```

`UndoToast` 位於 Special 套件中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。

提示開啟時顯示新的操作，會取代之前的操作，之前的操作保留（它的工作回傳 false，`Closed` 回報 `Replaced`）；提示留在原位，只有文字和倒數重新開始。不用 `ShowAsync` 時，呼叫 `Show` 並處理 `Undone` 或 `Closed`，或者繫結 `UndoCommand`。

### 倒數

`Duration`（預設 5 秒）是可以復原的時間，不包括指標停在上面或焦點在其中的時間。圓環按順時針方向變空，並顯示剩餘的整秒數。`IsCountdownVisible="False"` 隱藏圓環，讓提示更安靜；倒數照常進行。

<Demo name="Countdown" />

### 關閉原因

| `UndoToastCloseReason` | 何時 | 操作 |
|---|---|---|
| `Undone` | 復原按鈕、復原快速鍵或 `Undo()` | 被撤回 |
| `TimedOut` | 倒數結束 | 保留 |
| `Dismissed` | 關閉按鈕、Escape、`Dismiss()` 或 `IsOpen = false` | 保留 |
| `Replaced` | 開啟時用 `Show` 顯示新的操作 | 保留 |

### 復原快速鍵

提示開啟時，系統的復原快速鍵（Ctrl+Z，macOS 上為 Cmd+Z）會復原操作，除非焦點在需要自己使用這個快速鍵的文字方塊中。如果頁面有自己的復原功能，把 `IsShortcutEnabled` 設為 false。

### 結構

1. **倒數**（`IsCountdownVisible`）：28px 的圓環，顯示剩餘秒數。
2. **訊息**（`Message`）：發生了什麼，最多兩行。
3. **復原**（`UndoText`）：強調色的小型幽靈按鈕。
4. **關閉**：圓形按鈕，關閉提示並保留操作。

### 文案建議

用過去式說明發生了什麼，對象放在前面。

| 不要寫 | 改為 |
|---|---|
| Success! Your conversation has been archived | Conversation archived |
| Click here to undo | Undo |
| Items were moved | 3 files moved to Archive |

## API 參考

<!-- api: UndoToast -->

<!-- api: UndoToastClosedEventArgs -->

<!-- api: UndoToastLabels -->

| 型別 | 值 |
|---|---|
| `UndoToastCloseReason` | `Undone`、`TimedOut`、`Dismissed`、`Replaced` |

## 無障礙

- 提示是以訊息命名的禮貌型即時區域，出現時會被讀出；它的說明文字指出復原快速鍵（「Press Ctrl+Z to undo」）。
- 復原按鈕的名稱包含訊息（「Undo: Conversation archived」）；關閉按鈕名為「Dismiss」。
- 焦點在提示中時倒數暫停，鍵盤使用者有足夠的時間。
- 圓環只是裝飾；剩餘時間不會每秒讀出一次。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| `Tab` | 先到復原，再到關閉 |
| `Enter` / `Space` | 按下有焦點的按鈕 |
| `Escape`（焦點在提示中） | 關閉提示，操作保留 |
| `Ctrl` + `Z` / `Cmd` + `Z` | 提示開啟時復原操作 |

## 動效

- 提示從 96% 大小、下方 16px 處以變形彈簧升起，並在 240ms 內淡入；離開時下沉 8px、縮到 97%，在 180ms 內淡出。
- 倒數進行時，圓環每格畫面變空；倒數暫停時，圓環變為一半不透明度。
- 新的操作讓提示留在原位：只有文字和圓環重新開始。
- 減少動態效果時提示只淡入淡出（120ms）；關閉動態效果時立即出現和離開。倒數在所有動態效果等級下都會進行。

## 響應式

- 提示最窄 280px、最寬 416px；可用寬度更小時取可用寬度。
- 訊息最多換成兩行，之後以省略號結尾。
- 關閉的提示不占空間。

## 相關元件

- `Toast` 和 `ToastStack`：不需要復原的通知。
- `ConfirmPopover`：無法還原的操作。

| 元件 | 適用情境 |
|---|---|
| `UndoToast` | 操作立即生效，並能在幾秒內撤回 |
| `ConfirmPopover` | 操作無法還原，需要再按一次 |
| `Toast` | 只需確認某件事已經發生 |
