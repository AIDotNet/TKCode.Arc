---
title: 輪播
summary: 透過拖曳、輕掃或方向鍵瀏覽一排投影片。
source-hash: aa97f66dd6c8
---

<Demo name="Slides" />

## 何時使用

- 瀏覽一小組相似的項目，例如套房、優惠或照片。
- 提示還有更多項目：每側的相鄰項會暗示這一排的其餘內容。
- 透過可選的自動播放輪替展示精選內容。

## 何時不用

- 需要逐一對每個項目做出決定：用 `CardStack`。
- 比較兩張圖片：用 `ImageCompare`。
- 閱讀者需要掃讀或排序的長記錄清單：用 `SortableDataTable`。

## 用法

每個子元素對應一張投影片。`Label` 為輪播命名，供輔助技術使用。

```xml
<arc:Carousel Label="港灣套房精選">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
    <Border Height="200" Background="#8AA399" CornerRadius="12" />
</arc:Carousel>
```

### 繫結項目

當投影片來自資料時，將 `ItemsSource` 與 `ItemTemplate` 搭配使用。

```xml
<arc:Carousel Label="港灣套房" ItemsSource="{Binding Suites}">
    <arc:Carousel.ItemTemplate>
        <DataTemplate>
            <Border Height="200" Background="#2F4858" CornerRadius="12">
                <TextBlock Text="{Binding Name}" Margin="18" />
            </Border>
        </DataTemplate>
    </arc:Carousel.ItemTemplate>
</arc:Carousel>
```

### 投影片尺寸與標籤

投影片寬度為輪播寬度的 `SlideWidthRatio`（預設 0.8），上限為 `MaxSlideWidth`（340）。`SlideGap` 設定投影片之間的間距。`SlideLabelFormatter` 產生每張投影片播報時使用的文字。

```csharp
carousel.SlideLabelFormatter = (index, count) => $"檔案照片 {index + 1}，共 {count}";
```

### 自動播放

設定 `Interval` 會加入播放控制項。`IsAutoPlay` 開始輪替，控制項可切換其狀態。輪替會向前進行，播放到最後一張後回到第一張。投影片顯示期間，指示器會逐漸耗盡。

<Demo name="Rotation" />

```xml
<arc:Carousel Label="季節性優惠" Interval="0:0:4" IsAutoPlay="True">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
</arc:Carousel>
```

### 透過程式碼導覽

```csharp
carousel.SelectionChanged += (_, _) => readout.Text = $"第 {carousel.SelectedIndex + 1} 張，共 {count} 張";

carousel.Next();
carousel.Previous();
carousel.GoTo(3);
```

### 長輪播

有 120 張投影片時，只會建立目前位置附近的投影片。

<Demo name="LongCarousels" />

### 照片

投影片可以在繪製的場景之上顯示 `AsyncImage` 照片，因此離線時看起來仍然完整。

<Demo name="Photos" />

## API 參考

<!-- api: Carousel -->

## 無障礙

- `Label` 為輪播命名。選取變更時，投影片會透過 `SlideLabelFormatter` 以「2 / 5」之類的形式播報。
- 左右方向鍵每次移動一張投影片。Home 與 End 分別跳到第一張與最後一張。
- Tab 依序切換投影片、播放控制項、指示器以及上一張、下一張按鈕。
- 非作用中的投影片會從焦點順序與自動化樹中移除。
- 自動播放會在指標位於輪播上方、鍵盤焦點位於其中、正在拖曳、捲出可視範圍以及視窗最小化時暫停。自動播放不會播報每一張投影片。

## 動態效果

- 拖曳時投影片跟隨指標移動，並帶著慣性吸附到最近的投影片。鍵盤或按鈕操作會沿移動方向切換投影片。
- 相鄰投影片會隨與作用中投影片的距離而縮小並變暗。
- 指示器會延展成一個跟隨作用中投影片的膠囊形狀；自動播放等待期間，膠囊會逐漸耗盡。
- 減少動態效果時直接跳到目標投影片，並停止自動播放。關閉動態效果時立即到達最終狀態。

## 相關元件

- 每個項目都需要做出決定而不只是瀏覽時，用 `CardStack`。
- 同一張圖片的前後對比，用 `ImageCompare`。
- 在不相似的面板之間切換時，用 `Tabs`。
