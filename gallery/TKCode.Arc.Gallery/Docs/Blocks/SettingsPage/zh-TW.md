---
title: 設定頁面
summary: 完整的設定頁面：導覽區帶有帳戶卡片，旁邊顯示所選區段，並提供未儲存變更的儲存列。
source-hash: 4c5b5cdb75b0
---

<Demo name="Overview" />

## 何時使用

- 應用程式或工作區中有少量區段的設定區域，例如個人資料、通知、安全性與帳單。
- 各區段的變更需要一起儲存，並有清楚的儲存與捨棄步驟。
- 需要從寬視窗一路適應到手機寬度單欄的版面。

## 何時不用

- 只是一組簡單的選項：單獨使用 `SettingsGroup` 搭配 `SettingsRow`。
- 需要在應用程式任何位置依名稱找到並修改某項設定：使用 `SettingsCommand`。
- 在應用程式中不相關的頁面之間導覽：使用應用程式外殼的側邊欄。

## 用法

每個區段寫一個 `SettingsSection`。`Header`、`Icon` 與 `Badge` 會顯示在導覽中，所選區段則在右側顯示其 `Header`、`Description` 與內容。第一個已啟用的區段會被選取；設定 `SelectedIndex` 或呼叫 `Select(section)` 可顯示其他區段。`Title` 位於導覽上方，`Footer` 位於導覽下方，例如帳戶卡片。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SettingsPage Height="480">
        <arc:SettingsSection Header="個人資料" Icon="{x:Static arc:ArcIcons.User}" Description="他人如何看到你。">
            <arc:Input Label="全名" Text="Noor Haddad" />
        </arc:SettingsSection>
        <arc:SettingsSection Header="通知" Icon="{x:Static arc:ArcIcons.Bell}" Badge="2">
            <arc:SettingsGroup Appearance="Plain">
                <arc:SettingsRow Header="提及" Description="有人提及你時。">
                    <arc:Switch IsChecked="True" />
                </arc:SettingsRow>
            </arc:SettingsGroup>
        </arc:SettingsSection>
    </arc:SettingsPage>
</UserControl>
```

### 未儲存的變更

區段有未儲存的變更時，將其 `IsDirty` 設為 `true`。它的導覽項目會顯示一個圓點；選取該區段時，頁面底部會升起一個儲存列，其中有捨棄與儲存兩個按鈕。儲存（或按 Ctrl+S，macOS 上為 ⌘S）會觸發 `SaveRequested`，捨棄則會觸發 `DiscardRequested`。兩者都會帶有 `Section`。儲存或還原變更後，將 `IsDirty` 設回 `false`。

<Demo name="UnsavedChanges" />

```csharp
page.SaveRequested += async (_, e) =>
{
    await SaveProfileAsync();
    e.Section.IsDirty = false;
};
```

### 精簡版面

低於 `CompactWidth`（預設為 720）時，頁面會變為單欄：區段會變成標題下方一排可橫向捲動的膠囊，導覽下方的頁尾隱藏，內容的內距也會縮小。`IsCompact` 反映目前是否為精簡版面。

<Demo name="Compact" />

### 設定群組與設定列

`SettingsGroup` 與 `SettingsRow` 用來建構區段的內容，單獨放在任何地方也能使用。設定列可顯示選用的 `Icon`、`Header` 與 `Description`，並將用來變更設定的控制項作為其內容。寬度低於 `StackWidth`（預設為 440）時，控制項會移到文字下方。當控制項為 `Switch` 或 `Checkbox` 時，點擊文字即可切換該控制項；控制項會以標題與描述作為其無障礙名稱與說明文字。

群組預設繪製為卡片，可選擇設定 `Header`、`Description`、`HeaderActions`，以及帶淡色底的 `Footer` 條。`Appearance="Plain"` 會移除外框，適用於頁面內以細線分隔的群組。

<Demo name="Rows" />

### 文字

`Labels` 包含儲存列的文字與無障礙名稱。若要翻譯，請整體取代這個物件。

```csharp
page.Labels = new SettingsPageLabels
{
    UnsavedChanges = "未儲存的變更",
    Save = "儲存",
    Discard = "捨棄",
};
```

## API 參考

<!-- api: SettingsPage -->
<!-- api: SettingsSection -->
<!-- api: SettingsGroup -->
<!-- api: SettingsRow -->

## 無障礙

- 每個導覽項目都是一個標籤頁項目，名稱依序為區段名稱、徽章；若區段有未儲存的變更，還會加上「有未儲存的變更」。導覽的名稱為「設定區段」。
- 導覽只占一個 Tab 停駐點。上、下方向鍵（精簡版面中為左、右方向鍵）會移至上一個或下一個已啟用的區段並將其顯示；Home 與 End 分別移至第一個與最後一個。它們不會循環。
- 當所顯示的區段有未儲存的變更時，Ctrl+S（⌘S）可儲存。
- 鍵盤焦點會在導覽項目周圍繪製 2px 的焦點環。指標焦點則不會。
- 儲存列中的提示文字位於即時區域（禮貌）中。

## 動態效果

- 選取的膠囊會以輕快的彈簧在導覽項目之間滑動。
- 新進入的區段會上移 6px 並淡入（標準時長、進入緩動），離開的區段則原地淡出。
- 儲存列上移出現，並下移消失。
- 減少動態效果時會保留淡入淡出，取消位移。關閉動態效果時，所有內容會一次切換。

## 相關元件

- `SettingsCommand`、`SecuritySettings`、`BillingOverview`、`UsageBilling` 與 `CancelFlow`：其他設定區塊，可嵌入區段之中。
- `Tabs`：在同一物件的不同檢視之間切換。
- `Switch`、`Input`、`Select`、`BillingToggle`：範例中區段所用的控制項。
