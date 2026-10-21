---
title: 工具提示
summary: 為不熟悉的控制項提供簡短的輔助文字。
source-hash: 9321fab1844c
---

<Demo name="Sides" />

## 何時使用

- 純圖示按鈕或工具列項目的單行標籤。
- 頁面上被截斷之標籤的完整文字。
- 不影響主要操作的簡短說明，鍵盤使用者聚焦控制項即可看到。

## 何時不用

- 連結、按鈕或輸入框：用 `Popover`。工具提示內容無法互動。
- 人員或連結的豐富預覽：用 `HoverCard`。
- 觸控使用者必須看到的資訊：應顯示在頁面上，而不是放在工具提示中。

## 用法

工具提示是一個附加屬性。在任意控制項上設定 `arc:Tooltip.Tip`；懸停或鍵盤聚焦時顯示文字。

### 基本

```xml
<arc:Button Content="複製" arc:Tooltip.Tip="複製連結" />
```

### 方向

`arc:Tooltip.Side` 設定提示氣泡在觸發器的哪一側開啟：`Top`（預設）、`Bottom`、`Left` 或 `Right`。

```xml
<arc:Button Variant="Secondary" Content="分享" arc:Tooltip.Tip="與你的團隊分享"
            arc:Tooltip.Side="Bottom" />
```

### 繫結文字

`Tip` 接受任意物件。當文字來自檢視模型時進行繫結。

```xml
<arc:Button Content="儲存" arc:Tooltip.Tip="{Binding SaveHint}" />
```

### 修改文字與延遲

工具提示開啟期間可以在程式碼中修改文字。文字會交叉淡化，氣泡隨之調整大小以符合內容。設定 `ShowDelay` 可以修改第一個工具提示開啟前的等待時間，預設為 250ms。

<Demo name="LiveText" />

```csharp
Tooltip.SetTip(save, "剛剛已儲存");
Tooltip.SetShowDelay(save, TimeSpan.FromMilliseconds(400));
```

## API 參考

<!-- api: Tooltip -->

## 無障礙

- 工具提示開啟時，其文字會寫入觸發器的說明文字中。
- 工具提示不能取代名稱。僅含圖示的觸發器仍需設定自己的 `AutomationProperties.Name`。
- 工具提示在懸停和鍵盤聚焦時開啟，按 Escape 關閉。
- 氣泡中沒有可互動的元素，因此鍵盤使用者可以閱讀內容而不失去焦點。

## 動態效果

- 第一個工具提示等待 250ms，然後從觸發器向上升起 3px，同時從縮放 0.97 開始出現。
- 指標移動到相鄰的觸發器，或上一個工具提示在 300ms 之前剛關閉時，下一個工具提示立即開啟，僅以淡入方式呈現。
- 工具提示開啟期間文字變化時，新詞帶著柔和的模糊升起，氣泡以彈簧調整為新尺寸。
- 關閉為瞬間淡出，氣泡同時縮小到 0.98。
- 減少動態效果時移除移動，保留短暫淡入。關閉動態效果時工具提示立即顯示或移除。

## 相關元件

- `Popover` 用於錨定在控制項上的互動內容。
- `HoverCard` 用於人員或連結的豐富唯讀預覽。
