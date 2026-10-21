---
title: 提及輸入框
summary: 支援 @人員 與 #頻道 提及的多行文字方塊，提及作為整體權杖處理，並在插入點處顯示建議。
source-hash: 353fdfba5bf8
---

<Demo name="CommentBox" />

## 何時使用

- 留言與回覆方塊，其中提及需要透過 id 通知某人。
- 聊天與工作編輯器，需要在正文中連結頻道或專案。
- 將提及儲存為結構化資料而非純文字的編輯器。

## 何時不用

- 不含參照的一般多行文字：用 `Textarea`。
- 不屬於句子的值清單：用 `TagInput`。
- 文字中的標題、清單與連結：用富文字編輯器。
- 名稱或搜尋詞等單行文字：用 `Input` 或 `SearchField`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MentionInput Label="留言"
                      Placeholder="寫下留言。輸入 @ 提及，輸入 # 選擇頻道"
                      MinLines="1"
                      MaxLines="8" />
</UserControl>
```

此欄位沒有可見的標籤。`Label` 只為輔助技術提供欄位名稱。

### 人員與頻道

`People` 與 `Channels` 提供建議清單。將其留空即可關閉對應的觸發字元。每個人員與頻道都有 id、名稱以及選用的詳細資訊。

```csharp
var people = new[]
{
    new MentionPerson("u_maya", "Maya Chen") { Role = "產品設計師" },
    new MentionPerson("u_jordan", "Jordan Lee") { Role = "工程經理" }
};

var channels = new[]
{
    new MentionChannel("c_design", "design") { Description = "評審、審閱與交接", Members = 48 }
};

var comment = new MentionInput
{
    Label = "留言",
    People = people,
    Channels = channels,
    MaxSuggestions = 6
};
```

### 預填值

`Value` 是一個 `MentionValue`：純文字加上各提及及其位移量。在提及周圍編輯會移動它，在提及內部編輯則會將其移除。

<Demo name="Prefilled" />

```csharp
var text = "Thanks @Maya Chen. Can @Jordan Lee review it in #design?";
comment.Value = new MentionValue(text,
[
    new Mention(MentionKind.Person, "u_maya", "Maya Chen", 7, 17),
    new Mention(MentionKind.Person, "u_jordan", "Jordan Lee", 24, 34),
    new Mention(MentionKind.Channel, "c_design", "design", 47, 54)
]);
```

### 按 Enter 送出

啟用 `IsSubmitOnEnter` 後，Enter 送出，Shift+Enter 換行。輸入法正在組字時，Enter 永遠不會送出。

<Demo name="SubmitOnEnter" />

```csharp
message.IsSubmitOnEnter = true;
message.Submitted += (_, e) =>
{
    Console.WriteLine($"{e.Value.Text} ({e.Value.Mentions.Count} mentions)");
    message.Clear();
};
```

### 尺寸與位置

`MinLines` 與 `MaxLines` 設定高度範圍。`SuggestionPlacement` 設定清單的展開位置：`Auto` 選擇空間較大的一側，`Top` 與 `Bottom` 則強制指定一側。

<Demo name="Growth" />

<Demo name="Placement" />

### 命令與事件

`Insert`、`OpenSuggestions` 與 `Clear` 從程式碼操作欄位。`ValueChanged` 與 `MentionAdded` 回報編輯變化。

```csharp
button.Click += (_, _) => comment.OpenSuggestions(MentionKind.Person);

comment.MentionAdded += (_, e) => Console.WriteLine($"Added {e.Mention.Text}");
```

### 停用

整個欄位會淡化到 55%，且不接受任何輸入。

<Demo name="Disabled" />

### 序列化輸出

`MentionSerializer.Serialize` 會將值轉換為儲存文字：人員為 `<@id>`，頻道為 `<#id>`。

<Demo name="Serialized" />

## API 參考

<!-- api: MentionInput -->
<!-- api: MentionPerson -->
<!-- api: MentionChannel -->

## 無障礙

- 文字方塊是帶清單自動完成的組合方塊，名稱由 `Label` 提供。
- 建議清單名為 People 或 Channels。結果數量（例如 "3 people" 或 "No matches"）會以禮貌方式播報。
- 提及的顏色繪製在隱藏的圖層上，因此輔助技術讀取的是純文字。Escape 僅關閉建議清單。

## 動態效果

- 欄位隨內容變化在 `MinLines` 與 `MaxLines` 之間伸縮。
- 建議面板從插入點以輕微縮放展開，反白在各列之間滑動。
- 結果數量變化時，面板高度以彈簧效果調整。
- 減少動態效果時，高度與反白直接跳轉，面板僅淡入。關閉動態效果時立即顯示或隱藏。

## 相關元件

- `Textarea`：一般多行文字。
- `TagInput`：不屬於句子的短值清單。
- `Input`：單行輸入。
