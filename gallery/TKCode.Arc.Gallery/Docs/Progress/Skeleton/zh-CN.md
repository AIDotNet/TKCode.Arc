---
title: 骨架屏
summary: 内容仍在加载时预留空间。
source-hash: d9768e899d18
---

<Demo name="Profile" />

## 何时使用

- 资料、评论或卡片的形状已知，但数据仍在加载。
- 内容到达时应原地替换，布局不跳动。

## 何时不用

- 有可度量进度的工作：用 `Progress`。
- 已加载完成但没有内容可显示：用 `EmptyState`。
- AI 思考状态或状态行：用 `TextShimmer`。

## 用法

`Content` 为 null 时，骨架屏显示占位。设置 `Lines` 指定文字行数，设置 `IsAvatarVisible` 可在文字前显示圆形头像占位。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Skeleton IsAvatarVisible="True" Lines="3" />
</UserControl>
```

### 评论占位

`Lines` 的取值范围为 1 到 6。第一行更短、更高，最后一行的宽度为整体的 72%。

<Demo name="Lines" />

### 已加载的内容

将 `Content` 设为真实视图，并把 `IsLoading` 绑定到加载状态。`IsLoading` 变为 false 时，占位淡出，内容上移就位，高度平滑变化。

<Demo name="IntoContent" />

```xml
<arc:Skeleton IsAvatarVisible="True"
              Lines="2"
              IsLoading="{Binding IsLoadingProfile}">
    <StackPanel Spacing="8">
        <TextBlock Text="{Binding UserName}" />
        <TextBlock Text="{Binding Bio}" TextWrapping="Wrap" />
    </StackPanel>
</arc:Skeleton>
```

### 无障碍名称

加载期间，`Label` 为占位命名。默认值为 "正在加载内容"。

```xml
<arc:Skeleton Label="正在加载评论" Lines="5" />
```

```csharp
skeleton.IsLoading = false; // 显示 Content；占位隐藏
```

## API 参考

<!-- api: Skeleton -->

## 无障碍

- 占位显示时，`Label` 是无障碍名称。默认值为 "正在加载内容"。
- 占位是礼貌模式的实时区域，状态朗读为 "加载中"。
- 占位的形状不会被朗读。内容加载后，请为真实内容提供各自的标签。
- 控件不可获得焦点，也没有键盘交互。

## 动效

- 文字行与头像从上到下以平缓的波浪脉动，波浪持续约 20 秒后停止。
- 加载结束时，占位淡出，内容上移 4 px 就位，高度以弹簧过渡到内容高度。
- 减弱动效时，脉动停止，占位以短暂淡入切换为内容，高度不做动画。

## 相关组件

- `Progress`：工作量已知并可报告进度时使用。
- `EmptyState`：加载完成但没有内容可显示时使用。
- `TextShimmer`：用于行内状态或 AI 思考状态。
