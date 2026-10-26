---
title: 移动端搜索栏
summary: 带搜索图标的胶囊形文本框，有清除按钮和会触发 Submitted 的搜索键。
source-hash: 9e28f90235b4
---

<Demo name="Basic" />

## 何时使用

`Searchbar` 是用户输入查询词的文本框：在列表顶部，或在搜索页面上。

- **筛选或搜索列表**：放在页面顶部、导航栏下面。
- **搜索页面**：它是页面上的第一个元素，页面打开时获得焦点。

实时示例在手机外框里运行，那是桌面上的模拟；真机上软键盘的搜索键这里没有验证。

## 何时不用

- **普通的文字输入**：用 `TextField`。
- **带建议的桌面搜索框**：用桌面的搜索控件。

## 用法

`Searchbar` 是一个 `TextField`，所以 `Text`、`PlaceholderText` 和绑定都和那里一样。处理 `Submitted` 来执行搜索：用户按 Enter 或软键盘的搜索键时触发。

```xml
<arcm:Searchbar xmlns="https://github.com/avaloniaui"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                PlaceholderText="搜索消息"
                Text="{Binding Query}" />
```

```csharp
searchbar.Submitted += (_, _) => Search(searchbar.Text);
```

搜索图标是文本框的 `Icon`，默认已设置；清除按钮在文本框有焦点且有文字时显示。要在输入时就筛选，请绑定 `Text` 并响应它的变化。

### Token

`Searchbar` 与 `TextField` 共用 `TextFieldToken`：同样的 44 px 高度、内边距和图标大小。只有圆角不同：用 ArcDesign token 的胶囊圆角。

## API 参考

<!-- api: MobileSearchbar -->

## 无障碍

给文本框一个说明搜索对象的占位提示，例如“搜索消息”。文本框至少 44 px 高。

## 动效

文本框获得或失去焦点时，边框按快速曲线变色；减弱动效时立即变色。

## 相关组件

- `MobileTextField`：它所基于的文本框。
- `MobileNavbar`：搜索栏通常放在它下面。
- `MobileListGroup`：常用的结果列表。
