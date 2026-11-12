---
title: Avatar upload
summary: 拖入或選擇一張照片，在圓形下移動和縮放它，上傳時圓環圍繞照片逐漸填滿，失敗時可以重試。
source-hash: 4248f03e2fd5
---

<Demo name="Preview" />

## 何時使用

`AvatarUpload` 是用於個人或團隊頭像的欄位。圓形中顯示目前的頭像（沒有頭像時是一個邀請拖入的虛線圓），旁邊是欄位標題、一行關於可接受檔案的說明，以及上傳、更換或移除按鈕。新照片會先進入取景步驟：照片位於一個透明的圓形下，使用者拖動照片調整位置，用滾輪、雙指縮放、滑桿或鍵盤縮放；照片始終蓋滿圓形。儲存時剪出邊長為 `OutputSize` 像素的正方形。設定了上傳處理常式時，新頭像會立即顯示，上傳時圓環圍繞它逐漸填滿，進行中可以取消，失敗後可以重試。

- **個人設定**：使用者自己的頭像。
- **工作區、團隊或專案設定**：標誌或團隊合照。
- **新使用者引導**：一個可以略過、以後再做的頭像步驟。

## 何時不用

- **多個檔案或任意類型的檔案**：用 `FileDropzone`，它會列出檔案、檢查類型並上傳。
- **只顯示頭像**：用 `Avatar`。
- **封面圖或橫幅**：圓形裁切不適合寬圖；用與圖片形狀相同的欄位來取景。

## 用法

把 `Source` 繫結到目前的頭像，並設定 `UploadHandler` 把取景後的照片傳送到伺服器。

```xml
<arc:AvatarUpload x:Name="Photo" Label="Profile photo" DisplayName="Mina Okafor" />
```

```csharp
Photo.Source = currentAvatar;
Photo.UploadHandler = async (request, progress, cancel) =>
{
    // request.Bytes is the square picture as PNG or JPEG (request.ContentType), OutputSize pixels on a side;
    // request.Image is the same picture as a bitmap, and request.SourceRect the part of the original it was cut from.
    await api.UploadAvatarAsync(request.OpenStream(), request.ContentType, progress, cancel);
};
```

`AvatarUpload` 屬於 `TKCode.Arc` 套件：`UseArcControls()` 會註冊它。

照片有四種來源：拖到欄位上的檔案、檔案選擇器（圓形或上傳按鈕）、程式碼中的 `Edit(bitmap)`，以及用 `LoadAsync(file)`、`LoadAsync(stream, name)` 或 `LoadAsync(bytes, name)` 傳入的檔案、串流或位元組（例如貼上的圖片、相機拍攝）。檔案在解碼之前先被檢查：先依 `MaxFileSize`（10 MB）檢查大小（即使串流說不出自己的長度，讀取也會在上限處停下），再依檔案開頭的位元組檢查類型，所以改名為 `.png` 的 PDF 會被拒絕，名稱古怪的照片則不會。預設類型是 PNG、JPEG、WebP、GIF 和 BMP；`Accept` 可以收窄，寫法與 HTML 檔案輸入相同：MIME 類型、`image/*` 或副檔名。被拒絕的檔案會在標題下方顯示原因，並觸發 `FileRejected`。較大的照片在取景前會把長邊縮小到 2048 像素。上傳前會先觸發 `Cropped`，帶有取景後的照片；沒有 `UploadHandler` 時照片會立即套用。

`DisplayName` 提供沒有照片時圓形裡顯示的字首縮寫。`AllowRemove="False"` 會隱藏「移除」按鈕（`Remove()` 方法仍然可用）。

### 範例照片

設定 `Source` 給欄位一張目前的照片，用 `Edit(bitmap)` 對應用程式已有的照片取景。下面的三個人是虛構的，用程式碼繪製。

<Demo name="SamplePhotos" />

### 被拒絕的檔案與失敗的上傳

被拒絕的檔案不會進入取景步驟：原因會用危險色取代標題下方的那一行，並被朗讀出來。上傳失敗時會保留帶紅色邊緣的新照片，原樣顯示處理常式拋出的訊息，並提供「重試」。請拋出一個說明發生了什麼、什麼沒有改變的例外。

<Demo name="Failures" />

| 發生了什麼 | 顯示 | 事件 |
|---|---|---|
| 檔案大於 `MaxFileSize` | 「holiday.png is larger than 5 MB」 | `FileRejected`（`Size`） |
| 類型不被接受 | 「statement.pdf is not a PNG, JPG, WebP or GIF image」 | `FileRejected`（`Type`） |
| 檔案不是平台能解碼的圖片 | 「broken.png could not be read」 | `FileRejected`（`Unreadable`） |
| 上傳處理常式拋出例外 | 例外的訊息，以及重試和取消 | 無：`State` 變為 `Failed` |

### 儲存的照片

`Cropped` 和 `UploadHandler` 會收到一個 `AvatarUploadRequest`。`Bytes` 是依 `ContentType` 編碼的正方形照片，`OpenStream()` 會回傳它的串流。`OutputFormat` 選擇格式：`Auto`（預設）把 PNG 和類型未知的照片（呼叫 `Edit` 時沒有檔名）儲存為 PNG，其他照片儲存為 JPEG；`Png` 和 `Jpeg` 強制使用其一。JPEG 依 `JpegQuality`（92）儲存，因為沒有透明度，所以鋪在白色上。`SuggestedFileName` 是原檔名加上對應的副檔名。

