---
title: 簽名板
summary: 筆跡平順，速度越快越細；支援復原、重播，並可匯出 PNG 或 SVG。
source-hash: 339c265ce3b3
---

## 何時使用

- 合約、簽收確認與同意書等需要手寫簽名的情境。
- 需要以圖片或向量檔案保存簽名的情境，可使用 `ExportPng`、`ExportBitmap` 或 `ExportSvg` 匯出。
- 儲存前需要複核簽名的情境，支援復原、重做與重播。

## 何時不用

- 只需輸入姓名即可：用 `Input`。
- 只需確認操作、無須保留簽名：用 `HoldToConfirm`。
- 對已是檔案的文件簽名：用 `FileDropzone` 附加該檔案。

## 用法

簽名板會依據筆的壓力繪製筆跡，或依據滑鼠或手指的速度繪製筆跡。`Signer` 會在基線下方列印姓名，`Hint` 在第一筆落下之前顯示。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SignaturePad Signer="王小明"
                      Hint="請在此簽名" />
</UserControl>
```

### 墨水顏色與寬度

`InkColor` 與 `InkWidth` 設定新筆畫的預設值。每一筆都保留繪製時所用的顏色與寬度。方向鍵可在工具列的選項之間移動。

```xml
<arc:SignaturePad InkColor="Blue"
                  InkWidth="Bold"
                  Hint="請用藍色簽名" />
```

### 讀取筆畫

每次落筆、復原、重做與清除後都會執行 `StrokesChanged`。`Strokes` 為唯讀，在第一筆落下之前 `IsEmpty` 為 true。

```csharp
pad.StrokesChanged += (_, e) =>
{
    saveButton.IsEnabled = e.Strokes.Count > 0;
};
```

### 匯出

`ExportPng` 與 `ExportSvg` 會傳回依邊界裁切後的墨跡。`ExportPng` 可接受選用的 `scale` 參數，預設為 3。`SaveAsync` 會開啟儲存對話方塊，並以 `FileName` 作為預設檔名。當使用者取消或沒有任何筆畫時，它傳回 false。

```csharp
var png = pad.ExportPng();
var svg = pad.ExportSvg();

var saved = await pad.SaveAsync(SignatureFileKind.Png);
```

### 還原簽名

`Reset` 會取代筆畫，不播放清除動畫，並清空復原歷程。傳入先前保存的筆畫即可重新顯示；不傳參數呼叫則會重新開始。

```csharp
pad.Reset(storedStrokes);
```

## API 參考

<!-- api: SignaturePad -->

## 無障礙

- 簽名板是一個可聚焦的影像，名稱為「簽名板」。其名稱包含筆畫數量，鍵盤快速鍵寫在其說明文字中。
- 簽名板取得焦點時，Mod+Z 復原，Shift+Mod+Z 或 Mod+Y 重做。Delete 與 Backspace 清除簽名，清除操作可以復原。
- 墨水顏色與寬度是單選群組。只有被選取的選項在 Tab 順序中，方向鍵在各選項之間移動。
- 工具列按鈕帶有名稱與工具提示。落筆、復原、重做、清除、重播與匯出結果均以禮貌方式朗讀。

## 動態效果

- 清除時，一片刮板會橫掃簽名板，並在約半秒內將墨跡刮去。
- 重播會依原有節奏重繪每一筆，筆尖處帶有一個圓點，整個重播約需 3 秒。
- 提示文字淡入並上浮，顏色與寬度的醒目提示會滑動到所選選項。
- 減少動態效果時，取消刮除與模糊效果，改用短暫的淡入淡出。

## 相關元件

- `Input`：與手寫簽名並列或代替手寫簽名時，用於輸入姓名。
- `HoldToConfirm`：不需要簽名的確認操作。
- `FileDropzone`：附加已簽署文件等檔案。
