---
title: Invoice studio
summary: 帶實時打印預覽的發票編輯器：客戶與付款期限、包含數量、價格和稅率的明細行、按百分比或金額給出的折扣、隨每次編輯更新的合計，以及發送前的檢查。
source-hash: 6e730703f052
---

<Demo name="Preview" />

## 何時使用

`InvoiceStudio` 是自由職業者或小公司填寫發票的地方。表單先問發票開給誰、發票號以及何時付款，然後是明細行：交付了什麼、數量多少、單價多少、適用哪種稅率。折扣可以是百分比，也可以是固定金額，底部還可以寫一段備注。表單旁邊的紙張顯示發票打印出來的樣子，表單的合計和紙張都隨每一次輸入更新。發送會先檢查發票能否發出，再交給你的服務。

- **自由職業或代理機構工具中的開票。**
- **報價單和估價單**：結構相同（修改文字即可）。
- **後台修改**：需要關注打印結果的場合。

## 何時不用

- **只顯示已完成的發票**：直接顯示紙張或 PDF，不需要表單。
- **周期性訂閱**：使用 Billing overview 區塊。
- **很長的商品目錄**：讓使用者從目錄中選擇商品，再在程式碼中加入為明細行。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。設定開票方（`SenderName`、`SenderAddress`）和可選的 `Clients`，並用 `InvoiceLineItem` 填充 `Lines`。`InvoiceStudioSamples.Apply` 會用預覽中虛構的工作室填充區塊。

```xml
<arc:InvoiceStudio SenderName="Fernhill Design Co." InvoiceNumber="INV-0042" PaymentTerms="30">
    <arc:InvoiceLineItem Description="Brand workshop" Quantity="1" UnitPrice="1800" TaxRate="0.08" />
</arc:InvoiceStudio>
```

### 合計

小計是各行金額之和（數量乘以單價，四捨五入到分）。折扣在計稅之前從小計中扣除，可以是 `DiscountValue` 的百分比或該金額，但不會超過小計。稅按行計算：每行扣除它分攤的折扣之後，按該行的稅率計稅。`Totals` 保存這四個合計。

### 發送

發送會調用 `CreateDraft`：發票需要一個客戶，以及至少一行帶說明和金額的明細。缺少時，原因會顯示在按鈕下方並被讀出，焦點移到客戶或第一行。準備好的發票以 `InvoiceDraft`（只含可計費的行）交給 `SendAsync`；運行期間按鈕顯示載入圈，之後顯示 "Sent"，下方出現 "Sent to …"，並引發 `InvoiceSent`。之後的任何編輯都會讓它重新變為草稿。

```csharp
studio.SendAsync = async (invoice, cancellation) =>
    await billing.SendAsync(invoice.Client.Email!, invoice, cancellation);
```

### 窄區塊

低於 900 px 時，表單和預覽通過 Edit 和 Preview 切換輪流顯示；低於 560 px 時，每行的數量、價格和稅率放到說明下方。

<Demo name="Narrow" />

## API 參考

<!-- api: InvoiceStudio -->

<!-- api: InvoiceLineItem -->

<!-- api: InvoiceClient -->

<!-- api: InvoiceDraft -->

## 無障礙

- 每個欄位都有標籤或無障礙名稱：明細行的說明、數量、價格和稅率欄位以所在列命名，刪除按鈕名為 "Remove line 2" 或 "Remove Logo refinements"。
- 刪除一行後，焦點移到下一行的刪除按鈕。
- 預覽名為 "Invoice preview"，其中的文字是螢幕閱讀器可以讀出的真實文字。
- 禮貌的即時區域會讀出缺少的欄位和已發送的發票。

## 動效

- 表單和紙張在原地更新；數字不滾動，輸入時保持安靜。
- 發送按鈕的文字使用按鈕自身的切換效果，發送期間顯示載入圈。
- **減弱動效**和**關閉**時所有變化立即生效。

## 響應式

- 區塊讀取自身寬度：低於 900 px 時預覽放到切換後面，低於 560 px 時明細行占兩行。
- 紙張在兩種主題下都保持紙張與墨水的顏色，就像打印出來的一樣。

## 相關組件

- Checkout summary 區塊：帶付款按鈕的訂單摘要。
- `Input`、`NumberField`、`Select` 和 `Textarea`：表單使用的欄位。
