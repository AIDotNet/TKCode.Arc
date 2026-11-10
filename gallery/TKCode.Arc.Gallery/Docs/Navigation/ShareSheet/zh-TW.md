---
title: Share sheet
summary: 分享一個項目。複製它的連結、選擇一個人或一個目的地，或執行一個動作，在手機上是從底部升起的面板，在桌面上是彈出框。
source-hash: 03565f0ead93
---

<Demo name="Preview" />

## 何時使用

`ShareSheet` 把傳遞一個項目的所有方式集中在同一處。最上面是項目的連結和複製按鈕；下面是一排你最常分享的對象，每個都是帶著姓名縮寫的柔和圓形；接著是一排目的地，例如電子郵件、訊息或團隊空間；最後是幾個動作，例如嵌入或下載。在手機上，它是從底部升起、可以拖走的面板；在較寬的螢幕上，它是在發出請求的按鈕下方開啟的彈出框。這個面板只回報選了什麼；實際的傳送由你的應用程式負責。

- **分享文件、一組照片或一個頁面**給人或應用程式。
- **邀請**：把連結傳給幾個人或傳到行事曆。
- **與分享相鄰的匯出**：嵌入程式碼、下載、列印。

## 何時不用

- **管理誰有存取權**（角色、權限、連結設定）：那需要設定對話框。
- **只有單一目的地**：一個普通的「複製連結」按鈕更快。
- **很長的聯絡人清單**：這些列只顯示你常用的對象；搜尋所有人屬於選擇器的工作。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ShareSheet Link="https://example.com/a/autumn-hike" Subtitle="Autumn hike photos">
        <arc:Button Content="Share" Variant="Secondary" />
        <arc:ShareSheet.People>
            <arc:ShareSheetTarget Name="Mira Okafor" />
            <arc:ShareSheetTarget Name="Tomas Reyes" />
        </arc:ShareSheet.People>
        <arc:ShareSheet.Destinations>
            <arc:ShareSheetTarget Name="Email" Glyph="{x:Static arc:ArcIcons.Mail}" />
        </arc:ShareSheet.Destinations>
        <arc:ShareSheet.Actions>
            <arc:ShareSheetAction Label="Download a copy" Glyph="{x:Static arc:ArcIcons.Download}" />
        </arc:ShareSheet.Actions>
    </arc:ShareSheet>
</UserControl>
```

`ShareSheet` 位於 Special 套件：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。它的內容就是觸發按鈕：放在那裡的按鈕會開啟這個面板。`Open()`、`Close()`、`ShowAt(anchor)` 和雙向的 `IsOpen` 可以在程式碼中控制它。

`People` 和 `Destinations` 是 `ShareSheetTarget` 的清單：有 `Name`、選用的 `Detail`（由輔助技術朗讀）、選用的 `Glyph`（用來取代姓名縮寫）以及放你自己的值的 `Tag`。`Actions` 是 `ShareSheetAction` 的清單，每個有 `Label`、`Glyph` 和 `Command`。`Link` 會加入帶複製按鈕的連結列；設為 null 則隱藏這一列。沒有內容的群組不會顯示。

### 得知選了什麼

`Shared` 會在觸發時帶上 `e.Target` 中的對象，以及 `e.IsPerson` 表示它是否為一個人，除非 `ClosesOnShare` 為 false，否則面板會關閉。`ActionInvoked` 會在動作的命令執行之前觸發。`LinkCopied` 會在複製按鈕把連結放進剪貼簿之後觸發。

### 在哪裡開啟

`Presentation` 預設為 `Auto`：視窗寬度窄於 640 px 時是底部面板，否則是彈出框。`Popover` 和 `Sheet` 則固定選用其中一種。

<Demo name="Sheet" />

### 最佳實務

**建議**

- 依序顯示這個人最常分享的幾位對象。
- 把 `Subtitle` 設為項目的名稱，讓人清楚正在分享什麼。
- 在 `Shared` 之後於你的應用程式中確認分享成功，例如用一則提示訊息。

**避免**

- 超過大約八個人或目的地：這些列可以捲動，但人們看到的是前面那幾個。
- 把 `Actions` 用在與這個項目無關的事情上。

## API 參考

<!-- api: ShareSheet -->

## 無障礙

- 觸發按鈕會回報面板是否已展開。彈出框或面板開啟時會取得焦點（複製按鈕，沒有的話是第一個人），關閉時再把焦點還給觸發按鈕。
- 人和目的地是以 `Name` 命名的按鈕，`Detail` 作為輔助說明文字；動作是以 `Label` 命名的選單項目。輔助技術可以呼叫全部這些項目。
- 每一排以及動作清單在 Tab 順序中各算一個停靠點；方向鍵在其中移動。鍵盤焦點會在方塊或列的內部顯示一個焦點環。

### 鍵盤互動

| 按鍵 | 動作 |
|---|---|
| Tab | 在複製按鈕、人、目的地和動作之間移動。 |
| 左 / 右 | 沿著人或目的地的那一排移動。 |
| 上 / 下 | 在動作之間移動。 |
| Home / End | 移到該群組的第一個或最後一個項目。 |
| Enter 或 Space | 選擇有焦點的人、目的地或動作。 |
| Escape | 關閉面板並把焦點移回觸發按鈕。 |

## 動效

- 作為彈出框時，它從觸發按鈕那一側出現，帶有短暫的淡入和安定下來的彈簧；作為底部面板時，它從視窗底部升起，並可以向下甩動來關閉。兩者都是本函式庫自己的彈出框和底部面板。
- 複製按鈕會把標籤換成帶勾號的「Copied」，然後再換回來。
- 方塊和列自行繪製；滑鼠移過和取得焦點只會重繪它們。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 彈出框 | 從觸發按鈕淡入並安定下來 | 淡入 | 直接出現 |
| 底部面板 | 升起，跟隨拖曳 | 淡入 | 直接出現 |
| 複製標籤 | 模糊切換 | 交叉淡化 | 直接切換 |

## 響應式行為

- 視窗寬度低於 640 px 時（包含手機預覽），面板從底部升起並橫跨整個寬度；它的高度配合內容，最高為視窗的 92%。
- 作為彈出框時，面板寬 320 px。
- 當內容超過可容納的範圍時，這些列會橫向捲動，並把取得焦點的方塊捲動到可見處。

## 相關元件

- `Popover` 和 `BottomSheet` 是這個面板開啟時使用的兩種表面。
- `CopyButton` 是連結列上的複製按鈕。

| 元件 | 何時選擇 |
|---|---|
| `ShareSheet` | 一個項目可以傳給人、應用程式或一個連結 |
| `CopyButton` | 複製連結是唯一的分享方式 |
| `Dialog` | 分享需要存取設定或一則訊息 |
