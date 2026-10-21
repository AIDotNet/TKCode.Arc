---
title: 大頭貼群組
summary: 在較小的空間內展示團隊或一組貢獻者。
source-hash: 36b0c9528548
---

<Demo name="Team" />

## 何時使用

- 標明誰擁有或正在編輯某個項目，用於頁面標頭、卡片或表格儲存格。
- 人數較少的團隊清單，前幾位較為重要，其餘可收合為數量。
- 頁面開啟期間會變動的群組：人員加入或離開時，大頭貼堆疊隨之移動。

## 何時不用

- 只有一個人：用 `Avatar`。
- 需要閱讀每個人的姓名與角色的清單：用 `SortableDataTable` 或一般清單。
- 某位人員的帳號選單：用 `UserMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AvatarGroup Label="編輯者">
        <arc:AvatarGroupMember DisplayName="Maya Chen" Status="Online" />
        <arc:AvatarGroupMember DisplayName="Noah Park" />
        <arc:AvatarGroupMember DisplayName="Leah Fox" Status="Offline" />
    </arc:AvatarGroup>
</UserControl>
```

### 繫結成員

將 `ItemsSource` 繫結到 `AvatarGroupMember`、`Avatar` 或一般名稱的集合。該集合優先於內嵌的 `Items`。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 Label="團隊成員" />
```

```csharp
var team = new AvaloniaList<AvatarGroupMember>
{
    new("Maya Chen", status: AvatarStatus.Online),
    new("Noah Park"),
    new("Leah Fox", status: AvatarStatus.Offline),
};
group.ItemsSource = team;

// 加入和離開會為位置加入動畫；清單會就地更新。
team.Insert(1, new AvatarGroupMember("Omar Haddad"));
team.RemoveAt(1);
```

### 溢出

`MaxVisibleCount`（預設為 4）決定在 "+N" 標籤之前顯示多少人。`OverflowCount` 會回報收合進標籤的人數。只有可見的人員會建立控制項，因此長清單仍然輕量。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 MaxVisibleCount="3" />
```

### 尺寸

`Size` 套用於每個大頭貼與溢出標籤：`Small` 為 28，`Medium`（預設）為 36，`Large` 為 48。`XLarge` 會以 `Large` 繪製。

<Demo name="SizesAndLimits" />

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Small" MaxVisibleCount="3" />
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Large" MaxVisibleCount="5" />
```

### 內嵌成員

成員可以直接內嵌撰寫，無須繫結清單。

<Demo name="InlineMembers" />

## API 參考

<!-- api: AvatarGroup -->

<!-- api: AvatarGroupMember -->

## 無障礙

- 群組由 `Label` 命名。每個大頭貼為輔助技術保留其自身的名稱與狀態。
- 溢出標籤是一個由 `Label` 命名的影像，例如 "另外 2 位團隊成員"。
- 堆疊沒有鍵盤操作，也無法取得焦點。鍵盤作業請使用該群組所摘要的清單或表格。
- 游標懸停時顯示的名稱僅為視覺效果：每位人員的名稱也包含在大頭貼的無障礙名稱中。

## 動態效果

- 有人加入或離開時，其位置會以變形彈簧伸縮，同時輕微淡入淡出並縮放，因此堆疊是滑動而非跳動。
- 溢出數量增加時向上滾動，減少時向下滾動。
- 游標停在堆疊上時，人員會從中心向外散開，游標下的人員會略為升起並顯示光環與名稱，相鄰的人員則向兩側讓開。
- 減少動態效果時，移除懸停時的散開效果，保留光環與名稱；數量以淡入淡出變化。關閉動態效果時，所有變化立即完成。

## 相關元件

- `Avatar`：單一人員。
- `UserMenu`：群組成員的大頭貼需要開啟帳號操作時使用。
- `SortableDataTable`：需要比較姓名、角色及其他欄位時使用。
