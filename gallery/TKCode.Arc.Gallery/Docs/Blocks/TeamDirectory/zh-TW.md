---
title: Team directory
summary: 具備搜尋與團隊篩選的公司通訊錄，所選成員的個人檔案顯示在清單旁，從簡介與狀態到可複製的電子郵件地址。
source-hash: 5e6a845c52f5
---

<Demo name="Preview" />

## 何時使用

`TeamDirectory` 是同事互相查找的頁面。左側的清單會隨著輸入與所選團隊縮小範圍；右側完整顯示一個人：一段簡短的簡介、是否在線上、所屬團隊、正在進行的工作、所在地點，以及附有複製按鈕的電子郵件地址。

- **內部人員頁面。** 公司或某個部門的所有人，可依姓名、角色、團隊或城市搜尋。
- **含部門的團隊頁面。** 團隊按鈕一鍵檢視各個部門，並顯示固定的人數。
- **顯示脈絡的聯絡人選擇器。** 選一個人，了解他是誰並複製他的地址，或回應 `PersonSelected` 在別處開啟其完整個人檔案。

## 何時不用

- **管理成員。** 變更角色、邀請或移除成員屬於 Team members 或 Roles and permissions；此 Block 僅供讀取。
- **對外公開的團隊區塊。** 帶有大幅肖像與簡短引言的行銷頁面需要以圖片為主的版面，而不是可搜尋的清單。
- **大型人員表格。** 數百列並附排序與多欄的情境應使用 `DataGrid`。通訊錄在記憶體中篩選並配置每一列；對於大型組織，請在伺服器端篩選，只給此 Block 一頁人員。

## 用法

此 Block 衍生自 Avalonia 的 `TemplatedControl`。每個人是一個 `TeamDirectoryPerson`，它是一個 `AvaloniaObject`，包含姓名、角色、團隊、可用狀態、目前的工作、附時區的城市、電子郵件地址、簡介，以及選用的肖像（沒有肖像時顯示姓名縮寫）。變更任一欄位都會更新該列、個人檔案與符合結果。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TeamDirectory x:Name="Directory">
        <arc:TeamDirectoryPerson Id="lena" Name="Lena Hartmann" Role="Interaction designer" Team="Design"
                                 Focus="Checkout redesign" City="Vienna" TimeZone="CET"
                                 Email="lena@northwind.test"
                                 Bio="Turns rough ideas into flows people can follow." />
        <arc:TeamDirectoryPerson Id="theo" Name="Theo Brandt" Role="Mobile engineer" Team="Engineering"
                                 Availability="Away" City="Manchester" TimeZone="GMT"
                                 Email="theo@northwind.test" />
    </arc:TeamDirectory>
