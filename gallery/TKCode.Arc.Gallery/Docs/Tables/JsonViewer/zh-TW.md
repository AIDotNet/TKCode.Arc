---
title: JSON 檢視器
summary: 可摺疊的 JSON 樹狀結構，支援搜尋、長陣列分頁，以及複製值或路徑。
source-hash: 5fc22f22ccd6
---

<Demo name="WebhookPayload" />

## 何時使用

- 檢視 API 回應、Webhook 酬載、日誌項目或設定檔。
- 文件較大，使用者需要搜尋鍵或值，並只展開所需的分支。
- 展示服務資料的開發者工具與除錯面板。

## 何時不用

- 將整份 JSON 文件作為可複製的原始碼呈現：用 `CodeBlock`。
- 使用者需要選取的資料夾、檔案或其他巢狀分類：用 `TreeView`。
- 非 JSON 的值，例如任意 .NET 物件：請先將其轉換為 `JsonNode` 或 `JsonElement`。

## 用法

使用 `Json` 傳入 JSON 文字。如果資料已經解析，可用 `Value` 傳入 `JsonNode`，或用 `Element` 傳入 `JsonElement`。

```xml
<arc:JsonViewer Json="{Binding ResponseJson}"
                ExpandDepth="2"
                MaxTreeHeight="320" />
```

### 已解析的資料

如果資料已經是 `JsonNode`，請繫結 `Value`。在程式碼中請使用 `JsonNode` 的多載來建立 `JsonNode` 值，這種方式對 AOT 是安全的。

```csharp
var items = new JsonArray();
items.Add((JsonNode)new JsonObject { ["id"] = 1, ["sku"] = "SKU-0001", ["inStock"] = true });

viewer.Value = new JsonObject
{
    ["count"] = 1,
    ["items"] = items
};
```

如果文字不是有效的 JSON，`ParseError` 會保存錯誤訊息。

### 展開

`ExpandDepth` 設定初始時展開的層級數。預設僅展開根節點。`ExpandedPaths` 列出已展開的分支，可在程式碼中讀取與設定。

```csharp
viewer.ExpandAll();
viewer.CollapseAll();
viewer.Expand("root.data.lines");
viewer.Collapse("root.data.customer");
```

### 搜尋

`IsSearchable` 預設開啟。`Query` 會篩選鍵與值，展開包含相符項目的分支，並在 `MatchCount` 中回報相符數量。

<Demo name="Search" />

```xml
<arc:JsonViewer Json="{Binding Payload}"
                Query="{Binding SearchText, Mode=TwoWay}"
                SearchPlaceholder="搜尋鍵和值" />
```

```csharp
viewer.NextMatch();
viewer.PreviousMatch();
```

### 長陣列

`PageSize` 設定分支一次顯示的子項目數量。「顯示更多」列會載入下一頁。

<Demo name="LongArrays" />

```xml
<arc:JsonViewer Json="{Binding LargeCatalog}" PageSize="50" />
```

### 路徑與複製

`RootName` 是每條路徑的前綴，例如 `root.users[0].name`。`IsPathVisible` 顯示路徑列，`IsCopyable` 啟用複製功能。`CurrentRowChanged` 回報取得焦點的列，`Copied` 回報值或路徑已被複製。

```csharp
viewer.RootName = "payload";
viewer.Copied += (_, e) => status.Text = $"已複製 {e.Kind}，位於 {e.Path}";
viewer.CurrentRowChanged += (_, e) => current.Text = $"{e.Path} ({e.ValueType})";
```

### 窄寬度

寬度為 420px 及以下時，縮排會收緊，路徑列不再顯示類型。

<Demo name="Narrow" />

## API 參考

<!-- api: JsonViewer -->

## 無障礙

- 樹狀結構遵循 WAI-ARIA 樹狀模式：每一列都會公開其層級、在同層級中的位置，以及展開或選取狀態。
- Tab 順序中只有一列。朗讀某一列時，會讀出其鍵與值，或其類型與項目數。
- 方向鍵、Home 與 End 在各列之間移動。Enter 或 Space 會展開分支或載入下一頁。
- 按 Slash 將焦點移到搜尋方塊。Enter 與 Shift + Enter 逐一切換相符項目，Escape 清除搜尋。
- 相符數量位於禮貌區域中。複製結果會在狀態區域中播報。
- 列內的複製按鈕不在 Tab 順序中。使用 Ctrl/Cmd + C 複製目前的值，加上 Shift 則複製路徑。

## 動態效果

- 分支展開時，每一列的高度以平滑彈簧展開。載入分頁與全部展開使用相同的動態效果。
- 目前列的反白在各列之間滑動，樹狀結構會在反白穩定後捲動到該列。
- 複製圖示以輕微縮放與模糊轉變為勾號或叉號。
- 減少動態效果時，列的變更立即生效，反白與捲動直接移到目標位置。關閉動態效果時直接到最終狀態。

## 相關元件

- `TreeView`：用於使用者選取而非檢視的巢狀記錄。
- `CodeBlock`：將整份 JSON 呈現為可複製的原始碼。
- `CopyButton`：複製樹狀結構之外的單一值。
