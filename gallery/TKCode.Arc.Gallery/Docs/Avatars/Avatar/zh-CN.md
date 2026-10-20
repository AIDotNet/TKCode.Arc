---
title: 头像
summary: 用于人员与账号的紧凑身份标识。
source-hash: 3e5c344918ce
---

## 何时使用

- 名字、评论或记录旁边的单个人员，由照片或首字母标识。
- 页面头部或列表中的在线状态，配合 `Status` 圆点使用。
- 照片可能缺失或加载失败的场景：由首字母代替显示。

## 何时不用

- 小空间内显示多人并带有溢出计数：用 `AvatarGroup`。
- 点击后打开账号菜单的头像：用 `UserMenu`。
- 包含角色的完整人员列表：用 `SortableDataTable`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Avatar DisplayName="Ada Lovelace" />
</UserControl>
```

### 照片与回退

`Source` 用于加载照片。加载期间显示首字母；若加载失败，则保留首字母。

```xml
<arc:Avatar DisplayName="Mia Chen"
            Size="Large"
            Source="https://i.pravatar.cc/160?img=47" />
```

### 尺寸

`Size` 决定直径：`Small` 为 28，`Medium`（默认）为 36，`Large` 为 48，`XLarge` 为 88。

```xml
<arc:Avatar DisplayName="Ada Lovelace" Size="Small" />
<arc:Avatar DisplayName="Grace Hopper" Size="Medium" />
<arc:Avatar DisplayName="Alan Turing" Size="Large" />
<arc:Avatar DisplayName="Katherine Johnson" Size="XLarge" />
```

### 状态

`Status` 会在右下角显示在线或离线圆点。`AvatarStatus.None` 则隐藏该圆点。

```xml
<arc:Avatar DisplayName="Leah Fox" Size="Large" Status="Online" />
```

```csharp
avatar.Status = avatar.Status switch
{
    AvatarStatus.Online => AvatarStatus.Offline,
    AvatarStatus.Offline => AvatarStatus.None,
    _ => AvatarStatus.Online
};
```

### 首字母

首字母取自 `DisplayName` 前两个单词的首字母，并转为大写："Ada Lovelace" 得到 "AL"。`Initials` 返回相同的值，`Avatar.ComputeInitials` 可为任意名称计算首字母。

## API 参考

<!-- api: Avatar -->

## 无障碍

- 头像是一个以 `DisplayName` 为名称的图像。设置状态后，名称中会加入状态，例如 "Maya Chen，在线"。
- 照片和首字母仅作装饰，对辅助技术隐藏，因此名称只会被朗读一次。
- 状态圆点已隐藏；状态通过名称传递给辅助技术。
- 头像不可获得焦点，也没有键盘行为。

## 动效

- 仍在加载的照片会从柔和的模糊中淡入。已缓存的照片会立即显示。
- 状态圆点会以轻快的弹簧动画从小尺寸弹出，离开时缩小消失。
- 减少动效时，状态圆点立即切换，照片无淡入效果直接显示。关闭动效时，所有变化立即完成。

## 相关组件

- `AvatarGroup`：用于带溢出计数的人员堆叠。
- `UserMenu`：头像需要打开账号操作时使用。
- `Badge`：用于非人员的状态或类别标签。
