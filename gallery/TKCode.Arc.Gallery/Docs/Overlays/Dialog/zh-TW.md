---
title: 對話方塊
summary: 為需要使用者關注的決定提供專注的浮層面板。
source-hash: a7de713d0fa6
---

## 何時使用

- 必須在使用者繼續前確認的操作，例如刪除專案。
- 能放進一個面板的簡短表單，例如重新命名或邀請。
- 分步流程，且每一步的標題與說明不同。

## 何時不用

- 篇幅較長的表單或紀錄詳細資料：用 `Drawer`。
- 以行動裝置為主、需停靠在數個高度的任務：用 `BottomSheet`。
- 不應阻擋頁面的輕量內容：用 `Popover`。
- 指令清單：用 `DropdownMenu`。

## 用法

在觸發器旁宣告 `Dialog`。它本身不轉譯任何內容，在 `IsOpen` 為 true 時顯示面板。

### 在 XAML 中宣告

將主體內容放在內容區，按鈕放在 `Actions` 中。帶有 `arc:Dialog.Result` 的按鈕會以該值關閉對話方塊。

```xml
<arc:Dialog Title="重新命名專案"
            Description="請選擇一個團隊成員能快速辨識的簡短名稱。"
            IsOpen="{Binding IsRenaming}">
    <arc:Input Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="取消" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="儲存" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### 從程式碼中確認

`Dialog.ConfirmAsync` 會建立對話方塊、等待選擇，並且只有按下確認按鈕時回傳 `true`。按 Escape、點選遮罩或關閉按鈕都回傳 `false`。

```csharp
var confirmed = await Dialog.ConfirmAsync(deleteButton, "刪除專案？",
    "此操作將永久刪除 Atlas 改版專案，且無法復原。", "刪除", isDanger: true);
status.Text = confirmed ? "已刪除" : "已保留";
```

`Dialog.ShowAsync` 接受自訂內容與一組 `DialogAction`，並回傳被按下之操作的結果。

### 步驟標題

對話方塊開啟期間可以修改 `Title` 與 `Description`。新文案會在原位淡入替換，因此分步流程不需要第二個對話方塊。

```csharp
dialog.Title = "選擇角色";
dialog.Description = "編輯者可以修改內容；檢視者只能閱讀。";
```

### 尺寸

`Size` 決定面板寬度：`Small` 為 360，`Medium` 為 440（Arc 預設值），`Large` 為 560。面板寬度永遠不會超過視窗寬度減去 16px 的邊距。

```xml
<arc:Dialog Title="工作區限制" Size="Large" IsOpen="{Binding ShowLimits}">
    <TextBlock Text="限制依工作區分別套用。" />
</arc:Dialog>
```

### 關閉與結果

使用者關閉之前會觸發 `Closing`，該事件可以被取消。`Closed` 會回報關閉原因與結果。

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Reason == DismissReason.PressOutside && isSaving)
    {
        e.Cancel = true;
    }
};
dialog.Closed += (_, e) => status.Text = e.Result is true ? "已儲存" : "已關閉，方式：" + e.Reason;
```

## API 參考

<!-- api: Dialog -->

## 無障礙

- 面板名稱取自 `Title`，說明文字取自 `Description`。
- 關閉按鈕預設名稱為「關閉對話方塊」，可透過 `CloseButtonName` 修改。
- 對話方塊開啟時焦點移入其中，Tab 與 Shift+Tab 均停留在對話方塊內。
- 按 Escape 關閉對話方塊並將焦點返回給開啟它的控制項；除非 `CloseOnEscape` 為 false。

## 動態效果

- 遮罩淡入，面板以平滑彈簧從縮放 0.96 上升 8px。關閉動畫較短，且從進場的當前狀態開始。
- 標題或說明變化時，帶著柔和的模糊升起。
- 減少動態效果時僅保留短暫的不透明度淡入；關閉動態效果時對話方塊立即顯示或移除。

## 相關元件

- `Drawer` 用於長表單及需保持頁面可見的詳細資料面板。
- `BottomSheet` 用於以行動裝置為主、需停靠在數個高度的任務。
- `Popover` 用於不阻擋頁面的互動內容。
- `Alert` 用於留在頁面內的訊息，`Toast` 用於簡短的操作結果。
