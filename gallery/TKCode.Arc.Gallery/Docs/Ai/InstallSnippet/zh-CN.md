---
title: 安装片段
summary: 一张快速上手卡片：框架标签、三个编号步骤和高亮代码片段带开发者从安装走到第一个事件，"生成密钥"会把新密钥就地擦入占位符。
source-hash: 8834ae45e0b1
---

<Demo name="Preview" />

## 何时使用

`InstallSnippet` 是一张"30 秒上手"卡片。框架标签选择读者的技术栈，三个编号步骤（安装、添加密钥、上线）各显示一段高亮代码，并带有复制按钮。当框架通过包安装时，步骤 1 提供 npm、pnpm、yarn 和 bun 可选。"生成密钥"会创建一个密钥，并把它就地擦入占位符，所有用到它的片段都会更新。切换框架时，片段在为最长内容预留的空间里交叉淡化，因此卡片尺寸始终不变。

- **面向多个框架的快速上手**，无需为每个框架各写一页文档。
- **创建 API 密钥是入门一部分的引导流程**：注册后的第一屏，或等待第一个事件的空仪表板。
- **开发者工具的营销页面**，展示接入只需多少配置。

## 何时不用

- **只有一段代码、没有步骤**：使用 `CodeBlock`。
- **对线上 API 试发请求**：API 调试台更合适。
- **分支众多的长篇安装指南**：编写文档页面并链接过去。
- **完整显示已有的密钥**：请在 API 密钥页面中遮蔽显示（参见 `ProviderKeys`）。

## 用法

没有框架也没有模拟（见下文）时，卡片显示三个步骤和空的代码片段（上方的 Empty 选项卡）。没有设置密钥生成器时，"生成密钥"会编造一个演示密钥并如实说明：不会发送任何内容。传入你自己的框架和密钥接口：

```csharp
snippet.KeyGenerator = async token =>
{
    var response = await http.PostAsync("/api/keys", null, token);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<NewKey>(token))!.Key;
};
snippet.Copied += (_, e) => analytics.Track("onboarding_copy", e.Step.ToString());
```

返回密钥后，它会擦入步骤 2。抛出异常时，步骤 2 会以警示图标显示异常的消息（或"Could not create a key."），按钮变为"重试"（Try again）。当卡片离开窗口或读者再次请求密钥时，令牌会被取消。

### 结构

1. **标题区**：标题、其下的一行说明，以及右上角柔和的版本标签（`Version`；为空则隐藏）。
2. **框架标签**：每个框架一个标记和名称；凸起的高亮会滑到所选标签下。
3. **步骤**：轨道上的编号标记、标题、一行说明要做什么，以及带文件名、复制按钮和代码片段的代码块。片段被复制过的步骤会把编号换成对勾。
4. **步骤 1** 在框架通过包安装时，于标题右侧显示包管理器标签。
5. **步骤 2** 在标题右侧有"生成密钥"，代码下方有一行状态。

### 你的框架

<Demo name="YourFrameworks" />

每个 `InstallSnippetFramework` 都有 `Id`、`Label` 和三段片段。通过 npm、pnpm、yarn 或 bun 安装时设置 `Packages`（以空格分隔的包名），命令或脚本标签则设置 `Install`。在密钥所在位置写 `{{key}}`；在密钥出现之前，那里会显示带底色的占位符（`KeyPlaceholder`），并按原样复制：

```xml
<arc:InstallSnippet KeyPlaceholder="YOUR_CONTOSO_KEY">
    <arc:InstallSnippetFramework Id="ruby" Label="Ruby">
        <arc:InstallSnippetFramework.Install>
            <arc:InstallSnippetFile FileName="Terminal" Code="gem install contoso-labs" />
        </arc:InstallSnippetFramework.Install>
        <arc:InstallSnippetFramework.Key>
            <arc:InstallSnippetFile FileName=".env" Code="CONTOSO_KEY={{key}}" />
        </arc:InstallSnippetFramework.Key>
    </arc:InstallSnippetFramework>
</arc:InstallSnippet>
```

id 为 `node`、`python`、`go` 和 `html` 时会有各自的标记；其他 id 显示纯文字标签，除非你把 `Mark` 设为 24 × 24 的描边图形。高亮所用的语言跟随文件名（`Terminal` 和 `.env` 按 shell 处理），也可以设置 `Language`。每段片段保持在十行以内，卡片才便于快速浏览。

### 你的密钥服务

<Demo name="YourKeyService" />

`SelectedFramework` 和 `PackageManager` 是双向绑定的：绑定它们即可在不同页面间记住读者的选择。`FrameworkChanged`、`PackageManagerChanged`、`KeyGenerated` 和 `Copied` 报告读者的操作，复制事件会带上放入剪贴板的确切文本。对已登录且已有密钥的读者传入 `ApiKey`：每段片段都会显示它，"生成密钥"随之隐藏。

### 模拟的 SDK

