---
title: Newsletter signup
summary: 以一疊過往期數構成的電子郵件訂閱表單；訂閱後，寄給你的下一期會放到最上面。
source-hash: f68abc0e5c81
---

<Demo name="Preview" />

## 何時使用

使用 `NewsletterSignup` 收集電子報或候補名單的電子郵件地址，適用於電子報本身就是最好的訂閱理由的情境。在表單旁邊，它會顯示最近幾期的一疊期數，讓訪客在留下地址之前就能看到會收到什麼內容。當地址被接受後，寄給該訪客的下一期會放到這疊的最上面，讀者人數也會加一。

- **單頁行銷區段**：適用於設計、工程或產品電子報，文案與表單放在一側，期數疊放在另一側。
- **獨立的卡片**：放在頁尾、設定頁或對話方塊中，期數疊放在表單上方的托盤裡。
- **候補名單**：只需要表單即可的情境（關閉期數疊與讀者人數）。

## 何時不用

- **建立帳號或登入**：請使用帶有密碼與自有驗證規則的表單。
- **沒有期數可展示的電子報**：請使用網站頁尾區塊中的頁尾訂閱，那是單行的表單。
- **多個欄位**（姓名、主題、頻率）：此區塊只收集一個電子郵件地址。

## 用法

```xml
<arc:NewsletterSignup x:Name="Signup" />
```

```csharp
Signup.SubscribeAsync = async (email, token) =>
{
    await http.PostAsJsonAsync("/subscribe", new { email }, token);   // throw to show the error and keep the address
};
Signup.Subscribed += (_, e) => analytics.Track("newsletter", e.Email);
```

未設定任何內容時，區塊會顯示 Arc 的預設文案、刊物「Margins」與 12,480 位讀者，並模擬寄送（1100 ms，一律成功）。請將 `SubscribeAsync` 換成呼叫你的郵件服務商：回傳即顯示成功狀態，拋出例外則顯示失敗狀態。區塊被重設或移除時，權杖會被取消。

### 訂閱流程

| 階段 | 顯示內容 |
|---|---|
| `Idle` | 空白欄位、「Subscribe →」與隱私說明。寄送失敗後，按鈕會顯示「Try again →」，地址會保留。 |
| `Sending` | 欄位變為唯讀，按鈕顯示圓環與「Subscribing」。之後的按下會被忽略。 |
| `Done` | 按鈕變為綠色並出現勾號與「Subscribed」，確認訊息取代隱私說明，那疊期數的最上面會放上下一期。 |

`Phase` 與 `SubscribedEmail` 是唯讀屬性。`Reset()`（「Use a different email」按鈕會呼叫它）會清除地址，並回到 `Idle`。

### 驗證

空白欄位與格式錯誤的地址各有自己的訊息（`EmptyMessage`、`FormatMessage`）。欄位有文字而失去焦點時開始驗證；嘗試送出後，每次按鍵都會重新驗證。在此之前，輸入文字會清除錯誤。驗證失敗時膠囊形欄位會晃動，焦點保留在欄位內。

### 樣式變體

`Variant` 為 `Inline`（預設）或 `Card`。

<Demo name="Card" />

```xml
<arc:NewsletterSignup Variant="Card" />
```

### 刊物

`Publication` 包含名稱、`Upcoming` 期與 `Recent` 期（由新到舊）。最近的前三期組成那疊期數。每個 `NewsletterIssue` 有編號（即其識別值）、日期列、主旨，以及最多三個 `NewsletterStory` 項目，每項都有標題、縮圖 `IImage` 與閱讀時間。讀者頭像來自 `ReaderFaces`。

```csharp
var publication = new NewsletterPublication { Name = "Margins", Upcoming = next };
publication.Recent.Add(issue148);
publication.Recent.Add(issue147);
signup.Publication = publication;
signup.ReaderFaces.Add(faceBitmap);
```

未設定刊物時，區塊會使用 Arc 的範例刊物「Margins」，其內容沒有縮圖。將 `IsPublicationVisible` 或 `IsReadersVisible` 設為 false，可隱藏期數疊或讀者資訊。

