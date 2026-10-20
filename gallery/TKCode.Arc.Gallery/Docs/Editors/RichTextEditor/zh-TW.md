---
title: 富文字編輯器
summary: 輕量編輯器，支援 Markdown 快速輸入、浮動工具列、斜線選單，並可輸出 HTML 與 Markdown。
source-hash: 26be0d955878
---

## 何時使用

- 需要粗體、斜體、清單、引言與程式碼區塊的留言、筆記、貼文以及中短篇文件。
- 以 HTML 或 Markdown 儲存、並能重新讀回編輯器的內容。
- 需要編輯器自身復原歷程的寫作情境，該歷程與平台的復原相互獨立。

## 何時不用

- 不需要格式的純多行文字：用 `Textarea`。
- 在文字中提及人員或標籤：用 `MentionInput`。
- 在頁面上原地編輯的單行文字：用 `InlineEdit`。
- 原始程式碼或唯讀程式碼清單：用 `CodeBlock`。

## 用法

透過 `Markdown` 設定初始內容。`Markdown` 與 `Html` 都是雙向的：為其中任一項指派值都會重新載入編輯器，且兩者都會跟隨每次編輯更新。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RichTextEditor Markdown="## 筆記&#10;&#10;邊寫邊輸入 **Markdown**，或輸入 `/` 插入區塊。" />
</UserControl>
```

### 預留位置文字與區塊提示

`Placeholder` 在空白編輯器中顯示。`BlockHint` 顯示在放置插入點的空白行上。

```xml
<arc:RichTextEditor Placeholder="寫下留言"
                    BlockHint="輸入 / 插入區塊" />
```

### 唯讀

設定 `IsReadOnly` 後，編輯、工具列與快速鍵都會關閉。文字仍可選取並複製。

```xml
<arc:RichTextEditor IsReadOnly="True"
                    Markdown="## 發佈檢查清單&#10;&#10;1. 凍結分支&#10;2. 執行 **完整** 測試套件" />
```

### 讀取內容

每次編輯後都會執行 `ContentChanged`。其 `Value` 包含 `Html`、`Markdown`、`Text` 與 `IsEmpty`。方法 `GetHtml()`、`GetMarkdown()` 與 `GetText()` 可隨時讀取內容。

```csharp
editor.ContentChanged += (_, e) =>
{
    draft.Markdown = e.Value.Markdown;
    canPost = !e.Value.IsEmpty;
};

var html = editor.GetHtml();
```

### 自訂工具列

`Formats` 回報插入點處的標記與區塊類型，`CanUndo` 與 `CanRedo` 回報復原歷程的狀態。可用它們設定自己按鈕的狀態。`ToggleMark`、`SetBlockType`、`SetLink` 與 `EditLink` 用於驅動編輯器。

```csharp
editor.PropertyChanged += (_, e) =>
{
    if (e.Property == RichTextEditor.FormatsProperty)
    {
        boldButton.Variant = editor.Formats.IsBold ? ButtonVariant.Secondary : ButtonVariant.Ghost;
    }
};

boldButton.Click += (_, _) => editor.ToggleMark(RichTextMark.Bold);
headingButton.Click += (_, _) => editor.SetBlockType(RichTextBlockType.Heading2);
```

將 `UndoCommand` 與 `RedoCommand` 繫結到按鈕，或直接呼叫 `Undo()` 與 `Redo()`。

## API 參考

<!-- api: RichTextEditor -->

## 無障礙

- 編輯區是一個多行文字控制項。預設名稱為「編輯器」；設定 `AutomationProperties.Name` 可為其在表單中命名。
- 鍵盤：Mod+B、Mod+I、Mod+Shift+X、Mod+E 與 Mod+K 套用標記與連結。Mod+Z 與 Mod+Shift+Z 復原與重做。Alt+F10 將焦點移入浮動工具列，按 Escape 則返回文字。
- 編輯結果如「已套用格式」「已新增連結」和「已復原」以禮貌方式朗讀。
- 限制：編輯器不向螢幕閱讀器提供字元或行的導覽。它們會將整段文字當成一個值讀取。

## 動態效果

- 浮動工具列會滑動以跟隨選取範圍。在格式按鈕與連結欄位之間切換時，工具列尺寸會變形，兩個介面相互滑過。
- 區塊選單從 `/` 所在位置開啟，清單被篩選時，醒目提示會在各列之間滑動。
- 減少動態效果時，工具列的移動與尺寸調整一步完成，兩個介面之間交叉淡化。區塊選單只淡入淡出，不縮放也不移動。

## 相關元件

- `Textarea`：不含格式的純文字。
- `MentionInput`：提及人員或標籤的文字。
- `CodeBlock`：用於顯示程式碼，而非撰寫程式碼。
