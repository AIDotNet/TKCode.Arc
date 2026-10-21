---
title: 付款流程
summary: 一筆信用卡付款從結帳開始，經過權杖化、授權和發卡機構的決定，直到結算。
source-hash: 5a65871a5021
---

<Demo name="CardPayment" />

## 何時使用

- 說明客戶按下「付款」到商店收到款項之間發生的過程。
- 結帳頁和說明頁需要說明付款為何會被核准或拒絕時。
- 支付串接文件，依設定顯示金額、幣別和商店。

## 何時不用

- 輸入付款金額：用 `NumberField`；輸入信用卡資料本身則用表單。
- 繪製隨時間變化的總額：用圖表分類中的 `BarChart` 或 `LineChart`。

## 用法

故事會依序播放每一筆付款。圖表下方的說明文字描述目前步驟，每筆核准的付款都會讓商店餘額增加。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PaymentFlow />
</UserControl>
```

### 金額、幣別與商店

`Amount` 為 `decimal` 型別（預設 42），`Currency` 為 ISO 幣別代碼（預設 USD）。`MerchantName` 設定結帳時顯示的商店名稱，`CardLast4` 顯示信用卡號碼的末四碼。常見幣別以符號顯示，其他幣別以代碼顯示。

<Demo name="AmountAndMerchant" />

### 拒絕

`DeclineEvery` 每隔 N 筆付款拒絕一筆（預設 4）。設為 0 則核准所有付款。

```xml
<arc:PaymentFlow DeclineEvery="0" />
```

### 標籤

`Labels` 接受一個 `PaymentFlowLabels` 物件，用於設定圖表中所有可見的文字，包括標示（`已核准`、`已拒絕`、`已付款`）和步驟說明文字。

```csharp
var payment = new PaymentFlow
{
    Amount = 19.00m,
    Labels = new PaymentFlowLabels { Merchant = "商店", Issuer = "發卡機構", Approved = "核准", Declined = "拒絕" }
};
```

### 回應決定

發卡機構對付款作出決定時會觸發 `PaymentCompleted`，事件引數中包含 `Approved` 和 `Amount`。`PaymentCount` 和 `Balance` 分別給出已作出決定的付款筆數和商店餘額。

```csharp
payment.PaymentCompleted += (_, e) =>
    log.Text = $"{(e.Approved ? "已核准" : "已拒絕")} {e.Amount:0.00} · 餘額 {payment.Balance:0.00}";
```

### 播放

`IsPlaying` 用於播放和暫停故事，`Speed` 縮放播放速度（0.25 到 4），`Restart()` 從第一步重新開始。`Step`、`StepCount` 和 `StepChanged` 追蹤一筆付款的七個步驟。

```xml
<arc:PaymentFlow Speed="0.5" IsPlaying="False" />
```

### 減少動態效果

在減少動態效果下，插圖是核准路徑的靜態畫面。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: PaymentFlow -->

<!-- api: PaymentFlowLabels -->

## 無障礙

- `Label` 是無障礙名稱，目前的說明文字會作為說明文字提供給輔助技術。
- 插圖可取得焦點，焦點環寬 2px。
- 空白鍵或 Enter 播放與暫停故事，R 鍵從頭開始播放。
- 說明文字的變化不會被播報，因此故事不會向螢幕閱讀器使用者重複播報。

## 動態效果

- 金額浮現在客戶上方，信用卡資料傳送至閘道，請求經由卡組網路抵達發卡銀行。
- 發卡機構的決定以綠色「已核准」或紅色「已拒絕」標示顯示，回應沿另一條通道返回。
- 核准的付款沿虛線結算至商店帳戶，餘額讀數隨之增加。
- 減少動態效果和關閉動態效果時，顯示核准路徑的靜態畫面，並帶有「已核准」和「已付款」標示。播放按鈕會隱藏。

## 相關元件

- `DataFlow`：付款後儲存事件的管線。
- `MultiRegionFailover`：區域故障時保持支付服務可用。
- `Stepper`：使用者逐步完成的固定步驟序列。
