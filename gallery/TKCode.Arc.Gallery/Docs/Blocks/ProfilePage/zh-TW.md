---
title: Profile page
summary: 一個人的個人檔案頁：封面與頭像、附統計數據的簡介欄、Activity 與 Projects 分頁，以及儲存後欄位會亮一下的編輯面板。
source-hash: b37b9dc41572
---

<Demo name="Preview" />

## 何時使用

`ProfilePage` 是使用者開啟自己或他人的個人檔案時看到的頁面。頂部是一張寬幅封面，圓形頭像壓在封面的下緣；姓名、使用者名稱和一句話介紹緊隨其後，右側是 "Share" 與 "Edit profile" 按鈕。下方是一個窄的簡介欄，包含自我介紹、所在地、網站、加入時間以及最多三個數字，其餘空間由兩個分頁佔據：Activity 是依分組列出此人近期動態的清單，Projects 是訪客可以加星號的卡片格線。

"Edit profile" 會讓一個編輯面板在區塊內從右側滑入。它可以編輯姓名、一句話介紹、所在地、網站和自我介紹，並對它們進行檢查；儲存時，新值會寫回頁面，每個有變動的欄位會亮一下，讓讀者看到哪裡變了。

- **產品中成員自己的個人檔案。** 使用者看到的就是別人看到的內容，並且可以就地修改自己的資料，不需要另外的設定頁。
- **團隊成員的頁面。** 關閉編輯（`IsEditable="False"`），同樣的版面就成為一個關於同事的唯讀頁面，展示其近期工作和專案。
- **創作者或作品集頁面。** 專案卡片帶有封面、協作者和星號數，足以作為公開展示頁。

## 何時不使用

- **帳號設定。** 密碼、安全性、通知和帳單應放在 `SettingsPage`、`SecuritySettings` 和 `BillingOverview` 中。本區塊只編輯公開資訊。
- **同時展示很多人。** 瀏覽或搜尋團隊請使用團隊通訊錄區塊或表格；本區塊只針對一個人。
- **行銷用的團隊介紹區。** 介紹團隊的登陸頁需要在格線中放置肖像和簡短介紹，而不是分頁和編輯面板。
- **很長的動態串流。** Activity 分頁只是簡短的摘要（每組只有寥寥幾列）。無盡的歷史紀錄請使用時間軸或分頁清單。

## 用法

本區塊衍生自 Avalonia 的 `TemplatedControl`。給它一個 `ProfileDetails`，再填入三個集合；頭像和封面由你提供（沒有頭像時區塊顯示姓名首字母，沒有封面時顯示柔和的表面色或你設定的 `CoverBrush`）。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ProfilePage ProfileUrl="https://fieldnote.app/@rowan">
        <arc:ProfilePage.Profile>
            <arc:ProfileDetails Name="Rowan Ellery"
                                Handle="rowan"
                                Headline="Product designer at Fieldnote"
                                Location="Bergen, Norway"
                                Website="rowanellery.studio"
                                JoinedText="June 2020"
                                Bio="I design tools for field researchers and care about honest defaults and quiet motion." />
        </arc:ProfilePage.Profile>
        <arc:ProfilePage.Stats>
            <arc:ProfileStat Label="Followers" Value="3107" />
            <arc:ProfileStat Label="Following" Value="212" />
            <arc:ProfileStat Label="Shipped" Value="41" />
        </arc:ProfilePage.Stats>
        <arc:ProfilePage.Activity>
            <arc:ProfileActivity Kind="Shipped" Action="shipped" Target="Color tokens v3" TimeText="3h"
                                 Group="This week" Detail="New contrast pairs for charts and a calmer dark palette." />
            <arc:ProfileActivity Kind="Reviewed" Action="approved" Target="Compact table mode" TimeText="Wed" Group="This week" />
        </arc:ProfilePage.Activity>
    </arc:ProfilePage>