### 僅表單

<Demo name="FormOnly" />

沒有期數疊與讀者資訊時，區塊會變成置中、寬度至多 720 px 的單欄。

### 窄版容器

<Demo name="Narrow" />

區塊依自己的寬度排版，而非視窗的寬度。寬度 860 px 或以下時改為單欄。寬度 520 時收緊間距與字級；寬度 400 時隱藏閱讀時間，且已寄達指定收件人的期數只顯示收件人；寬度 360 時讀者資訊列可能換行。表單寬度低於 300 px 時，欄位與按鈕會上下堆疊，兩者都撐滿寬度。

### 文字與在地化

每個字串都是一個屬性，並附有 Arc 的英文預設值：`Placeholder`、`ButtonLabel`、`PrivacyNote`、`PrivacyLinkLabel`、`EmailLabel`、`RetryLabel`、`SendingLabel`、`DoneLabel`、`EmptyMessage`、`FormatMessage`、`FailedMessage`、`DoneMessage`、`DifferentEmailLabel`、`RecipientFormat`、`IssueFormat`、`MinutesFormat`、`ReadersWord`、`IncludingYou` 與 `SentToFormat`。

### 隱私連結

按下它會引發 `Navigate`（並執行 `NavigateCommand`）。當事件未被處理，且 `PrivacyLinkHref` 是絕對的 `http`、`https` 或 `mailto` 位址時，會以平台的啟動程式開啟。將 `IsPrivacyLinkVisible` 設為 false 可隱藏此連結。

## API 參考

<!-- api: NewsletterSignup -->

<!-- api: NewsletterPublication -->

<!-- api: NewsletterIssue -->

<!-- api: NewsletterStory -->

<!-- api: NewsletterSubscribedEventArgs -->

<!-- api: NewsletterNavigateEventArgs -->

## 無障礙

- 區塊是一個以標題命名的區域。欄位有隱藏標籤 `EmailLabel` 與電子郵件輸入提示。
- 錯誤會立即朗讀；確認訊息以禮貌模式朗讀，並包含連結指向的位置。
- 螢幕閱讀器只會讀到最前面的期數（「Margins, issue 148」）。後面的期數、縮圖與讀者頭像都是裝飾性的。
- 按鈕與連結的鍵盤焦點環位於其外側 2 px。懸停樣式只套用於滑鼠或筆。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在欄位、按鈕與連結之間移動。已訂閱的按鈕會離開 Tab 順序。 |
| 欄位中的 `Enter` | 檢查並送出地址（輸入法組字中會被忽略）。 |
| 按鈕上的 `Enter` / `Space` | 按下該按鈕。 |

寄送成功後，焦點會移到「Use a different email」；呼叫 `Reset()` 後，焦點會回到欄位。

## 動態效果

- 訂閱時，下一期會放到最上面：它從下方升起到期數疊上，其餘各期則向後退並縮小（無過衝的彈簧），最舊的一期則向後落下消失。
- 按鈕的文字與訊息行會以短暫的模糊就地交換；勾號會自行描繪出來。
- 被拒絕的地址或寄送失敗時，膠囊形欄位會晃動。讀者人數滾動加一，「, including you」的寬度會展開。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 期數交換位置 | 位置與縮放採彈簧，透明度在 120 ms 內變化 | 立即就位，透明度在 160 ms 內變化 | 立即 |
| 文字與訊息交換 | 上升並模糊，進入 240 ms、離開 160 ms | 淡入淡出，120 ms | 立即 |
| 晃動 | 360 ms | 無 | 無 |
| 勾號 | 340 ms 內描繪 | 已描繪 | 已描繪 |

### 響應式行為

斷點依區塊的寬度計算：860、520、400 與 360 px，另加表單本身的 300 px。

## 相關元件

- 網站頁尾區塊，適用於頁尾中的單行電子報欄位。
- CTA 區段區塊，適用於導向訂閱頁面的行動呼籲。
- `EmptyStates`，適用於帶有可重試動作的空白檢視。
