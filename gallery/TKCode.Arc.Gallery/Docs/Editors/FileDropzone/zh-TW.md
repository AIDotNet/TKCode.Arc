---
title: 檔案拖放區
summary: 面向拖放一個或多個檔案的大面積投放區域。
source-hash: a6b61cd5ce4d
---

## 何時使用

- 附件與上傳情境，使用者需要看到每個檔案、其上傳進度以及任何失敗資訊。
- 可能失敗、需要重試的上傳，或在上傳完成前需要移除的列。
- 需要在投放區旁邊顯示檔案類型、檔案大小與檔案數量限制的情境。

## 何時不用

- 只需要系統檔案對話方塊而不需要清單：在自己的按鈕中呼叫 `BrowseAsync`，或使用一般的 `Button`。
- 頁面為空，拖放是唯一操作：使用 `EmptyState`，以取得更醒目的行動呼籲。
- 從一組已知值中選擇一個：用 `Select` 或 `RadioGroup`。

## 用法

整個投放區都是點擊區域，因此在其任何位置點擊都會開啟系統檔案對話方塊。`Accept` 限制檔案類型，`MaxFiles` 與 `MaxFileSize` 分別限制檔案數量與單一檔案大小。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FileDropzone Label="新增檔案"
                      Description="將檔案拖放到此處，或從裝置中選擇"
                      Accept=".pdf,image/*"
                      MaxFiles="6" />
</UserControl>
```

### 最大 2 MB 的圖片

`Accept` 接受副檔名與 MIME 模式。被拒絕的類型或過多的檔案會使投放區晃動並顯示錯誤。超過 `MaxFileSize` 的檔案會失敗，且不會上傳。`MaxFileSize` 的單位為位元組。

```xml
<arc:FileDropzone Label="新增圖片"
                  Description="PNG、JPG 或 WebP，每個最大 2 MB"
                  Accept="image/*"
                  MaxFileSize="2097152"
                  MaxFiles="3" />
```

### 單一檔案

將 `AllowMultiple` 設為 `False` 後，新檔案會取代目前的檔案。

```xml
<arc:FileDropzone Label="新增合約"
                  Accept=".pdf"
                  AllowMultiple="False" />
```

### 上傳處理常式

`UploadHandler` 依序上傳每個檔案。透過 `IProgress<double>` 回報進度，並在 `CancellationToken` 被取消時停止。移除正在上傳的列會取消其上傳。

```csharp
zone.UploadHandler = async (item, progress, token) =>
{
    await UploadToServerAsync(item.File!, progress, token);
};

zone.FilesChanged += (_, e) =>
{
    // e.Files 保存本次新增的檔案。
};
```

### 由應用程式驅動的列

未設定上傳處理常式時，由應用程式驅動每一列。呼叫 `ReportProgress`、`CompleteUpload` 與 `FailUpload`，或直接設定項目的 `Status` 與 `Progress`。`Retry` 與 `Remove` 作用於某一列。

```csharp
zone.ReportProgress(item, 40);
zone.CompleteUpload(item);
zone.FailUpload(item, "伺服器已中斷連線", retryable: true);
```

### 投放區內的清單

`ListPlacement="Inside"` 會將清單顯示在投放區的邊緣之內。設定 `CompactAt` 後，當列出的列數達到該值時，提示會收合為一條細列。

```xml
<arc:FileDropzone ListPlacement="Inside"
                  CompactAt="2" />
```

## API 參考

<!-- api: FileDropzone -->

<!-- api: FileDropzoneItem -->

## 無障礙

- 投放區是一個按鈕。它的 Tab 停駐點就是投放區本身，按 Enter 或空白鍵即可開啟檔案對話方塊。
- 在清單中，上下方向鍵會將焦點移到相鄰列的同一操作上。Delete 與 Backspace 移除目前聚焦的列，焦點移到相鄰列。
- 當指標位於投放區上方或焦點位於其中時，Mod+V 可從剪貼簿貼上檔案，例如截圖。
- 上傳中的列會公開一個 0 到 100 的進度條。移除與重試按鈕的名稱中包含檔案名稱。
- 新增、完成與失敗等狀態以禮貌方式朗讀。被拒絕的檔案則以斷言方式朗讀。

## 動態效果

- 檔案拖到投放區上方時，紙張圖示會抬起，虛線邊框變為實線；拖曳的檔案經過時會有緩慢的脈衝效果。
- 新列從上方滑入，並從輕微模糊中放大出現，多個新列依序進入。被移除的列會在約 0.3 秒內收合。
- 進度條跟隨其目標變化，已完成的列會繪製一個勾號。失敗的列會晃動一次。
- 減少動態效果時，高度與抬起的變化改為短暫淡入淡出，進度變化立即生效，脈衝效果不再執行。

## 相關元件

- `EmptyState`：頁面為空、拖放是主要行動呼籲時使用。
- `Input` 與 `Textarea`：用於非檔案類的值。
- `SignaturePad`：用於手繪簽名而非附加檔案的情境。
