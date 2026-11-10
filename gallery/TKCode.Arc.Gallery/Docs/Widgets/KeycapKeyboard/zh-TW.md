---
title: Keycap keyboard
summary: 由鍵帽組成的鍵盤：點亮一個快速鍵，按住修飾鍵時顯示各鍵的作用，打字或點擊時按鍵會按下。
source-hash: 9289b98555f8
---

<Demo name="Preview" />

## 何時使用

`KeycapKeyboard` 繪製一個緊湊的鍵盤，鍵帽看起來像真的一樣。它可以用強調色點亮一個快速鍵，也可以在鍵上寫出每個鍵的作用：按住 Ctrl，帶 Ctrl 快速鍵的鍵就顯示它們的動作。真實鍵盤按下時對應的鍵會跟著按下，鍵也可以點擊；點擊的修飾鍵會一直保持按住，直到下一個鍵。

- **快速鍵說明**：「鍵盤快速鍵」面板，人們按住修飾鍵來查看。
- **新手引導與提示**：展示完成某件事的那一個組合鍵。
- **按鍵測試與打字練習**：打字時看著鍵亮起。

## 何時不用

- **輸入文字**：它不是用來輸入的螢幕鍵盤；請用系統內建的。
- **很長的快速鍵清單**：可搜尋的表格更好讀。
- **美式鍵盤以外的配置**：按鍵的位置和標籤都按美式配置。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:KeycapKeyboard Label="Editor shortcuts">
        <arc:KeycapBinding Gesture="Ctrl+C" Label="Copy" />
        <arc:KeycapBinding Gesture="Ctrl+V" Label="Paste" />
        <arc:KeycapBinding Gesture="Ctrl+Shift+P" Label="Run" />
    </arc:KeycapKeyboard>
</UserControl>
```

`KeycapKeyboard` 在 Special 套件中；請在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。在裡面放入 `KeycapBinding`：當按住的修飾鍵（`ActiveModifiers`）正好是某個快速鍵的修飾鍵時，它的鍵就顯示它的標籤。沒有修飾鍵的快速鍵在什麼都沒按住時顯示。每點擊一個鍵都會觸發 `KeyInvoked`；追蹤按鍵時，每次真實按鍵也會觸發。它帶有這個鍵、按住的修飾鍵以及它們組成的快速鍵。

### 一個快速鍵

`Shortcut` 點亮一個組合，寫法可以是「Ctrl+Shift+P」「Cmd+K」，也可以用 Mac 符號「⇧⌘P」。`Platform` 讓修飾鍵印上 Windows 或 Mac 的標籤（Auto 跟隨系統），`ShowsFunctionRow` 加上 Esc 和 F1 到 F12，`KeySize` 設定最大的鍵。

<Demo name="Shortcut" />

### 跟隨真實鍵盤

`Tracking` 決定哪些按鍵會讓鍵亮起：不追蹤、鍵盤取得焦點時的按鍵（預設），或視窗中的所有按鍵。視窗追蹤只是觀察；按鍵仍會送到擁有焦點的控制項。

<Demo name="Typing" />

### 最佳實踐

**建議**

- 快速鍵標籤只寫一個短詞：一個鍵大約能放八個字母。
- 快速鍵針對某個系統時，設定 `Platform`。
- 用 `Label` 給鍵盤命名，比如「Editor shortcuts」。

**避免**

- 在有密碼的介面上使用視窗追蹤：鍵會顯示輸入的內容。
- 同時點亮多個快速鍵；請改用快速鍵清單。

## API 參考

<!-- api: KeycapKeyboard -->

<!-- api: KeycapBinding -->

## 無障礙

- 鍵盤是一個群組，名稱來自 `Label`。它的說明文字會讀出點亮的快速鍵（「Shortcut Ctrl+Shift+P.」）和目前顯示的快速鍵（「Ctrl+C: Copy.」）。
- 它是一個 Tab 停駐點。預設追蹤方式下，它取得焦點時真實按鍵會按下對應的鍵並觸發 `KeyInvoked`；Tab 仍會把焦點移走。
- 指標取得焦點時不顯示焦點環；鍵盤取得焦點時在整個鍵盤周圍顯示焦點環。

### 鍵盤互動

| 按鍵 | 作用 |
|---|---|
| Tab | 把焦點移到鍵盤上，再移走。 |
| 任意鍵 | 在鍵盤上按下這個鍵（預設 `Tracking`）。 |
| Ctrl、Shift、Alt、Win 或 ⌘ | 按住時顯示這些修飾鍵的快速鍵。 |

## 動效

- 鍵按下時陷入鍵帽側面，放開時升起，大約 120 ms。
- 一個影格迴圈驅動所有的鍵，所有鍵停下後立即結束。
- 鍵和標籤由鍵盤自己繪製；每個影格不配置記憶體。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 按鍵 | 陷下再升起 | 直接按下再彈起 | 直接按下再彈起 |
| 按住修飾鍵 | 顯示它的快速鍵 | 顯示它的快速鍵 | 顯示它的快速鍵 |

## 響應式

- 鍵盤寬 15 個鍵位。空間足夠時鍵使用 `KeySize`（44 px），否則縮小以適應給定的寬度，所以在手機上也完整；標籤隨之縮小。
- 觸控時不顯示懸停效果；點按即點擊這個鍵。

## 相關元件

- `ShortcutRecorder` 記錄人們按下的快速鍵。
- `CommandPalette` 按名稱尋找命令，並顯示它們的快速鍵。

| 元件 | 適用場景 |
|---|---|
| `KeycapKeyboard` | 展示快速鍵所在的位置，或讓人查看 |
| `ShortcutRecorder` | 讓人設定自己的快速鍵 |
| `CommandPalette` | 按名稱尋找命令 |
