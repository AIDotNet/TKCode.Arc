---
title: 步驟條
summary: 顯示使用者在多步驟流程中的位置，以及已完成的步驟。
source-hash: 4431b755866c
---

## 何時使用

- 嚴格的多步驟流程，例如結帳或帳號設定，使用者每次只能前進一步。
- 垂直時間軸形式的步驟，每一步都有描述，失敗的步驟可顯示錯誤狀態。

## 何時不用

- 已完成工作的單一百分比：用 `Progress`。
- 沒有順序或使用者可以自由跳轉的步驟：用 `Tabs`。
- 步驟的內容：`Stepper` 只顯示位置。請自行轉譯步驟內容，並驅動 `CurrentIndex`。

## 用法

為 `Stepper` 提供步驟，以及進行中步驟的索引。索引等於步驟數時，表示所有步驟皆已完成。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Stepper CurrentIndex="1">
        <arc:Stepper.Steps>
            <arc:StepperStep Label="帳號" Description="建立登入資訊" />
            <arc:StepperStep Label="配送" Description="寄送到哪裡" />
            <arc:StepperStep Label="付款" Description="信用卡或發票" />
            <arc:StepperStep Label="確認" Description="檢查並確認" />
        </arc:Stepper.Steps>
    </arc:Stepper>
</UserControl>
```

### 繫結步驟

`ItemsSource` 優先於 `Steps`。繫結一組 `StepperStep` 物件的集合，並將 `CurrentIndex` 保存在檢視模型中。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}" />
```

### 可導覽的步驟

將 `IsNavigable` 設為 `True`，即可讓使用者透過點擊，或使用方向鍵與 Enter 返回已完成的步驟。處理 `StepSelected` 以做出回應。若該事件未被處理，所選步驟將成為目前步驟。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}"
             IsNavigable="True" />
```

```csharp
stepper.StepSelected += (_, e) =>
{
    // e.Index 是被選取的已完成步驟；e.Step 是它的 StepperStep。
    SaveDraft();
};
```

### 垂直方向與錯誤

將 `Orientation` 設為 `Vertical` 即可得到時間軸。在某個步驟上設定 `Error`，可將其標記變為警示圖示；錯誤訊息會取代該步驟的描述。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             Orientation="Vertical"
             Width="320" />
```

```csharp
steps[2].Error = "信用卡遭拒，請嘗試其他信用卡。";
steps[2].Error = null; // 清除錯誤
```

### 精簡與目前詳情

`IsCompact` 只顯示標記，並在下方標示目前步驟的名稱。寬度小於 480 px 的水平步驟條會自動切換到此模式。將 `Details` 設為 `Current` 時，只顯示目前步驟的描述。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             IsCompact="True" />
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="1"
             Details="Current" />
```

## API 參考

<!-- api: Stepper -->

<!-- api: StepperStep -->

## 無障礙

- `Label` 為步驟條命名。每個步驟都會以其狀態命名：已完成、未開始或錯誤。
- 目前步驟會被標示為目前步驟。
- 步驟變化會以禮貌方式播報，例如「第 2 步，共 4 步：配送」。所有步驟完成後，播報 `CompleteLabel`。
- 開啟 `IsNavigable` 後，已完成的步驟可取得焦點，並可用 Enter 或 Space 選取。方向鍵在可到達的步驟之間移動，Home 與 End 分別前往第一步或目前步驟。
- 尚未到達的步驟無法取得焦點，Tab 鍵會略過它們。

## 動態效果

- 進度跨越多個步驟時，連接線會依序填滿，新的目前標記會出現一圈逐漸擴大的光暈。
- 步驟完成時，數字會變為繪製的勾號；失敗時，變為警示標記。
- 變化的描述會從柔和的模糊中升起，它占用的空間以彈簧效果調整到新高度。
- 減少動態效果時，所有變化立即生效。

## 相關元件

- `Progress`：工作以百分比而非步驟計量時使用。
- `Tabs`：使用者可以任意順序切換分區時使用。
- `Skeleton`：某個步驟的內容仍在載入時使用。
