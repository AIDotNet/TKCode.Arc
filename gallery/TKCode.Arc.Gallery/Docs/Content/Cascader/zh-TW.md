---
title: 級聯選擇器
summary: 依層級逐級選擇一個節點，如省、市、區，欄位和選單與 Select 一致。
source-hash: cf03d1ad0529
---

<Demo name="Basic" />

## 何時使用

`Cascader` 在層級資料中選擇一個節點。開啟後先顯示第一級；選取一個父節點，它的子級會在右側的一欄中展開，最後選取的是葉節點。欄位隨後顯示完整路徑。適用於層級固定、共 2 到 5 級、而且使用者需要看到每一級同層項目的情境。

- **地址表單中的省、市、區**：三級合起來才是一個答案，儲存的就是完整路徑。
- **商品目錄編輯器中的分類**：例如「電子產品 / 手機 / 智慧型手機」。
- **組織架構中的職位**：在一個欄位裡選出部門和小組。

## 何時不用

- **很深的樹，或某一級有上千個節點**：把 `TreeView` 放進 `Popover`，或使用可搜尋的 `Combobox`。
- **只有一級**：使用 `Select` 或 `Combobox`。
- **一次選多個值**：多選版本不屬於 `Cascader`。
- **跨層級搜尋，或依需求載入子級**：本版本不支援。

## 用法

`Cascader` 衍生自 Avalonia 的 `TemplatedControl`。把根節點交給 `ItemsSource`，再用委派告訴它如何讀取節點：`ChildrenSelector`（與 `TreeView` 的簽章相同）、`TextSelector` 和 `ValueSelector`。不使用反射，因此相容 AOT 和裁剪。

```csharp
public sealed record Region(string Code, string Name, IReadOnlyList<Region>? Children = null);

cascader.ItemsSource = regions;
cascader.ChildrenSelector = node => ((Region)node).Children;
cascader.TextSelector = node => ((Region)node).Name;
cascader.ValueSelector = node => ((Region)node).Code;
```

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Cascader Label="Region"
                  Description="Pick a province, a city and a district."
                  Placeholder="Select a district"
                  Width="320" />
</UserControl>
```

沒有子節點的節點，或被 `IsLeafSelector` 判定為葉節點的節點，就是葉節點。欄位、選單表面、滑動醒目提示和清除按鈕與 `Select`、`Combobox` 相同。

### 選擇父節點

預設只能選葉節點：點擊父節點只會展開它的子級。把 `LeafOnly` 設為 `False`，任何一級都可以被選取。此時點擊父節點會選取它，並保持選單開啟，使用者仍然可以繼續往下選。

<Demo name="ParentSelectable" />

### 欄位顯示什麼

`ShowFullPath`（預設為 true）顯示完整路徑，節點之間用 `PathSeparator`（" / "）連接。設為 `False` 則只顯示選取的節點。`SelectedText` 就是欄位顯示的文字。

<Demo name="LeafName" />

### 滑鼠移入展開

`ExpandTrigger` 決定父節點如何展開子級。`Click`（預設）透過點擊或鍵盤展開；`Hover` 在指標停在父節點上時也會展開。

<Demo name="HoverExpand" />

### 繫結到檢視模型

`SelectedItem`、`SelectedValue` 和 `SelectedPath` 始終保持一致，前兩個是雙向的。值透過對 `ValueSelector` 傳回的內容呼叫 `Equals` 來比較，所以同名的兩個節點靠值區分。**值必須在整棵樹中唯一**：每個省下面都從 01 開始編號的代碼不是唯一的。如果兩個節點的值相同，取第一個，Debug 建置會輸出一則警告。

資料裡還沒有的值會被保留。資料載入期間欄位顯示預留位置文字，`SelectedValue` 保持寫入時的樣子；當 `ItemsSource` 或集合發出變更通知時，控制項會找到對應節點，並補齊 `SelectedItem` 和 `SelectedPath`。把資料換成不含該節點的集合，會移除選取項目和路徑，但保留值，所以重新載入不會沖掉檢視模型裡的值。

<Demo name="Binding" />

根集合發出 `CollectionChanged` 時會被監聽；它下面各級集合的變更在選單下次開啟時才會顯示。

### 停用與唯讀

`DisabledSelector` 標記顯示但不能選取或展開的節點。`IsReadOnly` 讓欄位可以取得焦點、可以開啟檢視，但不能修改或清除。`IsEnabled="False"` 停用整個欄位。

<Demo name="Disabled" />

### 清除

有值時，`IsClearable`（預設為 true）會顯示清除按鈕；點擊它會清除值，而且不會開啟選單。程式碼中可以呼叫 `Clear()`，欄位取得焦點時按 Delete 或 Backspace 也一樣。

## API 參考

<!-- api: Cascader -->

<!-- api: CascaderSelectionChangedEventArgs -->

<!-- api: CascaderExpandTrigger -->

## 協助工具

- 欄位對輔助技術呈現為可展開、可收合的組合方塊，它的值是 `SelectedText`。`Label` 是協助工具名稱，`Description` 是說明文字。
- 醒目提示移動時會播報節點名稱、所在層級和該欄的節點數量。
- 關閉選單後，焦點回到欄位上。

### 鍵盤

| 按鍵 | 條件 | 行為 |
|---|---|---|
| Enter、Space、Down、Up | 選單關閉 | 開啟選單並定位到已選路徑 |
| Delete、Backspace | 選單關閉且有值 | 清除值 |
| Up、Down | 選單開啟 | 在目前欄移動，跳過停用的節點 |
| Home、End、Page Up、Page Down | 選單開啟 | 在目前欄內跳轉 |
| Right | 選單開啟 | 展開醒目提示的父節點，並移到它的第一個子節點 |
| Left | 選單開啟 | 回到上一欄，醒目提示停在它的父節點 |
| Enter、Space | 選單開啟 | 選取葉節點；展開父節點（`LeafOnly` 為 false 時選取它） |
| Escape | 選單開啟 | 關閉，不變更值 |
| Tab | 選單開啟 | 關閉，不變更值，並移開焦點 |
| 字母鍵 | 選單開啟 | 跳到目前欄中下一個以該字母開頭的節點 |

## 動態效果

- 選單的開啟和關閉與 `Select` 相同。
- 欄出現或消失時，選單寬度用彈簧過渡到新的寬度，進行中的彈簧會被改向，而不是等它結束。新的一欄淡入，同時從左側滑入 8 px。選單開啟時，已選路徑上的所有欄會一次擺好，不會逐欄播放動畫。
- 醒目提示在列之間滑動，選取變更時欄位中的文字會捲動。
- 減少動態效果時，寬度不做動畫，新欄只淡入；關閉動態效果時一切立即完成。

## 相關元件

- `Select`：單級選項。
- `Combobox`：單級，透過輸入來搜尋。
- `TreeView`：在頁面上顯示整棵樹。
