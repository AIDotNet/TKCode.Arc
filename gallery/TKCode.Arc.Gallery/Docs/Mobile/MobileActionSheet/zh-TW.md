---
title: 移動端操作面板
summary: 從底部升起的操作清單，可有一個破壞性操作列和單獨的取消列，選擇一列、點遮罩或下拉即可關閉。
source-hash: e3b6387486bf
---

<Demo name="Basic" />

## 何時使用

`ActionSheet` 是移動端對選單的回答：圍繞同一個物件的一小組操作，從底部邊緣升起。

- **對某個項目的操作**：分享、加入相簿、刪除。
- **需要退路的選擇**：取消列始終在清單下方、與清單分開，拇指容易搆到。
- **破壞性操作**：既要容易找到、又不能輕易誤觸，把該列標記為 `IsDestructive`。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋和拖曳這裡沒有驗證。

## 何時不用

- **超過大約六個操作**：用帶 `ListGroup` 的 `Sheet`，它可以捲動和分組。
- **表單或有多個步驟的任務**：用 `Sheet`。
- **對單個操作的確認**：用對話框。

## 用法

把操作面板放進頁面的 `Overlay` 槽，用 `IsOpen` 開啟。它的內容是一列 `ActionSheetItem` 列。給每一列設定 `Command`，或者在整個面板上處理一次 `ActionInvoked`；選擇一列後面板會關閉。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:ActionSheet Title="照片" Message="IMG_2041.jpg" IsOpen="{Binding ShowOptions}">
            <arcm:ActionSheetItem Content="分享" Command="{Binding ShareCommand}" />
            <arcm:ActionSheetItem Content="加入相簿" />
            <arcm:ActionSheetItem Content="刪除照片" IsDestructive="True" Command="{Binding DeleteCommand}" />
        </arcm:ActionSheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

```csharp
void OnAction(object? sender, ActionSheetActionEventArgs e) => Log((string)e.Action.Content!);
```

`Title` 和 `Message` 是可選的，位於第一列上方。`CancelText` 是取消列的文字（`"Cancel"`）；設為 null 會隱藏該列。取消會關閉面板，但不觸發 `ActionInvoked`。操作面板就是一個 `Sheet`：點遮罩、下拉或按 Esc 都能關閉它（設定 `IsDismissible="False"` 可禁止），`SafeAreaBottom` 讓取消列避開 Home 指示條（見 `MobileSheet`）。

### Token

`ActionSheetToken` 保存 `PanelPadding`（兩張卡片周圍的空間）、`CancelMargin`（取消卡片上方的間隔）和 `HeaderPadding`；`ActionSheetItemToken` 保存 `RowHeight`（56）和 `ContentPadding`。顏色、圓角和字型來自 ArcDesign token：操作用強調色，破壞性操作用危險色。

## API 參考

<!-- api: MobileActionSheet -->

## 無障礙

每一列都是按鈕，高度至少 56 px，容易點中，也可以用 Tab 和 Enter 或空白鍵操作。Esc 會關閉可關閉的操作面板，開啟時焦點會移到面板上。破壞性的列有顏色，但也靠文字說明，所以不要只依賴顏色：寫「刪除照片」，而不是「刪除」。點遮罩只是捷徑，取消列才是看得見的退路。

## 動效

面板的升起和離開使用與 `MobileSheet` 相同的程式碼驅動運動值：拖曳時每次移動都會寫入位置，放開時手指的速度交給彈簧（開啟 0.4 秒平滑，關閉 0.3 秒），遮罩每幀跟隨位置；主題裡宣告的過渡跟不上手指。這些列是清單列，不是浮在表面上的按鈕，所以不會縮放：手指按下時，背景以一次短淡入變深。減弱動效時，面板直接跳到位置，按下的底色也立即改變。

## 相關元件

- `MobileSheet`：它所基於的面板，用於不是操作清單的內容。
- `MobilePage`：`Overlay` 槽承載操作面板。
- `MobileButton`：開啟它的常用方式。
