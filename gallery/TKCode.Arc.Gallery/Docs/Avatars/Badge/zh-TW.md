---
title: 徽章
summary: 用於狀態、類別或中繼資料的小型標籤。
source-hash: c21246ac5464
---

<Demo name="Tones" />

## 何時使用

- 標題旁或表格儲存格中的簡短狀態，例如 Live、Draft 或 Failed。
- 徽章保持顯示時原地變化的數量或狀態。
- 帶有色調與選用前導圖示的列標記。

## 何時不用

- 完整的回饋句子：用 `Alert` 或 `Toast`。
- 使用者可以開啟或關閉的值：用 `ChipGroup`。
- 周圍帶有脈絡說明的大型數字：用 `MetricCard`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Badge Content="草稿" />
</UserControl>
```

### 色調

`Tone` 設定顏色樣式：`Neutral`（預設）、`Success`、`Info`（強調色）、`Warning` 與 `Danger`。標籤本身必須說明狀態，因為色調並非唯一的提示。

```xml
<WrapPanel>
    <arc:Badge Content="草稿" />
    <arc:Badge Tone="Success" Content="上線" />
    <arc:Badge Tone="Info" Content="測試版" />
    <arc:Badge Tone="Warning" Content="降級" />
    <arc:Badge Tone="Danger" Content="失敗" />
</WrapPanel>
```

### 尺寸

`Size` 設定高度：`Medium`（預設）為 26，`Small` 為 22。在密集的表格中使用 `Small`。

<Demo name="Small" />

### 圖示

`Icon` 接受一個前導圖示。它會採用色調顏色，並對輔助技術隱藏。範例使用來自 `ArcIcons` 的 `ArcGlyph`。

<Demo name="Icons" />

```csharp
badge.Tone = BadgeTone.Success;
badge.Content = "通過";
badge.Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 };
```

### 原地變化

保持徽章掛載並修改 `Content`。新的字串或數字會從柔和的模糊中升起，膠囊寬度會以彈簧方式調整以符合內容。重新建立徽章會失去這項動態效果。

<Demo name="ChangingInPlace" />

## API 參考

<!-- api: Badge -->

## 無障礙

- 徽章是行內文字：其標籤會與周圍的文字一併朗讀。
- 圖示對輔助技術隱藏。標籤必須說明狀態，而不是顏色。
- 標籤變化時，舊文字對輔助技術隱藏，只朗讀目前的文字。
- 徽章不會宣告其變化。如果變化必須被宣告，請將徽章放入外圍內容的即時區域中。
- 徽章無法取得焦點，也沒有鍵盤操作。

## 動態效果

- 新標籤會從柔和的模糊中升起，舊標籤則帶著輕微模糊向上離開。膠囊寬度隨變形彈簧跟進。
- 新圖示會從略小的尺寸彈出。由字型變化造成的尺寸改變會立即完成。
- 減少動態效果時，標籤快速淡入，寬度直接跳轉。關閉動態效果時，所有變化立即完成。

## 相關元件

- `Alert` 或 `Toast`：用於一句回饋。
- `ChipGroup`：用於使用者可以選取的值。
- `MetricCard`：用於帶脈絡說明的大型數值。
