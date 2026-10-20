---
title: 头像组
summary: 在较小的空间内展示团队或一组贡献者。
source-hash: 2bb01028d491
---

## 何时使用

- 标明谁拥有或正在编辑某项内容，用于页面头部、卡片或表格单元格。
- 人数较少的团队列表，前几位较为重要，其余可折叠为计数。
- 页面打开期间会变化的分组：人员加入或离开时，头像堆叠随之移动。

## 何时不用

- 只有一个人：用 `Avatar`。
- 需要读取每个人的姓名和角色的列表：用 `SortableDataTable` 或普通列表。
- 某个人的账号菜单：用 `UserMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AvatarGroup Label="编辑者">
        <arc:AvatarGroupMember DisplayName="Maya Chen" Status="Online" />
        <arc:AvatarGroupMember DisplayName="Noah Park" />
        <arc:AvatarGroupMember DisplayName="Leah Fox" Status="Offline" />
    </arc:AvatarGroup>
</UserControl>
```

### 绑定成员

将 `ItemsSource` 绑定到 `AvatarGroupMember`、`Avatar` 或普通名称的集合。该集合优先于内联的 `Items`。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 Label="团队成员" />
```

```csharp
var team = new AvaloniaList<AvatarGroupMember>
{
    new("Maya Chen", status: AvatarStatus.Online),
    new("Noah Park"),
    new("Leah Fox", status: AvatarStatus.Offline),
};
group.ItemsSource = team;

// 加入和离开会为位置添加动画；列表会就地更新。
team.Insert(1, new AvatarGroupMember("Omar Haddad"));
team.RemoveAt(1);
```

### 溢出

`MaxVisibleCount`（默认为 4）决定在 "+N" 标签之前显示多少人。`OverflowCount` 会报告折叠进标签的人数。只有可见的人员才会创建控件，因此长列表依然开销较小。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 MaxVisibleCount="3" />
```

### 尺寸

`Size` 作用于每个头像和溢出标签：`Small` 为 28，`Medium`（默认）为 36，`Large` 为 48。`XLarge` 按 `Large` 绘制。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Small" MaxVisibleCount="3" />
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Large" MaxVisibleCount="5" />
```

## API 参考

<!-- api: AvatarGroup -->

<!-- api: AvatarGroupMember -->

## 无障碍

- 分组以 `Label` 命名。每个头像为辅助技术保留其自身的名称和状态。
- 溢出标签是一个以 `Label` 命名的图像，例如 "另外 2 位团队成员"。
- 堆叠没有键盘行为，也无法获得焦点。键盘操作请使用该分组所概括的列表或表格。
- 悬停时显示的名称仅为视觉效果：每个人的名称也包含在头像的无障碍名称中。

## 动效

- 有人加入或离开时，其位置会以变形弹簧伸缩，同时轻微淡入淡出和缩放，因此堆叠是滑动而非跳动。
- 溢出数量增加时向上滚动，减少时向下滚动。
- 指针悬停在堆叠上时，人员会从中心向外散开，指针下的人员会略微升起并显示光环和名称，相邻的人员则向两侧让开。
- 减弱动效时，移除悬停时的散开效果，保留光环和名称；数量以淡入淡出变化。关闭动效时，所有变化立即完成。

## 相关组件

- `Avatar`：单个人员。
- `UserMenu`：分组成员的头像需要打开账号操作时使用。
- `SortableDataTable`：需要比较姓名、角色及其他列时使用。
