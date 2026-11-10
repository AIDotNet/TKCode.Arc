---
title: 聯絡區塊
summary: 驗證後變形為確認訊息的聯絡表單、支援管道，以及附當地時間的辦公室卡片。
source-hash: dd414720dc34
---

<Demo name="Preview" />

## 何時使用

聯絡頁面只有一個任務：把問題送到對的人手裡。`ContactSection` 提供三種版面，按你希望被聯絡的方式選用。

- **一個入口**：`Form` 詢問姓名、位址、主題和留言，驗證後變成確認訊息。
- **多個入口**：`Channels` 列出聊天、電子郵件、銷售等方式，旁邊的面板顯示所選方式的詳情。
- **真實地點**：`Offices` 為每個辦公室顯示一張卡片，包含地址、即時當地時間以及是否營業。

## 何時不用

- **只收集電子郵件地址**：請使用 `NewsletterSignup`。
- **分多步的長表單**：請使用 `MultiStepForm`。
- **頁面上就能回答的問題**：先使用 `FaqSection`，再從它的聯絡列連結到這裡。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContactSection Title="Talk to us" />
</UserControl>
```

```csharp
contact.SubmitHandler = async message =>
{
    var response = await http.PostAsJsonAsync("/api/contact", message);
    response.EnsureSuccessStatusCode();
};
```

`ContactSection` 衍生自 `TemplatedControl`。**在應用程式中請設定 `SubmitHandler`**：否則表單只會等待 1.1 秒並回報成功，不會傳送任何內容。`Channels` 和 `Offices` 為空時顯示範例資料，也請替換。

### 版面

| `Variant` | 顯示內容 | 需設定 |
|---|---|---|
| `Form` | 標題和兩則資訊，旁邊是表單卡片 | `SubmitHandler`、`Topics`、`ResponseNote`、`ContactEmail` |
| `Channels` | 分頁清單，旁邊是詳情面板 | `Channels`、`SelectedChannel` |
| `Offices` | 每個辦公室一張卡片 | `Offices` |

`Title` 和 `Description` 可取代任一版面的標題。未設定時，每種版面顯示自己的文案。

### 表單

按下 **Send message**，或在姓名、電子郵件欄位中按 `Enter`，都會驗證欄位。第一個錯誤的欄位取得焦點，此後錯誤訊息隨輸入更新。

| 欄位 | 規則 |
|---|---|
| Name | 不能為空 |
| Work email | 沒有空格，一個 @，網域中有點，且點後至少兩個字元 |
| Message | 至少 20 個字元，最多 500 個，並即時計數 |

隨後 `SubmitHandler` 收到一個 `ContactMessage`，其中是去除首尾空格後的值，此時欄位為唯讀，按鈕顯示載入狀態。工作完成後，卡片以彈簧變形為確認訊息，其中寫明對方的姓名和主題，焦點移到它的標題上。工作失敗時，表單和已輸入的內容都會保留，欄位下方顯示一則訊息。

<Demo name="FailedSend" />

主題是一組單選標籤：醒目區塊滑向所選項，方向鍵可以移動它。`Topics` 可取代預設的四個主題。

### 管道

每個 `ContactChannel` 都有 `Value`、`Label`、其下一行 `Meta`、一個 24×24 的 `Icon` 和 `Detail`（可以是文字或任意控制項）。`SelectedChannel` 是雙向的，儲存 `Value`。面板以彈簧變化到每個詳情的高度，內容沿移動方向滑動。

```csharp
contact.Variant = ContactSectionVariant.Channels;
contact.Channels.Add(new ContactChannel
{
    Value = "email",
    Label = "Email the team",
    Meta = "One working day",
    Icon = ArcIcons.Mail,
    Detail = "Attach files or a short recording. A person answers every message."
});
```

### 辦公室

每個 `ContactOffice` 都有 `City`、`TimeZone` id、`Address` 各行、選用的 `Email`，以及當地 24 小時制的 `OpenHour` 和 `CloseHour`（預設 9 和 18）。辦公室在週一至週五的這些時段內營業。卡片在螢幕上時每 15 秒重新整理一次，只有這種版面執行計時器。系統不認識的時區顯示 `--:--` 和「Checking hours」。瀏覽器版本可能需要為此提供時區資料。

<Demo name="Offices" />

### 最佳實務

**建議**

- 說明回覆需要多久，並遵守承諾。
- 透過工作把錯誤傳回，讓對方保留已輸入的內容。
- 用文字寫明營業狀態：卡片會把它寫出來，而不只是用顏色表示。

**避免**

- 索取超出需要的資訊。每多一個欄位都會流失一些人。
- 不設定 `SubmitHandler` 就上線。表單看起來成功了，其實什麼都沒有傳送。

## API 參考

<!-- api: ContactSection -->

<!-- api: ContactMessage -->

<!-- api: ContactChannel -->

<!-- api: ContactOffice -->

## 無障礙

- 欄位有可見的標籤，錯誤在第一次嘗試後出現並隨輸入更新。傳送失敗會立即朗讀。
- 焦點會移到第一個錯誤的欄位、確認訊息的標題，以及按下「Send another message」後回到姓名欄位。
- 主題和管道各自只佔一個 Tab 停駐點，辦公室的營業狀態用文字寫明。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 方向鍵 | 在主題標籤或管道之間移動，到兩端時循環 |
| `Home` / `End` | 第一個或最後一個管道 |
| `Enter` | 在姓名或電子郵件欄位中傳送表單 |
| `Tab` / `Shift+Tab` | 在欄位和操作之間移動 |

## 動效

- 卡片從舊高度以彈簧變化到新高度（0.4 秒，無回彈）。新內容延遲 40 ms 後，在 240 ms 內從 4 px 模糊中移入 18 px，舊內容在 160 ms 內移出 12 px。
- 確認訊息的勾選標記延遲 160 ms 後在 420 ms 內繪製。主題或管道的醒目區塊以 morph 彈簧滑動。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 卡片切換內容 | 高度彈簧、滑動和模糊 | 交叉淡化 | 立即 |
| 醒目區塊移動 | Morph 彈簧 | 立即 | 立即 |
| 勾選標記繪製 | 420 ms | 立即顯示 | 立即顯示 |

## 響應式

- `ContactSection` 跟隨自身寬度，而不是視窗寬度。標題從 28 px 縮放到 52 px。
- 寬度不超過 820 px（`:stacked`）時，表單移到標題下方，管道清單變成可橫向捲動的一列，並隱藏說明列。
- 寬度不超過 520 px（`:compact`）時，姓名和電子郵件上下堆疊，傳送按鈕佔滿寬度，內距收緊。
- 辦公室卡片按可用空間排成盡量多的欄，每欄至少 240 px。
- 觸控：標籤和按鈕至少 36 px 高，沒有資訊依賴懸停。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `ContactSection` | 需要讓人聯絡到你 |
| `FaqSection` | 答案可能已經在頁面上 |
| `NewsletterSignup` | 只需要一個電子郵件地址 |
| `MultiStepForm` | 表單分多個步驟 |
