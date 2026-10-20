---
title: 主題切換
summary: 在亮色與暗色外觀之間切換的四種平滑方式。
source-hash: a1ed29b5c9c0
---

## 何時使用

- 頁首或工具列中單一的亮色與暗色切換。
- 頁面轉場需要與版面相配的切換：從按鈕處擴散的圓形、日蝕式、中線接縫或上升式。
- 只負責發出要求、由應用程式自行套用主題的切換。

## 何時不用

- 主題選項包含「跟隨系統」，或位於帳號選單中：使用 `UserMenu`。
- 不改變主題的一般動作按鈕：使用 `Button`。

## 用法

`ThemeSwitcher` 顯示下一次按下後將切換到的外觀。如果未設定 `Appearance`，它會跟隨視窗目前的實際主題變體。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ThemeSwitcher Variant="Reveal" />
</UserControl>
```

### 變體

`Variant` 選擇頁面轉場方式。`Reveal` 從按鈕處以圓形向外擴散新外觀。`Eclipse` 像日蝕一樣掠過頁面，適合展示頁面。`Split` 從中線接縫處展開，不依賴按鈕的位置。`Rise` 從底部升起，最為安靜。

```xml
<arc:ThemeSwitcher Variant="Eclipse" />
<arc:ThemeSwitcher Variant="Split" />
<arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
```

### 僅圖示與文字

`IsIconOnly` 顯示 38 乘 38 的方形按鈕。`Text` 設定圖示旁可見的文字，`Label` 設定無障礙名稱。未設定 `Label` 時，名稱會說明下一次按下將切換到哪種外觀。

```xml
<arc:ThemeSwitcher Text="外觀" Label="切換外觀" />
```

### 處理切換要求

主題變更前會觸發 `ThemeChangeRequested`。其引數包含下一個外觀、主題變體、觸發的按鈕以及擴散的起點。設定 `Handled` 後由你自行套用主題，切換控制項不會再次套用。

```csharp
switcher.ThemeChangeRequested += (_, e) =>
{
    e.Handled = true;
    switcher.Appearance = e.Next;
    ApplyAppearance(e.Next);
};
```

### 受控外觀

`Appearance` 為目前顯示的外觀，決定圖示、按下狀態與名稱。在程式碼中設定它，可使切換控制項與應用程式保持同步。

```xml
<arc:ThemeSwitcher Appearance="Light" Variant="Rise" />
```

```csharp
switcher.Toggle();
```

## API 參考

<!-- api: ThemeSwitcher -->

## 無障礙

- 切換控制項是一個切換按鈕。處於暗色外觀時它回報為開啟，其名稱說明下一個外觀。
- 太陽與月亮圖示對輔助技術隱藏。
- 按 Enter 或 Space 切換主題。
- 鍵盤焦點顯示標準的焦點框，與所有 Arc 按鈕一致。

## 動態效果

- 太陽與月亮沿同一路徑相互轉動、縮放，舊圖示迅速離開。
- 暗色模式下圖示會沿其變體的方向輕微移動：`Reveal` 使其放大，`Eclipse` 與 `Rise` 使其略微移動。
- 頁面轉場依變體揭示新外觀。舊視窗保留為快照，新外觀切入其上方。
- 首次設定外觀時不產生動畫。
- 減少動態效果時移除圖示的輕微移動與頁面轉場，圖示立即淡入。關閉動態效果時立即切換。

## 相關元件

- `Button`：不改變主題的一般動作。
- `UserMenu`：包含「跟隨系統」並位於帳號選單中的主題選擇。
- `SegmentedControl`：在幾個具名選項之間選擇並保持選取狀態。
- `IconMorph`：在兩種狀態之間變形的獨立圖示。
