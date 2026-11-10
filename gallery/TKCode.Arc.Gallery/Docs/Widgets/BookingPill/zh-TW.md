---
title: Booking pill
summary: 用於地點、日期和人數的緊湊預訂列。每一段打開自己的選擇器，打開的那一段會變寬，一塊浮起的底板滑到它下面。
source-hash: 96f596c9a67f
---

<Demo name="Preview" />

## 何時使用

`BookingPill` 把整個預訂搜尋放進一條圓角列：去哪裡、什麼時候、幾個人，最後是搜尋按鈕。每一段都會在列下方打開自己的選擇器：一個可以篩選的地點清單，一個用色帶標出從入住到離開的月份，以及 Adults、Children 和 Infants 三列的加減按鈕。選擇器打開時，整條列圍繞這一段改變形狀：這一段變寬，一塊浮起的底板滑到它下面，搜尋按鈕變長並顯示文字。選好地點後會接著選日期，選好離開日期後會接著選人數。

- **住宿與租屋**：房間、小屋、公寓。
- **餐位與活動**：一個地點、一天和一組人數。
- **任何由三個簡短條件組成**、人們會依次填寫的搜尋。

## 何時不用

- **很多篩選條件**：價格、類型和設施應該放在篩選面板或底部面板中。
- **自由文字搜尋**：一個搜尋框更簡單。
- **很窄的螢幕**：小於約 480 px 時各段會很擠；手機上更適合用全螢幕流程。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BookingPill>
        <arc:BookingPlace Name="Lisbon" Detail="Old town by the river" />
        <arc:BookingPlace Name="Kyoto" Detail="Temples and quiet lanes" />
    </arc:BookingPill>
</UserControl>
```

`BookingPill` 在 Special 套件中；請在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。把它提供的地點作為 `BookingPlace` 放在裡面。`Place`、`CheckIn`、`CheckOut`、`Adults`、`Children` 和 `Infants` 保存所選內容，可以雙向繫結；`OpenPart` 表示哪個選擇器打開著，`Open()` 和 `Close()` 在程式碼裡做同樣的事。搜尋按鈕觸發 `SearchRequested`，帶有列裡的全部內容。

### 預先填好，用你自己的文字

每個值都可以預先設定，每段文字都可以替換：各段的標籤、空值（「Anywhere」「Add dates」「Add guests」）、人數和晚數的格式，以及選擇器裡的文字。`MinDate`（預設今天）讓更早的日子變淡，`MaxGuests` 和 `MaxInfants` 限制加減按鈕，`CultureName` 決定日期和月份名稱。

<Demo name="Prefilled" />

### 最佳實踐

**建議**

- 提供一份簡短的可能地點清單；上方的輸入框會在輸入時篩選它。
- 每段標籤只用一個詞，比如「Where」「When」「Who」。
- 處理 `SearchRequested` 來執行搜尋；這些值也在屬性裡。

**避免**

- 頁面載入時就打開選擇器：讓人們從自己想要的地方開始。
- 超過三段；這條列是為三段設計的。

## API 參考

<!-- api: BookingPill -->

<!-- api: BookingPlace -->

## 無障礙

- 每一段都是一個按鈕，讀作標籤加值，例如「Where, Lisbon」。搜尋按鈕的名稱來自 `SearchText`。
- 用鍵盤打開一段時，焦點會移進它的選擇器：地點輸入框、月份，或第一個加號按鈕。Escape 關閉選擇器，並把焦點放回這一段。
- 一條禮貌的狀態訊息會讀出打開的段、選中的日子（「Saturday, March 14, 2026, check-in」）和人數。
- 指標取得焦點時不顯示焦點環；鍵盤取得焦點時在段、搜尋按鈕或日子周圍顯示焦點環。

### 鍵盤互動

| 按鍵 | 作用 |
|---|---|
| Tab | 在各段、搜尋按鈕和打開的選擇器之間移動。 |
| Enter 或 Space | 打開或關閉一段的選擇器；選中地點或日子。 |
| 下 / 上 | 在地點選擇器中，在輸入框和地點之間移動。 |
| 方向鍵 | 在月份中移動日期游標；Page Up 和 Page Down 切換月份。 |
| Home / End | 在月份中到這一週的開頭或結尾。 |
| Escape | 關閉選擇器。 |

## 動效

- 打開一段時，三段的寬度和打開那段下方的底板一起隨彈簧變化，整條列在一個動作中改變形狀；切換段時底板滑過去。
- 選擇器打開時，列的背景加深一級，搜尋按鈕的寬度隨彈簧變化以放下文字。
- 選擇器從列下方進入，切換到另一段時內容交叉淡化。
- 人數會滾動。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 打開一段 | 寬度和底板隨彈簧變化 | 直接跳到 | 直接跳到 |
| 切換段 | 底板滑過，選擇器交叉淡化 | 直接跳到 | 直接跳到 |
| 搜尋按鈕 | 寬度隨彈簧變化以放下文字 | 直接跳到 | 直接跳到 |
| 人數 | 滾動 | 直接跳到 | 直接跳到 |

## 響應式

- 列佔滿給它的寬度；三段平分寬度，空間不夠時值以省略號結束。
- 選擇器寬 340 px，浮在頁面上、位於列下方：地點選擇器靠左，日期置中，人數靠右；下方空間不夠時翻到列上方。
- 在列和選擇器以外的任何地方按下都會關閉選擇器。

## 相關元件

- `DateRangePicker` 在自己的欄位中選擇日期範圍。
- `SearchField` 是普通的搜尋框。

| 元件 | 適用場景 |
|---|---|
| `BookingPill` | 在一個緊湊的搜尋中選擇地點、日期和人數 |
| `DateRangePicker` | 只需要日期範圍 |
| `SearchField` | 單一的文字搜尋 |