用于演示时，让 `Frameworks` 保持为空，并给卡片一个 `InstallSnippetSimulation`：它的框架标签，以及"生成密钥"编造的密钥所用的前缀 `KeyPrefix`。你自己的 `Frameworks` 会让它让位，把 `Simulation` 设为 null 会清空卡片。这里的预览就用这种方式展示本库自己的安装步骤：通过 .NET CLI、PackageReference 或中央包管理添加 `TKCode.Arc.Special`，把模型服务商的密钥保存在用户机密、开发环境设置或环境变量中（供你的应用自己调用模型；Arc 的组件永远接触不到它），以及在 `Program.cs` 中注册。这些密钥都是编造的演示密钥。

```csharp
snippet.Simulation = new InstallSnippetSimulation
{
    Frameworks =
    [
        new InstallSnippetFramework("dotnet", ".NET CLI")
        {
            Install = new InstallSnippetFile("Terminal", "dotnet add package TKCode.Arc.Special --version 0.3.2-preview.1"),
            Key = new InstallSnippetFile("Terminal", "dotnet user-secrets set \"Chat:ApiKey\" \"{{key}}\""),
        },
    ],
    KeyPrefix = "demo_",
};
```

### 窄布局

<Demo name="Narrow" />

卡片填满容器，最宽 640px。宽度低于 460px 时内边距收紧，包管理器标签只显示标记（名称保留给屏幕阅读器，并显示在工具提示中）。低于 380px 时框架标签去掉标记，版本标签隐藏。过长的行在各自的代码块内横向滚动，而不是整个页面滚动。

### 自定义文案

`Labels` 包含卡片显示或朗读的每一个词，`Title`、`Description` 和 `Version` 除外：

```csharp
snippet.Labels = new InstallSnippetLabels
{
    InstallTitle = "Installer",
    KeyTitle = "Ajouter votre clé",
    CodeTitle = "Livrer",
    GenerateKey = "Générer une clé",
};
```

### 内容准则

- 步骤说明用几个词讲清片段该怎么用（"把密钥放进 .env，切勿提交。"）。
- 文件名使用读者真实的文件（`.env`、`app.py`）；命令显示为 Terminal。
- 每段片段保持简短、可直接运行，`{{key}}` 准确放在密钥所在位置。

### 最佳实践

- 把 `KeyGenerator` 接到你的密钥接口，并为已有密钥的读者传入 `ApiKey`。
- 跟踪 `Copied`，了解引导流程卡在哪一步。
- 不要把模拟密钥带进生产：没有 `KeyGenerator` 时，卡片会有意编造密钥。

## API 参考

<!-- api: InstallSnippet -->

<!-- api: InstallSnippetFramework -->

<!-- api: InstallSnippetFile -->

<!-- api: InstallSnippetSimulation -->

<!-- api: InstallSnippetLabels -->

## 无障碍

- 框架标签和包管理器标签都是采用游走焦点的标签列表：只有所选标签是 Tab 停靠点，每个标签都会报告自己是否被选中。步骤是框架标签的面板。
- 步骤是一个含三项的列表，每个标题都是标题元素。完成的步骤会朗读"，done"，并以对勾代替编号，因此进度从不只靠颜色表达。
- 一个礼貌型实时区域会播报框架切换、新密钥和错误，并在密钥为模拟时加以说明。复制按钮会自行播报"已复制"。
- 出错时显示警示图标和文字，按钮变为"重试"。
- 标记仅作装饰；每个标签都有名称，窄布局隐藏包管理器标签的名称时仍会保留它。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 在框架标签、包管理器标签、复制按钮和"生成密钥"之间移动 |
| 左箭头、右箭头 | 在标签列表中移到并选中上一个或下一个标签（循环） |
| Home、End | 在标签列表中选中第一个或最后一个标签 |
| Enter、Space | 复制片段或生成密钥 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 标签高亮 | 以无回弹的弹簧（0.24s）滑到测量出的标签 | 立即移动 | 立即 |
| 片段、文件名、步骤说明 | 旧内容在 100ms 内淡出，新内容在 160ms 内淡入并上升 2px，二者共用同一个单元格 | 120ms 交叉淡化，不上升 | 立即 |
| 新密钥 | 槽位从占位符的宽度缓动到密钥的宽度（按字符精确计算），同时密钥在 340ms 内从左侧擦入，随后底色在 480ms 内淡去 | 密钥立即出现；底色仍会淡去 | 立即 |
| 完成的步骤 | 标记填充强调色的同时，编号淡出、对勾淡入 | 对勾在 120ms 内淡入 | 立即 |

每个代码块都预留其步骤中最长片段的高度，每行步骤说明都预留最长说明的高度，因此切换框架不会移动任何东西。只有不透明度、裁剪和一个测量出的宽度参与动画，空闲时不运行任何东西。

## 相关组件

- `CodeBlock`：带复制按钮的单段代码。
- `CopyButton`：复制单个值。
- `ProviderKeys`：显示和轮换用户已有的密钥。
- `Tabs`：在同一处切换相关内容。
