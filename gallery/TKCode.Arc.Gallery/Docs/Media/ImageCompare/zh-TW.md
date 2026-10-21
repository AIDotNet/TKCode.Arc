---
title: 圖片比較
summary: 拖曳分隔線橫跨兩張圖片，查看變化之處。
source-hash: 7339ee162d6e
---

<Demo name="BeforeAndAfter" />

## 何時使用

- 展示同一張圖片的前後對比，例如修圖、重新設計或清理效果。
- 比較兩個結果，由讀者決定要揭示多少變化。
- 上下比較：將 `Orientation` 設為 `Vertical`。

## 何時不用

- 瀏覽多張圖片：用 `Carousel`。
- 拆分即時內容，例如應用程式中的兩個面板：用 `ResizablePanels`。
- 並排比較記錄：用 `SortableDataTable`。

## 用法

兩側應使用相同的裁切與尺寸，以便揭示的部分對齊。

```xml
<arc:ImageCompare Width="600" AspectRatio="1.5">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### 標籤與外框

`BeforeLabel` 與 `AfterLabel` 為兩側加上說明文字。分隔線經過某個說明時，該說明會淡出。`ShowLabels` 可隱藏兩個說明文字。`AspectRatio` 設定外框的寬高比（寬度除以高度），預設值為 1.5（3 / 2）。

<Demo name="WideFrame" />

```xml
<arc:ImageCompare AspectRatio="1.7777" ShowLabels="False" Position="30" Label="修圖預覽">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#8AA399" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### 上下比較

<Demo name="Vertical" />

```xml
<arc:ImageCompare Orientation="Vertical" BeforeLabel="原圖" AfterLabel="調色後">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

執行時修改 `Orientation` 會讓分隔線旋轉四分之一圈，揭示區域隨之移動。

### 圖片

`BeforeSource` 與 `AfterSource` 會在每一側的內容之上繪製圖片。圖片在背景載入，並從輕微模糊中淡入。若載入失敗，則顯示下方的內容。

<Demo name="Photos" />

```csharp
var compare = new ImageCompare
{
    Before = new Border { Background = Brushes.Gray },
    After = new Border { Background = Brushes.SteelBlue },
    BeforeSource = ImageSource.Parse("https://example.com/before.jpg"),
    AfterSource = ImageSource.Parse("https://example.com/after.jpg")
};
```

### 位置

`Position` 是分隔線的位置，以百分比計，從左側起算；垂直時從頂部起算。它是雙向繫結的，預設值為 50。`PositionChanged` 回報已確定的值：拖曳時每變化一個整百分點回報一次，放開時再回報一次。

```csharp
compare.PositionChanged += (_, e) => readout.Text = $"位置 {e.Position:0.#}%";
compare.Position = 75;
```

## API 參考

<!-- api: ImageCompare -->

## 無障礙

- `Label` 為分隔線把手命名，預設值為「前後比較」。
- 把手是可取得焦點的滑桿。其值為後圖所佔的比例，例如「後圖 40%」。
- 方向鍵每次移動分隔線 1%，按住 Shift 再按方向鍵移動 10%，PageUp 與 PageDown 每次移動 10%。
- Home 與 End 分別完整顯示後圖或前圖；在把手上按兩下會使其回到中間。
- 焦點框在按下按鍵後出現，而不是在焦點隨指標移動時出現。

## 動態效果

- 拖曳時分隔線與指標一比一跟隨，越過邊緣時產生阻尼，放開後停留在目前位置。拖曳時把手會拉伸成膠囊形狀。
- 分隔線經過某個說明時，該說明淡出。按鍵推向邊緣時會產生短促的回彈。
- 減少動態效果時，分隔線與把手直接定位，不使用彈簧或邊緣阻尼。關閉動態效果時立即到達最終狀態。

## 相關元件

- `Carousel`：瀏覽一組圖片或投影片，而不是比較兩張。
- `CardStack`：逐一瀏覽項目並對每一項做出決定。
- `Slider`：在沒有圖片的軌道上選擇數值。
