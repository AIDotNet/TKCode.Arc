---
title: 文字變形
summary: 逐字母將標籤變形為下一個狀態。
source-hash: a984607c41a0
---

## 何時使用

- 會變化的簡短狀態詞，例如「追蹤」與「已追蹤」，或「儲存」與「已儲存」。
- 自訂按鈕或標示內的標籤，需要原地變化。
- 計數或價格的首尾保持不動、只有中間變化的情境。

## 何時不用

- 需要點選行為與多種狀態的按鈕：用 `Button`。
- 需要換成多行的文字：`TextMorph` 永遠為單行。
- 長文字或內文：逐字母效果僅適用於簡短標籤。
- 顯示持續工作的狀態列：用 `TextShimmer`。

## 用法

將 `Text` 設為目前的標籤。當 `Text` 變更時，字母會滑動、升起或淡入到新標籤。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextMorph Text="部署" FontSize="28" />
</UserControl>
```

```csharp
status.Text = "部署中";
```

### 切換狀態

狀態詞從一個狀態切換到下一個狀態。兩個標籤共有的字母會滑動到新位置，新字母以短暫的錯落效果升起，被移除的字母則模糊消失。

```csharp
var words = new[] { "部署", "部署中", "已部署", "已回復" };
var index = 0;
next.Click += (_, _) => status.Text = words[index = (index + 1) % words.Length];
```

### 比對方式

`Matching` 設定新舊文字之間如何配對字母。

- `Occurrence`（預設）將每個字元與新文字中相同次序的字元配對。第二個 "i" 與新文字中的第二個 "i" 配對。
- `PrefixSuffix` 保持共同的開頭與結尾不動，只取代中間部分。適用於價格與計數。

```xml
<arc:TextMorph Text="NT$12 / 月" FontSize="20" Matching="PrefixSuffix" />
```

### 字型

`TextMorph` 使用 `TextElement` 的字型與前景屬性。可以在控制項或其父元素上設定。

```xml
<arc:TextMorph Text="已儲存" FontSize="16" FontWeight="SemiBold" />
```

## API 參考

<!-- api: TextMorph -->

## 無障礙

- 目前的文字即為控制項的名稱，輔助技術讀取的是純文字。
- 字母對輔助技術隱藏，因此變化不會被逐字母朗讀。
- 控制項本身不播報變化。需要播報新狀態時，請在父元素上使用即時區域。
- 本控制項無法取得焦點，也沒有鍵盤互動。

## 動態效果

- 共有的字母在彈簧作用下滑動到新位置。
- 新字母從柔和的模糊中以短暫的錯落效果逐漸清晰。被移除的字母模糊淡出。
- 容器寬度隨新標籤以彈簧方式變化。字型或容器尺寸變化時，則無動畫地直接跳到新寬度。
- 減少動態效果時標籤與寬度立即切換。關閉動態效果時同樣如此。

## 相關元件

- `Button`：用於可點選且有自身狀態的控制項。
- `TextShimmer`：顯示持續工作的狀態列。
- `TextReveal`：出現時只播放一次的標題。
