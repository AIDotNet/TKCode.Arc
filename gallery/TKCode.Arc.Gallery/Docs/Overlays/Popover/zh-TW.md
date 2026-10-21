---
title: 彈出框
summary: 用於提供情境資訊的小型定位浮層。
source-hash: 05cbf1a4a141
---

<Demo name="Share" />

## 何時使用

- 不應阻擋頁面的互動內容，例如分享設定或小型篩選表單。
- 由使用者點選按鈕或其他控制項後開啟的內容。
- 透過程式碼使用 `ShowAt` 錨定到任意控制項的內容。

## 何時不用

- 懸停或聚焦時的簡短文字標籤：用 `Tooltip`。
- 人員或連結的唯讀預覽：用 `HoverCard`。
- 使用者繼續前必須作出的決定：用 `Dialog`。
- 指令清單：用 `DropdownMenu`。

## 用法

`Popover` 錨定在觸發器上，預設為非模態，因此頁面其餘部分仍可操作。

### 觸發器與內容

將 `Trigger` 設為開啟面板的控制項。彈出框的內容即為面板。

```xml
<arc:Popover>
    <arc:Popover.Trigger>
        <arc:Button Variant="Secondary" Content="分享" />
    </arc:Popover.Trigger>
    <StackPanel Spacing="8">
        <TextBlock Text="分享連結" FontWeight="Medium" />
        <TextBlock Text="任何擁有連結的人都可以檢視。" />
    </StackPanel>
</arc:Popover>
```

### 附加到控制項

`arc:Popover.Attach` 可以為任意控制項加上彈出框。點選該控制項會切換面板的顯示狀態。

```xml
<arc:Button Content="篩選">
    <arc:Popover.Attach>
        <arc:Popover Placement="Top">
            <TextBlock Text="在頂部一側開啟。" />
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

### 位置與對齊

`Placement` 為偏好的一側：`Top`、`Bottom`、`Left` 或 `Right`。空間不足時面板會翻轉。`Alignment` 決定沿觸發器邊緣的位置：`Start`、`Center` 或 `End`。`Offset` 為與觸發器之間的間距，單位為像素，預設值為 6。

<Demo name="Sides" />

```xml
<arc:Popover Placement="Bottom" Alignment="Center" Offset="8">
    <arc:Popover.Trigger>
        <arc:Button Variant="Ghost" Content="篩選" />
    </arc:Popover.Trigger>
    <TextBlock Text="狀態：任意" />
</arc:Popover>
```

### 從程式碼中開啟

`ShowAt` 將彈出框錨定到任意控制項。`IsOpen` 是雙向的，`Opened` 與 `Closed` 會回報狀態變化。

<Demo name="FromCode" />

### 模態彈出框

將 `IsModal` 設為 true 後，面板外的按壓會被攔截，且只會關閉彈出框。焦點被限制在面板內。

```xml
<arc:Popover IsOpen="{Binding ShowPicker}" IsModal="True">
    <TextBlock Text="請先選擇一個選項。" />
</arc:Popover>
```

### 小型表單

彈出框可以放置小型表單，例如篩選條件。開啟時頁面仍可操作。

<Demo name="SmallForm" />

## API 參考

<!-- api: Popover -->

## 無障礙

- 在觸發器上按 Enter 或空白鍵會開啟彈出框。按 Escape 關閉，並將焦點返回觸發器。
- 面板開啟時焦點移入其中。面板外按壓會關閉彈出框，並將焦點返回觸發器。
- 當內容沒有標題時，請為彈出框設定 `AutomationProperties.Name`。該名稱用於標示面板。
- 非模態彈出框不限制焦點。將 `IsModal` 設為 true 會將焦點限制在面板內。
- 觸發器會公開彈出框是否已展開。

## 動態效果

- 面板以縮放 0.97 從朝向觸發器的 5px 處開始，快速淡入，並以彈簧從落定的一側就位。
- 關閉約需 160ms，移動距離為一半。開啟與關閉在動畫中途可以相互逆轉。
- 觸發器按下時改變顏色，但不縮放，因為面板是依據它測量的。
- 減少動態效果時移除移動與縮放，僅保留短暫淡入。關閉動態效果時面板立即顯示或移除。

## 相關元件

- `Tooltip` 用於懸停或聚焦時的單行標籤。
- `HoverCard` 用於懸停時開啟的唯讀預覽。
- `Dialog` 用於必須先處理才能繼續使用頁面的內容。
- `DropdownMenu` 用於指令清單。
