---
title: File upload
summary: 完整的檔案選取流程，具備類型與大小限制、上傳進度與錯誤回饋。
source-hash: f88aadb60fd9
---

<Demo name="Preview" />

## 何時使用

當使用者需要看到上傳進度、並能從無效檔案中復原時，使用 `FileUpload` 上傳文件或媒體。

- **逐一檔案顯示進度的上傳**，可以重試與移除。
- **上傳前必須依類型與大小驗證的附件**。
- **移除檔案即停止上傳**：移除檔案會取消其上傳的權杖。

## 何時不用

- **只選取檔案、不需要上傳狀態**：使用 `FileDropzone`。
- **上傳後還需要對應欄位的資料**：使用專門的匯入流程。

## 用法

```xml
<arc:FileUpload x:Name="Attachments" Accept="image/*,.pdf" MaxSize="10485760" />
```

```csharp
Attachments.Uploader = async (item, progress, token) =>
{
    await using var stream = await item.File!.OpenReadAsync();
    await UploadWithProgressAsync(item.Name, stream, progress, token);   // report 0-100; throw to mark the file failed
};
```

每個被接受的檔案在加入後立即透過 `Uploader` 上傳：以 progress 回報 0 到 100，上傳完成後返回，擲出例外則把檔案標為失敗。檔案被移除、被新檔案取代（`Multiple="False"`）或控制項離開視覺樹時，權杖會被取消。沒有上傳函式時，檔案只會驗證並列出，被接受的檔案顯示一個勾號。

### 類型與大小

<Demo name="Validation" />

`Accept` 接受以逗號分隔的副檔名（`.pdf`）、MIME 類型（`application/zip`）與萬用字元（`image/*`）。Avalonia 的檔案不帶 MIME 類型，因此控制項會依副檔名在內建的常見類型表中查詢；未知的副檔名只能依副檔名比對。`MaxSize` 是允許的最大位元組數。被拒絕的檔案會留在清單中並顯示錯誤（"This file type is not accepted." 或 "File is larger than 1 MB."），不會上傳。檔案選取器同樣依 `Accept` 篩選。

```xml
<arc:FileUpload Accept=".csv,text/plain" MaxSize="1048576"
                Label="Import contacts" Description="CSV or plain text, up to 1 MB." />
```

### 單一檔案

<Demo name="SingleFile" />

設定 `Multiple="False"` 時，選取器只取一個檔案，新檔案會取代整個清單並取消進行中的上傳。

```xml
<arc:FileUpload Multiple="False" Accept="image/*" Label="Upload a cover image" />
```

### 停用

<Demo name="Disabled" />

`IsEnabled="False"` 會讓拖放區變暗，不再開啟選取器，也不接受拖放。

### 檔案清單

`Files` 是檔案清單（最新的在最後），元素為 `FileUploadItem`。它是雙向繫結：新增或移除檔案時控制項會取代它，並以新清單引發 `FilesChanged`。每一項有 `File`（`IStorageFile`）、`Name`、`Size`、`Error`，以及上傳的 `Status`（`None`、`Uploading`、`Done`、`Failed`）與 `Progress`。

```csharp
upload.FilesChanged += (_, e) => SaveButton.IsEnabled = e.Files.All(f => f.Status == FileUploadStatus.Done);
```

`BrowseAsync()` 開啟選取器，`AddFilesAsync(storageItems)` 從你自己的來源（貼上、另一個拖放目標）新增檔案，`AddFiles(items)` 新增你以名稱與大小建立的項目。`Retry(item)` 與 `Remove(item)` 的作用與列上的按鈕相同。

### 文字

`Label`（"Upload files"）、`Description`（"Drop files here or browse from your device."）與 `BrowseLabel`（"Browse"）都是屬性。檔案停在拖放區上方時，標籤會變為 "Drop to add files"（或 "Drop to add a file"）。狀態列的內容例如 "2 files added. 1 file needs attention."、"report.pdf uploaded."、"report.pdf could not be uploaded." 與 "report.pdf removed."；`Status` 保存最後一則。

## API 參考

<!-- api: FileUpload -->

<!-- api: FileUploadItem -->

<!-- api: FileUploadStatus -->

<!-- api: FileUploadFilesChangedEventArgs -->

## 無障礙

- 拖放區是一個按鈕，名稱取自 `Label`，說明取自 `Description`。
- 禮貌模式的即時區域會播報新增、拒絕、上傳完成、上傳失敗與移除的檔案。
- 重試與移除按鈕的名稱帶有檔名（"Retry report.pdf"、"Remove report.pdf"）；勾號的名稱是 "Ready" 或 "Uploaded"。
- 移除檔案後，焦點移到下一列的移除按鈕，沒有則移到上一列的，再沒有則回到拖放區。
- 鍵盤焦點環位於拖放區與按鈕外側 2 px。懸停顏色只對滑鼠與觸控筆生效。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| 拖放區上的 `Enter` / `Space` | 開啟檔案選取器。 |
| `Tab` / `Shift+Tab` | 在拖放區與每一列的重試、移除按鈕之間移動。 |

## 動態效果

- 檔案停在拖放區上方時，邊框與填色變為強調色，雲朵圖示上移 3 px，標籤切換為 "Drop to add files"。
- 列的高度以彈簧展開與收合，因此移除檔案後清單會合攏空隙。狀態列以同樣方式展開，每則新訊息上升淡入。
- 進度條與計數百分比由同一個彈簧驅動。計數達到 100% 時階段變為 "Uploaded"，進度條摺疊消失，勾號彈出並描繪出來。
- 上傳失敗時階段切換為 "Upload failed"，並彈出重試按鈕。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 列與狀態列的高度 | 平滑彈簧，不透明度 240 ms 進入 | 立即出現，120 ms 淡出 | 立即 |
| 標籤、階段與狀態的切換 | 上升並模糊，進入 240 ms，離開 160 ms | 120 ms 淡出 | 立即 |
| 進度 | 平滑彈簧 | 直接跳到 | 直接跳到 |
| 勾號與重試 | 以靈敏彈簧彈出，勾號 240 ms 描繪 | 立即出現 | 立即 |
| 拖放區顏色與雲朵圖示 | 240 ms；雲朵圖示使用 CSS 彈簧 | 立即 | 立即 |

### 響應式行為

拖放區與清單填滿容器，檔名單行顯示，超出部分以刪節號截斷。在觸控裝置上，點按拖放區會開啟平台的檔案選取器。桌面宿主與瀏覽器宿主都支援從作業系統拖入檔案（Avalonia 12.1 會把瀏覽器中的拖放轉換為檔案）。

### 效能

上傳透過你的 `Uploader` 進行，每個被接受的檔案呼叫一次，同時進行的上傳數量沒有限制：大量檔案請自行排隊。清單沒有虛擬化，檔案數量應保持在合理範圍內。

## 相關元件

- `FileDropzone`：只選取檔案、不需要上傳狀態。
- `Progress`：顯示單一已知工作的完成程度。
