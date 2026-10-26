---
title: 移動端對話框
summary: 置中的提示或確認對話框，帶標題、訊息和最多三個操作，位於遮罩之上。
source-hash: 377004849ec9
---

<Demo name="Basic" />

## 何時使用

`Dialog` 讓使用者停下來做一個不能等的決定：確認刪除、允許或拒絕權限、在幾種繼續方式中選一種。

- **確認**：在無法復原的操作之前，把破壞性操作標成危險色。
- **提示**：必須先讀完才能繼續的資訊。
- **簡短的選擇**：兩三個操作，沒有其他內容。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋這裡沒有驗證。

## 何時不用

- **選項清單或表單**：用 `Sheet`，它從底部升起，拇指搆得到。
- **不需要回答的訊息**：用 `Toast`。
- **使用者事後可能想復原的事**：直接去做，並在 Toast 裡提供復原，而不是先詢問。

## 用法

把對話框放進頁面的 `Overlay` 槽，用 `IsOpen` 開啟。操作按鈕就是對話框的子項，請使用 `Button`。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Dialog Title="Delete this photo?"
                     Message="This cannot be undone."
                     IsOpen="{Binding ConfirmDelete}">
            <arcm:Button Content="Cancel" Variant="Tinted" />
            <arcm:Button Content="Delete" arcm:Dialog.IsDestructive="True" />
        </arcm:Dialog>
    </arcm:Page.Overlay>
</arcm:Page>
```

`IsOpen` 雙向繫結。按下操作按鈕會先執行它自己的 `Click` 處理或 `Command`，然後關閉對話框；設定 `CloseOnAction="False"` 可以自己決定何時關閉，例如請求結束之後。

兩個操作在每個都放得進半寬時同排顯示；三個操作，或標籤太寬放不下一排時，按全寬縱向堆疊。面板最寬 320，四周至少留出 24。

預設只有操作能關閉對話框。設定 `IsDismissible="True"` 後，點遮罩和按 Esc 也能關閉，適合有無害退出方式的對話框。

在按鈕上設定 `Dialog.IsDestructive="True"` 會用危險色繪製它：實心按鈕變成危險色膠囊，淡色按鈕是淺底上的危險色文字，純文字按鈕是危險色文字。

### 同一頁面上的多個浮層

`Page.Overlay` 只能放一個元素。要讓對話框與底部面板或 Toast 同時存在，把它們放進該槽裡的一個 `Grid`，Toast 放在最後，這樣它顯示在其他浮層之上，示範裡的兩個對話框就是這樣放的。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <Grid>
            <arcm:Sheet Title="Share" />
            <arcm:Dialog Title="Delete?" />
            <arcm:Toast />
        </Grid>
    </arcm:Page.Overlay>
</arcm:Page>
```

每個浮層在關閉時都不攔截觸控。

### Token

`DialogToken` 包含 `ScrimColor`（遮罩的完全不透明顏色，亮色和暗色不同）、`PanelMaxWidth`（320）、`PanelMargin`（24）和 `PanelPadding`。顏色、圓角和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileDialog -->

## 無障礙

Esc 會關閉可取消的對話框。開啟時焦點移到對話框上。給它一個把決定說成問句的 `Title`，操作用動詞命名（「刪除」「取消」），不要用「是」和「否」。按鈕至少 44 乘 44。不能被取消的對話框必須有退路：包含一個取消操作。

## 動效

遮罩淡入的同時，面板從 0.94 放大並淡入，用一根靈敏的彈簧，由同一個值驅動。關閉更快：0.16 秒，走退出曲線，沒有過衝；在對話框還在出現時關閉，它會從目前位置反向運動。它由程式碼驅動，因為對話框必須在退出動畫結束後才停止攔截觸控，而主題過渡無法發出這個訊號。減弱動效時，面板只淡入淡出，不縮放，用時 0.12 秒；關閉動效時，它立即出現和消失。

## 相關元件

- `MobilePage`：`Overlay` 槽承載對話框。
- `MobileButton`：對話框的操作，也是開啟它的常用方式。
- `MobileSheet`：用於選項和表單。
- `MobileToast`：用於不需要回答的訊息。