</UserControl>
```

### 結構

- **標題列**：標題與人數（永遠是總數），以及附清除按鈕的搜尋框。
- **團隊按鈕**：先是 All，然後是每個團隊及其總人數。一個柔和的膠囊位於被按下的按鈕後方，並滑向下一個按鈕。
- **清單**：每個符合項目一列：附可用狀態圓點的頭像、姓名與角色。正在顯示個人檔案的那一列位於一塊醒目提示之上，提示會在列與列之間滑動。
- **個人檔案**：大頭像、姓名與角色、簡介，以及 Status、Team、Working on、Location（城市與時區）和 Email（郵件連結與複製按鈕）等欄位。空白欄位會被省略。

### 搜尋與團隊

搜尋會在每次按鍵時執行。文字會去除前後空白，不分大小寫，作為一個整體與姓名、角色、團隊和城市比對："lena hart" 找得到 Lena Hartmann，"hartmann lena" 則找不到，而 "operations" 也會找到角色中沒有寫、但團隊是它的人。重音符號不會被摺疊，所以 "malmo" 找不到 Malmö。設定 `SearchFilter` 可以用不同的方式搜尋；它會收到每個人以及輸入的原始文字。

填寫了 `Teams` 時，團隊按鈕來自它，否則來自依首次出現順序排列的人員。按鈕上的數字是總數，不隨搜尋變動。`SelectedTeam`（All 為 null）與 `SearchText` 是雙向且彼此獨立的：變更其中一個會保留另一個。`MatchCount` 與 `VisiblePeople` 說明目前列出了什麼。

### 顯示的成員

`SelectedPersonId` 保存所選成員的索引鍵（即其 `Id`，沒有 `Id` 時則為其姓名）；只有點選、在某一列上按 Enter 或 Space，或是你的程式碼才會變更它。`DisplayedPerson` 是正在顯示個人檔案的人：所選的人在符合時顯示他，否則顯示第一個符合項目。把所選的人篩選掉，就會顯示第一個符合項目；把他找回來，他又會顯示。`PersonSelected` 與 `PersonSelectedCommand` 只回報點選，從不回報退回顯示，也不回報對同一個人的第二次點選。`SelectPerson` 從程式碼中顯示某個人，而不觸發該事件。

```csharp
directory.PersonSelected += (_, e) => navigator.Open($"/people/{e.Person.Id}");
directory.SelectedTeam = "Engineering";
directory.SelectPerson("jonas");
```

<Demo name="BoundState" />

### 沒有符合項目

沒有人符合時，清單會帶著搜尋文字說明這一點，並提供 Clear filters，它會清空搜尋並重新顯示所有團隊（所選的人會保留）；個人檔案區域顯示一則簡短提示而不是某個人。`ClearFilters()` 在程式碼中做同樣的事。

### 複製電子郵件地址

複製按鈕會複製所顯示成員的地址，然後在 `CopyResetDelay`（1.8 秒）內顯示 Copied（附打勾）或 Retry，之後恢復原狀；狀態訊息會告訴螢幕閱讀器結果如何。預設會寫入系統剪貼簿。設定 `CopyAsync` 可以用其他方式複製（遠端工作階段、稽核呼叫）：發生例外時顯示 Retry，`EmailCopied` 會回報每一次嘗試，包含該成員、地址與錯誤。地址本身是一個郵件連結；`EmailLinkClicked` 會先觸發，設定 `Handled` 可阻止 mailto: 連結開啟。

<Demo name="CopyFailure" />

### 窄容器

此 Block 讀取的是自身的寬度，而不是視窗的寬度。寬度不超過 640 時，個人檔案會收緊，複製按鈕只顯示圖示；不超過 500 時，個人檔案移到清單下方，按鈕重新顯示文字；不超過 400 時，搜尋框在標題下獨占一行，團隊人數被隱藏。

<Demo name="Narrow" />

### 有框線或扁平

`IsFramed`（預設）會繪製一張帶 1px 框線與 16 圓角的卡片。設為 false 可把通訊錄平放在頁面或窗格中。`IsSearchVisible` 與 `IsFilterVisible` 會隱藏搜尋框與團隊按鈕；`Labels` 接受一個 `TeamDirectoryLabels`，其中包含每個詞語與螢幕閱讀器文字，可用於翻譯或改寫它們。

<Demo name="Flat" />

## API 參考

<!-- api: TeamDirectory -->

<!-- api: TeamDirectoryPerson -->

<!-- api: TeamDirectoryLabels -->

<!-- api: TeamDirectoryPersonSelectedEventArgs -->

<!-- api: TeamDirectoryEmailCopiedEventArgs -->

<!-- api: TeamDirectoryPersonEventArgs -->

## 協助工具

- **Tab** 從搜尋框（有文字時還包括它的清除按鈕）依序移到每個團隊按鈕、每一列、郵件連結與複製按鈕。每一列與每個團隊按鈕都是獨立的停駐點。
- **Enter** 或 **Space** 按下團隊按鈕、點選一列，或按下複製按鈕。
- 在搜尋框中按 **Escape** 會清空它並把焦點留在其中；搜尋框為空時則交給頁面處理。
- 清除按鈕會清空搜尋並把焦點放回搜尋框。如果焦點原本在搜尋框中，Clear filters 會把焦點移到搜尋框，而不是讓焦點隨訊息一起消失。
- 團隊按鈕與列是切換按鈕：螢幕閱讀器會把被按下的團隊與正在顯示個人檔案的列讀作已按下。團隊按鈕構成一個有名稱的群組（"Team filter"），每個按鈕讀出其團隊與總人數（"Design, 2"）；清單也有名稱（"Directory"）。
- 一列讀出姓名與角色；其頭像讀出姓名，以及此人在線上還是離開。
- 個人檔案區域是一個禮貌（polite）的即時區域，因此在人員變更時會讀出新的人；個人檔案以姓名與角色命名。
- 複製按鈕名為 "Copy email address"，複製後變為 "Email address copied"；結果也會被播報，按鈕恢復原狀時播報會被清除。
- 每個詞語與名稱都在 `Labels` 中。

## 動態效果

| 內容 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 團隊膠囊移到被按下的按鈕 | 彈簧（視覺時長 .42，無回彈），位置與寬度 | 直接跳到位 | 直接跳到位 |
| 列醒目提示移到所顯示的列 | 同一條彈簧 | 直接跳到位 | 直接跳到位 |
| 符合項目變動時留下的列 | 在同一條彈簧上滑向新位置 | 直接跳到位 | 直接跳到位 |
| 離開的列 | 在原處於 170ms 內淡出，不佔空間 | 立即移除 | 立即移除 |
| 新出現的列 | 在原處於 150ms 內淡入 | 直接出現 | 直接出現 |
| 個人檔案切換 | 舊的在 180ms 內淡出；新的在 220ms 內淡入並上升 4px | 直接替換 | 直接替換 |
| 複製確認 | 圖示在 160ms 內交叉淡化，並在 580ms 的彈簧上縮放；顏色立即變化 | 顏色與圖示立即變化 | 立即變化 |
| 暫留與焦點顏色 | 160ms | 立即變化 | 立即變化 |

被打斷的滑動會從目前繪製的位置帶著速度繼續；被打斷的個人檔案會從已達到的透明度開始淡出，所以快速連續點選永遠不會出現跳動。

## 相關

- `Avatar`：附姓名縮寫與可用狀態圓點的頭像。
- `SearchField` 與 `CopyButton`：獨立的搜尋輸入框與複製按鈕。
- `GlidingIndicator`：在各項目之間滑動的醒目提示。
