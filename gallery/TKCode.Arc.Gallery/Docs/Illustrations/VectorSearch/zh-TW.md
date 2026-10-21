---
title: 向量搜尋
summary: 問題會被嵌入為向量，並透過餘弦相似度找出最相近的文件作為答案。
source-hash: 68b956871ae1
---

<Demo name="HelpCentreSearch" />

## 何時使用

- 說明語意搜尋如何找到與問題沒有任何相同字詞、卻仍然相關的答案。
- 說明中心和產品文件，展示問題如何與文章進行比對。
- 講解嵌入、餘弦相似度與 top-k 清單如何配合。

## 何時不用

- 在應用程式中回答真實使用者的問題：內建嵌入是雜湊字詞與三元組，而不是訓練好的模型，因此應使用你自己的搜尋服務，並用 `SearchField` 呈現其結果。
- 精確或帶篩選條件的查詢，例如訂單編號：用一般索引搭配 `SearchField`。
- 繪製真實量測值：用圖表分類中的 `LineChart`。

## 用法

故事會輸入一個問題，將其嵌入，找出最相近的文件，並列出它們及其相似度。內建索引包含四個主題下的 32 篇說明中心文章，以及三個範例問題。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:VectorSearch />
</UserControl>
```

### 答案數量

`TopK` 設定列出的答案數量（1 到 5，預設 3）。清單依相關程度由高到低排列，地圖會圈出最遠的那個答案。

```xml
<arc:VectorSearch TopK="4" />
```

### 你自己的文件與問題

`Documents` 接受 `VectorSearchDocument` 值（標題、主題和可選的關鍵字）。`Queries` 接受故事循環使用的問題。兩者設定後都會取代預設值。

<Demo name="YourDocuments" />

### 搜尋某個問題

`Search(query)` 會立即顯示一個問題，之後故事繼續播放它自己的問題。`Query` 是螢幕上顯示的問題，`Results` 保存其答案。答案確定時會觸發 `ResultsChanged`。

<Demo name="AskYourOwn" />

每個 `VectorSearchResult` 都包含 `Document`、`Score`（餘弦相似度）和 `Rank`（1 表示最相符）。

### 標籤

`Labels` 接受一個 `VectorSearchLabels` 物件，用於設定可見的文字：模型名稱、清單標題和步驟說明文字。

```csharp
search.Labels = new VectorSearchLabels { Model = "文字模型", Header = k => $"最佳 {k} 個相符結果" };
```

### 減少動態效果

在減少動態效果下，插圖是第一個問題答案的靜態畫面。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: VectorSearch -->

<!-- api: VectorSearchLabels -->

## 無障礙

- `Label` 是無障礙名稱，目前的說明文字會作為說明文字提供給輔助技術。
- 插圖可取得焦點，焦點環寬 2px。
- 空白鍵或 Enter 播放與暫停故事，R 鍵從頭開始播放。
- 說明文字的變化不會被播報。排名後的答案也可透過 `Results` 以文字形式取得，因此應將其呈現在使用者可以閱讀和瀏覽的清單中。

## 動態效果

- 問題會以帶閃爍游標的方式逐字輸入，然後嵌入模型開始處理。
- 向量條逐格填滿，查詢點飛入文件地圖。
- 半徑不斷擴大直到觸及最後一個答案，答案依排名依序點亮，其所在列向上浮入清單。
- 減少動態效果和關閉動態效果時，顯示第一個問題答案的靜態畫面，沒有游標和逐字輸入。播放按鈕會隱藏。

## 相關元件

- `SearchField`：應用程式中的問題輸入框，也用於呈現你自己的結果。
- `DataFlow`：搜尋所依據的文件如何被收集與儲存。
- `SortableDataTable`：以分數呈現長清單結果。
