---
title: Liquid tab bar
summary: 用於 App 主要分區的圖示標籤。一枚實心透鏡標出所選標籤並移到下一個，把它下方的標籤以反色繪製，描邊圖示因此變成實心；也可以拖曳或甩動它。
source-hash: 711ba2f97bb2
---

<Demo name="Preview" />

## 何時使用

`LiquidTabBar` 在幾個同級分區之間切換，每個分區由一個圖示和一個簡短的文字標籤表示。選取的標籤上蓋著一枚透鏡：一塊前景色的膠囊，
大小與該標籤完全一致。選取新的標籤時，透鏡滑過去，途中寬度變成新標籤的寬度，不會被拉長。透鏡下方的標籤會再畫一遍，前景色與背景色互換，
描邊圖示因此變成實心，徽章的顏色也隨之翻轉。預設只有選取的標籤顯示文字，且兩段文字從不重疊：透鏡出發前舊文字已經消失，停定時新文字才出現。

- **應用程式的幾個主要區域**：手機版面中的底部列，或精簡視窗中的頂部列。
- **使用者經常往返的少數分區**：看過一次文字後，只看圖示也能認出來。
- **帶計數的分區**：例如未讀訊息或待辦工作，以徽章顯示在圖示上。

## 何時不用

- **只有文字的分頁**，各自在頁面裡帶一個面板：`Tabs` 能處理，分頁多時還可以捲動。
- **選擇檢視或篩選條件**（日、週、月）：`SegmentedControl` 更小巧。
- **網站頁首裡的連結選單**：`MorphNav`。
- **超過五六個分區**：標籤列不會捲動或換行，只靠圖示也不再清楚。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiquidTabBar SelectedValue="{Binding Section}" AutomationProperties.Name="App sections">
        <arc:LiquidTabBarItem Value="feed" Label="Feed" Glyph="{x:Static arc:ArcIcons.House}" />
        <arc:LiquidTabBarItem Value="chat" Label="Chat" Glyph="{x:Static arc:ArcIcons.MessageCircle}"
                              Badge="2" BadgeLabel="2 new" />
        <arc:LiquidTabBarItem Value="me" Label="Me" Glyph="{x:Static arc:ArcIcons.User}" />
    </arc:LiquidTabBar>
