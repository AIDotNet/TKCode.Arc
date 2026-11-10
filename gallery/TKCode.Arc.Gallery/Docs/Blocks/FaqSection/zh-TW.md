---
title: 常見問題區塊
summary: 以手風琴、主題導覽或可搜尋並醒目標示相符項的清單呈現的常見問題。
source-hash: 40b5de4e9ee5
---

<Demo name="Preview" />

## 何時使用

在大家開口之前就回答那些反覆出現的問題。`FaqSection` 接受一組附純文字答案的問題，並按數量選用合適的版面。

- **定價頁或產品頁上的短清單**：`Accordion`，一個置中的單欄。
- **能按主題歸類的問題**：`Columns`，一列附數量的主題，旁邊是所選主題的問題。
- **大家會去搜尋的長清單**：`Search`，一個隨輸入篩選的搜尋框，並醒目標示找到的內容，即使它在未展開的答案裡。

## 何時不用

- **不是問題清單的摺疊內容**：請使用 `Accordion`。
- **答案需要程式碼、圖片或很長的結構**：請使用文件頁面。
- **上千個問題**：搜尋在每次按鍵時在記憶體中執行，適合幾十個問題。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:FaqSection Variant="Search" Title="Questions about billing">
        <arc:FaqSection.Items>
            <arc:FaqItem Question="Can I cancel at any time?" Answer="Yes. Your plan runs until the end of the period." />
            <arc:FaqItem Question="Do you offer refunds?" Answer="Annual plans can be refunded within 30 days." />
        </arc:FaqSection.Items>
    </arc:FaqSection>
</UserControl>
```

`FaqSection` 衍生自 `TemplatedControl`。`Items` 為空時顯示範例問題，上線前請替換。`Answer` 是純文字，搜尋會在其中比對和醒目標示。

### 版面

| `Variant` | 顯示內容 | 適合 |
|---|---|---|
| `Accordion` | 標題、問題和聯絡列排成一個置中的單欄，最寬 720 | 大約十個以內的問題 |
| `Columns` | 標題、主題和聯絡列，旁邊是所選主題的問題 | 附 `Category` 的問題 |
| `Search` | 搜尋框和即時計數，下方是一個篩選後的清單 | 長清單 |

### 展開的問題

`OpenItems` 儲存展開問題的 id，並且是雙向的。預設展開第一個問題，展開另一個時它會收合。設定 `IsMultiple` 可同時展開多個。id 是 `FaqItem.Id`，或者問題的文字。如果問題可能改寫，請提供穩定的 `Id`，這樣狀態不會因修改而遺失。

```csharp
faq.OpenItems = ["refunds", "seats"];
```

<Demo name="OwnQuestions" />

### 主題與搜尋

`SelectedCategory` 是 `Columns` 中選取的主題，`Query` 是 `Search` 中的搜尋文字，兩者都是雙向的。沒有 `Category` 的問題歸入「General」。

在 `Search` 中，問題的文字或答案相符時就會留在清單裡。只在答案中相符的未展開問題會顯示一小段摘錄，讓人看到它出現的原因。比對時忽略大小寫和重音：「cafe」能找到「Café」。沒有相符項時，清單的位置顯示一行說明和一個清除搜尋的按鈕。`TotalFormat`、`ResultsFormat`、`OneResultFormat`、`NoResultsFormat` 和各標籤屬性用於翻譯這些文字。

<Demo name="Searchable" />

### 聯絡列

清單下方的這一列提供聯絡真人的途徑。`Contact` 接受 `FaqContact`，包含 `Label`、`Description`，以及 `Href` 或 `Command`。按下時引發 `ContactInvoked`，執行命令，然後開啟位址，除非處理常式已將事件標記為已處理。將 `IsContactVisible` 設為 false 可隱藏這一列。

### 最佳實務

**建議**

- 按大家提問的方式寫問題，並在第一句話裡作答。
- 讓答案保持純文字，這樣搜尋才能讀取。
- 只有每個主題至少有兩個問題時才使用 `Columns`。

**避免**

- 把重要內容只放在未展開的答案裡。
- 超過大約五個主題。主題欄用於定位，而不是瀏覽。

## API 參考

<!-- api: FaqSection -->

<!-- api: FaqItem -->

<!-- api: FaqContact -->

<!-- api: FaqContactEventArgs -->

## 無障礙

- 每個問題都是以其文字命名的按鈕，說明文字指出它已展開還是已收合。
- 搜尋框有真實的標籤，計數列在變化時會被禮貌地朗讀。
- 被篩選掉的列在不顯示時離開 Tab 順序。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Up` / `Down` | 在同一清單的問題之間移動，到兩端時循環 |
| `Home` / `End` | 第一個或最後一個問題 |
| `Enter` / `Space` | 展開或收合目前的答案 |
| `Tab` / `Shift+Tab` | 在問題、主題、搜尋框和聯絡列之間移動 |

## 動效

- 答案以高度彈簧（0.4 秒，無回彈）展開，文字在 160 ms 內從 2 px 模糊中下移 6 px。收合用時 120 ms。加號以 Arc 的 CSS 彈簧在 580 ms 內轉動 45 度。
- 切換主題時，醒目區塊以 morph 彈簧移動，問題沿移動方向切換：進入 16 px，離開 12 px。
- 被篩選的列以同樣的高度彈簧收合，其餘的列隨之上移。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 答案展開 | 高度彈簧、位移和模糊 | 高度立即變化，無位移 | 立即 |
| 切換主題 | 滑動和模糊 | 立即切換 | 立即切換 |
| 搜尋篩選 | 列收合 | 列立即關閉 | 列立即關閉 |

## 響應式

- `FaqSection` 跟隨自身寬度，而不是視窗寬度。寬度達到 720 px 時，標題使用更大的字級。
- 寬度不超過 760 px（`:narrow`）時，主題欄移到問題上方，主題排成一列並可橫向捲動。
- 寬度不超過 480 px（`:compact`）時，內距收緊，答案使用全部寬度。
- 觸控：問題至少 64 px 高，懸停只改變顏色，因此沒有內容依賴懸停。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `FaqSection` | 一組問題和純文字答案 |
| `Accordion` | 不是問題清單的摺疊內容 |
| `SearchField` | 搜尋常見問題以外的內容 |
| `ContactSection` | 下一步是寫信給你 |
