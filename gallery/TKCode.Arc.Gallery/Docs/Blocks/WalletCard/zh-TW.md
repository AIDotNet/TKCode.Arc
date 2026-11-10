---
title: Wallet card
summary: 錢包裡的一張付款卡：用程式碼繪製的卡面和虛構的卡片組織標誌、餘額與本月消費占限額的比例、最近的交易、讓卡面結霜的凍結，以及顯示並複製卡號的卡片詳情。
source-hash: 3f898e148396
---

<Demo name="Preview" />

## 何時使用

`WalletCard` 像銀行或錢包應用那樣展示一張付款卡。左邊是用程式碼繪製的卡片：卡面顏色、芯片、非接觸標誌、一個虛構的卡片組織標誌（傾斜圓環裡的圓點）、遮住的卡號、持卡人和有效期。旁邊是人們打開這張卡時最關心的問題：還有多少可用、本月限額已經用了多少、最近幾筆消費是什麼。卡片下方有兩個操作。凍結會停止新的消費並讓卡面結霜；顯示詳情會在一段時間內顯示完整卡號和安全碼，並提供複製卡號。

- **錢包或銀行應用裡的卡片頁**，一次一張卡。
- **虛擬卡**：人們需要把卡號複製到結帳頁。
- **卡片丟失時的流程**：先凍結，再決定下一步。

## 何時不用

- **很多張卡的列表**：顯示一排小卡面，選中後再打開這個區塊。
- **加入新卡**：使用帶 `Input` 欄位的表單（Live checkout 區塊裡就有一個）。
- **完整帳單**：動態列表只顯示最近幾行；需要篩選和分頁的歷史記錄請用 `Table`。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。設定卡片（`HolderName`、`CardNumber`、`Expiry`、`SecurityCode`、`Finish`）、金額（`Balance`、`MonthlyLimit`、`SpentThisMonth`、`CurrencySymbol`），並用 `WalletTransaction` 按從新到舊的順序填充 `Transactions`。`WalletCardSamples.Apply` 會用預覽中虛構的持卡人填充區塊。

```xml
<arc:WalletCard HolderName="Ines Moreau" CardNumber="4929 1820 5531 4821" Expiry="08/29" SecurityCode="317"
                Balance="2481.56" MonthlyLimit="3000" SpentThisMonth="1240.18">
    <arc:WalletTransaction Merchant="Lantern Coffee" Amount="6.40" Category="Cafes" />
</arc:WalletCard>
```

### 凍結

凍結按鈕會用想要的狀態調用 `SetFrozenAsync` 並等待結果：按鈕顯示載入圈，在你的服務回應之前卡片保持原樣。返回 true 時，霜層蓋上卡面，凍結說明出現，並引發 `FrozenChanged`；返回 false（或拋出異常）時卡片不變，並讀出 "The card could not be changed. Try again."。沒有處理程式時立即生效。在程式碼裡設定 `IsFrozen` 會直接生效。

```csharp
card.SetFrozenAsync = async (frozen, cancellation) =>
    await cards.SetFrozenAsync(cardId, frozen, cancellation);
```

### 詳情

顯示詳情會把遮住的卡號換成完整卡號，顯示 CVC 和複製卡號按鈕，並在 `DetailsTimeout`（30 秒；0 表示一直顯示到手動隱藏）之後再次隱藏。按 Escape 也會隱藏。

### 卡面與限額

`Finish` 可選 Midnight、Ocean、Coral、Forest 或 Accent（主題的強調色）。消費超過限額時進度條變為危險色，`MonthlyLimit="0"` 會隱藏它。

<Demo name="Finishes" />

## API 參考

<!-- api: WalletCard -->

<!-- api: WalletTransaction -->

## 無障礙

- 卡面的名稱包含卡片組織、持卡人、卡號後四位以及是否已凍結。
- 凍結和顯示詳情是按鈕，名稱隨狀態變化（"Unfreeze"、"Hide details"）。
- 每一行動態都是按鈕，名稱包含商戶、帶符號的金額、日期以及待處理或已拒絕的狀態；**Up**、**Down**、**Home** 和 **End** 在行之間移動，**Enter** 或 **Space** 引發 `TransactionInvoked`。
- 禮貌的即時區域會讀出凍結、解凍、修改失敗以及詳情的顯示和隱藏。

## 動效

- 霜層在 480 ms 內淡入，淡出也一樣；完整卡號和 CVC 與遮住的卡號交叉淡化（淡出 160 ms，淡入 240 ms）。
- 限額進度條以 smooth 彈簧移動到新的比例。
- **減弱動效**：霜層和詳情在 120 ms 內淡入淡出，進度條立即到位。**關閉**時所有變化立即生效。

## 響應式

- 區塊讀取自身寬度。低於 680 px 時，卡片放在餘額和動態的上方，而不是旁邊。
- 卡面保持 1.586 的銀行卡比例，最寬 360 px。
- 過長的商戶名稱以省略號結尾；金額不會換行。

## 相關組件

- Credit top-up 區塊：用已保存的卡購買用量額度。
- Spend alert 區塊：在卡片消費之上設定預算和提醒。
- `CopyButton`、`Button` 和 `Badge`：區塊用到的控制項。