### 狀態

`State` 表示欄位正在做什麼：`Empty`、`Editing`（取景）、`Uploading`、`Done` 或 `Failed`。不使用處理常式、而是在 `Cropped` 中自己上傳的應用程式，可以用 `ReportProgress`、`CompleteUpload` 和 `FailUpload` 驅動圓環。

<Demo name="States" />

| 狀態 | 欄位顯示 |
|---|---|
| `Empty` | 帶人像圖示的虛線圓，以及「Upload photo」 |
| `Editing` | 取景區域、縮放列、取消和儲存 |
| `Uploading` | 新頭像上覆蓋一層淺色遮罩，圓環逐漸填滿，以及「Cancel upload」 |
| `Done` | 頭像（指標經過時上面顯示相機圖示）、更換和移除 |
| `Failed` | 帶紅色邊緣的新頭像、原因、重試和取消 |

取消上傳，或在失敗後取消，都會回到之前的頭像。

### 取景

取景區域是 256px 的正方形，圓形距離邊緣 20px。縮放範圍從照片剛好蓋滿圓形的大小，到它的 `MaxZoom`（4）倍。在取景區域按 Enter 儲存；在取景步驟的任何位置按 Escape 都會取消。

### 結構

1. **圓形**（`Size`，96px）：頭像或空狀態（`DisplayName` 的字首縮寫，沒有則是人像圖示）；可以拖入檔案，也是開啟檔案選擇器的按鈕。
2. **進度圓環**：3px，位於圓形外 5px，使用強調色，上傳時顯示。
3. **標題**（`Label`）和**狀態**（`Description`、進度，或用危險色顯示的問題）。
4. **按鈕**：依狀態顯示「Upload photo」或「Change」、「Remove」、「Retry」、「Cancel upload」。
5. **取景步驟**：邊緣變暗、中間是透明圓形的區域，縮小、縮放滑桿、放大、取消和儲存。

### 文案建議

| 不要寫 | 改為 |
|---|---|
| Choose a file to upload as your avatar image | Upload photo |
| Error: upload failed (500) | The connection dropped. Try again. |
| Supported formats: image/png, image/jpeg | PNG, JPG or WebP, up to 10 MB |

## API 參考

<!-- api: AvatarUpload -->

<!-- api: AvatarUploadRequest -->

<!-- api: AvatarUploadCroppedEventArgs -->

<!-- api: AvatarUploadRejectedEventArgs -->

<!-- api: AvatarUploadLabels -->

| 型別 | 值 |
|---|---|
| `AvatarUploadState` | `Empty`、`Editing`、`Uploading`、`Done`、`Failed` |
| `AvatarUploadRejection` | `Type`、`Size`、`Unreadable` |
| `AvatarImageFormat` | `Auto`、`Png`、`Jpeg` |

## 無障礙

- 按鈕是真正的按鈕，順序固定；圓形也可以點擊，但用鍵盤選擇檔案時使用上傳或更換按鈕。
- 一個禮貌型即時區域會朗讀結果（照片已更新、照片已移除）和依四分之一遞進的進度（「Uploading 50%」）；被拒絕的檔案或失敗的上傳會連同原因一起朗讀。
- 取景區域是一個可以取得焦點的元素，名為「Photo position」，說明文字列出了它的按鍵；開始取景時焦點移到這裡，結束後回到選擇按鈕。
- 失敗除了紅色邊緣，還會顯示原因。

### 鍵盤

| 按鍵 | 位置 | 作用 |
|---|---|---|
| `Enter` / `Space` | 按鈕 | 按下有焦點的按鈕 |
| 方向鍵 | 取景區域 | 移動照片 8px（按住 `Shift` 為 32px） |
| `+` / `-` | 取景區域 | 放大或縮小一級 |
| `0` | 取景區域 | 回到蓋滿圓形的大小並置中 |
| `Enter` | 取景區域 | 儲存 |
| `Escape` | 取景步驟 | 取消 |

## 動效

- 指標經過時，頭像上的遮罩和相機圖示在 160ms 內淡入；拖著檔案經過時，圓形的邊緣和填色會漸變。
- 進度圓環會平滑地接近每個回報的數值，而不是跳過去。
- 縮放按鈕和按鍵以靈敏的彈簧動畫縮放；拖動、滾輪和滑桿直接跟隨輸入。
- 減少動態效果時遮罩在 120ms 內淡入，邊緣立即變化，圓環直接跳到每個數值，縮放立即完成；關閉動態效果時一切立即完成。

## 響應式

- 圓形保持 `Size` 大小；空間較窄時，旁邊的標題、狀態和按鈕會換行。
- 取景步驟寬 256px；把欄位放在放得下的地方，小螢幕上可以放進對話框。

## 相關元件

- `Avatar`：顯示頭像。
- `FileDropzone`：任意類型、一次多個檔案。

| 元件 | 適用情境 |
|---|---|
| `AvatarUpload` | 一張在圓形中取景的照片 |
| `FileDropzone` | 多個任意類型的檔案，並列出進度 |
| `Avatar` | 只顯示頭像 |
