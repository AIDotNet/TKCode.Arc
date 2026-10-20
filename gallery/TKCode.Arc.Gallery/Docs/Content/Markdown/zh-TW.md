---
title: Markdown
summary: 以 Arc 的風格渲染 Markdown 文件與串流回覆，程式碼區塊使用 Arc 的語法配色高亮。
source-hash: 25cd452aaa53
---

## 何時使用

- 以 Markdown 形式儲存的版本說明、說明文字與文件。
- 來自模型或其他服務的串流回答，需要在文字抵達時即時顯示。
- 來源不可控的富文字，需要確保標題、清單、表格與連結的外觀一致。

## 何時不用

- 附複製按鈕的單一程式碼區塊：用 `CodeBlock`。
- 需要自身捲動的長文件：將控制項包裹在 `ScrollViewer` 中，或在長對話中使用 StreamMark 的 `TranscriptView`。
- 原地編輯 Markdown 原始碼：用 `Textarea`。

## 用法

靜態文字透過 `Text` 設定。文件高度隨內容增長，內部沒有捲動。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Markdown Text="## 版本說明&#10;&#10;Arc **0.4** 新增 `Markdown`。" />
</UserControl>
```

### 繫結文件

將 `Text` 繫結到檢視模型屬性。設定該屬性會取代目前仍在顯示的回覆。

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" FontSize="14" BlockSpacing="10" />
```

### 串流回覆

每個資料區塊抵達時呼叫 `Append`，回覆結束時呼叫 `Complete`。文字會依節奏逐步顯示，新詞以淡入方式出現。`IsStreaming` 在節奏化的待顯示內容全部呈現之前保持為 `true`。

```csharp
markdown.Append(chunk);   // 權杖抵達時呼叫，在 UI 執行緒上執行
markdown.Complete();      // 回覆結束時呼叫
```

`Complete` 之後再呼叫 `Append` 會開始新的回覆。若由你自行建立串流，則可以改為設定 `Stream`，例如交給工作執行緒透過 `MarkdownStream.Push` 寫入。

### 聊天尺寸

訊息氣泡可使用較小的 `FontSize`。`LineHeight` 預設為字級的 1.65 倍，程式碼字級隨之變化。

```xml
<arc:Markdown FontSize="14"
              Text="已將 **web-42** 部署到 `staging`。&#10;&#10;```sh&#10;kubectl rollout status deploy/web&#10;```" />
```

### 連結

絕對路徑的 `http`、`https` 與 `mailto` 連結會透過系統啟動器開啟。處理 `LinkClicked` 即可接管，並將事件標記為已處理以阻止預設行為。設定 `OpensLinks="False"` 可關閉預設行為。

```csharp
markdown.LinkClicked += (_, e) =>
{
    e.Handled = true;
    OpenInsideApp(e.Url);
};
```

## API 參考

<!-- api: Markdown -->

## 無障礙

- 文字可以選取並複製。連結帶底線，並使用強調色。
- 程式碼區塊使用與 `CodeBlock` 相同的配色。顏色用於輔助原始文字，從不取代原始文字。
- 在串流回覆尚未結束時，使用 `IsStreaming` 顯示狀態提示。

## 動態效果

- 串流回覆中的新文字在 240 毫秒內淡入。節奏控制會將文字分散開來，避免成批湧現。
- 減少動態效果時，淡入縮短為 160 毫秒，且只使用不透明度變化。關閉動態效果時，新文字立即顯示，沒有淡入。
- 節奏控制在所有動態效果等級下都生效。它控制文字何時出現，而不是一段動畫。

## 相關元件

- `CodeBlock`：單獨展示一段程式碼範例。
- `Textarea`：使用者需要編輯多行文字時使用。
