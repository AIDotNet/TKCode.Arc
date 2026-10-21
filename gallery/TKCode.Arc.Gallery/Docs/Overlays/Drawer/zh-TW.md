---
title: 抽屜
summary: 用於專注工作的暫時側邊面板。
source-hash: 55d20e5dba67
---

<Demo name="Sides" />

## 何時使用

- 篩選、設定或紀錄詳細資料，需要讓使用者保持在目前頁面的脈絡中。
- 對 `Dialog` 來說過長、又不應離開目前檢視的表單。
- 使用者可能透過將標題列拖向邊緣來關閉的面板。

## 何時不用

- 簡短的決定與確認：用 `Dialog`。
- 以行動裝置為主、需停靠在數個高度的面板：用 `BottomSheet`。
- 依附於某個控制項的小型內容，且不需要遮罩：用 `Popover`。

## 用法

`Drawer` 是一種從某一邊緣滑入的模態面板。可以在任意位置宣告它，並繫結 `IsOpen`。

### 邊緣

`Side` 決定面板從哪一邊緣滑入：`Left`、`Right`、`Top` 或 `Bottom`。預設值為 `Right`。

```xml
<arc:Drawer Title="篩選"
            Description="在不離開清單的情況下縮小範圍。"
            Side="Left"
            IsOpen="{Binding ShowFilters}">
    <StackPanel Spacing="16">
        <CheckBox Content="僅未結發票" />
        <CheckBox Content="包含草稿" />
    </StackPanel>
</arc:Drawer>
```

### 長內容

標題保持固定，主體區域可捲動。將完整內容放入抽屜即可，無需設定尺寸。

<Demo name="LongContent" />

```xml
<arc:Drawer Title="INV-2041" Description="由 Ada Lovelace 於 3 月 12 日支付。" IsOpen="{Binding ShowRecord}">
    <ItemsControl ItemsSource="{Binding LineItems}" />
</arc:Drawer>
```

### 操作

將按鈕放在 `Actions` 中。帶有 `arc:Dialog.Result` 的按鈕會以該值關閉抽屜，與 `Dialog` 中的行為相同。

```xml
<arc:Drawer Title="篩選" IsOpen="{Binding ShowFilters}">
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="重設" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="套用" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 從程式碼中開啟

當同一個抽屜服務於多個邊緣時，應同時設定 `Side` 與 `IsOpen`。`OpenCommand` 可以在不設定 `IsOpen` 的情況下，由按鈕開啟抽屜。

```csharp
drawer.Side = DrawerSide.Bottom;
drawer.IsOpen = true;

openDetails.Command = details.OpenCommand;
```

### 關閉原因

`Closed` 會回報抽屜關閉的原因：`CloseButton`、`Escape`、`PressOutside`、`Gesture`、`Action` 或 `Programmatic`。

```csharp
drawer.Closed += (_, e) => log.Text = $"關閉方式：{e.Reason}";
```

## API 參考

<!-- api: Drawer -->

## 無障礙

- 面板名稱取自 `Title`，說明文字取自 `Description`。
- 抽屜開啟時焦點移入其中，Tab 與 Shift+Tab 均停留在抽屜內，關閉後焦點返回開啟它的控制項。
- 按 Escape 關閉抽屜。關閉按鈕始終存在，預設名稱為「關閉抽屜」。
- 拖曳是可選操作。減少動態效果時拖曳功能關閉，因此關閉操作不依賴手勢。

## 動態效果

- 遮罩淡入的同時，面板以平滑彈簧從其邊緣滑入。
- 拖曳標題列朝邊緣移動時面板跟隨指標。超過面板的三分之一，或以快於 500px/s 的速度輕掃時，抽屜關閉；否則彈回。
- 點選、按 Escape 或按下遮罩會以短暫的補間動畫關閉抽屜，陰影在結束時淡出。
- 減少動態效果時關閉拖曳功能，滑動改為短暫淡入。關閉動態效果時抽屜立即顯示或移除。

## 相關元件

- `Dialog` 用於必須打斷頁面的決定。
- `BottomSheet` 用於以行動裝置為主、需停靠在數個高度並可展開的面板。
- `Popover` 用於依附於某個控制項的小型互動內容。