</UserControl>
```

### 結構

- **封面**：寬頁面上高 240（寬度低於 1000 時為 200，低於 640 時為 128）。`ProfileDetails.Cover` 繪製在 `ProfileDetails.CoverBrush` 之上。
- **身分區**：104 的頭像，帶 4px 的表面色描邊，一半壓在封面上；右側是 "Share" 與 "Edit profile"；下方是姓名（28px 展示字體）以及 "@handle · headline"。
- **簡介**：自我介紹為一個段落，然後是所在地、網站（點擊後在瀏覽器中開啟的連結）和 "Joined …"，值為空的列會被隱藏；其下以一條細線隔開，是三等分欄的統計數據。
- **分頁**：Activity 和 Projects 及其數量；2px 的底線標出目前選取的分頁。寬度低於 760 時，簡介區塊移到分頁上方。
- **編輯面板**：寬 420，位於區塊右側，下面有遮罩；無論頁面捲動到哪裡，它都覆蓋區塊的可見部分。寬度低於 420 時它佔滿整個區塊。

### Activity

每個 `ProfileActivity` 讀作 "{名} {Action} **{Target}**"，可帶一行選用的 `Detail`，結尾是 `TimeText`。名取自 `ProfileDetails.Name` 的第一個詞，所以改名後各列會隨之變化。各列依 `Group` 標題分組，分組順序依各組首次出現的先後。`Kind` 決定圖示：`Shipped` 為綠色火箭，`Starred` 為琥珀色星號，`Commented`、`Created` 和 `Reviewed` 為柔和色圖示。

### Projects 與星號

每個 `ProfileProject` 是一張卡片，包含 96px 的封面、標題和 `Visibility`（Public 或 Private）、描述、以重疊頭像形式顯示的 `Collaborators`、`UpdatedText` 以及一個星號按鈕。按下星號會切換 `IsStarred`，並在項目本身上將 `StarCount` 加一或減一（兩者都是雙向的），然後引發 `StarChanged`，附帶該專案、新狀態和數量，以便你的程式碼通知伺服器。卡片分兩欄排列，寬度低於 560 時為一欄。

### 控制分頁

`Tab` 是雙向的。可以繫結它，把分頁保存在你自己的狀態裡（例如網址中），也可以透過其他按鈕設定它；無論變化來自點擊分頁、方向鍵還是繫結，`TabChanged` 都會回報每一次變化，並附上舊分頁和新分頁。

<Demo name="ControlledTab" />

### 儲存

`SaveHandler` 接收編輯後的 `ProfileDetails`（已去除前後空白，使用者名稱、圖片和加入時間沿用原值）並傳回一個工作。工作執行期間，"Save" 按鈕顯示轉圈和 "Saving"；欄位、"Cancel" 和關閉按鈕仍可使用。完成後，`Profile` 會被新物件取代，面板關閉，每個去除空白後的值有變動的欄位會亮一下，`Saved` 會回報先前的檔案、新的檔案和 `ChangedFields`。沒有處理常式時，區塊會模擬一次儲存，在 `SimulatedSaveDelay`（700ms）之後成功。

在處理常式中擲回例外可以讓面板保持開啟：例外的訊息會顯示在面板按鈕上方的紅色提示列中，會被播報，並引發 `SaveFailed`。草稿保持原樣，因此使用者可以再試一次。

```csharp
var http = new HttpClient();
page.SaveHandler = async profile =>
{
    var json = JsonSerializer.Serialize(new { profile.Name, profile.Headline, profile.Location, profile.Website, profile.Bio });
    var body = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await http.PutAsync("https://api.example.com/me", body);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("We could not save your profile. Try again in a moment.");
    }
};
```

<Demo name="SaveError" />

儲存進行中關閉面板並不會取消儲存：完成時值仍會寫回，失敗則會被播報並透過 `SaveFailed` 回報。

### 面板中的規則

- **Name** 為必填：必須包含空白以外的內容。
- **Website** 為選填。填寫時，它必須像一個網址：選用的小寫 `http://` 或 `https://`，至少兩段以點分隔、由字母、數字、`-` 或 `_` 組成的部分（不含連接埠），以及選用的以 `/` 開頭且不含空白的路徑。`example.com`、`www.example.com/work` 和 `https://example.com/p?q=1` 可以通過；`example`、`localhost:3000` 和 `ftp://example.com` 不行。
- **Bio** 最多 `BioMaxLength` 個字元（200）；標籤旁的計數器顯示還剩多少。

錯誤在第一次嘗試儲存之前保持隱藏，之後隨每次按鍵更新。草稿與檔案不同之前，"Save" 保持停用。

### 分享

"Share" 會複製 `ProfileUrl`（未設定時複製 "https://example.com/@{handle}"），按鈕會在 `ShareFeedbackDuration`（1800ms）內顯示 "Copied" 或 "Failed"，且寬度不變。`CopyHandler` 可以取代系統剪貼簿，例如用於加入追蹤或測試失敗的情形；傳回 false 或擲回例外會顯示 "Failed"。`Shared` 回報結果。

### 唯讀個人檔案

