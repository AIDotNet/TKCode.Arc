---
title: AI setup wizard
summary: 为工作区开启 AI 的五步设置：服务商、模型和密钥，数据访问，隐私规则，连接测试，以及上线前的总结。
source-hash: e0fd9b4b2f2d
---

<Demo name="Preview" />

## 何时使用

`AiSetupWizard` 带领工作区管理员开启 AI 功能。共五步，每步一页：选择服务商和模型并输入密钥；选择助手可以读取哪些工作区数据；决定隐私规则（数据不用于训练、隐去个人信息、聊天记录保留多久）；用这些设置运行一次简短的测试；开启前检查全部选择。页面旁的步骤条显示管理员所在的位置，并可以回到任何已完成的步骤。每一步都会检查自己的字段，测试通过后才能进入总结。

- **管理员设置。** 工作区第一次启用助手，或改用另一家服务商时。
- **自建服务或自带密钥。** 不需要密钥的服务商（你自己的服务器）会跳过密钥输入框。
- **合规检查。** 总结在一处列出所有选择，然后才做出任何改动。

## 何时不用

- **个人偏好。** 不需要测试时，一个开关或 `Settings page` 区块就够了。
- **只选择模型。** 使用 `Model picker` 区块。
- **很长的表单。** 字段多且彼此无关时，使用分节的设置页，而不是分步。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。不设置任何数据时，它提供虚构的服务商和数据来源以及模拟的测试（`AiSetupWizardSamples`；密钥填 "fail" 可以看到失败的情况）。

```xml
<arc:AiSetupWizard Providers="{Binding Providers}" DataSources="{Binding DataSources}" />
```

```csharp
wizard.TestRunner = async (configuration, progress, cancellation) =>
{
    await client.PingAsync(configuration.ProviderId, cancellation);   progress.Report(1);
    await client.CheckKeyAsync(configuration.ApiKey, cancellation);   progress.Report(2);
    var reply = await client.AskAsync(configuration.Model, "Say hi", cancellation); progress.Report(3);
    await index.ReadOneAsync(configuration.DataSourceIds, cancellation); progress.Report(4);
    return new AiSetupTestResult(true, $"The model answered in {reply.Elapsed.TotalMilliseconds:0} ms.");
};
wizard.Completed += (_, e) => workspace.EnableAi(e.Configuration);
```

### 各个步骤

1. **服务商和模型**：`Providers` 显示为单选卡片；模型菜单列出所选服务商的 `Models`；`RequiresKey` 时显示密钥输入框，下方是 `KeyHint`。继续之前需要选好服务商，需要时还要填写密钥。
2. **数据访问**：`DataSources` 中每一项一个复选框；`IsEnabledByDefault` 决定初始状态。
3. **隐私**：两个开关（训练、隐去信息）以及来自 `HistoryChoices` 的保留时长（天数；0 表示不保留）。
4. **测试**：四项检查，从圆圈变为加载圈，再变为对勾或叉号；显示结果消息，并有再次运行的按钮。之前步骤中的任何改动都会要求重新测试。
5. **完成**：所有选择的总结，以及触发 `Completed` 并带上 `AiSetupConfiguration` 的按钮。

`CurrentStep`（双向）只会移到已经到过的步骤；`Next()`、`Back()`、`GoTo(step)`、`RunTestAsync()` 和 `Reset()` 从代码完成按钮所做的事。`Configuration`、`TestResult`、`IsTesting` 和 `IsCompleted` 报告当前状态。

### 窄布局

<Demo name="Narrow" />

### 文字

`Labels`（`AiSetupWizardLabels`）包含步骤名称和说明、按钮、字段标签、错误提示、开关文字、测试检查项、总结的各行名称以及步骤播报。

## API 参考

<!-- api: AiSetupWizard -->

<!-- api: AiSetupProvider -->

<!-- api: AiSetupDataSource -->

<!-- api: AiSetupConfiguration -->

<!-- api: AiSetupTestResult -->

<!-- api: AiSetupStep -->

<!-- api: AiSetupWizardLabels -->

## 无障碍

- 向导是一个表单地标；标题是二级标题，每页的标题是三级标题。
- 步骤条读出每一步的名称和状态；已完成的步骤可以用键盘选择。
- 切换步骤时播报 "Step 2 of 5: Data access"。缺少服务商或密钥时会播报，并显示在字段下方，焦点移到该字段。
- 每项测试检查报告自己的状态；结果消息会礼貌地朗读。
- 开关以其文字命名，并以下方的说明作为描述。

## 动效

- 页面沿前进方向滑动 12 px 并交叉淡化；步骤条依次填充连线并移动光圈。
- 检查行立即切换图标；测试运行期间测试按钮显示加载圈。
- 减弱动效时页面只交叉淡化不滑动；关闭动效时立即切换。

## 响应式

- 向导最宽 860。宽布局时，步骤条是页面旁宽 220 的一列。宽度不超过 640 时（`:compact`），步骤条变为横向并移到页面上方，内边距从 24 收紧到 16。
- 页面保持至少 360 的高度，步骤之间切换时页脚不会跳动。
- 单选卡片和复选框的文字会换行；没有任何功能依赖悬停。

## 相关组件

- `Stepper`、`RadioCards`、`Select`、`PasswordField`、`Checkbox`、`Switch` 和 `SegmentedControl`，组成它的控件。
- `Multi-step form`，通用的分步表单。
- `Model picker`，用于在产品中选择模型。
