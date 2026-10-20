---
title: 內容選單
summary: 將次要操作放在所選物件附近。
source-hash: 8ef17adf0a91
---

## 何時使用

- 針對檔案、卡片或列的次要操作，例如「複製連結」或「刪除檔案」，並緊鄰它們作用的物件。
- 不希望在版面中佔用可見選單按鈕的操作。
- 簡短、扁平的清單，末尾有一個破壞性操作列。

## 何時不用

- 使用者需要在不右鍵或長按的情況下找到的操作：用帶可見觸發器的 `DropdownMenu`。
- 以觸控為主、使用滑動操作的清單：用 `SwipeActions`。
- 內容本身需要點選操作：一般點選也會開啟選單，應在旁邊使用 `Button`。
- 有預設操作與其他替代項：用 `SplitButton`。

## 用法

用 `ContextMenu` 包裹目標元素並設定其 `Label`。內容即為目標。在程式碼中向 `Items` 加入選單列。下方程式碼片段中，`file` 即為該 `ContextMenu`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ContextMenu Label="Q3 報告.pdf 的操作">
        <Border Width="220" Height="120" Padding="16" CornerRadius="18" BorderThickness="1">
            <TextBlock Text="Q3 報告.pdf" />
        </Border>
    </arc:ContextMenu>
</UserControl>
```

```csharp
file.Items.Add(new MenuEntry { Header = "複製連結", Icon = ArcIcons.Copy });
file.Items.Add(new MenuEntry { Header = "刪除檔案", Icon = ArcIcons.Trash2, IsDestructive = true });
```

### 處理選取

`ItemClicked` 會回報被啟用的列。除非選單列設定了 `StaysOpenOnClick`，否則事件之後選單會關閉。

```csharp
file.ItemClicked += (_, e) => status.Text = $"Q3 報告.pdf：{e.Header}";
```

### 勾選列

沒有圖示的列在 `IsChecked` 為 true 時顯示勾號。在該列的 `Click` 處理常式中切換它。

```csharp
var pinned = new MenuEntry { Header = "已釘選", IsChecked = true };
pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
image.Items.Add(pinned);
```

### 資料驅動的列

`ItemsSource` 會為每個項目建立一列。`MenuEntry` 實例直接使用；其他資料需要 `ItemTemplate`。

```xml
<arc:ContextMenu Label="檔案操作" ItemsSource="{Binding FileActions}">
    <TextBlock Text="{Binding Name}" />
</arc:ContextMenu>
```

### 為任意控制項附加選單

`arc:ContextMenu.Menu` 無需包裹即可為任意控制項提供右鍵選單。

```xml
<Border Width="220" Height="96" CornerRadius="18" BorderThickness="1">
    <arc:ContextMenu.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="新增資料夾" />
            <arc:MenuEntry Header="重新整理" />
        </arc:MenuSurface>
    </arc:ContextMenu.Menu>
</Border>
```

### 以程式碼開啟與關閉

```csharp
file.Open(); // 顯示在目標下方
file.Close();
```

## API 參考

<!-- api: ContextMenu -->

<!-- api: MenuEntry -->

## 無障礙

- 目標可取得焦點；選單以 `Label` 播報，該標籤同時命名目標與選單。
- 選單列具有選單項目角色。Enter 與 Space 選取一列，Escape 關閉選單並將焦點交還目標。
- Shift+F10 與 ContextMenu 鍵可在已取得焦點的目標下方開啟選單。
- 沒有圖示的已勾選列會顯示勾號，但勾選狀態不會被播報。狀態很重要時，請將其寫入 `Header`。
- 破壞性操作列僅以顏色標示，因此應在 `Header` 中寫明將被刪除的內容。

## 動態效果

- 右鍵時選單以快速彈簧從指標處展開，並伴隨短暫的淡入。選單列以短促的錯開方式依序淡入。
- 點選、輕觸或 Shift+F10 會在目標下方以相同的動態效果開啟選單。
- 捲動或調整大小會關閉選單，選單不會隨目標移動。
- 減少動態效果時去除縮放與滑移，僅保留快速淡入。關閉動態效果時選單立即顯示或隱藏。

## 相關元件

- 操作需要可見觸發器時，用 `DropdownMenu`。
- 以觸控為主的列，透過滑動顯示操作時，用 `SwipeActions`。
- 一個操作為預設、選單中為替代項時，用 `SplitButton`。
- `MenuSurface` 是共用的選單彈層，可將選單錨定到任意控制項。
