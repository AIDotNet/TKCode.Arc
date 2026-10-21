---
title: 电话输入框
summary: 带国家/地区选择器、输入时即时格式化并输出 E.164 格式的电话字段。
source-hash: c869790b76d3
---

<Demo name="Default" />

## 何时使用

- 注册、结账与联系表单中需要收集电话号码的场景。
- 需要接收短信或来电、且国家/地区很重要的号码。
- 需要以 E.164 格式（`+14155550132`）保存号码的表单。

## 何时不用

- 不会被拨打的自由文本：用 `Input`。
- 验证码：请在你自己的界面中使用一次性验证码输入。它不属于本库。
- 运营商或号码类型校验：请使用完整的号码元数据服务。`PhoneInput` 仅检查长度与格式。
- 仅从很长的固定值列表中选择：用 `Select`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PhoneInput Label="电话号码"
                    Country="US"
                    Description="我们会向此号码发送登录验证码短信。" />
</UserControl>
```

### 预设值

`Value` 接受 E.164 字符串。国家/地区会根据号码自动选定，字段显示格式化后的本地号码。

<Demo name="PresetValue" />

### 限定国家/地区

`Countries` 限制选择器中的选项。`PreferredCountries` 列出在完整列表上方"推荐"分组中显示的国家/地区。列表为空时，显示按名称排序的单一列表。

<Demo name="RestrictedCountries" />

<Demo name="NoPreferredCountries" />

### 校验与错误

字段失去焦点后执行校验。`IsValidationEnabled="False"` 可将其关闭。`ErrorMessage` 会替换内置的提示信息。

<Demo name="ValidationOff" />

<Demo name="DescriptionAndError" />

### 绑定与事件

`Value` 与 `Country` 为双向绑定。`ValueChanged` 会在每次编辑时报告 E.164 值、格式化后的号码、国家/地区以及状态。

<Demo name="ValueChanged" />

```xml
<arc:PhoneInput Label="电话号码"
                Value="{Binding Phone, Mode=TwoWay}"
                Country="{Binding Country, Mode=TwoWay}" />
```

```csharp
phone.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.NewValue} ({e.Status}, valid: {e.IsValid})");
};

phone.OpenPicker();
```

### 禁用与隐藏标签

`IsEnabled="False"` 会让字段变淡。`IsLabelVisible="False"` 会隐藏标签，但标签仍为辅助技术提供号码的名称。

<Demo name="States" />

### 键盘

在国家/地区按钮上，ArrowDown 与 ArrowUp 会打开选择器，输入字母则打开搜索。在分隔符上按 Backspace 会删除其旁边的数字。

<Demo name="Keyboard" />

## API 参考

<!-- api: PhoneInput -->

## 无障碍

- `Label` 为字段命名。`IsLabelVisible="False"` 时标签隐藏，但仍作为名称。
- 国家/地区按钮命名为 "国家/地区，<name> +<code>"。搜索框是组合框，列表包含"推荐"与"全部国家/地区"两个分组。
- ArrowDown 与 ArrowUp 在国家/地区按钮上打开选择器。在搜索框内，方向键、PageUp 与 PageDown 用于移动，Home 与 End 跳到第一项与最后一项，Enter 选中，Escape 将焦点返回按钮。
- 通过粘贴或选择器更改国家/地区时，会以礼貌方式播报。
- 号码错误会在失去焦点后以辅助文字显示。有效号码会播报为 "有效的 <country> 号码"。

## 动效

- 字段的表面从国家/地区按钮展开为国家/地区列表，其圆角与宽度同步变化。
- 旗帜与拨号代码滚动切换到新的国家/地区。高亮在各行之间滑动。
- 有效标记以轻微的弹出效果出现。
- 减弱动效时，表面直接跳转到目标尺寸，内容以短暂淡入切换。关闭动效时立即切换。

## 相关组件

- `Input`：非电话号码的文本。
- `Select`：固定值列表；`Field` 或 `FieldFrame`：用于构建类似的带标签字段。
- `MoneyInput` 与 `NumberField`：具有各自格式规则的数值。
