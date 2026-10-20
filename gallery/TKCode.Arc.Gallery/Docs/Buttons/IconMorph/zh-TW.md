---
title: 圖示變形
summary: 在不同狀態之間變形的圖示：選單變關閉、播放變暫停、複製變勾選等。
source-hash: a46fa0c6e43e
---

## 何時使用

- 按鈕的圖示在每個狀態下意義不同，例如選單變為關閉圖示。
- 播放與暫停、主題、通知或喜歡的切換，希望使用者看到圖示的變形過程。
- 展開控制項的箭頭會在區段展開時轉動。

## 何時不用

- 帶確認的複製動作：使用 `CopyButton`，它本身就會變形圖示。
- 帶文字的亮暗切換：使用 `ThemeSwitcher`。
- 狀態永不改變的圖示：使用一般的 Lucide 圖示即可。
- 單獨將圖示作為控制項：`IconMorph` 僅作裝飾用途，應放在 `Button` 或其他具有名稱與狀態的控制項內。

## 用法

`IconMorph` 提供八組圖示對。`Kind` 選擇圖示對，`IsActive` 選擇第二個狀態。將它放入按鈕中，並為按鈕設定名稱。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Variant="Ghost" AutomationProperties.Name="開啟選單">
        <arc:IconMorph Kind="Menu" />
    </arc:Button>
</UserControl>
```

### 圖示類型

`Menu` 變為關閉，`Play` 變為暫停，`Plus` 變為勾選，`Copy` 變為勾選，`Theme` 變為月亮，`Chevron` 轉動，`Bell` 在開啟與關閉通知之間切換，`Heart` 表示喜歡。

```xml
<arc:IconMorph Kind="Play" />
<arc:IconMorph Kind="Theme" IsActive="True" />
<arc:IconMorph Kind="Heart" IsActive="{Binding IsLiked}" />
```

### 切換狀態

將 `IsActive` 繫結到按鈕的狀態。在點擊處理常式中翻轉它，並同步更新按鈕的名稱，使其與新的意義一致。

```csharp
var icon = new IconMorph { Kind = IconMorphKind.Play };
var button = new Button { Content = icon, Variant = ButtonVariant.Ghost };
button.Click += (_, _) =>
{
    icon.IsActive = !icon.IsActive;
    AutomationProperties.SetName(button, icon.IsActive ? "暫停" : "播放");
};
```

### 尺寸與線寬

`IconSize` 設定渲染尺寸，單位為像素，預設值為 24。`StrokeThickness` 在 24 單位的網格上設定，並隨 `IconSize` 等比縮放，因此預設線寬 1.75 在任何尺寸下都保持比例。

```xml
<arc:IconMorph Kind="Menu" IconSize="16" />
<arc:IconMorph Kind="Menu" IconSize="32" StrokeThickness="2" />
```

### 箭頭方向

對於 `Chevron`，`ChevronPointing` 設定靜止時的方向，`ChevronTurn` 設定激活時轉動的角度（單位為度）。值為 180 時由下向上翻轉，值為 90 時由右向下轉動。

```xml
<arc:IconMorph Kind="Chevron" />
<arc:IconMorph Kind="Chevron" ChevronPointing="Right" ChevronTurn="90" />
```

## API 參考

<!-- api: IconMorph -->

## 無障礙

- 圖示對輔助技術隱藏，名稱由外圍的按鈕提供。
- 在按鈕上呈現狀態：切換按鈕使用按下狀態，展開控制項使用展開狀態，或更改名稱。
- 意義改變時更改名稱，例如從播放改為暫停。
- 圖示不接收焦點，也不處理按鍵，由外圍的按鈕負責。

## 動態效果

- 線條旋轉並滑動到位，輪廓在兩種形狀之間插值。快速切換時，圖示會從目前位置反向變形。
- 複製圖示會收起後方的紙張並繪出一個勾。太陽會變成帶月牙缺口的月亮。
- 鈴鐺開啟時響一次，愛心每次被按下喜歡時都會彈出，並伴隨光環與火花。
- 減少動態效果時，所有變形立即完成，並略過光環與火花。關閉動態效果時同樣如此。

## 相關元件

- `CopyButton`：帶有自身確認的複製動作。
- `ThemeSwitcher`：帶文字標籤的亮暗切換。
- `Button`：承載圖示變形名稱與狀態的控制項。
- `ExpandingButtonGroup`：展開後顯示文字標籤的圖示動作。
