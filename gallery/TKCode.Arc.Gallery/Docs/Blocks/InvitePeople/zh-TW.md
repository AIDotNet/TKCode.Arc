---
title: Invite people
summary: 依姓名或電子郵件邀請人員加入工作區，可為每個人指定角色，並依方案顯示席位用量；頭像會從建議飛入標籤，再飛入待接受的邀請清單。
source-hash: 35edc5dc76be
---

<Demo name="Preview" />

## 何時使用

`InvitePeople` 是「Invite people」按鈕背後的表單：一個可輸入姓名與地址的欄位、一項席位檢查，以及仍在等待接受的邀請。在通訊錄中找到的人會變成帶有頭像與角色的標籤；無法邀請的內容會變成紅色標籤，並說明原因。傳送後，標籤會移入待接受的邀請清單，每筆邀請都可以重新傳送或撤回；邀請被接受後會離開清單，並加入頂端的團隊頭像中。

- **一次邀請多人。** 輸入幾個字母即可從通訊錄中選人，也可以輸入地址，或從試算表或電子郵件貼上整份名單：逗號、分號與換行都會分隔各個項目。
- **需要付費的席位。** 用量條依方案統計成員、待接受的邀請與新增標籤。數量超出時，傳送按鈕會被暫停，並就地提供下一個方案。
- **對話方塊或設定頁。** 有邊框時，本 Block 是最寬 600 的卡片；無邊框時，它放在你自己的容器中。

## 何時不用

- **管理已經加入的人。** 成員的角色、移除與席位變更請使用 `Team members`。
- **編輯角色能做什麼。** 請使用 `Roles and permissions`。
- **一般標籤。** 當項目不是人員（標籤、關鍵字、沒有通訊錄的收件者）時，請使用 `TagInput`。

## 用法

本 Block 衍生自 Avalonia 的 `TemplatedControl`。人員是 `InvitePeoplePerson` 物件（姓名、電子郵件、職稱與選用的肖像；沒有肖像時頭像顯示姓名縮寫）。`Directory` 存放所有可被找到的人，`Members` 是團隊，`PendingInvites` 是正在等待的邀請；通訊錄中已在團隊內或已有邀請的人，在建議中顯示為不可選。`Roles` 與 `Plans` 分別填入角色選單與席位用量條。

```xml
<arc:InvitePeople Title="Invite people"
                  Description="Bring people into Lumen Labs by name or email"
                  WorkspaceName="Lumen Labs">
    <arc:InvitePeople.Roles>
        <arc:InvitePeopleRole Name="Admin" Description="People, billing and settings" />
        <arc:InvitePeopleRole Name="Member" Description="Works on every project" IsDefault="True" />
        <arc:InvitePeopleRole Name="Guest" Description="Sees only shared projects" />
    </arc:InvitePeople.Roles>
    <arc:InvitePeople.Plans>
        <arc:InvitePeoplePlan Name="Basic" SeatLimit="10" />
        <arc:InvitePeoplePlan Name="Growth" SeatLimit="25" />
    </arc:InvitePeople.Plans>
    <arc:InvitePeoplePerson Name="Clara Weiss" Email="clara.weiss@lumen.example" Title="Brand designer" />
    <arc:InvitePeoplePerson Name="Mateus Rocha" Email="mateus.rocha@lumen.example" Title="Backend engineer" />
</arc:InvitePeople>
```

### 結構

- **標頭**：`Title`、`Description` 與團隊頭像（最多五個，其餘以「+N」膠囊表示）。在窄版 Block 中，頭像移到標題下方。
- **欄位**：標籤與文字方塊會依需要換行，欄位的高度以彈簧跟隨變化。下方是提示文字，以及在設定了 `SampleText` 時用於貼上它的按鈕。
- **錯誤面板**：當有標籤無法邀請時展開，每個標籤一行，並附有一個全部移除它們的按鈕。
- **席位**：「8 of 10 seats」、方案名稱、分為三段（成員、待接受、新增）的用量條與圖例。超出上限時，數量與新增段變紅，並有一行提示下一個方案。
- **頁尾**：重設按鈕（當 `IsResetVisible` 時顯示）與傳送按鈕，傳送按鈕的文字會統計標籤數量。
- **待接受**：邀請清單，最新的在前，每筆附有狀態、重新傳送按鈕與撤回按鈕。

