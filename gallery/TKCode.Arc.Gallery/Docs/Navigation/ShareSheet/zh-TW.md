---
title: Share sheet
summary: 就地分享：分享按鈕伸展成一張卡片，可以複製項目連結、設定誰能開啟、發布到管道或傳給幾個人，傳送後縮成一張確認卡片。
source-hash: 3baf3b4a19be
---

<Demo name="Preview" />

## 何時使用

`ShareSheet` 是放在工具列或頁首裡的分享按鈕。按下後，按鈕本身長成分享面板：同一個形狀從膠囊拉伸成卡片，所以面板看起來是按鈕打開了，而不是跳出了新視窗。
面板寫明分享的是什麼，顯示連結和複製按鈕、連結附帶的存取權限，提供幾個可以發布的管道，並列出你最常分享的人。選取一個人時，他的頭像會飛進上方的標籤列；按下傳送後，名單交給你的應用程式。傳送成功後，卡片收縮成一張帶「復原」和「完成」的確認卡片。
在手機尺寸的螢幕上，同一個面板改為從底部升起的面板。

- **大家反覆轉傳的項目**，例如文件、看板或設計稿，按鈕放在該項目自己的頁首裡。
- **連結和人員一次處理**：複製連結、決定誰能開啟、傳給同事，都在同一張卡片裡完成。
- **兩三個發布目標**，例如郵件或團隊聊天，每個按鈕各自回報進度和結果。

## 何時不用

- **只需要複製網址**：在連結旁邊放一個複製按鈕即可。
- **逐人設定角色，或依地址查找人員**：這屬於表單，請為它開啟一個對話框。
- **只有一組去處、無人可選**：彈出框或下拉選單用更少的東西就能做到。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ShareSheet Title="Quarterly review" Link="https://example.com/doc/quarterly-review" Access="team">
        <arc:ShareSheet.AccessLevels>
            <arc:ShareSheetAccess Value="private" Label="Private" Description="Only the people you add"
                                  Glyph="{x:Static arc:ArcIcons.Lock}" />
            <arc:ShareSheetAccess Value="team" Label="Your team" Description="Everyone on the team can comment"
                                  Glyph="{x:Static arc:ArcIcons.Users}" />
        </arc:ShareSheet.AccessLevels>
        <arc:ShareSheet.Channels>
            <arc:ShareSheetChannel Id="email" Label="Email" DoneLabel="Sent" Glyph="{x:Static arc:ArcIcons.Mail}" />
        </arc:ShareSheet.Channels>
        <arc:ShareSheet.People>
            <arc:ShareSheetPerson Id="ada" Name="Ada Moreno" />
            <arc:ShareSheetPerson Id="ravi" Name="Ravi Kapoor" />
        </arc:ShareSheet.People>
    </arc:ShareSheet>
</UserControl>
```

`ShareSheet` 位於 Special 套件：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。版面空間只給分享按鈕，因為面板畫在頁面上方的覆蓋層裡。按鈕下方要空出一些位置；靠近邊緣時，選一個朝遠離邊緣方向開啟的 `Align`。`IsOpen`（雙向）、`Open()` 與 `Close()` 用於在程式碼中控制它。

- `Title` 是項目的名稱，面板標題把它顯示為 Share “Title”。`Link` 是網址：它單獨佔一列，「複製」把它放到剪貼簿上（為 null 時不顯示該列）。
- `AccessLevels` 存放 `ShareSheetAccess` 項目（`Value`、`Label`、`Description`、`Glyph`）。`Access` 是選取的值，雙向繫結；初始為第一項。
- `Channels` 存放 `ShareSheetChannel` 項目（`Id`、`Label`、`DoneLabel`、`Glyph`）。發布後按鈕會把 `Label` 換成 `DoneLabel` 兩秒，所以兩者的字數要相近。
- `People` 存放 `ShareSheetPerson` 項目（`Id`、`Name`、`Avatar`）。沒有 `Avatar` 圖片時，面板依 id 產生一個柔和的漸層，同一個人總是同樣的樣子。`SelectedPeople` 列出已選取的人；`Pick` 與 `Unpick` 在程式碼中修改它。

### 傳送與發布

把伺服器呼叫交給面板，等待由它處理。`SendHandler` 收到人員和存取權限；執行期間傳送按鈕轉圈。若它擲回例外，按鈕上方出現一行錯誤，面板保持開啟，人員仍處於選取狀態；若它完成，觸發 `Sent`，面板收攏成確認卡片。確認卡片上的「復原」回到面板、保留同樣的人員，並觸發 `SendUndone`，讓你的應用程式撤回這次傳送。
`ChannelHandler` 對單一管道按鈕的作用相同：先轉圈，然後顯示該管道的 `DoneLabel`，處理常式擲回例外時顯示 Failed。

```csharp
sheet.SendHandler = async (submission, token) =>
    await api.ShareAsync(submission.People.Select(p => p.Id), submission.Access?.Value, token);