設定 `IsEditable="False"` 後，"Edit profile" 按鈕消失，`OpenEditor` 不執行任何動作；其餘功能，包括 "Share" 和星號，仍然可用。

<Demo name="ReadOnly" />

### 窄容器與高度

版面跟隨區塊自身的寬度，而不是視窗的寬度：封面在低於 1000 時縮小，各欄在低於 760 時堆疊，標頭在低於 640 時收緊（128 的封面、84 的頭像和 22px 的姓名），卡片在低於 560 時變為一欄，面板中的 Location 和 Website 欄位在低於 420 時堆疊。區塊高度為 `PageHeight`（760），父層提供的空間更小時則更矮，並在內部捲動。`IsFramed="False"` 會去掉邊框和圓角，適合整個視窗的頁面。

<Demo name="Narrow" />

### 文字

區塊顯示或播報的每個詞都在 `Labels` 中，它是一個帶英文預設值的 `ProfilePageLabels` 物件。取代整個物件即可翻譯；包含數值的文字（自我介紹計數器、"Joined …"、星號按鈕的名稱）是函式，因此語序可以改變。

## API 參考

<!-- api: ProfilePage -->

<!-- api: ProfileDetails -->

<!-- api: ProfileStat -->

<!-- api: ProfileActivity -->

<!-- api: ProfileProject -->

<!-- api: ProfileCollaborator -->

<!-- api: ProfilePageLabels -->

## 無障礙

鍵盤：

- **Tab** 依序移動到 "Share"、"Edit profile"、網站連結、選取的分頁和 Projects 分頁中的星號按鈕。
- 在分頁上按 **Left / Right** 會切換到另一個分頁並立即顯示，首尾循環。
- **Enter / Space** 按下取得焦點的按鈕或星號。
- 在面板中，焦點從 Name 開始，**Tab / Shift+Tab** 只在面板內移動。在單行欄位中按 **Enter** 會儲存；在自我介紹中則換行。**Escape** 不儲存並關閉面板，焦點回到 "Edit profile"。

螢幕閱讀器：

- 簡介欄、分頁欄和分頁列都有名稱（"About"、"Profile content"、"Profile sections"）。分頁是分頁項目；只有選取的那個是 Tab 停駐點。圖示以及使用者名稱與一句話介紹之間的圓點對螢幕閱讀器隱藏。
- "Share" 按鈕是禮貌的即時區域：它的新名稱（"Copied"、"Failed"）會被朗讀，結果也會被播報。
- 每個星號按鈕以專案及其數量命名（"Star Signup study, 34 stars"，加星號後為 "Unstar …"）。
- 面板以其標題命名。每個欄位以其標籤命名；錯誤作為欄位的說明文字給出。儲存失敗顯示在強制播報的即時區域並被播報；儲存成功則播報 "Profile saved"。
- 鍵盤焦點環為 2px，位於元素外側；由指標移動的焦點不顯示焦點環。

## 動態效果

- **分頁**：底線沿臨界阻尼彈簧（Smooth，約 0.9 秒）滑向選取的分頁，位置和寬度一起變化。舊內容在 120ms 內淡出，然後新內容淡入並在 240ms 內上升 6px；新內容就位後，面板高度沿同一彈簧跟隨變化。中途切回時，舊內容會重新淡入。
- **編輯面板**：從其完整寬度處沿 Smooth 彈簧滑入，同時遮罩在 240ms 內淡入；離開時使用更快的臨界阻尼彈簧（約 250ms）。
- **已儲存的欄位**：每個有變動的欄位會得到一層向外延伸 3px 的強調色淡底，保持 480ms，再在 1120ms 內淡出。
- **星號**：加星號時，星號從 60% 開始在快速彈簧上彈出；數量立即變化。取消星號沒有動畫。
- **標籤文字**："Share" 和 "Save" 以短促的淡入淡出、2px 模糊和 3px 上升切換標籤，寬度不變。
- **減少動態效果**：底線和內容立即切換，面板改為淡入（160ms）和淡出而不是滑動，標籤文字立即切換，星號不彈出。儲存時的亮起效果保留（沒有任何東西移動）。**關閉**則立即套用每一次變化，並跳過亮起效果。

## 相關元件

- `SettingsPage` 和 `SecuritySettings`，用於帳號設定。
- `Avatar` 和 `AvatarGroup`，用於其他位置的頭像。
- `Tabs`，用於本區塊之外的分頁。
- `Drawer`，用於視窗層級的側邊面板。