### 新增人員

輸入時依姓名或電子郵件篩選通訊錄（不會搜尋職稱）；可邀請的人排在前面，不可選的排在後面，最多五個（`MaxSuggestions`）。欄位為空時，先顯示四個人（`SuggestedCount`）。不在通訊錄中的完整地址會提供「Invite by email」。按 Enter 或點擊會選取反白的人；沒有反白時按 Enter、逗號或分號，會把文字變成標籤。

貼上一份名單（或呼叫 `AddText`）時，會辨識通訊錄中的姓名與地址、新地址以及 `Name <address>` 格式；它會略過重複項，並把無法邀請的項目以紅色標籤加入。欄位下方的提示會顯示幾秒鐘的摘要。選取紅色標籤會把它的文字放回欄位以便修改。

### 傳送

將 `SendAction` 設為傳送按鈕要執行的工作。它會收到新的待接受列（狀態為 `Sending`）與一個取消權杖。每封邀請送出時呼叫 `MarkDelivered`；工作完成時仍在傳送中的列會被標記為已傳送。有人接受邀請時呼叫 `MarkJoined`：該列顯示「Joined」，片刻後離開，此人加入 `Members`，其頭像飛入標頭。發生例外時，這些列會被收回，標籤回到欄位。

```csharp
// Your mail call; here it only waits.
static Task SendInviteAsync(string email, string? role, CancellationToken token) => Task.Delay(300, token);

invite.SendAction = async (request, token) =>
{
    foreach (var pending in request.Invites)
    {
        await SendInviteAsync(pending.Person!.Email!, pending.Role?.Name, token);
        invite.MarkDelivered(pending);
    }
};
invite.InviteJoined += (_, e) => log.Info($"{e.Invite.Person?.DisplayName} joined");
```

當標籤需要修正或席位超出時，傳送按鈕會被暫停（它仍可取得焦點並說明原因）；此時按下它，會讓紅色標籤或超限提示抖動。`ResendAction`、`RevokeAction` 與 `UpgradeAction` 對列內按鈕與升級按鈕的作用方式相同。下方的示範讓每第二次傳送失敗，並讓撤回 Theo 的邀請失敗，因此該列會回來。

<Demo name="Failure" />

### 狀態與事件

`Entries` 列出標籤，`SeatsUsed` 與 `IsOverLimit` 可以繫結，`CurrentPlan` 是雙向的。`EntriesChanged`、`RoleChanged`、`InvitesSent`、`SendFailed`、`InviteJoined`、`InviteResent`、`InviteRevoked` 與 `PlanUpgraded` 會回報發生的事情。`Reset()` 讓 Block 回到最初的樣子，並觸發 `ResetPerformed`。

<Demo name="EventLog" />

### 有邊框與無邊框

`IsFramed`（預設值）會繪製一張最寬 600 的卡片，帶 1px 邊框與 16 的圓角，頁尾與待接受清單上方有分隔線。將其設為 false，可把本 Block 放進你自己的對話方塊或面板中。

<Demo name="Flat" />

### 窄容器

本 Block 會讀取自身的寬度。寬度為 480 或更小時，內距從 20 收緊到 16；當內容窄於 440 時，待接受的列會把狀態放在姓名下方，建議中則省略職稱。

<Demo name="Narrow" />

### 其他文字

所有文字都在 `Labels`（`InvitePeopleLabels`）中，預設是英文：預留位置文字、提示、錯誤原因、席位列、按鈕名稱與每則朗讀通知。帶數值的文字是函式，因此語序可以改變。

## API 參考

<!-- api: InvitePeople -->

