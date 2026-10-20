---
title: 輸入框
summary: 標籤清楚、狀態實用的單行輸入框。
source-hash: 73a65b253fc6
---

## 何時使用

- 姓名、電子郵件地址、URL 等單行值。
- 隨輸入即時變化的驗證文案。

## 何時不用

- 多行文字：用 `Textarea`。
- 有固定格式的值：用 `PasswordField`、`SearchField` 或 `NumberField`。
- 在頁面上原地編輯文字：用 `InlineEdit`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Input Label="電子郵件"
               Placeholder="you@example.com"
               Description="我們不會公開你的電子郵件。" />
</UserControl>
```

### 輔助文字與錯誤提示

文案應由狀態推導，直接修改字串即可：只有變化的詞會移動，數字逐位滾動。不要在幾則獨立的訊息之間來回切換。

```xml
<arc:Input Label="使用者名稱"
           Text="{Binding UserName}"
           Description="{Binding UserNameHint}"
           ErrorMessage="{Binding UserNameError}" />
```

```csharp
input.TextChanged += (_, _) =>
{
    var text = input.Text ?? string.Empty;
    input.Description = $"3 到 20 個字元，還可輸入 {Math.Max(0, 20 - text.Length)} 個。";
    input.ErrorMessage = text.Length is > 0 and < 3 ? "至少輸入 3 個字元。" : null;
};
```

`ErrorMessage` 不為空時欄位即為無效狀態；為空時顯示 `Text` 繫結的驗證錯誤。

### 尺寸

`Size` 決定外框高度：`Small` 為 36，`Medium` 為 44（Arc 的預設外觀），`Large` 為 50。

```xml
<arc:Input Label="小" Size="Small" />
<arc:Input Label="大" Size="Large" />
```

### 前綴與後綴

```xml
<arc:Input Label="價格" Text="49">
    <arc:Input.Prefix>
        <TextBlock Text="NT$" />
    </arc:Input.Prefix>
    <arc:Input.Suffix>
        <TextBlock Text="TWD" />
    </arc:Input.Suffix>
</arc:Input>
```

## API 參考

<!-- api: Input -->

## 無障礙

- `Label` 是欄位的名稱。`IsLabelVisible="False"` 時標籤不顯示，但仍作為名稱。
- 輔助文字與錯誤提示合併為欄位的說明文字。
- 帶動畫的文字對輔助技術隱藏，輔助技術讀取的是一份純文字副本。
- 游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。

## 動態效果

- 輔助文字列與錯誤列以平滑彈簧展開高度；變化的詞帶著模糊升起並變清晰，數字逐位滾動。
- 減少動態效果時，訊息列直接以完整高度出現，換詞改為瞬間淡入；關閉動態效果時直接到最終狀態。
- 懸停與聚焦只加深邊框顏色，尺寸不變。

## 相關元件

- 多行文字用 `Textarea`，密碼用 `PasswordField`，搜尋用 `SearchField`。
- `Field` 與 `FieldFrame` 為任何編輯器或觸發器提供相同的標籤、外框與訊息列。
