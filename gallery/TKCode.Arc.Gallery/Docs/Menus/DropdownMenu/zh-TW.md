---
title: 下拉選單
summary: 錨定在觸發器上的聚焦操作清單。
source-hash: 7192f95491ae
---

<Demo name="Actions" />

## 何時使用

- 帶有標籤觸發器背後的一小組指令，例如「重新命名」「複製」與「刪除」。
- 列或卡片的操作選單，其中可見的入口比右鍵更清楚。
- 以分隔線為相關列分組、並將破壞性操作放在最後的清單。

## 何時不用

- 有預設操作與少數替代項：用 `SplitButton`。
- 針對某段內容的右鍵操作：用 `ContextMenu`。
- 頭像背後包含身分與主題的帳戶選單：用 `UserMenu`。
- 表單中屬於一部分的一般選項清單：用下拉式選取或單選群組。

## 用法

直接宣告選單列。指令用 `MenuEntry`，分隔線用 `MenuEntrySeparator`，群組標題用 `MenuEntryLabel`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DropdownMenu Label="操作">
        <arc:MenuEntry Header="重新命名" />
        <arc:MenuEntry Header="複製" />
        <arc:MenuEntry Header="刪除"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:DropdownMenu>
</UserControl>
```

透過程式碼以 `ArcIcons` 設定圖示，它是 24x24 的 Lucide 幾何圖形。

```csharp
var rename = (MenuEntry)menu.Items[0];
rename.Icon = ArcIcons.Pencil;
```

### 處理選取

`ItemClicked` 會回報被啟用的列。除非選單列設定了 `StaysOpenOnClick`，否則事件之後選單會關閉。

```csharp
menu.ItemClicked += (_, e) => status.Text = $"已選取：{e.Header}";
```

### 即時標籤與單選列

透過程式碼設定 `Label` 可變更觸發器文字。具有相同 `GroupName` 的單選列互斥，選取後選單會關閉。設定了 `StaysOpenOnClick="True"` 的核取列（`ToggleType="CheckBox"`）會保持選單開啟，便於在一次開啟中切換多個選項。

<Demo name="LabelAndToggles" />

### 資料驅動的列與自訂觸發器

`ItemsSource` 會為每個項目建立一列。`Trigger` 可用任意控制項取代內建按鈕。選單會錨定到該控制項，點選 `Button` 即可開啟。

<Demo name="CustomTrigger" />

```xml
<arc:DropdownMenu ItemsSource="{Binding FileActions}" Side="Bottom">
    <arc:DropdownMenu.Trigger>
        <Button Content="..." />
    </arc:DropdownMenu.Trigger>
</arc:DropdownMenu>
```

### 以程式碼開啟

```csharp
menu.Open(focusFirst: true);
menu.Close();
```

### MenuSurface

`MenuSurface` 是每個 Arc 選單所依據的可重複使用選單彈層。可在程式碼中針對任意錨點將其開啟。

<Demo name="MenuSurface" />

## API 參考

<!-- api: DropdownMenu -->

<!-- api: MenuEntry -->

## 無障礙

- 觸發器以其 `Label` 播報，並回報選單是否展開。
- 選單列具有選單項目角色。輸入首字母可依字母快速定位到對應列，停用的列會被略過。
- 在觸發器上按 Enter、Space 或 ArrowDown 會開啟選單並定位到第一列。Escape 關閉選單並將焦點交還觸發器。
- 破壞性操作列僅以顏色標示，因此其 `Header` 應寫明將被刪除的內容。
- 圖示與反白對輔助技術隱藏。

## 動態效果

- 選單以彈簧從觸發器邊緣展開，並伴隨短暫的淡入。關閉時更快、幅度更小。
- 指標移動時，一個反白區塊在列之間滑動；鍵盤操作時則直接跳轉。選單列以短促的錯開方式依序淡入。
- 新的 `Label` 會帶著輕微模糊升起，觸發器寬度隨之調整。選單開啟時，箭頭會旋轉。
- 減少動態效果時去除縮放與滑移，僅保留快速淡入。關閉動態效果時選單立即顯示。

## 相關元件

- 針對選取物件、透過右鍵開啟的次要操作：用 `ContextMenu`。
- 一個操作為預設、選單中為替代項時：用 `SplitButton`。
- 應用程式頂列中的帳戶入口：用 `UserMenu`。
- `MenuSurface` 是共用的選單彈層，可將選單錨定到任意控制項。
