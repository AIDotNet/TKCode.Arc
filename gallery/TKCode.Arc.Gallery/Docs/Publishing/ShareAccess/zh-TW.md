---
title: Share access
summary: 文件共用對話框：依電子郵件邀請成員並設定角色，修改或移除每個人的權限，選擇還有誰能開啟連結，並複製連結。
source-hash: f9608d5f119f
---

<Demo name="Preview" />

## 何時使用

`ShareAccess` 是文件、看板或資料夾「共用」對話框的內容。頂部的地址欄把輸入或貼上的每個電子郵件變成一個標籤，旁邊的選擇框設定新成員的角色（檢視者、評論者或編輯者），點擊 Invite 把他們加入清單。下面，每個有權限的人都有一行，顯示頭像、名字和電子郵件，以及一個角色按鈕，它的選單可以修改角色或移除這個人；擁有者那一行不能修改。「General access」決定還有誰能開啟連結：只有清單中的人、組織內的任何人，或擁有連結的任何人，每種都有自己的角色。底部可以複製連結並關閉對話框。

- **文件、看板和檔案**：人們和同事或訪客共用它們。
- **有組織的工作區**：中間的連結設定讓同事可以開啟，而不必公開連結。
- **管理頁面**：列出誰能看到某個資源。

## 何時不用

- **分享到其他應用程式**（訊息、社群貼文）：使用系統的分享面板，或 `ReferralCard` 的分享按鈕。
- **管理整個工作區的成員**，包括邀請、席位和帳單：使用成員表格。
- **只有一個連結**：在唯讀欄位旁邊使用 `CopyButton`。

## 用法

`ShareAccess` 位於 Special 套件中：在 `UseArc()` 旁邊用 `UseArcSpecial()` 註冊它。設定文件的名稱和連結、組織名稱（用於中間的連結設定）以及成員。把它放進 `Dialog` 或 `Popover`，也可以直接放在設定頁面中。

```xml
<arc:ShareAccess DocumentName="Spring trail plan"
                 OrganizationName="Fernhollow"
                 Link="https://notes.example/d/7hq2"
                 CurrentUserEmail="mira@fernhollow.example">
    <arc:ShareAccess.People>
        <arc:ShareAccessPerson Name="Mira Holt" Email="mira@fernhollow.example" Role="Owner" />
        <arc:ShareAccessPerson Name="Tobias Brandt" Email="tobias@fernhollow.example" Role="Editor" />
    </arc:ShareAccess.People>
</arc:ShareAccess>
```

每個變更都會立即在控制項中生效，並觸發一個事件供應用程式儲存：`PeopleInvited`（新加入的人和他們的角色）、`RoleChanged`、`AccessRemoved` 和 `LinkAccessChanged`。`LinkScope`、`LinkRole` 和 `InviteRole` 是雙向的。按下 Done 時觸發 `DoneRequested`，在這裡關閉對話框。

### 先寄出邀請

設定 `InviteHandler`，在成員加入清單之前先寄出邀請。它執行期間 Invite 按鈕顯示載入動畫；如果它擲回例外，例外訊息會顯示在地址欄下方，標籤保留以便再試一次。下面的範例會等待 0.9 秒，並拒絕任何包含「blocked」的地址。

<Demo name="InviteHandler" />

### 窄對話框

寬度小於 460px 時，角色選擇框和 Invite 按鈕移到地址欄下方，內距減為 16px。

<Demo name="Narrow" />

### 減少動態效果

在減少動態效果下，行和連結角色選擇框只淡入淡出而不移動，標籤交叉淡化。

<Demo name="ReducedMotion" />

### 結構

1. **標題**：使用展示字型的「Share “名稱”」。
2. **邀請列**：地址欄（`TagInput`）、角色選擇框（`Select`）和 Invite 按鈕。
3. **成員**：52px 高的行，包含小號 `Avatar`、名字和電子郵件（或「Invite sent」），以及帶選單的角色按鈕；清單超過 260px 時可以捲動。
4. **General access**：一個圓形圖塊，顯示鎖、人群或地球圖示；範圍選擇框和一行說明；連結開放時還有連結角色選擇框。
5. **底部**：`CopyButton`「Copy link」和 Done 按鈕。

### 內容建議

- 用人們熟悉的名稱稱呼文件；標題最多兩行。
- 有名字時就提供名字；沒有名字的行只顯示電子郵件。
- `OrganizationName` 保持簡短（「Fernhollow」，而不是正式全名）：它會出現在「Anyone at …」中。

### 常見問題

**什麼算是一個電子郵件地址？** 包含一個 @，網域中有點號且最後一段至少兩個字母的文字。從郵件用戶端貼上的「Name &lt;address&gt;」會保留其中的地址。比較地址時不區分大小寫。

**可以用程式碼邀請嗎？** `InviteAsync` 做的事和按鈕一樣；`ChangeRole` 和 `RemovePerson` 作用於一行。

## API 參考

<!-- api: ShareAccess -->

<!-- api: ShareAccessPerson -->

<!-- api: ShareAccessLabels -->

<!-- api: ShareAccessInvitedEventArgs -->

<!-- api: ShareAccessRoleChangedEventArgs -->

<!-- api: ShareAccessRemovedEventArgs -->

<!-- api: ShareAccessLinkChangedEventArgs -->

## 無障礙

- 標題是二級標題。地址欄、選擇框和清單都有名稱（「Add people by email」「Role for new people」「Who can open the link」「People with access」）。
- 每個角色按鈕的名稱包含這個人和角色，例如「Role of Tobias Brandt: Editor」；它的選單勾選目前角色，最後一項是「Remove access」。
- 被拒絕的地址會在地址欄下方顯示原因。一個禮貌的即時區域會播報邀請、角色修改、移除和失敗。
- 用鍵盤移除某人後，焦點移到下一個人的角色按鈕；沒有其他人時移到地址欄。
- 範圍圖塊只是裝飾；選擇框和說明行表達含義。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| Enter、逗號 | 在地址欄中，把輸入的地址變成標籤 |
| 空輸入時按 Enter、Ctrl/Cmd+Enter | 寄出邀請 |
| Backspace | 在空的地址欄中，先選取最後一個標籤，再按一次移除它 |
| Tab | 依序經過地址欄、角色選擇框、Invite、每個角色按鈕、連結選擇框、Copy link 和 Done |
| Enter / Space / Down | 在角色按鈕或選擇框上，開啟它的選單 |
| Escape | 關閉開啟的選單或選擇框 |

## 動效

- 新成員上升 6px 進入一個逐漸展開的行；被移除的人淡出，同時這一行收起。
- 角色按鈕的標籤使用 Arc 的標籤切換；範圍圖示彈出切換，說明行同樣切換，連結角色選擇框淡入，同時它的位置變寬。
- 標籤、選擇框、選單和按鈕保留它們自己的動效。
- 減少動態效果時只保留淡入淡出；關閉動效時一切立即完成。

## 響應式

- 對話框最寬 520px，可以隨容器變化。寬度小於 460px 時邀請列堆疊（地址欄在上，角色和 Invite 在下），內距為 16px。
- 名字和電子郵件以省略號結束；角色按鈕保持自己的大小。
- 每個按鈕至少 36px 高，角色選單輕觸即可開啟；沒有任何功能依賴懸停。

## 相關元件

- `TagInput`：單獨的地址欄。
- `Select`：角色和範圍選擇框。
- `AvatarGroup`：用精簡的一行顯示誰有權限。
- `CopyButton`：複製一個值並給出回饋。
- `Dialog`：承載共用對話框。
