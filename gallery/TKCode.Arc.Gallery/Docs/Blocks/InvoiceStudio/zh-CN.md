---
title: Invoice studio
summary: 带实时打印预览的发票编辑器：客户与付款期限、包含数量、价格和税率的明细行、按百分比或金额给出的折扣、随每次编辑更新的合计，以及发送前的检查。
source-hash: 6e730703f052
---

<Demo name="Preview" />

## 何时使用

`InvoiceStudio` 是自由职业者或小公司填写发票的地方。表单先问发票开给谁、发票号以及何时付款，然后是明细行：交付了什么、数量多少、单价多少、适用哪种税率。折扣可以是百分比，也可以是固定金额，底部还可以写一段备注。表单旁边的纸张显示发票打印出来的样子，表单的合计和纸张都随每一次输入更新。发送会先检查发票能否发出，再交给你的服务。

- **自由职业或代理机构工具中的开票。**
- **报价单和估价单**：结构相同（修改文案即可）。
- **后台修改**：需要关注打印结果的场合。

## 何时不用

- **只显示已完成的发票**：直接显示纸张或 PDF，不需要表单。
- **周期性订阅**：使用 Billing overview 区块。
- **很长的商品目录**：让用户从目录中选择商品，再在代码中添加为明细行。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。设置开票方（`SenderName`、`SenderAddress`）和可选的 `Clients`，并用 `InvoiceLineItem` 填充 `Lines`。`InvoiceStudioSamples.Apply` 会用预览中虚构的工作室填充区块。

```xml
<arc:InvoiceStudio SenderName="Fernhill Design Co." InvoiceNumber="INV-0042" PaymentTerms="30">
    <arc:InvoiceLineItem Description="Brand workshop" Quantity="1" UnitPrice="1800" TaxRate="0.08" />
</arc:InvoiceStudio>
```

### 合计

小计是各行金额之和（数量乘以单价，四舍五入到分）。折扣在计税之前从小计中扣除，可以是 `DiscountValue` 的百分比或该金额，但不会超过小计。税按行计算：每行扣除它分摊的折扣之后，按该行的税率计税。`Totals` 保存这四个合计。

### 发送

发送会调用 `CreateDraft`：发票需要一个客户，以及至少一行带说明和金额的明细。缺少时，原因会显示在按钮下方并被读出，焦点移到客户或第一行。准备好的发票以 `InvoiceDraft`（只含可计费的行）交给 `SendAsync`；运行期间按钮显示加载圈，之后显示 "Sent"，下方出现 "Sent to …"，并引发 `InvoiceSent`。之后的任何编辑都会让它重新变为草稿。

```csharp
studio.SendAsync = async (invoice, cancellation) =>
    await billing.SendAsync(invoice.Client.Email!, invoice, cancellation);
```

### 窄区块

低于 900 px 时，表单和预览通过 Edit 和 Preview 切换轮流显示；低于 560 px 时，每行的数量、价格和税率放到说明下方。

<Demo name="Narrow" />

## API 参考

<!-- api: InvoiceStudio -->

<!-- api: InvoiceLineItem -->

<!-- api: InvoiceClient -->

<!-- api: InvoiceDraft -->

## 无障碍

- 每个字段都有标签或无障碍名称：明细行的说明、数量、价格和税率字段以所在列命名，删除按钮名为 "Remove line 2" 或 "Remove Logo refinements"。
- 删除一行后，焦点移到下一行的删除按钮。
- 预览名为 "Invoice preview"，其中的文字是屏幕阅读器可以读出的真实文字。
- 礼貌的实时区域会读出缺少的字段和已发送的发票。

## 动效

- 表单和纸张在原地更新；数字不滚动，输入时保持安静。
- 发送按钮的文字使用按钮自身的切换效果，发送期间显示加载圈。
- **减弱动效**和**关闭**时所有变化立即生效。

## 响应式

- 区块读取自身宽度：低于 900 px 时预览放到切换后面，低于 560 px 时明细行占两行。
- 纸张在两种主题下都保持纸张与墨水的颜色，就像打印出来的一样。

## 相关组件

- Checkout summary 区块：带支付按钮的订单摘要。
- `Input`、`NumberField`、`Select` 和 `Textarea`：表单使用的字段。