<!-- api: InvitePeoplePerson -->

<!-- api: InvitePeopleRole -->

<!-- api: InvitePeoplePlan -->

<!-- api: InvitePeoplePendingInvite -->

<!-- api: InvitePeopleEntry -->

<!-- api: InvitePeopleSendRequest -->

## 無障礙

- 文字方塊名為「Email or name」，並由提示文字描述。建議清單不可取得焦點：反白移動時焦點仍留在文字方塊中，不可選的人會說明原因。
- 紅色標籤的按鈕名為「Edit {entry}. {reason}」；移除按鈕會說明移除的是哪個標籤；角色按鈕會說明這是誰的角色以及目前是什麼。
- 席位用量條名為「Seats used」，其值讀作「8 of 10 seats」（並附上超出上限的數量）。
- 傳送按鈕以數量命名（「Send 3 invites」）。被暫停時，它會說明原因：錯誤面板的標題，或超出上限的席位數。
- 撤回之後，焦點移到「待接受」標題。團隊頭像是一個群組（「Team, 6 people」）；「+N」膠囊讀作「and N more」。
- 禮貌模式的即時區域會播報每一步：新增與移除的人、貼上摘要、被標記的標籤、角色變更、傳送被暫停的原因、正在傳送與已傳送、被接受的邀請、重新傳送、撤回、升級與重設。

### 鍵盤

- 在欄位中按 **Arrow Down** 開啟建議（欄位為空時顯示初始清單）；**Arrow Up** 與 **Arrow Down** 移動反白並跳過不可選的人。**Enter** 新增反白的人，沒有反白時把文字變成標籤。**逗號**與**分號**會把文字變成標籤。**Escape** 先關閉清單，再清除文字。
- 在空欄位中按 **Backspace** 會標記最後一個標籤；再按一次 **Backspace** 將其移除。輸入任何內容都會清除標記。
- **Tab** 依序經過每個標籤的角色按鈕與移除按鈕，然後是欄位、貼上按鈕、錯誤面板的按鈕、升級按鈕、重設、傳送，以及每個待接受列的重新傳送與撤回按鈕。
- 在角色按鈕上，**Enter**、**Space** 或 **Arrow Down** 會開啟角色選單，焦點落在目前的角色上。**Arrow Up** 與 **Arrow Down** 循環移動，**Home** 與 **End** 跳轉，**Enter** 或 **Space** 選取，**Escape** 關閉；焦點回到角色按鈕。

## 動態效果

- **頭像飛行**：從建議飛入新標籤，傳送時從標籤飛入它的待接受列，被接受後從該列飛入標頭的團隊，每次都用一根彈簧（約 600 ms），出發處此時已經空出。
- **版面配置**：標籤增減時，標籤與文字方塊以彈簧移到新位置，欄位的高度及其下方的一切以平滑彈簧跟隨，待接受列也以同樣方式展開與收起（收起的列還會在 130 ms 內淡出）。
- **建議**：清單上升 4 px 並從 0.98 放大；反白在列與列之間滑動。角色選單從它的按鈕以同樣的方式進入。
- **數字與文字**：席位數、待接受數量與傳送按鈕上的數量會滾動；變化的文字（角色、方案、傳送按鈕的名詞、列的狀態、「Sent again」）帶短暫模糊交叉淡入淡出，同時寬度以彈簧變化。
- **拒絕回饋**：紅色標籤出現時抖動一次；傳送被暫停時按下它，紅色標籤（或超限提示）會抖動：6、6、4、4、2 px，歷時 420 ms。
- 減少動態效果會讓一切立即就位，只保留短暫的淡入淡出與顏色變化；沒有飛行、抖動與彈簧。關閉時所有變化立即生效。

## 相關

- `Team members`、`Roles and permissions`、`Team directory` 與 `Profile page`，即其他 People 區塊。
- `TagInput` 用於一般標籤，`Avatar` 與 `AvatarGroup` 用於肖像，`UsageMeter` 用於獨立的用量條。