sheet.ChannelHandler = (channel, token) => api.PostAsync(channel.Id, token);
```

<Demo name="Channels" />

### 面板往哪裡展開

`Align` 設定面板從哪個錨點展開。`End`（預設）固定按鈕的右上角，面板向左、向下伸展；`Start` 固定左上角，向右、向下伸展；`Center` 固定按鈕的中心，面板對稱地展開和收攏。面板若會越過視窗邊緣，會被移回視窗內。

<Demo name="Align" />

### 只有人員，以及手機上的底部面板

只有你設定的內容才會顯示對應的列：不設定 `Link`、`AccessLevels` 或 `Channels`，相應的列就省略。視埠寬度為 520 px 或更窄時（手機預覽也算），面板變成貼在底邊的底部面板，後面的頁面變暗；設定 `SheetOnPhones="False"` 可以在這些寬度下也保留浮動面板。

<Demo name="People" />

### 最佳實務

**建議**

- `People` 裡只放這個項目通常會傳給的幾個人，最可能的排在前面。
- 讓處理常式回傳工作，面板才能顯示進度、錯誤和結果。
- 管道名稱要短，完成詞的長度與之相近。

**避免**

- 把整本通訊錄塞進 `People`：這一列用來快速選人，不是用來查找人的。
- 超過三四個管道：按鈕共用一列。

## API 參考

<!-- api: ShareSheet -->

## 無障礙

- 分享按鈕公開面板的展開狀態。焦點隨流程移動：面板開啟時落在「複製」上（或現有的第一個控制項），傳送後落在「完成」上，全部關閉後回到分享按鈕。在頁面其他地方按下會關閉面板，焦點留在被按下的元素上。
- 存取權限清單是一組選項；選取的一項標示為已選，也是清單唯一的 Tab 停駐點，方向鍵在選項之間移動。
- 每位最近聯絡人都是一個帶全名的切換按鈕，每個標籤都有一個寫明要移除誰的移除按鈕。
- 螢幕閱讀器透過一個禮貌的即時區域聽到每一步：連結已複製、加入或移除了某人、管道的結果，以及分享送達了哪些人。傳送失敗時還會出現一行警示文字。

### 鍵盤互動

| 按鍵 | 動作 |
|---|---|
| Enter 或 Space | 在分享按鈕上時開啟面板；在其他地方時啟用取得焦點的控制項。 |
| Tab | 依序經過關閉按鈕、複製、存取權限列、管道、人員、標籤和傳送。 |
| 上、下、左、右 | 存取權限清單展開時，逐一經過其中的選項；越過最後一項後從第一項重新開始。 |
| Home / End | 移到第一個或最後一個存取權限選項。 |
| Escape | 存取權限清單展開時將其關閉；否則把面板收攏回分享按鈕。 |

## 動效

- 分享按鈕、面板和傳送後的確認卡片畫成同一個表面；每次換面時，彈簧把它的尺寸和圓角帶到新的形狀。內容以短暫的模糊交換：離開的內容在淡出時略微縮小，進入的內容從 94% 放大。從面板到確認卡片以及返回時，內容改為橫向滑動。
- 選取某人時，他的頭像會有一個副本飛向新的標籤。它畫在面板上方，所以標籤列的邊緣不會把它截斷。被移除的標籤淡出，後面的標籤移過來補上空位。
- 傳送按鈕上的數字隨著加入或移除人員向上或向下跳動。還沒選人就按傳送時，收件人這一列會短促、生硬地抖一下。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 開啟、確認、收回 | 尺寸彈簧，模糊與縮放交叉切換 | 尺寸直接到位，兩面淡入淡出 | 立即 |
| 面板 ↔ 確認卡片 | 兩面橫向滑動 32 px 並模糊切換 | 兩面淡入淡出 | 立即 |
| 選取一個人 | 頭像飛向標籤 | 標籤淡入 | 標籤直接出現 |
| 傳送按鈕中的人數 | 數字捲動 | 替換 | 替換 |
| 沒有選人就傳送 | 收件人列輕推 | 不移動 | 不移動 |
| 手機底部面板 | 向上滑入，跟隨把手拖曳 | 淡入 | 直接出現 |

## 響應式

- 寬度：面板 384 px，確認卡片 304 px，兩者都不超過視埠寬度減 32 px，因此在 320 px 的螢幕上也放得下。
- 視埠寬度不超過 520 px 時，面板是距兩側和底部 8 px 的底部面板。把把手往下拖過 120 px 或往下甩動即可關閉，拖得更短會彈回原位。Escape、點按變暗的頁面和「完成」也會關閉它。
- 懸停色只回應滑鼠或筆；觸控不會留下懸停狀態。

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `ShareSheet` | 一個項目透過連結、管道和幾個人來分享 |
| `ShareAccess` | 在對話框中管理人員及其角色 |
| `CopyButton` | 只需要複製連結 |
| `ActionMorph` | 按鈕為另一種操作長成一個小的內嵌表單 |
