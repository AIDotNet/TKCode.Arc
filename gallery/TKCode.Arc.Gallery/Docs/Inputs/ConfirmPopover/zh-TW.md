---
title: Confirm popover
summary: 在發起操作的按鈕旁彈出一個小問題，用於刪除這類操作；焦點先落在取消上，Escape 退出，操作也可以在關閉前先完成。
source-hash: 45442a9aefcf
---

<Demo name="Preview" />

## 何時使用

`ConfirmPopover` 在使用者操作的位置提出一個簡短的問題：按下垃圾桶按鈕後，旁邊彈出「刪除這些筆記？」，帶有取消和操作兩個按鈕。頁面保持可見，操作需要再按一次才會執行，焦點一開始落在取消上，所以誤按 Enter 不會造成損失。Escape 或在外面按下都會退出，焦點回到開啟它的按鈕。如果操作需要一點時間，確認按鈕可以先執行工作，並在完成前顯示載入動畫。

- **刪除清單或表格列中的一項**：一則留言、一個檔案、一個儲存的檢視、一個 API 金鑰。
- **放棄未儲存的內容**：在草稿自己的「放棄」按鈕上確認。
- **無法反悔的切換**：撤銷存取權、結束工作階段、向讀者發布。

## 何時不用

- **操作可以復原**：直接執行，再提供 `UndoToast`；一次詢問會拖慢每個人，只為保護少數誤操作的人。
- **決定需要不只一句話**：列出會失去的內容、輸入名稱來確認，或者除了是和否還有別的選擇，這些都應該放進 `Dialog`。
- **一次處理很多項**：批次刪除容易判斷失誤；用寫明數量的 `Dialog`。

## 用法

把提問的按鈕放進 `Trigger`，在 `Title` 中寫下問題，並處理 `Confirmed`。

```xml
<arc:ConfirmPopover Title="Delete this comment?" ConfirmText="Delete">
    <arc:ConfirmPopover.Trigger>
        <arc:Button Variant="Ghost" Content="Delete" />
    </arc:ConfirmPopover.Trigger>
</arc:ConfirmPopover>
```

`ConfirmPopover` 位於 Special 套件中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。

問題在一個 `Popover` 中開啟，因此位置設定相同（`Placement`、`Alignment`），放不下時會翻到另一側，動態效果也一樣。確認後觸發 `Confirmed`，以其他方式關閉時觸發 `Cancelled`，並帶有 `Reason`（取消按鈕、Escape 或在外面按下）。`Command` 在 `Confirmed` 之後執行。

### 附加到按鈕

`arc:ConfirmPopover.Attach` 為已有的按鈕加上一個問題，點擊按鈕即可開啟。

<Demo name="Attached" />

對於不會刪除任何東西的操作，把 `IsDestructive` 設為 false：確認按鈕和標記使用強調色，而不是危險色。當退出是少見的情況時，`InitialFocus="Confirm"` 讓焦點先落在操作按鈕上。`Icon` 可以替換警告三角形，設為 null 時隱藏標記。

### 執行工作

確認時會執行 `ConfirmHandler`。在它完成前，確認按鈕顯示載入動畫，取消按鈕無法使用；回傳 true 時作為已確認關閉，回傳 false 或擲回例外時保持開啟，方便再試一次。

<Demo name="Async" />

```csharp
confirm.ConfirmHandler = async () =>
{
    var response = await api.RevokeKeyAsync(keyId);
    return response.IsSuccessStatusCode;
};
```

### 從程式碼呼叫

`ConfirmAsync` 開啟問題並在關閉時給出答案，所以命令可以先問再做。

```csharp
if (await confirm.ConfirmAsync(rowButton))
{
    await DeleteAsync(row);
}
```

### 結構

1. **觸發按鈕**（`Trigger`）：提問的按鈕；它留在原位，按下時只改變顏色。
2. **標記**（`Icon`）：32px 的圓形，內含 16px 的圖示，使用危險色或強調色。
3. **問題**（`Title`）與**說明**（`Message`）：先是中等字重，再是次要文字；兩者都在 272px 內換行。
4. **按鈕**：取消（`CancelText`）和操作（`ConfirmText`），小尺寸，靠右對齊，間距 8px。

### 文案建議

針對具體的對象提問，並在按鈕上寫出操作名稱。

| 不要寫 | 改為 |
|---|---|
| Are you sure? | Delete these notes? |
| OK | Delete |
| This action cannot be undone. Are you really sure you want to continue? | They are removed for everyone. |

## API 參考

<!-- api: ConfirmPopover -->

<!-- api: ConfirmPopoverCancelledEventArgs -->

| 型別 | 值 |
|---|---|
| `ConfirmPopoverFocus` | `Cancel`（預設）、`Confirm` |

## 無障礙

- 問題開啟時焦點落在取消上（設定 `InitialFocus="Confirm"` 時落在操作上）；按鍵開啟後顯示焦點框，點擊開啟後不顯示。
- Tab 和 Shift+Tab 在問題內的兩個按鈕之間循環；左右方向鍵也可以在兩者之間移動。
- Escape 關閉問題，焦點回到觸發按鈕並顯示焦點框；在外面按下也會關閉，焦點回到觸發按鈕但不顯示焦點框。
- 彈出面板以問題命名，操作按鈕的說明文字也會重複問題，所以「Delete」會與「Delete these notes?」一起讀出。
- 觸發按鈕會回報問題是否開啟（展開或收合）。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| 在觸發按鈕上按 `Enter` / `Space` | 開啟問題 |
| `Tab` / `Shift` + `Tab` | 在取消和操作之間移動 |
| `Left` / `Right` | 在取消和操作之間移動 |
| `Enter` / `Space` | 按下有焦點的按鈕 |
| `Escape` | 取消並回到觸發按鈕 |

## 動效

- 問題的開啟和關閉使用 `Popover` 的動態效果：從朝向觸發按鈕 5px、97% 大小的位置出發，以彈簧動畫落定，大約 160ms 離開。
- `ConfirmHandler` 執行時，確認按鈕的文字淡出，換成載入動畫。
- 減少動態效果時問題只淡入淡出；關閉動態效果時立即出現和離開。

## 響應式

- 問題在彈出面板內寬 272px，較長的文字會換行。
- 靠近邊緣時翻到觸發按鈕的另一側，並保持在視窗內。

## 相關元件

- `Dialog`：需要更多空間或需要阻擋頁面的決定。
- `UndoToast`：最好直接執行、事後復原的操作。
- `Popover`：其他小型互動面板。

| 元件 | 適用情境 |
|---|---|
| `ConfirmPopover` | 一個會造成損失的操作需要再明確按一次 |
| `UndoToast` | 操作在幾秒內可以復原 |
| `Dialog` | 決定需要詳細說明、輸入或兩個以上的選擇 |
