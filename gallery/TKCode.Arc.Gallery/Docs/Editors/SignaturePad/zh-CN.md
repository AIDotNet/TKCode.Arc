---
title: 签名板
summary: 笔迹平滑，速度越快越细；支持撤销、回放，并可导出 PNG 或 SVG。
source-hash: 38f92cfe8afa
---

<Demo name="Sign" />

## 何时使用

- 合同、签收确认和同意书等需要手写签名的场景。
- 需要以图片或矢量文件保存签名的场景，可使用 `ExportPng`、`ExportBitmap` 或 `ExportSvg` 导出。
- 保存前需要复核签名的场景，支持撤销、重做与回放。

## 何时不用

- 只需输入姓名即可：用 `Input`。
- 只需确认操作、无需保留签名：用 `HoldToConfirm`。
- 对已是文件的文档签名：用 `FileDropzone` 附加该文件。

## 用法

签名板会根据笔的压力绘制笔迹，或根据鼠标或手指的速度绘制笔迹。`Signer` 会在基线下方打印姓名，`Hint` 在第一笔落下之前显示。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SignaturePad Signer="张三"
                      Hint="请在此签名" />
</UserControl>
```

### 墨迹颜色与宽度

`InkColor` 与 `InkWidth` 设置新笔画的默认值。每一笔都保留绘制时所用的颜色与宽度。方向键可在工具栏的选项之间移动。

<Demo name="InkAndNib" />

### 读取笔画

每次落笔、撤销、重做和清除后都会执行 `StrokesChanged`。`Strokes` 为只读，在第一笔落下之前 `IsEmpty` 为 true。

```csharp
pad.StrokesChanged += (_, e) =>
{
    saveButton.IsEnabled = e.Strokes.Count > 0;
};
```

### 导出

`ExportPng` 与 `ExportSvg` 返回按边界裁剪后的墨迹。`ExportPng` 可接受可选的 `scale` 参数，默认为 3。`SaveAsync` 会打开保存对话框，并以 `FileName` 作为默认文件名。当用户取消或没有任何笔画时，它返回 false。

<Demo name="UseTheSignature" />

```csharp
var png = pad.ExportPng();
var svg = pad.ExportSvg();

var saved = await pad.SaveAsync(SignatureFileKind.Png);
```

### 还原签名

`Reset` 会替换笔画，不播放清除动画，并清空撤销历史。传入之前保存的笔画即可重新显示；不传参数调用则会重新开始。

```csharp
pad.Reset(storedStrokes);
```

## API 参考

<!-- api: SignaturePad -->

## 无障碍

- 签名板是一个可聚焦的图像，名称为“签名板”。其名称包含笔画数量，键盘快捷键写在其帮助文字中。
- 签名板获得焦点时，Mod+Z 撤销，Shift+Mod+Z 或 Mod+Y 重做。Delete 与 Backspace 清除签名，清除操作可以撤销。
- 墨迹颜色与宽度是单选组。只有被选中的选项在 Tab 顺序中，方向键在各选项之间移动。
- 工具栏按钮带有名称与工具提示。落笔、撤销、重做、清除、回放与导出结果均以礼貌方式朗读。

## 动效

- 清除时，一个刮板会横扫签名板，并在约半秒内将墨迹刮去。
- 回放会按原有节奏重绘每一笔，笔尖处带有一个圆点，整个回放约需 3 秒。
- 提示文字淡入并上浮，颜色与宽度的高亮会滑动到所选选项。
- 减弱动效时，取消刮除与模糊效果，改用短暂的淡入淡出。

## 相关组件

- `Input`：与手写签名并列或代替手写签名时，用于输入姓名。
- `HoldToConfirm`：不需要签名的确认操作。
- `FileDropzone`：附加已签署文档等文件。
