---
title: AI search bar
summary: 一個能用自然語言作答的搜索框，答案顯示在常規結果之上，並帶有推薦問題、編號引用、來源卡片和可以接著問的問題。
source-hash: 49c2cccfb2fd
---

<Demo name="Preview" />

## 何時使用

`AiSearchBar` 是一個也能回答問題的搜索框。還沒輸入時，它會給出幾個推薦問題。提出問題後，最先出現的是一段簡短的自然語言回答，文中帶有小小的編號引用，指向所用的來源；接著是來源卡片，然後是可以接著問的問題，最後是常規的結果列表。人們既能得到答案，也能查看答案的出處，或自己打開某個結果。

- **幫助中心和員工手冊。** 像 "How many vacation days do new hires get?" 這樣的問題，值得一句話的回答，而不是十個連結。
- **工作區搜索。** 在文檔、工作單和知識庫中搜索，答案在上，文檔在下。
- **產品文檔。** 回答操作類問題，相關頁面一鍵可達。

## 何時不用

- **對話。** 需要層層追問時，使用 `AI chat` 區塊。
- **單純的篩選。** 按名稱或標簽縮小列表時，使用 `SearchField` 或 `Search results`。
- **沒有來源的回答。** 本區塊圍繞引用設計；如果回答無法引用任何內容，聊天回覆會更誠實。

## 用法

本區塊派生自 Avalonia 的 `TemplatedControl`。回答來自 `AnswerProvider`；不設定時，搜索框會根據一份虛構的手冊作答（`AiSearchBarSamples`），因此演示可以獨立執行。

```xml
<arc:AiSearchBar SuggestedQuestions="{Binding Questions}" />
```

```csharp
search.AnswerProvider = async (query, cancellation) =>
{
    var result = await assistant.AskAsync(query, cancellation);
    return new AiSearchAnswer
    {
        Text = result.Text,                 // "... from their first day [1]."
        Sources = result.Sources.Select(s => new AiSearchSource { Title = s.Title, Location = s.Path, Link = s.Url }).ToArray(),
        Results = result.Documents.Select(d => new AiSearchResult { Title = d.Title, Snippet = d.Snippet, Link = d.Url }).ToArray(),
        FollowUps = result.FollowUps,
    };
};
search.SourceInvoked += (_, e) => Open(e.SearchSource.Link);
search.ResultInvoked += (_, e) => Open(e.Result.Link);
```

### 結構

- **輸入框**：`Query`（雙向）、Ask 按鈕，以及有內容可清除時出現的清除按鈕。
- **推薦**（`:idle`）：`SuggestedQuestions` 顯示為小標籤；按下即提問。
- **搜索中**：一行閃光的提示和若干預留位置行。
- **回答**（`:answered`）：正文中寫著 `[1]`、`[2]` 的位置變成引用按鈕；來源顯示為帶編號的卡片；追問顯示為小標籤；結果顯示為帶標題、位置和摘要的行。
- **失敗**（`:failed`）：異常的消息和一個重試按鈕。

### 自己的回答

<Demo name="OwnAnswers" />

`State`、`Answer`、`AskedQuery` 和 `ErrorText` 說明搜索框當前所處的狀態。`SearchAsync(query)` 從程式碼提問，`Cancel()` 停止正在進行的搜索，`Clear()` 清空所有內容。新的問題會取消仍在進行的問題，只顯示最新的回答。

### 文字

`Labels`（`AiSearchBarLabels`）包含預留位置文字、按鈕、各個標題、搜索中和出錯的提示，以及引用和結果數量的讀屏名稱。

## API 參考

<!-- api: AiSearchBar -->

<!-- api: AiSearchAnswer -->

<!-- api: AiSearchSource -->

<!-- api: AiSearchResult -->

<!-- api: AiSearchBarState -->

<!-- api: AiSearchBarLabels -->

## 無障礙

- 搜索框是一個搜索地標；輸入框以 `Labels.Field` 命名。
- **Enter** 提問，**Escape** 停止正在進行的搜索或清空搜索框，**Down** 從輸入框移到第一個推薦、來源或結果。
- 每個引用都是一個按鈕，命名為 "Source 2: Travel and expenses"；每張來源卡片使用同樣的名稱，每個結果以標題命名、以摘要說明。
- "Reading your workspace…"、"Answer ready, 4 results" 和錯誤都會禮貌地朗讀。

## 動效

- 回答和推薦出現時在 160 ms 內淡入；等待期間搜索中的提示會閃光。
- 小標籤、卡片和行在停留時於 160 ms 內變色。
- 減弱動效和關閉動效時各個狀態立即顯示；閃光遵循它自己的動效規則。

## 響應式

- 搜索框最寬 720，在更窄的容器中占滿寬度。寬度不超過 440 時（`:compact`）內邊距從 20 收緊到 16。
- 小標籤和來源卡片會換行；來源卡片最寬 260，文字以省略號截斷。
- 結果行的標題和位置保持在一行，摘要換行顯示。

## 相關元件

- `SearchField` 和 `ExpandingSearch`，用於不需要回答的搜索。
- `AI answer`，用於對一個問題給出像文章一樣的長回答。
- `Search results`，帶篩選的完整結果頁。
