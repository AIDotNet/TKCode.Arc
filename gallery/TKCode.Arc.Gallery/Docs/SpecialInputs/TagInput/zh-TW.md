---
title: 標籤輸入
summary: 將簡短文字值轉換為可移除的標籤。
source-hash: 3617ec564045
---

<Demo name="Default" />

## 何時使用

- 關鍵字、標籤、主題或電子郵件地址等自由輸入的簡短值。
- 使用者透過輸入逐項新增的清單，每一項以 Enter 或逗號加入。
- 需要數量上限的清單，使用 `MaxTags` 設定。

## 何時不用

- 從固定清單中選擇的值：用 `MultiSelect` 或 `ChipGroup`。
- 單一值：用 `Input`。
- 內文中提及人員或頻道：用 `MentionInput`。
- 多行自由文字：用 `Textarea`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TagInput Label="標籤"
                  Placeholder="新增標籤"
                  Description="按 Enter 或逗號即可新增標籤。" />
</UserControl>
```

### 初始標籤

`Value` 保存標籤。它是 `IReadOnlyList<string>`，因此可在程式碼中設定，或綁定到一個集合。

```csharp
var tags = new TagInput
{
    Label = "標籤",
    Value = ["design", "motion", "react"]
};
```

### 數量上限與錯誤

`MaxTags` 會拒絕超出上限的新增。欄位會晃動，並播報已達上限。`ErrorMessage` 會將欄位標記為無效，並顯示錯誤列。

<Demo name="Limit" />

<Demo name="Error" />

### 貼上清單

貼上包含逗號、換行或定位字元的文字時，會被拆分為多個標籤。最後一段會保留在草稿中。

<Demo name="Paste" />

### 繫結與事件

`Value` 為雙向繫結。`ValueChanged` 會回報舊清單與新清單，以及 `Added` 與 `Removed` 標籤。

<Demo name="TwoWayValue" />

```xml
<arc:TagInput Label="關鍵字" Value="{Binding Keywords, Mode=TwoWay}" />
```

```csharp
tags.ValueChanged += (_, e) =>
{
    Console.WriteLine($"Added: {string.Join(", ", e.Added)}; removed: {string.Join(", ", e.Removed)}");
};

tags.AddTag("release");
tags.FocusEditor();
```

### 空欄位與預留位置文字

只要欄位中沒有標籤也沒有草稿，預留位置文字就會浮現。

<Demo name="Empty" />

### 說明文字

欄位下方的輔助文字，同時也會為螢幕閱讀器描述編輯器。

<Demo name="Description" />

### 換行

標籤會換到新的一列，外殼高度會隨平滑的彈簧動畫變化。移除其中一個，即可看到其餘標籤滑入新位置。

<Demo name="Wrapping" />

### 鍵盤

在起始位置按 Backspace 會選取最後一個標籤，Backspace 或 Delete 會移除所選的標籤，ArrowLeft 與 ArrowRight 會移動選取環，Escape 會清除選取。點擊標籤也會選取它。

<Demo name="Keyboard" />

### 停用

停用的欄位會在 `SurfaceMuted` 上淡化到 0.5 的不透明度。

<Demo name="Disabled" />

## API 參考

<!-- api: TagInput -->

## 無障礙

- `Label` 是欄位的名稱。`IsLabelVisible="False"` 時標籤不顯示，但仍作為名稱。
- 每個標籤都有一個移除按鈕，名稱為 "移除 <tag>"。
- 新增、移除、選取與重複項都會以禮貌方式播報。重複項不會被加入：既有的標籤會脈衝閃爍。
- 在輸入框起始位置按 Backspace 會選取最後一個標籤。Delete 移除選取的標籤；ArrowLeft 與 ArrowRight 在標籤與輸入框之間移動選取。Escape 清除選取。
- 草稿在欄位失去焦點時會轉換為標籤。

## 動態效果

- 新標籤在輸入位置以模糊與輕微縮放淡入。移除的標籤離開時，其餘標籤滑動到新的位置。
- 選取環在標籤之間滑動；標籤換行到新的一列時，外殼高度隨之調整。
- 重複項會讓既有標籤脈衝一次。被拒絕的新增會讓欄位晃動。
- 減少動態效果時，移除版面移動、縮放與模糊，僅保留簡短的淡入。關閉動態效果時直接跳到最終狀態。

## 相關元件

- 值來自固定清單時，用 `MultiSelect` 或 `ChipGroup`。
- 單一值用 `Input`。
- 內文中的 @人員與 #頻道參照用 `MentionInput`。
