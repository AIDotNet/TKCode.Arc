---
title: 新手问卷
summary: 首次使用时的简短问卷，一次问一个问题：单选、多选、文字回答、进度、跳过和结束时的感谢页。
source-hash: ffa8778ef0ff
---

<Demo name="Preview" />

## 何时使用

`OnboardingSurvey` 在新用户注册后立即问几个问题，一次一个，让应用据此选择模板、默认设置或起始页面。计数和进度条显示进度，“Skip survey”可随时结束问卷。问题可以用卡片单选、用复选框多选（可设上限），或在文本框中写几句话。可选的问题提供“Skip question”。用户返回时答案会保留，感谢页会在 `Completed` 中报告所有答案。

- **首次设置**：按职位、团队人数或目标调整工作区。
- **关键时刻后的简短调研**，例如试用结束时。
- **收集偏好**：做成一张长表单会显得太重。

## 何时不用

- **收集必须有效的账号信息**：使用会检查字段的 `MultiStepForm` 或 `SignupForm`。
- **带分支的长问卷**：使用专门的表单；这张卡片只适合几个问题。
- **介绍功能**：使用 `OnboardingIllustration`，它是展示而不是提问。

## 用法

```xml
<arc:OnboardingSurvey CanSkipSurvey="False">
    <arc:OnboardingSurveyQuestion Id="role" Title="What best describes your work?">
        <arc:OnboardingSurveyOption Label="Engineering" />
        <arc:OnboardingSurveyOption Label="Design" />
    </arc:OnboardingSurveyQuestion>
    <arc:OnboardingSurveyQuestion Id="note" Title="Anything we should know?" Kind="Text" IsOptional="True" />
</arc:OnboardingSurvey>
```

每个答案包含问题的 `Id`、所选选项的值（选项的 `Value`，或其文字）以及文字问题的内容。没有问题时，问卷会问四个虚构的示例问题。

<Demo name="OwnQuestions" />

## API 参考

<!-- api: OnboardingSurvey -->

## 无障碍

- 焦点会移到每个新问题的回答控件上，并读出计数和问题。
- Enter 继续，Alt+左方向键返回，数字键 1 到 9 选择选项。在文本框中 Enter 换行，Ctrl+Enter（Cmd+Enter）继续。
- 多选题达到上限时，其他选项会被禁用，提示文字说明可以选几个。

## 动效

- 问题按前进方向滑入，卡片高度以弹簧跟随；完成问题时进度条随之填满，感谢页上的对勾会自己画出来。
- 减少动效时问题交叉淡化，对勾立即显示。关闭动效时一切立即改变。

## 响应式

- 卡片最宽 540 px。小于 440 px 时内边距收紧，按钮竖排并占满整行。
- 选项卡片和复选框文字会换行，因此在手机上也易于阅读。

## 相关组件

- `RadioCards`、`Checkbox`、`Textarea` 和 `Progress`：问卷由它们构成。
- `MultiStepForm` 用于分步表单，`OnboardingIllustration` 用于图示形式的首次导览。