</UserControl>
```

`LiquidTabBar` 位於 Special 套件中：在 `UseArcControls()` 旁邊呼叫 `UseArcSpecial()` 註冊它。每個標籤是一個 `LiquidTabBarItem`，
帶有 `Value`、`Label` 和 `Glyph`（24 × 24 的描邊幾何圖形，例如 `ArcIcons`）。標籤列是一個可選取的清單：把 `SelectedValue`
雙向繫結到目前分區的值，或者使用 `SelectedIndex` 與 `SelectionChanged`。只要有標籤，就總有一個被選取；停用的標籤會被略過。

標籤列只負責切換，不承載各分區的內容。請在它旁邊依 `SelectedValue` 顯示對應內容，例如放在 `ContentControl` 或 `ArcContentSwap`
中，就像預覽範例那樣。

使用 `ItemsSource` 時，為 `LiquidTabBarItem` 撰寫樣式，把它的 `Label`、`Glyph` 和 `Badge` 繫結到資料項目；沒有 `Label` 的標籤會改用
其內容或值的文字。沒有 `Glyph` 的標籤是純文字標籤，始終顯示文字。請保留預設的項目面板：透鏡依據標籤列自己的版面配置移動。

### 文字顯示

`Labels` 決定文字顯示在哪裡。`Active`（預設）只展開選取的標籤；`Always` 給每個標籤都顯示文字，標籤列也更寬；`None` 只留下圖示，適合空間緊張時。
無論哪種模式，螢幕閱讀器都能讀到文字。切換模式時，各標籤以與切換選取項目相同的彈簧展開或收合。

<Demo name="Labels" />

### 徽章

`Badge` 在標籤的圖示上顯示一個計數。計數變化時，數字捲動到新的數值；為 0 時徽章縮小消失。`BadgeLabel` 是螢幕閱讀器在文字之後讀出的內容
（例如 "8 unread"）；不設定時讀出數字。

<Demo name="Badges" />

### 選取時的圖示

在透鏡裡，標籤會把圖示填實繪製。設定 `ActiveGlyph` 後，透鏡裡改畫另一個圖形，例如用開啟的資料夾代替關閉的資料夾。

<Demo name="ActiveGlyph" />

### 拖曳與甩動

從選取的標籤開始拖曳：透鏡跟隨指標，前緣略微領先；放開後停在最近的標籤上。快速甩動時，速度越快它被拋得越遠，而且絕不會停回起始的標籤；
拉過最外側的標籤時，它的退讓越來越小，像被拉長的橡皮筋。拖曳期間各標籤保持大小不變，放開時才改變選取項目。沒有移動的短按就是一般點按。

### 最佳做法

**建議**

- 文字保持一個短詞，標籤不超過五個。
- 每個標籤都設定 `Label`，即使使用 `Labels="None"`：它為螢幕閱讀器命名該標籤。
- 為徽章設定 `BadgeLabel`，說明數字計的是什麼。

**避免**

- 在同一個標籤列中混用有圖示和沒有圖示的標籤。
- 用計時器改變選取項目：透鏡每次移動都會吸引目光。

## API 參考

<!-- api: LiquidTabBar -->

<!-- api: LiquidTabBarItem -->

## 無障礙

- 標籤列是一個分頁清單，除非 `AutomationProperties.Name` 另有設定，否則名稱為 "Sections"；每個標籤都是一個帶選取狀態的分頁。
- 標籤的名稱是它的文字，後面接著徽章：設定了 `BadgeLabel` 時讀它，否則讀數字。
- 透鏡下的反色只是繪製效果，不是第二組元素，所以每個標籤只會被回報一次。
- 鍵盤焦點會在標籤周圍顯示 2px 的焦點環；使用指標時不顯示。

### 鍵盤互動

| 按鍵 | 動作 |
|---|---|
| Tab | 把焦點移到選取的標籤；只有選取的標籤在 Tab 順序中。 |
| Right / Left | 焦點與選取項目一起移到下一個或上一個可用標籤；在最後一個上按 Right 回到第一個，在第一個上按 Left 到最後一個。 |
| Home / End | 跳到第一個或最後一個可用標籤並選取它。 |
| Enter 或 Space | 選取取得焦點的標籤。 |

## 動態效果

- 透鏡由一個彈簧驅動（visual duration 0.42s，無回彈），選取項目在途中再次改變時保留速度。它的邊緣跟隨兩側之間的標籤，所以無論在哪裡，
  它都恰好是一個標籤的寬度。
- 標籤以同一個彈簧變寬和變窄。上一個標籤的文字在 50ms 內消失；下一個標籤的文字在其變寬的最後十分之一裡淡入，同時滑動 4px 到位。
- 滑鼠停留在其他標籤上時，透鏡朝向那一側的邊緣會向相鄰標籤伸出 14% 的距離。
- 按下時透鏡內縮 2px；拖曳時前緣領先；徽章數字以平滑的彈簧捲動。
- 減少動態效果時，透鏡和文字立即就位，不滑動、不伸出、不平移；關閉動態效果時相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 透鏡 | 沿彈簧滑動 | 跳到位 | 跳到位 |
| 標籤展開與收合 | 彈簧 | 跳到位 | 跳到位 |
| 朝停留標籤伸出 | 有 | 無 | 無 |
| 停留時的圖示顏色 | 160ms | 120ms | 立即 |

## 響應式

- 只顯示圖示的標籤寬 44px，顯示文字時再加寬文字的寬度；標籤列與其標籤一樣寬，不會捲動。空間狹窄時，`Labels="None"` 讓它保持最小。
- 標籤列的尺寸是其標籤最終穩定後的尺寸，所以透鏡滑動時周圍的內容不會移動。
- 使用觸控時，橫向拖曳移動透鏡，直向滑動仍會捲動頁面。只有滑鼠會讓透鏡伸出。

## 相關元件

- `Tabs`：帶各自面板的文字分頁。
- `SegmentedControl`：精簡的篩選與檢視切換。
- `MorphNav`：帶選單的網站導覽。

| 元件 | 適合的情況 |
|---|---|
| `LiquidTabBar` | 少數幾個帶圖示、經常切換的分區 |
| `Tabs` | 文字分頁、分頁很多，或分頁帶面板 |
| `SegmentedControl` | 在少數幾個檢視或篩選之間做精簡的選擇 |
