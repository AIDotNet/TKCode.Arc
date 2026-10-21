---
title: 程式碼區塊
summary: 以清楚的層級呈現程式碼，並提供便捷的複製入口。
source-hash: 134c97dd9bb2
---

<Demo name="Default" />

## 何時使用

- 說明文件或設定頁面中的原始碼片段與安裝指令。
- 較長的檔案，需要摺疊為幾行，並提供「顯示全部」操作。
- 使用者需要複製的程式碼，檔名能告訴他們程式碼應放在哪裡。

## 何時不用

- 只複製一個值，例如 API 金鑰：單獨使用 `CopyButton`。
- 使用者需要依鍵名逐層瀏覽的 JSON：用 `JsonViewer`，它以樹狀呈現並支援搜尋。
- 可導覽的資料夾與檔名層級：用 `TreeView`。

## 用法

```xml
<arc:CodeBlock FileName="greeting.tsx"
               Language="tsx"
               Code="{Binding Source}" />
```

### 摺疊長程式碼

`IsCollapsible` 會新增一個切換按鈕。`MaxLines` 設定區塊摺疊時顯示的行數。

<Demo name="Collapsible" />

`IsExpanded` 為雙向繫結，表示區塊是否展開。

```csharp
block.IsCollapsible = true;
block.MaxLines = 6;
block.IsExpanded = false;
```

### 語言

`Language` 設定語法醒目提示以及標題列中的標籤。它接受常見名稱與別名，例如 `cs`、`ts`、`py`、`sh` 與 `axaml`。沒有對應語法的語言會以純文字顯示。

<Demo name="Languages" />

```xml
<arc:CodeBlock FileName="stats.py" Language="py" Code="{Binding PythonSource}" />
<arc:CodeBlock FileName="notes.txt" Language="text" Code="純文字。" />
```

### 複製與行號

複製按鈕位於標題列，複製的是 `Code`。`ShowLineNumbers` 會新增行號欄，程式碼橫向捲動時行號保持不動。

<Demo name="LineNumbers" />

```xml
<arc:CodeBlock FileName="deploy.sh"
               Language="bash"
               ShowLineNumbers="True"
               Code="{Binding Script}" />
```

```csharp
block.Copied += (_, _) => status.Text = "程式碼已複製";
```

### 標籤文字

複製標籤與摺疊文字都是屬性，因此可以直接翻譯。

```xml
<arc:CodeBlock Code="{Binding Source}"
               CopyLabel="複製程式碼片段"
               ShowAllLinesFormat="顯示全部 {0} 行"
               ShowFewerLinesLabel="收合部分行" />
```

### 切換檔案

同時設定新的 `FileName`、`Language` 與 `Code`，即可就地換入另一個檔案。檔名所在的位置會跟隨新的寬度，新的程式碼上浮出現，高度以彈簧動態跟隨。

<Demo name="SwitchingFiles" />

### 窄寬度

寬度低於 420px 時，內距與文字會縮小，複製操作只保留圖示。

<Demo name="Narrow" />

## API 參考

<!-- api: CodeBlock -->

## 無障礙

- 程式碼區塊是一個帶標籤的區域。其名稱來自 `FileName`，因此頁面上的每個區塊都有各自的名稱。
- Tab 依序移動到複製按鈕、程式碼區域與展開切換按鈕。
- Enter 或 Space 會複製程式碼或切換展開狀態，取決於目前獲得焦點的控制項。
- 程式碼區域可以獲得焦點，鍵盤使用者可透過方向鍵捲動較長的行。
- 展開切換按鈕會向輔助技術公開其展開狀態。複製結果由 `CopyButton` 播報。

## 動態效果

- 展開或收合時，程式碼的高度以平滑彈簧變化。
- 新程式碼會淡入並輕微上升，讓更新清楚可見，而不會突然跳動。
- 切換按鈕的文字以短暫的模糊切換，箭頭隨之轉動。
- 減少動態效果時，高度立即設定，文字原地切換，沒有移動。關閉動態效果時直接到最終狀態。

## 相關元件

- `CopyButton` 用於複製單一值，例如權杖或 URL。
- `JsonViewer` 用於使用者需要搜尋與瀏覽的 JSON。
- `TreeView` 可放在程式碼區塊旁邊，讓所選檔案決定顯示的程式碼。
